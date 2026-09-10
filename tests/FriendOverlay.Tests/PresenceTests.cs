using FriendOverlay.Core;
using Xunit;

namespace FriendOverlay.Tests
{
    /// <summary>
    /// The game's EPlayerState has Idle = 0 and Offline = 14, so an unreported state defaults to
    /// "idle and joinable" rather than to "offline". Every list that cannot vouch for its own State
    /// field has to say so, otherwise strangers show up as joinable and 邀请 / 加入 light up for
    /// players who may well be offline.
    /// </summary>
    public class PresenceTests
    {
        private const int Idle = 0;
        private const int Battle = 1;
        private const int Offline = 14;

        [Fact]
        public void Resolve_ReportedStateWins()
        {
            var p = Presence.Resolve(Battle, Idle, Offline);

            Assert.Equal(Battle, p.State);
            Assert.True(p.Known);
        }

        [Fact]
        public void Resolve_FallsBackToListedStateWhenTrusted()
        {
            var p = Presence.Resolve(null, Battle, Offline);

            Assert.Equal(Battle, p.State);
            Assert.True(p.Known);
        }

        [Fact]
        public void Resolve_ReportedIdleIsStillKnown()
        {
            var p = Presence.Resolve(Idle, null, Offline);

            Assert.Equal(Idle, p.State);
            Assert.True(p.Known);
        }

        [Fact]
        public void Resolve_NothingReportedIsOfflineAndUnknown()
        {
            var p = Presence.Resolve(null, null, Offline);

            Assert.Equal(Offline, p.State);
            Assert.False(p.Known);
        }

        [Fact]
        public void Resolve_UntrustedListStateDoesNotBecomeIdle()
        {
            // A followers row whose State field the server never filled in arrives as 0 = Idle.
            // The caller passes null for "do not trust this field"; the result must not be joinable.
            var p = Presence.Resolve(null, null, Offline);

            Assert.NotEqual(Idle, p.State);
        }

        [Fact]
        public void UnknownRowCannotBeInvited()
        {
            var p = Presence.Resolve(null, null, Offline);

            Assert.Equal(
                InvitePath.Disabled,
                InviteRules.Resolve(
                    capability: true,
                    canCreateRoom: true,
                    inRoom: true,
                    online: p.Known && p.State != Offline,
                    flowBusy: false));
        }
    }
}
