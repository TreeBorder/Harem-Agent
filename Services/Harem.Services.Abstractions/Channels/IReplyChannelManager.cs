using Harem.Contracts.Domain.Messages;

namespace Harem.Services.Abstractions.Channels;

/// <summary>
/// 回复消息通道管理器——Chats 服务注册自己的出站通道，
/// ChatRouterWork 根据 sessionKey 前缀查询目标通道。
/// </summary>
public interface IReplyChannelManager
{
    void Register(string sessionKeyPrefix, IMessageChannel<ReplyMessage> channel);
    IMessageChannel<ReplyMessage>? GetChannel(string sessionKey);
}
