namespace Harem.Contracts.Domain.Entities;

/// <summary>
/// RPG会话信息
/// </summary>
public class SessionInfo
{
    /// <summary>
    /// 会话的主键，{MessageChannel.Name}:{direct|group}:{Topic}
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// 会话ID
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// 会话通道ID
    /// </summary>
    public string ChannelId { get; set; }

    /// <summary>
    /// 更新时间
    /// </summary>
    public long UpdateAt { get; set; }

    /// <summary>
    /// 会话年龄，以毫秒为单位
    /// </summary>
    public long AgeMs { get; set; }

    /// <summary>
    /// 系统消息是否发送
    /// </summary>
    public bool SystemSent { get; set; }

    /// <summary>
    /// 总输入Tokens
    /// </summary>
    public long InputTokens { get; set; }

    /// <summary>
    /// 总输出Tokens
    /// </summary>
    public long OutputTokens { get; set; }

    /// <summary>
    /// 总Tokens刷新状态
    /// </summary>
    public bool TotalTokensFresh { get; set; }

    /// <summary>
    /// 关联的model,ModelProvider/ModelName
    /// </summary>
    public string ModelKey { get; set; }

    /// <summary>
    /// 模型上下文最大令牌数
    /// </summary>
    public long ContextTokens { get; set; }
}