using Harem.Contracts.Domain.Entities;

namespace Harem.Services.Abstractions.Agents;

/// <summary>
/// 状态服务 - 提供平铺化的状态访问和更新
/// </summary>
public interface IStateService
{
    /// <summary>
    /// 获取所有状态（平铺成字典，包含固定+动态）
    /// </summary>
    Task<List<StateItem>> GetAllStatesAsync();

    /// <summary>
    /// 获取单个状态值
    /// </summary>
    Task<StateItem?> GetStateAsync(string key);

    /// <summary>
    /// 更新状态值（自动判断是固定还是动态）
    /// </summary>
    Task<bool> UpdateStateAsync(string key, string value, bool isAgent = false);

    /// <summary>
    /// 批量更新状态值（自动判断是固定还是动态）
    /// </summary>
    Task<bool> UpdateStatesAsync(Dictionary<string, string> states, bool isAgent = false);

    Task<bool> AddStateAsync(string key, string name, string value, string description);
}