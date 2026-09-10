namespace FriendOverlay.Core
{
    /// <summary>
    /// A player's presence plus whether anyone actually told us about it. The game's state enum has
    /// Idle = 0, so a struct field nobody filled in reads as "idle and joinable"; a list that cannot
    /// vouch for its own State field must pass null rather than let that default through.
    /// </summary>
    public readonly struct Presence
    {
        public Presence(int state, bool known)
        {
            State = state;
            Known = known;
        }

        public int State { get; }

        /// <summary>False when the state is a safe substitute, not something the server reported.</summary>
        public bool Known { get; }

        /// <param name="reported">State from the live presence dictionary, or null when absent.</param>
        /// <param name="listed">State carried by the list entry, or null when the caller does not trust it.</param>
        /// <param name="offlineState">The value to substitute when nothing is known.</param>
        public static Presence Resolve(int? reported, int? listed, int offlineState)
        {
            if (reported.HasValue)
                return new Presence(reported.Value, true);

            if (listed.HasValue)
                return new Presence(listed.Value, true);

            return new Presence(offlineState, false);
        }
    }
}
