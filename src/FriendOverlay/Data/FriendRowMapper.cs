using FriendOverlay.Core;
using Il2CppProtos.Friend;

namespace FriendOverlay.Data
{
    /// <summary>One place that turns a game FriendBaseInfo into a row, shared by both lists.</summary>
    public static class FriendRowMapper
    {
        /// <param name="trustListedState">
        /// Whether <c>info.State</c> can be believed. True for the following list, which the server
        /// fills in. False for the followers list: those entries describe players the local player
        /// does not follow, and an unfilled State reads as Idle = 0, i.e. online and joinable.
        /// </param>
        public static FriendRowVm Map(
            FriendBaseInfo info,
            Il2CppSystem.Collections.Generic.Dictionary<ulong, FriendStatus>? stateDic,
            bool trustListedState = true)
        {
            int? reported = null;
            try
            {
                if (stateDic != null && stateDic.ContainsKey(info.Userid))
                {
                    var st = stateDic[info.Userid];
                    if (st != null)
                        reported = st.State;
                }
            }
            catch
            {
                reported = null;
            }

            var presence = Presence.Resolve(
                reported,
                trustListedState ? info.State : (int?)null,
                (int)Il2CppProtos.Common.EPlayerState.Offline);
            var state = presence.State;

            var name = string.Empty;
            var face = string.Empty;
            try
            {
                if (info.RiskInfo != null)
                {
                    name = info.RiskInfo.Name ?? string.Empty;
                    face = info.RiskInfo.FaceUrl ?? string.Empty;
                }
            }
            catch
            {
                // ignore
            }

            var blocked = FaceBlockPolicy.IsBlocked(info);
            var plan = PortraitResolver.Resolve(info, blocked);

            return new FriendRowVm
            {
                UserId = info.Userid,
                Name = name,
                FaceUrl = face,
                FaceBlocked = blocked,
                Portrait = plan.Kind,
                PortraitRef = plan.ImageRef,
                FrameRef = plan.FrameRef,
                RankPoint = info.RankPoint,
                ForecastPoint = info.ForecastPoint,
                State = state,
                IsMutual = info.IsMutual,
                StateKnown = presence.Known,
                IsOnline = presence.Known && FriendStatusMapper.IsOnline(state),
                IsBusy = presence.Known && FriendStatusMapper.IsBusy(state),
                Platform = info.Platform,
                StatusLabel = presence.Known ? FriendStatusMapper.ToLabel(state) : L.T("status.unknown"),
                StatusKind = FriendStatusMapper.ToKind(state),
            };
        }
    }
}
