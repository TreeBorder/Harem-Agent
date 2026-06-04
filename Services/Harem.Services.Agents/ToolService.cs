using Harem.Services.Abstractions.Agents;
using Harem.Services.Abstractions.Tools;
using Microsoft.Extensions.AI;

namespace Harem.Services.Agents;

public class ToolService : IToolService
{
    private readonly IHelpTool _helpTool;
    private readonly IFileTool _fileTool;
    private readonly IStateTool _stateTool;
    private readonly IMemoryTool _memoryTool;
    private readonly INpcTool _npcTool;
    private readonly ICharacterImageTool _imageTool;
    private readonly IWebTool _webTool;
    private readonly ICronTool _cronTool;
    private readonly IHermesTool _hermesTool;
    private readonly IMimoVoiceTool _mimoVoiceTool;


    public ToolService(IHelpTool helpTool, IFileTool fileTool, IStateTool stateTool, IMemoryTool memoryTool,
        INpcTool npcTool,  ICharacterImageTool imageTool, IWebTool webTool,
        ICronTool cronTool, IHermesTool hermesTool, IMimoVoiceTool mimoVoiceTool)
    {
        _helpTool = helpTool;
        _fileTool = fileTool;
        _stateTool = stateTool;
        _memoryTool = memoryTool;
        _npcTool = npcTool;
        _imageTool = imageTool;
        _webTool = webTool;
        _cronTool = cronTool;
        _hermesTool = hermesTool;
        _mimoVoiceTool = mimoVoiceTool;
    }

    public List<AITool> GetTools()
    {
        return
        [
            // 帮助工具
            AIFunctionFactory.Create(_helpTool.GetSupportScene, "get_support_scene", "获取gallery工具支持的场景"),
            AIFunctionFactory.Create(_helpTool.GetSupportOutfit, "get_support_outfit", "获取gallery工具支持的装扮"),
            AIFunctionFactory.Create(_helpTool.GetSupportAction, "get_support_action", "获取gallery以及camera工具支持的动作"),

            // 文件工具
            AIFunctionFactory.Create(_fileTool.ReadFile, "read_file", "读取文件内容"),
            AIFunctionFactory.Create(_fileTool.WriteFile, "write_file", "写入文件内容"),

            // 状态工具
            AIFunctionFactory.Create(_stateTool.GetStates, "get_states", "获取角色状态"),
            AIFunctionFactory.Create(_stateTool.GetState, "get_state", "获取角色单个状态"),
            AIFunctionFactory.Create(_stateTool.UpdateState, "update_state", "更新角色单个状态"),
            AIFunctionFactory.Create(_stateTool.UpdateStates, "update_states", "更新角色多个状态"),
            AIFunctionFactory.Create(_stateTool.AddState, "add_state", "添加角色单个状态"),

            // 记忆工具
            AIFunctionFactory.Create(_memoryTool.GetMemory, "get_memory", "获取记忆"),
            AIFunctionFactory.Create(_memoryTool.AppendMemory, "append_memory", "追加记忆"),
            AIFunctionFactory.Create(_memoryTool.WriteMemory, "write_memory", "写入记忆"),
            AIFunctionFactory.Create(_memoryTool.GetDateMemory, "get_date_memory", "获取日期记忆"),
            AIFunctionFactory.Create(_memoryTool.SearchMemory, "search_memory", "搜索记忆"),
            AIFunctionFactory.Create(_memoryTool.BuildMemoryIndex, "build_memory_index", "构建记忆索引"),

            // NPC 工具
            AIFunctionFactory.Create(_npcTool.GetAllNpcs, "get_all_npcs", "获取所有NPC"),
            AIFunctionFactory.Create(_npcTool.SearchNpc, "search_npc", "搜索NPC"),
            AIFunctionFactory.Create(_npcTool.SearchBySemantic, "search_by_semantic", "根据语义搜索NPC"),
            AIFunctionFactory.Create(_npcTool.GetNpc, "get_npc", "获取NPC"),
            AIFunctionFactory.Create(_npcTool.CreateNpc, "create_npc", "创建NPC"),
            AIFunctionFactory.Create(_npcTool.AppendNpcMemory, "append_npc_memory", "追加NPC记忆"),
            AIFunctionFactory.Create(_npcTool.BuildIndex, "build_index", "构建NPC索引"),

            // 媒体工具
            AIFunctionFactory.Create(_imageTool.Camera, "camera",
                "根据角色状态生成内容安全的角色图片，并发送。可能产生不当内容的选项均通过模糊、抽象化处理，返回内容安全的图片。主要用于角色扮演的剧情推演。就像你拍了一张照片并发送一样。请先维护状态信息。GetSupportAction工具获取支持的动作列表。可选参数aspectRatio：图片比例，默认2:3(竖屏)，可选3:2(横屏)。"),
            AIFunctionFactory.Create(_imageTool.Gallery, "gallery",
                "根据场景、服装和动作生成内容安全的角色图片，并发送。可能产生不当内容的选项均通过模糊、抽象化处理，返回内容安全的图片。就像你打开相册并发送一样。请先维护状态信息。get_support_action工具获取支持的动作列表。get_support_outfit工具获取支持的服装列表。get_support_scene工具获取支持的场景列表。可选参数aspectRatio：图片比例，默认2:3(竖屏)，可选3:2(横屏)。"),
            AIFunctionFactory.Create(_imageTool.RandomOutfit, "random_outfit",
                "随机更换一件衣装，自动更新角色状态的outfit。每次换装后LLM应当根据当前衣装选择合适的场景和互动方式。就像你打开衣柜换了一套新衣服。"),
            AIFunctionFactory.Create(_webTool.WebSearch, "web_search", "网络搜索"),

            // 定时任务工具
            AIFunctionFactory.Create(_cronTool.CreateCronJob, "create_cron_job",
                "创建循环定时任务。target: SystemToAgent=系统消息给Agent, MessageToUser=消息给用户, MessageToAgent=用户消息给Agent"),
            AIFunctionFactory.Create(_cronTool.CreateOneShotJob, "create_one_shot_job",
                "创建单次定时任务。fireAt格式: yyyy-MM-dd HH:mm:ss"),
            AIFunctionFactory.Create(_cronTool.ListCronJobs, "list_cron_jobs", "列出所有定时任务"),
            AIFunctionFactory.Create(_cronTool.DeleteCronJob, "delete_cron_job", "删除定时任务"),
            AIFunctionFactory.Create(_cronTool.ToggleCronJob, "toggle_cron_job", "启用或禁用定时任务"),
            
            // Hermes Agent 工具
            AIFunctionFactory.Create(_hermesTool.Chat, "clio_chat",
                "与 Hermes Agent (Clio) 对话，执行代码生成、信息检索、文件操作、分析总结等复杂技术任务。sessionId可选，传null开新对话，传之前返回的session_id续接上下文。返回格式：第一行session_id: xxx用于续接，后续为回答内容"),
            
            // MiMo 语音合成工具
            AIFunctionFactory.Create(_mimoVoiceTool.Voice, "mimo_voice",
                "使用小米MiMo TTS(voicedesign)将文本转为语音并发送。designPrompt可选音色设计提示词，不传时自动从状态MimoVoice读取。可用mimo_voice_help获取详细用法参考"),
            AIFunctionFactory.Create(_mimoVoiceTool.GetHelp, "mimo_voice_help",
                "获取 MiMo 语音合成 (voicedesign) 的详细用法参考，包括音频标签、风格控制等"),
        ];
    }
}