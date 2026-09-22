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
    public void Accepted_Quit_Forces_Exit_Immediately()
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

    [Fact]
    public void Quit_Marker_Is_Detected_In_A_Log_Tail()
    {
        var marker = System.Text.Encoding.ASCII.GetBytes("x OnApplicationWantsToQuit y");
        Assert.True(QuitStallGuard.TailHasQuitMarker(marker, marker.Length));
    }

    [Fact]
    public void Older_Log_Text_Without_The_Marker_Does_Not_Arm()
    {
        var text = System.Text.Encoding.ASCII.GetBytes("shut down finish");
        Assert.False(QuitStallGuard.TailHasQuitMarker(text, text.Length));
        Assert.False(QuitStallGuard.TailHasQuitMarker(text, 0));
        Assert.False(QuitStallGuard.TailHasQuitMarker(null!, 4));
    }
}
