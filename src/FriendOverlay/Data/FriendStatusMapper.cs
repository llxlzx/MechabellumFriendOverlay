using FriendOverlay.Core;

namespace FriendOverlay.Data
{
    public static class FriendStatusMapper
    {
        public static string ToLabel(int state)
        {
            var key = PlayerStateCatalog.LabelKey(state);
            return key == null ? L.Tf("status.state_n", state) : L.T(key);
        }

        public static bool IsOnline(int state) => PlayerStateCatalog.IsOnline(state);

        public static bool IsBusy(int state) => PlayerStateCatalog.IsBusy(state);

        public static FriendStatusKind ToKind(int state) => PlayerStateCatalog.Kind(state);

        public static UnityEngine.Color ToColor(int state) => UI.Theme.StatusColor(ToKind(state));
    }
}
