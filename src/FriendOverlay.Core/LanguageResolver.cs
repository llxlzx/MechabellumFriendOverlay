using System;

namespace FriendOverlay.Core
{
    /// <summary>
    /// Maps game / Unity language signals to FriendOverlay locale codes.
    /// Traditional Chinese and unknown languages resolve to <c>en</c>.
    /// </summary>
    public static class LanguageResolver
    {
        // UnityEngine.SystemLanguage underlying ints (stable across Unity versions used by Melon).
        private const int UnityChinese = 6;
        private const int UnityEnglish = 10;
        private const int UnityGerman = 15;
        private const int UnityJapanese = 22;
        private const int UnityRussian = 30;
        private const int UnityChineseSimplified = 40;
        private const int UnityChineseTraditional = 41;

        private static string _current = "en";

        public static string Current
        {
            get => _current;
            set => _current = string.IsNullOrEmpty(value) ? "en" : value;
        }

        public static string ToCode(int unitySystemLanguage)
        {
            return unitySystemLanguage switch
            {
                UnityChinese or UnityChineseSimplified => "zh-CN",
                UnityChineseTraditional => "en",
                UnityEnglish => "en",
                UnityGerman => "de",
                UnityJapanese => "ja",
                UnityRussian => "ru",
                _ => "en",
            };
        }

        public static string ToCode(string? signal)
        {
            if (string.IsNullOrWhiteSpace(signal))
                return "en";

            var lower = signal!.Trim().ToLowerInvariant().Replace('_', '-');

            if (IsTraditional(lower))
                return "en";

            if (IsSimplifiedChinese(lower))
                return "zh-CN";

            if (lower == "english" || lower == "en" || StartsWith(lower, "en-"))
                return "en";

            if (lower == "russian" || lower == "ru" || StartsWith(lower, "ru-"))
                return "ru";

            if (lower == "japanese" || lower == "ja" || StartsWith(lower, "ja-"))
                return "ja";

            if (lower == "german" || lower == "de" || StartsWith(lower, "de-"))
                return "de";

            return "en";
        }

        private static bool StartsWith(string value, string prefix) =>
            value.StartsWith(prefix, StringComparison.Ordinal);

        private static bool ContainsOrdinal(string value, string fragment) =>
            value.IndexOf(fragment, StringComparison.Ordinal) >= 0;

        private static bool IsTraditional(string lower) =>
            lower == "chinesetraditional" || lower == "hant" || lower == "tw" || lower == "hk" || lower == "mo"
            || ContainsOrdinal(lower, "hant")
            || StartsWith(lower, "zh-hant")
            || StartsWith(lower, "zh-tw")
            || StartsWith(lower, "zh-hk")
            || StartsWith(lower, "zh-mo");

        private static bool IsSimplifiedChinese(string lower) =>
            lower == "chinese" || lower == "chinesesimplified" || lower == "zh" || lower == "zh-cn"
            || lower == "hans" || lower == "cn" || lower == "sg"
            || StartsWith(lower, "zh-hans")
            || StartsWith(lower, "zh-cn")
            || StartsWith(lower, "zh-sg");
    }
}
