using System;
using FriendOverlay.Core;
using FriendOverlay.State;
using MelonLoader;

namespace FriendOverlay.Data
{
    /// <summary>
    /// Preference-backed pin set. Purely local: the game server never learns about pins, because the
    /// game has no such field. Every change persists immediately so a crash cannot lose a fresh pin.
    /// </summary>
    public static class PinStore
    {
        private static PinnedIds _pins = new PinnedIds();

        public static PinnedIds Pins => _pins;

        public static string Csv => _pins.ToCsv();

        public static bool IsFull => _pins.IsFull;

        public static void Load(string? csv)
        {
            _pins = PinnedIds.Parse(csv);
            MelonLogger.Msg("[FriendOverlay] pins loaded: " + _pins.Count);
        }

        public static bool Contains(ulong userId) => _pins.Contains(userId);

        /// <summary>Returns true when the set changed (false when pinning while full).</summary>
        public static bool Toggle(ulong userId)
        {
            if (!_pins.Toggle(userId, out _))
                return false;

            Persist();
            return true;
        }

        public static void Remove(ulong userId)
        {
            if (_pins.Remove(userId))
                Persist();
        }

        private static void Persist()
        {
            try
            {
                OverlaySession.PersistPreferences?.Invoke();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[FriendOverlay] pin persist: " + ex.Message);
            }
        }
    }
}
