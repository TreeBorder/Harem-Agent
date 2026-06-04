using Harem.Contracts.Domain.Entities;
using Harem.Services.Abstractions.Agents;

namespace Harem.Services.Agents;

/// <summary>
/// 状态服务实现 - 将存储层的字典结构平铺为智能体友好的访问方式
/// </summary>
public class StateService : IStateService
{
    private readonly IFileService _fileService;
    private const string StateFile = "state.json";

    public StateService(IFileService fileService)
    {
        _fileService = fileService;
    }

    public async Task<List<StateItem>> GetAllStatesAsync()
    {
        var states = await _fileService.ReadJsonAsync<List<StateItem>>(StateFile);
        return states ?? [];
    }

    public async Task<StateItem?> GetStateAsync(string key)
    {
        var states = await GetAllStatesAsync();
        return states.FirstOrDefault(s => s.Key == key);
    }

    public async Task<bool> UpdateStateAsync(string key, string value, bool isAgent = false)
    {
        var states = await GetAllStatesAsync();
        var state = states.FirstOrDefault(s => s.Key == key);

        if (state == null)
            return false;

        if (isAgent && !state.AgentEditable) return false;
        state.Value = value;
        state.LastUpdatedAt = DateTime.UtcNow;

        await SaveStatesAsync(states);
        return true;
    }

    public async Task<bool> UpdateStatesAsync(Dictionary<string, string> states, bool isAgent = false)
    {
        bool resultStates = true;
        var existingStates = await GetAllStatesAsync();
        foreach (var state in states)
        {
            var existingState = existingStates.FirstOrDefault(s => s.Key == state.Key);
            if (existingState != null)
            {
                if (isAgent && !existingState.AgentEditable)
                {
                    resultStates = false;
                    continue;
                }

                existingState.Value = state.Value;
                existingState.LastUpdatedAt = DateTime.UtcNow;
            }
            else
            {
                resultStates = false;
            }
        }

        await SaveStatesAsync(existingStates);
        return resultStates;
    }

    public async Task<bool> AddStateAsync(string key, string name, string value, string description)
    {
        var states = await GetAllStatesAsync();

        if (states.Any(s => s.Key == key))
            return false;

        states.Add(new StateItem
        {
            Key = key,
            Name = name,
            Value = value,
            Description = description,
            LastUpdatedAt = DateTime.UtcNow
        });

        await SaveStatesAsync(states);
        return true;
    }

    private async Task SaveStatesAsync(List<StateItem> states)
    {
        await _fileService.WriteJsonAsync(StateFile, states);
    }
}