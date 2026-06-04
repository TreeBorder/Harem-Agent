using Microsoft.Extensions.AI;

namespace Harem.Services.Abstractions.Handlers;

/// <summary>
/// 指令分发结果
/// </summary>
public class CommandDispatchResult
{
    /// <summary>
    /// 是否为指令消息（以 '/' 开头）
    /// </summary>
    public bool IsCommand { get; init; }

    /// <summary>
    /// 是否应停止处理（不走 LLM）。为 false 时 InjectedMessages 非空，需注入上下文后继续
    /// </summary>
    public bool ShouldStop { get; init; }

    /// <summary>
    /// 指令处理器注入的上下文消息（SystemMessage / UserMessage），需在调用 LLM 前追加
    /// </summary>
    public List<ChatMessage> InjectedMessages { get; init; } = [];
}

/// <summary>
/// 预置指令分发服务。解析以 '/' 开头的消息，通过 KeyedService 查找 ICommandHandler 执行。
/// </summary>
public interface ICommandDispatchService
{
    /// <summary>
    /// 尝试分发指令。
    /// 非指令消息返回 null；指令消息返回完整分发结果，指示是否停止 / 注入消息。
    /// </summary>
    Task<CommandDispatchResult?> TryDispatchAsync(string message, string sessionKey, CancellationToken ct);
}
