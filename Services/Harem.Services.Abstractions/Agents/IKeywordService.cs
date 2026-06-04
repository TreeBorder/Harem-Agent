using Microsoft.Extensions.AI;

namespace Harem.Services.Abstractions.Agents;

public interface IKeywordService
{
    /// <summary>
    /// 提取今日天记忆中的关键词节点，合并到 keywords.json
    /// </summary>
    Task ExtractKeywordsAsync(string today, CancellationToken ct = default);

    /// <summary>
    /// 根据用户输入和会话已触发关键词，返回需要注入的消息及更新后的已触发关键词列表
    /// </summary>
    Task<(List<ChatMessage> Messages, List<string> TriggeredKeywords)> ResolveKeywordInjectionsAsync(
        string userMessage, List<string> sessionTriggeredKeywords);

    /// <summary>
    /// 获取当前关键词索引概要（供 /keyword 指令使用）
    /// </summary>
    Task<string> GetKeywordIndexAsync();
}
