using System;
using FriendOverlay.Core;
using FriendOverlay.State;
using Il2CppGameRiver.Client;
using MelonLoader;

namespace FriendOverlay.Actions
{
    public static class FriendActions
    {
        public static void Join(FriendRowVm row) => Safe(() =>
        {
            OverlaySession.Proxy?.RequestJoinUser(row.UserId, false);
        }, "Join");

        public static void Watch(FriendRowVm row) => Safe(() =>
        {
            OverlaySession.Proxy?.RequestJoinUser(row.UserId, true);
        }, "Watch");

        public static void Chat(FriendRowVm row) => Safe(() =>
        {
            OverlaySession.Proxy?.RequestPrivateChat(row.UserId, row.Name ?? string.Empty);
        }, "Chat");

        /// <summary>
        /// Mirrors the native FriendBtnListWindow invite button. Returns true only when a request
        /// actually went out, so the caller starts the 已邀请 window for real sends only.
        /// </summary>
        public static bool Invite(FriendRowVm row)
        {
            var lobby = Data.GameProxies.Lobby;
            if (lobby == null)
            {
                MelonLogger.Warning("[FriendOverlay] Invite: LobbyProxy unavailable");
                return false;
            }

            try
            {
                lobby.TryRequestInvite(row.UserId, row.Name ?? string.Empty, false);
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning(
                    "[FriendOverlay] TryRequestInvite failed, falling back to InviteUserJoin: " + ex.Message);
            }

            try
            {
                lobby.InviteUserJoin(row.UserId, false);
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[FriendOverlay] Invite: " + ex.Message);
                return false;
            }
        }

        /// <summary>Follow back someone in 关注我的人. Mirrors the native BeFollowCellNode follow button.</summary>
        public static void FollowBack(FriendRowVm row) => Safe(() =>
        {
            OverlaySession.Proxy?.RequestFollowUser(row.UserId);
            Data.FansListService.ForceRefresh();
            Data.FriendListService.ForceRefresh();
        }, "FollowBack");

        public static void Unfollow(FriendRowVm row) => Safe(() =>
        {
            OverlaySession.Proxy?.RequestCancelFollow(row.UserId);
            // A pin on someone no longer followed would keep an empty 置顶 slot forever.
            Data.PinStore.Remove(row.UserId);
            FriendOverlay.Data.FriendListService.ForceRefresh();
            Data.FansListService.ForceRefresh();
        }, "Unfollow");

        public static void Blacklist(FriendRowVm row) => Safe(() =>
        {
            var proxy = OverlaySession.Proxy;
            if (proxy == null)
                return;

            // BanPalyer is the entry point the native UI uses; it also persists the list and
            // raises the refresh notification. AddFriendBlackList alone skips both.
            try
            {
                proxy.BanPalyer(row.Name ?? string.Empty, row.UserId, row.FaceUrl ?? string.Empty);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[FriendOverlay] BanPalyer unavailable, falling back: " + ex.Message);
                proxy.AddFriendBlackList(new FriendBlackListConfig
                {
                    userid = row.UserId,
                    name = row.Name ?? string.Empty,
                    faceUrl = row.FaceUrl ?? string.Empty,
                });
            }

            Data.PinStore.Remove(row.UserId);
            FriendOverlay.Data.FriendListService.ForceRefresh();
            Data.FansListService.ForceRefresh();
        }, "Blacklist");

        /// <summary>
        /// Closes only the friends popup. Hiding the whole FriendPanel would also take the lobby
        /// friend button with it, because both live on the same GRUIElement.
        /// </summary>
        public static void ClosePanel()
        {
            var panel = OverlaySession.Panel;
            if (panel == null)
            {
                OverlaySession.End();
                return;
            }

            try
            {
                panel.ClosePanelBtnOnClicked();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[FriendOverlay] ClosePanel: " + ex.Message);
                try
                {
                    var layer = OverlaySession.PanelLayer;
                    OverlaySession.End();
                    if (layer != null)
                        layer.SetActive(false);
                }
                catch
                {
                    OverlaySession.End();
                }
                return;
            }

            // ClosePostfix normally ends the session; make sure a silent no-op close still does.
            if (OverlaySession.Panel != null)
                OverlaySession.End(restoreNativeLayer: false);
        }

        private static void Safe(Action action, string label)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[FriendOverlay] " + label + ": " + ex.Message);
            }
        }
    }
}
