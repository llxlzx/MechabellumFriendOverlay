using System;
using HarmonyLib;
using Il2CppGameRiver;
using Il2CppGameRiver.Client;
using MelonLoader;
using GameRiskInfo = Il2CppGameRiver.PlayerRiskInfo;

namespace FriendOverlay.Compat
{
    /// <summary>
    /// Optional game surfaces. Each flag gates exactly one feature; none of them can take the mod
    /// down. TypeProbe decides whether the overlay runs at all, this decides what it offers.
    /// </summary>
    public static class Capabilities
    {
        /// <summary>Inviting a friend into the room the player is already in.</summary>
        public static bool InviteUserJoin { get; private set; }

        /// <summary>Creating a room of a chosen battle type, which the picker path needs.</summary>
        public static bool CreateRoom { get; private set; }

        /// <summary>The 组队匹配 row. Separate probe so losing it cannot take the other two down.</summary>
        public static bool TeamInvite { get; private set; }

        public static bool Followers { get; private set; }
        public static bool Portrait { get; private set; }

        public static string Summary =>
            "inviteUserJoin=" + InviteUserJoin + " createRoom=" + CreateRoom + " teamInvite=" + TeamInvite +
            " followers=" + Followers + " portrait=" + Portrait;

        public static void Probe()
        {
            InviteUserJoin = Has(() =>
                AccessTools.Property(typeof(GameFacade), "Instance") != null &&
                AccessTools.Property(typeof(LobbyProxy), "JoinedRoom") != null &&
                AccessTools.Method(typeof(LobbyProxy), "InviteUserJoin", new[] { typeof(ulong), typeof(bool) }) != null);

            CreateRoom = Has(() =>
                InviteUserJoin &&
                AccessTools.Method(typeof(LobbyProxy), "IsHost") != null &&
                AccessTools.Method(
                    typeof(LobbyProxy),
                    "CreateRoom",
                    new[]
                    {
                        typeof(GameMode),
                        typeof(MatchMode),
                        typeof(bool),
                        typeof(MessageCenterV2.SessionResponse),
                    }) != null);

            TeamInvite = Has(() =>
                AccessTools.Method(typeof(TeamProxy), "RequestTeamInvite", new[] { typeof(ulong) }) != null);

            Followers = Has(() =>
                AccessTools.Property(typeof(FriendProxy), "followerBaseInfoList") != null &&
                AccessTools.Method(typeof(FriendProxy), "RequestLastFollower") != null &&
                AccessTools.Method(typeof(FriendProxy), "RequestFollowUser", new[] { typeof(ulong) }) != null);

            Portrait = Has(() =>
                AccessTools.Method(typeof(GameRiskInfo), "GetPortraitInfo") != null &&
                AccessTools.Method(typeof(GameRiskInfo), "GetPortrait") != null &&
                AccessTools.Method(typeof(PlayerPortraitInfo), "GetAvatarURL") != null &&
                AccessTools.Method(typeof(PlayerPortraitInfo), "GetAvatarOutLineURL") != null &&
                AccessTools.Method(typeof(PlayerPortraitInfo), "GetAvatarID") != null);

            MelonLogger.Msg("[FriendOverlay] capabilities " + Summary);
        }

        private static bool Has(Func<bool> check)
        {
            try
            {
                return check();
            }
            catch (Exception ex)
            {
                MelonLogger.Msg("[FriendOverlay] capability probe threw: " + ex.Message);
                return false;
            }
        }
    }
}
