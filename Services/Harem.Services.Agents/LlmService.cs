using System.Text;
using System.Text.RegularExpressions;
using Harem.Contracts.Configurations.AgentWorkspace;
using Harem.Contracts.Domain.Messages;
using Harem.Services.Abstractions.Agents;
using Harem.Services.Abstractions.Channels;
using Harem.Services.Agents.Handler;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Harem.Services.Agents;

public class LlmService : ILlmService
{
    private readonly ILogger<LlmService> _logger;
    private readonly IAgentService _agentService;
    private readonly IResponseService _responseService;
    private readonly IMessageChannel<ReplyMessage> _replyChannel;
    private readonly AgentOptions _agentOptions;

    public LlmService(
        ILogger<LlmService> logger,
        IAgentService agentService,
        IResponseService responseService,
        IMessageChannel<ReplyMessage> replyChannel,
        IOptions<AgentOptions> agentOptions)
    {
        _logger = logger;
        _agentService = agentService;
        _responseService = responseService;
        _replyChannel = replyChannel;
        _agentOptions = agentOptions.Value;
    }

    public async Task<LlmCallResult> CallAsync(
        AIAgent agent,
        AgentSession agentSession,
        List<ChatMessage> messages,
        string sessionKey,
        bool enableStreaming,
        CancellationToken ct)
    {
        if (!enableStreaming)
            return await CallNonStreamingAsync(agent, agentSession, messages, sessionKey, ct);

        return await CallStreamingAsync(agent, agentSession, messages, sessionKey, ct);
    }

    /// <summary>
    /// 非流式调用
    /// </summary>
    private async Task<LlmCallResult> CallNonStreamingAsync(
        AIAgent agent,
        AgentSession agentSession,
        List<ChatMessage> messages,
        string sessionKey,
        CancellationToken ct)
    {
        var agentResponse = await RunWithFallbackAsync(agent, agentSession, messages, sessionKey, ct);
        var responseText = agentResponse.Text;
        var processedContent = await _responseService.ProcessShortcutTagsAsync(responseText, sessionKey);

        if (!string.IsNullOrWhiteSpace(processedContent))
        {
            var segments = SplitParagraphs(processedContent);
            foreach (var segment in segments)
            {
                if (string.IsNullOrWhiteSpace(segment)) continue;
                await _replyChannel.WriteAsync(new ReplyMessage(sessionKey, segment.Replace("@", "").Trim()), ct);
                await Task.Delay(1000, ct);
            }
        }

        return new LlmCallResult(
            FullText: responseText,
            Content: processedContent ?? responseText,
            Summary: string.Empty,
            InputTokens: agentResponse.Usage?.InputTokenCount ?? 0,
            OutputTokens: agentResponse.Usage?.OutputTokenCount ?? 0);
    }

    /// <summary>
    /// 流式调用
    /// </summary>
    private async Task<LlmCallResult> CallStreamingAsync(
        AIAgent agent,
        AgentSession agentSession,
        List<ChatMessage> messages,
        string sessionKey,
        CancellationToken ct)
    {
        IAsyncEnumerable<AgentResponseUpdate> stream;
        try
        {
            stream = agent.RunStreamingAsync(messages, agentSession, cancellationToken: ct);
        }
        catch (Exception ex) when (ExceptionHandler.IsRecoverableException(ex, ct))
        {
            _logger.LogWarning(ex, "模型 {Model} 启动流式调用失败，尝试回退模型...", _agentService.CurrentModelKey);
            await _replyChannel.WriteAsync(
                new ReplyMessage(sessionKey, $"⚠️ 模型 `{_agentService.CurrentModelKey}` 暂时不可用，正在尝试回退模型..."), ct);

            stream = await FallbackStreamingAsync(agent, agentSession, messages, sessionKey, ct);
        }

        return await ConsumeStreamAsync(stream, sessionKey, ct);
    }

    /// <summary>
    /// 带回退的模型调用：主模型失败时依次尝试 FallbackModels
    /// </summary>
    private async Task<AgentResponse> RunWithFallbackAsync(
        AIAgent agent, AgentSession agentSession, List<ChatMessage> messages, string sessionKey, CancellationToken ct)
    {
        try
        {
            return await RunWithRetryAsync(agent, agentSession, messages, ct);
        }
        catch (Exception ex) when (ExceptionHandler.IsRecoverableException(ex, ct))
        {
            _logger.LogWarning(ex, "模型 {Model} 调用失败（含重试），尝试回退模型...", _agentService.CurrentModelKey);
        }

        await _replyChannel.WriteAsync(
            new ReplyMessage(sessionKey, $"⚠️ 模型 `{_agentService.CurrentModelKey}` 暂时不可用，正在尝试回退模型..."), ct);

        foreach (var fallbackKey in _agentOptions.FallbackModels)
        {
            try
            {
                _logger.LogInformation("回退到模型: {Model}", fallbackKey);
                _agentService.RebuildForModel(fallbackKey);
                var fallbackAgent = _agentService.GetOrCreateAgent();
                await _replyChannel.WriteAsync(
                    new ReplyMessage(sessionKey, $"✅ 已回退到模型 `{fallbackKey}`"), ct);
                return await RunWithRetryAsync(fallbackAgent, agentSession, messages, ct);
            }
            catch (Exception ex) when (ExceptionHandler.IsRecoverableException(ex, ct))
            {
                _logger.LogWarning(ex, "回退模型 {Model} 也失败了，继续尝试下一个...", fallbackKey);
            }
        }

        throw new InvalidOperationException(
            $"所有模型均不可用（主模型: {_agentOptions.DefaultModelKey}，回退: [{string.Join(", ", _agentOptions.FallbackModels)}]）");
    }

    /// <summary>
    /// 带短暂重试的模型调用：对 529 等瞬时过载错误自动重试一次
    /// </summary>
    private async Task<AgentResponse> RunWithRetryAsync(
        AIAgent agent, AgentSession agentSession, List<ChatMessage> messages, CancellationToken ct)
    {
        try
        {
            return await agent.RunAsync(messages, agentSession, cancellationToken: ct);
        }
        catch (Exception ex) when (ExceptionHandler.IsTransientOverload(ex) && !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "模型 {Model} 返回瞬时过载错误，等待 3 秒后重试...", _agentService.CurrentModelKey);
            await Task.Delay(TimeSpan.FromSeconds(3), ct);
            return await agent.RunAsync(messages, agentSession, cancellationToken: ct);
        }
    }

    /// <summary>
    /// 流式模型回退：主模型启动失败时依次尝试 FallbackModels
    /// </summary>
    private async Task<IAsyncEnumerable<AgentResponseUpdate>> FallbackStreamingAsync(
        AIAgent agent, AgentSession agentSession, List<ChatMessage> messages, string sessionKey, CancellationToken ct)
    {
        foreach (var fallbackKey in _agentOptions.FallbackModels)
        {
            try
            {
                _logger.LogInformation("回退到模型: {Model}", fallbackKey);
                _agentService.RebuildForModel(fallbackKey);
                var fallbackAgent = _agentService.GetOrCreateAgent();
                await _replyChannel.WriteAsync(
                    new ReplyMessage(sessionKey, $"✅ 已回退到模型 `{fallbackKey}`"), ct);
                return fallbackAgent.RunStreamingAsync(messages, agentSession, cancellationToken: ct);
            }
            catch (Exception ex) when (ExceptionHandler.IsRecoverableException(ex, ct))
            {
                _logger.LogWarning(ex, "回退模型 {Model} 也失败了，继续尝试下一个...", fallbackKey);
            }
        }

        throw new InvalidOperationException(
            $"所有模型均不可用（主模型: {_agentOptions.DefaultModelKey}，回退: [{string.Join(", ", _agentOptions.FallbackModels)}]）");
    }

    /// <summary>
    /// 消费流式响应，逐段输出到 ReplyChannel，返回提取的 Content/Summary
    /// </summary>
    private async Task<LlmCallResult> ConsumeStreamAsync(
        IAsyncEnumerable<AgentResponseUpdate> stream, string sessionKey, CancellationToken ct)
    {
        var replyBuilder = new StringBuilder();
        var contentBuffer = new StringBuilder();
        var insideContent = false;
        var enteredContent = false;
        var pastThinking = false;
        var inCodeBlock = false;

        await foreach (var chunk in stream.WithCancellation(ct))
        {
            replyBuilder.Append(chunk.Text);
            // 扫描代码块标记 ```，跟踪代码块状态
            for (var j = 0; j < chunk.Text.Length; j++)
            {
                if (j + 2 < chunk.Text.Length && chunk.Text[j] == '`' && chunk.Text[j + 1] == '`' && chunk.Text[j + 2] == '`')
                {
                    inCodeBlock = !inCodeBlock;
                    j += 2;
                }
            }
            var current = replyBuilder.ToString();

            // 跳过 <InnerVoice> 区间，防止 LLM 在思考中输出 <Content> 等标签干扰解析
            if (!pastThinking)
            {
                if (current.Contains("</InnerVoice>", StringComparison.OrdinalIgnoreCase))
                {
                    pastThinking = true;
                    var thinkingEnd = current.IndexOf("</InnerVoice>", StringComparison.OrdinalIgnoreCase) + Constants.Tags.InnerVoiceClose.Length;
                    contentBuffer.Append(current[thinkingEnd..]);
                    current = contentBuffer.ToString();
                }
                else if (current.Contains("<InnerVoice>", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                else
                {
                    pastThinking = true;
                    contentBuffer.Append(current);
                    current = contentBuffer.ToString();
                }
            }

            // 检测 <Content> 开始
            if (!insideContent && current.Contains("<Content>", StringComparison.OrdinalIgnoreCase))
            {
                insideContent = true;
                enteredContent = true;
                var idx = current.LastIndexOf("<Content>", StringComparison.OrdinalIgnoreCase);
                contentBuffer.Clear();
                contentBuffer.Append(current[(idx + 9)..]);
                current = contentBuffer.ToString();
            }
            // 从未进入 Content 模式，遇到 <Summary> 时把之前的纯文本当作 Content 发送
            else if (!enteredContent && current.Contains("<Summary>", StringComparison.OrdinalIgnoreCase))
            {
                var summaryIdx = current.IndexOf("<Summary>", StringComparison.OrdinalIgnoreCase);
                var plainText = current[..summaryIdx].Trim();
                if (!string.IsNullOrWhiteSpace(plainText))
                {
                    var segments = SplitParagraphs(plainText);
                    foreach (var seg in segments)
                    {
                        if (string.IsNullOrWhiteSpace(seg)) continue;
                        var processed = await _responseService.ProcessShortcutTagsAsync(seg.Trim(), sessionKey);
                        if (!string.IsNullOrWhiteSpace(processed))
                            await _replyChannel.WriteAsync(new ReplyMessage(sessionKey, processed.Replace("@", "")), ct);
                    }
                }
                // 标记已处理，后续 chunk 只积累 replyBuilder 不发送
                enteredContent = true;
                continue;
            }
            else if (insideContent)
            {
                contentBuffer.Append(chunk.Text);
                current = contentBuffer.ToString();
            }

            // 在 <Content> 内部遇到 <br/>、连续换行、或未闭合时遇到 <Summary> 就发送一段
            if (!insideContent) continue;
            while (true)
            {
                var brPos = current.IndexOf("<br/>", StringComparison.Ordinal);
                var nlMatch = Regex.Match(current, @"(\r?\n){2,}");
                var nlPos = nlMatch.Success ? nlMatch.Index : -1;
                var nlLen = nlMatch.Success ? nlMatch.Length : 0;
                var summaryPos = current.IndexOf("<Summary>", StringComparison.OrdinalIgnoreCase);

                // 代码块内的 <br/> 和双换行不触发分段
                if (inCodeBlock)
                {
                    brPos = -1;
                    nlPos = -1;
                }

                // 没有闭合 </Content> 但遇到 <Summary>，当作 Content 结束
                if (summaryPos >= 0)
                {
                    var seg = current[..summaryPos].Trim();
                    if (!string.IsNullOrWhiteSpace(seg))
                    {
                        var processed = await _responseService.ProcessShortcutTagsAsync(seg, sessionKey);
                        if (!string.IsNullOrWhiteSpace(processed))
                            await _replyChannel.WriteAsync(new ReplyMessage(sessionKey, processed.Replace("@", "")), ct);
                    }
                    insideContent = false;
                    break;
                }

                if (brPos < 0 && nlPos < 0) break;

                int splitPos, splitLen;
                if (brPos < 0)
                {
                    splitPos = nlPos;
                    splitLen = nlLen;
                }
                else if (nlPos < 0)
                {
                    splitPos = brPos;
                    splitLen = 5;
                }
                else if (brPos <= nlPos)
                {
                    splitPos = brPos;
                    splitLen = 5;
                }
                else
                {
                    splitPos = nlPos;
                    splitLen = nlLen;
                }

                var segment = current[..splitPos].Trim();
                if (!string.IsNullOrWhiteSpace(segment))
                {
                    var processed = await _responseService.ProcessShortcutTagsAsync(segment, sessionKey);
                    if (!string.IsNullOrWhiteSpace(processed))
                        await _replyChannel.WriteAsync(new ReplyMessage(sessionKey, processed.Replace("@", "")), ct);
                }

                current = current[(splitPos + splitLen)..];
                contentBuffer.Clear();
                contentBuffer.Append(current);
            }
        }

        // 发送 Content 最后一段
        if (insideContent)
        {
            var lastPart = contentBuffer.ToString();
            var endIdx = lastPart.IndexOf("</Content>", StringComparison.OrdinalIgnoreCase);
            if (endIdx < 0) endIdx = lastPart.IndexOf("<Summary>", StringComparison.OrdinalIgnoreCase); // 无闭合标签时 fallback
            if (endIdx >= 0) lastPart = lastPart[..endIdx];
            lastPart = lastPart.Trim();
            if (!string.IsNullOrWhiteSpace(lastPart))
            {
                var processed = await _responseService.ProcessShortcutTagsAsync(lastPart, sessionKey);
                if (!string.IsNullOrWhiteSpace(processed))
                    await _replyChannel.WriteAsync(new ReplyMessage(sessionKey, processed.Replace("@", "")), ct);
            }
        }

        // 后处理：从完整文本提取 Content
        var fullText = replyBuilder.ToString();
        return new LlmCallResult(
            FullText: fullText,
            Content: fullText,
            Summary: string.Empty,
            InputTokens: 0,
            OutputTokens: 0);
    }

    /// <summary>
    /// 按双换行或 &lt;br/&gt; 分段，但忽略代码块内部的换行。
    /// 先按 ``` 分区，代码块内容整段保留，外部内容用 Regex.Split 高效分段。
    /// </summary>
    private static List<string> SplitParagraphs(string text)
    {
        var segments = new List<string>();

        // 找到所有 ``` 边界
        var codeBlockRanges = new List<(int start, int end)>();
        var idx = 0;
        while (true)
        {
            var start = text.IndexOf("```", idx, StringComparison.Ordinal);
            if (start < 0) break;
            var end = text.IndexOf("```", start + 3, StringComparison.Ordinal);
            if (end < 0)
            {
                codeBlockRanges.Add((start, text.Length));
                break;
            }
            codeBlockRanges.Add((start, end + 3));
            idx = end + 3;
        }

        // 没有代码块，直接一次 Regex.Split
        if (codeBlockRanges.Count == 0)
        {
            foreach (var seg in Regex.Split(text, @"<br/>|(\r?\n){2,}"))
                if (!string.IsNullOrWhiteSpace(seg))
                    segments.Add(seg);
            return segments;
        }

        // 分区处理：代码块外 Regex.Split，代码块内整段保留
        idx = 0;
        foreach (var (start, end) in codeBlockRanges)
        {
            // 代码块前的内容
            if (idx < start)
            {
                foreach (var seg in Regex.Split(text[idx..start], @"<br/>|(\r?\n){2,}"))
                    if (!string.IsNullOrWhiteSpace(seg))
                        segments.Add(seg);
            }
            // 代码块内容整段保留
            segments.Add(text[start..end]);
            idx = end;
        }
        // 最后一段非代码块内容
        if (idx < text.Length)
        {
            foreach (var seg in Regex.Split(text[idx..], @"<br/>|(\r?\n){2,}"))
                if (!string.IsNullOrWhiteSpace(seg))
                    segments.Add(seg);
        }

        return segments;
    }
}
