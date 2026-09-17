using System;
using System.Collections.Generic;
using Il2CppGameRiver.Client;
using UnityEngine;

namespace FriendOverlay.UI
{
    /// <summary>
    /// Borrows the game's font and routes each friend row to an avatar source. Every lookup is
    /// optional: a null result means "use the built-in fallback".
    /// </summary>
    public static class GameAssets
    {
        /// <summary>
        /// When true, prefer the in-game friend-row font before Noto/YaHei. When false (default),
        /// resolve Noto → YaHei → game → GUI.skin.
        /// </summary>
        public static bool UseGameFont { get; set; }

        private const float SummaryInterval = 5f;
        private const float ParityRetryInterval = 10f;
        private const int ParityMaxAttempts = 12;

        private static readonly HashSet<ulong> _loggedRoutes = new HashSet<ulong>();
        private static readonly HashSet<ulong> _announce = new HashSet<ulong>();

        private static Font? _font;
        private static bool _fontResolved;
        private static bool _fontLogged;
        private static bool _parityLogged;
        private static int _parityAttempts;
        private static float _nextParityAt;
        private static float _nextSummaryAt;
        private static string _lastSummary = string.Empty;

        public static Font? UiFont
        {
            get
            {
                if (!_fontResolved)
                    ResolveFont();

                return _font;
            }
        }

        public static void Reset()
        {
            _font = null;
            _fontResolved = false;
            _fontLogged = false;
            _parityLogged = false;
            _parityAttempts = 0;
            _nextParityAt = 0f;
            _loggedRoutes.Clear();
            _announce.Clear();
            _nextSummaryAt = 0f;
            _lastSummary = string.Empty;
        }

        /// <summary>Cached avatar image for a friend, or null when nothing has been resolved yet.</summary>
        public static AvatarEntry? GetAvatar(ulong userId) => AvatarCache.Get(userId);

        /// <summary>
        /// Routes every row to exactly one image source and records where rows ended up. Photo rows
        /// use the per-user cache; official avatars, the moderation placeholder and frames go through
        /// the shared, reference-keyed cache because many players share the same asset.
        /// </summary>
        /// <param name="listTag">
        /// Which list these rows came from. The parity check only compares the following list — the
        /// native cells behind the overlay are that list's — and the summary is tagged so two lists
        /// ticking at once cannot make the deduped log line flip back and forth.
        /// </param>
        public static void RequestMissingAvatars(IReadOnlyList<Core.FriendRowVm> rows, string listTag = "following")
        {
            if (string.Equals(listTag, "following", StringComparison.Ordinal))
                LogNativeParityOnce(rows);

            var images = 0;
            var letters = 0;
            var blocked = 0;
            var pending = 0;
            var frames = 0;

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var priority = ImguiFriendOverlay.IsAvatarPriority(row.UserId);

                if (!string.IsNullOrEmpty(row.FrameRef) && SharedImageCache.Get(row.FrameRef) != null)
                    frames++;
                else if (priority && !string.IsNullOrEmpty(row.FrameRef))
                    SharedImageCache.Request(row.FrameRef);

                if (!priority)
                {
                    switch (row.Portrait)
                    {
                        case Core.PortraitKind.Letter:
                            letters++;
                            break;

                        case Core.PortraitKind.Blocked:
                            blocked++;
                            if (SharedImageCache.Get(row.PortraitRef) != null)
                                images++;
                            else
                                letters++;
                            break;

                        case Core.PortraitKind.Official:
                            if (SharedImageCache.Get(row.PortraitRef) != null)
                                images++;
                            else
                                letters++;
                            break;

                        default:
                            if (AvatarCache.IsBlocked(row.UserId))
                            {
                                letters++;
                            }
                            else if (!AvatarCache.NeedsImage(row.UserId))
                            {
                                images++;
                            }
                            else
                            {
                                letters++;
                            }

                            break;
                    }

                    continue;
                }

                switch (row.Portrait)
                {
                    case Core.PortraitKind.Letter:
                        letters++;
                        LogRouteOnce(row, "no portrait");
                        continue;

                    case Core.PortraitKind.Blocked:
                        // Refuse per-user photos for as long as the game hides this face, and show
                        // the placeholder the game substituted instead.
                        AvatarCache.Block(row.UserId);
                        blocked++;
                        if (SharedImageCache.Get(row.PortraitRef) != null)
                        {
                            images++;
                        }
                        else if (SharedImageCache.Request(row.PortraitRef))
                        {
                            pending++;
                        }
                        else
                        {
                            letters++;
                            LogRouteOnce(row, "blocked, placeholder unavailable");
                        }

                        continue;

                    case Core.PortraitKind.Official:
                        if (SharedImageCache.Get(row.PortraitRef) != null)
                        {
                            images++;
                            Announce(row, "official (cached)");
                        }
                        else if (SharedImageCache.Request(row.PortraitRef))
                        {
                            pending++;
                            Announce(row, "official");
                        }
                        else
                        {
                            letters++;
                            LogRouteOnce(row, "official-failed: " + row.PortraitRef);
                        }

                        continue;
                }

                // Photo. The moderation setting can be turned off mid-session; without this the row
                // would refuse every image for the rest of the session.
                if (AvatarCache.IsBlocked(row.UserId))
                {
                    AvatarCache.Unblock(row.UserId);
                    _loggedRoutes.Remove(row.UserId);

                    // Report where this row actually lands, not where we hope it lands.
                    _announce.Add(row.UserId);
                }

                if (!AvatarCache.NeedsImage(row.UserId))
                {
                    images++;
                    Announce(row, "photo (cached)");
                    continue;
                }

                if (AvatarLoader.WillAttempt(row.PortraitRef))
                {
                    AvatarLoader.Request(row.UserId, row.PortraitRef);
                    pending++;
                    Announce(row, "photo");
                    continue;
                }

                letters++;
                LogTerminalRoute(row);
            }

            LogSummary(listTag, images, letters, blocked, pending, frames);
        }

        /// <summary>
        /// Compares every instantiated native cell with our plan for the same player, then reports
        /// agreement counts per kind. This is the positive control for "show whichever the player
        /// picked": a wrong official-vs-photo decision is indistinguishable from a right one without
        /// it. A run that never saw both an Official row and a Photo row proves nothing, so it says
        /// "inconclusive" and keeps trying instead of latching a false pass.
        /// </summary>
        private static void LogNativeParityOnce(IReadOnlyList<Core.FriendRowVm> rows)
        {
            if (_parityLogged)
                return;

            var panel = State.OverlaySession.Panel;
            if (panel == null || rows.Count == 0)
                return;

            // An inconclusive run retries as the list fills in, but bounded: the log is evidence, not
            // a heartbeat.
            if (Anim.Now < _nextParityAt)
                return;

            _nextParityAt = Anim.Now + ParityRetryInterval;
            if (++_parityAttempts > ParityMaxAttempts)
            {
                _parityLogged = true;
                MelonLoader.MelonLogger.Warning(
                    "[FriendOverlay] portrait parity gave up: never saw both an official-avatar row " +
                    "and a photo row (item 23 unverified)");
                return;
            }

            try
            {
                var layout = panel.friendListContentLayout;
                var root = layout?.transform;
                if (root == null || root.childCount == 0)
                    return;

                var byName = new Dictionary<string, Core.FriendRowVm>(StringComparer.Ordinal);
                var ambiguous = new HashSet<string>(StringComparer.Ordinal);
                for (var i = 0; i < rows.Count; i++)
                {
                    var n = rows[i].Name ?? string.Empty;
                    if (n.Length == 0)
                        continue;

                    // Display names are not unique. A duplicate name cannot be matched to one row,
                    // and guessing would let a mismatch pass as agreement.
                    if (!byName.ContainsKey(n))
                        byName[n] = rows[i];
                    else
                        ambiguous.Add(n);
                }

                var compared = 0;
                var agree = 0;
                var official = 0;
                var photo = 0;
                var shown = 0;

                for (var c = 0; c < root.childCount; c++)
                {
                    var cell = root.GetChild(c)?.GetComponent<FriendCellNode>();
                    var label = cell?.nameLabel;
                    var img = cell?.playerImage;
                    if (cell == null || label == null || img == null)
                        continue;

                    var name = label.text ?? string.Empty;
                    if (name.Length == 0 || ambiguous.Contains(name) || !byName.TryGetValue(name, out var row))
                        continue;

                    var nativeAvatar = string.Empty;
                    var nativeOutline = string.Empty;
                    var nativeUrl = string.Empty;
                    var avatarActive = false;
                    var outlineActive = false;
                    try { nativeAvatar = img._avatar ?? string.Empty; } catch { }
                    try { nativeOutline = img._outline ?? string.Empty; } catch { }
                    try { nativeUrl = img.spriteUrl ?? string.Empty; } catch { }
                    try { avatarActive = img.avatarObj != null && img.avatarObj.activeSelf; } catch { }
                    try { outlineActive = img.outlineObj != null && img.outlineObj.activeSelf; } catch { }

                    // A non-empty layer that is switched off is not what the player sees.
                    var nativeShowsAvatar = avatarActive && nativeAvatar.Length > 0;
                    var nativeShowsPhoto = !nativeShowsAvatar && nativeUrl.Length > 0;

                    compared++;
                    if (row.Portrait == Core.PortraitKind.Official)
                        official++;
                    else if (row.Portrait == Core.PortraitKind.Photo)
                        photo++;

                    var ok = row.Portrait switch
                    {
                        Core.PortraitKind.Official => nativeShowsAvatar,
                        Core.PortraitKind.Photo => nativeShowsPhoto,
                        Core.PortraitKind.Blocked => !nativeShowsAvatar,
                        _ => !nativeShowsAvatar && !nativeShowsPhoto,
                    };

                    if (ok)
                    {
                        agree++;
                        continue;
                    }

                    if (shown < 5)
                    {
                        shown++;
                        MelonLoader.MelonLogger.Warning(
                            "[FriendOverlay] portrait parity MISMATCH uid=" + row.UserId +
                            " ours=" + row.Portrait + "|" + row.PortraitRef + "|" + row.FrameRef +
                            " native.avatar=" + nativeAvatar + " active=" + avatarActive +
                            " native.spriteUrl=" + nativeUrl +
                            " native.outline=" + nativeOutline + " active=" + outlineActive);
                    }
                }

                if (compared == 0)
                    return;

                // Both kinds must appear or the comparison never exercised the rule under test.
                var conclusive = official > 0 && photo > 0;
                MelonLoader.MelonLogger.Msg(
                    "[FriendOverlay] portrait parity compared=" + compared +
                    " agree=" + agree +
                    " official=" + official +
                    " photo=" + photo +
                    (conclusive ? " conclusive" : " INCONCLUSIVE (need >=1 official and >=1 photo row)"));

                if (conclusive)
                    _parityLogged = true;
            }
            catch (Exception ex)
            {
                _parityLogged = true;
                MelonLoader.MelonLogger.Msg("[FriendOverlay] portrait parity unavailable: " + ex.Message);
            }
        }

        /// <summary>
        /// Same as <see cref="LogRouteOnce"/>, but the reason costs a URL normalization to build, so
        /// it is only computed for the one frame that actually logs.
        /// </summary>
        private static void LogTerminalRoute(Core.FriendRowVm row)
        {
            _announce.Remove(row.UserId);

            if (!_loggedRoutes.Add(row.UserId))
                return;

            Emit(row, TerminalReason(row));
        }

        /// <summary>Reason tokens carry the failure inline so a route line stands on its own.</summary>
        private static string TerminalReason(Core.FriendRowVm row)
        {
            if (!AvatarLoader.Enabled)
                return "loader-disabled";

            return AvatarLoader.TryGetFailure(row.PortraitRef, out var error)
                ? "photo-failed: " + error
                : "photo-failed";
        }

        /// <summary>Logs the branch a formerly blocked or lettered row actually took.</summary>
        private static void Announce(Core.FriendRowVm row, string token)
        {
            if (_announce.Remove(row.UserId))
                MelonLoader.MelonLogger.Msg(
                    "[FriendOverlay] avatar route uid=" + row.UserId + " -> " + token);
        }

        private static void LogRouteOnce(Core.FriendRowVm row, string reason)
        {
            _announce.Remove(row.UserId);

            if (!_loggedRoutes.Add(row.UserId))
                return;

            Emit(row, reason);
        }

        private static void Emit(Core.FriendRowVm row, string reason)
        {
            MelonLoader.MelonLogger.Msg(
                "[FriendOverlay] avatar route uid=" + row.UserId +
                " name=" + (string.IsNullOrEmpty(row.Name) ? "?" : row.Name) +
                " -> letter (" + reason + ")");
        }

        private static void LogSummary(string listTag, int images, int letters, int blocked, int pending, int frames)
        {
            if (Anim.Now < _nextSummaryAt)
                return;

            _nextSummaryAt = Anim.Now + SummaryInterval;

            // images + letters equals the row count once pending reaches zero; blocked rows are
            // counted inside images when their placeholder loaded, inside letters otherwise.
            // Tagged with the list: two lists tick while the followers tab is open, and an untagged
            // line would differ every call and defeat the dedupe below.
            var summary =
                listTag +
                " images=" + images +
                " letters=" + letters +
                " blocked=" + blocked +
                " pending=" + pending +
                " frames=" + frames +
                " failedUrls=" + AvatarLoader.FailedUrlCount;

            if (string.Equals(summary, _lastSummary, StringComparison.Ordinal))
                return;

            _lastSummary = summary;
            MelonLoader.MelonLogger.Msg("[FriendOverlay] avatars " + summary);
        }

        /// <summary>
        /// Allow a second resolve after lobby cells exist (game-font borrow may have been null).
        /// </summary>
        public static void InvalidateFontResolve()
        {
            _font = null;
            _fontResolved = false;
        }

        private static void ResolveFont()
        {
            _fontResolved = true;
            _font = null;

            Core.UiFontFaceKind kind;
            if (UseGameFont)
            {
                if (TryBorrowGameFont(out _font))
                    kind = Core.UiFontFaceKind.Game;
                else if ((_font = EmbeddedFontLoader.TryCreateNoto()) != null)
                    kind = Core.UiFontFaceKind.Noto;
                else if ((_font = EmbeddedFontLoader.TryCreateYahei()) != null)
                    kind = Core.UiFontFaceKind.YaHei;
                else
                    kind = Core.UiFontFaceKind.Skin;
            }
            else if ((_font = EmbeddedFontLoader.TryCreateNoto()) != null)
            {
                kind = Core.UiFontFaceKind.Noto;
            }
            else if ((_font = EmbeddedFontLoader.TryCreateYahei()) != null)
            {
                kind = Core.UiFontFaceKind.YaHei;
            }
            else if (TryBorrowGameFont(out _font))
            {
                kind = Core.UiFontFaceKind.Game;
            }
            else
            {
                kind = Core.UiFontFaceKind.Skin;
            }

            if (!_fontLogged)
            {
                _fontLogged = true;
                MelonLoader.MelonLogger.Msg(
                    "[FriendOverlay] UI font face=" + kind +
                    (_font != null ? " name=" + _font.name : " (GUI.skin)"));
            }
        }

        private static bool TryBorrowGameFont(out Font? font)
        {
            font = null;
            var cell = FindFirstCell();
            if (cell == null)
                return false;

            try
            {
                var label = cell.nameLabel;
                if (label != null && label.font != null)
                {
                    font = label.font;
                    return true;
                }
            }
            catch
            {
                // fall through
            }

            return false;
        }

        internal static FriendCellNode? FindFirstCell()
        {
            var panel = State.OverlaySession.Panel;
            if (panel == null)
                return null;

            try
            {
                var layout = panel.friendListContentLayout;
                if (layout == null)
                    return null;

                var root = layout.transform;
                if (root == null)
                    return null;

                for (var i = 0; i < root.childCount; i++)
                {
                    var child = root.GetChild(i);
                    if (child == null)
                        continue;

                    var cell = child.GetComponent<FriendCellNode>();
                    if (cell != null)
                        return cell;
                }
            }
            catch
            {
                // panel torn down mid-frame
            }

            return null;
        }
    }
}
