using FriendOverlay.Core;

namespace FriendOverlay.Tests;

public class QuitStallGuardTests
{
    [Fact]
    public void Entering_Quit_Arms_A_Stuck_Callback_Fuse()
    {
        var decision = QuitStallGuard.OnEnter();

        Assert.True(decision.ForceExit);
        Assert.Equal(QuitStallGuard.StuckCallbackFuseMs, decision.DelayMs);
    }

    [Fact]
    public void Accepted_Quit_Forces_Exit_After_A_Short_Grace()
    {
        var decision = QuitStallGuard.OnReturned(quitAllowed: true);

        Assert.True(decision.ForceExit);
        Assert.Equal(QuitStallGuard.AcceptedQuitGraceMs, decision.DelayMs);
    }

    [Fact]
    public void Cancelled_Quit_Does_Not_Force_Exit()
    {
        var decision = QuitStallGuard.OnReturned(quitAllowed: false);

        Assert.False(decision.ForceExit);
        Assert.Equal(0, decision.DelayMs);
    }
}
