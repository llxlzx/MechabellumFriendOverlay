namespace FriendOverlay.Core
{
    /// <summary>
    /// Whether a null bake result should burn SharedImageCache SoftAttempts.
    /// Budget deferrals and GIF settle Pending must not count — otherwise Af_/Avtr_ keys
    /// Fail before they ever win a BakeBudget slot.
    /// </summary>
    public static class SharedImageLoadMiss
    {
        public static bool ShouldCountSoftMiss(bool hasFrames, bool isPending, bool budgetDeferred)
        {
            if (hasFrames)
                return false;
            if (isPending)
                return false;
            if (budgetDeferred)
                return false;
            return true;
        }
    }
}
