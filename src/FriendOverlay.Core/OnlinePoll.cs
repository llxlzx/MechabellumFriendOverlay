using System;
using System.Collections.Generic;

namespace FriendOverlay.Core
{
    /// <summary>
    /// Picks which ids to ask presence for this tick. Asking about the whole followed list at once is
    /// what wedged the game on 2026-09-10: 211 ids per request every 2.5s produced a matching
    /// `tcp session write buffer too long[1914]` each time until the client stopped responding. So the
    /// list is walked in bounded chunks instead, and one full pass still covers everyone.
    /// </summary>
    public sealed class OnlinePoll
    {
        /// <summary>
        /// 32 ids is roughly 300 bytes on the wire. The rejected request carried 211 ids and reported
        /// 1914 bytes, so this keeps a wide margin while a full pass over 211 ids still finishes in
        /// seven requests.
        /// </summary>
        public const int DefaultChunkSize = 32;

        private readonly int _chunkSize;

        // Latched at the start of each pass. Requests are sized against this rather than against the
        // live list, so paging in more followers mid-pass cannot grow one pass without bound.
        private readonly List<ulong> _cycle = new List<ulong>();
        private readonly HashSet<ulong> _present = new HashSet<ulong>();

        private int _cursor;

        public OnlinePoll(int chunkSize)
        {
            _chunkSize = chunkSize < 1 ? 1 : chunkSize;
        }

        public int ChunkSize => _chunkSize;

        public void Reset()
        {
            _cycle.Clear();
            _cursor = 0;
        }

        /// <summary>
        /// The next slice to request, in list order. Ids that left the list since the pass started are
        /// skipped rather than asked about, and an exhausted pass restarts immediately so a tick is
        /// never spent on an empty request.
        /// </summary>
        public IList<ulong> Next(IReadOnlyList<ulong> ids)
        {
            var chunk = new List<ulong>();
            if (ids == null || ids.Count == 0)
            {
                Reset();
                return chunk;
            }

            _present.Clear();
            for (var i = 0; i < ids.Count; i++)
                _present.Add(ids[i]);

            if (_cursor >= _cycle.Count)
                Relatch(ids);

            Fill(chunk);

            // Everything left in this pass had gone away; start the next pass now instead of returning
            // nothing and waiting out the interval.
            if (chunk.Count == 0)
            {
                Relatch(ids);
                Fill(chunk);
            }

            return chunk;
        }

        private void Fill(List<ulong> chunk)
        {
            while (_cursor < _cycle.Count && chunk.Count < _chunkSize)
            {
                var id = _cycle[_cursor++];
                if (_present.Contains(id))
                    chunk.Add(id);
            }
        }

        private void Relatch(IReadOnlyList<ulong> ids)
        {
            _cycle.Clear();
            for (var i = 0; i < ids.Count; i++)
                _cycle.Add(ids[i]);

            _cursor = 0;
        }
    }
}
