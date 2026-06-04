using System.Collections.Concurrent;
using Harem.Contracts.Configurations.AgentWorkspace;
using Harem.Contracts.Domain.Messages;
using Harem.Services.Abstractions.Agents;
using Harem.Services.Abstractions.Channels;
using Harem.Services.Channels;
using Mattermost;
using Mattermost.Events;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Harem.Chats.Mattermost;

/// <summary>
/// Mattermost 聊天通道：入站（WebSocket→Channel）和出站（Channel→Mattermost API）
/// </summary>
public class MattermostService : IHostedService
{
    private readonly IMattermostClient _client;
    private readonly ILogger<MattermostService> _logger;
    private readonly ISessionService _session;
    private readonly IMessageChannel<ReceivedMessage> _receiveChannel;
    private readonly IMessageChannel<ReplyMessage> _replyChannel = new ChatReplyChannel();
    private readonly IReplyChannelManager _channelManager;
    private CancellationTokenSource _cts = new();

    private readonly ConcurrentDictionary<string, TypingState> _typingPosts = new();

    private const string ChatType = "mattermost";

    private static readonly string[] TypingAnimations =
        ["正在输入 •••...", "正在输入 ••...•", "正在输入 •...••", "正在输入 ...•••", "正在输入 ..•••.", "正在输入 .•••.."];

    private static readonly TimeSpan TypingInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan TypingTimeout = TimeSpan.FromSeconds(60);

    private record TypingState(string PostId, string ChannelId, int Step = 0, DateTimeOffset CreatedAt = default)
    {
        public int Step = Step;
        public DateTimeOffset CreatedAt = CreatedAt == default ? DateTimeOffset.UtcNow : CreatedAt;
    }

    public MattermostService(IOptions<ChatOptions> options, ILogger<MattermostService> logger,
        IMessageChannel<ReceivedMessage> receiveChannel,
        ISessionService session, IReplyChannelManager channelManager)
    {
        _logger = logger;
        _receiveChannel = receiveChannel;
        _session = session;
        _channelManager = channelManager;
        _client = new MattermostClient(options.Value.ServerUrl, options.Value.Token);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _client.OnConnected += OnMattermostConnected;
        _client.OnDisconnected += OnMattermostDisconnected;
        _client.OnMessageReceived += OnMattermostMessageReceived;

        _channelManager.Register("mattermost:", _replyChannel);

        _ = TypingAnimationLoopAsync();
        _ = ReplyConsumeLoopAsync();
        _ = _client.StartReceivingAsync(_cts.Token);
        await Task.CompletedTask;
    }

    /// <summary>
    /// 后台循环：读取 _replyChannel 中的回复消息并发送到 Mattermost
    /// </summary>
    private async Task ReplyConsumeLoopAsync()
    {
        try
        {
            await foreach (var reply in _replyChannel.ReadAllAsync(_cts.Token))
            {
                if (!_client.IsConnected)
                {
                    _logger.LogWarning("Mattermost 未连接，跳过发送消息");
                    continue;
                }

                try
                {
                    await SendReplyToMattermostAsync(reply);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "发送 Mattermost 消息失败");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常关闭
        }
    }

    private async Task SendReplyToMattermostAsync(ReplyMessage reply)
    {
        var sessionInfo = await _session.GetSessionInfoAsync(reply.SessionKey);
        if (sessionInfo == null)
        {
            _logger.LogWarning("Session {Key} not found", reply.SessionKey);
            return;
        }

        List<string> fileIds = [];
        if (reply.Files is { Count: > 0 })
        {
            foreach (var file in reply.Files)
            {
                var fileDetails = await _client.UploadFileAsync(sessionInfo.ChannelId, file);
                fileIds.Add(fileDetails.Id);
            }
        }

        if (reply.IsTyping && string.IsNullOrEmpty(reply.Message) && fileIds.Count == 0)
        {
            var post = await _client.CreatePostAsync(sessionInfo.ChannelId, TypingAnimations[0]);
            _typingPosts[reply.SessionKey] = new TypingState(post.Id, sessionInfo.ChannelId);
        }
        else if (reply.IsTyping && _typingPosts.TryGetValue(reply.SessionKey, out var typing))
        {
            await _client.UpdatePostAsync(typing.PostId, reply.Message);
        }
        else
        {
            if (_typingPosts.TryRemove(reply.SessionKey, out var existingTyping))
            {
                if (fileIds.Count > 0)
                {
                    await _client.DeletePostAsync(existingTyping.PostId);
                    await _client.CreatePostAsync(sessionInfo.ChannelId, reply.Message, files: fileIds);
                }
                else
                {
                    await _client.UpdatePostAsync(existingTyping.PostId, reply.Message);
                }
            }
            else
            {
                if (fileIds.Count > 0)
                    await _client.CreatePostAsync(sessionInfo.ChannelId, reply.Message, files: fileIds);
                else
                    await _client.CreatePostAsync(sessionInfo.ChannelId, reply.Message);
            }
        }
    }

    /// <summary>
    /// Typing 动画循环：每 3 秒更新省略号动画，超时 60 秒后删除空 Post
    /// </summary>
    private async Task TypingAnimationLoopAsync()
    {
        using var timer = new PeriodicTimer(TypingInterval);
        while (await timer.WaitForNextTickAsync(_cts.Token))
        {
            try
            {
                foreach (var kvp in _typingPosts)
                {
                    var (sessionKey, typing) = kvp;

                    if (DateTimeOffset.UtcNow - typing.CreatedAt > TypingTimeout)
                    {
                        _typingPosts.TryRemove(sessionKey, out _);
                        try { await _client.DeletePostAsync(typing.PostId); }
                        catch (Exception ex) { _logger.LogWarning(ex, "删除超时 typing Post 失败"); }
                        continue;
                    }

                    typing.Step = (typing.Step + 1) % TypingAnimations.Length;
                    try { await _client.UpdatePostAsync(typing.PostId, TypingAnimations[typing.Step]); }
                    catch (Exception ex) { _logger.LogWarning(ex, "更新 typing 动画失败"); }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Typing 动画循环异常");
            }
        }
    }

    private void OnMattermostMessageReceived(object? sender, MessageEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Message.Post.Text))
            return;

        _logger.LogDebug(e.Message.RawPostData);
        var sessionKey = $"{ChatType}:{e.Message.ChannelType}:{e.Message.Post.UserId}";
        _ = _receiveChannel.WriteAsync(new ReceivedMessage(sessionKey, e.Message.Post.ChannelId, e.Message.Post.Text));
    }

    private void OnMattermostDisconnected(object? sender, DisconnectionEventArgs e)
    {
        _logger.LogInformation("Mattermost disconnected! At:{At}, CloseStatusDescription:{Desc}",
            e.DisconnectedAt, e.CloseStatusDescription);
    }

    private void OnMattermostConnected(object? sender, ConnectionEventArgs e)
    {
        _logger.LogInformation("Mattermost connected:{Uri}, At:{At}", e.Uri, e.ConnectedAt);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _client.StopReceivingAsync();
        return Task.CompletedTask;
    }
}
