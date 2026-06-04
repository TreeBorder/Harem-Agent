using Harem.Contracts.Domain.Entities;

namespace Harem.Services.Abstractions.Agents;

/// <summary>
/// 定时任务服务接口
/// </summary>
public interface ICronService
{
    /// <summary>
    /// 创建定时任务（写入配置 + 注册到 Quartz）
    /// </summary>
    Task<CronJobConfig> CreateJobAsync(CronJobConfig config);

    /// <summary>
    /// 删除定时任务（从 Quartz 移除 + 从配置删除）
    /// </summary>
    Task<bool> DeleteJobAsync(string jobId);

    /// <summary>
    /// 启用/禁用定时任务
    /// </summary>
    Task<bool> ToggleJobAsync(string jobId, bool enabled);

    /// <summary>
    /// 获取单个任务
    /// </summary>
    Task<CronJobConfig?> GetJobAsync(string jobId);

    /// <summary>
    /// 列出任务（可按 sessionKey 过滤）
    /// </summary>
    Task<List<CronJobConfig>> ListJobsAsync(string? sessionKey = null);

    /// <summary>
    /// 校验 Quartz cron 表达式是否合法
    /// </summary>
    bool ValidateCronExpression(string cronExpression);

    /// <summary>
    /// 启动时从 jobs.json 同步到 Quartz
    /// </summary>
    Task SyncToQuartzAsync();

    /// <summary>
    /// 记录执行结果（用于下次上下文注入）
    /// </summary>
    Task RecordExecutionResultAsync(string sessionId, string result);

    /// <summary>
    /// 获取并清除待注入的执行结果
    /// </summary>
    Task<List<string>?> ConsumePendingResultsAsync(string sessionId);

    /// <summary>
    /// 更新任务配置（执行次数、延后次数等）
    /// </summary>
    Task UpdateJobAsync(CronJobConfig config);

    /// <summary>
    /// 延后执行：重新调度触发器
    /// </summary>
    Task RescheduleJobAsync(string jobId, DateTimeOffset fireAt);

    /// <summary>
    /// 写入执行日志
    /// </summary>
    Task AppendLogAsync(string jobId, string message);
}
