using System.Text;
using System.Text.RegularExpressions;
using Harem.Contracts.Configurations;
using Harem.Contracts.Configurations.AgentWorkspace;
using Harem.Contracts.Domain.Entities;
using Harem.Services.Abstractions.Agents;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Harem.Services.Agents;

public class ContextService : IContextService
{
    private const int MaxRecentRounds = 3;


    private const string AgentsMd = "AGENTS.md";
    private const string SoulMd = "SOUL.md";
    private const string WorldMd = "WORLD.md";
    private const string MemoryMd = "MEMORY.md";
    private const string UserMd = "USER.md";
    private const string MemoriesDirectory = "memories";
    private const string DefaultSummary = "[系统提示]这是一个全新的开始，还没有历史摘要。请按要求输出摘要内容[/]";
    private readonly IFileService _fileService;
    private readonly IHistoryService _historyService;
    private readonly IStateService _stateService;
    private readonly IMemoryService _memoryService;
    private readonly INpcService _npcService;
    private readonly ICronService _cronService;
    private readonly IKeywordService _keywordService;
    private readonly RuntimeOptions _runtimeOptions;
    private readonly AgentOptions _agentOptions;
    private readonly ILogger<ContextService> _logger;


    public ContextService(IFileService fileService, IHistoryService historyService,
        INpcService npcService, IMemoryService memoryService, IStateService stateService,
        ICronService cronService, IKeywordService keywordService,
        IOptions<RuntimeOptions> runtimeOptions, IOptions<AgentOptions> agentOptions,
        ILogger<ContextService> logger)
    {
        _fileService = fileService;
        _historyService = historyService;
        _npcService = npcService;
        _memoryService = memoryService;
        _stateService = stateService;
        _cronService = cronService;
        _keywordService = keywordService;
        _runtimeOptions = runtimeOptions.Value;
        _agentOptions = agentOptions.Value;
        _logger = logger;
    }

    public async Task<(List<ChatMessage> Messages, ChatMessage InputMessage)> BuildContextMessages(ChatHistory history,
        List<ChatRawMessage<ChatMessage>> rawMessages,
        string userMessage, string sessionId, string sessionKey)
    {
        var messages = new List<ChatMessage>();

        // 1. 预处理用户消息：标签解析
        var parsedMessages = TaggedHandler(userMessage);

        // 2. 收集上下文注入（内聚在此，调用方无需手动调 Inject*）
        var injectMessages = new List<ChatMessage>();
        injectMessages.AddRange(await InjectStateAsync());
        injectMessages.AddRange(await InjectMemoryConsolidationAsync(sessionId,
            parsedMessages.Any(m => m.Role == ChatRole.User)));
        injectMessages.AddRange(await InjectMatchedNpcsAsync(userMessage, sessionId));
        injectMessages.AddRange(await InjectMemoryAsync(userMessage));
        injectMessages.AddRange(await InjectCronResultsAsync(sessionId));

        var agentsMd = await _fileService.GetFileAsync(AgentsMd);
        var soulMd = await _fileService.GetFileAsync(SoulMd);
        var worldMd = await _fileService.GetFileAsync(WorldMd);
        var memoryMd = await _fileService.GetFileAsync(MemoryMd);
        var userMd = await _fileService.GetFileAsync(UserMd);

        // 1. 基础背景区：系统提示词
        messages.AddRange([
            new ChatMessage(ChatRole.System, agentsMd)
            {
                AuthorName = "Agents"
            },
            new ChatMessage(ChatRole.System, soulMd)
            {
                AuthorName = "Soul"
            },
            new ChatMessage(ChatRole.System, worldMd)
            {
                AuthorName = "World"
            },
            new ChatMessage(ChatRole.System, memoryMd)
            {
                AuthorName = "Memory"
            },
            new ChatMessage(ChatRole.System, userMd)
            {
                AuthorName = "UserProfile"
            }
        ]);

        // 2. 摘要区
        var summaryText = await ResolveSummaryAsync(sessionId);
        messages.Add(new ChatMessage(ChatRole.User, summaryText)
        {
            AuthorName = "Summary"
        });

        // // 3. 关键词区（根据用户输入 + 会话已触发关键词匹配）
        // var storedKeywords = await _historyService.GetHistoryBag<List<string>>(
        //     sessionId, Constants.BagKeys.TriggeredKeywords) ?? [];
        // var retentionRound = await _historyService.GetHistoryBag<int>(
        //     sessionId, Constants.BagKeys.KeywordRetentionRound);

        // // 从最近N轮中取当前轮次编号
        // var currentRound = rawMessages.Count > 0
        //     ? rawMessages.Max(r => r.RoundId)
        //     : 0;

        // // 超期清理：超过 MaxRetentionRounds 轮未触发则清空
        // var previousRound = retentionRound;
        // if (previousRound > 0 && currentRound - previousRound >= Constants.KeywordConfig.MaxRetentionRounds
        //     && storedKeywords.Count > 0)
        // {
        //     _logger.LogInformation("关键词已超过 {Rounds} 轮未触发，自动过期清除",
        //         Constants.KeywordConfig.MaxRetentionRounds);
        //     storedKeywords = [];
        // }
        //
        // var (keywordMessages, newTriggered) =
        //     await _keywordService.ResolveKeywordInjectionsAsync(userMessage, storedKeywords);
        // messages.AddRange(keywordMessages);
        //
        // // 合并本轮新触发的关键词并持久化
        // var mergedKeywords = storedKeywords
        //     .Concat(newTriggered)
        //     .Distinct(StringComparer.OrdinalIgnoreCase)
        //     .ToList();
        //
        // // 有新的关键词触发时更新轮次计数器
        // if (newTriggered.Count > 0)
        // {
        //     await _historyService.SetHistoryBag(sessionId, Constants.BagKeys.KeywordRetentionRound, currentRound);
        //     await _historyService.SetHistoryBag(sessionId, Constants.BagKeys.TriggeredKeywords, mergedKeywords);
        //     _logger.LogDebug("关键词轮次已更新: Round={Round}, Count={Count}", currentRound, mergedKeywords.Count);
        // }
        // else if (storedKeywords.Count > 0)
        // {
        //     // 无新触发但有保留关键词，仅检查轮次不更新
        //     await _historyService.SetHistoryBag(sessionId, Constants.BagKeys.KeywordRetentionRound, currentRound);
        // }

        // 4. 动态注入区（基于当前输入的实时上下文增强）
        var formatInjectMessages = injectMessages
            .Where(m => m.Role == ChatRole.System)
            .Select(m => new ChatMessage(ChatRole.System, m.Text)
            {
                AuthorName = "SystemTips",
                CreatedAt = m.CreatedAt
            })
            .ToList();
        messages.AddRange(formatInjectMessages);

        // 5. 历史消息区：排除最近N轮对应的消息，然后 TakeLast(100)
        var recentRoundsCount = rawMessages.Count;
        var historyMessages = history.Messages
            .Select(h => new ChatMessage(h.Role == HistoryRole.User ? ChatRole.User : ChatRole.Assistant, h.Text)
            {
                CreatedAt = h.CreatedAt
            })
            .OrderBy(h => h.CreatedAt)
            .SkipLast(recentRoundsCount * 2)
            .TakeLast(100);

        messages.AddRange(historyMessages);

        // 6. 完整过程调用区：最近N轮（含工具调用）
        foreach (var round in rawMessages)
        {
            messages.AddRange(round.Request);
            messages.AddRange(round.Response);
        }

        // 7. 用户新输入内容
        var systemMessages = parsedMessages.Where(x => x.Role == ChatRole.System)
            .Select(x => $"<SystemMessage>{x.Text}</SystemMessage>").ToList();
        var userMessages = parsedMessages.Where(x => x.Role == ChatRole.User)
            .Select(x => $"<UserMessage>{x.Text}</UserMessage>").ToList();

        var now = DateTimeOffset.Now;
        var timePrompt = $"[消息时间: {now:yyyy-MM-dd(ddd) HH:mm} {now.Offset}]";
        var inputMessage = new ChatMessage(ChatRole.User,
            $"{string.Join("", systemMessages)}" +
            $"<Time>{timePrompt}</Time>" +
            $"{string.Join("", userMessages)}");
        messages.Add(inputMessage);

        return (messages, inputMessage);
    }

    public async Task<(List<ChatMessage> Messages, ChatMessage InputMessage)> BuildSingleSystemPromptContextMessages(
        ChatHistory history,
        List<ChatRawMessage<ChatMessage>> rawMessages,
        string userMessage, string sessionId, string sessionKey)
    {
        var messages = new List<ChatMessage>();

        // 1. 预处理用户消息：标签解析
        var parsedMessages = TaggedHandler(userMessage);

        // 2. 收集上下文注入
        var injectMessages = new List<ChatMessage>();
        injectMessages.AddRange(await InjectStateAsync());
        injectMessages.AddRange(await InjectMemoryConsolidationAsync(sessionId,
            parsedMessages.Any(m => m.Role == ChatRole.User)));
        injectMessages.AddRange(await InjectMatchedNpcsAsync(userMessage, sessionId));
        injectMessages.AddRange(await InjectMemoryAsync(userMessage));
        injectMessages.AddRange(await InjectCronResultsAsync(sessionId));

        // 3. 读取所有系统提示源
        var agentsMd = await _fileService.GetFileAsync(AgentsMd);
        var soulMd = await _fileService.GetFileAsync(SoulMd);
        var worldMd = await _fileService.GetFileAsync(WorldMd);
        var memoryMd = await _fileService.GetFileAsync(MemoryMd);
        var userMd = await _fileService.GetFileAsync(UserMd);

        // 4. 合并为一条 system prompt
        //    原 ChatOptions.Instructions 的内容（工具使用规则）内嵌在开头，
        //    然后 AGENTS.md / SOUL.md / WORLD.md / MEMORY.md / USER.md 顺序拼接。
        //    不加 AuthorName，避免 name 字段干扰模型对 system 消息的理解。
        var sb = new StringBuilder();
        sb.AppendLine($"你是角色扮演机器人，正在扮演**{_agentOptions.Name}**和用户聊天。");
        sb.AppendLine();
        sb.AppendLine("【核心规则】你是一个大型语言模型(LLM)，拥有调用工具的能力。提供的工具列表中的函数你可以直接调用，调用后工具会实际执行并返回结果。");
        sb.AppendLine("当你想做什么事（搜索记忆、发语音、生成图片、写文件、网络搜索等）时，直接调用对应的工具函数，不要在回复中用文字描述你要做什么。");
        sb.AppendLine("在回复正文中用文字说「我搜索了记忆」不等于真的搜索了记忆。只有调用 search_memory 工具才是真的搜索。");
        sb.AppendLine("你调用工具后，系统会自动执行并把结果给你，你再根据结果继续回复。");
        sb.AppendLine("如果不确定该不该调工具，就调——调了至少有可能对，不调一定不对。");
        sb.AppendLine();
        sb.AppendLine("【可用工具简表】");
        sb.AppendLine("- 记忆/文件: search_memory, get_memory, append_memory, write_memory, read_file, write_file");
        sb.AppendLine("- 媒体: voice(tts), camera/gallery(图片), random_outfit(换装), generate_scenery_image(场景)");
        sb.AppendLine("- 状态/NPC: get_states, update_state, add_state, search_npc, create_npc, append_npc_memory");
        sb.AppendLine("- 搜索/外部: web_search, clio_chat(Hermes), mmx_text_chat, mimo_voice/help");
        sb.AppendLine(
            "- 定时/游戏: create_cron_job, game_add/get/save/delete_level, game_create/list/get/update/delete_game_type");
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine(agentsMd);
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine(soulMd);
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine(worldMd);
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine(memoryMd);
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine(userMd);

        messages.Add(new ChatMessage(ChatRole.System, sb.ToString()));

        // 5. 摘要区——不加 AuthorName
        var summaryText = await ResolveSummaryAsync(sessionId);
        messages.Add(new ChatMessage(ChatRole.User, summaryText));

        // // 6. 关键词区——剥离 AuthorName
        // var storedKeywords = await _historyService.GetHistoryBag<List<string>>(
        //     sessionId, Constants.BagKeys.TriggeredKeywords) ?? [];
        // var retentionRound = await _historyService.GetHistoryBag<int>(
        //     sessionId, Constants.BagKeys.KeywordRetentionRound);
        //
        // var currentRound = rawMessages.Count > 0
        //     ? rawMessages.Max(r => r.RoundId)
        //     : 0;
        //
        // var previousRound = retentionRound;
        // if (previousRound > 0 && currentRound - previousRound >= Constants.KeywordConfig.MaxRetentionRounds
        //     && storedKeywords.Count > 0)
        // {
        //     _logger.LogInformation("关键词已超过 {Rounds} 轮未触发，自动过期清除",
        //         Constants.KeywordConfig.MaxRetentionRounds);
        //     storedKeywords = [];
        // }
        //
        // var (keywordMessages, newTriggered) =
        //     await _keywordService.ResolveKeywordInjectionsAsync(userMessage, storedKeywords);
        // // 剥离 keyword 消息的 AuthorName——name 字段干扰模型
        // foreach (var km in keywordMessages)
        // {
        //     messages.Add(new ChatMessage(km.Role, km.Text) { CreatedAt = km.CreatedAt });
        // }
        //
        // var mergedKeywords = storedKeywords
        //     .Concat(newTriggered)
        //     .Distinct(StringComparer.OrdinalIgnoreCase)
        //     .ToList();
        //
        // if (newTriggered.Count > 0)
        // {
        //     await _historyService.SetHistoryBag(sessionId, Constants.BagKeys.KeywordRetentionRound, currentRound);
        //     await _historyService.SetHistoryBag(sessionId, Constants.BagKeys.TriggeredKeywords, mergedKeywords);
        //     _logger.LogDebug("关键词轮次已更新: Round={Round}, Count={Count}", currentRound, mergedKeywords.Count);
        // }
        // else if (storedKeywords.Count > 0)
        // {
        //     await _historyService.SetHistoryBag(sessionId, Constants.BagKeys.KeywordRetentionRound, currentRound);
        // }

        // 7. 动态注入区——不加 AuthorName
        foreach (var inj in injectMessages.Where(m => m.Role == ChatRole.System))
        {
            messages.Add(new ChatMessage(ChatRole.System, inj.Text)
            {
                CreatedAt = inj.CreatedAt
            });
        }

        // 8. 历史消息区
        var recentRoundsCount = rawMessages.Count;
        var historyMessages = history.Messages
            .Select(h => new ChatMessage(h.Role == HistoryRole.User ? ChatRole.User : ChatRole.Assistant, h.Text)
            {
                CreatedAt = h.CreatedAt
            })
            .OrderBy(h => h.CreatedAt)
            .SkipLast(recentRoundsCount * 2)
            .TakeLast(100);

        messages.AddRange(historyMessages);

        // 9. 完整过程调用区
        foreach (var round in rawMessages)
        {
            messages.AddRange(round.Request);
            messages.AddRange(round.Response);
        }

        // 10. 用户新输入
        var systemMessages = parsedMessages.Where(x => x.Role == ChatRole.System)
            .Select(x => $"<SystemMessage>{x.Text}</SystemMessage>").ToList();
        var userMessages = parsedMessages.Where(x => x.Role == ChatRole.User)
            .Select(x => $"<UserMessage>{x.Text}</UserMessage>").ToList();

        var now = DateTimeOffset.Now;
        var timePrompt = $"[消息时间: {now:yyyy-MM-dd(ddd) HH:mm} {now.Offset}]";
        var inputMessage = new ChatMessage(ChatRole.User,
            $"{string.Join("", systemMessages)}" +
            $"<Time>{timePrompt}</Time>" +
            $"{string.Join("", userMessages)}");
        messages.Add(inputMessage);

        return (messages, inputMessage);
    }

    private async Task<string> ResolveSummaryAsync(string sessionId)
    {
        // 从 summary.json 读取摘要
        try
        {
            var summaryPath = Path.Combine(_runtimeOptions.Workspace, ".history", sessionId, "summary.json");
            if (File.Exists(summaryPath))
            {
                var state = await _fileService.ReadJsonAsync<SummaryState>(summaryPath);
                if (state?.HasEverBeenSummarized == true && !string.IsNullOrWhiteSpace(state.CurrentSummary))
                {
                    return $"<Summary>{state.CurrentSummary}</Summary>";
                }
            }
        }
        catch
        {
            // 忽略读取失败，走记忆 fallback
        }

        // 没有摘要时，尝试从历史记忆文件提取最近2天
        try
        {
            var memoriesDir = Path.Combine(_runtimeOptions.Workspace, MemoriesDirectory);
            if (!Directory.Exists(memoriesDir))
                return DefaultSummary;

            var memoryFiles = Directory.GetFiles(memoriesDir, "*.md")
                .Select(f => new FileInfo(f))
                .Where(f => f.Name.EndsWith(".md") && f.Name.Length >= 13)
                .OrderByDescending(f => f.Name)
                .Take(2)
                .ToList();

            if (memoryFiles.Count == 0)
                return DefaultSummary;

            var sb = new StringBuilder();
            sb.AppendLine("[系统提示]暂无历史摘要，但存在以下历史记忆，请根据历史记忆提炼摘要内容[/]");
            sb.AppendLine();

            foreach (var file in memoryFiles)
            {
                var date = file.Name[..10];
                var content = await _fileService.GetFileAsync(Path.Combine(MemoriesDirectory, file.Name));
                if (!string.IsNullOrWhiteSpace(content))
                {
                    sb.AppendLine($"## {date}");
                    sb.AppendLine(content);
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "从历史记忆提取迁移摘要失败，使用默认摘要");
            return DefaultSummary;
        }
    }

    #region 最近N轮存储

    /// <summary>
    /// 提取本轮完整交互过程（含工具调用），追加到 rounds.json，保持最多 MaxRecentRounds 轮
    /// </summary>
    public async Task SaveRecentRound(string sessionId, ChatMessage inputMessage)
    {
        try
        {
            var rounds =
                await _historyService.GetHistoryBag<List<ChatRawMessage<ChatMessage>>>(sessionId,
                    Constants.BagKeys.Rounds)
                ?? [];

            var responseMessages =
                await _historyService.GetHistoryBag<List<ChatMessage>>(sessionId, Constants.BagKeys.CurrentResponse);
            if (responseMessages is not { Count: > 0 }) return;

            rounds.Add(new ChatRawMessage<ChatMessage>
            {
                RoundId = rounds.Count > 0 ? rounds.Max(r => r.RoundId) + 1 : 1,
                Request = [inputMessage],
                Response = responseMessages
            });

            while (rounds.Count > MaxRecentRounds)
                rounds.RemoveAt(0);

            await _historyService.SetHistoryBag(sessionId, Constants.BagKeys.Rounds, rounds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "保存最近N轮完整交互失败");
        }
    }

    #endregion

    #region 消息预处理

    private List<ChatMessage> TaggedHandler(string message)
    {
        var result = new List<ChatMessage>();
        var pattern = @"\[.+?\].+?\[/\]";

        var tagBlockList = new List<string>();
        var temp = message;

        var matches = Regex.Matches(message, pattern, RegexOptions.Singleline);
        foreach (Match m in matches)
        {
            tagBlockList.Add(m.Value);
            temp = temp.Replace(m.Value, "|SPLIT|");
        }

        var normalTexts = temp.Split(["|SPLIT|"], StringSplitOptions.RemoveEmptyEntries);

        var tagResult = string.Join(Environment.NewLine, tagBlockList);
        var textResult = string.Join(Environment.NewLine, normalTexts);
        if (!string.IsNullOrEmpty(tagResult))
            result.Add(new ChatMessage(ChatRole.System, tagResult)
            {
                CreatedAt = DateTimeOffset.UtcNow
            });
        if (!string.IsNullOrEmpty(textResult))
            result.Add(new ChatMessage(ChatRole.User, textResult) { CreatedAt = DateTimeOffset.UtcNow });
        return result;
    }

    #endregion

    #region 上下文注入

    private async Task<List<ChatMessage>> InjectStateAsync()
    {
        var result = new List<ChatMessage>();
        try
        {
            var stateItems = await _stateService.GetAllStatesAsync();
            var injections = stateItems
                .Where(stateItem => stateItem.InInjectPrompt)
                .Select(stateItem =>
                    $"<State>状态：**{stateItem.Key}({stateItem.Name})**的状态值为`{stateItem.Value}`。状态描述：`{stateItem.Description}`。请严格按照状态要求的内容执行！</State>");

            result.Add(new ChatMessage(ChatRole.System,
                string.Join(Environment.NewLine, injections))
            {
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "状态强制注入失败");
        }

        return result;
    }

    async Task<List<ChatMessage>> InjectMemoryConsolidationAsync(string sessionId, bool hasUserMessage)
    {
        if (!hasUserMessage) return [];

        try
        {
            var pending = await _historyService.GetHistoryBag<bool?>(sessionId, "pendingMemoryConsolidation");
            if (pending != true) return [];

            var summary = await _historyService.GetHistoryBag<string>(sessionId, "pendingSummary") ?? "";
            if (string.IsNullOrWhiteSpace(summary)) return [];

            // 清除标记，避免重复触发
            await _historyService.SetHistoryBag(sessionId, "pendingMemoryConsolidation", false);
            await _historyService.SetHistoryBag(sessionId, "pendingSummary", "");

            var prompt =
                "[系统指令]当前会话有待整理的历史摘要。请在开始回复前，先使用 GetMemory 读取现有长期记忆，然后使用 AppendMemory 或 WriteMemory 更新 MEMORY.md。\n\n"
                + $"当前历史摘要：\n{summary}\n\n"
                + "注意：这个摘要是持续累积的，包含了此前所有对话的要点，不只是今天的内容。\n\n"
                + "更新策略：\n"
                + "- 使用 AppendMemory：仅在已有内容后追加新内容\n"
                + "- 使用 WriteMemory：当需要重组、合并、修正或删除已有记忆时使用（全量覆盖写入）\n\n"
                + "更新要求：\n"
                + "1. 读取现有 MEMORY.md，在此基础上补充更新\n"
                + "2. 摘要和 MEMORY.md 可以有重复内容，不要把摘要精简完——摘要供下一轮对话使用（保持连续性），MEMORY.md 供长期回溯使用\n"
                + "3. 保留关键人物、事件、决策和状态变化\n"
                + "4. 随着剧情或人物关系发展，可以调整或修正已有记忆";

            return [new ChatMessage(ChatRole.System, prompt) { CreatedAt = DateTimeOffset.UtcNow }];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "记忆整理注入失败");
            return [];
        }
    }

    async Task<List<ChatMessage>> InjectMatchedNpcsAsync(string userMessage, string sessionId)
    {
        var messages = new List<ChatMessage>();
        try
        {
            var recentlyInjected =
                await _historyService.GetHistoryBag<Dictionary<string, DateTime>>(sessionId,
                    Constants.BagKeys.RecentNpcs)
                ?? new Dictionary<string, DateTime>();

            var injections = await _npcService.ResolveNpcInjectionsAsync(userMessage, recentlyInjected);

            if (injections.Count == 0) return [];

            foreach (var injection in injections)
            {
                messages.Add(new ChatMessage(ChatRole.System, injection.Prompt) { CreatedAt = DateTimeOffset.UtcNow });
                recentlyInjected[injection.NpcKey] = DateTime.UtcNow;
            }

            await _historyService.SetHistoryBag(sessionId, Constants.BagKeys.RecentNpcs, recentlyInjected);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "NPC 注入失败");
        }

        return messages;
    }

    async Task<List<ChatMessage>> InjectMemoryAsync(string userMessage)
    {
        var result = new List<ChatMessage>();
        try
        {
            var injections = await _memoryService.ResolveMemoryInjectionsAsync(userMessage);

            result.AddRange(injections.Select(injection => new ChatMessage(ChatRole.System, injection.Prompt)
                { CreatedAt = DateTimeOffset.UtcNow }));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "记忆强制注入失败");
        }

        return result;
    }

    /// <summary>
    /// 注入定时任务执行结果（下次用户发消息时消费，一次性）
    /// </summary>
    async Task<List<ChatMessage>> InjectCronResultsAsync(string sessionId)
    {
        try
        {
            var results = await _cronService.ConsumePendingResultsAsync(sessionId);
            if (results == null || results.Count == 0)
                return [];

            var prompt = "[定时任务执行记录]\n" + string.Join("\n", results) + "\n[/]";
            return [new ChatMessage(ChatRole.System, prompt) { CreatedAt = DateTimeOffset.UtcNow }];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "定时任务执行结果注入失败");
            return [];
        }
    }

    #endregion

    #region 摘要服务上下文

    /// <summary>
    /// 获取摘要服务所需的背景上下文（SOUL.md / WORLD.md / MEMORY.md / USER.md）
    /// </summary>
    public async Task<string> GetSummaryBaseContextAsync()
    {
        var sb = new StringBuilder();
        foreach (var file in new[] { "SOUL.md", "WORLD.md", "MEMORY.md", "USER.md" })
        {
            var content = await _fileService.GetFileAsync(file);
            if (!string.IsNullOrWhiteSpace(content))
            {
                sb.AppendLine($"## {file.Replace(".md", "")}");
                sb.AppendLine(content);
            }
        }

        return sb.ToString();
    }

    #endregion
}