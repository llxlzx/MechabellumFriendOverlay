using System;

namespace FriendOverlay.Core
{
    /// <summary>What the caller must do this frame. None is 0 so a default value does nothing.</summary>
    public enum InviteStep
    {
        None = 0,
        SendInvite = 1,
        TimedOut = 2,
    }

    /// <summary>
    /// Drives 建房 → 等房间 → 邀请 for the picker path. Pure state so the timings are testable without a
    /// running game; the caller injects the clock and what it can see of the lobby.
    /// </summary>
    public sealed class PendingInviteState
    {
        public const double DefaultTimeoutSeconds = 10.0;

        private readonly double _timeoutSeconds;
        private bool _busy;
        private ulong _userId;
        private double _startedAt;

        public PendingInviteState(double timeoutSeconds)
        {
            _timeoutSeconds = timeoutSeconds;
        }

        /// <summary>A single global mutex: one create-room flow at a time, whoever the target is.</summary>
        public bool IsBusy => _busy;

        /// <summary>0 while idle.</summary>
        public ulong PendingUserId => _busy ? _userId : 0UL;

        /// <summary>
        /// Must be called before the CreateRoom side effect, not after: a double click would otherwise
        /// fire two create requests before the mutex closes.
        /// </summary>
        public bool Begin(ulong userId, double now, bool roomPresent)
        {
            // A room already there makes the "no room, then our room" transition unobservable, and 0 is
            // the sentinel Tick reports for "nobody", so neither can be accepted.
            if (userId == 0UL || _busy || roomPresent)
                return false;

            _busy = true;
            _userId = userId;
            _startedAt = now;
            return true;
        }

        /// <summary>
        /// <paramref name="weAreHost"/> is what separates the room we just made from one the player
        /// landed in some other way mid-flow; inviting into the latter would drag the friend somewhere
        /// unrelated, so it is treated as still waiting.
        /// </summary>
        public InviteStep Tick(double now, bool roomPresent, bool weAreHost, out ulong userId)
        {
            if (!_busy)
            {
                userId = 0UL;
                return InviteStep.None;
            }

            // Success is checked before the clock, so a backwards jump cannot discard a real room.
            if (roomPresent && weAreHost)
            {
                userId = _userId;
                Clear();
                return InviteStep.SendInvite;
            }

            var elapsed = now - _startedAt;

            // Backwards clock releases the mutex rather than holding it: a wedged mutex would kill the
            // invite button for every row until the game restarts.
            if (elapsed >= _timeoutSeconds || elapsed < 0.0)
            {
                userId = _userId;
                Clear();
                return InviteStep.TimedOut;
            }

            userId = _userId;
            return InviteStep.None;
        }

        /// <summary>
        /// The create-room callback reported a server refusal, so no room is coming. Returns whether a
        /// flow was actually dropped, which is what the caller logs.
        /// </summary>
        public bool Fail()
        {
            if (!_busy)
                return false;

            Clear();
            return true;
        }

        public void Reset() => Clear();

        private void Clear()
        {
            _busy = false;
            _userId = 0UL;
            _startedAt = 0.0;
        }
    }
}
