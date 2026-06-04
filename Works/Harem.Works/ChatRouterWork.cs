using Harem.Contracts.Domain.Messages;
using Harem.Services.Abstractions.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Harem.Works;

/// <summary>
/// 回复消息路由 Worker——唯一消费 ChatReplyChannel 的服务。
/// 根据 sessionKey 前缀，通过 IReplyChannelManager 查找目标通道并转发。
/// </summary>
public class ChatRouterWork(
    IMessageChannel<ReplyMessage> upstreamChannel,
    IReplyChannelManager channelManager,
    ILogger<ChatRouterWork> logger)
    : IHostedService
{
    private CancellationTokenSource? _cts;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = RouteLoopAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        return Task.CompletedTask;
    }

    private async Task RouteLoopAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var reply in upstreamChannel.ReadAllAsync(ct))
            {
                try
                {
                    var target = channelManager.GetChannel(reply.SessionKey);
                    if (target != null)
                    {
                        await target.WriteAsync(reply, ct);
                    }
                    else
                    {
                        logger.LogWarning("未找到目标通道: SessionKey={SessionKey}", reply.SessionKey);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "路由消息失败: SessionKey={SessionKey}", reply.SessionKey);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常关闭
        }
    }
}
