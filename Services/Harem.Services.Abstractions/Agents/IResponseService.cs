using Microsoft.Extensions.AI;

namespace Harem.Services.Abstractions.Agents;

/// <summary>
/// 响应解析、快捷标签处理、历史保存服务
/// </summary>
public interface IResponseService
{
    /// <summary>
    /// 解析 Content 中的快捷标签（tts/scenery）并触发对应工具调用，返回移除标签后的纯文本
    /// </summary>
    Task<string> ProcessShortcutTagsAsync(string content, string sessionKey);

    /// <summary>
    /// 保存本轮交互到历史记录（不含摘要，摘要由 SummaryService 独立管理）
    /// </summary>
    Task SaveRoundAsync(
        string sessionId,
        ChatMessage inputMessage,
        string finalContent);
}
