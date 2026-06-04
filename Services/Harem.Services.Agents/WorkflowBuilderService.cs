using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Harem.Contracts.Configurations.AgentWorkspace;
using Harem.Contracts.Domain.Entities;
using Harem.Services.Abstractions.Agents;
using Microsoft.Extensions.Options;

namespace Harem.Services.Agents;

public class WorkflowBuilderService : IWorkflowBuilderService
{
    private readonly IFileService _fileService;

    private readonly string _character;

    private const string ComfyDirectory = ".comfyui";
    private const string WorkflowDirectory = "workflows";
    private const string PromptsDirectory = "prompts";

    private const string ScenePromptBlockFile = "scene.json";
    private const string OutfitPromptBlockFile = "outfit.json";
    private const string ActionPromptBlockFile = "action.json";
    private const string CharacterPromptBlockFile = "character.json";
    private const string AnglePromptBlockFile = "angle.json";
    private const string EnvironmentPromptBlockFile = "environment.json";
    private const string QualityPromptBlockFile = "quality.json";

    private const string DefaultWorkflowFile = "sdxl.json";
    private const string NoFaceWorkflowFile = "sdxl_noface.json";

    public WorkflowBuilderService(IFileService fileService, IOptions<AgentOptions> options)
    {
        _fileService = fileService;
        _character = options.Value.Id;
    }

    private async Task<PromptBlock> GetPromptsAsync(string file, string key, bool nullRandom = true)
    {
        var blocks =
            await _fileService.ReadJsonAsync<Dictionary<string, PromptBlock>>(Path.Combine(ComfyDirectory,
                PromptsDirectory, file));

        if (blocks == null) throw new FileNotFoundException("prompts blocks not found");
        blocks.TryGetValue(key, out var block);
        if (block != null) return block;
        if (!nullRandom) throw new FileNotFoundException("prompts block not found");
        block ??= blocks.Values.OrderBy(x => Guid.NewGuid()).First();
        return block;
    }

    private async Task<PromptBlock> GetRandomPromptsAsync(string file)
    {
        var blocks =
            await _fileService.ReadJsonAsync<Dictionary<string, PromptBlock>>(Path.Combine(ComfyDirectory,
                PromptsDirectory, file));
        if (blocks == null) throw new FileNotFoundException("prompts blocks not found");
        var block = blocks.Values.OrderBy(x => Guid.NewGuid()).First();
        return block;
    }

    public async Task<string?> BuildAsync(string scene, string outfit, string action, string? aspectRatio = null)
    {
        // 根据比例设置宽高
        var (width, height) = aspectRatio switch
        {
            "3:2" => (1536, 1024),
            _ => (1024, 1536) // 默认 2:3
        };

        var charPrompt =
            await GetPromptsAsync(CharacterPromptBlockFile, _character);

        var qualityPrompt =
            await GetPromptsAsync(QualityPromptBlockFile, "default");

        var scenePrompt =
            await GetPromptsAsync(ScenePromptBlockFile, scene);

        var outfitPrompt =
            await GetPromptsAsync(OutfitPromptBlockFile, outfit);

        var actionPrompt =
            await GetPromptsAsync(ActionPromptBlockFile, action);

        var environmentKey = await ResolveEnvironmentKeyAsync(scene);
        var envPrompt =
            await GetPromptsAsync(EnvironmentPromptBlockFile, environmentKey);

        var anglePrompt =
            await GetRandomPromptsAsync(AnglePromptBlockFile);

        // 8. 构建提示词（按正确顺序）
        // 顺序: Quality → Env → Scene → Angle → Character → Outfit → Action
        var positiveParts = new List<string>();
        var negativeParts = new List<string>();

        // 1) Quality
        positiveParts.AddRange(qualityPrompt.PositivePrompts);
        negativeParts.AddRange(qualityPrompt.NegativePrompts);

        // 2) Env

        positiveParts.AddRange(envPrompt.PositivePrompts);
        negativeParts.AddRange(envPrompt.NegativePrompts);

        // 3) Scene

        positiveParts.AddRange(scenePrompt.PositivePrompts);
        negativeParts.AddRange(scenePrompt.NegativePrompts);

        // 4) Angle

        positiveParts.AddRange(anglePrompt.PositivePrompts);
        negativeParts.AddRange(anglePrompt.NegativePrompts);

        // 5) Character
        positiveParts.AddRange(charPrompt.PositivePrompts);
        negativeParts.AddRange(charPrompt.NegativePrompts);

        // 6) Outfit
        positiveParts.AddRange(outfitPrompt.PositivePrompts);
        negativeParts.AddRange(outfitPrompt.NegativePrompts);

        // 7) Action
        positiveParts.AddRange(actionPrompt.PositivePrompts);
        negativeParts.AddRange(actionPrompt.NegativePrompts);

        // 5. 去重并组合
        var positive = string.Join(", ", positiveParts.Distinct());
        var negative = string.Join(", ", negativeParts.Distinct());

        var seed = Random.Shared.Next(int.MaxValue);
        // 生成确定性的seed（基于参数+实际seed）
        var actualSeed = StableSeed(_character, scene, outfit, action, environmentKey, seed.ToString());

        var workflowKey = DefaultWorkflowFile;
        if (negative.Contains("face visible")) workflowKey = NoFaceWorkflowFile;

        var workflow = await _fileService.GetFileAsync(Path.Combine(ComfyDirectory, WorkflowDirectory, workflowKey));
        if (string.IsNullOrEmpty(workflow)) throw new FileNotFoundException("workflow not found");
        return workflow
            .Replace("{{PROMPT}}", EscapeJson(positive))
            .Replace("{{NEGATIVE_PROMPT}}", EscapeJson(negative))
            .Replace("{{SEED}}", actualSeed.ToString())
            .Replace("{{WIDTH}}", width.ToString())
            .Replace("{{HEIGHT}}", height.ToString())
            .Replace("{{STEPS}}", "25")
            .Replace("{{CFG}}", "7.0")
            .Replace("{{ROLE_NAME}}", _character)
            .Replace("{{FILENAME_PREFIX}}", _character);
    }

    public async Task<bool> CheckBlock(string type, string key)
    {
        var file = $"{type}.json";
        var blocks =
            await _fileService.ReadJsonAsync<Dictionary<string, PromptBlock>>(Path.Combine(ComfyDirectory,
                PromptsDirectory, file));

        if (blocks == null) return false;
        blocks.TryGetValue(key, out var block);
        return block != null;
    }

    private static string EscapeJson(string value)
    {
        return JsonSerializer.Serialize(value).Trim('"');
    }

    private static string GetTimePeriod()
    {
        var hour = DateTime.Now.Hour;
        return hour switch
        {
            >= 6 and < 11 => "morning",
            >= 11 and < 17 => "day",
            >= 17 and < 20 => "evening",
            _ => "night"
        };
    }

    /// <summary>
    /// 根据场景配置的 environmentCategory + 当前时间，在 environment.json 中按优先级查找。
    /// 链路: {category}_{time} → {category}_day(morning兜底) → {category} → default
    /// </summary>
    private async Task<string> ResolveEnvironmentKeyAsync(string scene)
    {
        // 1. 从 scene.json 读取场景声明的 environmentCategory
        var category = await GetSceneEnvironmentCategoryAsync(scene);

        // 未声明分类或分类为 studio，直接按 key 查找
        if (string.IsNullOrEmpty(category))
            return "default";
        if (category == "studio")
            return "studio";

        // 2. 获取时间段
        var time = GetTimePeriod();

        // 3. 加载 environment.json 可用键集合
        var envBlocks = await _fileService.ReadJsonAsync<Dictionary<string, PromptBlock>>(
            Path.Combine(ComfyDirectory, PromptsDirectory, EnvironmentPromptBlockFile));
        if (envBlocks == null)
            return "default";

        // 4. 按优先级尝试: {category}_{time} → {category}_day(morning兜底) → {category} → default
        var candidates = new List<string> { $"{category}_{time}" };
        if (time == "morning")
            candidates.Add($"{category}_day"); // outdoor_morning 不存在时兜底到 outdoor_day
        candidates.Add(category);

        foreach (var key in candidates)
        {
            if (envBlocks.ContainsKey(key))
                return key;
        }

        return "default";
    }

    /// <summary>
    /// 从 scene.json 中读取指定场景的 environmentCategory 字段。
    /// 该字段由配置声明，无需硬编码映射。
    /// </summary>
    private async Task<string?> GetSceneEnvironmentCategoryAsync(string scene)
    {
        var sceneText = await _fileService.GetFileAsync(
            Path.Combine(ComfyDirectory, PromptsDirectory, ScenePromptBlockFile));
        if (string.IsNullOrWhiteSpace(sceneText)) return null;
        var sceneJson = JsonElement.Parse(sceneText);

        if (sceneJson.ValueKind != JsonValueKind.Object)
            return null;

        if (!sceneJson.TryGetProperty(scene, out var sceneElement))
            return null;

        if (!sceneElement.TryGetProperty("environmentCategory", out var categoryElement))
            return null;

        return categoryElement.ValueKind == JsonValueKind.String
            ? categoryElement.GetString()
            : null;
    }

    private static int StableSeed(params string[] parts)
    {
        var input = string.Join("|", parts);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var seed = BitConverter.ToInt32(hash, 0);
        return seed & int.MaxValue;
    }
}