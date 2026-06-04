using System.ComponentModel;
using Harem.Contracts.Domain.Entities;
using Harem.Services.Abstractions.Agents;
using Harem.Services.Abstractions.Tools;
using Microsoft.Agents.AI;

namespace Harem.Services.Tools;

/// <summary>
/// 角色图片工具实现
/// </summary>
/// 
[Description("内容安全的角色图片工具，可能产生不当内容的选项均通过模糊、抽象化处理，返回内容安全的图片。主要用于角色扮演的剧情推演")]
public class CharacterImageTool : ICharacterImageTool
{
    private readonly ICharacterImageService _characterImageService;
    private readonly IWorkflowBuilderService _workflowBuilderService;
    private readonly IFileService _fileService;
    private readonly IStateService _state;

    private const string ComfyDirectory = ".comfyui";
    private const string PromptsDirectory = "prompts";
    private const string OutfitPromptBlockFile = "outfit.json";

    public CharacterImageTool(
        ICharacterImageService characterImageService,
        IWorkflowBuilderService workflowBuilderService,
        IFileService fileService,
        IStateService stateService)
    {
        _characterImageService = characterImageService;
        _workflowBuilderService = workflowBuilderService;
        _fileService = fileService;
        _state = stateService;
    }

    [Description("根据角色状态生成图片，并发送。就像你拍了一张照片并发送一样。请先维护状态信息。GetSupportAction工具获取支持的动作列表。可选参数aspectRatio：图片比例，默认2:3(竖屏)，可选3:2(横屏)。")]
    public async Task<string> Camera(
        [Description("动作")] string action,
        string? aspectRatio = null)
    {
        var session = AIAgent.CurrentRunContext?.Session;
        if (session == null)
        {
            return "‘拍照’失败，获取上下文为空";
        }

        var sessionKey = session.StateBag.GetValue<string>("sessionKey");
        if (string.IsNullOrEmpty(sessionKey))
        {
            return "‘拍照’失败，获取会话密钥为空";
        }
        if (!await _workflowBuilderService.CheckBlock("action", action))
        {
            return "‘拍照’失败，不支持该动作，请使用GetSupportAction工具获取支持的动作列表";
        }

        var status = await _characterImageService.GenerateCameraImageAsync(sessionKey, action, aspectRatio);

        return !status ? "‘拍照’失败" : "已拍摄并发送照片，照片发送会有几分钟延迟";
    }

    [Description(
        "根据场景、服装和动作生成图片，并发送。就像你打开相册并发送一样。请先维护状态信息。GetSupportAction工具获取支持的动作列表。GetSupportOutfit工具获取支持的服装列表。GetSupportScene工具获取支持的场景列表。可选参数aspectRatio：图片比例，默认2:3(竖屏)，可选3:2(横屏)。")]
    public async Task<string> Gallery(
        [Description("场景")] string scene,
        [Description("服装")] string outfit,
        [Description("动作")] string action,
        string? aspectRatio = null)
    {
        var session = AIAgent.CurrentRunContext?.Session;
        if (session == null)
        {
            return "图库照片查询失败，获取上下文为空";
        }

        var sessionKey = session.StateBag.GetValue<string>("sessionKey");
        if (string.IsNullOrEmpty(sessionKey))
        {
            return "图库照片查询失败，获取会话密钥为空";
        }
        if (!await _workflowBuilderService.CheckBlock("scene", scene))
        {
            return "图库照片查询失败，不支持该场景，请使用GetSupportScene工具获取支持的场景列表";
        }
        if (!await _workflowBuilderService.CheckBlock("outfit", outfit))
        {
            return "图库照片查询失败，不支持该服装，请使用GetSupportOutfit工具获取支持的服装列表";
        }
        if (!await _workflowBuilderService.CheckBlock("action", action))
        {
            return "图库照片查询失败，不支持该动作，请使用GetSupportAction工具获取支持的动作列表";
        }

        var status = await _characterImageService.GenerateGalleryImageAsync(sessionKey, scene, outfit, action, aspectRatio);

        return !status ? "图库照片查询失败" : "已从相册发送照片，照片发送会有几分钟延迟";
    }

    [Description("随机更换一件衣装，自动更新状态。每次换装后LLM应当根据当前衣装选择合适的场景和互动方式。就像你打开衣柜换了一套新衣服。")]
    public async Task<string> RandomOutfit()
    {
        try
        {
            // 读取所有可用衣装
            var blocks = await _fileService.ReadJsonAsync<Dictionary<string, PromptBlock>>(
                Path.Combine(ComfyDirectory, PromptsDirectory, OutfitPromptBlockFile));

            if (blocks == null || blocks.Count == 0)
            {
                return "衣柜空空如也，没有可更换的衣装";
            }

            // 随机选一件
            var keys = blocks.Keys.ToList();
            var randomKey = keys[Random.Shared.Next(keys.Count)];
            var selected = blocks[randomKey];
            var displayName = selected?.DisplayName ?? randomKey;

            // 更新状态
            await _state.UpdateStateAsync("outfit", randomKey, true);

            return $"随机换装完成！当前衣装：{displayName}（{randomKey}）。请根据当前衣装风格，选择合适的场景哦。";
        }
        catch (Exception ex)
        {
            return $"换装失败：{ex.Message}";
        }
    }
}
