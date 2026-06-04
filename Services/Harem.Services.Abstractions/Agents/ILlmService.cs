using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Harem.Services.Abstractions.Agents;

/// <summary>
/// LLM 调用服务（含 Fallback 和流式支持）
/// </summary>
public interface ILlmService
{
    /// <summary>
    /// 统一 LLM 调用入口，根据 enableStreaming 自动选择流式/非流式
    /// </summary>
    Task<LlmCallResult> CallAsync(
        AIAgent agent,
        AgentSession agentSession,
        List<ChatMessage> messages,
        string sessionKey,
        bool enableStreaming,
        CancellationToken ct);
}

/// <summary>
/// LLM 调用结果
/// </summary>
public record LlmCallResult(
    string FullText,
    string Content,
    string Summary,
    long InputTokens,
    long OutputTokens);
