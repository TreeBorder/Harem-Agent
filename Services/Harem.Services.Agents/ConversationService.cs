using Harem.Contracts.Domain.Entities;
using Harem.Contracts.Domain.Messages;
using Harem.Services.Abstractions.Agents;
using Harem.Services.Abstractions.Channels;
using Harem.Services.Abstractions.Handlers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Harem.Services.Agents;

public class ConversationService : IConversationService
{
    private readonly ILogger<ConversationService> _logger;
    private readonly IAgentService _agentService;
    private readonly ILlmService _llmService;
    private readonly IResponseService _responseService;
    private readonly IHistoryService _historyService;
    private readonly ISessionService _sessionService;
    private readonly IContextService _contextService;
    private readonly ISummaryService _summaryService;
    private readonly IMessageChannel<ReplyMessage> _replyChannel;
    private readonly ICommandDispatchService _commandDispatchService;

    public ConversationService(
        ILogger<ConversationService> logger,
        IAgentService agentService,
        ILlmService llmService,
        IResponseService responseService,
        IHistoryService historyService,
        ISessionService sessionService,
        IContextService contextService,
        ISummaryService summaryService,
        IMessageChannel<ReplyMessage> replyChannel,
        ICommandDispatchService commandDispatchService)
    {
        _logger = logger;
        _agentService = agentService;
        _llmService = llmService;
        _responseService = responseService;
        _historyService = historyService;
        _sessionService = sessionService;
        _contextService = contextService;
        _summaryService = summaryService;
        _replyChannel = replyChannel;
        _commandDispatchService = commandDispatchService;
    }

    public async Task ProcessMessageAsync(ReceivedMessage message, CancellationToken ct = default)
    {
        var trimmed = message.Message.TrimStart();

        // 空消息不处理
        if (string.IsNullOrWhiteSpace(trimmed)) return;

        // 1. 预置指令分发（/help, /session, /memory 等）
        var dispatchResult = await _commandDispatchService.TryDispatchAsync(
            message.Message, message.SessionKey, ct);
        if (dispatchResult is { IsCommand: true, ShouldStop: true })
            return; // 指令已完全处理，不走 LLM

        var agent = _agentService.GetOrCreateAgent();

        // 获取或创建会话
        var session = await _sessionService.GetSessionAsync(message.SessionKey);
        if (string.IsNullOrEmpty(session.Info.ChannelId) || !session.Info.ChannelId.Equals(message.ChannelId))
        {
            session.Info.ChannelId = message.ChannelId;
            await _sessionService.UpdateSessionAsync(session);
        }

        // 收到消息即开 typing Post
        await _replyChannel.WriteAsync(new ReplyMessage(session.Info.Key, "", IsTyping: true), ct);

        var agentSession = await agent.CreateSessionAsync(ct);
        agentSession.StateBag.SetValue("sessionKey", message.SessionKey);

        // 清除上一轮可能残留的临时响应数据
        await _historyService.SetHistoryBag<List<ChatMessage>>(session.Info.SessionId,
            Constants.BagKeys.CurrentResponse, []);

        var history = await _historyService.GetHistoryAsync(session.Info.SessionId);

        var rawMessage =
            await _historyService.GetHistoryBag<List<ChatRawMessage<ChatMessage>>>(session.Info.SessionId,
                Constants.BagKeys.Rounds);

        var (requestMessages, inputMessage) =
            await _contextService.BuildContextMessages(history, rawMessage ?? [], message.Message,
                session.Info.SessionId, message.SessionKey);
        // var (requestMessages, inputMessage) =
        //     await _contextService.BuildSingleSystemPromptContextMessages(history, rawMessage ?? [], message.Message,
        //         session.Info.SessionId, message.SessionKey);

        // 指令注入的上下文消息（如 /session reset），插入到用户输入之前
        if (dispatchResult?.InjectedMessages.Count > 0)
            requestMessages.InsertRange(requestMessages.Count - 1, dispatchResult.InjectedMessages);

        // 调用 LLM
        var enableStreaming = _agentService.IsStreamingEnabled();
        var llmResult = await _llmService.CallAsync(agent, agentSession, requestMessages, session.Info.Key,
            enableStreaming, ct);

        // 同步当前模型信息到 SessionInfo
        session.Info.ModelKey = _agentService.CurrentModelKey;
        session.Info.ContextTokens = _agentService.GetCurrentContextTokens();
        session.Info.InputTokens = llmResult.InputTokens;
        session.Info.OutputTokens = llmResult.OutputTokens;

        // 保存本轮交互
        var content = llmResult.Content;
        if (string.IsNullOrWhiteSpace(content))
            content = "[无回复]";
        await _responseService.SaveRoundAsync(session.Info.SessionId, inputMessage, content);

        // 30轮增量摘要触发
        _ = TriggerIncrementalSummaryAsync(session.Info.SessionId);

        // 保存 Agent 会话状态
        var serialized = await agent.SerializeSessionAsync(agentSession, cancellationToken: ct);
        session.Content = serialized;
        await _sessionService.UpdateSessionAsync(session);
    }

    /// <summary>
    /// 每30轮触发一次增量摘要（异步，不阻塞主流程）
    /// </summary>
    private async Task TriggerIncrementalSummaryAsync(string sessionId)
    {
        try
        {
            var rounds = await _historyService.GetHistoryBag<List<ChatRawMessage<ChatMessage>>>(
                sessionId, Constants.BagKeys.Rounds);
            var maxRoundId = rounds?.Max(r => r.RoundId) ?? 0;

            if (maxRoundId > 0 && maxRoundId % 30 == 0)
            {
                _ = _summaryService.GenerateIncrementalSummaryAsync(sessionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "触发增量摘要失败");
        }
    }
}