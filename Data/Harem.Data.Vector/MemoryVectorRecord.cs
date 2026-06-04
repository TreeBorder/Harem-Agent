using Microsoft.Extensions.VectorData;

namespace Harem.Data.Vector;

/// <summary>
/// 向量记忆记录 - 用于 VectorData 标准抽象
/// </summary>
public class MemoryVectorRecord
{
    /// <summary>
    /// 记录唯一标识
    /// </summary>
    [VectorStoreKey]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 原始内容
    /// </summary>
    [VectorStoreData]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 来源文件
    /// </summary>
    [VectorStoreData]
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// 向量嵌入 (qwen3-embedding:0.6b, 1024维)
    /// </summary>
    [VectorStoreVector(1024)]
    public ReadOnlyMemory<float> Vector { get; set; }
}
