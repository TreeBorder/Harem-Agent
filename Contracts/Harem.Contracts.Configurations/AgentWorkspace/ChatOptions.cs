namespace Harem.Contracts.Configurations.AgentWorkspace;

/// <summary>
/// 聊天渠道配置
/// </summary>
public class ChatOptions
{
    /// <summary>
    /// 渠道名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 渠道类型：mattermost / discord / telegram 等
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 服务器地址
    /// </summary>
    public string ServerUrl { get; set; } = string.Empty;

    /// <summary>
    /// 访问令牌
    /// </summary>
    public string Token { get; set; } = string.Empty;
}