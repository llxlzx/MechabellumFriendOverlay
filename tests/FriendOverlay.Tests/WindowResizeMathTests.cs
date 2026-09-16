using FriendOverlay.Core;

namespace FriendOverlay.Tests;

public class WindowResizeMathTests
{
    const float W = 900f;
    const float H = 540f;
    const float T = 8f;

    [Fact]
    public void HitTest_corner_beats_edge()
    {
        var edge = WindowResizeMath.HitTest(W - 2f, H - 2f, W, H, T);
        Assert.Equal(WindowResizeEdge.SE, edge);
    }

    [Fact]
    public void HitTest_west_edge()
    {
        Assert.Equal(WindowResizeEdge.W, WindowResizeMath.HitTest(2f, H / 2f, W, H, T));
    }

    [Fact]
    public void HitTest_title_interior_is_none()
    {
        Assert.Equal(WindowResizeEdge.None, WindowResizeMath.HitTest(W / 2f, H / 2f, W, H, T));
    }

    [Fact]
    public void ApplyDelta_west_moves_x_and_shrinks_width()
    {
        var r = WindowResizeMath.ApplyDelta(WindowResizeEdge.W, 100f, 50f, 900f, 540f, dx: 40f, dy: 0f, minW: 720f, minH: 420f);
        Assert.Equal(140f, r.X);
        Assert.Equal(50f, r.Y);
        Assert.Equal(860f, r.W);
        Assert.Equal(540f, r.H);
    }

    [Fact]
    public void ApplyDelta_north_moves_y_and_shrinks_height()
    {
        var r = WindowResizeMath.ApplyDelta(WindowResizeEdge.N, 100f, 50f, 900f, 540f, dx: 0f, dy: 30f, minW: 720f, minH: 420f);
        Assert.Equal(100f, r.X);
        Assert.Equal(80f, r.Y);
        Assert.Equal(900f, r.W);
        Assert.Equal(510f, r.H);
    }

    [Fact]
    public void ApplyDelta_respects_min_size_on_west()
    {
        var r = WindowResizeMath.ApplyDelta(WindowResizeEdge.W, 100f, 50f, 740f, 540f, dx: 100f, dy: 0f, minW: 720f, minH: 420f);
        Assert.Equal(720f, r.W);
        Assert.Equal(120f, r.X); // 100 + (740-720)
    }

    [Fact]
    public void HitTest_enlarged_corner_pad_still_se_away_from_thin_edge()
    {
        // Outside 8px edge strips but inside 24px SE corner pad.
        var edge = WindowResizeMath.HitTest(W - 20f, H - 20f, W, H, edgeThickness: 8f, cornerThickness: 24f);
        Assert.Equal(WindowResizeEdge.SE, edge);
    }

    [Fact]
    public void HitTest_bottom_center_stays_south_with_corner_pad()
    {
        Assert.Equal(
            WindowResizeEdge.S,
            WindowResizeMath.HitTest(W / 2f, H - 2f, W, H, edgeThickness: 8f, cornerThickness: 24f));
    }

    [Fact]
    public void ApplyDelta_se_grows_width_height()
    {
        var r = WindowResizeMath.ApplyDelta(WindowResizeEdge.SE, 100f, 50f, 900f, 540f, dx: 20f, dy: 10f, minW: 720f, minH: 420f);
        Assert.Equal(100f, r.X);
        Assert.Equal(50f, r.Y);
        Assert.Equal(920f, r.W);
        Assert.Equal(550f, r.H);
    }
}
