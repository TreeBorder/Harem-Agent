using System.Text.RegularExpressions;
using Spectre.Console;

namespace Harem.Cli;

/// <summary>
/// Markdown 转 Spectre.Console Markup 渲染
/// </summary>
public static partial class MarkdownRenderer
{
    /// <summary>
    /// 清理 Harem 内部标签
    /// </summary>
    public static string StripInternalTags(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text ?? "";

        // <InnerVoice>...</InnerVoice> → 保留内容但灰色斜体（以后处理）
        // <Content>...</Content> → 提取内容
        text = ContentTagRegex().Replace(text, "$1");

        // <Time>...</Time> → 去掉
        text = TimeTagRegex().Replace(text, "");

        // <UserMessage>...</UserMessage> → 提取内容
        text = UserMessageTagRegex().Replace(text, "$1");

        // <Thinking>...</Thinking> → 去掉
        text = ThinkingTagRegex().Replace(text, "");

        // 其他 <...> 标签 → 去掉
        text = OtherTagsRegex().Replace(text, "");

        return text.Trim();
    }

    /// <summary>
    /// 将 Markdown 文本转换为 Spectre.Console Markup 格式
    /// </summary>
    public static string ToSpectreMarkup(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return markdown ?? "";

        // 先清理内部标签
        var cleaned = StripInternalTags(markdown);

        // 先转义 Spectre 特殊字符（方括号）
        var result = cleaned.EscapeMarkup();

        // 代码块 ```...``` → Panel 区域
        result = CodeBlockRegex().Replace(result, m =>
        {
            var lang = m.Groups[1].Value;
            var code = m.Groups[2].Value;
            var header = string.IsNullOrEmpty(lang) ? "" : $"[grey]{lang.EscapeMarkup()}[/]\n";
            var panel = new Panel($"[yellow on black]{code.EscapeMarkup()}[/]")
            {
                Border = BoxBorder.Rounded,
                Padding = new Padding(1, 0, 1, 0)
            };
            // Panel 通过 AnsiConsole.Render 输出，这里返回时用特殊标记包裹
            // 简化处理：用方框字符模拟
            return $"\n[grey]```{lang}[/]\n[yellow on black]{code.EscapeMarkup()}[/]\n[grey]```[/]\n";
        });

        // **粗体**
        result = BoldRegex().Replace(result, "[bold]$1[/]");

        // *斜体*（注意不要匹配到 **）
        result = ItalicRegex().Replace(result, "[italic]$1[/i]");

        // `行内代码`
        result = InlineCodeRegex().Replace(result, "[yellow on black]$1[/]");

        // 行首 > 引用
        result = BlockquoteRegex().Replace(result, "[grey]> $1[/]");

        // 行首 - 或 * 列表
        result = UnorderedListRegex().Replace(result, "  • $1");

        // 行首数字列表
        result = OrderedListRegex().Replace(result, "  $1. $2");

        // 水平分割线 ---
        result = ThematicBreakRegex().Replace(result, "\n[grey]─────────────────────[/]\n");

        return result;
    }

    /// <summary>
    /// 渲染文本（支持逐字打印效果）
    /// </summary>
    public static void Render(string text, bool streaming = false)
    {
        var markup = ToSpectreMarkup(text);
        if (streaming)
            AnsiConsole.Markup(markup);
        else
            AnsiConsole.MarkupLine(markup);
    }

    [GeneratedRegex(@"```(\w*)\n([\s\S]*?)```", RegexOptions.Multiline)]
    private static partial Regex CodeBlockRegex();

    [GeneratedRegex(@"\*\*(.+?)\*\*")]
    private static partial Regex BoldRegex();

    [GeneratedRegex(@"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)")]
    private static partial Regex ItalicRegex();

    [GeneratedRegex(@"`([^`]+)`")]
    private static partial Regex InlineCodeRegex();

    [GeneratedRegex(@"^>\s?(.*)", RegexOptions.Multiline)]
    private static partial Regex BlockquoteRegex();

    [GeneratedRegex(@"^[-*]\s+(.*)", RegexOptions.Multiline)]
    private static partial Regex UnorderedListRegex();

    [GeneratedRegex(@"^\d+\.\s+(.*)", RegexOptions.Multiline)]
    private static partial Regex OrderedListRegex();

    [GeneratedRegex(@"^---+$", RegexOptions.Multiline)]
    private static partial Regex ThematicBreakRegex();

    // ===== 标签清理 =====
    [GeneratedRegex(@"<Content>(.*?)</Content>", RegexOptions.Singleline)]
    private static partial Regex ContentTagRegex();
    [GeneratedRegex(@"<Time>.*?</Time>", RegexOptions.Singleline)]
    private static partial Regex TimeTagRegex();
    [GeneratedRegex(@"<UserMessage>(.*?)</UserMessage>", RegexOptions.Singleline)]
    private static partial Regex UserMessageTagRegex();
    [GeneratedRegex(@"<Thinking>.*?</Thinking>", RegexOptions.Singleline)]
    private static partial Regex ThinkingTagRegex();
    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex OtherTagsRegex();
}
