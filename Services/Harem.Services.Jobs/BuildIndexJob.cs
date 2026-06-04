using Harem.Services.Abstractions.Agents;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Harem.Services.Jobs;

/// <summary>
/// 定时构建索引 Job（记忆 + NPC 向量索引）
/// 默认每天凌晨执行一次
/// </summary>
[DisallowConcurrentExecution]
public class BuildIndexJob : IJob
{
    private readonly IMemoryService _memoryService;
    private readonly INpcService _npcService;
    private readonly ILogger<BuildIndexJob> _logger;

    public BuildIndexJob(
        IMemoryService memoryService,
        INpcService npcService,
        ILogger<BuildIndexJob> logger)
    {
        _memoryService = memoryService;
        _npcService = npcService;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            _logger.LogInformation("开始构建索引...");

            _logger.LogDebug("构建记忆索引...");
            await _memoryService.BuildMemoryIndexAsync();

            _logger.LogDebug("构建 NPC 索引...");
            await _npcService.BuildIndexAsync();

            _logger.LogInformation("索引构建完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "索引构建失败");
        }
    }
}
