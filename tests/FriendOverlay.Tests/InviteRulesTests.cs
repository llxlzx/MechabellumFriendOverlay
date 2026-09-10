using FriendOverlay.Core;
using Xunit;

namespace FriendOverlay.Tests;

public class InviteRulesTests
{
    /// <summary>
    /// Gate held open, so this is the whole 2x2 of the part that actually decides a path. Row 4 is
    /// load-bearing: already in a room means invite straight away and never open the picker.
    /// </summary>
    [Theory]
    [InlineData(false, false, InvitePath.Disabled)]
    [InlineData(false, true, InvitePath.Direct)]
    [InlineData(true, false, InvitePath.Picker)]
    [InlineData(true, true, InvitePath.Direct)]
    public void Resolve_WithGateOpen_PrefersDirectThenPicker(bool canCreateRoom, bool inRoom, InvitePath expected)
    {
        Assert.Equal(
            expected,
            InviteRules.Resolve(capability: true, canCreateRoom, inRoom, online: true, flowBusy: false));
    }

    /// <summary>
    /// Seven rows exhaust (capability, online, flowBusy) minus the one open combination, and the four
    /// calls exhaust (canCreateRoom, inRoom): 7 x 4 + the theory above = all 32 worlds. The array
    /// compare is deliberate, so a failure names which room state broke.
    /// </summary>
    [Theory]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(false, false, true)]
    public void Resolve_WithGateClosed_IsDisabledForEveryRoomState(bool capability, bool online, bool flowBusy)
    {
        var paths = new[]
        {
            InviteRules.Resolve(capability, canCreateRoom: false, inRoom: false, online, flowBusy),
            InviteRules.Resolve(capability, canCreateRoom: false, inRoom: true, online, flowBusy),
            InviteRules.Resolve(capability, canCreateRoom: true, inRoom: false, online, flowBusy),
            InviteRules.Resolve(capability, canCreateRoom: true, inRoom: true, online, flowBusy),
        };

        Assert.Equal(
            new[] { InvitePath.Disabled, InvitePath.Disabled, InvitePath.Disabled, InvitePath.Disabled },
            paths);
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
