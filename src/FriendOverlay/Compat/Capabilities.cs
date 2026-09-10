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
        public static bool Invite { get; private set; }
        public static bool Followers { get; private set; }
        public static bool Portrait { get; private set; }

        public static string Summary =>
            "invite=" + Invite + " followers=" + Followers + " portrait=" + Portrait;

        public static void Probe()
        {
            Invite = Has(() =>
                AccessTools.Property(typeof(GameFacade), "Instance") != null &&
                AccessTools.Property(typeof(LobbyProxy), "JoinedRoom") != null &&
                (AccessTools.Method(typeof(LobbyProxy), "TryRequestInvite", new[] { typeof(ulong), typeof(string), typeof(bool) }) != null ||
                 AccessTools.Method(typeof(LobbyProxy), "InviteUserJoin", new[] { typeof(ulong), typeof(bool) }) != null));

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
