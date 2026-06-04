namespace Harem.Contracts.Configurations.AgentWorkspace;

public class ModelProviderOptions
{
    /// <summary>
    /// 模型提供商名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 模型提供商描述
    /// </summary>
    public string? Description { get; set; }

    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// 模型提供商兼容协议,OpenAI 或 Anthropic
    /// </summary>
    public string Protocol { get; set; } = "OpenAI";

    /// <summary>
    /// 模型列表
    /// </summary>
    public List<Model> Models { get; set; } = new();
}

public class Model
{
    /// <summary>
    /// 模型名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 模型描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 模型上下文最大令牌数
    /// </summary>
    public int ContextTokens { get; set; }

    /// <summary>
    /// 聊天行为配置（可选，不配则用模型默认值）
    /// </summary>
    public ChatBehaviorOptions? ChatBehavior { get; set; }
}

/// <summary>
/// 聊天行为配置 - 控制模型生成的核心参数
/// </summary>
public class ChatBehaviorOptions
{
    /// <summary>
    /// 生成温度，越高越随机。范围 0.0 ~ 2.0
    /// </summary>
    public float? Temperature { get; set; }

    /// <summary>
    /// 最大输出令牌数
    /// </summary>
    public int? MaxOutputTokens { get; set; }

    /// <summary>
    /// Top-P 核采样概率阈值
    /// </summary>
    public float? TopP { get; set; }

    /// <summary>
    /// 推理努力程度: "low" | "medium" | "high"
    /// 仅对支持推理的模型有效（如 deepseek-reasoner）
    /// </summary>
    public string? ReasoningEffort { get; set; }

    /// <summary>
    /// 是否允许单次响应中调用多个工具
    /// </summary>
    public bool? AllowMultipleToolCalls { get; set; }

    /// <summary>
    /// 停止序列，遇到这些字符串时停止生成
    /// </summary>
    public string[]? StopSequences { get; set; }

    /// <summary>
    /// 是否开启流式输出（默认开启）
    /// 关闭后改用非流式调用，适用于 SDK 流式存在兼容性问题的模型
    /// </summary>
    public bool EnableStreaming { get; set; } = false;

    /// <summary>
    /// 是否为推理模型（默认关闭）
    /// 开启后传入 ReasoningEffort 配置，适用于 deepseek-reasoner 等推理模型
    /// </summary>
    public bool EnableReasoning { get; set; } = false;
}