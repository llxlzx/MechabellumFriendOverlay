using System.Collections.Generic;

namespace FriendOverlay.Core
{
    /// <summary>
    /// What clicking 邀请 on a row should do. Disabled is 0 so a default-initialised RowContext fails
    /// closed rather than offering an invite the game cannot honour.
    /// </summary>
    public enum InvitePath
    {
        Disabled = 0,
        Direct = 1,
        Picker = 2,
    }

    public static class InviteRules
    {
        /// <summary>
        /// Mirrors the native button: already in a room invites directly, otherwise the battle-type
        /// picker creates one first. Callers render a disabled button rather than hiding it.
        /// <para>
        /// <paramref name="capability"/> means "inviting is possible right now", so the call site folds
        /// the live lobby lookup into it; a dead lobby needs no separate flag because it already forces
        /// both room flags false.
        /// </para>
        /// </summary>
        public static InvitePath Resolve(bool capability, bool canCreateRoom, bool inRoom, bool online, bool flowBusy)
        {
            if (!capability || !online || flowBusy)
                return InvitePath.Disabled;

            // Order matters: a player already in a room must not be offered a second one.
            if (inRoom)
                return InvitePath.Direct;

            return canCreateRoom ? InvitePath.Picker : InvitePath.Disabled;
        }
    }

    /// <summary>
    /// Per-row "已邀请" window so a double click cannot fire two invites. The clock is injected so
    /// tests never sleep; the UI passes Time.unscaledTime.
    /// </summary>
    public sealed class InviteCooldown
    {
        private readonly double _seconds;
        private readonly Dictionary<ulong, double> _markedAt = new Dictionary<ulong, double>();

        public InviteCooldown(double seconds)
        {
            _seconds = seconds;
        }

        public void Mark(ulong userId, double now) => _markedAt[userId] = now;

        public bool IsActive(ulong userId, double now)
        {
            if (!_markedAt.TryGetValue(userId, out var at))
                return false;

            var elapsed = now - at;

            // A clock that moved backwards is not "0 seconds ago"; treating it as active would wedge
            // the row on 已邀请 until the session ends.
            return elapsed >= 0.0 && elapsed < _seconds;
        }

        public void Clear() => _markedAt.Clear();
    }
}
