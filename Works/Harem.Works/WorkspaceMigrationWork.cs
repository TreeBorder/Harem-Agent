using System.Text;
using System.Text.Json;
using Harem.Contracts.Domain.Entities;
using Harem.Services.Abstractions.Agents;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Harem.Works;

/// <summary>
/// 工作区配置迁移服务：启动时自动检测并升级旧格式配置文件，并初始化定时任务
/// </summary>
public class WorkspaceMigrationWork : IHostedService
{
    private readonly IFileService _fileService;
    private readonly ICronService _cronService;
    private readonly ILogger<WorkspaceMigrationWork> _logger;

    public WorkspaceMigrationWork(IFileService fileService, ICronService cronService, ILogger<WorkspaceMigrationWork> logger)
    {
        _fileService = fileService;
        _cronService = cronService;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await MigrateHeartbeatConfigAsync();
        await MigrateStateConfigAsync();

        // 初始化定时任务：从 .cron/jobs.json 同步到 Quartz
        try
        {
            await _cronService.SyncToQuartzAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "定时任务初始化失败");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// 迁移心跳配置：确保包含所有新字段，移除旧字段
    /// </summary>
    private async Task MigrateHeartbeatConfigAsync()
    {
        try
        {
            var doc = await _fileService.GetJsonFileAsync(".heartbeat/config.json");
            if (doc == null)
            {
                _logger.LogWarning("心跳配置文件不存在，跳过迁移");
                return;
            }

            if (!doc.Value.TryGetProperty("Heartbeat", out var heartbeatNode))
            {
                _logger.LogWarning("心跳配置缺少 Heartbeat 根节点，跳过迁移");
                return;
            }

            var modified = false;
            using var ms = new MemoryStream();
            await using var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true });

            writer.WriteStartObject();
            writer.WritePropertyName("Heartbeat");
            writer.WriteStartObject();

            // 复制现有字段（跳过旧字段）
            foreach (var prop in heartbeatNode.EnumerateObject())
            {
                if (prop.NameEquals("StateUpdateAvoidMinutes"))
                {
                    modified = true;
                    continue;
                }

                prop.WriteTo(writer);
            }

            // 检查并添加缺失的新字段
            var newFields = new Dictionary<string, JsonElement>
            {
                ["StateUpdateLongingIncrease"] = JsonSerializer.SerializeToElement(5),
                ["LonelinessIncrease"] = JsonSerializer.SerializeToElement(7),
                ["LonelinessCap"] = JsonSerializer.SerializeToElement(100),
            };

            foreach (var (key, defaultValue) in newFields)
            {
                if (!heartbeatNode.TryGetProperty(key, out _))
                {
                    writer.WritePropertyName(key);
                    defaultValue.WriteTo(writer);
                    modified = true;
                }
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
            await writer.FlushAsync();

            if (modified)
            {
                var json = Encoding.UTF8.GetString(ms.ToArray());
                await _fileService.WriteFileAsync(".heartbeat/config.json", json);
                _logger.LogInformation("心跳配置文件已自动迁移为新格式");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "心跳配置文件迁移失败");
        }
    }

    private async Task MigrateStateConfigAsync()
    {
        try
        {
            var states = await _fileService.ReadJsonAsync<List<StateItem>>("state.json");
            if (states == null || states.Count == 0)
            {
                _logger.LogWarning("状态配置文件不存在或为空，跳过迁移");
                return;
            }

            var modified = false;

            // 2. 规范化各状态的权限
            var expectedPermissions = new Dictionary<string, (bool AgentEditable, bool InInjectPrompt)>
            {
                ["longing"] = (false, false),
                ["loneliness"] = (false, true),
                ["voiceId"] = (false, false),
                ["emotion"] = (true, true),
                ["scene"] = (true, true),
                ["outfit"] = (true, true)
            };

            foreach (var state in states)
            {
                if (expectedPermissions.TryGetValue(state.Key, out var expected))
                {
                    if (state.AgentEditable != expected.AgentEditable ||
                        state.InInjectPrompt != expected.InInjectPrompt)
                    {
                        state.AgentEditable = expected.AgentEditable;
                        state.InInjectPrompt = expected.InInjectPrompt;
                        modified = true;
                    }
                }
            }

            if (modified)
            {
                await _fileService.WriteJsonAsync("state.json", states);
                _logger.LogInformation("状态配置文件已自动迁移为新格式");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "状态配置文件迁移失败");
        }
    }
}