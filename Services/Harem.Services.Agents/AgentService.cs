using System.ClientModel;
using System.ClientModel.Primitives;
using Anthropic;
using Harem.Contracts.Configurations.AgentWorkspace;
using Harem.Services.Abstractions.Agents;
using Harem.Services.Agents.Policy;
using Harem.Services.Agents.Provider;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using ChatOptions = Microsoft.Extensions.AI.ChatOptions;

namespace Harem.Services.Agents;

public class AgentService : IAgentService
{
    private readonly ILogger<AgentService> _logger;
    private readonly AgentOptions _agentOptions;
    private readonly IToolService _toolService;
    private readonly IHistoryService _historyService;
    private readonly ISessionService _sessionService;

    private AIAgent? _agent;
    private string _currentModelKey;

    public string CurrentModelKey => _currentModelKey;

    public AgentService(
        ILogger<AgentService> logger,
        IOptions<AgentOptions> agentOptions,
        IToolService toolService,
        IHistoryService historyService,
        ISessionService sessionService)
    {
        _logger = logger;
        _agentOptions = agentOptions.Value;
        _toolService = toolService;
        _historyService = historyService;
        _sessionService = sessionService;
        _currentModelKey = _agentOptions.DefaultModelKey;
    }

    public AIAgent GetOrCreateAgent()
    {
        _agent ??= CreateAgent(_currentModelKey);
        return _agent;
    }

    public void RebuildForModel(string modelKey)
    {
        _currentModelKey = modelKey;
        _agent = CreateAgent(modelKey);
    }

    public int GetCurrentContextTokens()
    {
        var (_, model) = ResolveModel(_currentModelKey);
        return model.ContextTokens;
    }

    public bool IsStreamingEnabled()
    {
        var (_, model) = ResolveModel(_currentModelKey);
        return model.ChatBehavior?.EnableStreaming ?? true;
    }

    /// <summary>
    /// 解析模型配置字符串，格式: "ProviderName/ModelName"
    /// </summary>
    public (ModelProviderOptions Provider, Model Model) ResolveModel(string modelKey)
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
    /// 根据模型配置创建 AIAgent（按 Protocol 分发到 OpenAI / Anthropic 客户端）
    /// </summary>
    private AIAgent CreateAgent(string modelKey)
    {
        var (provider, model) = ResolveModel(modelKey);
        var behavior = model.ChatBehavior;
        var protocol = provider.Protocol.ToLowerInvariant();
        var chatOptions = new ChatOptions
        {
            // Instructions 不设置——工具使用说明已内嵌在 BuildSingleSystemPromptContextMessages
            // 合并的 system prompt 中（AGENTS.md + SOUL.md + WORLD.md + MEMORY.md + USER.md + 工具规则）。
            // 若此处设 Instructions，SDK 会生成一条独立的 system 消息[0]，
            // 与后面的 5 条 system 消息割裂，导致模型忽略工具。
            Temperature = behavior?.Temperature,
            MaxOutputTokens = behavior?.MaxOutputTokens,
            TopP = behavior?.TopP,
            Reasoning = behavior?.EnableReasoning == true
                ? BuildReasoningOptions(behavior.ReasoningEffort)
                : null,
            StopSequences = behavior?.StopSequences,
            AllowMultipleToolCalls = true,
            ToolMode = ChatToolMode.Auto,
            Tools = _toolService.GetTools()
        };
        IChatClient chat;
        switch (protocol)
        {
            case "openai":
            {
                var openAiClientOptions = new OpenAIClientOptions
                {
                    Endpoint = new Uri(provider.BaseUrl)
                };
                openAiClientOptions.AddPolicy(new DumpPolicy(), PipelinePosition.PerCall);
                // if (model.Name.StartsWith("deepseek-v4", StringComparison.CurrentCultureIgnoreCase))
                // {
                //     openAiClientOptions.AddPolicy(new DeepSeekV4Policy(), PipelinePosition.PerCall);
                // }

                chat = new OpenAIClient(
                        new ApiKeyCredential(provider.ApiKey), openAiClientOptions)
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

        return chat
            .AsAIAgent(new ChatClientAgentOptions
            {
                Name = _agentOptions.Name,
                ChatOptions = chatOptions,
                AIContextProviders = [new RpgChatHistoryProvider(_historyService, _sessionService)]
            });
    }

    /// <summary>
    /// 根据配置字符串构建 ReasoningOptions，不配则返回 null（SDK 默认行为）
    /// </summary>
    private static ReasoningOptions? BuildReasoningOptions(string? effort)
    {
        if (effort is null) return null;

        return new ReasoningOptions
        {
            Effort = effort.ToLowerInvariant() switch
            {
                "low" => ReasoningEffort.Low,
                "high" => ReasoningEffort.High,
                _ => ReasoningEffort.Medium
            },
            Output = ReasoningOutput.None
        };
    }
}