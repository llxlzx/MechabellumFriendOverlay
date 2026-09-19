using System;

namespace FriendOverlay.Core
{
    /// <summary>
    /// Pure editor for the overlay search box. Unity IME and the clipboard stay outside this type.
    /// </summary>
    public static class SearchText
    {
        public const int MaxLength = 48;

        public static string Apply(string? current, string? typed = null, string? pasted = null)
        {
            var text = current ?? string.Empty;
            text = Consume(text, typed);
            text = Consume(text, pasted);
            return text;
        }

        private static string Consume(string text, string? incoming)
        {
            if (incoming is not { Length: > 0 } chars)
                return text;

            foreach (var ch in chars)
            {
                if (ch == '\b')
                {
                    if (text.Length > 0)
                        text = text.Substring(0, text.Length - 1);
                }
                else if (ch != '\n' && ch != '\r' && !char.IsControl(ch))
                {
                    if (text.Length < MaxLength)
                        text += ch;
                }
            }

            return text;
        }

        /// <summary>
        /// One input frame. An open composition is display-only; pinyin in
        /// <paramref name="inputString"/> must not land in the committed text.
        /// When composition ends, commit the previous string unless this frame's
        /// keys already appended that same suffix.
        /// </summary>
        public static (string Text, string Composition) Advance(
            string? committed,
            string? previousComposition,
            string? compositionNow,
            string? inputString)
        {
            var text = committed ?? string.Empty;
            var previous = previousComposition ?? string.Empty;
            var now = compositionNow ?? string.Empty;

            if (now.Length > 0)
                return (text, now);

            if (previous.Length > 0)
            {
                var viaKeys = Apply(text, inputString);
                if (AlreadyHasSuffix(text, previous, viaKeys))
                    return (viaKeys, string.Empty);
                return (Apply(text, previous), string.Empty);
            }

            return (Apply(text, inputString), string.Empty);
        }

        private static bool AlreadyHasSuffix(string text, string suffix, string viaKeys) =>
            viaKeys.Length == text.Length + suffix.Length
            && viaKeys.StartsWith(text, StringComparison.Ordinal)
            && viaKeys.EndsWith(suffix, StringComparison.Ordinal);
    }
}
