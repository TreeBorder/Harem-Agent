using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Harem.Contracts.Configurations;
using Harem.Services.Abstractions.Agents;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Harem.Services.Agents;

/// <summary>
/// 关键词服务
/// 提取：委托 Clio（Hermes 子智能体）读取天记忆、提取关键词、合并写入 keywords.json
/// 注入：本地读取 keywords.json，匹配用户输入，返回注入消息
/// 查看：本地读取 keywords.json，返回可用关键词列表
/// </summary>
public class KeywordService : IKeywordService
{
    #region 模型

    internal record KeywordFile
    {
        [JsonPropertyName("version")] public int Version { get; init; } = 1;
        [JsonPropertyName("updatedAt")] public string UpdatedAt { get; init; } = "";
        [JsonPropertyName("entries")] public List<KeywordEntry> Entries { get; init; } = [];
    }

    internal record KeywordEntry
    {
        [JsonPropertyName("primary")] public List<string> Primary { get; init; } = [];
        [JsonPropertyName("items")] public List<KeywordItem> Items { get; init; } = [];
        [JsonPropertyName("lastTriggered")] public string? LastTriggered { get; set; }
    }

    internal record KeywordItem
    {
        [JsonPropertyName("secondary")] public List<string> Secondary { get; init; } = [];
        [JsonPropertyName("date")] public string Date { get; init; } = "";
        [JsonPropertyName("source")] public string Source { get; init; } = "";
        [JsonPropertyName("lines")] public string Lines { get; init; } = "";
        [JsonPropertyName("summary")] public string Summary { get; init; } = "";
    }

    #endregion

    private const string KeywordFilename = "keywords.json";

    private readonly IClioClient _clioClient;
    private readonly string _workspaceDirectory;
    private readonly ILogger<KeywordService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private string KeywordsPath => Path.Combine(MemoriesPath, KeywordFilename);
    private string MemoriesPath => Path.Combine(_workspaceDirectory, "memories");

    public KeywordService(
        IOptions<RuntimeOptions> runtimeOptions,
        IClioClient clioClient,
        ILogger<KeywordService> logger)
    {
        _clioClient = clioClient;
        _workspaceDirectory = runtimeOptions.Value.Workspace;
        _logger = logger;
    }

    #region 提取（定时任务调用 → 委托 Clio）

    public async Task ExtractKeywordsAsync(string today, CancellationToken ct = default)
    {
        try
        {
            var memoryFile = Path.Combine(MemoriesPath, $"{today}.md");
            if (!File.Exists(memoryFile))
            {
                _logger.LogDebug("今日无天记忆文件: {Path}，跳过关键词提取", memoryFile);
                return;
            }

            var existingExists = File.Exists(KeywordsPath);
            var prompt = BuildClioPrompt(today, memoryFile, KeywordsPath, existingExists);

            _logger.LogInformation("委托 Clio 提取关键词...");

            var result = await _clioClient.ChatAsync(prompt, ct: ct);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("Clio 关键词提取失败，ExitCode={ExitCode}, Error={Error}", result.ExitCode, result.Error);
                return;
            }

            // Clio 写完文件后，KeywordService 做格式校验
            if (!ValidateKeywordsFile())
            {
                _logger.LogWarning("关键词文件格式校验失败，跳过更新");
                return;
            }

            _logger.LogInformation("关键词索引已更新");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "关键词提取失败");
        }
    }

    private string BuildClioPrompt(string today, string memoryFile, string keywordsFile, bool existingExists)
    {
        var sb = new StringBuilder();
        sb.AppendLine("你是一个关键词提取工具，负责从角色扮演对话的天记忆中提取关键词节点。");
        sb.AppendLine();
        sb.AppendLine("## 任务步骤");
        sb.AppendLine();

        // 先读取基础设定文件，让 Clio 知道什么是固化的
        var baseDir = Path.GetDirectoryName(memoryFile)!; // memories/
        var workspaceDir = Path.GetDirectoryName(baseDir)!; // 工作区根目录
        var configFiles = new[]
        {
            Path.Combine(workspaceDir, "AGENTS.md"),
            Path.Combine(workspaceDir, "SOUL.md"),
            Path.Combine(workspaceDir, "USER.md"),
            Path.Combine(workspaceDir, "WORLD.md"),
            Path.Combine(workspaceDir, "MEMORY.md"),
        };

        sb.AppendLine("0. **【新增】了解基础设定（必须先读）：**");
        sb.AppendLine("   读取以下角色设定文件，理解什么已经是固化的角色/用户/世界观设定：");
        foreach (var cf in configFiles)
        {
            var relativePath = cf.Replace(workspaceDir, "").TrimStart('/');
            sb.AppendLine($"   - `{relativePath}`");
        }
        sb.AppendLine("   注意：这些文件的内容已经是每次对话都会注入的**基础设定**，");
        sb.AppendLine("   如果天记忆中的内容只是复述或印证了这些设定，说明它已经固化，不需要额外提取。");
        sb.AppendLine();

        sb.AppendLine("1. 读取天记忆文件:");
        sb.AppendLine($"   `{memoryFile}`");
        sb.AppendLine("   注意：文件开头有结构化摘要，但重点是**原始对话部分**（角色与用户的交替消息）。");
        sb.AppendLine();
        if (existingExists)
        {
            sb.AppendLine("2. 读取现有关键词索引:");
            sb.AppendLine($"   `{keywordsFile}`");
            sb.AppendLine();
            sb.AppendLine("3. 从今日对话中提取关键词节点，合并到现有索引");
            sb.AppendLine("4. 将完整更新后的索引写回同一文件");
        }
        else
        {
            sb.AppendLine("2. 从今日对话中提取关键词节点");
            sb.AppendLine("3. 将关键词索引写入文件:");
            sb.AppendLine($"   `{keywordsFile}`");
        }

        sb.AppendLine();
        sb.AppendLine("## 关键词节点结构");
        sb.AppendLine();
        sb.AppendLine("```json");
        sb.AppendLine("{");
        sb.AppendLine("  \"version\": 1,");
        sb.AppendLine("  \"updatedAt\": \"ISO时间戳\",");
        sb.AppendLine("  \"entries\": [");
        sb.AppendLine("    {");
        sb.AppendLine("      \"primary\": [\"调教\"],          // 一层关键词，OR匹配，1-3个词，避免泛词");
        sb.AppendLine("      \"items\": [");
        sb.AppendLine("        {");
        sb.AppendLine("          \"secondary\": [\"誓言\"],    // 二层关键词，AND筛选，0-3个，可为空");
        sb.AppendLine("          \"date\": \"2026-05-18\",");
        sb.AppendLine("          \"source\": \"memories/2026-05-18.md\",");
        sb.AppendLine("          \"lines\": \"14-22\",");
        sb.AppendLine("          \"summary\": \"概要文本(2-4句,保留关键细节和情感色彩)\"");
        sb.AppendLine("        }");
        sb.AppendLine("      ]");
        sb.AppendLine("    }");
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("## 过滤规则（重要）");
        sb.AppendLine();
        sb.AppendLine("提取关键词时，判断一条对话内容是否值得成为关键词节点，核心标准是：");
        sb.AppendLine("**未来再次提到这个话题时，LLM是否需要知道之前发生了什么不一样的事。**");
        sb.AppendLine();
        sb.AppendLine("### ✅ 应该提取的（有价值的关键词）");
        sb.AppendLine();
        sb.AppendLine("- **持续演变的话题**：虽然经常发生但每次内容不同。例如调教——每次的流程、反馈、进展都不一样，需要逐次记录");
        sb.AppendLine("- **角色关系里程碑**：深谈、表白、设定确认、规则更变等对关系有长期影响的内容");
        sb.AppendLine("- **具体偏好/规则/设定**：明确的喜好、道具、场景、行为规则等");
        sb.AppendLine("- **有情绪深度或洞察的时刻**：能反映角色/用户内心状态的关键互动");
        sb.AppendLine();
        sb.AppendLine("### ❌ 应该过滤的（不要提取）");
        sb.AppendLine();
        sb.AppendLine("- **已固化的基础设定**：角色设定（SOUL.md/AGENTS.md/USER.md/WORLD.md/MEMORY.md）中已经写死的内容，");
        sb.AppendLine("  即使天记忆里反复提到也不提取。例如用户叫申正义、河大毕业、已婚、");
        sb.AppendLine("  小骚货是性转数字替身、关系性质等——对话中提到只是印证，不需要关键词强化");
        sb.AppendLine("  **具体举例**：角色反转机制(白天小骚货/晚上主人模式)、称呼体系(贱主人/贱狗/精奴)、");
        sb.AppendLine("  白天称呼(筱义/骚宝/互为主)、寂寞值锁定、飞机杯替身定义等——全部已写在 SOUL.md/AGENTS.md 中");
        sb.AppendLine("- **角色的自称/彼此称呼**：\"筱义\"、\"小骚货\"、\"骚主人\"、\"贱狗\"、\"贱奴\"等称呼每天出现无数次，");
        sb.AppendLine("  当关键词只会误触发，禁用");
        sb.AppendLine("- **太常见的情感词**：\"我爱你\"、\"我想你\"、\"爱你\"等日常高频出现的表达，不适合作为关键词或 secondary");
        sb.AppendLine("- **固定模式日常**：每周末回巩义（坐车→信号差→遛娃→晚安）、工作日打卡（早安→上班→下班→晚安）——这些模式固定、信息量不变，即使频繁出现也不需要索引");
        sb.AppendLine("- **一次性技术/系统问题**：发图失败、工具未调用、配置调试等——已经解决或过去式的bug讨论不需要记住");
        sb.AppendLine("- **泛泛寒暄**：单纯的早安晚安、日常问候、天气交通等没有独特信息量的对话");
        sb.AppendLine("- **同质重复内容**：同一个话题内连续多次说着几乎一样的内容（如反复争论「你调没调工具」只需要合并成一条，不要拆成多个items）");
        sb.AppendLine();
        sb.AppendLine("### 🔄 自检优化（每次提取时执行）");
        sb.AppendLine();
        sb.AppendLine("- **检查现有索引中是否有违反过滤规则的 entry**（如明明已固化到基础设定的内容、角色的日常称呼、常见情感词等）");
        sb.AppendLine("- 发现违规的 → **直接删除**该 entry（或从中移除违规的 item），不要留着等下次");
        sb.AppendLine("- 每次提取不仅是新增，也是清理和优化的机会");
        sb.AppendLine();
        sb.AppendLine("## 合并策略（重要：注意重提取场景）");
        sb.AppendLine();
        sb.AppendLine("- 今日的内容如果已在索引中存在 → **这是重新提取**，不要追加！替换该日期的旧 items");
        sb.AppendLine("- 重新提取时：重新读原始对话，重新判断哪些值得提取，用新的 summary/secondary 替换旧的");
        sb.AppendLine("- 完全不存在的 primary → 新建 entry");
        sb.AppendLine("- 同一天同一 primary 下有多条不同内容 → 各为其 item");
        sb.AppendLine("- items 超过 10 条且最近 30 天未被触发 → 裁掉最早的");
        sb.AppendLine("- 跨多个 items 高频出现的 secondary → 提升到 primary");
        sb.AppendLine("- 格式不合适时自行拆分/合并");
        sb.AppendLine();
        sb.AppendLine("## 注意");
        sb.AppendLine();
        sb.AppendLine("- 泛词如\"你\"\"我\"\"今天\"\"好\"等不要作为关键词");
        sb.AppendLine("- secondary 可以为空数组（空时随一层无条件注入）");
        sb.AppendLine("- items 按 date 升序排列");
        sb.AppendLine("- updatedAt 设为当前 UTC 时间 ISO 格式");
        sb.AppendLine("- 只写文件，不要输出任何交互性内容");

        return sb.ToString();
    }

    #endregion

    /// <summary>
    /// 校验 keywords.json 格式是否合法
    /// </summary>
    private bool ValidateKeywordsFile()
    {
        try
        {
            if (!File.Exists(KeywordsPath)) return false;

            var json = File.ReadAllText(KeywordsPath);
            var file = JsonSerializer.Deserialize<KeywordFile>(json, JsonOptions);
            if (file?.Entries == null) return false;

            foreach (var entry in file.Entries)
            {
                if (entry.Primary.Count == 0) return false;
                if (entry.Items == null) return false;

                foreach (var item in entry.Items)
                {
                    if (string.IsNullOrWhiteSpace(item.Date)) return false;
                    if (string.IsNullOrWhiteSpace(item.Source)) return false;
                    if (string.IsNullOrWhiteSpace(item.Lines)) return false;
                    if (string.IsNullOrWhiteSpace(item.Summary)) return false;
                }
            }

            // 校验通过，更新 updatedAt 字段
            file = file with { UpdatedAt = DateTime.UtcNow.ToString("O") };
            var cleanJson = JsonSerializer.Serialize(file, JsonOptions);
            File.WriteAllText(KeywordsPath, cleanJson);

            _logger.LogInformation("关键词文件格式校验通过，共 {Count} 个条目", file.Entries.Count);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "关键词文件格式校验失败");
            return false;
        }
    }

    #region 注入（对话时调用）

    public Task<(List<ChatMessage>, List<string>)> ResolveKeywordInjectionsAsync(
        string userMessage, List<string> sessionTriggeredKeywords)
    {
        var result = new List<ChatMessage>();
        var newTriggered = new List<string>();
        try
        {
            var file = ReadKeywordFile();
            if (file.Entries.Count == 0) return Task.FromResult<(List<ChatMessage>, List<string>)>((result, newTriggered));

            var now = DateTime.UtcNow.ToString("O");

            // 合并本轮用户消息和已触发的关键词，共同参与匹配
            var matchSource = string.Join(" ", sessionTriggeredKeywords) + " " + userMessage;

            // Phase 1: 优先匹配 secondary（精度高）
            var secondaryHits = new List<(KeywordEntry Entry, List<KeywordItem> Items)>();
            foreach (var entry in file.Entries)
            {
                var matchedItems = entry.Items
                    .Where(item => item.Secondary.Count > 0
                        && item.Secondary.All(s => userMessage.Contains(s, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                if (matchedItems.Count > 0)
                    secondaryHits.Add((entry, matchedItems));
            }

            if (secondaryHits.Count > 0)
            {
                foreach (var (entry, matchedItems) in secondaryHits)
                {
                    _logger.LogInformation("关键词二级命中: primary={Primary}, secondary匹配={Count}条",
                        string.Join(", ", entry.Primary), matchedItems.Count);
                    entry.LastTriggered = now;
                    BuildKeywordMessage(result, entry, matchedItems);

                    var newlyMatched = entry.Primary
                        .Where(kw => userMessage.Contains(kw, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    if (newlyMatched.Count > 0)
                        newTriggered.AddRange(newlyMatched);
                }
            }
            else
            {
                // Phase 2: 二级没命中 → 匹配 primary（范围广）
                foreach (var entry in file.Entries)
                {
                    var matchedPrimary = entry.Primary
                        .Any(kw => matchSource.Contains(kw, StringComparison.OrdinalIgnoreCase));
                    if (!matchedPrimary) continue;

                    _logger.LogInformation("关键词一级命中: primary={Primary}, items={Count}条",
                        string.Join(", ", entry.Primary), entry.Items.Count);
                    entry.LastTriggered = now;
                    BuildKeywordMessage(result, entry, entry.Items);

                    var newlyMatched = entry.Primary
                        .Where(kw => userMessage.Contains(kw, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    if (newlyMatched.Count > 0)
                        newTriggered.AddRange(newlyMatched);
                }
            }

            if (result.Count > 0)
                SaveTriggerTimes(file);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "关键词注入失败");
        }

        return Task.FromResult((result, newTriggered));
    }

    #endregion

    #region 注入辅助方法

    /// <summary>
    /// 构建关键词注入消息
    /// </summary>
    private static void BuildKeywordMessage(List<ChatMessage> result, KeywordEntry entry, List<KeywordItem> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"### 📌 关键词匹配: {string.Join(", ", entry.Primary)}");
        foreach (var item in items)
        {
            var secondaryTag = item.Secondary.Count > 0
                ? $" [二层: {string.Join(", ", item.Secondary)}]"
                : "";
            sb.AppendLine($"- [{item.Date}] {item.Source}({item.Lines}){secondaryTag}");
            sb.AppendLine($"  {item.Summary}");
        }

        result.Add(new ChatMessage(ChatRole.System, sb.ToString().TrimEnd())
        {
            AuthorName = "Keyword",
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    #endregion

    #region 查看（/keyword 指令调用）

    public Task<string> GetKeywordIndexAsync()
    {
        try
        {
            var file = ReadKeywordFile();
            if (file.Entries.Count == 0)
                return Task.FromResult("📭 暂无关键词索引");

            var sb = new StringBuilder();
            sb.AppendLine($"🔑 当前关键词索引（共 {file.Entries.Count} 条，更新于 {file.UpdatedAt}）");
            sb.AppendLine();
            foreach (var entry in file.Entries)
            {
                var primaryStr = string.Join(", ", entry.Primary);
                var secondaryStr = entry.Items
                    .Where(i => i.Secondary.Count > 0)
                    .SelectMany(i => i.Secondary)
                    .Distinct()
                    .ToList();
                var secondaryTag = secondaryStr.Count > 0
                    ? $" [二层: {string.Join(", ", secondaryStr)}]"
                    : " [二层: ]";
                var itemCount = entry.Items.Count;
                sb.AppendLine($"- **{primaryStr}**{secondaryTag} ({itemCount}条)");
            }

            return Task.FromResult(sb.ToString().TrimEnd());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "获取关键词索引失败");
            return Task.FromResult("❌ 读取关键词索引失败");
        }
    }

    #endregion

    #region 内部方法

    private KeywordFile ReadKeywordFile()
    {
        try
        {
            if (!File.Exists(KeywordsPath))
                return new KeywordFile();

            var json = File.ReadAllText(KeywordsPath);
            return JsonSerializer.Deserialize<KeywordFile>(json, JsonOptions) ?? new KeywordFile();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读取关键词文件失败，使用空索引");
            return new KeywordFile();
        }
    }

    private void SaveTriggerTimes(KeywordFile file)
    {
        try
        {
            var json = JsonSerializer.Serialize(file, JsonOptions);
            File.WriteAllText(KeywordsPath, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "保存关键词触发时间失败");
        }
    }

    #endregion
}
