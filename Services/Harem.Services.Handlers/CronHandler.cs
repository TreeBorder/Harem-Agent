using Harem.Contracts.Domain.Entities;
using Harem.Services.Abstractions.Agents;
using Harem.Services.Abstractions.Handlers;

namespace Harem.Services.Handlers;

/// <summary>
/// 定时任务用户命令处理器
/// </summary>
public class CronHandler : ICommandHandler
{
    public const string CommandName = "cron";
    public string Command => CommandName;

    private readonly ICronService _cronService;
    private readonly ISessionService _sessionService;

    public CronHandler(ICronService cronService, ISessionService sessionService)
    {
        _cronService = cronService;
        _sessionService = sessionService;
    }

    public async Task<CommandResult> HandleAsync(string args, string sessionKey)
    {
        var parts = args.Split(' ', 2);
        var subCmd = string.IsNullOrWhiteSpace(parts[0]) ? "list" : parts[0].ToLowerInvariant();
        var subArgs = parts.Length > 1 ? parts[1] : "";

        return subCmd switch
        {
            "add" => await HandleAddAsync(subArgs, sessionKey, isOneShot: false),
            "once" => await HandleAddAsync(subArgs, sessionKey, isOneShot: true),
            "list" => await HandleListAsync(sessionKey),
            "remove" => await HandleRemoveAsync(subArgs),
            "enable" => await HandleToggleAsync(subArgs, true),
            "disable" => await HandleToggleAsync(subArgs, false),
            "help" => HandleHelp(),
            _ => new CommandResult { CommandReply = $"未知子命令 `{subCmd}`。用法: `/cron [add|once|list|remove|enable|disable|help]`" }
        };
    }

    /// <summary>
    /// 创建定时任务（循环或单次）
    /// </summary>
    private async Task<CommandResult> HandleAddAsync(string args, string sessionKey, bool isOneShot)
    {
        // 解析参数: 名称 cron表达式/时间 内容
        var parts = args.Split(' ', 3);
        if (parts.Length < 3)
        {
            var usage = isOneShot
                ? "用法: `/cron once <名称> <触发时间 yyyy-MM-dd HH:mm:ss> <内容>`"
                : "用法: `/cron add <名称> <cron表达式> <内容>`";
            return new CommandResult { CommandReply = usage };
        }

        var name = parts[0];
        var schedule = parts[1];
        var content = parts[2];

        // 获取会话信息（需要 channelId）
        var sessionInfo = await _sessionService.GetSessionInfoAsync(sessionKey);
        if (sessionInfo == null)
            return new CommandResult { CommandReply = $"未找到会话 {sessionKey}" };

        try
        {
            CronJobConfig config;
            if (isOneShot)
            {
                if (!DateTimeOffset.TryParse(schedule, out var fireAt))
                    return new CommandResult { CommandReply = $"无效的时间格式 '{schedule}'，请使用 yyyy-MM-dd HH:mm:ss" };

                config = new CronJobConfig
                {
                    Name = name,
                    IsOneShot = true,
                    FireAt = fireAt,
                    Target = CronJobTarget.MessageToAgent,
                    SessionKey = sessionKey,
                    ChannelId = sessionInfo.ChannelId,
                    Content = content
                };
            }
            else
            {
                if (!_cronService.ValidateCronExpression(schedule))
                    return new CommandResult { CommandReply = $"无效的 cron 表达式: `{schedule}`" };

                config = new CronJobConfig
                {
                    Name = name,
                    CronExpression = schedule,
                    IsOneShot = false,
                    Target = CronJobTarget.MessageToAgent,
                    SessionKey = sessionKey,
                    ChannelId = sessionInfo.ChannelId,
                    Content = content
                };
            }

            var created = await _cronService.CreateJobAsync(config);
            var type = created.IsOneShot ? "单次" : "循环";
            var scheduleInfo = created.IsOneShot
                ? $"触发时间: {created.FireAt:yyyy-MM-dd HH:mm:ss}"
                : $"Cron: `{created.CronExpression}`";

            return new CommandResult
            {
                CommandReply = $"✅ {type}定时任务创建成功\n- ID: `{created.Id}`\n- 名称: {created.Name}\n- {scheduleInfo}\n- 目标: {created.Target}"
            };
        }
        catch (Exception ex)
        {
            return new CommandResult { CommandReply = $"❌ 创建定时任务失败: {ex.Message}" };
        }
    }

    private async Task<CommandResult> HandleListAsync(string sessionKey)
    {
        var jobs = await _cronService.ListJobsAsync(sessionKey);
        if (jobs.Count == 0)
            return new CommandResult { CommandReply = "当前会话没有定时任务。" };

        var lines = new List<string> { $"**定时任务列表** (共 {jobs.Count} 个)", "" };
        foreach (var job in jobs)
        {
            var status = job.Enabled ? "✅" : "⏸️";
            var type = job.IsOneShot ? "单次" : "循环";
            var schedule = job.IsOneShot
                ? $"触发: {job.FireAt:yyyy-MM-dd HH:mm:ss}"
                : $"Cron: `{job.CronExpression}`";
            var executed = job.ExecutionCount > 0 ? $" (已执行{job.ExecutionCount}次)" : "";
            lines.Add($"{status} **[{job.Id}]** {job.Name}");
            lines.Add($"   类型: {type} | {schedule} | 目标: {job.Target}{executed}");
        }

        return new CommandResult { CommandReply = string.Join("\n", lines) };
    }

    private async Task<CommandResult> HandleRemoveAsync(string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            return new CommandResult { CommandReply = "用法: `/cron remove <id>`" };

        var success = await _cronService.DeleteJobAsync(jobId.Trim());
        return success
            ? new CommandResult { CommandReply = $"✅ 定时任务 `{jobId}` 已删除" }
            : new CommandResult { CommandReply = $"❌ 未找到定时任务 `{jobId}`" };
    }

    private async Task<CommandResult> HandleToggleAsync(string jobId, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            var action = enabled ? "enable" : "disable";
            return new CommandResult { CommandReply = $"用法: `/cron {action} <id>`" };
        }

        var success = await _cronService.ToggleJobAsync(jobId.Trim(), enabled);
        if (!success)
            return new CommandResult { CommandReply = $"❌ 未找到定时任务 `{jobId}`" };

        var status = enabled ? "启用" : "禁用";
        return new CommandResult { CommandReply = $"✅ 定时任务 `{jobId}` 已{status}" };
    }

    private static CommandResult HandleHelp()
    {
        var help = """
            **定时任务命令帮助**
            
            `/cron add <名称> <cron表达式> <内容>` - 创建循环定时任务
            `/cron once <名称> <时间> <内容>` - 创建单次定时任务
            `/cron list` - 列出所有定时任务
            `/cron remove <id>` - 删除定时任务
            `/cron enable <id>` - 启用定时任务
            `/cron disable <id>` - 禁用定时任务
            `/cron help` - 显示此帮助
            
            **Cron 表达式示例:**
            `0 0 8 * * ?` - 每天 08:00
            `0 0 12 * * ?` - 每天 12:00
            `0 0 9 ? * MON` - 每周一 09:00
            `0 0/30 * * * ?` - 每 30 分钟
            `0 0 8,12,18 * * ?` - 每天 8/12/18 点
            
            **时间格式 (单次任务):**
            `2026-05-08 18:00:00`
            """;

        return new CommandResult { CommandReply = help };
    }
}
