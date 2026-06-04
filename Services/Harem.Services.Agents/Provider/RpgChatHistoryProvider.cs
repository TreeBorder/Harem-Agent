using Harem.Services.Abstractions.Agents;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Harem.Services.Agents.Provider;

public sealed class RpgChatHistoryProvider : AIContextProvider
{
    private readonly IHistoryService _historyService;
    private readonly ISessionService _sessionService;


    public RpgChatHistoryProvider(IHistoryService historyService, ISessionService sessionService)
    {
        _historyService = historyService;
        _sessionService = sessionService;
    }

    protected override async ValueTask InvokedCoreAsync(InvokedContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.InvokeException is not null)
        {
            return;
        }

        var sessionKey = context.Session?.StateBag.GetValue<string>("sessionKey");
        if (string.IsNullOrEmpty(sessionKey))
        {
            return;
        }

        var session = await _sessionService.GetSessionInfoAsync(sessionKey);
        if (session == null)
        {
            return;
        }

        var responseMessages = context.ResponseMessages?.ToList();
        if (responseMessages == null || responseMessages.Count == 0) return;

        // 累积多轮工具调用的消息
        var existing =
            await _historyService.GetHistoryBag<List<ChatMessage>>(session.SessionId, Constants.BagKeys.CurrentResponse) ?? [];
        existing.AddRange(responseMessages);
        await _historyService.SetHistoryBag(session.SessionId, Constants.BagKeys.CurrentResponse, existing);
    }
}