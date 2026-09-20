namespace FriendOverlay.Core
{
    /// <summary>
    /// Pure font face preference helpers driven by locale code.
    /// </summary>
    public static class FontSelector
    {
        private static readonly string[] YaheiNames =
        {
            "Microsoft YaHei UI",
            "Microsoft YaHei",
            "微软雅黑",
        };

        private static readonly string[] JapaneseNames =
        {
            "Yu Gothic UI",
            "Yu Gothic",
            "Meiryo",
        };

        private static readonly string[] LatinNames =
        {
            "Segoe UI",
            "Arial",
        };

        public static string ProbeSample(string languageCode) => languageCode switch
        {
            "zh-CN" => "钢铁ABC",
            "ja" => "あア漢字",
            "ru" => "Ру",
            _ => "Ag",
        };

        public static string[] SystemFontNames(string languageCode) => languageCode switch
        {
            "zh-CN" => YaheiNames,
            "ja" => JapaneseNames,
            _ => LatinNames,
        };

        public static bool PreferEmbeddedNotoFirst(string languageCode) =>
            string.Equals(languageCode, "zh-CN", System.StringComparison.Ordinal);
    }
}
