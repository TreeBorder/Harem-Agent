namespace Harem.Contracts.Configurations.AgentWorkspace;

/// <summary>
/// 心跳系统配置
/// </summary>
public class HeartbeatOptions
{
    /// <summary>
    /// 是否启用心跳
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// 最小间隔（分钟）
    /// </summary>
    public int MinMinutes { get; set; } = 45;

    /// <summary>
    /// 最大间隔（分钟）
    /// </summary>
    public int MaxMinutes { get; set; } = 75;

    /// <summary>
    /// 会话活跃冷却（分钟），最近活跃则不触发
    /// 也作为冷宫区阈值，闲置超过此值才触发惊喜问候判定
    /// </summary>
    public int CooldownMinutes { get; set; } = 30;

    /// <summary>
    /// 炽热区阈值（分钟），此区间内跳过思念值增加和惊喜问候
    /// 默认20分钟：刚聊完或正在聊，完全不动思念值
    /// </summary>
    public int HotZoneMinutes { get; set; } = 20;

    /// <summary>
    /// 日志最大保留条数
    /// </summary>
    public int MaxLogEntries { get; set; } = 100;

    /// <summary>
    /// 普通规则：思念值增量范围下限
    /// </summary>
    public int NormalIncreaseMin { get; set; } = 1;

    /// <summary>
    /// 普通规则：思念值增量范围上限（含）
    /// </summary>
    public int NormalIncreaseMax { get; set; } = 7;

    /// <summary>
    /// 特殊规则：思念值增量范围下限
    /// </summary>
    public int SpecialIncreaseMin { get; set; } = 5;

    /// <summary>
    /// 特殊规则：思念值增量范围上限（含）
    /// </summary>
    public int SpecialIncreaseMax { get; set; } = 15;

    /// <summary>
    /// 特殊规则：触发阈值，低于此值不触发
    /// </summary>
    public int SpecialTriggerThreshold { get; set; } = 80;

    /// <summary>
    /// 触发后扣除的思念值
    /// </summary>
    public int TriggerPenalty { get; set; } = 50;

    /// <summary>
    /// 思念值上限
    /// </summary>
    public int LongingCap { get; set; } = 100;

    /// <summary>
    /// 等级阈值（从低到高，长度即等级数-1，等级0为默认）
    /// 默认 [15, 30, 50, 70] 对应5个等级：0/1/2/3/4
    /// </summary>
    public List<int> LevelThresholds { get; set; } = [15, 30, 50, 70];

    /// <summary>
    /// 等级波动概率（百分比）
    /// </summary>
    public int LevelFluctuationChance { get; set; } = 20;

    /// <summary>
    /// 是否开启夜间心跳（23:00 ~ 06:30），关闭则此时间段不触发惊喜问候
    /// </summary>
    public bool EnableNightHeartbeat { get; set; } = false;

    /// <summary>
    /// 状态更新时间点（本地时间），默认早中晚各一次
    /// </summary>
    public List<TimeOnly> StateUpdateTimes { get; set; } = [new(8, 0), new(12, 0), new(18, 0)];

    /// <summary>
    /// 状态更新时的思念值增量（固定值）
    /// </summary>
    public int StateUpdateLongingIncrease { get; set; } = 5;

    /// <summary>
    /// 寂寞值每次心跳增量（固定值）
    /// </summary>
    public int LonelinessIncrease { get; set; } = 7;

    /// <summary>
    /// 寂寞值上限
    /// </summary>
    public int LonelinessCap { get; set; } = 100;
}

/// <summary>
/// 心跳提示词配置（.heartbeat/prompts.json）
/// </summary>
public class HeartbeatPrompts
{
    public Dictionary<string, string> Text { get; set; } = new();
    public Dictionary<string, string> Story { get; set; } = new();
    public Dictionary<string, string> Image { get; set; } = new();
    public Dictionary<string, string> Voice { get; set; } = new();
}
