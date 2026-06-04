using Harem.Contracts.Domain.Entities;
using Harem.Services.Abstractions.Agents;
using Harem.Services.Abstractions.Handlers;

namespace Harem.Services.Handlers;

public class SummaryHandler : ICommandHandler
{
    public const string CommandName = "summary";
    public string Command => CommandName;

    private readonly ISummaryService _summaryService;
    private readonly ISessionService _sessionService;

    public SummaryHandler(ISummaryService summaryService, ISessionService sessionService)
    {
        _summaryService = summaryService;
        _sessionService = sessionService;
    }

    public async Task<CommandResult> HandleAsync(string args, string sessionKey)
    {
        var subCmd = args.Trim().ToLowerInvariant();
        var session = await _sessionService.GetSessionAsync(sessionKey);

        switch (subCmd)
        {
            case "update":
                await _summaryService.GenerateIncrementalSummaryAsync(session.Info.SessionId);
                return new CommandResult
                {
                    CommandReply = "✅ 增量摘要已完成，新消息已纳入摘要。"
                };

            case "force":
                await _summaryService.ForceRegenerateSummaryAsync(session.Info.SessionId);
                return new CommandResult
                {
                    CommandReply = "✅ 摘要已强制重生成，所有历史消息已重新摘要。"
                };

            default:
                return new CommandResult
                {
                    CommandReply = "可用子命令：\n- `/summary update` — 增量摘要，将新消息纳入\n- `/summary force` — 强制重生成，忽略已有摘要全部重来"
                };
        }
    }
}
