using Harem.Contracts.Domain.Entities;
using Microsoft.Extensions.AI;

namespace Harem.Services.Abstractions.Agents;

public interface IContextService
{
    /// <summary>
    /// 构建完整上下文：预处理用户消息 → 收集注入 → 系统提示 → 摘要 → 历史 → 新输入
    /// </summary>
    Task<(List<ChatMessage> Messages, ChatMessage InputMessage)> BuildContextMessages(
        ChatHistory history,
        List<ChatRawMessage<ChatMessage>> rawMessages,
        string userMessage,
        string sessionId,
        string sessionKey);

    /// <summary>
    /// 构建单条 system prompt 的上下文：AGENTS/SOUL/WORLD/MEMORY/USER 合并为一条 ChatRole.System，
    /// 工具使用说明内嵌其中。其余结构（摘要/关键词/注入/历史/轮次/用户输入）保持不变。
    /// </summary>
    Task<(List<ChatMessage> Messages, ChatMessage InputMessage)> BuildSingleSystemPromptContextMessages(
        ChatHistory history,
        List<ChatRawMessage<ChatMessage>> rawMessages,
        string userMessage,
        string sessionId,
        string sessionKey);

    /// <summary>
    /// 保存最近N轮完整交互过程
    /// </summary>
    Task SaveRecentRound(string sessionId, ChatMessage inputMessage);

    /// <summary>
    /// 获取摘要服务所需的背景上下文（SOUL.md / WORLD.md / MEMORY.md / USER.md）
    /// </summary>
    Task<string> GetSummaryBaseContextAsync();
}
