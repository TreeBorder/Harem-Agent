using Harem.Contracts.Domain.Entities;
using Harem.Services.Abstractions.Agents;
using Harem.Services.Abstractions.Tools;
using Microsoft.Agents.AI;

namespace Harem.Services.Tools;

/// <summary>
/// 定时任务工具实现
/// </summary>
public class CronTool : ICronTool
{
    private readonly ICronService _cronService;
    private readonly ISessionService _sessionService;

    public CronTool(ICronService cronService, ISessionService sessionService)
    {
        _cronService = cronService;
        _sessionService = sessionService;
    }

    public async Task<string> CreateCronJob(string name, string cronExpression, string target, string content)
    {
        var (sessionKey, channelId, error) = await GetCurrentSessionInfoAsync();
        if (error != null) return error;

        if (!Enum.TryParse<CronJobTarget>(target, true, out var targetEnum))
            return $"无效的目标类型 '{target}'。可选: SystemToAgent, MessageToUser, MessageToAgent";

        try
        {
            var config = new CronJobConfig
            {
                Name = name,
                CronExpression = cronExpression,
                IsOneShot = false,
                Target = targetEnum,
                SessionKey = sessionKey!,
                ChannelId = channelId!,
                Content = content
            };

            var created = await _cronService.CreateJobAsync(config);
            return $"循环定时任务创建成功: ID={created.Id}, 名称={created.Name}, Cron={created.CronExpression}, 目标={created.Target}";
        }
        catch (Exception ex)
        {
            return $"创建定时任务失败: {ex.Message}";
        }
    }

    public async Task<string> CreateOneShotJob(string name, string fireAt, string target, string content)
    {
        var (sessionKey, channelId, error) = await GetCurrentSessionInfoAsync();
        if (error != null) return error;

        if (!Enum.TryParse<CronJobTarget>(target, true, out var targetEnum))
            return $"无效的目标类型 '{target}'。可选: SystemToAgent, MessageToUser, MessageToAgent";

        if (!DateTimeOffset.TryParse(fireAt, out var fireAtOffset))
            return $"无效的时间格式 '{fireAt}'，请使用 yyyy-MM-dd HH:mm:ss 格式";

        try
        {
            var config = new CronJobConfig
            {
                Name = name,
                IsOneShot = true,
                FireAt = fireAtOffset,
                Target = targetEnum,
                SessionKey = sessionKey!,
                ChannelId = channelId!,
                Content = content
            };

            var created = await _cronService.CreateJobAsync(config);
            return $"单次定时任务创建成功: ID={created.Id}, 名称={created.Name}, 触发时间={created.FireAt:yyyy-MM-dd HH:mm:ss}, 目标={created.Target}";
        }
        catch (Exception ex)
        {
            return $"创建定时任务失败: {ex.Message}";
        }
    }

    public async Task<string> ListCronJobs()
    {
        var (sessionKey, _, error) = await GetCurrentSessionInfoAsync();
        if (error != null) return error;

        var jobs = await _cronService.ListJobsAsync(sessionKey);
        if (jobs.Count == 0)
            return "当前会话没有定时任务";

        var lines = new List<string> { $"共 {jobs.Count} 个定时任务:" };
        foreach (var job in jobs)
        {
            var status = job.Enabled ? "✅" : "⏸️";
            var type = job.IsOneShot ? "单次" : "循环";
            var schedule = job.IsOneShot
                ? $"触发: {job.FireAt:yyyy-MM-dd HH:mm:ss}"
                : $"Cron: {job.CronExpression}";
            var executed = job.ExecutionCount > 0 ? $", 已执行{job.ExecutionCount}次" : "";
            lines.Add($"  {status} [{job.Id}] {job.Name} ({type}, {schedule}, {job.Target}{executed})");
        }

        return string.Join("\n", lines);
    }

    public async Task<string> DeleteCronJob(string jobId)
    {
        var success = await _cronService.DeleteJobAsync(jobId);
        return success ? $"定时任务 {jobId} 已删除" : $"未找到定时任务 {jobId}";
    }

    public async Task<string> ToggleCronJob(string jobId, bool enabled)
    {
        var success = await _cronService.ToggleJobAsync(jobId, enabled);
        if (!success) return $"未找到定时任务 {jobId}";
        return enabled ? $"定时任务 {jobId} 已启用" : $"定时任务 {jobId} 已禁用";
    }

    private async Task<(string? sessionKey, string? channelId, string? error)> GetCurrentSessionInfoAsync()
    {
        var session = AIAgent.CurrentRunContext?.Session;
        if (session == null)
            return (null, null, "获取上下文为空");

        var sessionKey = session.StateBag.GetValue<string>("sessionKey");
        if (string.IsNullOrEmpty(sessionKey))
            return (null, null, "获取会话密钥为空");

        var sessionInfo = await _sessionService.GetSessionAsync(sessionKey);
        if (sessionInfo == null)
            return (null, null, $"未找到会话 {sessionKey}");

        return (sessionKey, sessionInfo.Info.ChannelId, null);
    }
}
