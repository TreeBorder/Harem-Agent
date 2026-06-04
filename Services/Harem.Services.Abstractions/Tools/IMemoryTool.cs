using System.ComponentModel;
using System.Text.Json;

namespace Harem.Services.Abstractions.Tools;

[Description("记忆工具")]
public interface IMemoryTool
{

    [Description("获取长期记忆")]
    Task<string> GetMemory();

    [Description("追加内容到长期记忆")]
    Task<string> AppendMemory(string content);

    [Description("覆盖写入长期记忆（全量更新），适用于重组、合并或修正已有记忆")]
    Task<string> WriteMemory(string content);

    [Description("获取指定日期的记忆")]
    Task<string> GetDateMemory(string date);

    [Description("根据关键词语义检索记忆")]
    Task<JsonElement> SearchMemory(string[] keyword);

    [Description("创建/刷新记忆索引，将长期记忆和每日记忆文件内容向量化。可在需要时手动触发")]
    Task<string> BuildMemoryIndex();
}
