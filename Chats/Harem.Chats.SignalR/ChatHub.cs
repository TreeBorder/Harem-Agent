using Harem.Contracts.Domain.Messages;
using Harem.Services.Abstractions.Channels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Harem.Chats.SignalR;

/// <summary>
/// SignalR Hub — 接收客户端消息，写入 Channel 管线
/// </summary>
public class ChatHub(
    IMessageChannel<ReceivedMessage> receiveChannel,
    ILogger<ChatHub> logger)
    : Hub
{
    private const string DefaultSessionKey = "signalr:direct:user";
    private const string ChannelId = "signalr";

    public async Task SendMessage(string message, string? sessionKey = null)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        var key = sessionKey ?? DefaultSessionKey;
        logger.LogDebug("SignalR 收到消息 [Session: {Key}]: {Message}", key, message);
        await receiveChannel.WriteAsync(new ReceivedMessage(key, ChannelId, message));
    }

    /// <summary>
    /// 客户端连接后自动加入以 sessionKey 命名的组，便于定向推送
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var httpCtx = Context.GetHttpContext();
        var sessionKey = httpCtx?.Request.Query["sessionKey"].FirstOrDefault() ?? DefaultSessionKey;
        await Groups.AddToGroupAsync(Context.ConnectionId, sessionKey);
        logger.LogInformation("SignalR 客户端已连接 [Group: {Group}]", sessionKey);
        await base.OnConnectedAsync();
    }
}
