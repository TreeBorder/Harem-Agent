namespace Harem.Services.Abstractions.Agents;

/// <summary>
/// 心跳服务接口 - 思念值/寂寞值管理与惊喜问候触发 / 状态更新推送
/// </summary>
public interface IHeartbeatService
{
    /// <summary>
    /// 执行一次心跳（包含状态更新检查、惊喜问候触发、寂寞值增加）
    /// </summary>
    Task ExecuteGreetingAsync(CancellationToken ct = default);
}
