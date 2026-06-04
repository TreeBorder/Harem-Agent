namespace Harem.Contracts.Domain.Entities;

/// <summary>
/// 角色状态，单一状态
/// </summary>
public class StateItem
{
    /// <summary>
    /// 状态的键
    /// </summary>
    public string Key { get; set; } = "";

    /// <summary>
    /// 状态的名称，中文，用于展示
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// 数值类状态，数值范围[-100,100]
    /// 字符类状态，如场景，今日服装
    /// </summary>
    public string Value { get; set; } = "";

    /// <summary>
    /// 参数说明/用途描述
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

    public bool InInjectPrompt { get; set; } = false;
    public bool AgentEditable { get; set; } = true;
}