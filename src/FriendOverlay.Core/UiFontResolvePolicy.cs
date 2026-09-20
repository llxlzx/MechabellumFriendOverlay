namespace FriendOverlay.Core
{
    public enum UiFontFaceKind
    {
        Skin = 0,
        Game = 1,
        System = 2,
        Noto = 3,
    }

    /// <summary>
    /// Pure ordering for which UI font face to prefer. Actual load happens in Melon UI.
    /// </summary>
    public static class UiFontResolvePolicy
    {
        /// <summary>
        /// Language-aware order. zh-CN: Noto → System(YaHei) → Game → Skin.
        /// Other codes: System → Noto → Game → Skin. When <paramref name="useGameFont"/>,
        /// Game wins first if available.
        /// </summary>
        public static UiFontFaceKind Choose(
            string languageCode,
            bool useGameFont,
            bool notoOk,
            bool systemOk,
            bool gameOk)
        {
            if (useGameFont)
            {
                if (gameOk) return UiFontFaceKind.Game;
                return ChooseWithoutGame(languageCode, notoOk, systemOk);
            }

            var primary = ChooseWithoutGame(languageCode, notoOk, systemOk);
            if (primary != UiFontFaceKind.Skin)
                return primary;

            return gameOk ? UiFontFaceKind.Game : UiFontFaceKind.Skin;
        }

        /// <summary>Legacy overload used by older call sites (zh-CN / Noto-first defaults).</summary>
        public static UiFontFaceKind Choose(bool useGameFont, bool notoOk, bool yaheiOk, bool gameOk) =>
            Choose("zh-CN", useGameFont, notoOk, yaheiOk, gameOk);

        private static UiFontFaceKind ChooseWithoutGame(string languageCode, bool notoOk, bool systemOk)
        {
            if (FontSelector.PreferEmbeddedNotoFirst(languageCode))
            {
                if (notoOk) return UiFontFaceKind.Noto;
                if (systemOk) return UiFontFaceKind.System;
                return UiFontFaceKind.Skin;
            }

            if (systemOk) return UiFontFaceKind.System;
            if (notoOk) return UiFontFaceKind.Noto;
            return UiFontFaceKind.Skin;
        }
    }
}
