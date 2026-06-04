using System.Collections.Concurrent;
using Harem.Data.Abstractions.Vector;
using Microsoft.SemanticKernel.Connectors.SqliteVec;

namespace Harem.Data.Vector;

/// <summary>
/// 基于 Microsoft.Extensions.VectorData + sqlite-vec 的向量索引实现
/// </summary>
public class VectorDataIndex : IVectorIndex
{
    private readonly ConcurrentDictionary<string, SqliteCollection<string, MemoryVectorRecord>> _collections =
        new();

    private readonly object _lock = new();

    public async Task AddAsync(string indexPath, string id, float[] vector, string content,
        CancellationToken ct = default)
    {
        var collection = await GetOrCreateCollectionAsync(indexPath, ct);
        await collection.UpsertAsync(new MemoryVectorRecord
        {
            Id = id,
            Content = content,
            Vector = vector
        }, cancellationToken: ct);
    }

    public async Task AddBatchAsync(string indexPath, IEnumerable<VectorEntry> entries, CancellationToken ct = default)
    {
        var collection = await GetOrCreateCollectionAsync(indexPath, ct);
        var records = entries.Select(e => new MemoryVectorRecord
        {
            Id = e.Id,
            Content = e.Content,
            Source = e.Metadata.GetValueOrDefault("source", ""),
            Vector = e.Vector
        }).ToList();

        await collection.UpsertAsync(records, cancellationToken: ct);
    }

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string indexPath, float[] queryVector, int topK,
        CancellationToken ct = default)
    {
        if (!IndexExists(indexPath))
            return [];

        var collection = await GetOrCreateCollectionAsync(indexPath, ct);
        var results = collection.SearchAsync(queryVector, topK, cancellationToken: ct);

        var searchResults = new List<SearchResult>();
        await foreach (var result in results)
        {
            searchResults.Add(new SearchResult
            {
                Id = result.Record.Id,
                Score = result.Score ?? 0,
                Content = result.Record.Content,
                Metadata = new Dictionary<string, string>
                {
                    ["source"] = result.Record.Source
                }
            });
        }

        return searchResults.OrderByDescending(r => r.Score).ToList();
    }

    public async Task DeleteAsync(string indexPath, string id, CancellationToken ct = default)
    {
        if (!IndexExists(indexPath))
            return;

        var collection = await GetOrCreateCollectionAsync(indexPath, ct);
        await collection.DeleteAsync(id, cancellationToken: ct);
    }

    public Task RebuildAsync(string indexPath, CancellationToken ct = default)
    {
        // sqlite-vec 自动维护索引，无需手动重建
        return Task.CompletedTask;
    }

    public async Task<int> CountAsync(string indexPath, CancellationToken ct = default)
    {
        if (!IndexExists(indexPath))
            return 0;

        var collection = await GetOrCreateCollectionAsync(indexPath, ct);
        // 由于没有直接的 Count API，通过获取所有记录来统计
        // 实际生产环境不建议这样做，但当前版本 VectorData 可能没有 Count
        return 0; // TODO: 等 VectorData 提供 Count API
    }

    public bool IndexExists(string indexPath)
    {
        var dbPath = GetDatabasePath(indexPath);
        return File.Exists(dbPath);
    }

    /// <summary>
    /// 获取或创建向量集合
    /// </summary>
    private async Task<SqliteCollection<string, MemoryVectorRecord>> GetOrCreateCollectionAsync(
        string indexPath, CancellationToken ct)
    {
        if (_collections.TryGetValue(indexPath, out var existing))
        {
            // 校验缓存的 collection 对应的数据库文件是否仍然存在
            var dbPath = GetDatabasePath(indexPath);
            if (File.Exists(dbPath))
                return existing;

            // 文件被删除，清除缓存，重新创建
            _collections.TryRemove(indexPath, out _);
        }

        lock (_lock)
        {
            if (_collections.TryGetValue(indexPath, out existing))
            {
                var dbPath = GetDatabasePath(indexPath);
                if (File.Exists(dbPath))
                    return existing;

                _collections.TryRemove(indexPath, out _);
            }

            var dbPath2 = GetDatabasePath(indexPath);
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath2)!);

            var connectionString = $"Data Source={dbPath2}";
            var store = new SqliteVectorStore(connectionString);
            var collection = store.GetCollection<string, MemoryVectorRecord>("vectors");

            // 异步创建集合（如果尚不存在）
            collection.EnsureCollectionExistsAsync(ct).GetAwaiter().GetResult();

            _collections[indexPath] = collection;
            return collection;
        }
    }

    private static string GetDatabasePath(string indexPath)
    {
        return Path.Combine(indexPath, "vectors.db");
    }
}