using Il2CppGameRiver.Client;
using UnityEngine;

namespace FriendOverlay.State
{
    public enum OverlayMode
    {
        Overlay = 0,
        Native = 1,
    }

    public static class OverlaySession
    {
        public static bool Degraded { get; set; }
        public static bool OverlayVisible { get; set; }
        public static OverlayMode Mode { get; set; } = OverlayMode.Overlay;
        public static FriendPanel? Panel { get; private set; }
        public static FriendProxy? Proxy { get; private set; }
        public static GameObject? PanelLayer { get; private set; }

        public static bool PreferOverlayDefault { get; set; } = true;
        public static KeyCode ToggleHotkey { get; set; } = KeyCode.F8;

        /// <summary>
        /// Hide the native panel by zeroing a CanvasGroup instead of deactivating it. Avatars no
        /// longer depend on this, but it is the tested hide path. Falls back to SetActive on failure.
        /// </summary>
        public static bool UseTransparentHide { get; set; } = true;

        /// <summary>Set by the mod entry point so window/sort prefs survive a crash or alt-F4.</summary>
        public static System.Action? PersistPreferences { get; set; }

        private static CanvasGroup? _group;
        private static bool _groupAddedByUs;
        private static bool _transparentFailed;

        /// <summary>Start or refresh a friends session using the user's default overlay preference.</summary>
        public static void Begin(FriendPanel panel, bool resetModeToPreference = true)
        {
            if (Panel != panel)
            {
                // Hand the old panel back before dropping our only handle to its CanvasGroup.
                RestoreTransparency();

                _group = null;
                _groupAddedByUs = false;
                _transparentFailed = false;
                ResetAvatarPipeline();
                ResetSessionServices();
            }

            Panel = panel;
            try
            {
                Proxy = panel.friendProxy;
                PanelLayer = panel.panelLayer;
            }
            catch
            {
                Proxy = null;
                PanelLayer = null;
            }

            if (resetModeToPreference)
                Mode = PreferOverlayDefault ? OverlayMode.Overlay : OverlayMode.Native;

            ApplyModeVisibility();
            OverlayVisible = Mode == OverlayMode.Overlay;
        }

        public static void End(bool restoreNativeLayer = true)
        {
            OverlayVisible = false;

            UI.InputShield.Destroy();

            // Always undo our own CanvasGroup change, otherwise the native panel would stay
            // invisible the next time the game shows it without us.
            RestoreTransparency();

            try
            {
                if (restoreNativeLayer && PanelLayer != null)
                    PanelLayer.SetActive(true);
            }
            catch
            {
                // ignore
            }

            Panel = null;
            Proxy = null;
            PanelLayer = null;
            _group = null;
            _groupAddedByUs = false;
            ResetAvatarPipeline();
            ResetSessionServices();

            try
            {
                PersistPreferences?.Invoke();
            }
            catch
            {
                // preferences are best-effort
            }
        }

        /// <summary>
        /// One place to tear the avatar pipeline down. Both the session end and the mid-session
        /// panel swap need all of it; clearing only part of it strands per-uid state against a
        /// cache that no longer has the matching entries.
        /// </summary>
        private static void ResetAvatarPipeline()
        {
            UI.AvatarLoader.Reset();
            UI.GameSpriteResolver.Reset();
            UI.SharedImageCache.Clear();
            Data.PortraitResolver.Reset();
            Data.FaceBlockPolicy.Reset();
            UI.AvatarCache.Clear();
            UI.GameAssets.Reset();
        }

        /// <summary>
        /// Per-session state that is not the avatar pipeline: proxy handles plus the panel's own
        /// session-scoped statics (invite cooldown, active tab, menus). One path, called from both a
        /// mid-session panel swap and session end, so a new service cannot be wired into one and
        /// forgotten in the other.
        /// </summary>
        private static void ResetSessionServices()
        {
            Data.GameProxies.Reset();
            UI.ImguiFriendOverlay.OnSessionEnd();
        }

        public static void ToggleMode()
        {
            if (Panel == null || Degraded)
                return;

            Mode = Mode == OverlayMode.Overlay ? OverlayMode.Native : OverlayMode.Overlay;
            ApplyModeVisibility();
            OverlayVisible = Mode == OverlayMode.Overlay;
        }

        public static void ReapplyVisibility() => ApplyModeVisibility();

        public static void FailOpen(string reason)
        {
            Degraded = true;
            OverlayVisible = false;
            Mode = OverlayMode.Native;

            UI.InputShield.Destroy();
            RestoreTransparency();

            try
            {
                if (PanelLayer != null)
                    PanelLayer.SetActive(true);
            }
            catch
            {
                // ignore
            }

            MelonLoader.MelonLogger.Error("[FriendOverlay] Fail-open: " + reason);
        }

        private static void ApplyModeVisibility()
        {
            var wantNative = Mode == OverlayMode.Native;

            // Reconcile the shield before the PanelLayer guard: leaving a stale full-screen
            // blocker behind would make the whole game unclickable.
            if (wantNative)
            {
                UI.InputShield.Destroy();
            }
            else
            {
                UI.InputShield.Ensure();
                if (!UI.InputShield.Active)
                {
                    // Hiding the native panel without input isolation would let clicks fall
                    // through to the lobby, so hand the UI back instead.
                    FailOpen("input shield unavailable");
                    return;
                }
            }

            if (PanelLayer == null)
                return;

            if (UseTransparentHide && !_transparentFailed && TrySetTransparent(!wantNative))
                return;

            try
            {
                PanelLayer.SetActive(wantNative);
            }
            catch
            {
                FailOpen("panelLayer.SetActive failed");
            }
        }

        private static bool TrySetTransparent(bool hidden)
        {
            try
            {
                var layer = PanelLayer;
                if (layer == null)
                    return false;

                if (_group == null)
                {
                    _group = layer.GetComponent<CanvasGroup>();
                    if (_group == null)
                    {
                        _group = layer.AddComponent<CanvasGroup>();
                        _groupAddedByUs = _group != null;
                    }
                }

                if (_group == null)
                    return false;

                if (!layer.activeSelf)
                    layer.SetActive(true);

                _group.alpha = hidden ? 0f : 1f;
                _group.blocksRaycasts = !hidden;
                _group.interactable = !hidden;
                return true;
            }
            catch (System.Exception ex)
            {
                _transparentFailed = true;
                MelonLoader.MelonLogger.Warning(
                    "[FriendOverlay] CanvasGroup hide unavailable, using SetActive: " + ex.Message);
                return false;
            }
        }

        private static void RestoreTransparency()
        {
            try
            {
                // The session can end on a path that never ran TrySetTransparent, so look the
                // component up again rather than trusting the cached reference.
                var group = _group;
                if (group == null && PanelLayer != null)
                    group = PanelLayer.GetComponent<CanvasGroup>();

                if (group == null)
                    return;

                group.alpha = 1f;
                group.blocksRaycasts = true;
                group.interactable = true;

                if (_groupAddedByUs)
                    UnityEngine.Object.Destroy(group);
            }
            catch
            {
                // ignore
            }
        }
    }
}
