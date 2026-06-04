using System.Text.Json;
using Harem.Services.Abstractions.Agents;
using Harem.Services.Abstractions.Tools;

namespace Harem.Services.Tools;

/// <summary>
/// 状态工具实现 - 智能体通过此工具操作自身状态
/// </summary>
public class StateTool : IStateTool
{
    private readonly IStateService _stateService;

    public StateTool(IStateService stateService)
    {
        _stateService = stateService;
    }

    public async Task<JsonElement> GetStates()
    {
        var states = await _stateService.GetAllStatesAsync();
        var dict = states.ToDictionary(
            s => s.Key,
            s => new { s.Name, s.Value, s.Description, s.LastUpdatedAt }
        );
        return JsonSerializer.SerializeToElement(dict);
    }

    public async Task<JsonElement> GetState(string key)
    {
        var state = await _stateService.GetStateAsync(key);
        if (state == null)
            return JsonSerializer.SerializeToElement(new
            {
                Message = $"状态 {key} 获取失败，状态不存在"
            });

        return JsonSerializer.SerializeToElement(new
        {
            state.Name,
            state.Value,
            state.Description,
            state.LastUpdatedAt
        });
    }

    public async Task<string> UpdateState(string key, string value)
    {
        var success = await _stateService.UpdateStateAsync(key, value, true);
        return success ? $"状态 {key} 已更新为 {value}" : $"状态 {key} 更新失败，状态不存在或禁止修改";
    }

    public async Task<string> UpdateStates(Dictionary<string, string> states)
    {
        var success = await _stateService.UpdateStatesAsync(states, true);
        return success ? "批量更新状态成功" : "批量更新状态失败";
    }

    public async Task<string> AddState(string key, string name, string value, string description)
    {
        var success = await _stateService.AddStateAsync(key, name, value, description);
        return success ? $"状态 {key} 已添加" : $"状态 {key} 添加失败，键已存在";
    }
}