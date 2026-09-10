using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FriendOverlay.Core
{
    /// <summary>
    /// Ordered set of pinned user ids, capped so the pinned section can never swallow the list.
    /// Pure data: persistence and the game are the caller's business.
    /// </summary>
    public sealed class PinnedIds
    {
        public const int Max = 20;

        private readonly List<ulong> _ids = new List<ulong>();

        public int Count => _ids.Count;

        public bool IsFull => _ids.Count >= Max;

        public IReadOnlyList<ulong> Items => _ids;

        public bool Contains(ulong id) => _ids.Contains(id);

        public bool Add(ulong id)
        {
            if (id == 0 || IsFull || _ids.Contains(id))
                return false;

            _ids.Add(id);
            return true;
        }

        public bool Remove(ulong id) => _ids.Remove(id);

        /// <summary>
        /// Returns false when nothing changed, which happens only when pinning while full. Unpinning
        /// is checked first so a full set is never a trap the player cannot get out of.
        /// </summary>
        public bool Toggle(ulong id, out bool nowPinned)
        {
            if (Remove(id))
            {
                nowPinned = false;
                return true;
            }

            nowPinned = Add(id);
            return nowPinned;
        }

        public static PinnedIds Parse(string? csv)
        {
            var set = new PinnedIds();
            if (string.IsNullOrWhiteSpace(csv))
                return set;

            foreach (var part in csv!.Split(','))
            {
                if (ulong.TryParse(part.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var id))
                    set.Add(id);
            }

            return set;
        }

        public string ToCsv()
        {
            var sb = new StringBuilder();
            for (var i = 0; i < _ids.Count; i++)
            {
                if (i > 0)
                    sb.Append(',');
                sb.Append(_ids[i].ToString(CultureInfo.InvariantCulture));
            }

            return sb.ToString();
        }
    }
}
