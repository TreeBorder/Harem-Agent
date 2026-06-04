using Harem.Contracts.Domain.Entities;
using Harem.Services.Abstractions.Agents;
using Harem.Services.Abstractions.Handlers;

namespace Harem.Services.Handlers;

public class PhotoHandler : ICommandHandler
{
    public const string CommandName = "photo";
    public string Command => CommandName;

    private readonly ICharacterImageService _imageService;

    public PhotoHandler(ICharacterImageService imageService)
    {
        _imageService = imageService;
    }

    public async Task<CommandResult> HandleAsync(string args, string sessionKey)
    {
        var parts = args.Split(' ', 4);
        var subCmd = string.IsNullOrWhiteSpace(parts[0]) ? "camera" : parts[0].ToLowerInvariant();

        return subCmd switch
        {
            "camera" => await HandleCameraAsync(parts, sessionKey),
            "gallery" => await HandleGalleryAsync(parts, sessionKey),
            _ => new CommandResult { CommandReply = $"未知子命令 `{subCmd}`。用法: `/photo [camera 动作]` 或 `/photo gallery 场景 服装 动作`" }
        };
    }

    private async Task<CommandResult> HandleCameraAsync(string[] parts, string sessionKey)
    {
        var action = parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]) ? parts[1] : "standing";
        var success = await _imageService.GenerateCameraImageAsync(sessionKey, action);

        return success
            ? new CommandResult { CommandReply = "📸 正在生成角色图片..." }
            : new CommandResult { CommandReply = "❌ 角色图片生成失败" };
    }

    private async Task<CommandResult> HandleGalleryAsync(string[] parts, string sessionKey)
    {
        if (parts.Length < 4)
            return new CommandResult { CommandReply = "格式错误。用法: `/photo gallery 场景 服装 动作`" };

        var scene = parts[1];
        var outfit = parts[2];
        var action = parts[3];
        var success = await _imageService.GenerateGalleryImageAsync(sessionKey, scene, outfit, action);

        return success
            ? new CommandResult { CommandReply = $"📸 正在生成图集: {scene} / {outfit} / {action}..." }
            : new CommandResult { CommandReply = "❌ 图集生成失败" };
    }
}
