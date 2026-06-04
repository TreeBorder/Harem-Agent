using Harem.Contracts.Domain.Messages;
using Harem.Protocol.ComfyUI;
using Harem.Services.Abstractions.Channels;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Harem.Services.Jobs;

public class CharacterImageTrackingJob : IJob
{
    private readonly IMessageChannel<ReplyMessage> _replyChannel;
    private readonly IComfyUiClientFactory _comfyUiClientFactory;
    private readonly ILogger<CharacterImageTrackingJob> _logger;

    public CharacterImageTrackingJob(IMessageChannel<ReplyMessage> replyChannel, IComfyUiClientFactory comfyUiClientFactory,
        ILogger<CharacterImageTrackingJob> logger)
    {
        _replyChannel = replyChannel;
        _comfyUiClientFactory = comfyUiClientFactory;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var sessionKey = context.JobDetail.JobDataMap.GetString("SessionKey");
        var promptId = context.JobDetail.JobDataMap.GetString("PromptId");
        if (string.IsNullOrEmpty(sessionKey) || string.IsNullOrEmpty(promptId))
        {
            _logger.LogWarning("Missing session key or prompt id");
            await context.Scheduler.DeleteJob(context.JobDetail.Key, context.CancellationToken);
            return;
        }

        try
        {
            var client = _comfyUiClientFactory.CreateClient();
            var history = await client.GetHistoryAsync(promptId);

            if (history?[promptId] == null)
            {
                // 未查询到结果，等待下一次任务
                return;
            }

            // 解析结果
            var images = client.ExtractImageUrls(history, promptId);

            if (images.Length > 0)
            {
                foreach (var image in images)
                {
                    await _replyChannel.WriteAsync(new ReplyMessage(sessionKey, $"![角色图]({image})"));
                }
            }

            await context.Scheduler.DeleteJob(context.JobDetail.Key, context.CancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking photo task {promptId}", promptId);
        }
    }
}