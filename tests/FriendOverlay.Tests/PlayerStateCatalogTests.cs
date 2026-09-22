using FriendOverlay.Core;

namespace FriendOverlay.Tests;

public class PlayerStateCatalogTests
{
    [Fact]
    public void Offline_Is_16_And_Not_Online()
    {
        Assert.Equal(16, PlayerStateCatalog.Offline);
        Assert.Equal("state.offline", PlayerStateCatalog.LabelKey(16));
        Assert.False(PlayerStateCatalog.IsOnline(16));
        Assert.False(PlayerStateCatalog.IsBusy(16));
        Assert.Equal(FriendStatusKind.Offline, PlayerStateCatalog.Kind(16));
    }

    [Theory]
    [InlineData(14, "state.async1v1")]
    [InlineData(15, "state.async2v2")]
    public void Async_Battles_Are_Online_Busy(int state, string labelKey)
    {
        Assert.Equal(labelKey, PlayerStateCatalog.LabelKey(state));
        Assert.True(PlayerStateCatalog.IsOnline(state));
        Assert.True(PlayerStateCatalog.IsBusy(state));
        Assert.Equal(FriendStatusKind.Battle, PlayerStateCatalog.Kind(state));
    }

    [Fact]
    public void Idle_And_Competition_Wait_Stay_Joinable()
    {
        Assert.Equal("state.idle", PlayerStateCatalog.LabelKey(0));
        Assert.True(PlayerStateCatalog.IsOnline(0));
        Assert.False(PlayerStateCatalog.IsBusy(0));
        Assert.Equal(FriendStatusKind.Idle, PlayerStateCatalog.Kind(0));

        Assert.Equal("state.comp_idle", PlayerStateCatalog.LabelKey(13));
        Assert.True(PlayerStateCatalog.IsOnline(13));
        Assert.False(PlayerStateCatalog.IsBusy(13));
        Assert.Equal(FriendStatusKind.Waiting, PlayerStateCatalog.Kind(13));
    }

    [Fact]
    public void Unknown_State_Has_No_Label()
    {
        Assert.Null(PlayerStateCatalog.LabelKey(99));
        Assert.Equal(FriendStatusKind.Battle, PlayerStateCatalog.Kind(99));
    }
}
