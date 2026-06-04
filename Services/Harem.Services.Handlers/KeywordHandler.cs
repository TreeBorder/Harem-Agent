using Harem.Contracts.Domain.Entities;
using Harem.Services.Abstractions.Agents;
using Harem.Services.Abstractions.Handlers;

namespace Harem.Services.Handlers;

public class KeywordHandler : ICommandHandler
{
    public const string CommandName = "keyword";
    public string Command => CommandName;

    private readonly IKeywordService _keywordService;

    public KeywordHandler(IKeywordService keywordService)
    {
        _keywordService = keywordService;
    }

    public Task<CommandResult> HandleAsync(string args, string sessionKey)
    {
        var trimmed = args?.Trim() ?? "";

        // 手动关键词提取（丢后台，避免阻塞）
        if (trimmed.StartsWith("extract ", StringComparison.OrdinalIgnoreCase))
        {
            var date = trimmed["extract ".Length..].Trim();
            if (string.IsNullOrWhiteSpace(date))
                return Task.FromResult(new CommandResult { CommandReply = "请指定日期，例如: /keyword extract 2026-05-18" });

            // 后台跑，耗时无所谓
            _ = Task.Run(() => _keywordService.ExtractKeywordsAsync(date));

            return Task.FromResult(new CommandResult { CommandReply = $"🔄 已派 Clio 去提取 {date} 的关键词，稍后使用 `/keyword` 查看结果" });
        }

        return _keywordService.GetKeywordIndexAsync()
            .ContinueWith(t => new CommandResult { CommandReply = t.Result });
    }
}
