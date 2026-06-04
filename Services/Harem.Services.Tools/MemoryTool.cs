using System.Text.Json;
using Harem.Services.Abstractions.Agents;
using Harem.Services.Abstractions.Tools;

namespace Harem.Services.Tools;

/// <summary>
/// 记忆工具实现
/// </summary>
public class MemoryTool : IMemoryTool
{
    private readonly IMemoryService _memoryService;

    public MemoryTool(IMemoryService memoryService)
    {
        _memoryService = memoryService;
    }

    public async Task<string> GetMemory()
    {
        var content = await _memoryService.GetLongTermMemoryAsync();
        return string.IsNullOrEmpty(content) ? "长期记忆为空" : content;
    }

    public async Task<string> AppendMemory(string content)
    {
        var success = await _memoryService.AppendLongTermMemoryAsync(content);
        return success ? "已追加到长期记忆" : "追加长期记忆失败";
    }

    public async Task<string> WriteMemory(string content)
    {
        var success = await _memoryService.WriteLongTermMemoryAsync(content);
        return success ? "已覆盖写入长期记忆" : "覆盖写入长期记忆失败";
    }

    public async Task<string> GetDateMemory(string date)
    {
        var content = await _memoryService.GetDailyMemoryAsync(date);
        return string.IsNullOrEmpty(content) ? $"{date} 没有记忆记录" : content;
    }

    public async Task<JsonElement> SearchMemory(string[] keyword)
    {
        var results = await _memoryService.SearchMemoryAsync(keyword);

        if (results.Count == 0)
            return JsonSerializer.SerializeToElement(new { Message = "未找到相关记忆" });

        var formatted = results.Select(r => new
        {
            r.Source,
            r.Content,
            Score = Math.Round(r.Score, 4)
        });

        return JsonSerializer.SerializeToElement(formatted);
    }

    public async Task<string> BuildMemoryIndex()
    {
        try
        {
            await _memoryService.BuildMemoryIndexAsync();
            return "记忆索引构建完成";
        }
        catch (Exception ex)
        {
            return $"记忆索引构建失败：{ex.Message}";
        }
    }
}
