using System;
using FriendOverlay.Core;
using UnityEngine;

namespace FriendOverlay.UI
{
    /// <summary>
    /// Palette, scale and GUIStyle cache. Styles may only be constructed inside OnGUI, so every
    /// draw entry point calls <see cref="EnsureStyles"/> first. All setters are guarded because
    /// IL2CPP strips parts of the GUIStyle surface on some builds.
    /// </summary>
    public static class Theme
    {
        public static readonly Color Bg0 = Hex(0x0A1322, 0.97f);
        public static readonly Color Bg1 = Hex(0x0E1A2E, 0.98f);

        /// <summary>
        /// Bg0's hue at full opacity. The chrome around the list is what hides rows that overflow the
        /// viewport, so it cannot be even slightly translucent, but it still has to match the window.
        /// </summary>
        public static readonly Color ChromeBg = Hex(0x0A1322, 1f);
        public static readonly Color Card = Hex(0x142440, 0.96f);
        public static readonly Color CardHover = Hex(0x1B3050, 0.98f);

        public static readonly Color Line = Hex(0x3E6CA8);
        public static readonly Color LineDim = Hex(0x22375A);
        public static readonly Color Accent = Hex(0x5AC8E0);
        public static readonly Color AccentSoft = Hex(0x5AC8E0, 0.35f);
        public static readonly Color AccentFaint = Hex(0x5AC8E0, 0.16f);

        public static readonly Color TextHi = Hex(0xF2F6FF);
        public static readonly Color TextMain = Hex(0xD6E1F5);
        public static readonly Color TextMuted = Hex(0x7F93B5);

        public static readonly Color StBattle = Hex(0xE8912A);
        public static readonly Color StIdle = Hex(0x3FD0A0);

        // Deliberately off the accent hue: while Pve shared Accent's cyan, a PvE row was
        // indistinguishable from every hovered or selected control in the panel.
        public static readonly Color StPve = Hex(0xB388FF);
        public static readonly Color StWaiting = Hex(0xD9C23A);
        public static readonly Color StOffline = Hex(0x5E6B82);

        public static readonly Color Danger = Hex(0xB8433F);
        public static readonly Color DangerHover = Hex(0xD4534E);
        public static readonly Color Steam = Hex(0x2A74C8);
        public static readonly Color WeGame = Hex(0xD9A33A);

        public static readonly Color TitleBar = Hex(0x10233D, 1f);
        public static readonly Color TitleBarHi = Hex(0x17355A, 1f);
        public static readonly Color Chip = Hex(0x172942, 1f);
        public static readonly Color ChipHover = Hex(0x21406A, 1f);

        /// <summary>0 means auto (derived from screen height).</summary>
        public static float UserScale { get; set; }

        public static bool StylesOk { get; private set; }

        public static GUIStyle? Title { get; private set; }
        public static GUIStyle? Tab { get; private set; }
        public static GUIStyle? Name { get; private set; }
        public static GUIStyle? Stat { get; private set; }
        public static GUIStyle? Meta { get; private set; }
        public static GUIStyle? MetaRight { get; private set; }
        public static GUIStyle? Button { get; private set; }
        public static GUIStyle? Section { get; private set; }
        public static GUIStyle? StatusRight { get; private set; }
        public static GUIStyle? Badge { get; private set; }
        public static GUIStyle? Avatar { get; private set; }
        public static GUIStyle? Invisible { get; private set; }

        private static float _builtScale = -1f;
        private static int _builtFontId = int.MinValue;
        private static bool _loggedFailure;

        public static float Scale
        {
            get
            {
                if (UserScale > 0.05f)
                    return Mathf.Clamp(UserScale, 0.75f, 3f);

                var h = Screen.height;
                if (h <= 0)
                    return 1f;

                return Mathf.Clamp(h / 1080f, 1f, 2f);
            }
        }

        public static float S(float px) => Mathf.Round(px * Scale);

        public static void EnsureStyles()
        {
            var scale = Scale;
            var font = GameAssets.UiFont;
            var fontId = font != null ? font.GetInstanceID() : 0;
            if (Title != null && Mathf.Approximately(_builtScale, scale) && fontId == _builtFontId)
                return;

            try
            {
                Title = Make(20, FontStyle.Bold, TextAnchor.MiddleLeft, scale);
                Tab = Make(15, FontStyle.Bold, TextAnchor.MiddleCenter, scale);
                Name = Make(16, FontStyle.Bold, TextAnchor.MiddleLeft, scale);
                Stat = Make(13, FontStyle.Normal, TextAnchor.MiddleLeft, scale);
                Meta = Make(12, FontStyle.Normal, TextAnchor.MiddleLeft, scale);
                MetaRight = Make(12, FontStyle.Normal, TextAnchor.MiddleRight, scale);
                Button = Make(13, FontStyle.Bold, TextAnchor.MiddleCenter, scale);
                Section = Make(13, FontStyle.Bold, TextAnchor.MiddleLeft, scale);
                StatusRight = Make(14, FontStyle.Bold, TextAnchor.MiddleRight, scale);
                Badge = Make(11, FontStyle.Bold, TextAnchor.MiddleCenter, scale);
                Avatar = Make(22, FontStyle.Bold, TextAnchor.MiddleCenter, scale);
                Invisible = Make(1, FontStyle.Normal, TextAnchor.MiddleCenter, scale);
                StylesOk = true;
            }
            catch (Exception ex)
            {
                StylesOk = false;
                Title = Tab = Name = Stat = Meta = MetaRight = null;
                Button = Section = StatusRight = Badge = Avatar = Invisible = null;
                LogFailureOnce("GUIStyle unavailable, falling back to default skin: " + ex.Message);
            }

            _builtScale = scale;
            _builtFontId = fontId;
        }

        public static Color StatusColor(FriendStatusKind kind) => kind switch
        {
            FriendStatusKind.Idle => StIdle,
            FriendStatusKind.Pve => StPve,
            FriendStatusKind.Waiting => StWaiting,
            FriendStatusKind.Battle => StBattle,
            _ => StOffline,
        };

        public static Color PlatformColor(int platform) => platform switch
        {
            1 => Steam,
            2 => WeGame,
            _ => LineDim,
        };

        private static GUIStyle Make(int size, FontStyle fontStyle, TextAnchor anchor, float scale)
        {
            var style = new GUIStyle();

            // Prefer GameAssets.UiFont (YaHei-first by default). Null inherits GUI.skin.font.
            Guard(() => style.font = GameAssets.UiFont);
            Guard(() => style.fontSize = Mathf.Max(1, Mathf.RoundToInt(size * scale)));
            Guard(() => style.fontStyle = fontStyle);
            Guard(() => style.alignment = anchor);
            Guard(() => style.wordWrap = false);
            Guard(() => style.clipping = TextClipping.Clip);
            Guard(() => style.richText = false);
            Guard(() => style.normal.textColor = Color.white);
            Guard(() => style.hover.textColor = Color.white);
            Guard(() => style.active.textColor = Color.white);
            Guard(() => style.padding = new RectOffset(0, 0, 0, 0));

            return style;
        }

        private static void Guard(Action set)
        {
            try
            {
                set();
            }
            catch (Exception ex)
            {
                LogFailureOnce("GUIStyle setter rejected: " + ex.Message);
            }
        }

        private static void LogFailureOnce(string message)
        {
            if (_loggedFailure)
                return;

            _loggedFailure = true;
            MelonLoader.MelonLogger.Warning("[FriendOverlay] " + message);
        }

        private static Color Hex(uint rgb, float a = 1f) => new Color(
            ((rgb >> 16) & 0xFF) / 255f,
            ((rgb >> 8) & 0xFF) / 255f,
            (rgb & 0xFF) / 255f,
            a);
    }
}
