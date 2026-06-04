using Harem.Contracts.Domain.Entities;

namespace Harem.Services.Abstractions.Agents;

/// <summary>
/// 记忆服务接口
/// </summary>
public interface IMemoryService
{
    /// <summary>
    /// 读取长期记忆（MEMORY.md）
    /// </summary>
    Task<string> GetLongTermMemoryAsync();

    /// <summary>
    /// 追加内容到长期记忆
    /// </summary>
    Task<bool> AppendLongTermMemoryAsync(string content);

    /// <summary>
    /// 覆盖写入长期记忆（全量更新）
    /// </summary>
    Task<bool> WriteLongTermMemoryAsync(string content);

    /// <summary>
    /// 读取指定日期记忆（memories/YYYY-MM-DD.md）
    /// </summary>
    Task<string> GetDailyMemoryAsync(string date);

    /// <summary>
    /// 追加内容到每日记忆
    /// </summary>
    Task<bool> AppendDailyMemoryAsync(string date, string content);

    /// <summary>
    /// 覆盖写入每日记忆的"线上记忆"段（保留"线下记忆"段），用于修复重复导出
    /// </summary>
    Task<bool> RewriteDailyMemoryAsync(string date, string content);

    /// <summary>
    /// 将 ChatHistory 按日期分组，覆盖写入每日记忆（修复模式）
    /// </summary>
    Task<IReadOnlyList<string>> RewriteHistoryByDateAsync(ChatHistory history);

    /// <summary>
    /// 语义检索记忆
    /// </summary>
    Task<IReadOnlyList<MemorySearchResult>> SearchMemoryAsync(string[] keywords);

    /// <summary>
    /// 构建记忆索引（将 MEMORY.md 和 memories/ 下的文件向量化入库，按文件修改时间增量索引）
    /// </summary>
    Task BuildMemoryIndexAsync();

    // /// <summary>
    // /// 将序列化的 AgentSession 对话内容导出到当天记忆（从 JsonElement 解析，用于 Job 等无 Agent 实例的场景）
    // /// </summary>
    // /// <param name="sessionContent">AgentSession 序列化后的 JsonElement</param>
    // /// <param name="date">日期字符串，如 2025-04-20</param>
    // Task ExportSessionToDailyMemoryAsync(JsonElement sessionContent, string date);
    Task ExportHistoryToDailyMemoryAsync(ChatHistory history, string date);


    /// <summary>
    /// 解析消息中的强制记忆注入语法 #关键词#，返回需要注入的提示词列表
    /// </summary>
    /// <param name="userMessage">用户消息文本</param>
    /// <returns>需要注入的记忆提示词列表，无匹配时返回空列表</returns>
    Task<IReadOnlyList<MemoryInjection>> ResolveMemoryInjectionsAsync(string userMessage);
}

/// <summary>
/// 记忆检索结果
/// </summary>
public class MemorySearchResult
{
    /// <summary>
    /// 来源文件，如 MEMORY.md 或 memories/2025-04-20.md
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// 段落起始行号（1-based）
    /// </summary>
    public int Line { get; set; }

    /// <summary>
    /// 匹配的记忆片段
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 相似度分数
    /// </summary>
    public double Score { get; set; }
}

/// <summary>
/// 记忆注入结果
/// </summary>
public class MemoryInjection
{
    /// <summary>
    /// 用户查询的关键词
    /// </summary>
    public string Keyword { get; set; } = string.Empty;

    /// <summary>
    /// 注入的系统提示词
    /// </summary>
    public string Prompt { get; set; } = string.Empty;
}