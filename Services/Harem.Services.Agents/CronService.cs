using System.Text.Json;
using Harem.Contracts.Configurations;
using Harem.Contracts.Configurations.AgentWorkspace;
using Harem.Contracts.Domain.Entities;
using Harem.Services.Abstractions.Agents;
using Harem.Services.Jobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Harem.Services.Agents;

/// <summary>
/// 定时任务服务实现
/// </summary>
public class CronService : ICronService
{
    private const string JobsFile = ".cron/jobs.json";
    private const string LogFile = ".cron/execution.log";
    private const string PendingResultsPrefix = "pendingCronResults";

    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IOptions<CronOptions> _cronOptions;
    private readonly IOptions<RuntimeOptions> _runtimeOptions;
    private readonly ILogger<CronService> _logger;

    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly Dictionary<string, List<string>> _pendingResults = new();

    public CronService(
        ISchedulerFactory schedulerFactory,
        IOptions<CronOptions> cronOptions,
        IOptions<RuntimeOptions> runtimeOptions,
        ILogger<CronService> logger)
    {
        _schedulerFactory = schedulerFactory;
        _cronOptions = cronOptions;
        _runtimeOptions = runtimeOptions;
        _logger = logger;
    }

    #region CRUD

    public async Task<CronJobConfig> CreateJobAsync(CronJobConfig config)
    {
        var options = _cronOptions.Value;

        // 校验
        if (string.IsNullOrWhiteSpace(config.Name))
            throw new ArgumentException("任务名称不能为空");
        if (string.IsNullOrWhiteSpace(config.Content))
            throw new ArgumentException("消息内容不能为空");
        if (string.IsNullOrWhiteSpace(config.SessionKey))
            throw new ArgumentException("会话Key不能为空");

        if (!config.IsOneShot)
        {
            if (string.IsNullOrWhiteSpace(config.CronExpression))
                throw new ArgumentException("循环任务必须提供 cron 表达式");
            if (!ValidateCronExpression(config.CronExpression))
                throw new ArgumentException($"无效的 cron 表达式: {config.CronExpression}");
        }
        else
        {
            if (config.FireAt == null)
                throw new ArgumentException("单次任务必须提供触发时间");
        }

        // 生成短ID
        config.Id = Guid.NewGuid().ToString("N")[..8];
        config.CreatedAt = DateTimeOffset.UtcNow;
        config.Enabled = true;

        // 读取现有任务，检查上限
        var jobs = await LoadJobsAsync();
        if (jobs.Count >= options.MaxJobs)
            throw new InvalidOperationException($"定时任务数量已达上限 ({options.MaxJobs})");

        jobs.Add(config);
        await SaveJobsAsync(jobs);

        // 注册到 Quartz
        await RegisterToQuartzAsync(config);

        _logger.LogInformation("创建定时任务: {Id} ({Name}), Target={Target}", config.Id, config.Name, config.Target);
        return config;
    }

    public async Task<bool> DeleteJobAsync(string jobId)
    {
        var jobs = await LoadJobsAsync();
        var job = jobs.FirstOrDefault(j => j.Id == jobId);
        if (job == null) return false;

        jobs.Remove(job);
        await SaveJobsAsync(jobs);

        // 从 Quartz 移除
        await UnregisterFromQuartzAsync(jobId);

        _logger.LogInformation("删除定时任务: {Id} ({Name})", jobId, job.Name);
        return true;
    }

    public async Task<bool> ToggleJobAsync(string jobId, bool enabled)
    {
        var jobs = await LoadJobsAsync();
        var job = jobs.FirstOrDefault(j => j.Id == jobId);
        if (job == null) return false;

        job.Enabled = enabled;
        await SaveJobsAsync(jobs);

        if (enabled)
            await RegisterToQuartzAsync(job);
        else
            await UnregisterFromQuartzAsync(jobId);

        _logger.LogInformation("定时任务 {Id} 已{Action}", jobId, enabled ? "启用" : "禁用");
        return true;
    }

    public async Task<CronJobConfig?> GetJobAsync(string jobId)
    {
        var jobs = await LoadJobsAsync();
        return jobs.FirstOrDefault(j => j.Id == jobId);
    }

    public async Task<List<CronJobConfig>> ListJobsAsync(string? sessionKey = null)
    {
        var jobs = await LoadJobsAsync();
        if (!string.IsNullOrEmpty(sessionKey))
            jobs = jobs.Where(j => j.SessionKey == sessionKey).ToList();
        return jobs;
    }

    public bool ValidateCronExpression(string cronExpression)
    {
        return CronExpression.IsValidExpression(cronExpression);
    }

    #endregion

    #region Quartz 集成

    public async Task SyncToQuartzAsync()
    {
        var jobs = await LoadJobsAsync();
        var enabledJobs = jobs.Where(j => j.Enabled).ToList();

        foreach (var job in enabledJobs)
        {
            try
            {
                await RegisterToQuartzAsync(job);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "同步定时任务到 Quartz 失败: {Id} ({Name})", job.Id, job.Name);
            }
        }

        _logger.LogInformation("定时任务同步完成，共 {Count} 个启用任务", enabledJobs.Count);
    }

    private async Task RegisterToQuartzAsync(CronJobConfig config)
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        var jobKey = new JobKey($"CronJob_{config.Id}");
        var triggerKey = new TriggerKey($"CronTrigger_{config.Id}");

        // 构建 JobDataMap
        var dataMap = new JobDataMap
        {
            { "jobId", config.Id },
            { "jobName", config.Name },
            { "target", config.Target.ToString() },
            { "sessionKey", config.SessionKey },
            { "channelId", config.ChannelId },
            { "content", config.Content },
            { "isOneShot", config.IsOneShot },
            { "delayCount", config.DelayCount }
        };

        // 注册 Job
        var job = JobBuilder.Create<CronJob>()
            .WithIdentity(jobKey)
            .UsingJobData(dataMap)
            .Build();

        // 构建 Trigger
        ITrigger trigger;
        if (config.IsOneShot)
        {
            trigger = TriggerBuilder.Create()
                .WithIdentity(triggerKey)
                .StartAt(config.FireAt!.Value)
                .Build();
        }
        else
        {
            trigger = TriggerBuilder.Create()
                .WithIdentity(triggerKey)
                .WithCronSchedule(config.CronExpression!)
                .Build();
        }

        await scheduler.ScheduleJob(job, trigger);
        _logger.LogDebug("已注册 Quartz 任务: {JobKey}", jobKey);
    }

    private async Task UnregisterFromQuartzAsync(string jobId)
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        var jobKey = new JobKey($"CronJob_{jobId}");
        await scheduler.DeleteJob(jobKey);
        _logger.LogDebug("已移除 Quartz 任务: {JobKey}", jobKey);
    }

    /// <summary>
    /// 更新 Quartz 中的任务（延后重试时调用）
    /// </summary>
    public async Task RescheduleJobAsync(string jobId, DateTimeOffset fireAt)
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        var triggerKey = new TriggerKey($"CronTrigger_{jobId}");

        var newTrigger = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .StartAt(fireAt)
            .Build();

        await scheduler.RescheduleJob(triggerKey, newTrigger);
    }

    #endregion

    #region 执行结果注入

    public Task RecordExecutionResultAsync(string sessionId, string result)
    {
        lock (_pendingResults)
        {
            if (!_pendingResults.ContainsKey(sessionId))
                _pendingResults[sessionId] = new List<string>();
            _pendingResults[sessionId].Add(result);
        }
        return Task.CompletedTask;
    }

    public Task<List<string>?> ConsumePendingResultsAsync(string sessionId)
    {
        lock (_pendingResults)
        {
            if (_pendingResults.TryGetValue(sessionId, out var results) && results.Count > 0)
            {
                var copy = new List<string>(results);
                results.Clear();
                return Task.FromResult<List<string>?>(copy);
            }
        }
        return Task.FromResult<List<string>?>(null);
    }

    #endregion

    #region 文件持久化

    private async Task<List<CronJobConfig>> LoadJobsAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            var path = Path.Combine(_runtimeOptions.Value.Workspace, JobsFile);
            if (!File.Exists(path))
                return new List<CronJobConfig>();

            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<List<CronJobConfig>>(json) ?? new List<CronJobConfig>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读取定时任务配置失败");
            return new List<CronJobConfig>();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task SaveJobsAsync(List<CronJobConfig> jobs)
    {
        await _fileLock.WaitAsync();
        try
        {
            var path = Path.Combine(_runtimeOptions.Value.Workspace, JobsFile);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var json = JsonSerializer.Serialize(jobs, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            await File.WriteAllTextAsync(path, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存定时任务配置失败");
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// 更新任务配置（执行次数、延后次数等）
    /// </summary>
    public async Task UpdateJobAsync(CronJobConfig config)
    {
        var jobs = await LoadJobsAsync();
        var index = jobs.FindIndex(j => j.Id == config.Id);
        if (index >= 0)
        {
            jobs[index] = config;
            await SaveJobsAsync(jobs);
        }
    }

    #endregion

    #region 日志

    public async Task AppendLogAsync(string jobId, string message)
    {
        try
        {
            var options = _cronOptions.Value;
            var logPath = Path.Combine(_runtimeOptions.Value.Workspace, LogFile);
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

            var entry = JsonSerializer.Serialize(new
            {
                Time = DateTime.UtcNow.ToString("O"),
                JobId = jobId,
                Message = message
            });

            await File.AppendAllTextAsync(logPath, entry + Environment.NewLine);
            await TrimLogAsync(logPath, options.MaxLogEntries);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "定时任务日志写入失败");
        }
    }

    private static async Task TrimLogAsync(string logPath, int maxEntries)
    {
        if (!File.Exists(logPath)) return;

        var lines = (await File.ReadAllLinesAsync(logPath))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        if (lines.Count > maxEntries)
        {
            lines = lines.Skip(lines.Count - maxEntries).ToList();
            await File.WriteAllLinesAsync(logPath, lines);
        }
    }

    #endregion
}
