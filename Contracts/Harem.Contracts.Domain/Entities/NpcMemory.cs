namespace Harem.Contracts.Domain.Entities;

/// <summary>
/// NPC 记忆片段（故事/互动记录）
/// </summary>
public class NpcMemory
{
    /// <summary>
    /// 唯一标识
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// 记忆内容
    /// </summary>
    public string Content { get; set; } = "";

    /// <summary>
    /// 发生时间
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 对亲密度的影响（可为负）
    /// </summary>
    public double IntimacyDelta { get; set; } = 0;
}
