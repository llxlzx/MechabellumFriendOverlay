using System;

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
        /// Written by the game only on the path that accepts quit, immediately before it can stall.
        /// </summary>
        public const string QuitMarker = "OnApplicationWantsToQuit";

        /// <summary>
        /// No delay. A sleep after the marker lets the quit callback freeze managed
        /// threads, including the watcher, so the process never gets ended.
        /// </summary>
        public const int AcceptedQuitGraceMs = 0;

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

        public static bool TailHasQuitMarker(byte[] data, int count)
        {
            if (data == null || count <= 0)
                return false;

            var marker = System.Text.Encoding.ASCII.GetBytes(QuitMarker);
            var limit = Math.Min(count, data.Length) - marker.Length;
            for (var i = 0; i <= limit; i++)
            {
                var match = true;
                for (var j = 0; j < marker.Length; j++)
                {
                    if (data[i + j] != marker[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                    return true;
            }

            return false;
        }
    }
}
