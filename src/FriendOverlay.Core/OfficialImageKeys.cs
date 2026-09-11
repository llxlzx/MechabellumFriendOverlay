using System;

namespace FriendOverlay.Core
{
    /// <summary>
    /// Exact-only GIF / outline key helpers. Prefix matching previously could bind Avtr_G_02-08A
    /// to Avtr_G_02-08B after stripping the letter.
    /// </summary>
    public static class OfficialImageKeys
    {
        public static bool IsLikelyGifKey(string imageRef) =>
            !string.IsNullOrEmpty(imageRef) &&
            imageRef.IndexOf("_G_", StringComparison.OrdinalIgnoreCase) >= 0;

        public static bool IsOutlineKey(string imageRef) =>
            !string.IsNullOrEmpty(imageRef) &&
            imageRef.StartsWith("Af_", StringComparison.OrdinalIgnoreCase);

        /// <summary>Yields the raw key, then the trailing A–Z letter stripped once (exact only).</summary>
        public static System.Collections.Generic.IEnumerable<string> ExactVariants(string imageRef)
        {
            if (string.IsNullOrEmpty(imageRef))
                yield break;

            yield return imageRef;

            if (imageRef.Length < 2)
                yield break;

            var last = imageRef[imageRef.Length - 1];
            if ((last >= 'A' && last <= 'Z') || (last >= 'a' && last <= 'z'))
                yield return imageRef.Substring(0, imageRef.Length - 1);
        }

        public static bool ExactEquals(string a, string b) =>
            string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
