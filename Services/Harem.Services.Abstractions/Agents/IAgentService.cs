using Microsoft.Agents.AI;

namespace Harem.Services.Abstractions.Agents;

/// <summary>
/// Agent 创建与模型管理服务
/// </summary>
public interface IAgentService
{
    /// <summary>
    /// 获取当前 Agent，若未创建则按默认模型初始化
    /// </summary>
    AIAgent GetOrCreateAgent();

    /// <summary>
    /// 使用指定模型重建 Agent
    /// </summary>
    void RebuildForModel(string modelKey);

    /// <summary>
    /// 获取当前模型的上下文 Token 上限
    /// </summary>
    int GetCurrentContextTokens();

    /// <summary>
    /// 当前模型是否启用流式输出
    /// </summary>
    bool IsStreamingEnabled();

    /// <summary>
    /// 当前使用的模型 Key
    /// </summary>
    string CurrentModelKey { get; }
}
