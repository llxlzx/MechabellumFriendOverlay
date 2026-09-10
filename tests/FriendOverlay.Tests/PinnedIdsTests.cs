using FriendOverlay.Core;
using Xunit;

namespace FriendOverlay.Tests;

public class PinnedIdsTests
{
    [Fact]
    public void Parse_IgnoresJunkWhitespaceAndDuplicates()
    {
        var pins = PinnedIds.Parse(" 1, 2,x,2 ,,3,-4,0");
        Assert.Equal(new ulong[] { 1, 2, 3 }, pins.Items.ToArray());
    }

    [Fact]
    public void Parse_NullOrBlankIsEmpty()
    {
        Assert.Equal(0, PinnedIds.Parse(null).Count);
        Assert.Equal(0, PinnedIds.Parse("   ").Count);
    }

    [Fact]
    public void Parse_TruncatesToMax()
    {
        var csv = string.Join(",", Enumerable.Range(1, PinnedIds.Max + 5).Select(i => i.ToString()));
        var pins = PinnedIds.Parse(csv);
        Assert.Equal(PinnedIds.Max, pins.Count);
        Assert.True(pins.IsFull);
        Assert.Equal((ulong)PinnedIds.Max, pins.Items[pins.Count - 1]);
    }

    [Fact]
    public void Add_RefusesZeroDuplicateAndOverflow()
    {
        var pins = new PinnedIds();
        Assert.False(pins.Add(0));
        Assert.True(pins.Add(5));
        Assert.False(pins.Add(5));
        for (ulong i = 100; pins.Count < PinnedIds.Max; i++)
            Assert.True(pins.Add(i));
        Assert.False(pins.Add(9999));
    }

    [Fact]
    public void Toggle_AddsThenRemovesAndReportsState()
    {
        var pins = new PinnedIds();
        Assert.True(pins.Toggle(7, out var nowPinned));
        Assert.True(nowPinned);
        Assert.True(pins.Contains(7));

        Assert.True(pins.Toggle(7, out nowPinned));
        Assert.False(nowPinned);
        Assert.False(pins.Contains(7));
    }

    [Fact]
    public void Toggle_WhenFullDoesNotChange()
    {
        var pins = PinnedIds.Parse(string.Join(",", Enumerable.Range(1, PinnedIds.Max)));
        Assert.False(pins.Toggle(777, out var nowPinned));
        Assert.False(nowPinned);
        Assert.Equal(PinnedIds.Max, pins.Count);
    }

    /// <summary>Unpinning must work even while full, otherwise a full set can never be reduced.</summary>
    [Fact]
    public void Toggle_WhenFullStillUnpinsAnExistingId()
    {
        var pins = PinnedIds.Parse(string.Join(",", Enumerable.Range(1, PinnedIds.Max)));
        Assert.True(pins.Toggle(1, out var nowPinned));
        Assert.False(nowPinned);
        Assert.Equal(PinnedIds.Max - 1, pins.Count);
    }

    [Fact]
    public void Remove_ReturnsFalseWhenAbsent()
    {
        var pins = new PinnedIds();
        Assert.False(pins.Remove(1));
    }

    [Fact]
    public void ToCsv_RoundTripsInOrder()
    {
        var pins = PinnedIds.Parse("30,10,20");
        Assert.Equal("30,10,20", pins.ToCsv());
        Assert.Equal(string.Empty, new PinnedIds().ToCsv());
    }
}
