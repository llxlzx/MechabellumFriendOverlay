using FriendOverlay.Core;
using UnityEngine;

namespace FriendOverlay.UI.Widgets
{
    public static class SegmentedChips
    {
        private static readonly FriendFilter[] Filters =
        {
            FriendFilter.All,
            FriendFilter.Online,
            FriendFilter.Busy,
            FriendFilter.Mutual,
            FriendFilter.Offline,
        };

        private static string LabelOf(FriendFilter filter) => filter switch
        {
            FriendFilter.All => L.T("filter.all"),
            FriendFilter.Online => L.T("filter.online"),
            FriendFilter.Busy => L.T("filter.busy"),
            FriendFilter.Mutual => L.T("filter.mutual"),
            _ => L.T("filter.offline"),
        };

        /// <summary>Draws the filter strip left-aligned inside <paramref name="r"/>.</summary>
        public static FriendFilter Draw(Rect r, FriendFilter current)
        {
            var result = current;
            var chipW = Theme.S(58f);
            var gap = Theme.S(4f);
            var x = r.x;

            for (var i = 0; i < Filters.Length; i++)
            {
                var filter = Filters[i];
                var rect = new Rect(x, r.y, chipW, r.height);
                if (Chip(rect, LabelOf(filter), filter == current))
                    result = filter;
                x += chipW + gap;
            }

            return result;
        }

        public static float Width => Filters.Length * Theme.S(58f) + (Filters.Length - 1) * Theme.S(4f);

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
