using Harem.Contracts.Domain.Entities;
using Harem.Services.Abstractions.Agents;
using Harem.Services.Abstractions.Handlers;
using Microsoft.Extensions.Logging;

namespace Harem.Services.Handlers;

public class SessionHandler : ICommandHandler
{
    public const string CommandName = "session";
    public string Command => CommandName;

    private readonly ISessionService _sessionService;
    private readonly IHistoryService _historyService;
    private readonly IMemoryService _memoryService;
    private readonly ISummaryService _summaryService;
    private readonly ILogger<SessionHandler> _logger;

    public SessionHandler(
        ISessionService sessionService,
        IHistoryService historyService,
        IMemoryService memoryService,
        ISummaryService summaryService,
        ILogger<SessionHandler> logger)
    {
        _sessionService = sessionService;
        _historyService = historyService;
        _memoryService = memoryService;
        _summaryService = summaryService;
        _logger = logger;
    }

    public async Task<CommandResult> HandleAsync(string args, string sessionKey)
    {
        var parts = args.Split(' ', 2);
        var subCmd = string.IsNullOrWhiteSpace(parts[0]) ? "info" : parts[0].ToLowerInvariant();
        var subArgs = parts.Length > 1 ? parts[1] : "";

        return subCmd switch
        {
            "reset" => await HandleResetAsync(subArgs, sessionKey),
            "list" => await HandleListAsync(),
            "info" => await HandleInfoAsync(sessionKey),
            "systemsent" => await HandleSystemSentAsync(sessionKey),
            _ => new CommandResult { CommandReply = $"未知子命令 `{subCmd}`。用法: `/session [reset|list|info|systemsent]`" }
        };
    }

    /// <summary>
    /// 重置会话：先做一次最终摘要，导出到天记忆，保留摘要，清空对话记录
    /// </summary>
    private async Task<CommandResult> HandleResetAsync(string args, string sessionKey)
    {
        var session = await _sessionService.GetSessionAsync(sessionKey);

        // 1. 先做一次最终摘要，确保断点不丢
        await _summaryService.GenerateIncrementalSummaryAsync(session.Info.SessionId);

        // 2. 获取历史并导出到天记忆
        var history = await _historyService.GetHistoryAsync(session.Info.SessionId);
        try
        {
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            await _memoryService.ExportHistoryToDailyMemoryAsync(history, today);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "导出会话到天记忆失败");
        }

        // 3. 重置会话（生成新 SessionId）
        var newSession = await _sessionService.ResetSessionAsync(sessionKey);

        // 5. 新历史为空（摘要由后续会话从 summary.json 读取）
        var newHistory = new ChatHistory
        {
            Messages = []
        };
        await _historyService.SaveHistoryAsync(newSession.Info.SessionId, newHistory);

        return new CommandResult
        {
            SystemMessage = "会话已重置，历史记录已导出到天记忆，摘要已保留。基于摘要继续对话。",
            UserMessage = string.IsNullOrWhiteSpace(args) ? string.Empty : args,
            CommandReply = $"✅ 会话已重置 (ID: `{newSession.Info.SessionId[..8]}…`)"
        };
    }

    /// <summary>
    /// 列出所有会话
    /// </summary>
    private async Task<CommandResult> HandleListAsync()
    {
        var sessions = await _sessionService.GetAllSessionsAsync();
        if (sessions.Count == 0)
            return new CommandResult { CommandReply = "当前没有会话记录。" };

        var lines = new List<string> { "**📋 会话列表**", "" };
        lines.Add("| Key | SessionId | SystemSent | 更新时间 |");
        lines.Add("|-----|-----------|------------|----------|");
        foreach (var s in sessions)
        {
            var shortId = s.SessionId.Length > 8 ? $"{s.SessionId[..8]}…" : s.SessionId;
            var updateTime = DateTime.FromFileTimeUtc(s.UpdateAt).ToString("MM-dd HH:mm");
            var sysTag = s.SystemSent ? "✅" : "—";
            lines.Add($"| `{s.Key}` | {shortId} | {sysTag} | {updateTime} |");
        }

        return new CommandResult { CommandReply = string.Join("\n", lines) };
    }

    /// <summary>
    /// 查看当前会话详情
    /// </summary>
    private async Task<CommandResult> HandleInfoAsync(string sessionKey)
    {
        var session = await _sessionService.GetSessionAsync(sessionKey);
        var info = session.Info;
        var updateTime = DateTime.FromFileTimeUtc(info.UpdateAt).ToString("yyyy-MM-dd HH:mm:ss");

        var lines = new List<string>
        {
            $"**📝 会话详情**",
            $"",
            $"- **Key**: `{info.Key}`",
            $"- **SessionId**: `{info.SessionId}`",
            $"- **SystemSent**: {(info.SystemSent ? "✅ 开启" : "❌ 关闭")}",
            $"- **ModelKey**: {(string.IsNullOrEmpty(info.ModelKey) ? "默认" : $"`{info.ModelKey}`")}",
            $"- **更新时间**: {updateTime}",
            $"- **InputTokens**: {info.InputTokens}",
            $"- **OutputTokens**: {info.OutputTokens}",
        };

        return new CommandResult { CommandReply = string.Join("\n", lines) };
    }

    /// <summary>
    /// 切换当前会话的 SystemSent 状态（全局至多一个开启，互斥由 SessionService 保证）
    /// </summary>
    private async Task<CommandResult> HandleSystemSentAsync(string sessionKey)
    {
        var session = await _sessionService.GetSessionAsync(sessionKey);
        var allSessions = await _sessionService.GetAllSessionsAsync();

        // 当前已是开启状态 → 关闭
        if (session.Info.SystemSent)
        {
            // 唯一会话不允许关闭 SystemSent
            if (allSessions.Count <= 1)
                return new CommandResult { CommandReply = "⚠️ 当前仅有一个会话，SystemSent 不允许关闭" };

            session.Info.SystemSent = false;
            await _sessionService.UpdateSessionAsync(session);
            return new CommandResult { CommandReply = "✅ 当前会话 SystemSent 已关闭 ❌" };
        }

        // 开启：SessionService.UpdateSessionAsync 内部会自动关闭其他会话的 SystemSent
        session.Info.SystemSent = true;
        await _sessionService.UpdateSessionAsync(session);

        return new CommandResult { CommandReply = "✅ 当前会话 SystemSent 已开启 ✅" };
    }
}
