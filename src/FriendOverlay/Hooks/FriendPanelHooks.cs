using System;
using FriendOverlay.Compat;
using FriendOverlay.Data;
using FriendOverlay.State;
using HarmonyLib;
using Il2CppGameRiver.Client;
using Il2CppProtos.Friend;
using MelonLoader;

namespace FriendOverlay.Hooks
{
    public static class FriendPanelHooks
    {
        public static void Apply(HarmonyLib.Harmony harmony)
        {
            var click = AccessTools.Method(typeof(FriendPanel), nameof(FriendPanel.clickFriendBtn));
            var release = AccessTools.Method(typeof(FriendPanel), nameof(FriendPanel.Release));
            var close = AccessTools.Method(typeof(FriendPanel), nameof(FriendPanel.ClosePanelBtnOnClicked));

            if (click == null)
                throw new MissingMethodException("FriendPanel.clickFriendBtn");

            harmony.Patch(click, postfix: new HarmonyMethod(typeof(FriendPanelHooks), nameof(ClickFriendBtnPostfix)));
            harmony.Patch(release, postfix: new HarmonyMethod(typeof(FriendPanelHooks), nameof(ReleasePostfix)));
            harmony.Patch(close, postfix: new HarmonyMethod(typeof(FriendPanelHooks), nameof(ClosePostfix)));

            // Secondary: some code paths may still call Show/Hide on the element root.
            var show = AccessTools.Method(typeof(GRUIElement), nameof(GRUIElement.Show));
            var hide = AccessTools.Method(typeof(GRUIElement), nameof(GRUIElement.Hide));
            if (show != null)
                harmony.Patch(show, postfix: new HarmonyMethod(typeof(FriendPanelHooks), nameof(ShowPostfix)));
            if (hide != null)
                harmony.Patch(hide, postfix: new HarmonyMethod(typeof(FriendPanelHooks), nameof(HidePostfix)));

            // Optional: only useful when the followers surface exists at all.
            if (Capabilities.Followers)
            {
                var lastFollower = AccessTools.Method(typeof(FriendProxy), nameof(FriendProxy.OnResponseLastFollower));
                if (lastFollower != null)
                {
                    harmony.Patch(
                        lastFollower,
                        postfix: new HarmonyMethod(typeof(FriendPanelHooks), nameof(LastFollowerPostfix)));
                }
            }
        }

        public static void LastFollowerPostfix(ResponseLastFollower follower)
        {
            try
            {
                FansListService.OnResponse(follower);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[FriendOverlay] LastFollowerPostfix: " + ex.Message);
            }
        }

        /// <summary>
        /// Both lists start and stop together. There are two open sites and four close sites, so a
        /// per-site copy is how one of them ends up forgotten.
        /// </summary>
        private static void PanelOpened()
        {
            FriendListService.OnPanelOpened();
            FansListService.OnPanelOpened();
        }

        private static void PanelClosed()
        {
            FriendListService.OnPanelClosed();
            FansListService.OnPanelClosed();
        }

        public static void ClickFriendBtnPostfix(FriendPanel __instance)
        {
            if (OverlaySession.Degraded || __instance == null)
                return;

            try
            {
                var open = IsPanelOpen(__instance);
                MelonLogger.Msg("[FriendOverlay] clickFriendBtn open=" + open);

                if (open)
                {
                    // Begin already reapplies visibility over the SetActive(true) the game just did.
                    OverlaySession.Begin(__instance);
                    PanelOpened();
                    MelonLogger.Msg(
                        "[FriendOverlay] inRoom=" + GameProxies.IsInRoom() + " " + Compat.Capabilities.Summary);
                }
                else
                {
                    PanelClosed();
                    OverlaySession.End(restoreNativeLayer: false);
                }
            }
            catch (Exception ex)
            {
                OverlaySession.FailOpen(ex.Message);
            }
        }

        public static void ShowPostfix(GRUIElement __instance)
        {
            if (OverlaySession.Degraded)
                return;

            try
            {
                var panel = __instance.TryCast<FriendPanel>();
                if (panel == null)
                    return;

                // FriendPanel also owns the lobby friend button, so Show() fires when the HUD
                // appears. Only treat it as an open when the popup layer is actually up.
                if (!IsPanelOpen(panel))
                    return;

                // Re-entering Begin for a session we already own would reset the user's mode.
                // Compare with Unity's operator: two wrappers can point at the same native object.
                if (OverlaySession.Panel == panel)
                    return;

                OverlaySession.Begin(panel);
                PanelOpened();
            }
            catch (Exception ex)
            {
                OverlaySession.FailOpen(ex.Message);
            }
        }

        public static void HidePostfix(GRUIElement __instance)
        {
            try
            {
                if (__instance.TryCast<FriendPanel>() == null)
                    return;

                PanelClosed();
                OverlaySession.End(restoreNativeLayer: false);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[FriendOverlay] HidePostfix: " + ex.Message);
                OverlaySession.End(restoreNativeLayer: false);
            }
        }

        public static void ReleasePostfix(FriendPanel __instance)
        {
            try
            {
                PanelClosed();
                OverlaySession.End(restoreNativeLayer: false);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[FriendOverlay] ReleasePostfix: " + ex.Message);
            }
        }

        public static void ClosePostfix(FriendPanel __instance)
        {
            try
            {
                PanelClosed();
                OverlaySession.End(restoreNativeLayer: false);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[FriendOverlay] ClosePostfix: " + ex.Message);
            }
        }

        private static bool IsPanelOpen(FriendPanel panel)
        {
            try
            {
                if (panel.uiState == FriendPanel.UIState.Show)
                    return true;
                if (panel.uiState == FriendPanel.UIState.Hide || panel.uiState == FriendPanel.UIState.None)
                    return false;
            }
            catch
            {
                // fall through to panelLayer
            }

            try
            {
                var layer = panel.panelLayer;
                return layer != null && layer.activeSelf;
            }
            catch
            {
                return OverlaySession.Panel == null;
            }
        }
    }
}
