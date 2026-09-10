using System.Collections.Generic;

namespace FriendOverlay.Core
{
    public enum BattleTypeKind
    {
        Vs1v1 = 0,
        Vs2v2 = 1,
        Survive = 2,
        TeamMatch = 3,
        Scuffle4 = 4,
        Rift1v1 = 5,
        Rift2v2 = 6,
    }

    /// <summary>
    /// One button of the native 「邀请参与的战斗类型」 window. GameMode and MatchMode are ints because Core
    /// is netstandard2.0 and cannot see the Il2Cpp enums; the mod side casts them back.
    /// </summary>
    public sealed class BattleType
    {
        public BattleType(
            BattleTypeKind kind,
            string label,
            int gameMode,
            int matchMode,
            bool isTeamMatch,
            bool verified)
        {
            Kind = kind;
            Label = label;
            GameMode = gameMode;
            MatchMode = matchMode;
            IsTeamMatch = isTeamMatch;
            Verified = verified;
        }

        public BattleTypeKind Kind { get; }

        public string Label { get; }

        public int GameMode { get; }

        public int MatchMode { get; }

        /// <summary>Joins an existing queue instead of creating a room, so it carries no room mode.</summary>
        public bool IsTeamMatch { get; }

        /// <summary>
        /// Whether a live trace of the native window confirmed this row's call. Unverified rows render
        /// as 暂未开放 and hold NoRoomMode, so a guess can never reach CreateRoom.
        /// </summary>
        public bool Verified { get; }
    }

    public static class BattleTypeCatalog
    {
        /// <summary>Single sentinel for "this row does not create a room", verified or not.</summary>
        public const int NoRoomMode = -1;

        private static readonly BattleType[] _entries =
        {
            Unverified(BattleTypeKind.Vs1v1, "1对1"),
            Unverified(BattleTypeKind.Vs2v2, "2对2"),
            Unverified(BattleTypeKind.Survive, "生存模式"),
            new BattleType(BattleTypeKind.TeamMatch, "组队匹配", NoRoomMode, NoRoomMode, true, false),
            Unverified(BattleTypeKind.Scuffle4, "4人混战"),
            Unverified(BattleTypeKind.Rift1v1, "时空裂隙 1V1"),
            Unverified(BattleTypeKind.Rift2v2, "时空裂隙 2V2"),
        };

        /// <summary>Native window order, top to bottom.</summary>
        public static IList<BattleType> Entries => _entries;

        public static BattleType? ByKind(BattleTypeKind kind)
        {
            for (var i = 0; i < _entries.Length; i++)
            {
                if (_entries[i].Kind == kind)
                    return _entries[i];
            }

            return null;
        }

        private static BattleType Unverified(BattleTypeKind kind, string label) =>
            new BattleType(kind, label, NoRoomMode, NoRoomMode, false, false);
    }
}
