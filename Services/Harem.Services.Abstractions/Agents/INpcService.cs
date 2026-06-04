using Harem.Contracts.Domain.Entities;

namespace Harem.Services.Abstractions.Agents;

/// <summary>
/// NPC 服务接口 - 管理 NPC 的增删改查和记忆片段
/// </summary>
public interface INpcService
{
    /// <summary>
    /// 获取 NPC 总览列表（可选包含已归档）
    /// </summary>
    Task<IReadOnlyList<NpcInfo>> GetAllNpcsAsync(bool includeArchived = false);

    /// <summary>
    /// 按关键词检索 NPC（匹配 name、tags、summary）
    /// </summary>
    Task<IReadOnlyList<NpcInfo>> SearchByKeywordAsync(string keyword);

    /// <summary>
    /// 获取单个 NPC
    /// </summary>
    Task<NpcInfo?> GetNpcAsync(string key);

    /// <summary>
    /// 创建 NPC
    /// </summary>
    Task<bool> CreateNpcAsync(NpcInfo npc);

    /// <summary>
    /// 更新 NPC 基础信息
    /// </summary>
    Task<bool> UpdateNpcAsync(NpcInfo npc);

    /// <summary>
    /// 归档/解归档 NPC
    /// </summary>
    Task<bool> SetArchiveAsync(string key, bool archived);

    /// <summary>
    /// 获取 NPC 的记忆片段
    /// </summary>
    Task<IReadOnlyList<NpcMemory>> GetMemoriesAsync(string key);

    /// <summary>
    /// 追加记忆片段，同时更新 LastInteractAt
    /// </summary>
    Task<bool> AppendMemoryAsync(string key, NpcMemory memory);

    /// <summary>
    /// 更新亲密度和最后互动时间
    /// </summary>
    Task<bool> UpdateIntimacyAsync(string key, double delta);

    /// <summary>
    /// 构建 NPC 向量索引（增量更新）
    /// </summary>
    Task BuildIndexAsync();

    /// <summary>
    /// 语义检索 NPC
    /// </summary>
    Task<IReadOnlyList<NpcInfo>> SearchBySemanticAsync(string description);

    /// <summary>
    /// 解析需要注入的 NPC 提示词列表
    /// 支持自动匹配（关键词+语义）和强制注入（{NPC名/标签} 语法）
    /// </summary>
    /// <param name="userMessage">用户消息文本</param>
    /// <param name="recentlyInjected">最近注入记录（npcKey → 注入时间），用于冷却去重</param>
    /// <returns>需要注入的 NPC 提示词列表</returns>
    Task<IReadOnlyList<NpcInjection>> ResolveNpcInjectionsAsync(
        string userMessage,
        Dictionary<string, DateTime>? recentlyInjected = null);
}

/// <summary>
/// NPC 注入结果
/// </summary>
public class NpcInjection
{
    /// <summary>
    /// NPC 唯一标识
    /// </summary>
    public string NpcKey { get; set; } = string.Empty;

    /// <summary>
    /// 注入的系统提示词
    /// </summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    /// 是否为强制注入（用户通过 {NPC} 语法显式指定）
    /// </summary>
    public bool Forced { get; set; }
}
