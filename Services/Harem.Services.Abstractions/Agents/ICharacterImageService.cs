namespace Harem.Services.Abstractions.Agents;

/// <summary>
/// 角色图片服务
/// </summary>
public interface ICharacterImageService
{
    /// <summary>
    /// 生成角色图片（Camera模式 - 根据当前状态）
    /// </summary>
    /// <param name="sessionKey">异步返回的sessionKey</param>
    /// <param name="action">动作</param>
    /// <param name="aspectRatio">图片比例，默认2:3，可选3:2</param>
    Task<bool> GenerateCameraImageAsync(string sessionKey, string action, string? aspectRatio = null);

    /// <summary>
    /// 生成角色图片（Gallery模式 - 指定场景、服装、动作）
    /// </summary>
    /// <param name="scene">场景</param>
    /// <param name="outfit">服装</param>
    /// <param name="action">动作</param>
    /// <param name="aspectRatio">图片比例，默认2:3，可选3:2</param>
    /// <param name="sessionKey">异步返回的sessionKey</param>
    Task<bool> GenerateGalleryImageAsync(string sessionKey, string scene, string outfit, string action, string? aspectRatio = null);
}