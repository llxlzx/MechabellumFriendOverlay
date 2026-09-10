using System;
using System.Reflection;
using HarmonyLib;
using Il2CppGameRiver.Client;
using MelonLoader;
using UnityEngine;

namespace FriendOverlay.Compat
{
    public sealed class TypeProbeResult
    {
        public bool Ok { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public static class TypeProbe
    {
        public static TypeProbeResult Probe()
        {
            try
            {
                var friendPanel = typeof(FriendPanel);
                var friendProxy = typeof(FriendProxy);
                var grui = typeof(GRUIElement);

                if (friendPanel == null || friendProxy == null || grui == null)
                    return Fail("Required types missing.");

                if (AccessTools.Property(friendPanel, "panelLayer") == null)
                    return Fail("FriendPanel.panelLayer missing.");

                if (AccessTools.Property(friendPanel, "friendProxy") == null)
                    return Fail("FriendPanel.friendProxy missing.");

                if (AccessTools.Method(friendPanel, "clickFriendBtn") == null)
                    return Fail("FriendPanel.clickFriendBtn missing.");

                if (AccessTools.Property(friendPanel, "uiState") == null)
                    return Fail("FriendPanel.uiState missing.");

                if (AccessTools.Method(friendPanel, "ClosePanelBtnOnClicked") == null)
                    return Fail("FriendPanel.ClosePanelBtnOnClicked missing.");

                if (AccessTools.Method(friendPanel, "Release") == null)
                    return Fail("FriendPanel.Release missing.");

                // Show/Hide are optional secondary hooks (friends UI primarily uses clickFriendBtn).
                _ = AccessTools.Method(grui, "Show");
                _ = AccessTools.Method(grui, "Hide");

                RequireMethod(friendProxy, "GetFriendBaseInfoList");
                RequireMethod(friendProxy, "GetTotalFollowCount");
                RequireMethod(friendProxy, "RequestFollowList");
                RequireMethod(friendProxy, "RequestFollowListNext");
                RequireMethod(friendProxy, "RequestFollowListNextPageIfFree");
                RequireMethod(friendProxy, "CanRequestFollowList");
                RequireMethod(friendProxy, "RequestOnline");
                RequireMethod(friendProxy, "RequestJoinUser");
                RequireMethod(friendProxy, "RequestPrivateChat");
                RequireMethod(friendProxy, "RequestCancelFollow");
                RequireMethod(friendProxy, "AddFriendBlackList");
                RequireMethod(friendProxy, "RefreshFriendList");

                // Touch Unity IMGUI entry used by overlay.
                _ = typeof(GUI);
                _ = typeof(GameObject);

                return new TypeProbeResult { Ok = true, Message = "TypeProbe OK" };
            }
            catch (Exception ex)
            {
                return Fail(ex.Message);
            }
        }

        private static void RequireMethod(Type type, string name)
        {
            if (AccessTools.Method(type, name) == null)
                throw new MissingMethodException(type.FullName + "." + name);
        }

        private static TypeProbeResult Fail(string message)
        {
            MelonLogger.Warning("[FriendOverlay] TypeProbe failed: " + message);
            return new TypeProbeResult { Ok = false, Message = message };
        }
    }
}
