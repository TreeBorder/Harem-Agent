using System.Text.Json;
using System.Text.RegularExpressions;
using Harem.Contracts.Configurations;
using Harem.Contracts.Domain.Entities;
using Harem.Data.Abstractions.Vector;
using Harem.Protocol.Agents.Abstractions;
using Harem.Services.Abstractions.Agents;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Harem.Services.Agents;

/// <summary>
/// NPC 服务实现 - 基于工作区文件系统
/// </summary>
public class NpcService : INpcService
{
    private readonly IFileService _fileService;
    private readonly IEmbeddingClient _embeddingClient;
    private readonly IVectorIndex _vectorIndex;
    private readonly ILogger<NpcService> _logger;
    private readonly string _workspaceDirectory;

    private const string NpcDirectory = ".npc";
    private const string RecordFile = ".npc/record.json";
    private const string VectorIndexDirectory = ".vector/npc";
    private const string IndexManifestFile = "manifest.json";

    public NpcService(
        IFileService fileService,
        IEmbeddingClient embeddingClient,
        IVectorIndex vectorIndex,
        ILogger<NpcService> logger,
        IOptions<RuntimeOptions> runtimeOptions)
    {
        _fileService = fileService;
        _embeddingClient = embeddingClient;
        _vectorIndex = vectorIndex;
        _logger = logger;
        _workspaceDirectory = runtimeOptions.Value.Workspace;
    }

    public async Task<IReadOnlyList<NpcInfo>> GetAllNpcsAsync(bool includeArchived = false)
    {
        var record = await LoadRecordAsync();
        var npcs = new List<NpcInfo>();

        foreach (var key in record)
        {
            var npc = await LoadNpcAsync(key);
            if (npc is null) continue;
            if (!includeArchived && npc.Archived) continue;
            npcs.Add(npc);
        }

        return npcs;
    }

    public async Task<IReadOnlyList<NpcInfo>> SearchByKeywordAsync(string keyword)
    {
        var keywordLower = keyword.ToLowerInvariant();
        var all = await GetAllNpcsAsync(includeArchived: false);

        return all.Where(n =>
            n.Key.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
            n.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
            n.Summary.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
            n.Tags.Any(t => t.Contains(keyword, StringComparison.OrdinalIgnoreCase))
        ).OrderByDescending(n => n.CurrentIntimacy()).ToList();
    }

    public async Task<NpcInfo?> GetNpcAsync(string key)
    {
        return await LoadNpcAsync(key);
    }

    public async Task<bool> CreateNpcAsync(NpcInfo npc)
    {
        var record = await LoadRecordAsync();
        if (record.Contains(npc.Key))
            return false;

        record.Add(npc.Key);
        await SaveRecordAsync(record);

        var infoPath = GetInfoPath(npc.Key);
        await _fileService.WriteJsonAsync(infoPath, npc);

        // 创建记忆目录
        var memDir = GetMemoryDir(npc.Key);
        Directory.CreateDirectory(memDir);

        return true;
    }

    public async Task<bool> UpdateNpcAsync(NpcInfo npc)
    {
        var record = await LoadRecordAsync();
        if (!record.Contains(npc.Key))
            return false;

        var infoPath = GetInfoPath(npc.Key);
        await _fileService.WriteJsonAsync(infoPath, npc);
        return true;
    }

    public async Task<bool> SetArchiveAsync(string key, bool archived)
    {
        var npc = await LoadNpcAsync(key);
        if (npc is null) return false;

        npc.Archived = archived;
        return await UpdateNpcAsync(npc);
    }

    public async Task<IReadOnlyList<NpcMemory>> GetMemoriesAsync(string key)
    {
        var memDir = GetMemoryDir(key);
        if (!Directory.Exists(memDir))
            return [];

        var memories = new List<NpcMemory>();
        foreach (var file in Directory.GetFiles(memDir, "*.json"))
        {
            var npcMemory = await _fileService.ReadJsonAsync<NpcMemory>(file);
            if (npcMemory is null) continue;
            memories.Add(npcMemory);
        }

        return memories.OrderByDescending(m => m.Timestamp).ToList();
    }

    public async Task<bool> AppendMemoryAsync(string key, NpcMemory memory)
    {
        var npc = await LoadNpcAsync(key);
        if (npc is null) return false;

        // 保存记忆片段
        var memPath = $".npc/{key}/{memory.Id}.json";
        await _fileService.WriteJsonAsync(memPath, memory);

        // 更新亲密度和互动时间
        npc.Intimacy = Math.Clamp(npc.Intimacy + memory.IntimacyDelta, 0, 100);
        npc.LastInteractAt = DateTime.UtcNow;
        await UpdateNpcAsync(npc);

        return true;
    }

    public async Task<bool> UpdateIntimacyAsync(string key, double delta)
    {
        var npc = await LoadNpcAsync(key);
        if (npc is null) return false;

        npc.Intimacy = Math.Clamp(npc.Intimacy + delta, 0, 100);
        npc.LastInteractAt = DateTime.UtcNow;
        return await UpdateNpcAsync(npc);
    }

    // === 私有方法 ===

    private async Task<List<string>> LoadRecordAsync()
    {
        var record = await _fileService.ReadJsonAsync<List<string>>(RecordFile);
        return record ?? [];
    }

    private async Task SaveRecordAsync(List<string> record)
    {
        await _fileService.WriteJsonAsync(RecordFile, record);
    }

    private async Task<NpcInfo?> LoadNpcAsync(string key)
    {
        var infoPath = GetInfoPath(key);
        var npcInfo = await _fileService.ReadJsonAsync<NpcInfo>(infoPath);
        return npcInfo;
    }

    private static string GetInfoPath(string key) => $".npc/{key}/info.json";

    private static string GetMemoryDir(string key)
    {
        // IFileService 可能基于工作区目录，这里需要绝对路径
        // 实际项目中 IFileService 应该支持相对路径解析
        // 暂时假设 _fileService 能处理 .npc/{key}/ 路径
        return $".npc/{key}";
    }

    public async Task<IReadOnlyList<NpcInjection>> ResolveNpcInjectionsAsync(
        string userMessage,
        Dictionary<string, DateTime>? recentlyInjected = null)
    {
        recentlyInjected ??= new();
        var now = DateTime.UtcNow;
        var allNpcs = await GetAllNpcsAsync(includeArchived: false);
        var forcedKeys = new HashSet<string>();
        var autoKeys = new HashSet<string>();

        // 1. 解析强制注入语法 {NPC名/标签}
        var forceMatches = Regex.Matches(userMessage, @"\{(.+?)\}");
        foreach (Match m in forceMatches)
        {
            var query = m.Groups[1].Value.Trim();
            if (string.IsNullOrEmpty(query)) continue;

            // 从名称和标签中检索
            foreach (var npc in allNpcs)
            {
                if (npc.Name.Equals(query, StringComparison.OrdinalIgnoreCase) ||
                    npc.Tags.Any(t => t.Equals(query, StringComparison.OrdinalIgnoreCase)))
                {
                    forcedKeys.Add(npc.Key);
                }
            }
        }

        // 2. 自动匹配：关键词匹配
        foreach (var npc in allNpcs)
        {
            if (userMessage.Contains(npc.Name, StringComparison.OrdinalIgnoreCase) ||
                npc.Tags.Any(t => userMessage.Contains(t, StringComparison.OrdinalIgnoreCase)))
            {
                autoKeys.Add(npc.Key);
            }
        }

        // 3. 自动匹配：语义检索（补充关键词未命中的）
        if (autoKeys.Count < 2)
        {
            try
            {
                var semanticResults = await SearchBySemanticAsync(userMessage);
                foreach (var npc in semanticResults)
                    autoKeys.Add(npc.Key);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "NPC 语义检索失败");
            }
        }

        // 4. 组装结果
        var injections = new List<NpcInjection>();

        // 4a. 强制注入：无条件，不受冷却和数量限制
        foreach (var key in forcedKeys)
        {
            var npc = allNpcs.FirstOrDefault(n => n.Key == key);
            if (npc is null) continue;

            var prompt = BuildInjectionPrompt(npc);
            if (prompt is null) continue;

            injections.Add(new NpcInjection
            {
                NpcKey = npc.Key,
                Prompt = prompt,
                Forced = true
            });
        }

        // 4b. 自动注入：按亲密度排序，最多2个，受30分钟冷却限制
        var autoMatched = autoKeys
            .Where(k => !forcedKeys.Contains(k)) // 去重：已被强制注入的不再自动注入
            .Select(k => allNpcs.FirstOrDefault(n => n.Key == k))
            .Where(n => n is not null)
            .OrderByDescending(n => n!.CurrentIntimacy())
            .Take(2);

        foreach (var npc in autoMatched)
        {
            if (npc is null) continue;

            // 30 分钟内注入过的不重复
            if (recentlyInjected.TryGetValue(npc.Key, out var last) && (now - last).TotalMinutes < 30)
                continue;

            var prompt = BuildInjectionPrompt(npc);
            if (prompt is null) continue;

            injections.Add(new NpcInjection
            {
                NpcKey = npc.Key,
                Prompt = prompt,
                Forced = false
            });
        }

        return injections;
    }

    /// <summary>
    /// 根据记忆深度生成注入提示词
    /// </summary>
    private static string? BuildInjectionPrompt(NpcInfo npc)
    {
        return npc.GetMemoryDepth() switch
        {
            MemoryDepth.Vivid => $"你记得 {npc.Name}：{npc.Summary}",
            MemoryDepth.Familiar => $"你对 {npc.Name} 有印象：{npc.Summary}",
            MemoryDepth.Vague => $"你隐约记得有个叫 {npc.Name} 的人...",
            _ => null
        };
    }

    // === 向量索引 ===

    public async Task BuildIndexAsync()
    {
        var indexPath = Path.Combine(_workspaceDirectory, VectorIndexDirectory);
        var manifestPath = Path.Combine(indexPath, IndexManifestFile);
        var manifest = await LoadManifestAsync(manifestPath);

        var entriesToIndex = new List<(string Id, string Source, string Content)>();
        var npcDir = Path.Combine(_workspaceDirectory, NpcDirectory);
        if (!Directory.Exists(npcDir))
        {
            _logger.LogInformation("NPC 目录不存在，跳过索引");
            return;
        }

        foreach (var key in await LoadRecordAsync())
        {
            var infoPath = Path.Combine(npcDir, key, "info.json");
            if (File.Exists(infoPath))
            {
                var relative = $".npc/{key}/info.json";
                await IndexNpcFileAsync(infoPath, relative, $"npc:{key}:info", manifest, entriesToIndex);
            }

            var memDir = Path.Combine(npcDir, key);
            if (Directory.Exists(memDir))
            {
                foreach (var memFile in Directory.GetFiles(memDir, "*.json").Where(f => !f.EndsWith("info.json")))
                {
                    var fileName = Path.GetFileName(memFile);
                    var relative = $".npc/{key}/{fileName}";
                    var id = $"npc:{key}:memory:{Path.GetFileNameWithoutExtension(fileName)}";
                    await IndexNpcFileAsync(memFile, relative, id, manifest, entriesToIndex);
                }
            }
        }

        if (entriesToIndex.Count == 0)
        {
            _logger.LogInformation("NPC 索引已是最新");
            return;
        }

        var vectorEntries = new List<VectorEntry>();
        foreach (var (id, source, content) in entriesToIndex)
        {
            var vector = await _embeddingClient.GenerateEmbeddingAsync(content);
            vectorEntries.Add(new VectorEntry
            {
                Id = id,
                Vector = vector,
                Content = content,
                Metadata = new Dictionary<string, string>
                {
                    ["source"] = source,
                    ["npcKey"] = source.Split('/')[2] // .npc/{key}/...
                }
            });
        }

        await _vectorIndex.AddBatchAsync(indexPath, vectorEntries);
        await SaveManifestAsync(manifestPath, manifest);
        _logger.LogInformation("NPC 索引构建完成: {Count} 条", vectorEntries.Count);
    }

    public async Task<IReadOnlyList<NpcInfo>> SearchBySemanticAsync(string description)
    {
        var queryVector = await _embeddingClient.GenerateEmbeddingAsync(description);
        var indexPath = Path.Combine(_workspaceDirectory, VectorIndexDirectory);

        if (!_vectorIndex.IndexExists(indexPath))
            return [];

        var results = await _vectorIndex.SearchAsync(indexPath, queryVector, 5);
        var npcKeys = results
            .Select(r => r.Metadata.GetValueOrDefault("npcKey", ""))
            .Where(k => !string.IsNullOrEmpty(k))
            .Distinct()
            .ToList();

        var npcs = new List<NpcInfo>();
        foreach (var key in npcKeys)
        {
            var npc = await GetNpcAsync(key);
            if (npc is not null && !npc.Archived)
                npcs.Add(npc);
        }

        return npcs.OrderByDescending(n => n.CurrentIntimacy()).ToList();
    }

    private async Task IndexNpcFileAsync(
        string fullPath,
        string relativePath,
        string id,
        Dictionary<string, DateTime> manifest,
        List<(string Id, string Source, string Content)> entries)
    {
        var lastModified = File.GetLastWriteTimeUtc(fullPath);
        if (manifest.TryGetValue(relativePath, out var indexedTime) && lastModified <= indexedTime)
            return;

        var text = await _fileService.GetFileAsync(relativePath);
        if (string.IsNullOrWhiteSpace(text))
            return;

        entries.Add((id, relativePath, text));
        manifest[relativePath] = lastModified;
    }

    private async Task<Dictionary<string, DateTime>> LoadManifestAsync(string manifestPath)
    {
        if (!File.Exists(manifestPath))
            return [];

        try
        {
            var json = await File.ReadAllTextAsync(manifestPath);
            return JsonSerializer.Deserialize<Dictionary<string, DateTime>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private async Task SaveManifestAsync(string manifestPath, Dictionary<string, DateTime> manifest)
    {
        var dir = Path.GetDirectoryName(manifestPath)!;
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(manifestPath, json);
    }
}