using System.Text.Json;
using System.Text.RegularExpressions;
using Harem.Contracts.Configurations;
using Harem.Contracts.Domain.Entities;
using Harem.Data.Abstractions.Vector;
using Harem.Protocol.Agents.Abstractions;
using Harem.Services.Abstractions.Agents;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Harem.Services.Agents;

/// <summary>
/// 记忆服务实现
/// </summary>
public class MemoryService : IMemoryService
{
    private readonly IFileService _fileService;
    private readonly IEmbeddingClient _embeddingClient;
    private readonly IVectorIndex _vectorIndex;
    private readonly ILogger<MemoryService> _logger;
    private readonly string _workspaceDirectory;

    private const string LongTermMemoryFile = "MEMORY.md";
    private const string DailyMemoryDirectory = "memories";
    private const string VectorIndexDirectory = ".vector";
    private const string IndexManifestFile = "manifest.json";
    private const string KeywordFilename = "keywords.json";

    public MemoryService(
        IFileService fileService,
        IEmbeddingClient embeddingClient,
        IVectorIndex vectorIndex,
        ILogger<MemoryService> logger,
        IOptions<RuntimeOptions> runtimeOptions)
    {
        _fileService = fileService;
        _embeddingClient = embeddingClient;
        _vectorIndex = vectorIndex;
        _logger = logger;
        _workspaceDirectory = runtimeOptions.Value.Workspace;
    }

    public async Task<string> GetLongTermMemoryAsync()
    {
        return await _fileService.GetFileAsync(LongTermMemoryFile) ?? "";
    }

    public async Task<bool> AppendLongTermMemoryAsync(string content)
    {
        var existing = await _fileService.GetFileAsync(LongTermMemoryFile);
        var newContent = string.IsNullOrEmpty(existing)
            ? content
            : $"{existing.TrimEnd()}\n\n{content}";

        return await _fileService.WriteFileAsync(LongTermMemoryFile, newContent);
    }

    public async Task<bool> WriteLongTermMemoryAsync(string content)
    {
        return await _fileService.WriteFileAsync(LongTermMemoryFile, content);
    }

    public async Task<string> GetDailyMemoryAsync(string date)
    {
        var fileName = $"{DailyMemoryDirectory}/{date}.md";
        return await _fileService.GetFileAsync(fileName) ?? "";
    }

    public async Task<bool> AppendDailyMemoryAsync(string date, string content)
    {
        var fileName = $"{DailyMemoryDirectory}/{date}.md";
        var existing = await _fileService.GetFileAsync(fileName);

        if (string.IsNullOrEmpty(existing))
        {
            var newContent = $"# {date}\n\n## 线上记忆\n{content}\n\n## 线下记忆\n";
            return await _fileService.WriteFileAsync(fileName, newContent);
        }

        if (existing.Contains("## 线下记忆"))
        {
            var offlineIndex = existing.IndexOf("## 线下记忆", StringComparison.Ordinal);
            var before = existing[..offlineIndex].TrimEnd();
            var after = existing[offlineIndex..];
            var newContent = $"{before}\n{content}\n\n{after}";
            return await _fileService.WriteFileAsync(fileName, newContent);
        }

        var simpleNew = $"{existing.TrimEnd()}\n\n{content}";
        return await _fileService.WriteFileAsync(fileName, simpleNew);
    }

    public async Task<bool> RewriteDailyMemoryAsync(string date, string content)
    {
        var fileName = $"{DailyMemoryDirectory}/{date}.md";
        var existing = await _fileService.GetFileAsync(fileName);

        string offlineSection = "";
        if (!string.IsNullOrEmpty(existing) && existing.Contains("## 线下记忆"))
        {
            var offlineIndex = existing.IndexOf("## 线下记忆", StringComparison.Ordinal);
            offlineSection = existing[offlineIndex..];
        }

        var newContent = $"# {date}\n\n## 线上记忆\n{content}\n\n{offlineSection}";
        return await _fileService.WriteFileAsync(fileName, newContent.TrimEnd() + "\n");
    }

    public async Task<IReadOnlyList<string>> RewriteHistoryByDateAsync(ChatHistory history)
    {
        var dates = new List<string>();
        var groups = history.Messages
            .GroupBy(m => m.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd"))
            .OrderBy(g => g.Key);

        foreach (var group in groups)
        {
            var date = group.Key;
            var conversation = FormatChatHistory(group.ToList());
            if (string.IsNullOrWhiteSpace(conversation))
                continue;

            await RewriteDailyMemoryAsync(date, conversation);
            dates.Add(date);
        }

        return dates;
    }

    public async Task<IReadOnlyList<MemorySearchResult>> SearchMemoryAsync(string[] keywords)
    {
        var queryText = string.Join(" ", keywords);
        var results = new List<MemorySearchResult>();

        // 1. 优先从关键词索引中查找
        try
        {
            var keywordPath = Path.Combine(_workspaceDirectory, DailyMemoryDirectory, KeywordFilename);
            if (File.Exists(keywordPath))
            {
                var keywordJson = await File.ReadAllTextAsync(keywordPath);
                var keywordFile = JsonSerializer.Deserialize<KeywordFile>(keywordJson, KeywordJsonOptions);
                if (keywordFile?.Entries != null)
                {
                    foreach (var entry in keywordFile.Entries)
                    {
                        var allKeywords = entry.Primary
                            .Concat(entry.Items.SelectMany(i => i.Secondary))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();

                        var anyMatch = allKeywords
                            .Any(kw => queryText.Contains(kw, StringComparison.OrdinalIgnoreCase));

                        if (!anyMatch) continue;

                        // 拼接该 entry 下所有 item 的摘要
                        var combined = string.Join("\n", entry.Items.Select(i =>
                            $"[{i.Date}] {i.Summary}"));

                        results.Add(new MemorySearchResult
                        {
                            Source = $"keywords:{string.Join(",", entry.Primary)}",
                            Content = $"📌 关键词命中: {string.Join(", ", entry.Primary)}\n{combined}",
                            Score = 1.0
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "关键词索引检索失败，继续向量检索");
        }

        // 2. 向量检索（作为补充）
        try
        {
            var queryVector = await _embeddingClient.GenerateEmbeddingAsync(queryText);
            var indexPath = Path.Combine(_workspaceDirectory, VectorIndexDirectory);
            var vectorResults = await _vectorIndex.SearchAsync(indexPath, queryVector, 10);

            results.AddRange(vectorResults.Select(r => new MemorySearchResult
            {
                Source = r.Metadata.GetValueOrDefault("source", "unknown"),
                Line = int.TryParse(r.Metadata.GetValueOrDefault("line", "0"), out var line) ? line : 0,
                Content = r.Content,
                Score = r.Score
            }));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "向量检索失败");
        }

        return results;
    }

    // 关键词索引 JSON 模型（复用 KeywordService 中的定义）
    private record KeywordFile
    {
        public int Version { get; init; } = 1;
        public string UpdatedAt { get; init; } = "";
        public List<KeywordEntry> Entries { get; init; } = [];
    }

    private record KeywordEntry
    {
        public List<string> Primary { get; init; } = [];
        public List<KeywordItem> Items { get; init; } = [];
        public string? LastTriggered { get; set; }
    }

    private record KeywordItem
    {
        public List<string> Secondary { get; init; } = [];
        public string Date { get; init; } = "";
        public string Source { get; init; } = "";
        public string Lines { get; init; } = "";
        public string Summary { get; init; } = "";
    }

    private static readonly JsonSerializerOptions KeywordJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task BuildMemoryIndexAsync()
    {
        var indexPath = Path.Combine(_workspaceDirectory, VectorIndexDirectory);
        var manifestPath = Path.Combine(indexPath, IndexManifestFile);

        // 加载索引清单（记录文件修改时间）
        var manifest = await LoadManifestAsync(manifestPath);

        var entriesToIndex = new List<(string Source, string Content)>();

        // 处理长期记忆
        await IndexFileAsync(LongTermMemoryFile, LongTermMemoryFile, manifest, entriesToIndex);

        // 处理每日记忆
        var memoriesDir = Path.Combine(_workspaceDirectory, DailyMemoryDirectory);
        if (Directory.Exists(memoriesDir))
        {
            foreach (var file in Directory.GetFiles(memoriesDir, "*.md"))
            {
                var relativePath = $"{DailyMemoryDirectory}/{Path.GetFileName(file)}";
                await IndexFileAsync(relativePath, relativePath, manifest, entriesToIndex);
            }
        }

        if (entriesToIndex.Count == 0)
        {
            _logger.LogInformation("No new or modified memory files to index");
            return;
        }

        // 按段落分割，生成向量，批量入库
        var vectorEntries = new List<VectorEntry>();
        foreach (var (source, content) in entriesToIndex)
        {
            var paragraphs = SplitParagraphs(content);
            foreach (var paragraph in paragraphs)
            {
                if (string.IsNullOrWhiteSpace(paragraph.Text))
                    continue;

                var vector = await _embeddingClient.GenerateEmbeddingAsync(paragraph.Text);
                vectorEntries.Add(new VectorEntry
                {
                    Id = $"{source}:{Guid.NewGuid():N}",
                    Vector = vector,
                    Content = paragraph.Text,
                    Metadata = new Dictionary<string, string>
                    {
                        ["source"] = source,
                        ["line"] = paragraph.StartLine.ToString()
                    }
                });
            }
        }

        await _vectorIndex.AddBatchAsync(indexPath, vectorEntries);

        // 保存更新后的清单
        await SaveManifestAsync(manifestPath, manifest);

        _logger.LogInformation("Memory index built: {Count} entries indexed", vectorEntries.Count);
    }

    public async Task ExportHistoryToDailyMemoryAsync(ChatHistory history, string date)
    {
        var dayMessages = history.Messages.Where(x => x.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd") == date)
            .ToList();
        var conversation = FormatChatHistory(dayMessages);
        if (string.IsNullOrWhiteSpace(conversation))
        {
            _logger.LogDebug("历史记录为空，跳过导出");
            return;
        }

        await AppendDailyMemoryAsync(date, conversation);
        _logger.LogInformation("已导出历史记录到 {Date} 的每日记忆", date);
    }

    /// <summary>
    /// 解析消息中的强制记忆注入语法 #关键词#，语义检索后返回提示词
    /// </summary>
    public async Task<IReadOnlyList<MemoryInjection>> ResolveMemoryInjectionsAsync(string userMessage)
    {
        var injections = new List<MemoryInjection>();

        // 解析 #xxx# 语法（排除 Markdown 标题 ## 或 ###，要求 # 后紧跟非#非空内容再接 #）
        var matches = Regex.Matches(userMessage, @"#([^#\s]+)#");
        foreach (Match m in matches)
        {
            var keyword = m.Groups[1].Value.Trim();
            if (string.IsNullOrEmpty(keyword)) continue;

            try
            {
                var results = await SearchMemoryAsync([keyword]);
                if (results.Count == 0) continue;

                // 取 top 3 结果拼接
                var relevant = results.Take(3).ToList();
                var content = string.Join("\n", relevant.Select(r => r.Content));

                injections.Add(new MemoryInjection
                {
                    Keyword = keyword,
                    Prompt = $"[强制回忆:{keyword}]\n{content}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "强制记忆注入检索失败: {Keyword}", keyword);
            }
        }

        return injections;
    }

    /// <summary>
    /// 将 ChatMessage 列表格式化为 Markdown 对话文本
    /// </summary>
    private static string FormatChatMessages(IReadOnlyList<ChatMessage> messages)
    {
        var lines = new List<string>();
        foreach (var msg in messages)
        {
            if (msg.Role != ChatRole.User && msg.Role != ChatRole.Assistant)
                continue;

            var text = msg.Text;
            if (string.IsNullOrWhiteSpace(text))
                continue;
            var time = msg.CreatedAt?.ToLocalTime().ToString("HH:mm") ?? "";
            var timeTag = !string.IsNullOrEmpty(time) ? $" [{time}]" : "";
            var label = msg.Role == ChatRole.Assistant ? "**我**" : "**用户**";
            lines.Add($"{label}{timeTag}: {text}");
        }

        return lines.Count > 0 ? string.Join("\n\n", lines) : string.Empty;
    }

    /// <summary>
    /// 将 ChatHistory 格式化为 Markdown 对话文本（直接从 History 导出，不依赖 Session）
    /// </summary>
    private static string FormatChatHistory(IReadOnlyList<HistoryMessage> messages)
    {
        var lines = new List<string>();
        foreach (var msg in messages)
        {
            var time = msg.CreatedAt.ToLocalTime().ToString("HH:mm");
            var timeTag = $" [{time}]";

            if (msg.Role == HistoryRole.Assistant)
            {
                lines.Add($"**角色**{timeTag}: {msg.Text}");
            }
            else if (msg.Role == HistoryRole.User)
            {
                var userMessage = Regex.Match(msg.Text, @"<UserMessage>(.*?)</UserMessage>", RegexOptions.Singleline)
                    .Groups[1].Value;
                if (!string.IsNullOrWhiteSpace(userMessage))
                    lines.Add($"**用户**{timeTag}: {userMessage}");
            }
        }

        return lines.Count > 0 ? string.Join("\n\n", lines) : string.Empty;
    }


    /// <summary>
    /// 检查文件是否需要索引（新增或修改过的）
    /// </summary>
    private async Task IndexFileAsync(
        string relativePath,
        string sourceKey,
        Dictionary<string, DateTime> manifest,
        List<(string Source, string Content)> entriesToIndex)
    {
        var fullPath = Path.Combine(_workspaceDirectory, relativePath);
        if (!File.Exists(fullPath))
            return;

        var lastModified = File.GetLastWriteTimeUtc(fullPath);

        if (manifest.TryGetValue(sourceKey, out var indexedTime) && lastModified <= indexedTime)
            return; // 未修改，跳过

        var content = await _fileService.GetFileAsync(relativePath);
        if (!string.IsNullOrEmpty(content))
        {
            entriesToIndex.Add((sourceKey, content));
            manifest[sourceKey] = lastModified;
        }
    }

    /// <summary>
    /// 按空行分割段落，记录每个段落的起始行号
    /// </summary>
    private static List<Paragraph> SplitParagraphs(string content)
    {
        var lines = content.Split('\n');
        var paragraphs = new List<Paragraph>();
        var currentLines = new List<string>();
        var startLine = 1;

        for (var i = 0; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                if (currentLines.Count > 0)
                {
                    paragraphs.Add(new Paragraph(string.Join('\n', currentLines), startLine));
                    currentLines.Clear();
                }

                startLine = i + 2; // 下一非空行的行号（1-based）
            }
            else
            {
                if (currentLines.Count == 0)
                    startLine = i + 1; // 1-based
                currentLines.Add(lines[i]);
            }
        }

        if (currentLines.Count > 0)
            paragraphs.Add(new Paragraph(string.Join('\n', currentLines), startLine));

        return paragraphs;
    }

    /// <summary>
    /// 段落文本与起始行号
    /// </summary>
    private record Paragraph(string Text, int StartLine);

    private async Task<Dictionary<string, DateTime>> LoadManifestAsync(string manifestPath)
    {
        if (!File.Exists(manifestPath))
            return [];

        try
        {
            var json = await File.ReadAllTextAsync(manifestPath);
            var dict = JsonSerializer.Deserialize<Dictionary<string, DateTime>>(json);
            return dict ?? [];
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