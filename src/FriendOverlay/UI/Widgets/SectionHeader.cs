using FriendOverlay.Core;
using UnityEngine;

namespace FriendOverlay.UI.Widgets
{
    public static class SectionHeader
    {
        public static float Height => Theme.S(30f);

        public static string TitleOf(FriendSectionKind kind) => kind switch
        {
            FriendSectionKind.Pinned => "置顶",
            FriendSectionKind.OnlineJoinable => "在线 · 可加入",
            FriendSectionKind.OnlineBusy => "在线 · 对战中",
            _ => "离线",
        };

        public static Color ColorOf(FriendSectionKind kind) => kind switch
        {
            FriendSectionKind.Pinned => Theme.Accent,
            FriendSectionKind.OnlineJoinable => Theme.StIdle,
            FriendSectionKind.OnlineBusy => Theme.StBattle,
            _ => Theme.StOffline,
        };

        /// <summary>Returns true when the header was clicked (collapse toggle).</summary>
        public static bool Draw(Rect r, FriendSectionKind kind, int count, bool collapsed, bool interactive)
        {
            var accent = ColorOf(kind);
            var hovered = interactive && Gfx.Hover(r);

            var arrowR = new Rect(r.x, r.y, Theme.S(18f), r.height);
            Gfx.Text(arrowR, collapsed ? "▸" : "▾", hovered ? Theme.TextHi : Theme.TextMuted, Theme.Badge);

            var title = TitleOf(kind);
            var titleR = new Rect(arrowR.xMax, r.y, Theme.S(120f), r.height);
            Gfx.Text(titleR, title, hovered ? Theme.TextHi : accent, Theme.Section);

            var badgeW = Theme.S(38f);
            var badgeR = new Rect(titleR.xMax + Theme.S(4f), r.y + Theme.S(6f), badgeW, r.height - Theme.S(12f));
            Gfx.RoundRect(badgeR, new Color(accent.r, accent.g, accent.b, 0.18f), badgeR.height * 0.5f);
            Gfx.Text(badgeR, count.ToString(), accent, Theme.Badge);

            var lineY = r.y + r.height * 0.5f;
            var lineX = badgeR.xMax + Theme.S(10f);
            if (r.xMax - lineX > 0f)
            {
                Gfx.FadeLine(
                    new Rect(lineX, lineY, r.xMax - lineX, 1f),
                    new Color(accent.r, accent.g, accent.b, 0.45f));
            }

            return interactive && Gfx.Hit(r);
        }
    }
}
