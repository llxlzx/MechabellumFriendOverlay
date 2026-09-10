using System;
using System.Collections.Generic;

namespace FriendOverlay.Core
{
    public enum FriendSectionKind
    {
        OnlineJoinable = 0,
        OnlineBusy = 1,
        Offline = 2,
        Pinned = 3,
    }

    public sealed class FriendSection
    {
        public FriendSection(FriendSectionKind kind, IReadOnlyList<FriendRowVm> rows)
        {
            Kind = kind;
            Rows = rows ?? throw new ArgumentNullException(nameof(rows));
        }

        public FriendSectionKind Kind { get; }

        public IReadOnlyList<FriendRowVm> Rows { get; }
    }
}
