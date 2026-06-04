namespace Harem.Services.Agents;

/// <summary>
/// 集中管理的常量，避免魔法字符串散落各处
/// </summary>
public static class Constants
{
    public static class BagKeys
    {
        public const string Rounds = "rounds";
        public const string CurrentResponse = "currentResponse";
        public const string FormatWarning = "formatWarning";
        public const string RecentNpcs = "recentNpcs";
        public const string TriggeredKeywords = "triggeredKeywords";
        public const string KeywordRetentionRound = "keywordRetentionRound";
    }

    public static class KeywordConfig
    {
        /// <summary>
        /// 关键词触发后保留的最大轮数，超期自动过期
        /// </summary>
        public const int MaxRetentionRounds = 5;
        /// <summary>
        /// 无二层匹配时默认注入的最近条目数
        /// </summary>
        public const int DefaultFallbackItems = 1;
    }

    public static class Tags
    {
        public const string ContentOpen = "<Content>";
        public const string ContentClose = "</Content>";
        public const string SummaryOpen = "<Summary>";
        public const string SummaryClose = "</Summary>";
        public const string InnerVoiceOpen = "<InnerVoice>";
        public const string InnerVoiceClose = "</InnerVoice>";
        public const string ParagraphBreak = "<br/>";

        // Shortcut tags
        public const string Tts = "tts";
        public const string Scenery = "scenery";
    }
}
