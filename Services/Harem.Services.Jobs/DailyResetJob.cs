using Harem.Services.Abstractions.Agents;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Harem.Services.Jobs;

/// <summary>
/// 每日重置上下文 Job：先做一次摘要，将当前会话对话导出到当天记忆，然后标记记忆整理
/// 默认每天凌晨 4:00 执行（在 BuildIndexJob 之后）
/// </summary>
[DisallowConcurrentExecution]
public class DailyResetJob : IJob
{
    private readonly ISessionService _sessionService;
    private readonly IMemoryService _memoryService;
    private readonly IHistoryService _historyService;
    private readonly ISummaryService _summaryService;
    private readonly IKeywordService _keywordService;
    private readonly ILogger<DailyResetJob> _logger;

    public DailyResetJob(
        ISessionService sessionService,
        IMemoryService memoryService,
        IHistoryService historyService,
        ISummaryService summaryService,
        IKeywordService keywordService,
        ILogger<DailyResetJob> logger)
    {
        _sessionService = sessionService;
        _memoryService = memoryService;
        _historyService = historyService;
        _summaryService = summaryService;
        _keywordService = keywordService;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            _logger.LogInformation("开始每日上下文重置...");

            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var sessions = await _sessionService.GetAllSessionsAsync();

            foreach (var sessionInfo in sessions)
            {
                try
                {
                    // 1. 先做一次最终摘要，确保断点不丢
                    await _summaryService.GenerateIncrementalSummaryAsync(sessionInfo.SessionId);

                    // 2. 导出到天记忆
                    var history = await _historyService.GetHistoryAsync(sessionInfo.SessionId);
                    await _memoryService.ExportHistoryToDailyMemoryAsync(history, today);

                    // 3. 标记该会话有待整理的记忆，下次对话时由 LLM 自主整理到 MEMORY.md
                    var summaryState = await _summaryService.GetSummaryStateAsync(sessionInfo.SessionId);
                    if (summaryState.HasEverBeenSummarized)
                    {
                        await _historyService.SetHistoryBag(sessionInfo.SessionId, "pendingMemoryConsolidation", true);
                        await _historyService.SetHistoryBag(sessionInfo.SessionId, "pendingSummary",
                            summaryState.CurrentSummary.Trim());
                        _logger.LogInformation("会话 {Key} 已标记待整理记忆", sessionInfo.Key);
                    }

                    _logger.LogDebug("已处理会话: {Key}", sessionInfo.Key);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "处理会话 {Key} 失败", sessionInfo.Key);
                }
            }

            // 4. 提取关键词（sideless，一次全量刷新）
            await _keywordService.ExtractKeywordsAsync(today, context.CancellationToken);

            _logger.LogInformation("每日上下文重置完成，共处理 {Count} 个会话", sessions.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "每日上下文重置失败");
        }
    }
}
