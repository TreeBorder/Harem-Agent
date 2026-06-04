namespace Harem.Contracts.Configurations.AgentWorkspace;

/// <summary>
/// Web 服务配置
/// </summary>
public class WebOptions
{
    /// <summary>
    /// 公开访问地址（如 Tailscale 域名），用于生成可点击的关卡链接
    /// 示例: https://harem.taila384f8.ts.net
    /// 不配置则使用 http://localhost:5000
    /// </summary>
    public string? PublicUrl { get; set; }
}
