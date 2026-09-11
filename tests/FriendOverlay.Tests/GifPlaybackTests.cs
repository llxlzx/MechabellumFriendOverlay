using FriendOverlay.Core;
using Xunit;

namespace FriendOverlay.Tests;

public class GifPlaybackTests
{
    [Fact]
    public void FrameIndex_AdvancesByFrameSeconds()
    {
        Assert.Equal(0, GifPlayback.FrameIndex(0.00, 0.1f, 4));
        Assert.Equal(0, GifPlayback.FrameIndex(0.099, 0.1f, 4));
        Assert.Equal(1, GifPlayback.FrameIndex(0.10, 0.1f, 4));
        Assert.Equal(3, GifPlayback.FrameIndex(0.35, 0.1f, 4));
    }

    [Fact]
    public void FrameIndex_Wraps()
    {
        Assert.Equal(0, GifPlayback.FrameIndex(0.40, 0.1f, 4));
        Assert.Equal(1, GifPlayback.FrameIndex(0.50, 0.1f, 4));
    }

    [Fact]
    public void FrameIndex_InvalidCount_ReturnsZero()
    {
        Assert.Equal(0, GifPlayback.FrameIndex(1.0, 0.1f, 0));
        Assert.Equal(0, GifPlayback.FrameIndex(1.0, 0.1f, -1));
    }

    [Fact]
    public void FrameIndex_NonPositiveSeconds_UsesDefault()
    {
        var dt = GifPlayback.DefaultFrameSeconds;
        Assert.Equal(0, GifPlayback.FrameIndex(dt * 0.5, 0f, 3));
        Assert.Equal(1, GifPlayback.FrameIndex(dt * 1.5, 0f, 3));
    }

    [Fact]
    public void CapFrameCount_ClampsToMax()
    {
        Assert.Equal(GifPlayback.MaxFrames, GifPlayback.CapFrameCount(100));
        Assert.Equal(5, GifPlayback.CapFrameCount(5));
        Assert.Equal(0, GifPlayback.CapFrameCount(0));
    }
}
