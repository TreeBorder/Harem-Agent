using System.ClientModel;
using System.Text;
using Anthropic;
using Harem.Contracts.Configurations;
using Harem.Contracts.Configurations.AgentWorkspace;
using Harem.Contracts.Domain.Entities;
using Harem.Services.Abstractions.Agents;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using ChatOptions = Microsoft.Extensions.AI.ChatOptions;

namespace Harem.Services.Agents;

public class SummaryService : ISummaryService
{
    private const string SummaryFilename = "summary.json";

    private readonly IFileService _fileService;
    private readonly IHistoryService _historyService;
    private readonly IAgentService _agentService;
    private readonly IContextService _contextService;
    private readonly string _workspaceDirectory;
    private readonly AgentOptions _agentOptions;
    private readonly ILogger<SummaryService> _logger;

    public SummaryService(
        IFileService fileService,
        IHistoryService historyService,
        IAgentService agentService,
        IContextService contextService,
        IOptions<RuntimeOptions> runtimeOptions,
        IOptions<AgentOptions> agentOptions,
        ILogger<SummaryService> logger)
    {
        _fileService = fileService;
        _historyService = historyService;
        _agentService = agentService;
        _contextService = contextService;
        _workspaceDirectory = runtimeOptions.Value.Workspace;
        _agentOptions = agentOptions.Value;
        _logger = logger;
    }

    public async Task GenerateIncrementalSummaryAsync(string sessionId)
    {
        try
        {
            var state = await GetSummaryStateAsync(sessionId);
            var history = await _historyService.GetHistoryAsync(sessionId);

            // 筛选自上次摘要后的新消息
            List<HistoryMessage> newMessages;
            List<HistoryMessage> redundantMessages = [];
            if (state.LastSummarizedAt.HasValue)
            {
                newMessages = history.Messages
                    .Where(m => m.CreatedAt > state.LastSummarizedAt.Value)
                    .OrderBy(m => m.CreatedAt)
                    .ToList();

                // 已摘要的原始消息中取最近一组（约30轮 = 60条）作为冗余参考
                redundantMessages = history.Messages
                    .Where(m => m.CreatedAt <= state.LastSummarizedAt.Value)
                    .OrderBy(m => m.CreatedAt)
                    .TakeLast(60)
                    .ToList();
            }
            else
            {
                // 从未摘要过：取最近3天（含当天）的消息，太早的消息没必要纳入摘要
                var threeDaysAgo = DateTime.UtcNow.Date.AddDays(-2);
                newMessages = history.Messages
                    .Where(m => m.CreatedAt >= threeDaysAgo)
                    .OrderBy(m => m.CreatedAt)
                    .ToList();
            }

            if (newMessages.Count == 0)
            {
                _logger.LogDebug("会话 {SessionId} 没有新消息需要摘要", sessionId);
                return;
            }

            // 构建摘要请求上下文
            var context = await BuildSummaryContextAsync(
                state.CurrentSummary, redundantMessages, newMessages, state.HasEverBeenSummarized);
            var newSummary = await CallSummaryLlmAsync(context);

            if (string.IsNullOrWhiteSpace(newSummary))
            {
                _logger.LogWarning("摘要 LLM 返回空内容，跳过更新");
                return;
            }

            // 更新状态
            // state.CurrentSummary = newSummary;
            state.LastSummarizedAt = newMessages.Last().CreatedAt;
            state.HasEverBeenSummarized = true;

            // 在摘要末尾追加时间戳标记（代码控制，不由 LLM 输出）
            var localTime = state.LastSummarizedAt.Value.ToLocalTime();
            var summaryWithTimestamp = $"{newSummary}\n\n[SummaryUpdated]{localTime:yyyy-MM-dd HH:mm zzz}[/SummaryUpdated]";
            state.CurrentSummary = summaryWithTimestamp;
            await SaveSummaryStateAsync(sessionId, state);

            _logger.LogInformation(
                "会话 {SessionId} 摘要已更新，共 {NewCount} 条新消息，最后时间 {LastTime}",
                sessionId, newMessages.Count, state.LastSummarizedAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成会话 {SessionId} 增量摘要失败", sessionId);
        }
    }

    public async Task ForceRegenerateSummaryAsync(string sessionId)
    {
        try
        {
            var history = await _historyService.GetHistoryAsync(sessionId);
            var threeDaysAgo = DateTime.UtcNow.Date.AddDays(-2);
            var allMessages = history.Messages
                .Where(m => m.CreatedAt >= threeDaysAgo)
                .OrderBy(m => m.CreatedAt)
                .ToList();

            if (allMessages.Count == 0)
            {
                _logger.LogDebug("会话 {SessionId} 没有消息，跳过强制重生成", sessionId);
                return;
            }

            // 无历史摘要，全部消息作为新消息处理
            var context = await BuildSummaryContextAsync(string.Empty, [], allMessages, false);
            var newSummary = await CallSummaryLlmAsync(context);

            if (string.IsNullOrWhiteSpace(newSummary))
            {
                _logger.LogWarning("强制重生成摘要返回空内容");
                return;
            }

            var state = new SummaryState
            {
                CurrentSummary = newSummary,
                LastSummarizedAt = allMessages.Last().CreatedAt,
                HasEverBeenSummarized = true
            };

            var localTime = state.LastSummarizedAt.Value.ToLocalTime();
            state.CurrentSummary = $"{newSummary}\n\n[SummaryUpdated]{localTime:yyyy-MM-dd HH:mm zzz}[/SummaryUpdated]";
            await SaveSummaryStateAsync(sessionId, state);

            _logger.LogInformation(
                "会话 {SessionId} 摘要已强制重生成，共 {Count} 条消息",
                sessionId, allMessages.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "强制重生成会话 {SessionId} 摘要失败", sessionId);
        }
    }

    public async Task<SummaryState> GetSummaryStateAsync(string sessionId)
    {
        var path = GetSummaryPath(sessionId);
        try
        {
            return await _fileService.ReadJsonAsync<SummaryState>(path)
                   ?? new SummaryState();
        }
        catch
        {
            return new SummaryState();
        }
    }

    public async Task<bool> NeedsSummaryAsync(string sessionId)
    {
        var state = await GetSummaryStateAsync(sessionId);
        var history = await _historyService.GetHistoryAsync(sessionId);

        if (!state.LastSummarizedAt.HasValue)
            return history.Messages.Count > 0;

        return history.Messages.Any(m => m.CreatedAt > state.LastSummarizedAt.Value);
    }

    private async Task SaveSummaryStateAsync(string sessionId, SummaryState state)
    {
        var path = GetSummaryPath(sessionId);
        await _fileService.WriteJsonAsync(path, state);
    }

    private string GetSummaryPath(string sessionId)
    {
        var historyDir = Path.Combine(_workspaceDirectory, ".history", sessionId);
        return Path.Combine(historyDir, SummaryFilename);
    }

    /// <summary>
    /// 构建摘要 LLM 的输入上下文（含角色设定、世界观、冗余原始消息）
    /// </summary>
    private async Task<string> BuildSummaryContextAsync(
        string currentSummary,
        List<HistoryMessage> redundantMessages,
        List<HistoryMessage> newMessages,
        bool hasExistingSummary)
    {
        var sb = new StringBuilder();

        // 背景上下文
        var baseContext = await _contextService.GetSummaryBaseContextAsync();
        if (!string.IsNullOrWhiteSpace(baseContext))
        {
            sb.AppendLine(baseContext);
        }

        if (hasExistingSummary && !string.IsNullOrWhiteSpace(currentSummary))
        {
            sb.AppendLine("## 历史摘要（已涵盖之前全部对话）");
            sb.AppendLine(currentSummary);
            sb.AppendLine();
        }

        if (redundantMessages.Count > 0)
        {
            sb.AppendLine("## 冗余历史消息（已纳入摘要的原始对话，供参考）");
            foreach (var msg in redundantMessages)
            {
                var role = msg.Role == HistoryRole.User ? "用户" : "AI";
                var time = msg.CreatedAt.ToLocalTime().ToString("MM-dd HH:mm zzz");
                var cleanText = StripTags(msg.Text);
                if (!string.IsNullOrWhiteSpace(cleanText))
                    sb.AppendLine($"[{time}][{role}] {cleanText}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("## 还未摘要的消息（待纳入摘要）");
        foreach (var msg in newMessages)
        {
            var role = msg.Role == HistoryRole.User ? "用户" : "AI";
            var time = msg.CreatedAt.ToLocalTime().ToString("MM-dd HH:mm zzz");
            var cleanText = StripTags(msg.Text);
            if (!string.IsNullOrWhiteSpace(cleanText))
                sb.AppendLine($"[{time}][{role}] {cleanText}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// 调用主模型生成摘要（纯文本，无工具、无格式约束）
    /// </summary>
    private async Task<string> CallSummaryLlmAsync(string content)
    {
        var modelKey = _agentService.CurrentModelKey;
        var (provider, model) = ResolveModel(modelKey);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, BuildSummaryPrompt()),
            new(ChatRole.User, content)
        };

        var chat = CreateChatClient(provider, model);

        try
        {
            var response = await chat.GetResponseAsync(messages);
            return response.Text?.Trim() ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "摘要 LLM 调用失败 ({ModelKey})", modelKey);
            return string.Empty;
        }
    }

    private string BuildSummaryPrompt()
    {
        var agentName = _agentOptions.Name;
        return $$""""
你是一个对话摘要生成器，负责为「{{agentName}}」的角色扮演对话生成并维护摘要。

你的输入中包含：
- **SOUL.md / WORLD.md** — 角色设定和世界观背景，帮助你判断什么信息重要
- **MEMORY.md** — 长期记忆摘要，帮助你了解已有的记忆重点
- **USER.md** — 用户画像，帮助你理解对话对象
- **历史摘要** — 此前累积的摘要（可能包含多轮对话的要点）
- **新对话记录** — 本次新增的对话内容

你的任务是输出一份**完整的更新版摘要**，而不是只输出新增部分。

具体要求：
1. 参考角色设定和世界观，判断哪些对话内容对角色关系和剧情发展重要
2. **必须包含历史摘要中的所有关键信息**（可以适当精简浓缩，但不能丢弃）
3. 将新对话中的要点、决策、事件、状态变化整合进去
4. 保持简洁连贯，不要逐句抄录原文
5. 用中文，第三人称概述
6. 只输出摘要文本，不要任何标签包裹，不要任何解释
"""";
    }

    /// <summary>
    /// 解析模型配置字符串 "ProviderName/ModelName"
    /// </summary>
    private (ModelProviderOptions Provider, Model Model) ResolveModel(string modelKey)
    {
        var parts = modelKey.Split('/', 2);
        if (parts.Length != 2)
            throw new ArgumentException($"模型格式错误: {modelKey}，应为 Provider/Model");

        var providerName = parts[0];
        var modelName = parts[1];

        var provider = _agentOptions.ModelProviders
                           .FirstOrDefault(p => p.Name == providerName)
                       ?? throw new Exception($"未找到模型提供者: {providerName}");

        var model = provider.Models.FirstOrDefault(m => m.Name == modelName)
                    ?? throw new Exception($"未找到模型: {modelName}（提供者: {providerName}）");

        return (provider, model);
    }

    /// <summary>
    /// 创建纯粹的 IChatClient（无工具、无上下文提供者）
    /// </summary>
    private static IChatClient CreateChatClient(ModelProviderOptions provider, Model model)
    {
        var behavior = model.ChatBehavior;
        var protocol = provider.Protocol.ToLowerInvariant();
        var chatOptions = new ChatOptions
        {
            Temperature = behavior?.Temperature ?? 0.7f,
            MaxOutputTokens = behavior?.MaxOutputTokens,
            TopP = behavior?.TopP
        };

        IChatClient chat;
        switch (protocol)
        {
            case "openai":
            {
                var clientOptions = new OpenAIClientOptions { Endpoint = new Uri(provider.BaseUrl) };
                // if (model.Name.StartsWith("deepseek-v4", StringComparison.CurrentCultureIgnoreCase))
                // {
                //     clientOptions.AddPolicy(new DeepSeekV4Policy(), PipelinePosition.PerCall);
                // }
                chat = new OpenAIClient(new ApiKeyCredential(provider.ApiKey), clientOptions)
                    .GetChatClient(model.Name).AsIChatClient();
                break;
            }
            case "anthropic":
                chat = new AnthropicClient(
                        new Anthropic.Core.ClientOptions
                            { BaseUrl = provider.BaseUrl, ApiKey = provider.ApiKey })
                    .AsIChatClient(model.Name);
                break;
            default:
                throw new NotSupportedException($"不支持的协议: {provider.Protocol}");
        }

        return chat;
    }

    /// <summary>
    /// 移除消息中的 XML 标签（如 &lt;Content&gt;、&lt;InnerVoice&gt; 等），保留纯文本
    /// </summary>
    private static string StripTags(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        // 移除 <xxx> 和 </xxx> 标签
        return System.Text.RegularExpressions.Regex.Replace(text, @"<[^>]+>", "").Trim();
    }
}
