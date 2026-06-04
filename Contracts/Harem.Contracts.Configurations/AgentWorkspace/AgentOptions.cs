namespace Harem.Contracts.Configurations.AgentWorkspace;

public class AgentOptions
{
    /// <summary>
    /// 唯一标识（对应工作区文件夹名）
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 显示名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 默认模型
    /// </summary>
    public string DefaultModelKey { get; set; } = string.Empty;

    /// <summary>
    /// 后备模型
    /// </summary>
    public string[] FallbackModels { get; set; } = [];

    /// <summary>
    /// 聊天渠道配置，支持单一渠道
    /// </summary>
    public ChatOptions Chat { get; set; } = new();

    /// <summary>
    /// 模型提供者配置
    /// </summary>
    public List<ModelProviderOptions> ModelProviders { get; set; } = [];

    /// <summary>
    /// ComfyUI 配置（角色图片生成）
    /// </summary>
    public ComfyUiOptions? ComfyUi { get; set; }

    /// <summary>
    /// Ollama 配置（向量嵌入）
    /// </summary>
    public OllamaOptions? Ollama { get; set; }

    /// <summary>
    /// Web 服务配置（公开地址等）
    /// </summary>
    public WebOptions? Web { get; set; }
    
    /// <summary>
    /// MiMo 语音合成配置
    /// </summary>
    public MimoVoiceOptions? MimoVoice { get; set; }

    /// <summary>
    /// 子智能体配置（clio / mmx 文本对话）
    /// </summary>
    public SubAgentOptions? SubAgent { get; set; }
}

/// <summary>
/// MiMo 语音合成配置（独立于 ModelProviders，专用 TTS API Key）
/// </summary>
public class MimoVoiceOptions
{
    /// <summary>
    /// MimoCli 可执行文件绝对路径
    /// </summary>
    public string Path { get; set; } = "mimo";

    /// <summary>
    /// MiMo TTS API Key（对应 XIAOMI_API_KEY）
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
}

/// <summary>
/// ComfyUI 服务配置
/// </summary>
public class ComfyUiOptions
{
    /// <summary>
    /// ComfyUI 服务地址
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;
}

/// <summary>
/// Ollama 服务配置
/// </summary>
public class OllamaOptions
{
    /// <summary>
    /// Ollama 服务地址
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>
    /// 嵌入模型名称
    /// </summary>
    public string EmbeddingModel { get; set; } = "qwen3-embedding:0.6b";

    /// <summary>
    /// 嵌入向量维度
    /// </summary>
    public int EmbeddingDimension { get; set; } = 1024;
}

/// <summary>
/// 子智能体服务配置（clio / mmx 文本对话）
/// </summary>
public class SubAgentOptions
{
    /// <summary>
    /// clio 可执行文件绝对路径
    /// </summary>
    public string ClioPath { get; set; } = "clio";

    /// <summary>
    /// clio / hermes 所在目录（Supervisor 环境 PATH 注入用）
    /// clio 脚本内部 exec hermes，需此目录在 PATH 中才能找到 hermes
    /// </summary>
    public string? BinPath { get; set; }
}