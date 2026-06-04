using System.ComponentModel;

namespace Harem.Services.Abstractions.Agents;

/// <summary>
/// MiMo TTS 语音合成服务 — 通过 MimoCli CLI 调用小米 MiMo TTS API
/// </summary>
public interface IMimoService
{
    /// <summary>
    /// 合成 MiMo TTS 语音并通过 Reply 渠道发送
    /// </summary>
    /// <param name="text">要合成的文本</param>
    /// <param name="designPrompt">音色设计提示词，null 则用 speak 模式</param>
    /// <param name="sessionKey">会话密钥，用于发送语音消息</param>
    [Description("合成MiMo TTS语音并发送")]
    Task<bool> SynthesizeAndSendAsync(string text, string? designPrompt, string sessionKey);
}