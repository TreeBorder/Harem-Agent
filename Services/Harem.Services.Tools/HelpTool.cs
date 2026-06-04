using System.Text.Json;
using Harem.Contracts.Domain.Entities;
using Harem.Services.Abstractions.Agents;
using Harem.Services.Abstractions.Tools;

namespace Harem.Services.Tools;

/// <summary>
/// 帮助工具 - 返回 ComfyUI 图片生成支持的参数列表
/// </summary>
public class HelpTool : IHelpTool
{
    private readonly IFileService _fileService;
    private const string ComfyDirectory = ".comfyui";
    private const string PromptsDirectory = "prompts";

    public HelpTool(IFileService fileService)
    {
        _fileService = fileService;
    }

    public async Task<JsonElement> GetSupportScene()
    {
        return await GetPromptKeysAsync("scene.json");
    }

    public async Task<JsonElement> GetSupportOutfit()
    {
        return await GetPromptKeysAsync("outfit.json");
    }

    public async Task<JsonElement> GetSupportAction()
    {
        return await GetPromptKeysAsync("action.json");
    }

    private async Task<JsonElement> GetPromptKeysAsync(string fileName)
    {
        try
        {
            var path = Path.Combine(ComfyDirectory, PromptsDirectory, fileName);
            var json = await _fileService.GetJsonFileAsync(path);
            if (json is null)
                return JsonSerializer.SerializeToElement(new { Message = $"未找到 {fileName} 配置" });
            var blocks = json.Value.Deserialize<Dictionary<string, PromptBlock>>();

            if (blocks == null || blocks.Count == 0)
                return JsonSerializer.SerializeToElement(new { Message = $"未找到 {fileName} 配置" });

            var items = blocks.Select(b => new
            {
                Key = b.Key,
                Name = b.Value.DisplayName ?? b.Key,
                Note = b.Value.Note ?? ""
            });

            return JsonSerializer.SerializeToElement(items);
        }
        catch (Exception ex)
        {
            return JsonSerializer.SerializeToElement(new { Message = $"读取 {fileName} 失败: {ex.Message}" });
        }
    }
}
