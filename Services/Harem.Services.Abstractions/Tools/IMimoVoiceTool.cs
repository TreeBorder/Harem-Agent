using System.ComponentModel;

namespace Harem.Services.Abstractions.Tools;

/// <summary>
/// MiMo 语音合成工具接口
/// 通过 MimoCli CLI 调用小米 MiMo TTS API
/// </summary>
[Description("小米MiMo语音合成工具")]
public interface IMimoVoiceTool
{
    /// <summary>
    /// 使用小米 MiMo TTS 将文本转为语音并发送
    /// </summary>
    /// <param name="content">要合成的文本</param>
    /// <param name="designPrompt">音色设计提示词，不传时自动从状态系统的 MimoVoice 读取</param>
    [Description("使用小米MiMo TTS(voicedesign)将文本转为语音并发送。designPrompt可选音色设计提示词，不传时自动从状态MimoVoice读取。可用mimo_voice_help获取详细用法参考（含音频标签、风格控制、导演模式等）")]
    Task<string> Voice(string content, string? designPrompt = null);

    /// <summary>
    /// 获取 MiMo 语音合成 (mimo-v2.5-tts-voicedesign) 的用法参考
    /// </summary>
    [Description("获取 MiMo 语音合成 (voicedesign) 的详细用法参考，包括音频标签、风格控制、导演模式等。调用 mimo_voice 前可先调用此工具了解支持的风格参数")]
    Task<string> GetHelp();
}
