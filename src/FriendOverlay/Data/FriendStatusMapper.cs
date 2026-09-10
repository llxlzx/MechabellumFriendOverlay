using System.Collections.Generic;
using FriendOverlay.Core;
using Il2CppProtos.Common;

namespace FriendOverlay.Data
{
    public static class FriendStatusMapper
    {
        public static string ToLabel(int state)
        {
            return state switch
            {
                (int)EPlayerState.Idle => "空闲",
                (int)EPlayerState.Battle1V1 => "1V1对战",
                (int)EPlayerState.Battle2V2 => "2V2对战",
                (int)EPlayerState.BattleSurvive => "生存战",
                (int)EPlayerState.BattleSurvive2V2 => "生存2V2",
                (int)EPlayerState.BattleChaosFaction => "混乱阵营",
                (int)EPlayerState.BattleDimensionalRift => "次元裂隙",
                (int)EPlayerState.BattleDimensionalRift2 => "次元裂隙2",
                (int)EPlayerState.BattleLevel => "关卡",
                (int)EPlayerState.BattleGuider => "指挥学院",
                (int)EPlayerState.MultiBattleWatch => "观战中",
                (int)EPlayerState.CustomRoom => "自定义房间",
                (int)EPlayerState.CompetitionBattle => "比赛中",
                (int)EPlayerState.CompetitionIdle => "等待比赛中",
                (int)EPlayerState.Offline => "离线",
                _ => "状态" + state,
            };
        }

        public static bool IsOnline(int state) => state != (int)EPlayerState.Offline;

        public static bool IsBusy(int state)
        {
            return state != (int)EPlayerState.Idle
                   && state != (int)EPlayerState.Offline
                   && state != (int)EPlayerState.CompetitionIdle;
        }

        public static FriendStatusKind ToKind(int state)
        {
            return state switch
            {
                (int)EPlayerState.Offline => FriendStatusKind.Offline,
                (int)EPlayerState.Idle => FriendStatusKind.Idle,
                (int)EPlayerState.CompetitionIdle => FriendStatusKind.Waiting,
                (int)EPlayerState.BattleGuider => FriendStatusKind.Pve,
                (int)EPlayerState.BattleLevel => FriendStatusKind.Pve,
                (int)EPlayerState.BattleDimensionalRift => FriendStatusKind.Pve,
                (int)EPlayerState.BattleDimensionalRift2 => FriendStatusKind.Pve,
                (int)EPlayerState.BattleChaosFaction => FriendStatusKind.Pve,
                (int)EPlayerState.MultiBattleWatch => FriendStatusKind.Pve,
                _ => FriendStatusKind.Battle,
            };
        }

        public static UnityEngine.Color ToColor(int state) => UI.Theme.StatusColor(ToKind(state));
    }
}
