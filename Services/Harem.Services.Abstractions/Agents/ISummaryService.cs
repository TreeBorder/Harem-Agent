using Harem.Contracts.Domain.Entities;

namespace Harem.Services.Abstractions.Agents;

/// <summary>
/// 摘要服务：独立于主 LLM 流程，后台增量生成对话摘要
/// </summary>
public interface ISummaryService
{
    /// <summary>
    /// 对指定会话执行一次增量摘要。
    /// 读取 lastSummarizedAt 之后的新消息，结合历史摘要 + 冗余原始消息生成新摘要，写回 summary.json
    /// </summary>
    Task GenerateIncrementalSummaryAsync(string sessionId);

    /// <summary>
    /// 强制重新生成摘要：忽略已有摘要，基于全部历史消息重新生成
    /// </summary>
    Task ForceRegenerateSummaryAsync(string sessionId);

    /// <summary>
    /// 读取会话的摘要状态。如果 summary.json 不存在，返回默认空状态
    /// </summary>
    Task<SummaryState> GetSummaryStateAsync(string sessionId);

    /// <summary>
    /// 检查会话是否需要摘要（有新消息超出 lastSummarizedAt）
    /// </summary>
    Task<bool> NeedsSummaryAsync(string sessionId);
}
