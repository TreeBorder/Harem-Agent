using Harem.Services.Abstractions.Agents;
using Harem.Services.Abstractions.Tools;
using Microsoft.Extensions.Logging;

namespace Harem.Services.Tools;

/// <summary>
/// MiMo 语音合成工具 — 通过 MimoCli CLI 调用小米 MiMo TTS API
/// </summary>
public class MimoVoiceTool : IMimoVoiceTool
{
    private readonly IStateService _stateService;
    private readonly IMimoService _mimoService;
    private readonly ILogger<MimoVoiceTool> _logger;

    private const string MimoVoiceStateKey = "MimoVoice";

    public MimoVoiceTool(
        IStateService stateService,
        IMimoService mimoService,
        ILogger<MimoVoiceTool> logger)
    {
        _stateService = stateService;
        _mimoService = mimoService;
        _logger = logger;
    }

    public async Task<string> Voice(string content, string? designPrompt = null)
    {
        if (string.IsNullOrWhiteSpace(content))
            return "语音合成失败：文本内容为空";

        try
        {
            // 没传 designPrompt 时，从状态系统读取音色设计参数
            if (string.IsNullOrWhiteSpace(designPrompt))
            {
                // 支持 MimoVoice 和 mimo_voice 两种 key 格式
                var mimoVoiceState = await _stateService.GetStateAsync(MimoVoiceStateKey);
                mimoVoiceState ??= await _stateService.GetStateAsync("mimo_voice");
                if (mimoVoiceState != null && !string.IsNullOrWhiteSpace(mimoVoiceState.Value))
                {
                    designPrompt = mimoVoiceState.Value;
                    _logger.LogInformation("MimoVoice 从状态读取: {Value}", mimoVoiceState.Value);
                }
            }

            // 获取 sessionKey
            var session = Microsoft.Agents.AI.AIAgent.CurrentRunContext?.Session;
            string? sessionKey = null;
            if (session != null)
            {
                sessionKey = session.StateBag.GetValue<string>("sessionKey");
            }

            if (string.IsNullOrEmpty(sessionKey))
                return "语音合成失败：获取会话密钥为空";

            var success = await _mimoService.SynthesizeAndSendAsync(content, designPrompt, sessionKey);
            if (!success)
                return "语音合成失败：CLI 执行或文件发送异常";

            var modeLabel = !string.IsNullOrWhiteSpace(designPrompt) ? "voicedesign" : "speak";
            return $"✅ MiMo语音合成 ({modeLabel}) 已完成并发送";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MimoVoice 合成异常");
            return $"语音合成异常：{ex.Message}";
        }
    }

    public Task<string> GetHelp()
    {
        var docs = @"MiMo 语音合成 (mimo-v2.5-tts-voicedesign) — 用法参考

mimo_voice 有两个参数：
- content：要合成的文本（必填），文本中可嵌入风格标签和音频标签
- designPrompt：音色设计提示词（可选），不传时自动从状态 mimo_voice 读取

===== 音色设计 (designPrompt) =====

一条好的音色描述通常涵盖以下维度（不需要面面俱到）：
- 性别与年龄：如 young woman in her mid-20s、五十多岁的中年男性
- 音色/质感：如 deep and gravelly、丝滑醇厚带着磁性
- 情绪/语气：如 warm and confident、温柔但带着一丝疲惫
- 语速/节奏：如 slow and deliberate、语速极快像连珠炮

可选维度增加丰富度：
- 角色/人设：narrator, podcast host, 评书先生, 深夜电台DJ
- 说话风格：casual and colloquial, 一本正经地, 压低嗓音像在密谋
- 场景描写：narrating a nature documentary, 在给投资人路演
- 年代参照：1940s film noir, 八十年代译制片配音

写法建议：

简洁描述型 — 用关键词或一句话快速勾勒声音轮廓：
Heavy Russian accent, gruff middle-aged male, blunt and matter-of-fact.

专业描述型 — 通过场景、人设或多维度细节立体刻画声音：
Young female, extreme close-up with a binaural, ear-to-ear ASMR feel. Audible breathing, subtle swallowing, and soft natural lip sounds. She speaks very slowly, creating a deeply relaxing and immersive experience.

一位年迈的老先生，说带北方口音的普通话，语速缓慢而沉稳，嗓音略带沙哑和沧桑感，仿佛一位饱经风霜的老爷爷在讲故事，充满岁月的智慧。

注意事项：
- 长度：1-4句即可，核心特征描述清楚比堆砌维度更重要
- 避免矛盾特征（如""稚嫩的童声 + CEO气场""）
- 避免混响、回声、EQ等音质效果词
- 避免模糊词（""普通的""正常的""外国的""）
- 中英文均可
- 合成文本要贴合音色，如温柔治愈系女声搭配晚安独白

===== 风格标签 =====

在 content 开头加 (风格) 定整体基调，支持复合标签：
(慵懒)再让我睡五分钟…  (怅然 温柔)这么多年过去了…
支持的括号格式：() （） []

可用风格：
基础情绪：开心 悲伤 愤怒 恐惧 惊讶 兴奋 委屈 平静 冷漠
复合情绪：怅然 欣慰 无奈 愧疚 释然 嫉妒 厌倦 忐忑 动情
整体语调：温柔 高冷 活泼 严肃 慵懒 俏皮 深沉 干练 凌厉
音色定位：磁性 醇厚 清亮 空灵 稚嫩 苍老 甜美 沙哑 醇雅
人设腔调：夹子音 御姐音 正太音 大叔音
方    言：东北话 四川话 河南话 粤语

===== 音频标签 =====

在 content 中任意位置插入 [标签] 进行细粒度控制：

语速与节奏：[吸气] [深呼吸] [叹气] [长叹一口气] [喘息] [屏息]
情绪状态：[紧张] [害怕] [激动] [疲惫] [委屈] [撒娇] [心虚] [震惊] [不耐烦]
语音特征：[颤抖] [声音颤抖] [变调] [破音] [鼻音] [气声] [沙哑]
哭笑表达：[笑] [轻笑] [大笑] [冷笑] [抽泣] [呜咽] [哽咽] [嚎啕大哭]

示例：
（紧张，深呼吸）呼……冷静，冷静。不就是一个面试吗……
（极其疲惫，有气无力）师傅……到地方了叫我一声……（长叹一口气）我先眯一会儿。";

        return Task.FromResult(docs);
    }
}