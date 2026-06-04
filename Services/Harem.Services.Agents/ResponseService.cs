using System.Text.RegularExpressions;
using Harem.Services.Abstractions.Agents;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Harem.Services.Agents;

public class ResponseService : IResponseService
{
    private readonly ILogger<ResponseService> _logger;
    private readonly IHistoryService _historyService;
    private readonly IContextService _contextService;
    private readonly IMimoService _mimoService;

    public ResponseService(
        ILogger<ResponseService> logger,
        IHistoryService historyService,
        IContextService contextService,
        IMimoService mimoService)
    {
        _logger = logger;
        _historyService = historyService;
        _contextService = contextService;
        _mimoService = mimoService;
    }

    /// <summary>
    /// 解析 Content 中的快捷标签并触发对应工具调用，返回移除标签后的纯文本
    /// </summary>
    public async Task<string> ProcessShortcutTagsAsync(string content, string sessionKey)
    {
        if (string.IsNullOrWhiteSpace(content)) return content;

        // 修复 LLM 输出的断裂标签
        content = RepairShortcutTagFormat(content);

        // 过滤不成对的开始标签
        content = FilterUnclosedMimoTags(content);

        // 1. 解析 <audio design=""> — MiMo TTS（带音色设计）
        var audioDesignPattern = @"<audio\s+design=""([^""]*)""\s*>(.*?)</audio>";
        var audioDesignMatches = Regex.Matches(content, audioDesignPattern, RegexOptions.Singleline);
        foreach (Match match in audioDesignMatches)
        {
            var text = match.Groups[2].Value.Trim();
            if (string.IsNullOrWhiteSpace(text)) continue;
            try
            {
                await _mimoService.SynthesizeAndSendAsync(text.Replace("@", ""), match.Groups[1].Value, sessionKey);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "<audio design> 标签解析异常");
            }
        }
        content = Regex.Replace(content, audioDesignPattern, "", RegexOptions.Singleline);

        // 2. 解析 [mimo] 快捷标签
        var shortcutPattern = @"[\[\(]mimo[\]\)](.*?)\[/mimo\]";
        var shortcutMatches = Regex.Matches(content, shortcutPattern, RegexOptions.Singleline);

        if (shortcutMatches.Count == 0)
            return StripOrphanedClosingTags(content).Replace("@", "").Trim();

        foreach (Match match in shortcutMatches)
        {
            var text = match.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(text)) continue;

            try
            {
                await _mimoService.SynthesizeAndSendAsync(text.Replace("@", ""), null, sessionKey);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "快捷标签解析异常: {Tag}", "mimo");
            }
        }

        var cleaned = Regex.Replace(content, shortcutPattern, "", RegexOptions.Singleline).Replace("@", "").Trim();
        cleaned = StripOrphanedClosingTags(cleaned);
        return cleaned;
    }

    /// <summary>
    /// 保存本轮交互到历史记录
    /// </summary>
    public async Task SaveRoundAsync(
        string sessionId,
        ChatMessage inputMessage,
        string finalContent)
    {
        await _contextService.SaveRecentRound(sessionId, inputMessage);

        var history = await _historyService.GetHistoryAsync(sessionId);
        await _historyService.SaveHistoryAsync(sessionId, history, inputMessage, finalContent);
    }

    /// <summary>
    /// 修复 LLM 输出的断裂标签
    /// </summary>
    private static string RepairShortcutTagFormat(string content)
    {
        var openPattern = @"[\[\(]mimo[\]\)]";
        var closePattern = @"\[/mimo\]";

        var opens = Regex.Matches(content, openPattern, RegexOptions.Singleline);
        var closes = Regex.Matches(content, closePattern, RegexOptions.Singleline);

        if (closes.Count == 0) return content;

        if (opens.Count == 0)
        {
            var result = content;
            for (int i = closes.Count - 1; i >= 0; i--)
                result = result.Remove(closes[i].Index, closes[i].Length);
            return result;
        }

        var toRemove = new List<(int pos, int len)>();

        for (int oi = 0; oi < opens.Count; oi++)
        {
            var openPos = opens[oi].Index;
            var nextOpenPos = oi + 1 < opens.Count ? opens[oi + 1].Index : int.MaxValue;

            var closeIndices = new List<int>();
            for (int ci = 0; ci < closes.Count; ci++)
            {
                if (closes[ci].Index > openPos && closes[ci].Index < nextOpenPos)
                    closeIndices.Add(ci);
            }

            if (closeIndices.Count > 1)
            {
                for (int i = 0; i < closeIndices.Count - 1; i++)
                {
                    var c = closes[closeIndices[i]];
                    toRemove.Add((c.Index, c.Length));
                }
            }
        }

        for (int ci = 0; ci < closes.Count; ci++)
        {
            if (closes[ci].Index < opens[0].Index)
                toRemove.Add((closes[ci].Index, closes[ci].Length));
        }

        if (toRemove.Count == 0) return content;

        toRemove.Sort((a, b) => b.pos.CompareTo(a.pos));
        var repaired = content;
        foreach (var (pos, len) in toRemove)
            repaired = repaired.Remove(pos, len);

        return repaired;
    }

    /// <summary>
    /// 清理残留的闭合标签
    /// </summary>
    private static string StripOrphanedClosingTags(string content)
    {
        content = Regex.Replace(content, @"\[/mimo\]", "", RegexOptions.Singleline);
        content = Regex.Replace(content, @"</audio>", "", RegexOptions.Singleline);
        return content.Trim();
    }

    /// <summary>
    /// 过滤不成对的 [mimo] 开始标签
    /// </summary>
    private static string FilterUnclosedMimoTags(string content)
    {
        var openPattern = @"(?<!\[)\[mimo\](?!\])";
        var closePattern = @"\[/(?:mimo|)\]";

        var openMatches = Regex.Matches(content, openPattern, RegexOptions.Singleline);
        if (openMatches.Count == 0) return content;

        var closeMatches = Regex.Matches(content, closePattern, RegexOptions.Singleline);
        if (openMatches.Count <= closeMatches.Count) return content;

        var result = content;
        var closeIndex = 0;
        var unclosedOpenTags = new List<Match>();

        for (int i = 0; i < openMatches.Count; i++)
        {
            var openTag = openMatches[i];
            bool hasCorrespondingClose = false;
            while (closeIndex < closeMatches.Count)
            {
                if (closeMatches[closeIndex].Index > openTag.Index)
                {
                    hasCorrespondingClose = true;
                    closeIndex++;
                    break;
                }
                closeIndex++;
            }

            if (!hasCorrespondingClose)
                unclosedOpenTags.Add(openTag);
        }

        for (int i = unclosedOpenTags.Count - 1; i >= 0; i--)
            result = result.Remove(unclosedOpenTags[i].Index, unclosedOpenTags[i].Length);

        return result;
    }
}
