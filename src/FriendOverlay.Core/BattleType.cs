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

        // GameMode: Normal=0 Competition=1 Guider=2 Survive=3 Rift=4
        // MatchMode: VS_1_1=0 VS_2_2=1 VS_4_Scuffle=2 VS_2_2_Scuffle=3
        // Every cell below was read off the native window on 2026-09-10; see
        // docs/superpowers/plans/2026-09-10-invite-trace-table.md for the log lines.
        private static readonly BattleType[] _entries =
        {
            Room(BattleTypeKind.Vs1v1, "battle.vs1v1", 0, 0),
            Room(BattleTypeKind.Vs2v2, "battle.vs2v2", 0, 1),
            // Survive pairs with VS_2_2, not VS_1_1, which is why this table had to be captured.
            Room(BattleTypeKind.Survive, "battle.survive", 3, 1),
            // Creates no room at all: the native button only calls TeamProxy.RequestTeamInvite.
            new BattleType(BattleTypeKind.TeamMatch, "battle.team", NoRoomMode, NoRoomMode, true, true),
            Room(BattleTypeKind.Scuffle4, "battle.scuffle4", 0, 2),
            Room(BattleTypeKind.Rift1v1, "battle.rift1v1", 4, 0),
            Room(BattleTypeKind.Rift2v2, "battle.rift2v2", 4, 1),
        };

        private static readonly IList<BattleType> _readOnly = System.Array.AsReadOnly(_entries);

        /// <summary>Native window order, top to bottom.</summary>
        public static IList<BattleType> Entries => _readOnly;

        public static BattleType? ByKind(BattleTypeKind kind)
        {
            for (var i = 0; i < _entries.Length; i++)
            {
                if (_entries[i].Kind == kind)
                    return _entries[i];
            }

            return null;
        }

        private static BattleType Room(BattleTypeKind kind, string label, int gameMode, int matchMode) =>
            new BattleType(kind, label, gameMode, matchMode, false, true);
    }
}
