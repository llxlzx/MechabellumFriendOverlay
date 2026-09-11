using FriendOverlay.Core;

namespace FriendOverlay.Tests;

public class OverlayPerfSettingsTests
{
    [Fact]
    public void AnimatedOfficialAvatars_defaults_false()
    {
        OverlayPerfSettings.ResetToDefaults();
        Assert.False(OverlayPerfSettings.AnimatedOfficialAvatars);
    }

    [Fact]
    public void TargetGifFrameCount_is_one_when_animation_off()
    {
        OverlayPerfSettings.ResetToDefaults();
        Assert.Equal(1, OverlayPerfSettings.TargetGifFrameCount(12));
        OverlayPerfSettings.AnimatedOfficialAvatars = true;
        Assert.Equal(12, OverlayPerfSettings.TargetGifFrameCount(12));
        Assert.Equal(GifPlayback.MaxFrames, OverlayPerfSettings.TargetGifFrameCount(100));
    }
}
