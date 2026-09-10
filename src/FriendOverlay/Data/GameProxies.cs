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
