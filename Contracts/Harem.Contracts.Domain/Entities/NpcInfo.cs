namespace Harem.Contracts.Domain.Entities;

/// <summary>
/// NPC 基础信息（人物卡）
/// </summary>
public class NpcInfo
{
    /// <summary>
    /// 唯一标识，如 alice、boss_001
    /// </summary>
    public string Key { get; set; } = "";

    /// <summary>
    /// 显示名称
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// 关键词标签，如 ["闺蜜", "咖啡爱好者"]
    /// </summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// 人物摘要，AI 注入时看到的描述
    /// </summary>
    public string Summary { get; set; } = "";

    /// <summary>
    /// 基础亲密度（0-100）
    /// </summary>
    public double Intimacy { get; set; } = 50;

    /// <summary>
    /// 最后互动时间
    /// </summary>
    public DateTime LastInteractAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 是否已归档（遗忘后标记）
    /// </summary>
    public bool Archived { get; set; } = false;

    /// <summary>
    /// 当前有效亲密度（时间衰减后）
    /// </summary>
    public double CurrentIntimacy()
    {
        var days = (DateTime.UtcNow - LastInteractAt).TotalDays;
        return Intimacy * Math.Exp(-0.01 * days);
    }

    /// <summary>
    /// 记忆深度等级
    /// </summary>
    public MemoryDepth GetMemoryDepth() => CurrentIntimacy() switch
    {
        > 60 => MemoryDepth.Vivid,
        > 30 => MemoryDepth.Familiar,
        > 10 => MemoryDepth.Vague,
        _ => MemoryDepth.Forgotten
    };
}

public enum MemoryDepth
{
    Vivid,      // 记得细节、性格、过往
    Familiar,   // 记得关系，细节模糊
    Vague,      // 只记得名字和大概关系
    Forgotten   // 完全遗忘
}
