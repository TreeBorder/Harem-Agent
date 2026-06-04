using Harem.Contracts.Configurations.AgentWorkspace;
using Harem.Services.Abstractions.Agents;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Harem.Services.Jobs;

[DisallowConcurrentExecution]
public class HeartbeatJob : IJob
{
    private readonly IHeartbeatService _heartbeatService;
    private readonly IOptions<HeartbeatOptions> _heartbeatOptions;
    private readonly ILogger<HeartbeatJob> _logger;

    public HeartbeatJob(IHeartbeatService heartbeatService, IOptions<HeartbeatOptions> heartbeatOptions, ILogger<HeartbeatJob> logger)
    {
        _heartbeatService = heartbeatService;
        _heartbeatOptions = heartbeatOptions;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            _logger.LogDebug("执行心跳任务...");
            await _heartbeatService.ExecuteGreetingAsync(context.CancellationToken);

            // 从注入的配置读取调度间隔
            var config = _heartbeatOptions.Value;
            var random = Random.Shared;
            var nextInterval = TimeSpan.FromMinutes(random.Next(config.MinMinutes, config.MaxMinutes + 1));
            var nextTime = DateTimeOffset.UtcNow.Add(nextInterval);

            var trigger = TriggerBuilder.Create()
                .StartAt(nextTime)
                .Build();

            await context.Scheduler.RescheduleJob(context.Trigger.Key, trigger);
            _logger.LogDebug("下次心跳调度于 {NextTime:HH:mm:ss}", nextTime.LocalDateTime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "心跳任务执行失败");
        }
    }
}
