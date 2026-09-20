using FriendOverlay.Core;

namespace FriendOverlay.Tests;

public class UiFontResolvePolicyTests
{
    [Fact]
    public void ZhCN_prefers_Noto_then_System_then_game()
    {
        Assert.Equal(UiFontFaceKind.Noto, UiFontResolvePolicy.Choose("zh-CN", false, true, true, true));
        Assert.Equal(UiFontFaceKind.System, UiFontResolvePolicy.Choose("zh-CN", false, false, true, true));
        Assert.Equal(UiFontFaceKind.Game, UiFontResolvePolicy.Choose("zh-CN", false, false, false, true));
        Assert.Equal(UiFontFaceKind.Skin, UiFontResolvePolicy.Choose("zh-CN", false, false, false, false));
    }

    [Theory]
    [InlineData("ja")]
    [InlineData("en")]
    [InlineData("de")]
    [InlineData("ru")]
    public void NonZh_prefers_System_then_Noto_then_game(string code)
    {
        Assert.Equal(UiFontFaceKind.System, UiFontResolvePolicy.Choose(code, false, true, true, true));
        Assert.Equal(UiFontFaceKind.Noto, UiFontResolvePolicy.Choose(code, false, true, false, true));
        Assert.Equal(UiFontFaceKind.Game, UiFontResolvePolicy.Choose(code, false, false, false, true));
    }

    [Fact]
    public void UseGameFont_prefers_game_before_Noto()
    {
        Assert.Equal(UiFontFaceKind.Game, UiFontResolvePolicy.Choose("zh-CN", true, true, true, true));
        Assert.Equal(UiFontFaceKind.Noto, UiFontResolvePolicy.Choose("zh-CN", true, true, true, false));
        Assert.Equal(UiFontFaceKind.System, UiFontResolvePolicy.Choose("en", true, true, true, false));
    }

    [Fact]
    public void Legacy_overload_behaves_like_zhCN()
    {
        Assert.Equal(UiFontFaceKind.Noto, UiFontResolvePolicy.Choose(false, true, true, true));
        Assert.Equal(UiFontFaceKind.System, UiFontResolvePolicy.Choose(false, false, true, true));
    }
}
