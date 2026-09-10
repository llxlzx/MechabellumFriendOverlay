using FriendOverlay.Core;
using Xunit;

namespace FriendOverlay.Tests;

public class InviteRulesTests
{
    [Theory]
    [InlineData(true, true, true, true)]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, false)]
    public void CanInvite_RequiresCapabilityRoomAndOnline(bool capability, bool inRoom, bool online, bool expected)
    {
        Assert.Equal(expected, InviteRules.CanInvite(capability, inRoom, online));
    }

    [Fact]
    public void Cooldown_IsActiveInsideWindowOnly()
    {
        var c = new InviteCooldown(5.0);
        c.Mark(7, 100.0);

        Assert.True(c.IsActive(7, 100.0));
        Assert.True(c.IsActive(7, 104.9));
        Assert.False(c.IsActive(7, 105.0));
    }

    [Fact]
    public void Cooldown_UnknownUserIsInactive()
    {
        Assert.False(new InviteCooldown(5.0).IsActive(1, 0.0));
    }

    [Fact]
    public void Cooldown_IsPerUser()
    {
        var c = new InviteCooldown(5.0);
        c.Mark(1, 0.0);
        Assert.False(c.IsActive(2, 1.0));
    }

    [Fact]
    public void Cooldown_ReMarkExtendsWindow()
    {
        var c = new InviteCooldown(5.0);
        c.Mark(1, 0.0);
        c.Mark(1, 4.0);
        Assert.True(c.IsActive(1, 8.0));
    }

    [Fact]
    public void Cooldown_ClearForgetsEverything()
    {
        var c = new InviteCooldown(5.0);
        c.Mark(1, 0.0);
        c.Clear();
        Assert.False(c.IsActive(1, 1.0));
    }

    /// <summary>
    /// A clock that jumps backwards (scene reload, Time reset) must not wedge a row as invited.
    /// </summary>
    [Fact]
    public void Cooldown_BackwardsClockIsNotActive()
    {
        var c = new InviteCooldown(5.0);
        c.Mark(1, 100.0);
        Assert.False(c.IsActive(1, 1.0));
    }
}
