using FriendOverlay.Core;
using Xunit;

namespace FriendOverlay.Tests;

public class BakeBudgetTests
{
    public BakeBudgetTests()
    {
        BakeBudget.Limit = BakeBudget.DefaultLimit;
        BakeBudget.Reset();
    }

    [Fact]
    public void TryConsume_allows_DefaultLimit_then_denies_until_next_frame()
    {
        BakeBudget.BeginFrame(1);
        Assert.True(BakeBudget.TryConsume());
        Assert.True(BakeBudget.TryConsume());
        Assert.False(BakeBudget.TryConsume());
        Assert.Equal(0, BakeBudget.Remaining);

        BakeBudget.BeginFrame(2);
        Assert.True(BakeBudget.TryConsume());
        Assert.Equal(BakeBudget.DefaultLimit - 1, BakeBudget.Remaining);
    }

    [Fact]
    public void BeginFrame_same_frame_does_not_reset()
    {
        BakeBudget.BeginFrame(5);
        Assert.True(BakeBudget.TryConsume());
        Assert.True(BakeBudget.TryConsume());
        BakeBudget.BeginFrame(5);
        Assert.False(BakeBudget.TryConsume());
    }
}
