namespace FriendOverlay.Core
{
    /// <summary>
    /// EPlayerState as of Mechabellum build 25440024. Offline moved from 14 to 16 when
    /// AsyncBattle1V1 and AsyncBattle2V2 were inserted.
    /// </summary>
    public static class PlayerStateCatalog
    {
        public const int Idle = 0;
        public const int Battle1V1 = 1;
        public const int Battle2V2 = 2;
        public const int BattleSurvive = 3;
        public const int BattleSurvive2V2 = 4;
        public const int BattleChaosFaction = 5;
        public const int BattleDimensionalRift = 6;
        public const int BattleDimensionalRift2 = 7;
        public const int BattleLevel = 8;
        public const int BattleGuider = 9;
        public const int MultiBattleWatch = 10;
        public const int CustomRoom = 11;
        public const int CompetitionBattle = 12;
        public const int CompetitionIdle = 13;
        public const int AsyncBattle1V1 = 14;
        public const int AsyncBattle2V2 = 15;
        public const int Offline = 16;

        public static string? LabelKey(int state) => state switch
        {
            Idle => "state.idle",
            Battle1V1 => "state.1v1",
            Battle2V2 => "state.2v2",
            BattleSurvive => "state.survive",
            BattleSurvive2V2 => "state.survive2v2",
            BattleChaosFaction => "state.scuffle4",
            BattleDimensionalRift => "state.rift",
            BattleDimensionalRift2 => "state.rift2",
            BattleLevel => "state.level",
            BattleGuider => "state.guider",
            MultiBattleWatch => "state.watch",
            CustomRoom => "state.custom",
            CompetitionBattle => "state.comp_battle",
            CompetitionIdle => "state.comp_idle",
            AsyncBattle1V1 => "state.async1v1",
            AsyncBattle2V2 => "state.async2v2",
            Offline => "state.offline",
            _ => null,
        };

        public static bool IsOnline(int state) => state != Offline;

        public static bool IsBusy(int state) =>
            state != Idle && state != Offline && state != CompetitionIdle;

        public static FriendStatusKind Kind(int state) => state switch
        {
            Offline => FriendStatusKind.Offline,
            Idle => FriendStatusKind.Idle,
            CompetitionIdle => FriendStatusKind.Waiting,
            BattleGuider => FriendStatusKind.Pve,
            BattleLevel => FriendStatusKind.Pve,
            BattleDimensionalRift => FriendStatusKind.Pve,
            BattleDimensionalRift2 => FriendStatusKind.Pve,
            MultiBattleWatch => FriendStatusKind.Pve,
            _ => FriendStatusKind.Battle,
        };
    }
}
