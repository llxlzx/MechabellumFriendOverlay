using System;
using System.Collections.Generic;

namespace FriendOverlay.Core
{
    public static class VisibleRowRange
    {
        public static (int startInclusive, int endExclusive) Compute(
            float scrollY,
            float viewportHeight,
            IReadOnlyList<float> itemOffsets,
            IReadOnlyList<float> itemHeights,
            int marginItems)
        {
            if (itemOffsets == null || itemHeights == null)
                return (0, 0);
            var n = Math.Min(itemOffsets.Count, itemHeights.Count);
            if (n <= 0)
                return (0, 0);

            var viewTop = scrollY;
            var viewBottom = scrollY + Math.Max(0f, viewportHeight);
            var first = -1;
            var last = -1;
            for (var i = 0; i < n; i++)
            {
                var top = itemOffsets[i];
                var bottom = top + itemHeights[i];
                var visible = bottom > viewTop && top < viewBottom;
                if (!visible && i == n - 1 && top >= viewBottom && top < viewBottom + itemHeights[i] && bottom > viewBottom)
                    visible = true;
                if (!visible)
                    continue;
                if (first < 0)
                    first = i;
                last = i;
            }

            if (first < 0)
                return (0, 0);

            var m = marginItems < 0 ? 0 : marginItems;
            var start = Math.Max(0, first - m);
            var end = Math.Min(n, last + 1 + m);
            return (start, end);
        }
    }
}
