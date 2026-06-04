namespace Harem.Contracts.Configurations.AgentWorkspace;

/// <summary>
/// 定时任务系统配置
/// </summary>
public class CronOptions
{
    /// <summary>
    /// 是否启用定时任务系统
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 最大任务数
    /// </summary>
    public int MaxJobs { get; set; } = 50;

    /// <summary>
    /// 执行日志最大保留条数
    /// </summary>
    public int MaxLogEntries { get; set; } = 200;

    /// <summary>
    /// 活跃会话检测窗口（分钟），会话在此时间内有更新则延后执行
    /// </summary>
    public int ActiveSessionMinutes { get; set; } = 10;

    /// <summary>
    /// 最大延后重试次数，超过则放弃执行
    /// </summary>
    public int MaxDelayRetries { get; set; } = 6;

    /// <summary>
    /// 每次延后间隔（分钟）
    /// </summary>
    public int DelayIntervalMinutes { get; set; } = 10;
}
