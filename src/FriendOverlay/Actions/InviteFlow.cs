using System;
using FriendOverlay.Core;
using Il2CppGameRiver;
using Il2CppGameRiver.Client;
using Il2CppInterop.Runtime;
using MelonLoader;
using UnityEngine;

namespace FriendOverlay.Actions
{
    /// <summary>
    /// The picker path: create a room of the chosen battle type, wait for it, then invite. One flow at a
    /// time globally, and it deliberately outlives both the picker and the overlay panel — a player who
    /// closes either one still expects the invite they asked for to arrive.
    /// </summary>
    public static class InviteFlow
    {
        /// <summary>
        /// Invite rooms are created private, matching what the native invite window does. Confirm from
        /// the isPrivate column of the trace table before changing this.
        /// </summary>
        private const bool RoomIsPrivate = true;

        private static readonly PendingInviteState _state =
            new PendingInviteState(PendingInviteState.DefaultTimeoutSeconds);

        // Rooted in a static field on purpose: a locally converted delegate can be collected while the
        // game still holds the native pointer, and the callback then lands in freed memory.
        private static readonly MessageCenterV2.SessionResponse? CreateResponse =
            DelegateSupport.ConvertDelegate<MessageCenterV2.SessionResponse>((Func<int, bool>)OnCreateResponse);

        // Captured at Start so the flow keeps talking to the proxy it created the room on, even if the
        // resolver hands out a different instance later.
        private static LobbyProxy? _lobby;

        /// <summary>Raised on the frame the invite actually goes out, carrying the target user id.</summary>
        public static Action<ulong>? OnSent;

        public static bool Busy => _state.IsBusy;

        public static ulong PendingUserId => _state.PendingUserId;

        /// <summary>
        /// Returns false when nothing was started, in which case the caller must not show any pending
        /// state. Unverified battle types are refused here rather than in the UI, so a guessed mode can
        /// never reach CreateRoom.
        /// </summary>
        public static bool Start(ulong userId, BattleType type)
        {
            if (type == null || type.IsTeamMatch || !type.Verified)
                return false;

            if (!Compat.Capabilities.CreateRoom)
                return false;

            // The game would take a null callback into native code; refuse before anything is claimed.
            if (CreateResponse == null)
            {
                MelonLogger.Warning("[FriendOverlay] InviteFlow: create-room callback unavailable");
                return false;
            }

            var lobby = Data.GameProxies.Lobby;
            if (lobby == null)
            {
                MelonLogger.Warning("[FriendOverlay] InviteFlow: LobbyProxy unavailable");
                return false;
            }

            bool roomPresent;
            try
            {
                roomPresent = lobby.JoinedRoom != null;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[FriendOverlay] InviteFlow: cannot read JoinedRoom: " + ex.Message);
                return false;
            }

            // Claim the mutex before the side effect, otherwise a double click fires two CreateRoom
            // calls before anything is marked busy.
            if (!_state.Begin(userId, Time.unscaledTime, roomPresent))
                return false;

            _lobby = lobby;

            try
            {
                lobby.CreateRoom((GameMode)type.GameMode, (MatchMode)type.MatchMode, RoomIsPrivate, CreateResponse);
                MelonLogger.Msg("[FriendOverlay] InviteFlow: creating " + type.Label + " room for " + userId);
                return true;
            }
            catch (Exception ex)
            {
                _state.Reset();
                _lobby = null;
                MelonLogger.Warning("[FriendOverlay] InviteFlow: CreateRoom failed: " + ex.Message);
                return false;
            }
        }

        /// <summary>Driven from OnUpdate, not from the panel, so closing the overlay cannot stall it.</summary>
        public static void Tick()
        {
            if (!_state.IsBusy)
                return;

            var roomPresent = false;
            var weAreHost = false;

            try
            {
                var lobby = _lobby;
                if (lobby != null)
                {
                    roomPresent = lobby.JoinedRoom != null;
                    if (roomPresent)
                        weAreHost = lobby.IsHost();
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[FriendOverlay] InviteFlow: lobby read failed: " + ex.Message);
            }

            var step = _state.Tick(Time.unscaledTime, roomPresent, weAreHost, out var userId);

            if (step == InviteStep.SendInvite)
            {
                _lobby = null;
                if (FriendActions.InviteUserJoin(userId))
                {
                    MelonLogger.Msg("[FriendOverlay] InviteFlow: invited " + userId + " into our room");
                    OnSent?.Invoke(userId);
                }
            }
            else if (step == InviteStep.TimedOut)
            {
                _lobby = null;
                MelonLogger.Warning(
                    "[FriendOverlay] InviteFlow: gave up waiting for our room, invite to " + userId + " dropped");
            }
        }

        /// <summary>Session teardown must not touch this; only a hard reset of the mod should.</summary>
        public static void Reset()
        {
            _state.Reset();
            _lobby = null;
        }

        /// <summary>
        /// The server answered the create request. A non-zero code means no room is coming, so the row
        /// recovers now instead of sitting out the full timeout. False leaves the game's own error
        /// handling in place.
        /// </summary>
        private static bool OnCreateResponse(int errorCode)
        {
            try
            {
                if (errorCode == 0)
                    return false;

                if (_state.Fail())
                {
                    _lobby = null;
                    MelonLogger.Warning("[FriendOverlay] InviteFlow: CreateRoom refused, code=" + errorCode);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[FriendOverlay] InviteFlow: response handler: " + ex.Message);
            }

            return false;
        }
    }
}
