using Harem.Protocol.Agents.Abstractions;
using OllamaSharp;
using OllamaSharp.Models;

namespace Harem.Protocol.Ollama;

/// <summary>
/// Ollama 嵌入向量客户端实现
/// </summary>
public class OllamaEmbeddingClient : IEmbeddingClient
{
    private readonly OllamaApiClient _client;
    private readonly string _defaultModel;
    private readonly int _dimension;

    public OllamaEmbeddingClient(string baseUrl, string defaultModel = "nomic-embed-text", int dimension = 768)
    {
        _client = new OllamaApiClient(baseUrl);
        _defaultModel = defaultModel;
        _dimension = dimension;
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, string? model = null, CancellationToken ct = default)
    {
        var request = new EmbedRequest
        {
            Model = model ?? _defaultModel,
            Input = [text]
        };

        var response = await _client.EmbedAsync(request, ct);

        if (response?.Embeddings == null || response.Embeddings.Count == 0)
            throw new InvalidOperationException("Failed to generate embedding");

        return response.Embeddings[0].Select(d => (float)d).ToArray();
    }

    public async Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IEnumerable<string> texts, string? model = null,
        CancellationToken ct = default)
    {
        var textList = texts.ToList();
        var request = new EmbedRequest
        {
            Model = model ?? _defaultModel,
            Input = textList
        };

        var response = await _client.EmbedAsync(request, ct);

        if (response?.Embeddings == null)
            throw new InvalidOperationException("Failed to generate embeddings");

        return response.Embeddings
            .Select(e => e.Select(d => (float)d).ToArray())
            .ToList();
    }

    public int GetDimension(string? model = null)
    {
        // 不同模型可能有不同维度，这里返回默认配置
        return _dimension;
    }
}