using System.Text.Json;
using Harem.Contracts.Domain.Entities;
using Harem.Services.Abstractions.Agents;
using Harem.Services.Abstractions.Tools;

namespace Harem.Services.Tools;

/// <summary>
/// NPC 工具实现
/// </summary>
public class NpcTool : INpcTool
{
    private readonly INpcService _npcService;

    public NpcTool(INpcService npcService)
    {
        _npcService = npcService;
    }

    public async Task<JsonElement> GetAllNpcs()
    {
        var npcs = await _npcService.GetAllNpcsAsync(includeArchived: false);
        var items = npcs.Select(n => new
        {
            n.Key,
            n.Name,
            n.Tags,
            Intimacy = n.CurrentIntimacy(),
            Depth = n.GetMemoryDepth().ToString()
        });
        return JsonSerializer.SerializeToElement(items);
    }

    public async Task<JsonElement> SearchNpc(string keyword)
    {
        var results = await _npcService.SearchByKeywordAsync(keyword);
        var items = results.Select(n => new
        {
            n.Key,
            n.Name,
            n.Summary,
            n.Tags,
            Intimacy = n.CurrentIntimacy()
        });
        return JsonSerializer.SerializeToElement(items);
    }

    public async Task<JsonElement> GetNpc(string key)
    {
        var npc = await _npcService.GetNpcAsync(key);
        if (npc is null)
            return JsonSerializer.SerializeToElement(new { Message = $"NPC `{key}` 不存在" });

        var memories = await _npcService.GetMemoriesAsync(key);
        return JsonSerializer.SerializeToElement(new
        {
            npc.Key,
            npc.Name,
            npc.Summary,
            npc.Tags,
            Intimacy = npc.CurrentIntimacy(),
            Depth = npc.GetMemoryDepth().ToString(),
            Memories = memories.Take(5).Select(m => m.Content)
        });
    }

    public async Task<string> CreateNpc(string key, string name, string summary, string[] tags)
    {
        var success = await _npcService.CreateNpcAsync(new NpcInfo
        {
            Key = key,
            Name = name,
            Summary = summary,
            Tags = tags.ToList()
        });

        return success ? $"已创建 NPC `{name}`" : $"NPC `{key}` 已存在";
    }

    public async Task<string> AppendNpcMemory(string key, string content)
    {
        var npc = await _npcService.GetNpcAsync(key);
        if (npc is null)
            return $"NPC `{key}` 不存在";

        var success = await _npcService.AppendMemoryAsync(key, new NpcMemory
        {
            Content = content,
            IntimacyDelta = 1 // 默认每次互动+1
        });

        return success ? $"已为 `{npc.Name}` 追加记忆" : "追加失败";
    }

    public async Task<string> BuildIndex()
    {
        try
        {
            await _npcService.BuildIndexAsync();
            return "NPC 索引构建完成";
        }
        catch (Exception ex)
        {
            return $"NPC 索引构建失败：{ex.Message}";
        }
    }

    public async Task<JsonElement> SearchBySemantic(string description)
    {
        var results = await _npcService.SearchBySemanticAsync(description);
        var items = results.Select(n => new
        {
            n.Key,
            n.Name,
            n.Summary,
            n.Tags,
            Intimacy = n.CurrentIntimacy()
        });
        return JsonSerializer.SerializeToElement(items);
    }
}
