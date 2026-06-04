using Harem.Contracts.Domain.Messages;
using Harem.Services.Abstractions.Agents;
using Harem.Services.Abstractions.Channels;
using Harem.Services.Abstractions.Handlers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Harem.Services.Agents;

/// <summary>
/// 预置指令分发服务。解析以 '/' 开头的消息，通过 KeyedService 查找 ICommandHandler 执行。
/// 全异步，不阻塞主流程。
/// </summary>
public class CommandDispatchService : ICommandDispatchService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IMessageChannel<ReplyMessage> _replyChannel;
    private readonly IAgentService _agentService;
    private readonly ILogger<CommandDispatchService> _logger;

    public CommandDispatchService(
        IServiceProvider serviceProvider,
        IMessageChannel<ReplyMessage> replyChannel,
        IAgentService agentService,
        ILogger<CommandDispatchService> logger)
    {
        _serviceProvider = serviceProvider;
        _replyChannel = replyChannel;
        _agentService = agentService;
        _logger = logger;
    }

    public async Task<CommandDispatchResult?> TryDispatchAsync(string message, string sessionKey, CancellationToken ct)
    {
        var trimmed = message.TrimStart();
        if (!trimmed.StartsWith('/'))
            return null;

        // 解析指令名和参数（支持前导空格绕过 Mattermost 斜杠命令）
        var spaceIdx = trimmed.IndexOf(' ');
        var cmd = spaceIdx > 0 ? trimmed[1..spaceIdx] : trimmed[1..];
        var args = spaceIdx > 0 ? trimmed[(spaceIdx + 1)..] : "";

        try
        {
            var handler = _serviceProvider.GetKeyedService<ICommandHandler>(cmd);

            // 未注册的指令
            if (handler == null)
            {
                await _replyChannel.WriteAsync(
                    new ReplyMessage(sessionKey, $"未知指令 `/{cmd}`，输入 `/help` 查看帮助"), ct);
                return new CommandDispatchResult { IsCommand = true, ShouldStop = true };
            }

            // 执行指令
            var result = await handler.HandleAsync(args, sessionKey);

            // 1. 直接回复（CommandReply）
            if (!string.IsNullOrEmpty(result.CommandReply))
                await _replyChannel.WriteAsync(new ReplyMessage(sessionKey, result.CommandReply), ct);

            // 2. 模型切换（SwitchModelKey）
            if (!string.IsNullOrEmpty(result.SwitchModelKey))
            {
                try
                {
                    _agentService.RebuildForModel(result.SwitchModelKey);
                    _logger.LogInformation("模型已切换到: {Model}", result.SwitchModelKey);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "切换模型失败: {Model}", result.SwitchModelKey);
                    await _replyChannel.WriteAsync(
                        new ReplyMessage(sessionKey, $"切换模型失败: {ex.Message}"), ct);
                    return new CommandDispatchResult { IsCommand = true, ShouldStop = true };
                }
            }

            // 3. 上下文注入消息（SystemMessage / UserMessage，如 /session reset）
            var injectedMessages = new List<ChatMessage>();
            if (!string.IsNullOrEmpty(result.SystemMessage))
                injectedMessages.Add(new ChatMessage(ChatRole.System, result.SystemMessage)
                    { CreatedAt = DateTimeOffset.UtcNow });
            if (!string.IsNullOrEmpty(result.UserMessage))
                injectedMessages.Add(new ChatMessage(ChatRole.User, result.UserMessage)
                    { CreatedAt = DateTimeOffset.UtcNow });

            return new CommandDispatchResult
            {
                IsCommand = true,
                ShouldStop = injectedMessages.Count == 0, // 有注入消息则继续走 LLM
                InjectedMessages = injectedMessages
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理指令时出错: {Command}", cmd);
            return new CommandDispatchResult { IsCommand = true, ShouldStop = true };
        }
    }
}
