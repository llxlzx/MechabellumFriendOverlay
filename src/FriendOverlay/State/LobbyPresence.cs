using FriendOverlay.Core;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FriendOverlay.State
{
    /// <summary>
    /// Closes the overlay when the local player leaves the multiplayer lobby into loading/match.
    /// </summary>
    public static class LobbyPresence
    {
        private static string? _lobbySceneName;
        private static int _presenceFailFrames;
        private static string? _lastActiveScene;

        public static void NoteSessionBegan()
        {
            try
            {
                _lobbySceneName = SceneManager.GetActiveScene().name;
            }
            catch
            {
                _lobbySceneName = null;
            }

            _presenceFailFrames = 0;
            _lastActiveScene = _lobbySceneName;
        }

        public static void NoteSessionEnded()
        {
            _lobbySceneName = null;
            _presenceFailFrames = 0;
            _lastActiveScene = null;
        }

        public static void OnSceneLoaded(string sceneName)
        {
            if (OverlaySession.Panel == null && !OverlaySession.OverlayVisible)
                return;

            if (LobbyLeavePolicy.ShouldCloseOnSceneChange(_lobbySceneName, sceneName))
            {
                MelonLogger.Msg("[FriendOverlay] leave-lobby scene change → close overlay (" +
                                _lobbySceneName + " → " + sceneName + ")");
                OverlaySession.End();
            }
        }

        public static void Tick()
        {
            if (OverlaySession.Degraded)
                return;

            if (OverlaySession.Panel == null)
            {
                _presenceFailFrames = 0;
                return;
            }

            string? active = null;
            try
            {
                active = SceneManager.GetActiveScene().name;
            }
            catch
            {
                // ignore
            }

            if (!string.IsNullOrEmpty(active) &&
                !string.Equals(active, _lastActiveScene, System.StringComparison.Ordinal))
            {
                _lastActiveScene = active;
                if (LobbyLeavePolicy.ShouldCloseOnSceneChange(_lobbySceneName, active))
                {
                    MelonLogger.Msg("[FriendOverlay] leave-lobby active scene → close overlay (" +
                                    _lobbySceneName + " → " + active + ")");
                    OverlaySession.End();
                    return;
                }
            }

            if (ProbeLobbyStillPresent())
            {
                _presenceFailFrames = 0;
                return;
            }

            _presenceFailFrames++;
            if (LobbyLeavePolicy.ShouldCloseOnPresenceLost(_presenceFailFrames))
            {
                MelonLogger.Msg("[FriendOverlay] leave-lobby presence lost → close overlay");
                OverlaySession.End();
            }
        }

        /// <summary>
        /// True while the session's FriendPanel / panel layer still looks like a live lobby HUD.
        /// </summary>
        public static bool ProbeLobbyStillPresent()
        {
            var panel = OverlaySession.Panel;
            if (panel == null)
                return false;

            try
            {
                // Il2Cpp objects can be "fake null"
                if (panel.Equals(null))
                    return false;
            }
            catch
            {
                return false;
            }

            try
            {
                var layer = OverlaySession.PanelLayer;
                if (layer != null && !layer.Equals(null) && layer)
                {
                    // Layer may be alpha-hidden by us; still "present" if the object exists.
                    return true;
                }
            }
            catch
            {
                // fall through
            }

            try
            {
                return panel.gameObject != null && panel.gameObject;
            }
            catch
            {
                return false;
            }
        }
    }
}
