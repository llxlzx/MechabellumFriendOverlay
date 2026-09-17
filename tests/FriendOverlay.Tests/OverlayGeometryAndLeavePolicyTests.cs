using FriendOverlay.Core;
using Xunit;

namespace FriendOverlay.Tests
{
    public class OverlayGeometryTests
    {
        [Fact]
        public void TryClamp_noop_when_above_safe_band()
        {
            var changed = OverlayGeometry.TryClampToBottomSafeArea(
                60, 48, 900, 540, 1920, 1080,
                out var x, out var y, out var w, out var h);

            Assert.False(changed);
            Assert.Equal(60, x);
            Assert.Equal(48, y);
            Assert.Equal(900, w);
            Assert.Equal(540, h);
        }

        [Fact]
        public void TryClamp_shrinks_tall_window_off_bottom_band()
        {
            // 48+740=788 > 1080-280=800? Wait 788 < 800, use taller
            // bottom safe max = 800; window y=48 h=800 → bottom 848 > 800
            var changed = OverlayGeometry.TryClampToBottomSafeArea(
                60, 48, 900, 800, 1920, 1080,
                out _, out var y, out _, out var h);

            Assert.True(changed);
            Assert.True(y + h <= 1080 - OverlayGeometry.BottomSafeBand + 0.5f);
        }
    }

    public class LobbyLeavePolicyTests
    {
        [Fact]
        public void SceneChange_closes_when_names_differ()
        {
            Assert.True(LobbyLeavePolicy.ShouldCloseOnSceneChange("Lobby", "BattleLoading"));
        }

        [Fact]
        public void SceneChange_stays_when_same()
        {
            Assert.False(LobbyLeavePolicy.ShouldCloseOnSceneChange("Lobby", "Lobby"));
        }

        [Fact]
        public void SceneChange_noop_without_lobby_capture()
        {
            Assert.False(LobbyLeavePolicy.ShouldCloseOnSceneChange(null, "BattleLoading"));
        }

        [Fact]
        public void Presence_closes_after_threshold()
        {
            Assert.False(LobbyLeavePolicy.ShouldCloseOnPresenceLost(2));
            Assert.True(LobbyLeavePolicy.ShouldCloseOnPresenceLost(3));
        }

        [Fact]
        public void MatchLoading_closes_when_visible()
        {
            Assert.True(LobbyLeavePolicy.ShouldCloseOnMatchLoadingVisible(true));
            Assert.False(LobbyLeavePolicy.ShouldCloseOnMatchLoadingVisible(false));
        }
    }
}
