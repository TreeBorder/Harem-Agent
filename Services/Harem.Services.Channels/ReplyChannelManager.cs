using System.Collections.Concurrent;
using Harem.Contracts.Domain.Messages;
using Harem.Services.Abstractions.Channels;

namespace Harem.Services.Channels;

/// <summary>
/// 回复消息通道管理器——非 IHostedService，纯注册/查询。
/// Chats 服务在 StartAsync 时调用 Register() 注册自己的专用通道。
/// ChatRouterWork 在路由时调用 GetChannel() 获取目标通道。
/// </summary>
public class ReplyChannelManager : IReplyChannelManager
{
    private readonly ConcurrentDictionary<string, IMessageChannel<ReplyMessage>> _routes = new(StringComparer.OrdinalIgnoreCase);

    public void Register(string sessionKeyPrefix, IMessageChannel<ReplyMessage> channel)
    {
        _routes[sessionKeyPrefix] = channel;
    }

    public IMessageChannel<ReplyMessage>? GetChannel(string sessionKey)
    {
        // 按最长前缀匹配，例如 "mattermost:" 优先于 "matter"
        string? matchedPrefix = null;
        foreach (var prefix in _routes.Keys)
        {
            if (sessionKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                if (matchedPrefix == null || prefix.Length > matchedPrefix.Length)
                    matchedPrefix = prefix;
            }
        }

        return matchedPrefix != null ? _routes[matchedPrefix] : null;
    }
}
