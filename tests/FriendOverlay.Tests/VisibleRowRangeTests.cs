using FriendOverlay.Core;

namespace FriendOverlay.Tests;

public class VisibleRowRangeTests
{
    [Fact]
    public void Compute_returns_intersecting_indices_with_margin()
    {
        // Three 40px rows stacked; viewport 50px at scroll 30 → rows 0..2 partially relevant
        var offsets = new float[] { 0f, 40f, 80f };
        var heights = new float[] { 40f, 40f, 40f };
        var (start, end) = VisibleRowRange.Compute(
            scrollY: 30f,
            viewportHeight: 50f,
            itemOffsets: offsets,
            itemHeights: heights,
            marginItems: 0);
        Assert.Equal(0, start);
        Assert.Equal(3, end);
    }

    [Fact]
    public void Compute_applies_index_margin()
    {
        var offsets = new float[] { 0f, 40f, 80f, 120f, 160f };
        var heights = new float[] { 40f, 40f, 40f, 40f, 40f };
        // Viewport shows only row index 2 (offset 80) when scrollY=80, height=40
        var (start, end) = VisibleRowRange.Compute(80f, 40f, offsets, heights, marginItems: 1);
        Assert.Equal(1, start);
        Assert.Equal(4, end);
    }

    [Fact]
    public void Compute_empty_is_zero_span()
    {
        var (start, end) = VisibleRowRange.Compute(0f, 100f, Array.Empty<float>(), Array.Empty<float>(), 2);
        Assert.Equal(0, start);
        Assert.Equal(0, end);
    }
}
