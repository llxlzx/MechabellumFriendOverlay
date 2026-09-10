using FriendOverlay.Core;
using Xunit;

namespace FriendOverlay.Tests;

public class PendingInviteStateTests
{
    private static PendingInviteState Flow() => new PendingInviteState(10.0);

    [Fact]
    public void Begin_FromIdleTakesTheFlow()
    {
        var flow = Flow();

        Assert.True(flow.Begin(7, 100.0, roomPresent: false));
        Assert.True(flow.IsBusy);
        Assert.Equal(7ul, flow.PendingUserId);
    }

    [Fact]
    public void Begin_WhileBusyIsRejectedAndKeepsTheFirstUser()
    {
        var flow = Flow();
        flow.Begin(7, 100.0, roomPresent: false);

        Assert.False(flow.Begin(8, 100.5, roomPresent: false));
        Assert.Equal(7ul, flow.PendingUserId);
    }

    /// <summary>
    /// The flow detects success as "no room, then our room". Starting with a room already there makes
    /// that transition unobservable, so the caller must take the direct path instead.
    /// </summary>
    [Fact]
    public void Begin_WhenRoomIsAlreadyPresentIsRejected()
    {
        var flow = Flow();

        Assert.False(flow.Begin(7, 100.0, roomPresent: true));
        Assert.False(flow.IsBusy);
        Assert.Equal(0ul, flow.PendingUserId);
    }

    [Fact]
    public void Begin_RejectsUserIdZero()
    {
        var flow = Flow();

        Assert.False(flow.Begin(0, 100.0, roomPresent: false));
        Assert.False(flow.IsBusy);
    }

    [Fact]
    public void Tick_WhileIdleReportsNothing()
    {
        var flow = Flow();

        Assert.Equal(InviteStep.None, flow.Tick(100.0, roomPresent: true, weAreHost: true, out var id));
        Assert.Equal(0ul, id);
    }

    [Fact]
    public void Tick_WithNoRoomYetKeepsWaiting()
    {
        var flow = Flow();
        flow.Begin(7, 100.0, roomPresent: false);

        Assert.Equal(InviteStep.None, flow.Tick(100.5, roomPresent: false, weAreHost: false, out var id));
        Assert.Equal(7ul, id);
        Assert.True(flow.IsBusy);
    }

    [Fact]
    public void Tick_WhenOurRoomAppearsSendsTheInvite()
    {
        var flow = Flow();
        flow.Begin(7, 100.0, roomPresent: false);
        flow.Tick(100.5, roomPresent: false, weAreHost: false, out _);

        Assert.Equal(InviteStep.SendInvite, flow.Tick(101.0, roomPresent: true, weAreHost: true, out var id));
        Assert.Equal(7ul, id);
    }

    /// <summary>
    /// A room the player landed in some other way (accepted an invite, matchmaking) is not the room we
    /// created, and inviting into it would drag the friend somewhere unrelated.
    /// </summary>
    [Fact]
    public void Tick_RoomWeDoNotHostIsNotTreatedAsSuccess()
    {
        var flow = Flow();
        flow.Begin(7, 100.0, roomPresent: false);

        Assert.Equal(InviteStep.None, flow.Tick(101.0, roomPresent: true, weAreHost: false, out _));
        Assert.True(flow.IsBusy);
    }

    [Fact]
    public void Tick_ForeignRoomEventuallyTimesOutRatherThanInviting()
    {
        var flow = Flow();
        flow.Begin(7, 100.0, roomPresent: false);
        flow.Tick(101.0, roomPresent: true, weAreHost: false, out _);

        Assert.Equal(InviteStep.TimedOut, flow.Tick(110.0, roomPresent: true, weAreHost: false, out var id));
        Assert.Equal(7ul, id);
        Assert.False(flow.IsBusy);
    }

    [Fact]
    public void Tick_SendsTheInviteOnlyOncePerFlow()
    {
        var flow = Flow();
        flow.Begin(7, 100.0, roomPresent: false);
        Assert.Equal(InviteStep.SendInvite, flow.Tick(101.0, true, true, out _));

        Assert.Equal(InviteStep.None, flow.Tick(101.1, true, true, out var id));
        Assert.Equal(0ul, id);
    }

    [Fact]
    public void Tick_AfterSendingIsIdleSoTheNextInviteCanStart()
    {
        var flow = Flow();
        flow.Begin(7, 100.0, roomPresent: false);
        flow.Tick(101.0, true, true, out _);

        Assert.False(flow.IsBusy);
        Assert.True(flow.Begin(8, 102.0, roomPresent: false));
        Assert.Equal(8ul, flow.PendingUserId);
    }

    /// <summary>Guards a one-shot latch bug: waiting must not be recorded as a decision.</summary>
    [Fact]
    public void Tick_RoomFlickeringOffBeforeItAppearsStillSends()
    {
        var flow = Flow();
        flow.Begin(7, 100.0, roomPresent: false);

        Assert.Equal(InviteStep.None, flow.Tick(100.2, false, false, out _));
        Assert.Equal(InviteStep.None, flow.Tick(100.4, false, false, out _));
        Assert.Equal(InviteStep.SendInvite, flow.Tick(100.6, true, true, out _));
    }

    [Fact]
    public void Tick_JustBeforeTheTimeoutIsStillWaiting()
    {
        var flow = Flow();
        flow.Begin(7, 100.0, roomPresent: false);

        Assert.Equal(InviteStep.None, flow.Tick(109.9, false, false, out _));
        Assert.True(flow.IsBusy);
    }

    /// <summary>Boundary is inclusive, matching InviteCooldown's elapsed &lt; seconds.</summary>
    [Fact]
    public void Tick_AtTheTimeoutBoundaryTimesOut()
    {
        var flow = Flow();
        flow.Begin(7, 100.0, roomPresent: false);

        Assert.Equal(InviteStep.TimedOut, flow.Tick(110.0, false, false, out var id));
        Assert.Equal(7ul, id);
        Assert.False(flow.IsBusy);
    }

    [Fact]
    public void Tick_AfterTimingOutReportsNothingAgain()
    {
        var flow = Flow();
        flow.Begin(7, 100.0, roomPresent: false);
        flow.Tick(110.0, false, false, out _);

        Assert.Equal(InviteStep.None, flow.Tick(110.5, false, false, out var id));
        Assert.Equal(0ul, id);
    }

    [Fact]
    public void Tick_RoomArrivingAfterTheTimeoutDoesNotSendAStaleInvite()
    {
        var flow = Flow();
        flow.Begin(7, 100.0, roomPresent: false);
        flow.Tick(110.0, false, false, out _);

        Assert.Equal(InviteStep.None, flow.Tick(110.5, roomPresent: true, weAreHost: true, out _));
    }

    [Fact]
    public void Tick_AfterTimingOutAllowsANewFlow()
    {
        var flow = Flow();
        flow.Begin(7, 100.0, roomPresent: false);
        flow.Tick(110.0, false, false, out _);

        Assert.True(flow.Begin(8, 111.0, roomPresent: false));
    }

    /// <summary>
    /// Opposite polarity from InviteCooldown on purpose: here failing open means releasing the mutex,
    /// because a wedged mutex would kill the invite button mod-wide until restart.
    /// </summary>
    [Fact]
    public void Tick_BackwardsClockTimesOutInsteadOfWedgingTheMutex()
    {
        var flow = Flow();
        flow.Begin(7, 100.0, roomPresent: false);

        Assert.Equal(InviteStep.TimedOut, flow.Tick(1.0, false, false, out var id));
        Assert.Equal(7ul, id);
        Assert.False(flow.IsBusy);
    }

    [Fact]
    public void Tick_BackwardsClockStillSendsWhenOurRoomIsThere()
    {
        var flow = Flow();
        flow.Begin(7, 100.0, roomPresent: false);

        Assert.Equal(InviteStep.SendInvite, flow.Tick(1.0, roomPresent: true, weAreHost: true, out _));
    }

    /// <summary>
    /// The create-room callback carries a server code; a non-zero one means no room is coming, so the
    /// row should recover immediately instead of sitting out the full timeout.
    /// </summary>
    [Fact]
    public void Fail_DropsTheFlowImmediately()
    {
        var flow = Flow();
        flow.Begin(7, 100.0, roomPresent: false);

        Assert.True(flow.Fail());
        Assert.False(flow.IsBusy);
        Assert.Equal(InviteStep.None, flow.Tick(101.0, roomPresent: true, weAreHost: true, out _));
    }

    [Fact]
    public void Fail_WhileIdleReportsNothingToDrop()
    {
        Assert.False(Flow().Fail());
    }

    [Fact]
    public void Reset_DropsAnInFlightFlow()
    {
        var flow = Flow();
        flow.Begin(7, 100.0, roomPresent: false);
        flow.Reset();

        Assert.False(flow.IsBusy);
        Assert.Equal(0ul, flow.PendingUserId);
        Assert.Equal(InviteStep.None, flow.Tick(101.0, roomPresent: true, weAreHost: true, out _));
    }

    [Fact]
    public void Reset_WhileIdleIsHarmless()
    {
        var flow = Flow();
        flow.Reset();

        Assert.False(flow.IsBusy);
        Assert.True(flow.Begin(7, 100.0, roomPresent: false));
    }

    /// <summary>Pins the intended composition of the two units, with no mocks in between.</summary>
    [Fact]
    public void IsBusy_DisablesInviteForEveryRowIncludingTheDirectPath()
    {
        var flow = Flow();
        flow.Begin(7, 100.0, roomPresent: false);

        Assert.Equal(
            InvitePath.Disabled,
            InviteRules.Resolve(true, canCreateRoom: true, inRoom: false, online: true, flowBusy: flow.IsBusy));
        Assert.Equal(
            InvitePath.Disabled,
            InviteRules.Resolve(true, canCreateRoom: true, inRoom: true, online: true, flowBusy: flow.IsBusy));
    }
}
