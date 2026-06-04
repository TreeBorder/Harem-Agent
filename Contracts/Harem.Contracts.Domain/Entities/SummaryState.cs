namespace Harem.Contracts.Domain.Entities;

public class SummaryState
{
    /// <summary>
    /// 当前累积摘要内容（纯文本，不包含标签包裹）
    /// </summary>
    public string CurrentSummary { get; set; } = string.Empty;

    /// <summary>
    /// 最后一条被纳入摘要的消息的 CreatedAt
    /// </summary>
    public DateTimeOffset? LastSummarizedAt { get; set; }

    /// <summary>
    /// 初始化时的"基础"摘要（每日重置后不覆盖此字段，用于区分"没有摘要"和"空摘要"）
    /// </summary>
    public bool HasEverBeenSummarized { get; set; }
}
