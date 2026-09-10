using System;
using System.Collections.Generic;
using FriendOverlay.Compat;
using FriendOverlay.Core;
using FriendOverlay.State;
using Il2CppProtos.Friend;
using MelonLoader;
using UnityEngine;
using Il2CppListUlong = Il2CppSystem.Collections.Generic.List<ulong>;

namespace FriendOverlay.Data
{
    /// <summary>
    /// 「关注我的人」: players who follow the local player. Source is FriendProxy.followerBaseInfoList,
    /// refreshed through RequestLastFollower(). RequestFollowerStatus is deliberately unused — its
    /// reply (ResponseFollowStatus.Following) pages the *following* status, not this list.
    /// Only ticks while its tab is visible, so the extra RequestOnline traffic stays opt-in.
    /// </summary>
    public static class FansListService
    {
        private static readonly List<FriendRowVm> _snapshot = new List<FriendRowVm>();
        private static readonly List<FriendBaseInfo> _captured = new List<FriendBaseInfo>();

        /// <summary>Same bounded rotation the friend list uses; see OnlinePoll.</summary>
        private static readonly OnlinePoll _onlinePoll = new OnlinePoll(OnlinePoll.DefaultChunkSize);

        private static readonly List<ulong> _pollIds = new List<ulong>();

        private static bool _opened;
        private static bool _requested;
        private static bool _loggedCoverage;
        private static float _nextSnapshotAt;
        private static float _nextOnlineAt;

        public static IReadOnlyList<FriendRowVm> Snapshot => _snapshot;

        public static bool Available => Capabilities.Followers;

        /// <summary>Set by the panel when the tab is showing; gates Tick.</summary>
        public static bool Active { get; set; }

        public static bool IsLoading { get; private set; }

        public static string StatusText { get; private set; } = string.Empty;

        public static void OnPanelOpened()
        {
            if (!Available)
                return;

            _opened = true;
            _requested = false;
            _nextSnapshotAt = 0f;
            _nextOnlineAt = 0f;
            _onlinePoll.Reset();

            // A mid-session panel swap reaches here without a close, and the _captured fallback
            // would otherwise resurrect followers fetched through the previous proxy.
            _snapshot.Clear();
            _captured.Clear();
            StatusText = string.Empty;
            _loggedCoverage = false;
        }

        public static void OnPanelClosed()
        {
            _opened = false;
            Active = false;
            IsLoading = false;
            _requested = false;
            _snapshot.Clear();
            _captured.Clear();
            StatusText = string.Empty;
            _nextSnapshotAt = 0f;
            _nextOnlineAt = 0f;
            _loggedCoverage = false;
            _onlinePoll.Reset();
        }

        public static void Tick()
        {
            if (!_opened || !Active || OverlaySession.Degraded || OverlaySession.Proxy == null)
                return;

            try
            {
                if (!_requested)
                {
                    _requested = true;
                    RequestList();
                }

                if (Time.unscaledTime >= _nextSnapshotAt)
                {
                    RefreshSnapshot();
                    _nextSnapshotAt = Time.unscaledTime + 0.75f;
                }

                if (OverlaySession.OverlayVisible && Time.unscaledTime >= _nextOnlineAt)
                {
                    RequestOnlineBatch();
                    _nextOnlineAt = Time.unscaledTime + 2.5f;
                }

                UI.GameAssets.RequestMissingAvatars(_snapshot, "followers");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[FriendOverlay] FansListService.Tick: " + ex.Message);
            }
        }

        /// <summary>
        /// Only refreshes while the tab is showing. Unfollow and Blacklist fire from either tab, and
        /// an inactive tab must not add server traffic for a list nobody is looking at.
        /// </summary>
        public static void ForceRefresh()
        {
            if (!_opened || !Active || !Available)
                return;

            RequestList();
            RefreshSnapshot();
            RequestOnlineBatch();
        }

        /// <summary>
        /// Fed by the OnResponseLastFollower postfix. Keeps the tab working even if the proxy stores
        /// the reply somewhere we do not read; the proxy list stays the primary source.
        /// </summary>
        public static void OnResponse(ResponseLastFollower? msg)
        {
            if (msg == null)
                return;

            _captured.Clear();
            var count = 0;
            try
            {
                var followers = msg.Follower;
                if (followers != null)
                {
                    count = followers.Count;
                    for (var i = 0; i < followers.Count; i++)
                    {
                        FriendBaseInfo? info = null;
                        try { info = followers[i]; } catch { continue; }
                        if (info != null)
                            _captured.Add(info);
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[FriendOverlay] followers response unreadable: " + ex.Message);
            }

            var newCount = 0;
            try { newCount = msg.New?.Count ?? 0; } catch { newCount = 0; }

            IsLoading = false;
            _nextSnapshotAt = 0f;
            _loggedCoverage = false;
            MelonLogger.Msg("[FriendOverlay] followers response count=" + count + " new=" + newCount);
        }

        private static void RequestList()
        {
            var proxy = OverlaySession.Proxy;
            if (proxy == null)
                return;

            try
            {
                proxy.RequestLastFollower();
                IsLoading = true;
                if (_snapshot.Count == 0)
                    StatusText = "同步中";
            }
            catch (Exception ex)
            {
                IsLoading = false;
                MelonLogger.Warning("[FriendOverlay] RequestLastFollower: " + ex.Message);
            }
        }

        private static void RefreshSnapshot()
        {
            var proxy = OverlaySession.Proxy;
            if (proxy == null)
                return;

            Il2CppSystem.Collections.Generic.List<FriendBaseInfo>? list = null;
            try { list = proxy.followerBaseInfoList; } catch { list = null; }

            Il2CppSystem.Collections.Generic.Dictionary<ulong, FriendStatus>? stateDic = null;
            try { stateDic = proxy.friendBasePlayerStateDic; } catch { stateDic = null; }

            FaceBlockPolicy.BeginSnapshot();
            _snapshot.Clear();
            var seen = new HashSet<ulong>();

            if (list != null)
            {
                for (var i = 0; i < list.Count; i++)
                {
                    FriendBaseInfo? info = null;
                    try { info = list[i]; } catch { continue; }
                    if (info == null || !seen.Add(info.Userid))
                        continue;

                    _snapshot.Add(FriendRowMapper.Map(info, stateDic, trustListedState: false));
                }
            }

            // Merge rather than substitute. Either source can be short, and preferring one whenever
            // it is non-empty would silently drop followers the other one knows about.
            foreach (var info in _captured)
            {
                if (seen.Add(info.Userid))
                    _snapshot.Add(FriendRowMapper.Map(info, stateDic, trustListedState: false));
            }

            var known = 0;
            for (var i = 0; i < _snapshot.Count; i++)
            {
                if (_snapshot[i].StateKnown)
                    known++;
            }

            LogCoverageOnce(known, _snapshot.Count);

            if (IsLoading && _snapshot.Count == 0)
            {
                StatusText = "同步中";
                return;
            }

            // Never claim a total we cannot verify: RequestLastFollower has no completion signal.
            StatusText = _snapshot.Count > 0 && known == 0
                ? "已加载 " + _snapshot.Count + " · 状态未知"
                : "已加载 " + _snapshot.Count;
        }

        /// <summary>
        /// RequestOnline is not documented to answer for uids the local player does not follow, and
        /// the native 关注我的人 cell shows no state at all, so there is no precedent to copy. This
        /// records whether the server actually answered, so a silently all-offline tab is visible in
        /// the log instead of looking like everyone happens to be offline.
        /// </summary>
        private static void LogCoverageOnce(int known, int total)
        {
            if (_loggedCoverage || total == 0)
                return;

            _loggedCoverage = true;
            MelonLogger.Msg("[FriendOverlay] followers states known=" + known + "/" + total);
        }

        private static void RequestOnlineBatch()
        {
            var proxy = OverlaySession.Proxy;
            if (proxy == null || _snapshot.Count == 0)
                return;

            try
            {
                _pollIds.Clear();
                foreach (var row in _snapshot)
                    _pollIds.Add(row.UserId);

                var chunk = _onlinePoll.Next(_pollIds);
                if (chunk.Count == 0)
                    return;

                var ids = new Il2CppListUlong();
                for (var i = 0; i < chunk.Count; i++)
                    ids.Add(chunk[i]);

                proxy.RequestOnline(ids);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[FriendOverlay] followers RequestOnline: " + ex.Message);
            }
        }
    }
}
