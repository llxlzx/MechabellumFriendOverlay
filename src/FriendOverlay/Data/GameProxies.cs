using System;
using Il2CppGameRiver.Client;
using MelonLoader;
using UnityEngine;

namespace FriendOverlay.Data
{
    /// <summary>
    /// Locates proxies the FriendPanel does not hand us. Lookups are retried on a timer rather than
    /// memoized as a permanent failure, because a proxy can be registered after the panel opens.
    /// </summary>
    public static class GameProxies
    {
        private const float RetrySeconds = 2f;

        private static LobbyProxy? _lobby;
        private static float _nextLobbyTry;
        private static bool _loggedLobbyMiss;

        private static TeamProxy? _team;
        private static float _nextTeamTry;
        private static bool _loggedTeamMiss;

        public static LobbyProxy? Lobby
        {
            get
            {
                if (_lobby != null)
                    return _lobby;

                // Called per row per frame from the invite gate, so a missing proxy must never turn
                // into a facade lookup per row.
                if (Time.unscaledTime < _nextLobbyTry)
                    return null;

                _nextLobbyTry = Time.unscaledTime + RetrySeconds;
                _lobby = ResolveLobby();
                return _lobby;
            }
        }

        public static TeamProxy? Team
        {
            get
            {
                if (_team != null)
                    return _team;

                if (Time.unscaledTime < _nextTeamTry)
                    return null;

                _nextTeamTry = Time.unscaledTime + RetrySeconds;
                _team = ResolveTeam();
                return _team;
            }
        }

        /// <summary>True only while the local player sits in a room, which is what invite targets.</summary>
        public static bool IsInRoom()
        {
            var lobby = Lobby;
            if (lobby == null)
                return false;

            try
            {
                return lobby.JoinedRoom != null;
            }
            catch
            {
                return false;
            }
        }

        public static void Reset()
        {
            _lobby = null;
            _nextLobbyTry = 0f;
            _loggedLobbyMiss = false;
            _team = null;
            _nextTeamTry = 0f;
            _loggedTeamMiss = false;
        }

        private static TeamProxy? ResolveTeam()
        {
            try
            {
                var facade = GameFacade.Instance;
                if (facade == null)
                    return MissTeam("GameFacade.Instance is null");

                TeamProxy? proxy = null;

                try
                {
                    TeamProxy? found = null;
                    if (facade.TryRetrieveProxy<TeamProxy>(out found))
                        proxy = found;
                }
                catch (Exception ex)
                {
                    MelonLogger.Msg("[FriendOverlay] TryRetrieveProxy<TeamProxy> unavailable: " + ex.Message);
                }

                if (proxy == null)
                {
                    try
                    {
                        proxy = facade.RetrieveProxy<TeamProxy>(Proxy<TeamProxy>.NAME);
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Msg("[FriendOverlay] RetrieveProxy<TeamProxy>(NAME) unavailable: " + ex.Message);
                    }
                }

                if (proxy == null)
                    return MissTeam("TeamProxy not registered");

                MelonLogger.Msg("[FriendOverlay] TeamProxy resolved");
                _loggedTeamMiss = false;
                return proxy;
            }
            catch (Exception ex)
            {
                return MissTeam(ex.Message);
            }
        }

        private static TeamProxy? MissTeam(string why)
        {
            if (!_loggedTeamMiss)
            {
                _loggedTeamMiss = true;
                MelonLogger.Warning("[FriendOverlay] TeamProxy unavailable, 组队匹配 disabled for now: " + why);
            }

            return null;
        }

        private static LobbyProxy? ResolveLobby()
        {
            try
            {
                var facade = GameFacade.Instance;
                if (facade == null)
                    return Miss("GameFacade.Instance is null");

                LobbyProxy? proxy = null;

                try
                {
                    LobbyProxy? found = null;
                    if (facade.TryRetrieveProxy<LobbyProxy>(out found))
                        proxy = found;
                }
                catch (Exception ex)
                {
                    MelonLogger.Msg("[FriendOverlay] TryRetrieveProxy<LobbyProxy> unavailable: " + ex.Message);
                }

                if (proxy == null)
                {
                    // Il2CppInterop can fail to instantiate the generic; the named overload is the
                    // same lookup with the PureMVC default proxy name supplied explicitly.
                    try
                    {
                        proxy = facade.RetrieveProxy<LobbyProxy>(Proxy<LobbyProxy>.NAME);
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Msg("[FriendOverlay] RetrieveProxy<LobbyProxy>(NAME) unavailable: " + ex.Message);
                    }
                }

                if (proxy == null)
                    return Miss("LobbyProxy not registered");

                MelonLogger.Msg("[FriendOverlay] LobbyProxy resolved");
                _loggedLobbyMiss = false;
                return proxy;
            }
            catch (Exception ex)
            {
                return Miss(ex.Message);
            }
        }

        private static LobbyProxy? Miss(string why)
        {
            if (!_loggedLobbyMiss)
            {
                _loggedLobbyMiss = true;
                MelonLogger.Warning("[FriendOverlay] LobbyProxy unavailable, invite disabled for now: " + why);
            }

            return null;
        }
    }
}
