namespace FriendOverlay.Core
{
    public sealed class FriendRowVm
    {
        public ulong UserId { get; set; }
        public string Name { get; set; } = string.Empty;

        /// <summary>Raw custom-photo URL from the proto. Kept for blacklist config parity.</summary>
        public string FaceUrl { get; set; } = string.Empty;

        /// <summary>The game itself hides this friend's face.</summary>
        public bool FaceBlocked { get; set; }

        /// <summary>What the game would draw for this player, as decided by <see cref="PortraitPlanner"/>.</summary>
        public PortraitKind Portrait { get; set; } = PortraitKind.Letter;

        /// <summary>URL or sprite path for <see cref="Portrait"/>; empty for Letter.</summary>
        public string PortraitRef { get; set; } = string.Empty;

        /// <summary>URL or sprite path of the avatar frame; empty when none.</summary>
        public string FrameRef { get; set; } = string.Empty;

        public int RankPoint { get; set; }
        public int ForecastPoint { get; set; }
        public int AsyncPoint1V1 { get; set; }
        public int AsyncPoint2V2 { get; set; }
        public int State { get; set; }
        public bool IsMutual { get; set; }
        public bool IsOnline { get; set; }
        public bool IsBusy { get; set; }

        /// <summary>
        /// False when no one reported this player's state and <see cref="State"/> is a safe
        /// substitute. Actions that need a live player stay disabled while this is false.
        /// </summary>
        public bool StateKnown { get; set; } = true;
        public int Platform { get; set; }
        public string StatusLabel { get; set; } = string.Empty;
        public FriendStatusKind StatusKind { get; set; } = FriendStatusKind.Offline;
    }
}
