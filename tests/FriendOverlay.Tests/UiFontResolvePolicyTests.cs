using FriendOverlay.Core;

namespace FriendOverlay.Tests;

public class UiFontResolvePolicyTests
{
    [Fact]
    public void Default_prefers_Noto_then_YaHei_then_game()
    {
        Assert.Equal(UiFontFaceKind.Noto, UiFontResolvePolicy.Choose(false, true, true, true));
        Assert.Equal(UiFontFaceKind.YaHei, UiFontResolvePolicy.Choose(false, false, true, true));
        Assert.Equal(UiFontFaceKind.Game, UiFontResolvePolicy.Choose(false, false, false, true));
        Assert.Equal(UiFontFaceKind.Skin, UiFontResolvePolicy.Choose(false, false, false, false));
    }

    [Fact]
    public void UseGameFont_prefers_game_before_Noto()
    {
        Assert.Equal(UiFontFaceKind.Game, UiFontResolvePolicy.Choose(true, true, true, true));
        Assert.Equal(UiFontFaceKind.Noto, UiFontResolvePolicy.Choose(true, true, true, false));
        Assert.Equal(UiFontFaceKind.YaHei, UiFontResolvePolicy.Choose(true, false, true, false));
    }
}
