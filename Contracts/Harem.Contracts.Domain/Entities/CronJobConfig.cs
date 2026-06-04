namespace Harem.Contracts.Domain.Entities;

/// <summary>
/// 定时任务配置实体
/// </summary>
public class CronJobConfig
{
    /// <summary>
    /// 短ID（8位，方便用户引用）
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 任务名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Quartz cron 表达式（循环任务）
    /// </summary>
    public string? CronExpression { get; set; }

    /// <summary>
    /// 是否单次任务
    /// </summary>
    public bool IsOneShot { get; set; }

    /// <summary>
    /// 单次任务触发时间（UTC）
    /// </summary>
    public DateTimeOffset? FireAt { get; set; }

    /// <summary>
    /// 任务目标类型
    /// </summary>
    public CronJobTarget Target { get; set; }

    /// <summary>
    /// 关联的会话Key
    /// </summary>
    public string SessionKey { get; set; } = string.Empty;

    /// <summary>
    /// 关联的渠道ID
    /// </summary>
    public string ChannelId { get; set; } = string.Empty;

    /// <summary>
    /// 消息内容
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 最后执行时间
    /// </summary>
    public DateTimeOffset? LastFireTime { get; set; }

    /// <summary>
    /// 执行次数
    /// </summary>
    public int ExecutionCount { get; set; }

    /// <summary>
    /// 延后重试次数（活跃会话检测）
    /// </summary>
    public int DelayCount { get; set; }

    /// <summary>
    /// 缓存的音频文件路径（TTS 定时任务复用）
    /// </summary>
    public string? CachedAudioPath { get; set; }
}

/// <summary>
/// 定时任务目标类型
/// </summary>
public enum CronJobTarget
{
    /// <summary>
    /// 系统消息→Agent（触发Agent响应）
    /// </summary>
    SystemToAgent,

    /// <summary>
    /// 消息→用户（直接发送，不经过Agent处理）
    /// </summary>
    MessageToUser,

    /// <summary>
    /// 用户消息→Agent（触发Agent处理）
    /// </summary>
    MessageToAgent
}
