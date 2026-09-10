namespace FriendOverlay.Core
{
    public enum PortraitKind
    {
        /// <summary>Nothing to load; draw the initial only.</summary>
        Letter = 0,

        /// <summary>Custom photo URL; per-user download.</summary>
        Photo = 1,

        /// <summary>Official in-game avatar asset; shared across players.</summary>
        Official = 2,

        /// <summary>Game hides this face; show the placeholder the game substituted.</summary>
        Blocked = 3,
    }

    public sealed class PortraitPlan
    {
        public PortraitPlan(PortraitKind kind, string imageRef, string frameRef)
        {
            Kind = kind;
            ImageRef = imageRef ?? string.Empty;
            FrameRef = frameRef ?? string.Empty;
        }

        public PortraitKind Kind { get; }

        /// <summary>URL or sprite path of the main image; empty for Letter.</summary>
        public string ImageRef { get; }

        /// <summary>URL or sprite path of the avatar frame; empty when the player has none.</summary>
        public string FrameRef { get; }
    }

    /// <summary>
    /// Picks the image the game itself would draw, which is whatever the player selected in game.
    ///
    /// The game only reports a non-empty avatar URL when that player chose an official avatar, so an
    /// avatar URL is the player's own choice rather than a preference of ours; a player on a custom
    /// photo reports no avatar and gets the photo. This mirrors
    /// GRImage.SetPlayerPortrait(portraitURL, outline, avatar), where the avatar layer sits above the
    /// photo layer. Moderation only touches the photo layer, and the game has already swapped that
    /// URL for its placeholder before we read it, so a blocked row simply shows the substitution.
    /// </summary>
    public static class PortraitPlanner
    {
        public static PortraitPlan Decide(bool blocked, string? portrait, string? avatarUrl, string? outlineUrl)
        {
            var frame = Clean(outlineUrl);

            var avatar = Clean(avatarUrl);
            if (avatar.Length > 0)
                return new PortraitPlan(PortraitKind.Official, avatar, frame);

            var photo = Clean(portrait);
            if (photo.Length == 0)
                return new PortraitPlan(PortraitKind.Letter, string.Empty, frame);

            return new PortraitPlan(blocked ? PortraitKind.Blocked : PortraitKind.Photo, photo, frame);
        }

        /// <summary>
        /// The game reports "unset" as null, empty, or whitespace depending on the field. Treating a
        /// blank string as a loadable reference is what produces an empty box instead of a letter.
        /// </summary>
        private static string Clean(string? value) =>
            string.IsNullOrWhiteSpace(value) ? string.Empty : value!.Trim();
    }
}
