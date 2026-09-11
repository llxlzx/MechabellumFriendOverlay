using System;
using System.Collections.Generic;
using FriendOverlay.Core;
using FriendOverlay.State;
using Il2CppGameRiver.Client;
using Il2CppProtos.Friend;
using MelonLoader;
using UnityEngine;
using Il2CppListUlong = Il2CppSystem.Collections.Generic.List<ulong>;

namespace FriendOverlay.Data
{
    public static class FriendListService
    {
        private const int DefaultPageSize = 20;

        /// <summary>Pages asked for per tick. Two keeps a 200+ list quick without flooding.</summary>
        private const int MaxRequestsPerTick = 2;

        /// <summary>
        /// No-progress ticks tolerated once every page has been asked for. The server can simply hold
        /// fewer rows than GetTotalFollowCount claims, and a list that never settles would sit on
        /// 「同步中」 forever.
        /// </summary>
        private const int MaxStaleHits = 8;

        /// <summary>Consecutive ticks the game's own request gate may block us before we push one page.</summary>
        private const int GateBypassAfter = 6;

        /// <summary>Hard ceiling on the page scan so a bogus total cannot spin the request loop.</summary>
        private const int MaxPageBound = 512;

        /// <summary>
        /// Presence is polled in rotating chunks rather than all at once. Asking about the whole list
        /// every tick is what hung the game on 2026-09-10; see OnlinePoll for the numbers.
        /// </summary>
        private static readonly OnlinePoll _onlinePoll = new OnlinePoll(OnlinePoll.DefaultChunkSize);

        private static readonly List<ulong> _pollIds = new List<ulong>();

        private static readonly List<FriendRowVm> _snapshot = new List<FriendRowVm>();

        /// <summary>
        /// Pages this session has requested. Tracked locally rather than read off the proxy so a page
        /// the game never records (an out-of-range probe, say) is not requested again every tick.
        /// </summary>
        private static readonly HashSet<int> _requestedPages = new HashSet<int>();

        private static float _nextPageAt;
        private static float _nextOnlineAt;
        private static float _nextSnapshotAt;
        private static bool _pagingActive;
        private static bool _opened;
        private static int _lastLoadedCount = -1;
        private static int _stalePageHits;
        private static float _pageInterval = 0.35f;
        private static int _gateBlockedHits;
        private static string _lastPagingLog = string.Empty;
        private static string _lastPollLog = string.Empty;

        public static IReadOnlyList<FriendRowVm> Snapshot => _snapshot;
        public static int TotalFollowCount { get; private set; }
        public static bool IsPaging => _pagingActive;
        public static string StatusText { get; private set; } = string.Empty;

        public static void OnPanelOpened()
        {
            _opened = true;
            _pagingActive = true;
            _nextPageAt = 0f;
            _nextOnlineAt = 0f;
            _nextSnapshotAt = 0f;
            _stalePageHits = 0;
            _gateBlockedHits = 0;
            _pageInterval = 0.35f;
            _requestedPages.Clear();
            _onlinePoll.Reset();
            _lastPollLog = string.Empty;
            // Show cached list first, then page in background.
            RefreshSnapshot(forceOnline: false);
            TryRequestPages(force: true);
            RequestOnlineBatch();
        }

        public static void OnPanelClosed()
        {
            _opened = false;
            _pagingActive = false;
            _snapshot.Clear();
            StatusText = string.Empty;
            _lastLoadedCount = -1;
            _stalePageHits = 0;
            _gateBlockedHits = 0;
            _requestedPages.Clear();
            _lastPagingLog = string.Empty;
            _lastPollLog = string.Empty;
            _onlinePoll.Reset();
        }

        public static void Tick()
        {
            if (!_opened || OverlaySession.Degraded || OverlaySession.Proxy == null)
                return;

            try
            {
                if (_pagingActive)
                    TryRequestPages(force: false);

                if (Time.unscaledTime >= _nextSnapshotAt)
                {
                    RefreshSnapshot(forceOnline: false);
                    _nextSnapshotAt = Time.unscaledTime + 0.75f;
                }

                // Presence traffic only while somebody is actually looking. A panel left open on the
                // native side used to keep polling for as long as the session lasted.
                if (OverlaySession.OverlayVisible && Time.unscaledTime >= _nextOnlineAt)
                {
                    RequestOnlineBatch();
                    _nextOnlineAt = Time.unscaledTime + 2.5f;
                }

                if (OverlaySession.OverlayVisible)
                    UI.GameAssets.RequestMissingAvatars(_snapshot);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[FriendOverlay] FriendListService.Tick: " + ex.Message);
            }
        }

        public static void ForceRefresh()
        {
            var proxy = OverlaySession.Proxy;
            if (proxy == null)
                return;

            try
            {
                proxy.RefreshFriendList(true);
                _pagingActive = true;
                _stalePageHits = 0;
                _gateBlockedHits = 0;
                _lastLoadedCount = -1;
                _pageInterval = 0.35f;
                // The game drops its cached pages here, so our record of what was asked for is stale.
                _requestedPages.Clear();
                TryRequestPages(force: true);
                RefreshSnapshot(forceOnline: true);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[FriendOverlay] ForceRefresh: " + ex.Message);
            }
        }

        private static void UpdateStatusText(int loaded, int total)
        {
            if (total < 0)
                total = 0;
            if (loaded < 0)
                loaded = 0;

            TotalFollowCount = Math.Max(total, TotalFollowCount);
            var denom = Math.Max(TotalFollowCount, loaded);

            if (denom > 0 && loaded >= denom)
            {
                _pagingActive = false;
                StatusText = "已同步 " + loaded + "/" + denom;
                return;
            }

            StatusText = (_pagingActive ? "同步中 " : "已同步 ") + loaded + "/" + denom;
        }

        private static void TryRequestPages(bool force)
        {
            var proxy = OverlaySession.Proxy;
            if (proxy == null)
                return;

            if (!force && Time.unscaledTime < _nextPageAt)
                return;

            _nextPageAt = Time.unscaledTime + _pageInterval;

            try
            {
                var loaded = CountLoaded(proxy);
                UpdateStatusText(loaded, proxy.GetTotalFollowCount());
                var total = TotalFollowCount;

                if (total > 0 && loaded >= total)
                {
                    Settle(loaded, total, "complete");
                    return;
                }

                if (loaded == _lastLoadedCount)
                    _stalePageHits++;
                else
                    _stalePageHits = 0;
                _lastLoadedCount = loaded;

                var gateOpen = CanRequest(proxy);
                if (gateOpen)
                    _gateBlockedHits = 0;
                else
                    _gateBlockedHits++;

                // The game's gate runs off its own clock. If it never opens for us the list would
                // sit on 「同步中」 forever, so let a single page through after a few blocked ticks.
                var bypass = !gateOpen && (force || _gateBlockedHits >= GateBypassAfter);
                if (bypass)
                    _gateBlockedHits = 0;

                var budget = gateOpen ? MaxRequestsPerTick : bypass ? 1 : 0;
                var pageSize = PageSize();
                var issued = 0;
                var nextPage = NextMissingPage(proxy, total, pageSize);

                while (nextPage >= 0 && issued < budget)
                {
                    proxy.RequestFollowList(nextPage);
                    _requestedPages.Add(nextPage);
                    issued++;
                    nextPage = NextMissingPage(proxy, total, pageSize);
                }

                LogPaging(loaded, total, nextPage, issued);

                // Backing off only helps while responses are still arriving; pages already in
                // flight just need time.
                _pageInterval = _stalePageHits >= 3 && issued == 0 ? 1.0f : 0.35f;

                if (nextPage < 0 && issued == 0)
                {
                    // Every page the total implies has been asked for. Nudge the game's own queue in
                    // case it tracks pages we cannot see, then settle if nothing more shows up.
                    try { proxy.RequestFollowListNextPageIfFree(); }
                    catch { }

                    if (_stalePageHits >= MaxStaleHits)
                        Settle(loaded, total, "no further pages");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[FriendOverlay] pagination: " + ex.Message);
                _pagingActive = false;
                UpdateStatusText(_snapshot.Count, TotalFollowCount);
            }
        }

        private static void Settle(int loaded, int total, string reason)
        {
            _pagingActive = false;
            _stalePageHits = 0;
            _gateBlockedHits = 0;
            _pageInterval = 0.35f;
            UpdateStatusText(loaded, total);
            Log("paging done loaded=" + loaded + " total=" + total + " (" + reason + ")");
        }

        private static bool CanRequest(FriendProxy proxy)
        {
            try { return proxy.CanRequestFollowList(Time.unscaledTime); }
            catch { return true; }
        }

        private static int PageSize()
        {
            try
            {
                var n = FriendProxy.FRIEND_PAGE_COUNT;
                if (n >= 1 && n <= 500)
                    return n;
            }
            catch { }

            return DefaultPageSize;
        }

        /// <summary>
        /// Lowest page this session has not asked for yet, or -1 once every page is covered. The scan
        /// starts at 0 and runs one past the computed bound because the server's first page index is
        /// not documented; a spare probe is cheap and re-fetching a page the game already holds only
        /// replaces it.
        /// </summary>
        private static int NextMissingPage(FriendProxy proxy, int total, int pageSize)
        {
            var bound = PageBound(proxy, total, pageSize);
            if (bound <= 0)
                return -1;

            for (var page = 0; page <= bound; page++)
            {
                if (!_requestedPages.Contains(page))
                    return page;
            }

            return -1;
        }

        /// <summary>
        /// Highest page worth asking for. The server's own page count wins when it has answered at
        /// least once, so a wrong local page size cannot cut the list short.
        /// </summary>
        private static int PageBound(FriendProxy proxy, int total, int pageSize)
        {
            var byTotal = total > 0 && pageSize > 0 ? (total + pageSize - 1) / pageSize : 0;

            var byServer = 0;
            try { byServer = proxy.maxRequestFollowListPage; }
            catch { byServer = 0; }

            var bound = Math.Max(byTotal, byServer);
            return bound <= 0 ? 0 : Math.Min(bound, MaxPageBound);
        }

        private static void LogPaging(int loaded, int total, int nextPage, int issued)
        {
            Log("paging loaded=" + loaded + " total=" + total + " next=" + nextPage +
                " issued=" + issued + " asked=" + _requestedPages.Count);
        }

        private static void Log(string line)
        {
            if (line == _lastPagingLog)
                return;

            _lastPagingLog = line;
            MelonLogger.Msg("[FriendOverlay] " + line);
        }

        private static int CountLoaded(FriendProxy proxy)
        {
            try
            {
                var list = proxy.GetFriendBaseInfoList();
                return list == null ? 0 : list.Count;
            }
            catch
            {
                return _snapshot.Count;
            }
        }

        private static void RefreshSnapshot(bool forceOnline)
        {
            var proxy = OverlaySession.Proxy;
            if (proxy == null)
                return;

            var list = proxy.GetFriendBaseInfoList();
            _snapshot.Clear();
            if (list == null)
            {
                UpdateStatusText(0, TotalFollowCount);
                return;
            }

            Il2CppSystem.Collections.Generic.Dictionary<ulong, FriendStatus>? stateDic = null;
            try { stateDic = proxy.friendBasePlayerStateDic; }
            catch { stateDic = null; }

            FaceBlockPolicy.BeginSnapshot();

            for (var i = 0; i < list.Count; i++)
            {
                FriendBaseInfo? info = null;
                try { info = list[i]; } catch { continue; }
                if (info == null)
                    continue;

                _snapshot.Add(FriendRowMapper.Map(info, stateDic));
            }

            var total = Math.Max(TotalFollowCount, proxy.GetTotalFollowCount());
            UpdateStatusText(_snapshot.Count, total);

            if (forceOnline)
                RequestOnlineBatch();
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
                LogPoll(_pollIds.Count);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[FriendOverlay] RequestOnline: " + ex.Message);
            }
        }

        /// <summary>
        /// Deduped on the list size, so paging logs a handful of lines and a settled list logs none —
        /// enough to prove the request size is bounded without writing a line every 2.5s for an hour.
        /// </summary>
        private static void LogPoll(int total)
        {
            var line = "online poll ids=" + total + " chunk=" + _onlinePoll.ChunkSize;
            if (line == _lastPollLog)
                return;

            _lastPollLog = line;
            MelonLogger.Msg("[FriendOverlay] " + line);
        }
    }
}
