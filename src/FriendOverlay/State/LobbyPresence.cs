using FriendOverlay.Core;
using Il2CppGameRiver.Client;
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
        private static int _nextLoadingProbeFrame;

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

            if (OverlaySession.Panel == null && !OverlaySession.OverlayVisible)
            {
                _presenceFailFrames = 0;
                return;
            }

            // Harmony OnOpen is primary; poll lightly in case OnOpen was missed.
            if (Time.frameCount >= _nextLoadingProbeFrame)
            {
                _nextLoadingProbeFrame = Time.frameCount + 5;
                if (LobbyLeavePolicy.ShouldCloseOnMatchLoadingVisible(ProbeMatchLoadingVisible()))
                {
                    MelonLogger.Msg("[FriendOverlay] leave-lobby match loading → close overlay");
                    OverlaySession.End();
                    return;
                }
            }

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
        /// True when the game's main-scene loading window is active (loading bar UI).
        /// </summary>
        public static bool ProbeMatchLoadingVisible()
        {
            try
            {
                var windows = Resources.FindObjectsOfTypeAll<MainSceneLoadingWindow>();
                if (windows == null || windows.Length == 0)
                    return false;

                for (var i = 0; i < windows.Length; i++)
                {
                    var w = windows[i];
                    if (w == null || w.Equals(null))
                        continue;

                    try
                    {
                        var go = w.gameObject;
                        if (go != null && !go.Equals(null) && go.activeInHierarchy)
                            return true;
                    }
                    catch
                    {
                        // next
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
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
