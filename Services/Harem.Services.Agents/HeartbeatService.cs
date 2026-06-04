using System.Text.Json;
using Harem.Contracts.Configurations;
using Harem.Contracts.Configurations.AgentWorkspace;
using Harem.Contracts.Domain.Messages;
using Harem.Services.Abstractions.Agents;
using Harem.Services.Abstractions.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Harem.Services.Agents;

/// <summary>
/// 心跳服务实现 - 思念值管理与惊喜问候触发 / 状态更新推送
/// </summary>
public class HeartbeatService : IHeartbeatService
{
    private readonly IFileService _fileService;
    private readonly IStateService _stateService;
    private readonly ISessionService _sessionService;
    private readonly IMessageChannel<ReceivedMessage> _receiveChannel;
    private readonly ILogger<HeartbeatService> _logger;
    private readonly IOptions<HeartbeatOptions> _heartbeatOptions;
    private readonly IOptions<RuntimeOptions> _runtimeOptions;

    private const string PromptsFile = ".heartbeat/prompts.json";
    private const string LogFile = ".heartbeat/heartbeat.log";

    // 状态键
    private const string LongingKey = "longing";

    private const string LonelinessKey = "loneliness";

    // 模式键
    private const string ModeKey = "mode";

    public HeartbeatService(
        IFileService fileService,
        IStateService stateService,
        ISessionService sessionService,
        IMessageChannel<ReceivedMessage> receiveChannel,
        ILogger<HeartbeatService> logger,
        IOptions<HeartbeatOptions> heartbeatOptions,
        IOptions<RuntimeOptions> runtimeOptions)
    {
        _fileService = fileService;
        _stateService = stateService;
        _sessionService = sessionService;
        _receiveChannel = receiveChannel;
        _logger = logger;
        _heartbeatOptions = heartbeatOptions;
        _runtimeOptions = runtimeOptions;
    }

    #region 惊喜问候心跳

    public async Task ExecuteGreetingAsync(CancellationToken ct = default)
    {
        var config = _heartbeatOptions.Value;
        if (!config.Enabled)
        {
            _logger.LogInformation("心跳已禁用");
            return;
        }

        // 读取当前模式
        var mode = (await _stateService.GetStateAsync(ModeKey))?.Value ?? "normal";

        // 寂寞值增加（每次心跳都增加）
        await IncreaseLonelinessAsync(config);

        if (mode == "dating")
        {
            await _stateService.UpdateStateAsync(LongingKey, "0");
            await _stateService.UpdateStateAsync(LonelinessKey, "0");
            _logger.LogInformation("约会状态中，跳过心跳并重置思念值和寂寞值");
            await AppendLogAsync("datingSkip", false, "dating", 0, config.MaxLogEntries, null);
            return;
        }

        // 惊喜问候心跳
        await ExecuteSurpriseGreetingAsync(mode, config, ct);
    }

    private async Task IncreaseLonelinessAsync(HeartbeatOptions config)
    {
        var lonelinessStr = (await _stateService.GetStateAsync(LonelinessKey))?.Value;
        var loneliness = 0;
        if (!string.IsNullOrEmpty(lonelinessStr) && int.TryParse(lonelinessStr, out var parsed))
            loneliness = parsed;

        loneliness = Math.Min(loneliness + config.LonelinessIncrease, config.LonelinessCap);
        await _stateService.UpdateStateAsync(LonelinessKey, loneliness.ToString());
    }
    //
    // private async Task ExecuteStateUpdateHeartbeatAsync(HeartbeatOptions config, CancellationToken ct)
    // {
    //     // 思念值+5
    //     var longingStr = (await _stateService.GetStateAsync(LongingKey))?.Value;
    //     var longing = 0;
    //     if (!string.IsNullOrEmpty(longingStr) && int.TryParse(longingStr, out var parsed))
    //         longing = parsed;
    //     longing = Math.Min(longing + config.StateUpdateLongingIncrease, config.LongingCap);
    //     await _stateService.UpdateStateAsync(LongingKey, longing.ToString());
    //
    //     // 获取启用了系统消息的会话
    //     var targetSession = await _sessionService.GetSystemSentSessionAsync();
    //     if (targetSession is not null)
    //     {
    //         var message = await BuildStateUpdateMessageAsync();
    //         await _receiveChannel.WriteAsync(
    //             new ReceivedMessage(targetSession.Key, targetSession.ChannelId, message), ct);
    //         _logger.LogInformation("状态更新消息已发送到 {Session}", targetSession.Key);
    //     }
    //     else
    //     {
    //         _logger.LogInformation("状态更新触发，但没有启用的会话");
    //     }
    //
    //     // 写日志
    //     await AppendLogAsync("stateUpdate", true, "state", longing, config.MaxLogEntries, targetSession?.Key);
    // }

    private async Task ExecuteSurpriseGreetingAsync(string mode, HeartbeatOptions config, CancellationToken ct)
    {
        // 夜间心跳检查
        if (!config.EnableNightHeartbeat && IsNightTime())
        {
            _logger.LogInformation("夜间心跳已禁用，跳过");
            await AppendLogAsync("greeting", false, "nightSkip", 0, config.MaxLogEntries, null);
            return;
        }

        // 获取启用了系统消息的会话
        var targetSession = await _sessionService.GetSystemSentSessionAsync();
        if (targetSession is null)
        {
            _logger.LogInformation("没有启用 SystemSent 的会话，跳过心跳");
            await AppendLogAsync("greeting", false, "noSession", 0, config.MaxLogEntries, null);
            return;
        }

        // 计算会话闲置时间
        var updateTime = DateTime.FromFileTimeUtc(targetSession.UpdateAt);
        var idleMinutes = (DateTime.UtcNow - updateTime).TotalMinutes;

        // 读取思念值
        var longing = 0;
        var longingStr = (await _stateService.GetStateAsync(LongingKey))?.Value;
        if (!string.IsNullOrEmpty(longingStr) && int.TryParse(longingStr, out var parsed))
            longing = parsed;

        // ── 三档分区 ──

        // 🔥 炽热区：跳过所有，思念值不变，不触发问候
        if (idleMinutes < config.HotZoneMinutes)
        {
            _logger.LogInformation("炽热区（闲置 {Idle:F1} 分钟 < HotZone {HotZone}），跳过心跳",
                idleMinutes, config.HotZoneMinutes);
            await AppendLogAsync("hotZoneSkip", false, "idle", longing, config.MaxLogEntries, targetSession.Key);
            return;
        }

        // 🌤️ 温存区：只增加思念值，不触发问候
        if (idleMinutes < config.CooldownMinutes)
        {
            var random = Random.Shared;
            var isSpecial = mode == "special";
            var increase = isSpecial
                ? random.Next(config.SpecialIncreaseMin, config.SpecialIncreaseMax + 1)
                : random.Next(config.NormalIncreaseMin, config.NormalIncreaseMax + 1);
            longing = Math.Min(longing + increase, config.LongingCap);
            await _stateService.UpdateStateAsync(LongingKey, longing.ToString());

            _logger.LogInformation("温存区（闲置 {Idle:F1} 分钟），思念值 +{Increase} = {Longing}，跳过问候",
                idleMinutes, increase, longing);
            await AppendLogAsync("warmZone", false, "longing", longing, config.MaxLogEntries, targetSession.Key);
            return;
        }

        // ❄️ 冷宫区：完整心跳逻辑（增加思念值 + 问候判定）
        var result = await ProcessGreetingAsync(longing, mode, config, ct);
        await _stateService.UpdateStateAsync(LongingKey, result.Longing.ToString());

        if (result.Triggered && !string.IsNullOrEmpty(result.PromptContent))
        {
            var message = $"[系统消息]可先使用State状态相关工具，查询和更新角色状态。{result.PromptContent}[/]";
            await _receiveChannel.WriteAsync(new ReceivedMessage(targetSession.Key, targetSession.ChannelId, message), ct);
            _logger.LogInformation("冷宫区触发惊喜问候到 {Session}", targetSession.Key);
        }
        else
        {
            _logger.LogInformation("冷宫区（闲置 {Idle:F1} 分钟），思念值 {Longing}，未触发",
                idleMinutes, result.Longing);
        }

        await AppendLogAsync("greeting", result.Triggered, result.PromptType, result.Longing,
            config.MaxLogEntries, targetSession.Key);
    }

    private async Task<HeartbeatResult> ProcessGreetingAsync(int longing, string mode, HeartbeatOptions config,
        CancellationToken ct)
    {
        var random = Random.Shared;
        var isSpecial = mode == "special";

        // 增加思念值
        var increase = isSpecial
            ? random.Next(config.SpecialIncreaseMin, config.SpecialIncreaseMax + 1)
            : random.Next(config.NormalIncreaseMin, config.NormalIncreaseMax + 1);

        longing = Math.Min(longing + increase, config.LongingCap);

        // 特殊规则：低于阈值不触发
        if (isSpecial && longing < config.SpecialTriggerThreshold)
        {
            return new HeartbeatResult { Longing = longing, Triggered = false };
        }

        if (longing < 0)
        {
            return new HeartbeatResult { Longing = longing, Triggered = false };
        }

        // 判断触发概率
        var triggerChance = longing;
        var roll = random.Next(config.LongingCap) + 1;

        if (roll > triggerChance)
        {
            return new HeartbeatResult { Longing = longing, Triggered = false };
        }

        // 触发！扣除惩罚值
        var triggeredLonging = longing;
        longing -= config.TriggerPenalty;

        // 计算等级
        var level = GetLevel(triggeredLonging, config);

        // 选择类型
        var (promptType, promptContent) = await SelectPromptAsync(isSpecial, level, random, ct);

        return new HeartbeatResult
        {
            Longing = longing,
            Triggered = true,
            PromptType = promptType,
            PromptContent = promptContent
        };
    }

    #endregion

    // #region 状态更新消息构建

    // private async Task<string> BuildStateUpdateMessageAsync()
    // {
    //     var states = await _stateService.GetAllStatesAsync();
    //     var sb = new StringBuilder();
    //
    //     sb.AppendLine("[系统消息]");
    //     sb.AppendLine("# 当前状态");
    //     sb.AppendLine();
    //
    //     foreach (var state in states)
    //     {
    //         var editable = state.AgentEditable ? "可修改" : "不可修改";
    //         sb.AppendLine($"- {state.Name}({state.Key}): {state.Value} [{editable}]");
    //         if (!string.IsNullOrWhiteSpace(state.Description))
    //             sb.AppendLine($"  说明: {state.Description}");
    //     }
    //
    //     sb.AppendLine();
    //     sb.AppendLine("# 指令");
    //     sb.AppendLine("请根据当前时间和人设，使用工具更新需要变化的状态（如场景、服装等）。");
    //     sb.AppendLine("仅修改\"可修改\"的状态，\"不可修改\"的状态禁止修改。");
    //     sb.AppendLine("使用 key 调用工具修改状态。");
    //     sb.AppendLine();
    //     sb.AppendLine("⚠️ 此消息为系统消息，不要输出<Content>正文回复，不要发送任何角色消息。");
    //     sb.AppendLine("[/]");
    //
    //     return sb.ToString();
    // }
    //
    // #endregion

    #region 辅助方法

    // private static bool IsTrueValue(string? value)
    // {
    //     if (string.IsNullOrWhiteSpace(value)) return false;
    //     return value.Trim() is "true" or "yes" or "是" or "1" or "on";
    // }

    private static bool IsNightTime()
    {
        var hour = DateTime.Now.Hour;
        // 23:00 ~ 06:30
        return hour >= 23 || hour < 6 || (hour == 6 && DateTime.Now.Minute < 30);
    }

    private static int GetLevel(int missYouValue, HeartbeatOptions config)
    {
        var random = Random.Shared;
        var thresholds = config.LevelThresholds;
        var maxLevel = thresholds.Count;

        var baseLevel = 0;
        for (var i = thresholds.Count - 1; i >= 0; i--)
        {
            if (missYouValue >= thresholds[i])
            {
                baseLevel = i + 1;
                break;
            }
        }

        if (random.Next(100) < config.LevelFluctuationChance)
        {
            var delta = random.Next(2) == 0 ? 1 : -1;
            baseLevel = Math.Clamp(baseLevel + delta, 0, maxLevel);
        }

        return baseLevel;
    }

    private async Task<(string Type, string Content)> SelectPromptAsync(bool isSpecial, int level, Random random,
        CancellationToken ct)
    {
        var prompts = await LoadPromptsAsync();
        var levels = new[] { "l1", "l2", "l3", "l4", "l5" };
        var levelKey = level < levels.Length ? levels[level] : levels[^1];

        string type;
        Dictionary<string, string> dict;

        if (isSpecial)
        {
            var isVoice = random.Next(2) == 0;
            type = isVoice ? "voice" : "image";
            dict = isVoice ? prompts.Voice : prompts.Image;
        }
        else
        {
            var isText = random.Next(2) == 0;
            if (isText)
            {
                var isStory = random.Next(2) == 0;
                type = isStory ? "story" : "text";
                dict = isStory ? prompts.Story : prompts.Text;
            }
            else
            {
                var isVoice = random.Next(2) == 0;
                type = isVoice ? "voice" : "image";
                dict = isVoice ? prompts.Voice : prompts.Image;
            }
        }

        if (dict.TryGetValue(levelKey, out var content) && !string.IsNullOrEmpty(content))
            return (type, content);

        return (type, "请以角色身份主动发送一条消息，以表达思念。");
    }

    private async Task<HeartbeatPrompts> LoadPromptsAsync()
    {
        try
        {
            var heartbeatPrompts = await _fileService.ReadJsonAsync<HeartbeatPrompts>(PromptsFile);
            return heartbeatPrompts ?? new HeartbeatPrompts();
        }
        catch
        {
            return new HeartbeatPrompts();
        }
    }

    private async Task AppendLogAsync(string type, bool triggered, string promptType, int longing,
        int maxLogEntries, string? sessionKey)
    {
        try
        {
            var logPath = Path.Combine(_runtimeOptions.Value.Workspace, LogFile);
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

            var entry = new
            {
                Time = DateTime.UtcNow.ToString("O"),
                Type = type,
                Triggered = triggered,
                PromptType = promptType,
                Longing = longing,
                SessionKey = sessionKey ?? "none"
            };

            var line = JsonSerializer.Serialize(entry);
            await File.AppendAllTextAsync(logPath, line + Environment.NewLine);

            await TrimLogAsync(logPath, maxLogEntries);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "心跳日志写入失败");
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

    private record HeartbeatResult
    {
        public int Longing { get; init; }
        public bool Triggered { get; init; }
        public string PromptType { get; init; } = "";
        public string PromptContent { get; init; } = "";
    }

    #endregion
}