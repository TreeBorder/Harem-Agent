using System.ComponentModel;

namespace Harem.Services.Abstractions.Tools;

/// <summary>
/// Hermes Agent (Clio) 工具接口
/// </summary>
[Description("Hermes AI Agent 工具")]
public interface IHermesTool
{
    /// <summary>
    /// 异步调用 Hermes Agent (Clio) 执行复杂任务
    /// 工具立即返回"已派发"，Clio 在后台执行完成后结果会通过系统消息推回给 Agent
    /// </summary>
    [Description("异步调用 Hermes Agent (Clio) 执行代码生成、信息检索、文件操作、分析总结等复杂技术任务。工具立即返回不阻塞当前对话，结果会通过系统消息异步推回。sessionId可选——不传开新会话，传之前返回的session_id续接上下文")]
    Task<string> Chat(string query, string? sessionId = null);
}
