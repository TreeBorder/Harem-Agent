using Harem.Contracts.Domain.Entities;

namespace Harem.Services.Abstractions.Agents;

/// <summary>
/// Session服务接口
/// </summary>
public interface ISessionService
{
    /// <summary>
    /// 创建新会话
    /// </summary>
    /// <param name="channel">渠道名称，如mattermost</param>
    /// <param name="type">会话类型，如direct(私聊)或group(群聊)</param>
    /// <param name="topic">聊天主题，一般为渠道内部对话的标识</param>
    SessionInfo CreateNewSessionAsync(string channel, string type, string topic);

    /// <summary>
    /// 更新会话信息
    /// </summary>
    Task<bool> UpdateSessionAsync(Session session);

    /// <summary>
    /// 获取会话信息
    /// </summary>
    /// <param name="channel">渠道名称，如mattermost</param>
    /// <param name="type">会话类型，如direct(私聊)或group(群聊)</param>
    /// <param name="topic">聊天主题，一般为渠道内部对话的标识</param>
    Task<Session> GetSessionAsync(string channel, string type, string topic);

    /// <summary>
    /// 根据会话标识获取会话信息
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    Task<Session> GetSessionAsync(string key);

    /// <summary>
    /// 获取所有会话信息
    /// </summary>
    Task<IReadOnlyList<SessionInfo>> GetAllSessionsAsync();

    /// <summary>
    /// 获取启用了系统消息的会话（SystemSent=true，全局唯一）
    /// </summary>
    Task<SessionInfo?> GetSystemSentSessionAsync();

    /// <summary>
    /// 获取会话信息
    /// </summary>
    /// <param name="channel">渠道名称，如mattermost</param>
    /// <param name="type">会话类型，如direct(私聊)或group(群聊)</param>
    /// <param name="topic">聊天主题，一般为渠道内部对话的标识</param>
    Task<SessionInfo> GetSessionInfoAsync(string channel, string type, string topic);

    /// <summary>
    /// 根据会话标识获取会话信息
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    Task<SessionInfo?> GetSessionInfoAsync(string key);

    /// <summary>
    /// 重置会话：生成新 SessionId，清空 Content，保留会话元信息
    /// </summary>
    Task<Session> ResetSessionAsync(string key);
}