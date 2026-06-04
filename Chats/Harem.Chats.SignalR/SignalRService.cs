using Harem.Contracts.Configurations;
using Harem.Contracts.Domain.Messages;
using Harem.Services.Abstractions.Channels;
using Harem.Services.Channels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Harem.Chats.SignalR;

/// <summary>
/// SignalR 出站通道：后台读取自己的专属 ReplyMessage 通道并推送至 SignalR 客户端
/// </summary>
public class SignalRService(
    IHubContext<ChatHub> hubContext,
    IReplyChannelManager channelManager,
    IOptions<RuntimeOptions> runtimeOptions,
    ILogger<SignalRService> logger)
    : IHostedService
{
    private readonly IMessageChannel<ReplyMessage> _replyChannel = new ChatReplyChannel();
    private readonly string _workspace = runtimeOptions.Value.Workspace;

    public SignalRService() : this(null!, null!, null!, null!)
    {
        // 主构造器已注入所有依赖
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        channelManager.Register("signalr:", _replyChannel);
        _ = ReplyLoopAsync(cancellationToken);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task ReplyLoopAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var reply in _replyChannel.ReadAllAsync(ct))
            {
                try
                {
                    var groupName = reply.SessionKey;

                    if (reply.IsTyping)
                    {
                        await hubContext.Clients.Group(groupName)
                            .SendAsync("ReceiveTyping", reply.Message, ct);
                    }
                    else if (reply.Files is { Count: > 0 })
                    {
                        var fileUrls = reply.Files
                            .Select(ToUrl)
                            .ToList();

                        await hubContext.Clients.Group(groupName)
                            .SendAsync("ReceiveFile", new
                            {
                                sessionKey = reply.SessionKey,
                                message = reply.Message,
                                files = fileUrls,
                                timestamp = DateTimeOffset.UtcNow
                            }, ct);
                    }
                    else
                    {
                        await hubContext.Clients.Group(groupName)
                            .SendAsync("ReceiveMessage", new
                            {
                                sessionKey = reply.SessionKey,
                                message = reply.Message,
                                files = reply.Files,
                                timestamp = DateTimeOffset.UtcNow
                            }, ct);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "发送 SignalR 消息失败: {SessionKey}", reply.SessionKey);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常关闭
        }
    }

    /// <summary>
    /// 将本地文件路径转为 /files/ 开头的可下载 URL
    /// </summary>
    private string ToUrl(string localPath)
    {
        var relative = Path.GetRelativePath(_workspace, localPath);
        return $"/files/{relative.Replace('\\', '/').TrimStart('.')}";
    }
}