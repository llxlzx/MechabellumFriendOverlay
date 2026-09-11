using FriendOverlay.Core;
using Xunit;

namespace FriendOverlay.Tests;

public class OfficialImageKeysTests
{
    [Fact]
    public void ExactVariants_StripsTrailingLetterOnce()
    {
        Assert.Equal(
            new[] { "Avtr_G_02-08A", "Avtr_G_02-08" },
            OfficialImageKeys.ExactVariants("Avtr_G_02-08A"));
    }

    [Fact]
    public void ExactEquals_DoesNotPrefixMatch()
    {
        Assert.False(OfficialImageKeys.ExactEquals("Avtr_G_1", "Avtr_G_10"));
        Assert.True(OfficialImageKeys.ExactEquals("Avtr_G_02-08", "avtr_g_02-08"));
    }

    [Fact]
    public void IsLikelyGifKey_DetectsGSegment()
    {
        Assert.True(OfficialImageKeys.IsLikelyGifKey("Avtr_G_02-08A"));
        Assert.False(OfficialImageKeys.IsLikelyGifKey("Avtr_05-16A"));
    }
}
