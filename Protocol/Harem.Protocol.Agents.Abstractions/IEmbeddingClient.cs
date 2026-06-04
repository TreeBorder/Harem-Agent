namespace Harem.Protocol.Agents.Abstractions;

/// <summary>
/// 嵌入向量客户端接口
/// </summary>
public interface IEmbeddingClient
{
    /// <summary>
    /// 生成文本的嵌入向量
    /// </summary>
    Task<float[]> GenerateEmbeddingAsync(string text, string? model = null, CancellationToken ct = default);

    /// <summary>
    /// 批量生成嵌入向量
    /// </summary>
    Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IEnumerable<string> texts, string? model = null, CancellationToken ct = default);

    /// <summary>
    /// 获取向量维度
    /// </summary>
    int GetDimension(string? model = null);
}
