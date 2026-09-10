using System;
using System.Collections.Generic;
using System.Linq;

namespace FriendOverlay.Core
{
    public static class FriendQueryPipeline
    {
        public static IReadOnlyList<FriendRowVm> Apply(
            IReadOnlyList<FriendRowVm> src,
            string query,
            FriendFilter filter,
            FriendSortKey sort)
        {
            if (src == null)
                throw new ArgumentNullException(nameof(src));

            IEnumerable<FriendRowVm> q = src;

            var trimmed = query?.Trim() ?? string.Empty;
            if (trimmed.Length > 0)
            {
                q = q.Where(r =>
                    (!string.IsNullOrEmpty(r.Name) &&
                     r.Name.IndexOf(trimmed, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    r.UserId.ToString().IndexOf(trimmed, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            switch (filter)
            {
                case FriendFilter.Online:
                    q = q.Where(r => r.IsOnline);
                    break;
                case FriendFilter.Offline:
                    q = q.Where(r => !r.IsOnline);
                    break;
                case FriendFilter.Mutual:
                    q = q.Where(r => r.IsMutual);
                    break;
                case FriendFilter.Busy:
                    q = q.Where(r => r.IsBusy);
                    break;
            }

            switch (sort)
            {
                case FriendSortKey.Name:
                    q = q.OrderBy(r => r.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase);
                    break;
                case FriendSortKey.RankPoint:
                    q = q.OrderByDescending(r => r.RankPoint)
                        .ThenByDescending(r => r.ForecastPoint)
                        .ThenBy(r => r.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase);
                    break;
                case FriendSortKey.ForecastPoint:
                    q = q.OrderByDescending(r => r.ForecastPoint)
                        .ThenByDescending(r => r.RankPoint)
                        .ThenBy(r => r.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase);
                    break;
                default:
                    q = q.OrderByDescending(r => r.IsOnline)
                        .ThenByDescending(r => r.IsMutual)
                        .ThenByDescending(r => r.RankPoint)
                        .ThenByDescending(r => r.ForecastPoint)
                        .ThenBy(r => r.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase);
                    break;
            }

            return q.ToList();
        }

        public static IReadOnlyList<FriendSection> GroupIntoSections(IReadOnlyList<FriendRowVm> rows) =>
            GroupIntoSections(rows, null);

        /// <summary>
        /// Splits an already filtered/sorted view into availability sections, preserving order
        /// inside each section. Pinned rows form their own leading section and are moved out of the
        /// availability sections, never copied. Empty sections are omitted.
        /// </summary>
        public static IReadOnlyList<FriendSection> GroupIntoSections(IReadOnlyList<FriendRowVm> rows, PinnedIds? pinned)
        {
            if (rows == null)
                throw new ArgumentNullException(nameof(rows));

            var pinnedRows = new List<FriendRowVm>();
            var joinable = new List<FriendRowVm>();
            var busy = new List<FriendRowVm>();
            var offline = new List<FriendRowVm>();

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null)
                    continue;

                if (pinned != null && pinned.Contains(row.UserId))
                    pinnedRows.Add(row);
                else if (!row.IsOnline)
                    offline.Add(row);
                else if (row.IsBusy)
                    busy.Add(row);
                else
                    joinable.Add(row);
            }

            var sections = new List<FriendSection>(4);
            if (pinnedRows.Count > 0)
                sections.Add(new FriendSection(FriendSectionKind.Pinned, pinnedRows));
            if (joinable.Count > 0)
                sections.Add(new FriendSection(FriendSectionKind.OnlineJoinable, joinable));
            if (busy.Count > 0)
                sections.Add(new FriendSection(FriendSectionKind.OnlineBusy, busy));
            if (offline.Count > 0)
                sections.Add(new FriendSection(FriendSectionKind.Offline, offline));

            return sections;
        }
    }
}
