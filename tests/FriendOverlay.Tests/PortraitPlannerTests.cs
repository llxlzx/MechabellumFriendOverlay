using FriendOverlay.Core;
using Xunit;

namespace FriendOverlay.Tests;

/// <summary>
/// The product rule is "show whatever the player picked in game". The game only reports a non-empty
/// avatar URL when that player selected an official avatar, so a non-empty avatar URL *is* the
/// player's choice — these tests must never be read as "official beats a custom photo".
/// </summary>
public class PortraitPlannerTests
{
    [Fact]
    public void Decide_PlayerPickedOfficialAvatar_ShowsThatAvatar()
    {
        var plan = PortraitPlanner.Decide(false, "https://cdn/photo.jpg", "ui/avatar_07", "ui/frame_02");

        Assert.Equal(PortraitKind.Official, plan.Kind);
        Assert.Equal("ui/avatar_07", plan.ImageRef);
        Assert.Equal("ui/frame_02", plan.FrameRef);
    }

    [Fact]
    public void Decide_PlayerPickedOwnPhoto_ShowsThePhoto()
    {
        var plan = PortraitPlanner.Decide(false, "https://cdn/photo.jpg", "", null);

        Assert.Equal(PortraitKind.Photo, plan.Kind);
        Assert.Equal("https://cdn/photo.jpg", plan.ImageRef);
        Assert.Equal(string.Empty, plan.FrameRef);
    }

    /// <summary>A frame with no avatar still belongs to the player and must survive the letter case.</summary>
    [Fact]
    public void Decide_LetterWhenNothingToShow_KeepsTheFrame()
    {
        var plan = PortraitPlanner.Decide(false, "", null, "ui/frame_02");

        Assert.Equal(PortraitKind.Letter, plan.Kind);
        Assert.Equal(string.Empty, plan.ImageRef);
        Assert.Equal("ui/frame_02", plan.FrameRef);
    }

    [Fact]
    public void Decide_BlockedPhotoUsesGameSubstitutedPortrait()
    {
        // The game already swapped the URL for its dark placeholder; we show exactly that.
        var plan = PortraitPlanner.Decide(true, "ui/dark_avatar", null, null);

        Assert.Equal(PortraitKind.Blocked, plan.Kind);
        Assert.Equal("ui/dark_avatar", plan.ImageRef);
    }

    [Fact]
    public void Decide_BlockedWithoutPortraitFallsBackToLetter()
    {
        var plan = PortraitPlanner.Decide(true, "", null, null);
        Assert.Equal(PortraitKind.Letter, plan.Kind);
    }

    /// <summary>Moderation covers the photo layer only, so an official avatar is still shown.</summary>
    [Fact]
    public void Decide_BlockedDoesNotHideOfficialAvatar()
    {
        var plan = PortraitPlanner.Decide(true, "ui/dark_avatar", "ui/avatar_07", null);

        Assert.Equal(PortraitKind.Official, plan.Kind);
        Assert.Equal("ui/avatar_07", plan.ImageRef);
    }

    [Fact]
    public void Decide_NullInputsAreSafe()
    {
        var plan = PortraitPlanner.Decide(false, null, null, null);

        Assert.Equal(PortraitKind.Letter, plan.Kind);
        Assert.Equal(string.Empty, plan.ImageRef);
        Assert.Equal(string.Empty, plan.FrameRef);
    }

    /// <summary>Whitespace-only refs come back from the game as "unset", not as a loadable image.</summary>
    [Fact]
    public void Decide_WhitespaceRefsCountAsUnset()
    {
        var plan = PortraitPlanner.Decide(false, "   ", "  ", " ");

        Assert.Equal(PortraitKind.Letter, plan.Kind);
        Assert.Equal(string.Empty, plan.ImageRef);
        Assert.Equal(string.Empty, plan.FrameRef);
    }
}
