using System.Collections.Generic;

namespace FriendOverlay.Core
{
    public static class InviteRules
    {
        /// <summary>
        /// Invite is offered only when the game exposes it, the local player sits in a room and the
        /// friend is online. Callers render a disabled button otherwise, never hide it.
        /// </summary>
        public static bool CanInvite(bool capability, bool inRoom, bool online) =>
            capability && inRoom && online;
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
