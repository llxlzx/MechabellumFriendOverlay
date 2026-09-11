namespace FriendOverlay.Core
{
    public static class OverlayPerfSettings
    {
        public static bool AnimatedOfficialAvatars { get; set; }

        public static void ResetToDefaults()
        {
            AnimatedOfficialAvatars = false;
        }

        public static int TargetGifFrameCount(int spriteListCount)
        {
            var capped = GifPlayback.CapFrameCount(spriteListCount);
            if (!AnimatedOfficialAvatars)
                return capped <= 0 ? 0 : 1;
            return capped;
        }
    }
}
