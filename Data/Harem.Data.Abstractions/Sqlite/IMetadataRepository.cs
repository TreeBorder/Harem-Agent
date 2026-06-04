namespace Harem.Data.Abstractions.Sqlite;

/// <summary>
/// 元数据仓储接口（SQLite 存储辅助数据）
/// </summary>
public interface IMetadataRepository
{
    /// <summary>
    /// 存储键值对
    /// </summary>
    Task SetAsync(string agentWorkspacePath, string key, string value, CancellationToken ct = default);

    /// <summary>
    /// 获取键值对
    /// </summary>
    Task<string?> GetAsync(string agentWorkspacePath, string key, CancellationToken ct = default);

    /// <summary>
    /// 删除键值对
    /// </summary>
    Task DeleteAsync(string agentWorkspacePath, string key, CancellationToken ct = default);

    /// <summary>
    /// 获取所有键
    /// </summary>
    Task<IReadOnlyList<string>> GetAllKeysAsync(string agentWorkspacePath, CancellationToken ct = default);
}
