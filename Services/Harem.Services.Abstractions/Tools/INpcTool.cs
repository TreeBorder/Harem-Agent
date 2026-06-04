using System.ComponentModel;
using System.Text.Json;

namespace Harem.Services.Abstractions.Tools;

[Description("NPC 管理工具 - 管理角色认识的人")]
public interface INpcTool
{
    [Description("获取所有记得的 NPC 列表")]
    Task<JsonElement> GetAllNpcs();
    
    [Description("按关键词查找 NPC")]
    Task<JsonElement> SearchNpc(string keyword);

    [Description("获取单个 NPC 的详细信息")]
    Task<JsonElement> GetNpc(string key);

    [Description("创建新 NPC")]
    Task<string> CreateNpc(string key, string name, string summary, string[] tags);

    [Description("为 NPC 追加记忆片段")]
    Task<string> AppendNpcMemory(string key, string content);

    [Description("构建/刷新 NPC 向量索引")]
    Task<string> BuildIndex();

    [Description("语义检索 NPC")]
    Task<JsonElement> SearchBySemantic(string description);
}