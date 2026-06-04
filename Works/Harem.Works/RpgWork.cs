using Harem.Contracts.Domain.Messages;
using Harem.Services.Abstractions.Agents;
using Harem.Services.Abstractions.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Harem.Works;

public class RpgWork : IHostedService
{
    private readonly ILogger<RpgWork> _logger;
    private readonly IConversationService _conversationService;
    private readonly IMessageChannel<ReceivedMessage> _receiveChannel;

    public RpgWork(ILogger<RpgWork> logger, IMessageChannel<ReceivedMessage> receiveChannel, IConversationService conversationService)
    {
        _logger = logger;
        _receiveChannel = receiveChannel;
        _conversationService = conversationService;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("RpgWork started.");
        _ = RunMessageLoopAsync(cancellationToken);
    }

    /// <summary>
    /// 消息接收主循环
    /// </summary>
    private async Task RunMessageLoopAsync(CancellationToken ct)
    {
        await foreach (var message in _receiveChannel.ReadAllAsync(ct))
        {
            try
            {
                await _conversationService.ProcessMessageAsync(message, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理消息时出错: {Message}", message.Message);
            }
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("RpgWork stopped.");
        
    }
}