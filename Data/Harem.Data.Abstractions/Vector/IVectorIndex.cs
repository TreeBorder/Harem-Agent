namespace Harem.Data.Abstractions.Vector;

/// <summary>
/// 向量索引接口（FAISS 封装）
/// </summary>
public interface IVectorIndex
{
    /// <summary>
    /// 添加向量
    /// </summary>
    Task AddAsync(string indexPath, string id, float[] vector, string content, CancellationToken ct = default);

    /// <summary>
    /// 批量添加向量
    /// </summary>
    Task AddBatchAsync(string indexPath, IEnumerable<VectorEntry> entries, CancellationToken ct = default);

    /// <summary>
    /// 语义检索
    /// </summary>
    Task<IReadOnlyList<SearchResult>> SearchAsync(string indexPath, float[] queryVector, int topK,
        CancellationToken ct = default);

    /// <summary>
    /// 删除指定ID的向量
    /// </summary>
    Task DeleteAsync(string indexPath, string id, CancellationToken ct = default);

    /// <summary>
    /// 重建索引（压缩优化）
    /// </summary>
    Task RebuildAsync(string indexPath, CancellationToken ct = default);

    /// <summary>
    /// 获取索引中的向量数量
    /// </summary>
    Task<int> CountAsync(string indexPath, CancellationToken ct = default);

    /// <summary>
    /// 检查索引是否存在
    /// </summary>
    bool IndexExists(string indexPath);
}

/// <summary>
/// 向量条目
/// </summary>
public class VectorEntry
{
    /// <summary>
    /// 唯一标识
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 向量数据
    /// </summary>
    public float[] Vector { get; set; } = [];

    /// <summary>
    /// 原始内容
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 元数据
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = [];
}

/// <summary>
/// 检索结果
/// </summary>
public class SearchResult
{
    /// <summary>
    /// 唯一标识
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 相似度分数
    /// </summary>
    public double Score { get; set; }

    /// <summary>
    /// 原始内容
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 元数据
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = [];
}