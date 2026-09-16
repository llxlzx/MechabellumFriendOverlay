using System;
using System.Collections.Generic;
using FriendOverlay.Actions;
using FriendOverlay.Core;
using FriendOverlay.Data;
using FriendOverlay.State;
using FriendOverlay.UI.Widgets;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace FriendOverlay.UI
{
    /// <summary>
    /// Absolute-rect IMGUI only. GUILayout / GUI.TextField / GUI.BeginScrollView / Texture2D.SetPixel
    /// are all stripped on this IL2CPP build, so everything here is drawn from explicit rects.
    /// Chrome is painted after the list so overflowing rows get masked instead of clipped.
    /// </summary>
    public static class ImguiFriendOverlay
    {
        private enum ItemKind
        {
            Header = 0,
            Row = 1,
        }

        private struct ListItem
        {
            public ItemKind Kind;
            public FriendSectionKind Section;
            public int Count;
            public FriendRowVm? Row;
            public float Offset;
            public float Height;
        }

        private const string FollowingLabel = "关注";
        private const string FollowersLabel = "关注我的人";

        private static readonly SearchBox _search = new SearchBox();
        private static readonly ScrollBar _scroll = new ScrollBar();
        private static readonly List<ListItem> _items = new List<ListItem>();
        private static IReadOnlyList<FriendRowVm> _view = new List<FriendRowVm>();
        private static int _viewFrame = -1;

        private static FriendFilter _filter = FriendFilter.All;
        private static FriendSortKey _sort = FriendSortKey.Default;
        private static Rect _window;
        private static bool _windowReady;
        private static bool _loggedDrawError;

        private static bool _dragging;
        private static float _dragOffX;
        private static float _dragOffY;
        private static WindowResizeEdge _resizeEdge;
        private static float _resizeStartX;
        private static float _resizeStartY;
        private static float _resizeStartW;
        private static float _resizeStartH;
        private static float _resizeStartMouseX;
        private static float _resizeStartMouseY;

        private static bool IsResizing => _resizeEdge != WindowResizeEdge.None;

        private static ulong _menuRowId;
        private static Rect _menuRect;
        private static int _menuOpenedFrame = -1;
        private static float _hoverStart;
        private static ulong _hoverRowId;
        private static float _visibleSince = -1f;
        private static bool _wasVisible;
        private static float _lastViewportH = 320f;
        private static readonly InviteCooldown _inviteCooldown = new InviteCooldown(5.0);
        private static bool _inRoom;
        private static Rect _tabStrip;
        private static readonly HashSet<ulong> _avatarPriorityIds = new HashSet<ulong>();
        private static int _avatarPriorityFrame;

        public static FriendListTab Tab { get; private set; } = FriendListTab.Following;

        /// <summary>
        /// Called when an invite actually left the client, including one the create-room flow sent long
        /// after the row was clicked. Marking at send rather than at click keeps 已邀请 truthful: a flow
        /// that timed out sent nothing.
        /// </summary>
        public static void NoteInviteSent(ulong userId) => _inviteCooldown.Mark(userId, Time.unscaledTime);

        private static IReadOnlyList<FriendRowVm> Source =>
            Tab == FriendListTab.Followers ? FansListService.Snapshot : FriendListService.Snapshot;

        private static readonly GUI.WindowFunction? WindowFn =
            DelegateSupport.ConvertDelegate<GUI.WindowFunction>((Action<int>)DrawWindow);

        public static FriendSortKey CurrentSort
        {
            get => _sort;
            set => _sort = value;
        }

        public static FriendFilter CurrentFilter
        {
            get => _filter;
            set => _filter = value;
        }

        private static string HotkeyName => OverlaySession.ToggleHotkey.ToString();

        public static bool CollapsedJoinable { get; set; }

        public static bool CollapsedBusy { get; set; }

        public static bool CollapsedOffline { get; set; }

        public static bool CollapsedPinned { get; set; }

        public static Rect WindowRect
        {
            get => _window;
            set
            {
                _window = value;
                _windowReady = value.width > 1f && value.height > 1f;
            }
        }

        /// <summary>Polled once per frame from OnUpdate; OnGUI can run several times per frame.</summary>
        public static void UpdateInput()
        {
            if (OverlaySession.Panel == null || OverlaySession.Mode != OverlayMode.Overlay)
            {
                _dragging = false;
                _resizeEdge = WindowResizeEdge.None;
                return;
            }

            var before = _search.Text;
            _search.HandleInput();
            if (!string.Equals(before, _search.Text, StringComparison.Ordinal))
            {
                _scroll.ScrollTo(0f);
                InvalidateView();
            }

            if (!_search.Focused)
            {
                if (Input.GetKeyDown(KeyCode.R))
                    RefreshActive();

                _scroll.HandleKeys(_lastViewportH);

                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    // Closing the picker abandons the choice, not an invite already in flight.
                    if (ImguiBattleTypePicker.IsOpen)
                        ImguiBattleTypePicker.Close();
                    else if (_menuRowId != 0)
                        _menuRowId = 0;
                }

                if (Input.GetKeyDown(KeyCode.L) &&
                    (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
                {
                    RowCard.ForceLetters = !RowCard.ForceLetters;
                    MelonLoader.MelonLogger.Msg("[FriendOverlay] force letters: " + RowCard.ForceLetters);
                }
            }

            FollowDrag();
        }

        public static void OnSessionEnd()
        {
            // A pending confirm still holds a closure over a row from the old session.
            ImguiConfirm.Close();
            ImguiBattleTypePicker.Close();
            RowCard.ForceLetters = false;
            _menuRowId = 0;
            _inviteCooldown.Clear();
            _inRoom = false;
            _tabStrip = new Rect(0f, 0f, 0f, 0f);
            Tab = FriendListTab.Following;
            FansListService.Active = false;
            _dragging = false;
            _resizeEdge = WindowResizeEdge.None;
            _scroll.Reset();
            _search.Clear();
            _search.Blur();
            _visibleSince = -1f;
            _wasVisible = false;
            _avatarPriorityIds.Clear();
            _avatarPriorityFrame = 0;
            InvalidateView();
        }

        public static bool IsAvatarPriority(ulong userId)
        {
            if (_avatarPriorityFrame == 0)
                return true;
            if (Time.frameCount - _avatarPriorityFrame > 2)
                return true;
            if (_avatarPriorityIds.Count == 0)
                return false;
            return _avatarPriorityIds.Contains(userId);
        }

        public static void Draw()
        {
            if (OverlaySession.Panel == null)
                return;

            Theme.EnsureStyles();
            Gfx.ProbeOnce();
            EnsureWindow();

            if (OverlaySession.Mode == OverlayMode.Native)
            {
                _wasVisible = false;
                DrawNativeToggle();
                return;
            }

            if (!OverlaySession.OverlayVisible)
            {
                _wasVisible = false;
                return;
            }

            if (!_wasVisible)
            {
                _wasVisible = true;
                _visibleSince = Anim.Now;
            }

            Gfx.GlobalAlpha = Anim.Progress(_visibleSince, 0.15f);

            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _window = GUI.Window(92001, _window, WindowFn!, string.Empty);
            GUI.backgroundColor = prevBg;

            ClampWindowToScreen();
            Gfx.GlobalAlpha = 1f;
            ImguiConfirm.Draw();
            ImguiBattleTypePicker.Draw();
            SyncInputShield();
            ConsumePointerEvents();
        }

        /// <summary>
        /// Keeps the uGUI blocker on the same footprint IMGUI swallows events for, so the lobby stays
        /// clickable around the panel. The cases are deliberately the same ones ConsumePointerEvents
        /// treats as screen-wide.
        /// </summary>
        private static void SyncInputShield()
        {
            if (!InputShield.Active)
                return;

            if (_dragging || IsResizing || ImguiConfirm.IsOpen || ImguiBattleTypePicker.IsOpen)
                InputShield.SyncFullScreen();
            else
                InputShield.SyncRect(_window);
        }

        /// <summary>
        /// Swallow pointer events that landed on the overlay so IMGUI does not hand them on.
        /// The uGUI side is covered separately by <see cref="InputShield"/>.
        /// </summary>
        private static void ConsumePointerEvents()
        {
            var e = Event.current;
            if (e == null)
                return;

            if (e.type != EventType.MouseDown &&
                e.type != EventType.MouseUp &&
                e.type != EventType.MouseDrag &&
                e.type != EventType.ScrollWheel)
                return;

            if (_dragging || IsResizing || ImguiConfirm.IsOpen || ImguiBattleTypePicker.IsOpen ||
                _window.Contains(e.mousePosition))
                e.Use();
        }

        /// <summary>IMGUI chip over the native friend chrome; also the uGUI shield footprint.</summary>
        private static Rect NativeToggleRect() =>
            new Rect(Theme.S(16f), Theme.S(16f), Theme.S(180f), Theme.S(38f));

        private static int _nativeChipClickFrame = -1;

        private static void DrawNativeToggle()
        {
            var r = NativeToggleRect();

            // Native mode destroys the overlay-sized shield; without a chip-sized one the same
            // click reaches the hex buttons under the chip. The shield owns EventSystem hits, so
            // GUI.Button often never sees MouseUp — drive the toggle from screen mouse instead.
            InputShield.Ensure();
            if (InputShield.Active)
                InputShield.SyncRect(r);

            var hovered = Gfx.Hover(r) || NativeChipPointerIn(r);
            var cut = Theme.S(6f);
            Gfx.Chamfer(r, hovered ? Theme.ChipHover : Theme.TitleBar, cut);
            Gfx.ChamferBorder(r, hovered ? Theme.Accent : Theme.Line, cut);
            Gfx.Text(r, "打开叠加面板  ·  " + HotkeyName, Theme.TextHi, Theme.Button);

            if (NativeChipClicked(r))
                OverlaySession.ToggleMode();
        }

        private static bool NativeChipPointerIn(Rect r)
        {
            var gui = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            return r.Contains(gui);
        }

        private static bool NativeChipClicked(Rect r)
        {
            if (!Input.GetMouseButtonUp(0) || !NativeChipPointerIn(r))
                return false;

            // OnGUI runs several times per frame; GetMouseButtonUp stays true for all of them.
            if (_nativeChipClickFrame == Time.frameCount)
                return false;

            _nativeChipClickFrame = Time.frameCount;
            return true;
        }

        private static void DrawWindow(int id)
        {
            try
            {
                CaptureChromeDrag();

                var pad = Theme.S(14f);
                var titleH = Theme.S(52f);
                var toolbarH = Theme.S(36f);
                var statusH = Theme.S(24f);
                var footerH = Theme.S(26f);
                var full = new Rect(0f, 0f, _window.width, _window.height);

                Gfx.Fill(full, Theme.Bg0);

                var listTop = titleH + Theme.S(8f) + toolbarH + Theme.S(6f) + statusH;
                var listBottom = _window.height - footerH - Theme.S(8f);
                var listRect = new Rect(pad, listTop, _window.width - pad * 2f, Mathf.Max(Theme.S(120f), listBottom - listTop));
                _lastViewportH = listRect.height;

                var src = Source;
                EnsureView(src);

                DrawList(listRect, src.Count);

                // A row that only half fits is still drawn at full height, so rows reach into the
                // strips above and below the viewport. Those strips are the mask, which means they
                // have to be opaque: the window fill alone is 0.97 and lets scrolled rows glow
                // through the search row.
                Gfx.Fill(new Rect(0f, titleH, _window.width, listRect.y - titleH), Theme.ChromeBg);
                Gfx.Fill(new Rect(0f, listRect.yMax, _window.width, _window.height - listRect.yMax), Theme.ChromeBg);

                DrawTitleBar(new Rect(0f, 0f, _window.width, titleH), pad, _view.Count);
                DrawToolbar(new Rect(pad, titleH + Theme.S(8f), _window.width - pad * 2f, toolbarH));
                DrawStatusBar(new Rect(pad, titleH + Theme.S(8f) + toolbarH + Theme.S(6f), _window.width - pad * 2f, statusH),
                    _view.Count,
                    src.Count);
                DrawFooter(new Rect(0f, _window.height - footerH, _window.width, footerH), pad);

                if (_menuRowId != 0)
                    DrawOverflowMenu();

                Gfx.PanelDouble(full, new Color(0f, 0f, 0f, 0f), Theme.Line, Theme.LineDim);
            }
            catch (Exception ex)
            {
                if (!_loggedDrawError)
                {
                    _loggedDrawError = true;
                    MelonLoader.MelonLogger.Error("[FriendOverlay] DrawWindow failed: " + ex);
                }
            }
        }

        private static void DrawTitleBar(Rect bar, float pad, int shown)
        {
            Gfx.Fill(bar, Theme.TitleBar);
            Gfx.ScanLines(bar, new Color(1f, 1f, 1f, 0.025f), 3f);

            // One control row: tabs, count and actions share a height and a vertical centre so the
            // bar reads as a single strip.
            var ctrlH = Theme.S(32f);
            var ctrlY = bar.y + (bar.height - ctrlH) * 0.5f;
            var gap = Theme.S(6f);

            var followingW = TabWidth(FollowingLabel);
            var followersW = FansListService.Available ? TabWidth(FollowersLabel) : 0f;
            var strip = new Rect(pad, ctrlY, followingW + followersW, ctrlH);

            Gfx.Fill(strip, Theme.Chip);

            if (TitleTab(new Rect(strip.x, ctrlY, followingW, ctrlH), FollowingLabel, Tab == FriendListTab.Following))
                SwitchTab(FriendListTab.Following);

            if (FansListService.Available)
            {
                var followersX = strip.x + followingW;
                if (TitleTab(new Rect(followersX, ctrlY, followersW, ctrlH), FollowersLabel, Tab == FriendListTab.Followers))
                    SwitchTab(FriendListTab.Followers);

                Gfx.Fill(new Rect(followersX, ctrlY + Theme.S(7f), 1f, ctrlH - Theme.S(14f)), Theme.LineDim);
            }

            Gfx.Border(strip, Theme.Line);

            // Recorded so the chrome drag never steals a click that landed on a tab.
            _tabStrip = strip;

            var btnW = Theme.S(76f);
            var x = bar.width - pad - btnW;

            if (ChromeButton(new Rect(x, ctrlY, btnW, ctrlH), "关闭", Theme.Danger))
                FriendActions.ClosePanel();
            x -= btnW + gap;
            if (ChromeButton(new Rect(x, ctrlY, btnW, ctrlH), "原生面板", Theme.Chip))
                OverlaySession.ToggleMode();
            x -= btnW + gap;
            if (ChromeButton(new Rect(x, ctrlY, btnW, ctrlH), "刷新", Theme.Chip))
                RefreshActive();

            // RequestLastFollower reports no total and no completion, so this tab counts what it has
            // rather than implying the list is whole.
            var countText = Tab == FriendListTab.Followers
                ? "已加载 " + Math.Max(FansListService.Snapshot.Count, shown)
                : "已关注 " + Math.Max(FriendListService.TotalFollowCount, shown);

            // Right-aligned against the action group, well clear of the tabs it used to crowd.
            var countLeft = strip.xMax + Theme.S(16f);
            var countW = x - Theme.S(14f) - countLeft;
            if (countW >= Theme.S(48f))
                Gfx.Text(new Rect(countLeft, ctrlY, countW, ctrlH), countText, Theme.TextMuted, Theme.MetaRight);

            Gfx.Fill(new Rect(0f, bar.yMax - Theme.S(2f), bar.width, Theme.S(2f)), Theme.AccentSoft);

            // Slow light sweep along the accent rule, a quiet "powered" cue.
            var sweepW = Theme.S(90f);
            var sweepX = -sweepW + (bar.width + sweepW * 2f) * Anim.Sweep(6f);
            Gfx.Fill(new Rect(sweepX, bar.yMax - Theme.S(2f), sweepW, Theme.S(2f)), Theme.Accent);
        }

        private static void DrawToolbar(Rect r)
        {
            var gap = Theme.S(10f);
            var sortW = Theme.S(132f);
            var chipsW = SegmentedChips.Width;
            var searchW = Mathf.Max(Theme.S(180f), r.width - chipsW - sortW - gap * 2f);

            _search.Draw(new Rect(r.x, r.y, searchW, r.height));

            var chipsX = r.x + searchW + gap;
            var filter = SegmentedChips.Draw(new Rect(chipsX, r.y, chipsW, r.height), _filter);
            if (filter != _filter)
            {
                _filter = filter;
                _scroll.ScrollTo(0f);
                InvalidateView();
            }

            var sortR = new Rect(r.xMax - sortW, r.y, sortW, r.height);
            if (ChromeButton(sortR, "排序 · " + SortLabel(_sort), Theme.Chip))
                CycleSort();
        }

        private static void DrawStatusBar(Rect r, int shown, int loaded)
        {
            var paging = Tab == FriendListTab.Followers ? FansListService.IsLoading : FriendListService.IsPaging;
            var color = paging ? Theme.StWaiting : Theme.StIdle;
            var alpha = paging ? 0.35f + 0.65f * Anim.Pulse(1.2f) : 1f;

            Gfx.Diamond(
                new Vector2(r.x + Theme.S(5f), r.y + r.height * 0.5f),
                Theme.S(4f),
                new Color(color.r, color.g, color.b, alpha),
                true);

            var text = Tab == FriendListTab.Followers ? FansListService.StatusText : FriendListService.StatusText;
            var status = string.IsNullOrEmpty(text) ? "准备中" : text;

            // Two halves rather than overlapping bands, so the counts cannot run into the status.
            var textX = r.x + Theme.S(16f);
            var half = (r.xMax - textX) * 0.5f;
            Gfx.Text(new Rect(textX, r.y, half, r.height), status, Theme.TextMuted, Theme.Meta);
            Gfx.Text(new Rect(textX + half, r.y, half, r.height),
                "显示 " + shown + " / " + loaded,
                Theme.TextMuted,
                Theme.MetaRight);
        }

        private static void DrawFooter(Rect r, float pad)
        {
            Gfx.Fill(r, Theme.Bg1);
            Gfx.Fill(new Rect(r.x, r.y, r.width, 1f), Theme.LineDim);
            Gfx.Text(new Rect(pad, r.y, r.width - pad * 2f - Theme.S(20f), r.height),
                "拖动标题栏移动  ·  拖边框/四角缩放  ·  ⋯/右键 更多操作  ·  邀请可选战斗类型  ·  " + HotkeyName + " 原生面板  ·  Ctrl+F 搜索  ·  R 刷新  ·  Esc 关闭菜单" +
                (RowCard.ForceLetters ? "  ·  Ctrl+L 字母模式（诊断）" : string.Empty),
                Theme.TextMuted,
                Theme.Meta);

            // Resize grip.
            var grip = new Rect(r.xMax - Theme.S(16f), r.yMax - Theme.S(16f), Theme.S(14f), Theme.S(14f));
            var gripColor = IsResizing || Gfx.Hover(grip) ? Theme.Accent : Theme.Line;
            for (var i = 0; i < 3; i++)
            {
                var o = i * Theme.S(4f);
                Gfx.Fill(new Rect(grip.x + o, grip.yMax - Theme.S(2f), grip.width - o, Theme.S(1f)), gripColor);
                Gfx.Fill(new Rect(grip.xMax - Theme.S(2f), grip.y + o, Theme.S(1f), grip.height - o), gripColor);
            }
        }

        /// <summary>OnGUI runs several times per frame; the query only needs to run once.</summary>
        private static void EnsureView(IReadOnlyList<FriendRowVm> src)
        {
            if (_viewFrame == Time.frameCount)
                return;

            _viewFrame = Time.frameCount;

            // One proxy read per frame: the room can be created or left while the panel is open, but
            // the invite gate must not cost a lookup per row.
            _inRoom = Compat.Capabilities.InviteUserJoin && GameProxies.IsInRoom();

            _view = FriendQueryPipeline.Apply(src, _search.Text, _filter, _sort);
            BuildItems(_view);
        }

        private static void DrawList(Rect listRect, int totalLoaded)
        {
            Gfx.Fill(listRect, Theme.Bg1);
            Gfx.Border(listRect, Theme.LineDim);

            var trackW = Theme.S(8f);
            var inner = new Rect(listRect.x + Theme.S(6f), listRect.y + Theme.S(6f),
                listRect.width - Theme.S(12f) - trackW, listRect.height - Theme.S(12f));

            var contentH = _items.Count == 0
                ? 0f
                : _items[_items.Count - 1].Offset + _items[_items.Count - 1].Height;

            var track = new Rect(listRect.xMax - trackW - Theme.S(3f), inner.y, trackW, inner.height);
            var scrollY = _scroll.Draw(track, inner, contentH);

            if (_items.Count == 0)
            {
                _avatarPriorityIds.Clear();
                _avatarPriorityFrame = Time.frameCount;
                var msg = Tab == FriendListTab.Followers
                    ? (FansListService.IsLoading || totalLoaded == 0 ? "正在拉取关注我的人…" : "没有符合条件的玩家")
                    : (totalLoaded == 0 ? "正在拉取关注列表…" : "没有符合条件的好友");
                Gfx.Text(new Rect(inner.x, inner.y + Theme.S(24f), inner.width, Theme.S(28f)), msg, Theme.TextMuted, Theme.Stat);
                return;
            }

            var offsets = new float[_items.Count];
            var heights = new float[_items.Count];
            for (var i = 0; i < _items.Count; i++)
            {
                offsets[i] = _items[i].Offset;
                heights[i] = _items[i].Height;
            }

            var (a, b) = VisibleRowRange.Compute(scrollY, inner.height, offsets, heights, marginItems: 2);
            _avatarPriorityIds.Clear();
            for (var i = a; i < b; i++)
            {
                var row = _items[i].Row;
                if (row != null)
                    _avatarPriorityIds.Add(row.UserId);
            }

            _avatarPriorityFrame = Time.frameCount;

            var menuOwnerVisible = false;

            for (var i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                var y = inner.y + item.Offset - scrollY;
                if (y + item.Height < inner.y - item.Height || y > inner.yMax + item.Height)
                    continue;

                var rect = new Rect(inner.x, y, inner.width, item.Height);
                var fullyVisible = y >= inner.y - 0.5f && y + item.Height <= inner.yMax + 0.5f;

                if (item.Kind == ItemKind.Header)
                {
                    if (SectionHeader.Draw(rect, item.Section, item.Count, IsCollapsed(item.Section), fullyVisible && _menuRowId == 0))
                        ToggleCollapsed(item.Section);
                    continue;
                }

                var row = item.Row;
                if (row == null)
                    continue;

                var isMenuOwner = _menuRowId == row.UserId;
                var interactive = fullyVisible && (_menuRowId == 0 || isMenuOwner);

                var ctx = BuildContext(row);
                var hoverT = TrackHover(row.UserId, interactive && Gfx.Hover(rect));
                var action = RowCard.Draw(rect, row, ctx, interactive, isMenuOwner, hoverT);
                if (action != RowAction.None)
                    Invoke(action, row);

                // Recomputed after Draw so a menu opened this frame is already anchored correctly.
                if (_menuRowId == row.UserId)
                {
                    menuOwnerVisible = true;
                    var menuH = RowCard.MenuHeightFor(row, ctx);
                    _menuRect = new Rect(
                        rect.xMax - RowCard.MenuWidth - Theme.S(6f),
                        Mathf.Min(rect.yMax - Theme.S(4f), listRect.yMax - menuH - Theme.S(4f)),
                        RowCard.MenuWidth,
                        menuH);
                }
            }

            if (_menuRowId != 0 && !menuOwnerVisible)
                _menuRowId = 0;
        }

        private static void DrawOverflowMenu()
        {
            var row = FindRow(_menuRowId);
            if (row == null)
            {
                _menuRowId = 0;
                return;
            }

            var action = RowCard.DrawMenu(_menuRect, row, BuildContext(row));
            if (action != RowAction.None)
                Invoke(action, row);
            else if (Time.frameCount != _menuOpenedFrame &&
                     Event.current != null &&
                     Event.current.type == EventType.MouseDown &&
                     !_menuRect.Contains(Event.current.mousePosition))
            {
                _menuRowId = 0;
            }
        }

        private static void BuildItems(IReadOnlyList<FriendRowVm> view)
        {
            _items.Clear();
            var offset = 0f;
            var sections = FriendQueryPipeline.GroupIntoSections(
                view,
                Tab == FriendListTab.Following ? PinStore.Pins : null);

            for (var s = 0; s < sections.Count; s++)
            {
                var section = sections[s];
                _items.Add(new ListItem
                {
                    Kind = ItemKind.Header,
                    Section = section.Kind,
                    Count = section.Rows.Count,
                    Offset = offset,
                    Height = SectionHeader.Height,
                });
                offset += SectionHeader.Height;

                if (IsCollapsed(section.Kind))
                    continue;

                for (var i = 0; i < section.Rows.Count; i++)
                {
                    _items.Add(new ListItem
                    {
                        Kind = ItemKind.Row,
                        Section = section.Kind,
                        Row = section.Rows[i],
                        Offset = offset,
                        Height = RowCard.Height,
                    });
                    offset += RowCard.Height + RowCard.Gap;
                }

                offset += Theme.S(4f);
            }
        }

        private static float TrackHover(ulong userId, bool hovered)
        {
            if (!hovered)
                return 0f;

            if (_hoverRowId != userId)
            {
                _hoverRowId = userId;
                _hoverStart = Anim.Now;
            }

            return Anim.Progress(_hoverStart, 0.08f);
        }

        private static RowContext BuildContext(FriendRowVm row) => new RowContext
        {
            Tab = Tab,
            InvitePath = InviteRules.Resolve(
                capability: Compat.Capabilities.InviteUserJoin && GameProxies.Lobby != null,
                canCreateRoom: Compat.Capabilities.CreateRoom,
                inRoom: _inRoom,
                online: row.IsOnline,
                flowBusy: Actions.InviteFlow.Busy),
            InviteRecent = _inviteCooldown.IsActive(row.UserId, Time.unscaledTime),
            InvitePending = Actions.InviteFlow.PendingUserId == row.UserId,
            IsPinned = Tab == FriendListTab.Following && PinStore.Contains(row.UserId),
            PinFull = PinStore.IsFull,
        };

        private static void SwitchTab(FriendListTab tab)
        {
            if (Tab == tab)
                return;

            Tab = tab;
            _menuRowId = 0;
            _scroll.ScrollTo(0f);
            InvalidateView();
            // Gating Tick on visibility keeps the extra RequestOnline traffic out of the common case.
            FansListService.Active = tab == FriendListTab.Followers;
        }

        /// <summary>Segmented tabs size to their label, so a wider CJK caption is never clipped.</summary>
        private static float TabWidth(string label) => Theme.S(30f) + label.Length * Theme.S(18f);

        private static bool TitleTab(Rect r, string label, bool active)
        {
            var hovered = Gfx.Hover(r);

            if (active)
                Gfx.Fill(r, Theme.TitleBarHi);
            else if (hovered)
                Gfx.Fill(r, Theme.ChipHover);

            // The active state is the baseline alone: a chamfered outline here ate the label and
            // broke the shared edge between the two tabs.
            if (active)
                Gfx.Fill(new Rect(r.x, r.yMax - Theme.S(2f), r.width, Theme.S(2f)), Theme.Accent);

            Gfx.Text(r, label, active ? Theme.TextHi : hovered ? Theme.TextMain : Theme.TextMuted, Theme.Tab);
            return Gfx.Hit(r);
        }

        private static void RefreshActive()
        {
            if (Tab == FriendListTab.Followers)
                FansListService.ForceRefresh();
            else
                FriendListService.ForceRefresh();
        }

        private static void Invoke(RowAction action, FriendRowVm row)
        {
            switch (action)
            {
                case RowAction.Join:
                    FriendActions.Join(row);
                    break;
                case RowAction.Watch:
                    FriendActions.Watch(row);
                    break;
                case RowAction.Chat:
                    FriendActions.Chat(row);
                    break;
                case RowAction.Invite:
                    // Mirrors the native button: in a room invites straight away, otherwise the battle
                    // type has to be chosen before a room can exist. Disabled is spelled out rather than
                    // folded into the direct path, so a network call never depends on RowCard having
                    // refused to emit the action.
                    switch (BuildContext(row).InvitePath)
                    {
                        case InvitePath.Picker:
                            ImguiBattleTypePicker.Open(row.UserId, row.Name ?? string.Empty);
                            break;
                        case InvitePath.Direct:
                            if (FriendActions.Invite(row))
                                NoteInviteSent(row.UserId);
                            break;
                    }

                    break;
                case RowAction.ToggleMenu:
                    _menuRowId = _menuRowId == row.UserId ? 0 : row.UserId;
                    _menuOpenedFrame = Time.frameCount;
                    break;
                case RowAction.FollowBack:
                    FriendActions.FollowBack(row);
                    break;
                case RowAction.TogglePin:
                    _menuRowId = 0;
                    if (PinStore.Toggle(row.UserId))
                        InvalidateView();
                    break;
                case RowAction.Unfollow:
                    _menuRowId = 0;
                    ImguiConfirm.Ask("取消关注", "确认取消关注 " + row.Name + " ？", () => FriendActions.Unfollow(row));
                    break;
                case RowAction.Blacklist:
                    _menuRowId = 0;
                    ImguiConfirm.Ask("加入黑名单", "确认拉黑 " + row.Name + " ？", () => FriendActions.Blacklist(row));
                    break;
            }
        }

        private static FriendRowVm? FindRow(ulong userId)
        {
            var snapshot = Source;
            for (var i = 0; i < snapshot.Count; i++)
            {
                if (snapshot[i].UserId == userId)
                    return snapshot[i];
            }

            return null;
        }

        private static bool IsCollapsed(FriendSectionKind kind) => kind switch
        {
            FriendSectionKind.Pinned => CollapsedPinned,
            FriendSectionKind.OnlineJoinable => CollapsedJoinable,
            FriendSectionKind.OnlineBusy => CollapsedBusy,
            _ => CollapsedOffline,
        };

        private static void InvalidateView() => _viewFrame = -1;

        private static void ToggleCollapsed(FriendSectionKind kind)
        {
            InvalidateView();
            switch (kind)
            {
                case FriendSectionKind.Pinned:
                    CollapsedPinned = !CollapsedPinned;
                    break;
                case FriendSectionKind.OnlineJoinable:
                    CollapsedJoinable = !CollapsedJoinable;
                    break;
                case FriendSectionKind.OnlineBusy:
                    CollapsedBusy = !CollapsedBusy;
                    break;
                default:
                    CollapsedOffline = !CollapsedOffline;
                    break;
            }
        }

        private static bool ChromeButton(Rect r, string label, Color bg)
        {
            var hovered = Gfx.Hover(r);
            var cut = Theme.S(5f);
            var fill = hovered
                ? new Color(Mathf.Clamp01(bg.r + 0.14f), Mathf.Clamp01(bg.g + 0.14f), Mathf.Clamp01(bg.b + 0.14f), bg.a)
                : bg;

            Gfx.Chamfer(r, fill, cut);
            Gfx.ChamferBorder(r, hovered ? Theme.Accent : Theme.LineDim, cut);
            Gfx.Text(r, label, Theme.TextHi, Theme.Button);
            return Gfx.Hit(r);
        }

        private static void EnsureWindow()
        {
            if (_windowReady)
                return;

            _windowReady = true;
            _window = new Rect(
                Theme.S(FriendOverlay.Core.OverlayGeometry.DefaultX),
                Theme.S(FriendOverlay.Core.OverlayGeometry.DefaultY),
                Theme.S(FriendOverlay.Core.OverlayGeometry.DefaultW),
                Theme.S(FriendOverlay.Core.OverlayGeometry.DefaultH));
        }

        private static void CaptureChromeDrag()
        {
            var e = Event.current;
            if (e == null)
                return;

            if (e.type == EventType.MouseUp && e.button == 0)
            {
                _dragging = false;
                _resizeEdge = WindowResizeEdge.None;
                return;
            }

            if (e.type != EventType.MouseDown || e.button != 0)
                return;

            if (_tabStrip.Contains(e.mousePosition))
                return;

            var thickness = Theme.S(8f);
            var edge = WindowResizeMath.HitTest(
                e.mousePosition.x, e.mousePosition.y, _window.width, _window.height, thickness);
            if (edge != WindowResizeEdge.None)
            {
                _resizeEdge = edge;
                _resizeStartX = _window.x;
                _resizeStartY = _window.y;
                _resizeStartW = _window.width;
                _resizeStartH = _window.height;
                _resizeStartMouseX = Input.mousePosition.x;
                _resizeStartMouseY = Input.mousePosition.y;
                e.Use();
                return;
            }

            var titleH = Theme.S(52f);
            var title = new Rect(0f, 0f, _window.width - Theme.S(260f), titleH);
            if (title.Contains(e.mousePosition))
            {
                _dragging = true;
                var mp = Input.mousePosition;
                _dragOffX = mp.x - _window.x;
                _dragOffY = Screen.height - mp.y - _window.y;
                e.Use();
            }
        }

        private static void FollowDrag()
        {
            if (!_dragging && !IsResizing)
                return;

            if (!Input.GetMouseButton(0))
            {
                _dragging = false;
                _resizeEdge = WindowResizeEdge.None;
                return;
            }

            var mp = Input.mousePosition;
            if (_dragging)
            {
                _window.x = mp.x - _dragOffX;
                _window.y = Screen.height - mp.y - _dragOffY;
            }
            else
            {
                var dx = mp.x - _resizeStartMouseX;
                // Unity mouse Y is bottom-up; +dy means pointer moved down on screen.
                var dy = _resizeStartMouseY - mp.y;
                var minW = Theme.S(720f);
                var minH = Theme.S(420f);
                var next = WindowResizeMath.ApplyDelta(
                    _resizeEdge,
                    _resizeStartX,
                    _resizeStartY,
                    _resizeStartW,
                    _resizeStartH,
                    dx,
                    dy,
                    minW,
                    minH);
                _window.x = next.X;
                _window.y = next.Y;
                _window.width = next.W;
                _window.height = next.H;
            }

            ClampWindowToScreen();
        }

        private static void ClampWindowToScreen()
        {
            var minW = Theme.S(720f);
            var minH = Theme.S(420f);
            _window.width = Mathf.Clamp(_window.width, minW, Mathf.Max(minW, Screen.width));
            _window.height = Mathf.Clamp(_window.height, minH, Mathf.Max(minH, Screen.height));

            _window.x = Mathf.Clamp(_window.x, -_window.width + Theme.S(120f), Mathf.Max(0f, Screen.width - Theme.S(80f)));
            _window.y = Mathf.Clamp(_window.y, 0f, Mathf.Max(0f, Screen.height - Theme.S(40f)));
        }

        private static void CycleSort()
        {
            InvalidateView();
            _scroll.ScrollTo(0f);
            _sort = _sort switch
            {
                FriendSortKey.Default => FriendSortKey.RankPoint,
                FriendSortKey.RankPoint => FriendSortKey.ForecastPoint,
                FriendSortKey.ForecastPoint => FriendSortKey.Name,
                _ => FriendSortKey.Default,
            };
        }

        private static string SortLabel(FriendSortKey key) => key switch
        {
            FriendSortKey.Name => "名称 A-Z",
            FriendSortKey.RankPoint => "战力 ↓",
            FriendSortKey.ForecastPoint => "洞察 ↓",
            _ => "状态",
        };
    }
}
