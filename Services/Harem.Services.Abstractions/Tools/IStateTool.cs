using System.ComponentModel;
using System.Text.Json;

namespace Harem.Services.Abstractions.Tools;

[Description("状态维护工具")]
public interface IStateTool
{
    [Description("获取全部状态值")]
    Task<JsonElement> GetStates();

    [Description("获取指定状态值")]
    Task<JsonElement> GetState(string key);

    [Description("更新状态值")]
    Task<string> UpdateState(string key, string value);

    [Description("批量更新状态值")]
    Task<string> UpdateStates(Dictionary<string, string> states);

    [Description("添加状态")]
    Task<string> AddState(string key, string name, string value, string description);
}