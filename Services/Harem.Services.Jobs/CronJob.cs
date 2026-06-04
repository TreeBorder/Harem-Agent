using Harem.Contracts.Configurations.AgentWorkspace;
using Harem.Contracts.Domain.Entities;
using Harem.Contracts.Domain.Messages;
using Harem.Services.Abstractions.Agents;
using Harem.Services.Abstractions.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Harem.Services.Jobs;

/// <summary>
/// 定时任务 Quartz Job - 根据配置分发消息
/// </summary>
[DisallowConcurrentExecution]
public class CronJob : IJob
{
    private readonly ICronService _cronService;
    private readonly ISessionService _sessionService;
    private readonly IMessageChannel<ReplyMessage> _replyChannel;
    private readonly IMessageChannel<ReceivedMessage> _receiveChannel;
    private readonly IOptions<CronOptions> _cronOptions;
    private readonly ILogger<CronJob> _logger;

    public CronJob(
        ICronService cronService,
        ISessionService sessionService,
        IMessageChannel<ReplyMessage> replyChannel,
        IMessageChannel<ReceivedMessage> receiveChannel,
        IOptions<CronOptions> cronOptions,
        ILogger<CronJob> logger)
    {
        _cronService = cronService;
        _sessionService = sessionService;
        _replyChannel = replyChannel;
        _receiveChannel = receiveChannel;
        _cronOptions = cronOptions;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var dataMap = context.MergedJobDataMap;
        var jobId = dataMap.GetString("jobId")!;
        var jobName = dataMap.GetString("jobName")!;
        var targetStr = dataMap.GetString("target")!;
        var sessionKey = dataMap.GetString("sessionKey")!;
        var channelId = dataMap.GetString("channelId")!;
        var content = dataMap.GetString("content")!;
        var isOneShot = dataMap.GetBoolean("isOneShot");
        var delayCount = dataMap.GetInt("delayCount");

        var target = Enum.Parse<CronJobTarget>(targetStr);
        var options = _cronOptions.Value;

        try
        {
            // 1. 活跃会话检测
            var sessionInfo = await _sessionService.GetSessionInfoAsync(sessionKey);
            if (sessionInfo != null)
            {
                var updateTime = DateTime.FromFileTimeUtc(sessionInfo.UpdateAt);
                var sessionIdle = DateTime.UtcNow - updateTime;

                if (sessionIdle.TotalMinutes < options.ActiveSessionMinutes)
                {
                    // 检查延后次数
                    if (delayCount >= options.MaxDelayRetries)
                    {
                        var abandonMsg = $"定时任务 '{jobName}' 已延后 {delayCount} 次（共 {options.DelayIntervalMinutes * delayCount} 分钟），放弃执行";
                        _logger.LogWarning("定时任务放弃执行: {JobId} ({JobName}), 延后次数={DelayCount}", jobId, jobName, delayCount);

                        await _cronService.AppendLogAsync(jobId, abandonMsg);

                        var sessionId = sessionInfo.SessionId;
                        await _cronService.RecordExecutionResultAsync(sessionId,
                            $"[定时任务] {abandonMsg}");

                        if (isOneShot)
                            await _cronService.DeleteJobAsync(jobId);

                        return;
                    }

                    // 延后执行
                    delayCount++;
                    var delayMinutes = options.DelayIntervalMinutes;
                    var nextFire = DateTimeOffset.UtcNow.AddMinutes(delayMinutes);

                    await _cronService.RescheduleJobAsync(jobId, nextFire);

                    var jobConfig = await _cronService.GetJobAsync(jobId);
                    if (jobConfig != null)
                    {
                        jobConfig.DelayCount = delayCount;
                        await _cronService.UpdateJobAsync(jobConfig);
                    }

                    var delayMsg = $"检测到对话处于活跃状态（空闲 {sessionIdle.TotalMinutes:F0} 分钟），已延后 {delayMinutes} 分钟发送（第 {delayCount}/{options.MaxDelayRetries} 次）";
                    _logger.LogInformation("定时任务延后: {JobId} ({JobName}), {Message}", jobId, jobName, delayMsg);
                    await _cronService.AppendLogAsync(jobId, delayMsg);

                    return;
                }
            }

            // 2. 执行分发
            var cachedConfig = await _cronService.GetJobAsync(jobId);
            var hasCachedAudio = !string.IsNullOrEmpty(cachedConfig?.CachedAudioPath) &&
                                 File.Exists(cachedConfig.CachedAudioPath);

            if (hasCachedAudio && target == CronJobTarget.MessageToUser)
            {
                // 有缓存音频，直接发送文件
                await _replyChannel.WriteAsync(new ReplyMessage(sessionKey, "", [cachedConfig!.CachedAudioPath!]));
                _logger.LogInformation("定时任务发送缓存音频: {JobId} ({JobName}), Path={Path}", jobId, jobName, cachedConfig.CachedAudioPath);
            }
            else if (hasCachedAudio)
            {
                // 有缓存音频但目标是 Agent，发送音频 + 文本消息
                await _replyChannel.WriteAsync(new ReplyMessage(sessionKey, "", [cachedConfig!.CachedAudioPath!]));

                // 对于 SystemToAgent，仍然发送系统消息让 Agent 知道
                if (target == CronJobTarget.SystemToAgent)
                {
                    var sysMsg = $"[定时任务系统消息]定时语音消息已发送[/]";
                    await _receiveChannel.WriteAsync(new ReceivedMessage(sessionKey, channelId, sysMsg));
                }
            }
            else
            {
                // 无缓存，走正常流程
                switch (target)
                {
                    case CronJobTarget.SystemToAgent:
                        var sysMsg = $"[定时任务系统消息]{content}[/]";
                        await _receiveChannel.WriteAsync(new ReceivedMessage(sessionKey, channelId, sysMsg));
                        break;

                    case CronJobTarget.MessageToAgent:
                        await _receiveChannel.WriteAsync(new ReceivedMessage(sessionKey, channelId, content));
                        break;

                    case CronJobTarget.MessageToUser:
                        await _replyChannel.WriteAsync(new ReplyMessage(sessionKey, content));
                        break;
                }
            }

            // 3. 更新任务状态
            if (cachedConfig != null)
            {
                cachedConfig.LastFireTime = DateTimeOffset.UtcNow;
                cachedConfig.ExecutionCount++;
                cachedConfig.DelayCount = 0;
                await _cronService.UpdateJobAsync(cachedConfig);
            }

            // 4. 记录执行结果到上下文注入
            var execSessionInfo = await _sessionService.GetSessionInfoAsync(sessionKey);
            if (execSessionInfo != null)
            {
                var resultBrief = hasCachedAudio
                    ? "已发送缓存语音消息"
                    : target switch
                    {
                        CronJobTarget.SystemToAgent => "已向Agent发送系统消息",
                        CronJobTarget.MessageToAgent => "已向Agent发送消息",
                        CronJobTarget.MessageToUser => "已向用户发送消息",
                        _ => "已执行"
                    };
                await _cronService.RecordExecutionResultAsync(execSessionInfo.SessionId,
                    $"[定时任务] '{jobName}' 执行成功: {resultBrief}");
            }

            // 5. 写日志
            var logMsg = hasCachedAudio
                ? $"执行成功, Target={target}, 使用缓存音频={cachedConfig!.CachedAudioPath}"
                : $"执行成功, Target={target}";
            await _cronService.AppendLogAsync(jobId, logMsg);

            // 6. 单次任务执行后删除
            if (isOneShot)
            {
                await _cronService.DeleteJobAsync(jobId);
                _logger.LogInformation("单次定时任务已执行并删除: {JobId} ({JobName})", jobId, jobName);
            }
            else
            {
                _logger.LogInformation("定时任务已执行: {JobId} ({JobName}), Target={Target}", jobId, jobName, target);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "定时任务执行失败: {JobId} ({JobName})", jobId, jobName);
            await _cronService.AppendLogAsync(jobId, $"执行失败: {ex.Message}");
        }
    }
}
