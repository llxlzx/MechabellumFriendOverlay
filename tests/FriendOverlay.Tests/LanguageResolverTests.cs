using FriendOverlay.Core;

namespace FriendOverlay.Tests;

public class LanguageResolverTests
{
    [Theory]
    [InlineData("Chinese", "zh-CN")]
    [InlineData("ChineseSimplified", "zh-CN")]
    [InlineData("zh", "zh-CN")]
    [InlineData("zh-CN", "zh-CN")]
    [InlineData("zh_cn", "zh-CN")]
    [InlineData("hans", "zh-CN")]
    [InlineData("cn", "zh-CN")]
    [InlineData("sg", "zh-CN")]
    [InlineData("ChineseTraditional", "en")]
    [InlineData("hant", "en")]
    [InlineData("tw", "en")]
    [InlineData("hk", "en")]
    [InlineData("mo", "en")]
    [InlineData("English", "en")]
    [InlineData("en", "en")]
    [InlineData("en-US", "en")]
    [InlineData("Russian", "ru")]
    [InlineData("ru", "ru")]
    [InlineData("Japanese", "ja")]
    [InlineData("ja", "ja")]
    [InlineData("German", "de")]
    [InlineData("de", "de")]
    [InlineData("Korean", "en")]
    [InlineData("fr", "en")]
    [InlineData("", "en")]
    [InlineData(null, "en")]
    public void ToCode_maps_signals(string? signal, string expected)
    {
        Assert.Equal(expected, LanguageResolver.ToCode(signal));
    }

    [Theory]
    [InlineData(40, "zh-CN")] // ChineseSimplified
    [InlineData(41, "en")] // ChineseTraditional
    [InlineData(6, "zh-CN")] // Chinese
    [InlineData(10, "en")]
    [InlineData(30, "ru")]
    [InlineData(22, "ja")]
    [InlineData(15, "de")]
    [InlineData(23, "en")] // Korean
    [InlineData(42, "en")] // Unknown
    public void ToCode_maps_unity_system_language_int(int value, string expected)
    {
        Assert.Equal(expected, LanguageResolver.ToCode(value));
    }

    [Fact]
    public void Current_defaults_to_en()
    {
        var previous = LanguageResolver.Current;
        try
        {
            LanguageResolver.Current = "en";
            Assert.Equal("en", LanguageResolver.Current);
        }
        finally
        {
            LanguageResolver.Current = previous;
        }
    }
}
