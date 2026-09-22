namespace FriendOverlay.Core
{
    /// <summary>
    /// Steam stays "in game" until Mechabellum.exe actually exits. The game's
    /// OnApplicationWantsToQuit can accept the quit after the window is already gone
    /// and then never finish. These decisions force that process out, and leave a
    /// quit the game itself cancelled alone.
    /// </summary>
    public static class QuitStallGuard
    {
        /// <summary>
        /// Fuse if the quit callback never returns. The cancel path (give up / shutdown)
        /// returns quickly; this only covers a stuck callback.
        /// </summary>
        public const int StuckCallbackFuseMs = 12000;

        /// <summary>
        /// After the game has accepted quit, how long to wait for a normal process exit.
        /// </summary>
        public const int AcceptedQuitGraceMs = 4000;

        public readonly struct Decision
        {
            public Decision(bool forceExit, int delayMs)
            {
                ForceExit = forceExit;
                DelayMs = delayMs;
            }

            public bool ForceExit { get; }
            public int DelayMs { get; }
        }

        public static Decision OnEnter()
        {
            return new Decision(true, StuckCallbackFuseMs);
        }

        public static Decision OnReturned(bool quitAllowed)
        {
            if (!quitAllowed)
                return new Decision(false, 0);

            return new Decision(true, AcceptedQuitGraceMs);
        }
    }
}
