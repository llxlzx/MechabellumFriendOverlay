namespace FriendOverlay.Core
{
    /// <summary>
    /// Shared clock math for official GIF avatars/frames drawn through IMGUI.
    /// </summary>
    public static class GifPlayback
    {
        public const float DefaultFrameSeconds = 1f / 12f;
        public const int MaxFrames = 32;

        public static int FrameIndex(double timeSeconds, float frameSeconds, int frameCount)
        {
            if (frameCount <= 0)
                return 0;

            var dt = frameSeconds > 0f ? (double)frameSeconds : DefaultFrameSeconds;
            // float frameSeconds (e.g. 0.1f) is not exact in double; nudge time so boundaries land on the next frame.
            var ticks = (long)System.Math.Floor((timeSeconds + 1e-6) / dt);
            var i = (int)(ticks % frameCount);
            if (i < 0)
                i += frameCount;
            return i;
        }

        public static int CapFrameCount(int count)
        {
            if (count <= 0)
                return 0;
            return count > MaxFrames ? MaxFrames : count;
        }
    }
}
