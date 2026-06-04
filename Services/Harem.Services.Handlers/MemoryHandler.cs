using Harem.Contracts.Domain.Entities;
using Harem.Services.Abstractions.Agents;
using Harem.Services.Abstractions.Handlers;

namespace Harem.Services.Handlers;

public class MemoryHandler : ICommandHandler
{
    public const string CommandName = "memory";
    public string Command => CommandName;

    private readonly IMemoryService _memoryService;
    private readonly ISessionService _sessionService;
    private readonly IHistoryService _historyService;

    public MemoryHandler(
        IMemoryService memoryService,
        ISessionService sessionService,
        IHistoryService historyService)
    {
        _memoryService = memoryService;
        _sessionService = sessionService;
        _historyService = historyService;
    }

    public async Task<CommandResult> HandleAsync(string args, string sessionKey)
    {
        var parts = args.Split(' ', 2);
        var subCmd = string.IsNullOrWhiteSpace(parts[0]) ? "search" : parts[0].ToLowerInvariant();
        var subArgs = parts.Length > 1 ? parts[1] : "";

        return subCmd switch
        {
            "search" => await HandleSearchAsync(subArgs),
            "index" => await HandleIndexAsync(),
            "get" => await HandleGetAsync(subArgs),
            "longterm" => await HandleLongTermAsync(),
            "reexport" => await HandleReexportAsync(sessionKey),
            _ => new CommandResult { CommandReply = $"未知子命令 `{subCmd}`。用法: `/memory [search|index|get|longterm|reexport]`" }
        };
    }

    private async Task<CommandResult> HandleSearchAsync(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return new CommandResult { CommandReply = "请输入搜索关键词。用法: `/memory search 关键词`" };

        var results = await _memoryService.SearchMemoryAsync([keyword]);
        if (results.Count == 0)
            return new CommandResult { CommandReply = $"没有找到与 `{keyword}` 相关的记忆。" };

        var lines = new List<string> { $"**记忆搜索: {keyword}**", "" };
        foreach (var r in results.Take(5))
        {
            var preview = r.Content.Length > 300 ? r.Content[..300] + "..." : r.Content;
            var location = r.Line > 0 ? $"L{r.Line}" : "";
            var sourceTag = string.IsNullOrEmpty(location) ? r.Source : $"{r.Source}:{location}";
            lines.Add($"- **[{sourceTag}]** (相似度: {r.Score:P0})");
            lines.Add($"  {preview}");
        }

        return new CommandResult { CommandReply = string.Join("\n", lines) };
    }

    private async Task<CommandResult> HandleIndexAsync()
    {
        await _memoryService.BuildMemoryIndexAsync();
        return new CommandResult { CommandReply = "✅ 记忆索引构建完成" };
    }

    private async Task<CommandResult> HandleGetAsync(string date)
    {
        if (string.IsNullOrWhiteSpace(date))
            date = DateTime.UtcNow.ToString("yyyy-MM-dd");

        var content = await _memoryService.GetDailyMemoryAsync(date);
        if (string.IsNullOrWhiteSpace(content))
            return new CommandResult { CommandReply = $"`{date}` 没有记忆记录。" };

        return new CommandResult { CommandReply = $"**{date} 记忆**\n\n{content}" };
    }

    private async Task<CommandResult> HandleLongTermAsync()
    {
        var content = await _memoryService.GetLongTermMemoryAsync();
        if (string.IsNullOrWhiteSpace(content))
            return new CommandResult { CommandReply = "长期记忆为空。" };

        return new CommandResult { CommandReply = $"**长期记忆**\n\n{content}" };
    }

    /// <summary>
    /// 从当前 session 的 history.json 重新按日期导出到天记忆（覆盖模式，修复重复问题）
    /// </summary>
    private async Task<CommandResult> HandleReexportAsync(string sessionKey)
    {
        var session = await _sessionService.GetSessionAsync(sessionKey);
        var history = await _historyService.GetHistoryAsync(session.Info.SessionId);

        if (history.Messages.Count == 0)
            return new CommandResult { CommandReply = "当前会话没有历史记录，无需导出。" };

        var dates = await _memoryService.RewriteHistoryByDateAsync(history);

        if (dates.Count == 0)
            return new CommandResult { CommandReply = "历史记录为空或格式异常，未导出任何内容。" };

        var dateList = string.Join("、", dates.Select(d => $"`{d}`"));
        return new CommandResult { CommandReply = $"✅ 已重新导出到天记忆（覆盖模式）: {dateList}" };
    }
}
