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
                (int)EPlayerState.Idle => L.T("state.idle"),
                (int)EPlayerState.Battle1V1 => L.T("state.1v1"),
                (int)EPlayerState.Battle2V2 => L.T("state.2v2"),
                (int)EPlayerState.BattleSurvive => L.T("state.survive"),
                (int)EPlayerState.BattleSurvive2V2 => L.T("state.survive2v2"),
                // Native invite button / CreateRoom path: BtnChaosFactionOnClicked → VS_4_Scuffle.
                (int)EPlayerState.BattleChaosFaction => L.T("state.scuffle4"),
                (int)EPlayerState.BattleDimensionalRift => L.T("state.rift"),
                (int)EPlayerState.BattleDimensionalRift2 => L.T("state.rift2"),
                (int)EPlayerState.BattleLevel => L.T("state.level"),
                (int)EPlayerState.BattleGuider => L.T("state.guider"),
                (int)EPlayerState.MultiBattleWatch => L.T("state.watch"),
                (int)EPlayerState.CustomRoom => L.T("state.custom"),
                (int)EPlayerState.CompetitionBattle => L.T("state.comp_battle"),
                (int)EPlayerState.CompetitionIdle => L.T("state.comp_idle"),
                (int)EPlayerState.Offline => L.T("state.offline"),
                _ => L.Tf("status.state_n", state),
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
                (int)EPlayerState.MultiBattleWatch => FriendStatusKind.Pve,
                _ => FriendStatusKind.Battle,
            };
        }

        public static UnityEngine.Color ToColor(int state) => UI.Theme.StatusColor(ToKind(state));
    }
}
