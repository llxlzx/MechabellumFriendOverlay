using FriendOverlay.Core;

namespace FriendOverlay.Tests;

public class FontSelectorTests
{
    [Theory]
    [InlineData("zh-CN", "钢铁ABC")]
    [InlineData("ja", "あア漢字")]
    [InlineData("en", "Ag")]
    [InlineData("de", "Ag")]
    [InlineData("ru", "Ру")]
    public void ProbeSample_matches_language(string code, string sample)
    {
        Assert.Equal(sample, FontSelector.ProbeSample(code));
    }

    [Fact]
    public void SystemFontNames_ja_prefers_yu_gothic_then_meiryo()
    {
        var names = FontSelector.SystemFontNames("ja");
        Assert.Contains("Yu Gothic UI", names);
        Assert.Contains("Yu Gothic", names);
        Assert.Contains("Meiryo", names);
        Assert.Equal("Yu Gothic UI", names[0]);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("de")]
    [InlineData("ru")]
    public void SystemFontNames_latin_prefers_segoe_then_arial(string code)
    {
        var names = FontSelector.SystemFontNames(code);
        Assert.Equal(new[] { "Segoe UI", "Arial" }, names);
    }

    [Fact]
    public void SystemFontNames_zhCN_uses_yahei_names()
    {
        var names = FontSelector.SystemFontNames("zh-CN");
        Assert.Contains("Microsoft YaHei UI", names);
        Assert.Contains("Microsoft YaHei", names);
    }

    [Fact]
    public void PreferEmbeddedNotoFirst_only_zhCN()
    {
        Assert.True(FontSelector.PreferEmbeddedNotoFirst("zh-CN"));
        Assert.False(FontSelector.PreferEmbeddedNotoFirst("ja"));
        Assert.False(FontSelector.PreferEmbeddedNotoFirst("en"));
        Assert.False(FontSelector.PreferEmbeddedNotoFirst("de"));
        Assert.False(FontSelector.PreferEmbeddedNotoFirst("ru"));
    }
}
