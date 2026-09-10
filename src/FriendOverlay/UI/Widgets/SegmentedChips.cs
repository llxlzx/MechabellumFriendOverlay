using FriendOverlay.Core;
using UnityEngine;

namespace FriendOverlay.UI.Widgets
{
    public static class SegmentedChips
    {
        private static readonly (FriendFilter Filter, string Label)[] Items =
        {
            (FriendFilter.All, "全部"),
            (FriendFilter.Online, "在线"),
            (FriendFilter.Busy, "忙碌"),
            (FriendFilter.Mutual, "互关"),
            (FriendFilter.Offline, "离线"),
        };

        /// <summary>Draws the filter strip left-aligned inside <paramref name="r"/>.</summary>
        public static FriendFilter Draw(Rect r, FriendFilter current)
        {
            var result = current;
            var chipW = Theme.S(58f);
            var gap = Theme.S(4f);
            var x = r.x;

            for (var i = 0; i < Items.Length; i++)
            {
                var item = Items[i];
                var rect = new Rect(x, r.y, chipW, r.height);
                if (Chip(rect, item.Label, item.Filter == current))
                    result = item.Filter;
                x += chipW + gap;
            }

            return result;
        }

        public static float Width => Items.Length * Theme.S(58f) + (Items.Length - 1) * Theme.S(4f);

        private static bool Chip(Rect r, string label, bool active)
        {
            var hovered = Gfx.Hover(r);
            var cut = Theme.S(5f);

            var fill = active ? Theme.AccentFaint : hovered ? Theme.ChipHover : Theme.Chip;
            Gfx.Chamfer(r, fill, cut);
            Gfx.ChamferBorder(r, active ? Theme.Accent : hovered ? Theme.Line : Theme.LineDim, cut);
            if (active)
                Gfx.Fill(new Rect(r.x + cut, r.yMax - Theme.S(2f), r.width - cut * 2f, Theme.S(2f)), Theme.Accent);

            Gfx.Text(r, label, active ? Theme.TextHi : hovered ? Theme.TextMain : Theme.TextMuted, Theme.Tab);
            return Gfx.Hit(r);
        }
    }
}
