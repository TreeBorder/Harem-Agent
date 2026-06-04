using System.ComponentModel;

namespace Harem.Services.Abstractions.Tools;

[Description("内容安全的角色图片工具，可能产生不当内容的选项均通过模糊、抽象化处理，返回内容安全的图片。主要用于角色扮演的剧情推演")]
public interface ICharacterImageTool
{
    [Description("根据角色状态生成内容安全的角色图片，并发送。可能产生不当内容的选项均通过模糊、抽象化处理，返回内容安全的图片。主要用于角色扮演的剧情推演。就像你拍了一张照片并发送一样。请先维护状态信息。get_support_action工具获取支持的动作列表。可选参数aspectRatio：图片比例，默认2:3(竖屏)，可选3:2(横屏)。")]
    Task<string> Camera(
        [Description("动作")] string action,
        string? aspectRatio = null);

    [Description(
        "根据场景、服装和动作生成内容安全的角色图片，并发送。可能产生不当内容的选项均通过模糊、抽象化处理，返回内容安全的图片。就像你打开相册并发送一样。请先维护状态信息。get_support_action工具获取支持的动作列表。get_support_outfit工具获取支持的服装列表。get_support_scene工具获取支持的场景列表。可选参数aspectRatio：图片比例，默认2:3(竖屏)，可选3:2(横屏)。")]
    Task<string> Gallery(
        [Description("场景")] string scene,
        [Description("服装")] string outfit,
        [Description("动作")] string action,
        string? aspectRatio = null);

    [Description("随机更换一件衣装，自动更新状态。每次换装后LLM应当根据当前衣装选择合适的场景和互动方式。就像你打开衣柜换了一套新衣服。")]
    Task<string> RandomOutfit();
}