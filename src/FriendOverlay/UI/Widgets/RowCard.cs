using FriendOverlay.Core;
using UnityEngine;

namespace FriendOverlay.UI.Widgets
{
    public enum RowAction
    {
        None = 0,
        Join = 1,
        Watch = 2,
        Chat = 3,
        ToggleMenu = 4,
        Unfollow = 5,
        Blacklist = 6,
        Invite = 7,
        TogglePin = 8,
        FollowBack = 9,
    }

    public enum FriendListTab
    {
        Following = 0,
        Followers = 1,
    }

    /// <summary>Per-row facts the card cannot derive from the row itself.</summary>
    public struct RowContext
    {
        public FriendListTab Tab;
        public InvitePath InvitePath;
        public bool InviteRecent;

        /// <summary>This row's create-room invite is still waiting for the room.</summary>
        public bool InvitePending;
        public bool IsPinned;
        public bool PinFull;
    }

    public static class RowCard
    {
        /// <summary>
        /// Session-only diagnostic toggle (Ctrl+L). Without it the letter fallback has no positive
        /// control: a healthy list never renders one, so "no letters" and "letters are broken" look
        /// identical.
        /// </summary>
        public static bool ForceLetters { get; set; }

        public static float Height => Theme.S(72f);

        public static float Gap => Theme.S(6f);

        public static float MenuWidth => Theme.S(150f);

        public static float MoreWidth => Theme.S(30f);

        public static float ActionsHeight => Theme.S(30f);

        private static float FollowBackWidth => Theme.S(72f);

        private static int MenuItemCount(FriendRowVm row, RowContext ctx) =>
            ctx.Tab == FriendListTab.Followers ? (row.IsMutual ? 2 : 1) : 3;

        /// <summary>Menu height depends on which items the row actually offers.</summary>
        public static float MenuHeightFor(FriendRowVm row, RowContext ctx) =>
            Theme.S(34f) * MenuItemCount(row, ctx) + Theme.S(8f);

        public static RowAction Draw(Rect r, FriendRowVm row, RowContext ctx, bool interactive, bool menuOpen, float hoverT)
        {
            var action = RowAction.None;
            var statusColor = Theme.StatusColor(row.StatusKind);
            var hovered = (interactive && Gfx.Hover(r)) || menuOpen;
            var cut = Theme.S(6f);

            // Right-click anywhere on the row is a second, discoverable way into the same menu.
            var e = Event.current;
            if (interactive && e != null && e.type == EventType.MouseDown && e.button == 1 && r.Contains(e.mousePosition))
            {
                e.Use();
                action = RowAction.ToggleMenu;
            }

            Gfx.Chamfer(r, hovered ? Theme.CardHover : Theme.Card, cut);
            Gfx.ChamferBorder(r, hovered ? Theme.Line : Theme.LineDim, cut);
            if (hovered)
                Gfx.Brackets(r, Theme.Accent, Theme.S(10f) * Mathf.Max(0.2f, hoverT), Theme.S(2f));

            Gfx.Fill(new Rect(r.x, r.y + cut, Theme.S(3f), r.height - cut * 2f), statusColor);

            var pad = Theme.S(12f);
            var avatarSize = r.height - Theme.S(18f);
            var avatarR = new Rect(r.x + pad, r.y + Theme.S(9f), avatarSize, avatarSize);
            DrawAvatar(avatarR, row, statusColor);

            var textX = avatarR.xMax + pad;
            var rightLimit = r.xMax - pad;
            var actionsW = ActionsWidth(ctx);
            var statusW = Theme.S(110f);
            var restingW = statusW + Theme.S(6f) + MoreWidth;

            // The status diamond sits left of the pill (or of the action buttons) and is wide enough
            // now that the name and stats have to stop short of it.
            var dotSpace = Theme.S(28f);
            var textW = Mathf.Max(Theme.S(80f), rightLimit - textX - (hovered ? actionsW : restingW) - dotSpace);

            var nameX = textX;
            if (row.IsMutual)
            {
                var mutualR = new Rect(nameX, r.y + Theme.S(10f), Theme.S(16f), Theme.S(22f));
                Gfx.Text(mutualR, "⇌", Theme.Accent, Theme.Name);
                nameX += Theme.S(20f);
            }

            var name = string.IsNullOrEmpty(row.Name) ? "#" + row.UserId : row.Name;
            Gfx.Text(new Rect(nameX, r.y + Theme.S(9f), Mathf.Max(Theme.S(60f), textW - (nameX - textX)), Theme.S(24f)),
                name,
                Theme.TextHi,
                Theme.Name);

            DrawStats(new Rect(textX, r.y + Theme.S(37f), textW, Theme.S(22f)), row);

            var actionsY = r.y + (r.height - ActionsHeight) * 0.5f;
            if (hovered)
            {
                var actionsR = new Rect(rightLimit - actionsW, actionsY, actionsW, ActionsHeight);
                var hoverAction = DrawActions(actionsR, row, ctx, menuOpen);
                if (hoverAction != RowAction.None)
                    action = hoverAction;

                // The action buttons take the label's place, so without this the status disappears
                // exactly while the cursor is on the row the player is deciding about.
                Gfx.Diamond(
                    new Vector2(actionsR.x - Theme.S(14f), r.y + r.height * 0.5f),
                    Theme.S(7f),
                    statusColor,
                    row.StatusKind != FriendStatusKind.Offline);
            }
            else
            {
                // The overflow button stays visible at rest; otherwise unfollow and block are
                // invisible until the cursor happens to land on the row.
                // Must stay dead on non-interactive rows, otherwise a row masked by the chrome or
                // sitting under an open menu would swallow those clicks.
                var moreR = new Rect(rightLimit - MoreWidth, actionsY, MoreWidth, ActionsHeight);
                if (Button(moreR, "⋯", Theme.Chip, interactive, false, subdued: true) && action == RowAction.None)
                    action = RowAction.ToggleMenu;

                // Tinted pill behind the label, same idiom as the section count badge. Coloured text
                // alone was too thin a signal to read at a glance down a long list. The pill hugs its
                // label and keeps its right edge on the column, so the marker still forms a clean
                // vertical line whatever the labels are.
                var dotLead = Theme.S(26f);
                var chipH = Theme.S(22f);
                var chipW = Mathf.Min(statusW, dotLead + LabelWidth(row.StatusLabel) + Theme.S(10f));
                var chipR = new Rect(
                    moreR.x - Theme.S(6f) - chipW,
                    r.y + (r.height - chipH) * 0.5f,
                    chipW,
                    chipH);

                Gfx.RoundRect(chipR, new Color(statusColor.r, statusColor.g, statusColor.b, 0.18f), chipH * 0.5f);
                Gfx.Text(new Rect(chipR.x + dotLead, chipR.y, chipR.width - dotLead - Theme.S(10f), chipR.height),
                    row.StatusLabel,
                    statusColor,
                    Theme.StatusRight);

                Gfx.Diamond(
                    new Vector2(chipR.x + Theme.S(14f), r.y + r.height * 0.5f),
                    Theme.S(7f),
                    statusColor,
                    row.StatusKind != FriendStatusKind.Offline);
            }

            return action;
        }

        /// <summary>
        /// Overflow menu, drawn after the list so it stacks above other rows. 关注我的人 never offers
        /// pin, and only offers 取消关注 for people the player actually follows back (mutual).
        /// </summary>
        public static RowAction DrawMenu(Rect r, FriendRowVm row, RowContext ctx)
        {
            Gfx.Fill(r, Theme.Bg1);
            Gfx.Border(r, Theme.Line);

            var itemH = Theme.S(34f);
            var item = new Rect(r.x + Theme.S(4f), r.y + Theme.S(4f), r.width - Theme.S(8f), itemH);

            if (ctx.Tab == FriendListTab.Following)
            {
                // Unpinning stays enabled at the cap, otherwise a full set could never be reduced.
                var pinLabel = ctx.IsPinned
                    ? L.T("menu.unpin")
                    : ctx.PinFull
                        ? L.Tf("menu.pin_full", Core.PinnedIds.Max)
                        : L.T("menu.pin");
                if (MenuItem(item, pinLabel, Theme.TextMain, ctx.IsPinned || !ctx.PinFull))
                    return RowAction.TogglePin;
                item.y += itemH;
            }

            if (ctx.Tab == FriendListTab.Following || row.IsMutual)
            {
                if (MenuItem(item, L.T("menu.unfollow"), Theme.TextMain, true))
                    return RowAction.Unfollow;
                item.y += itemH;
            }

            if (MenuItem(item, L.T("menu.blacklist"), Theme.DangerHover, true))
                return RowAction.Blacklist;

            return RowAction.None;
        }

        /// <summary>
        /// Rough advance width for the status label. GUIStyle.CalcSize is one more API this IL2CPP
        /// build may not carry, and a pill background does not need to be pixel exact — CJK glyphs are
        /// about square at this size, Latin ones about half.
        /// </summary>
        private static float LabelWidth(string? label)
        {
            if (string.IsNullOrEmpty(label))
                return 0f;

            var w = 0f;
            foreach (var ch in label!)
                w += ch > 0x2E80 ? Theme.S(15f) : Theme.S(9f);

            return w;
        }

        public static string FormatNum(int n)
        {
            if (n >= 1_000_000)
                return (n / 1_000_000f).ToString("0.0") + "M";
            return n.ToString("N0");
        }

        private static bool MenuItem(Rect r, string label, Color color, bool enabled)
        {
            var hovered = enabled && Gfx.Hover(r);
            if (hovered)
                Gfx.Fill(r, Theme.ChipHover);

            var text = enabled ? color : new Color(Theme.TextMuted.r, Theme.TextMuted.g, Theme.TextMuted.b, 0.5f);
            Gfx.Text(new Rect(r.x + Theme.S(10f), r.y, r.width - Theme.S(20f), r.height), label, text, Theme.Button);
            return enabled && Gfx.Hit(r);
        }

        private static float ActionsWidth(RowContext ctx)
        {
            var w = Theme.S(52f);
            var gap = Theme.S(5f);
            var width = w * 4f + MoreWidth + gap * 4f;
            if (ctx.Tab == FriendListTab.Followers)
                width += FollowBackWidth + gap;
            return width;
        }

        private static RowAction DrawActions(Rect r, FriendRowVm row, RowContext ctx, bool menuOpen)
        {
            var action = RowAction.None;
            var w = Theme.S(52f);
            var gap = Theme.S(5f);
            var x = r.x;

            if (ctx.Tab == FriendListTab.Followers)
            {
                var followR = new Rect(x, r.y, FollowBackWidth, r.height);
                if (row.IsMutual)
                    Button(followR, L.T("btn.mutual"), Theme.Chip, false, false);
                else if (Button(followR, L.T("btn.follow_back"), Theme.Chip, true, true))
                    action = RowAction.FollowBack;
                x += FollowBackWidth + gap;
            }

            var canJoin = row.IsOnline && !row.IsBusy;
            var canWatch = row.IsOnline && row.IsBusy;

            if (Button(new Rect(x, r.y, w, r.height), L.T("btn.join"), Theme.Chip, canJoin, ctx.Tab == FriendListTab.Following))
                action = RowAction.Join;
            x += w + gap;

            if (Button(new Rect(x, r.y, w, r.height), L.T("btn.watch"), Theme.Chip, canWatch, false))
                action = RowAction.Watch;
            x += w + gap;

            if (Button(new Rect(x, r.y, w, r.height), L.T("btn.chat"), Theme.Chip, true, false))
                action = RowAction.Chat;
            x += w + gap;

            // Disabled rather than hidden, so the player can see inviting exists and learn it needs
            // a room, instead of wondering where the button went.
            var inviteLabel = ctx.InvitePending
                ? L.T("btn.inviting")
                : ctx.InviteRecent
                    ? L.T("btn.invited")
                    : L.T("btn.invite");
            if (Button(new Rect(x, r.y, w, r.height), inviteLabel, Theme.Chip,
                    ctx.InvitePath != Core.InvitePath.Disabled && !ctx.InviteRecent, false))
                action = RowAction.Invite;
            x += w + gap;

            var moreR = new Rect(x, r.y, MoreWidth, r.height);
            if (Button(moreR, "⋯", menuOpen ? Theme.ChipHover : Theme.Chip, true, false))
                action = RowAction.ToggleMenu;

            return action;
        }

        private static bool Button(Rect r, string label, Color bg, bool enabled, bool primary, bool subdued = false)
        {
            var hovered = enabled && Gfx.Hover(r);
            var cut = Theme.S(4f);
            var fill = hovered
                ? Theme.ChipHover
                : primary && enabled
                    ? Theme.AccentFaint
                    : bg;

            if (!enabled)
                fill = new Color(fill.r, fill.g, fill.b, fill.a * 0.4f);
            else if (subdued && !hovered)
                fill = new Color(fill.r, fill.g, fill.b, fill.a * 0.55f);

            Gfx.Chamfer(r, fill, cut);
            var border = !enabled
                ? new Color(Theme.LineDim.r, Theme.LineDim.g, Theme.LineDim.b, 0.4f)
                : hovered
                    ? Theme.Accent
                    : primary
                        ? Theme.Line
                        : Theme.LineDim;

            if (enabled && subdued && !hovered)
                border = new Color(border.r, border.g, border.b, border.a * 0.6f);

            Gfx.ChamferBorder(r, border, cut);

            var text = enabled ? Theme.TextHi : new Color(Theme.TextMuted.r, Theme.TextMuted.g, Theme.TextMuted.b, 0.5f);
            if (enabled && subdued && !hovered)
                text = Theme.TextMuted;
            Gfx.Text(r, label, text, Theme.Button);

            if (!enabled)
                return false;

            return Gfx.Hit(r);
        }

        private static void DrawAvatar(Rect r, FriendRowVm row, Color statusColor)
        {
            Gfx.Fill(r, Theme.Bg1);

            Texture2D? texture = null;
            Sprite? sprite = null;
            if (!ForceLetters)
            {
                if (row.Portrait == PortraitKind.Photo)
                {
                    var entry = GameAssets.GetAvatar(row.UserId);
                    if (entry != null)
                    {
                        texture = AvatarCache.IsUsableTexture(entry.Texture) ? entry.Texture : null;
                        sprite = texture == null && AvatarCache.IsUsableSprite(entry.Sprite) ? entry.Sprite : null;
                    }
                }
                else if (row.Portrait == PortraitKind.Official || row.Portrait == PortraitKind.Blocked)
                {
                    var shared = SharedImageCache.Get(row.PortraitRef);
                    texture = SharedImageCache.CurrentTexture(shared);
                    // Official atlas sprites are never drawn via Gfx.Sprite (collage risk).
                }
            }

            var hasImage = texture != null;

            // Letter first, image on top. An opaque avatar hides it; a fully transparent or
            // zero-pixel image leaves it showing. Dimension checks cannot tell those apart, and a
            // washed-out glyph behind a cosmetic icon beats an empty box.
            var glyph = hasImage
                ? new Color(Theme.TextMuted.r, Theme.TextMuted.g, Theme.TextMuted.b, Theme.TextMuted.a * 0.5f)
                : Theme.TextMuted;
            Gfx.Text(r, string.IsNullOrEmpty(row.Name) ? "#" : row.Name.Substring(0, 1), glyph, Theme.Avatar);

            var inner = new Rect(r.x + 1f, r.y + 1f, r.width - 2f, r.height - 2f);
            if (texture != null)
                Gfx.Texture(inner, texture, Color.white);
            // Official atlas sprites must not use Gfx.Sprite — IMGUI UVs show multi-tile collages.
            else if (sprite != null && row.Portrait == PortraitKind.Photo)
                Gfx.Sprite(inner, sprite, Color.white);

            Gfx.Border(r, statusColor);

            // Frame sits above the status border like the native outline layer; Ctrl+L hides it too.
            if (!ForceLetters)
                DrawFrame(r, row.FrameRef);

            var badge = Theme.S(14f);
            var badgeR = new Rect(r.xMax - badge, r.yMax - badge, badge, badge);
            var platformTex = PlatformIcons.Get(row.Platform);
            if (platformTex != null)
            {
                Gfx.Texture(badgeR, platformTex, Color.white);
            }
            else if (!(row.Platform == 1 && PlatformIcons.IgnoreSteamIcon()))
            {
                Gfx.Fill(badgeR, Theme.PlatformColor(row.Platform));
                Gfx.Border(badgeR, Theme.Bg0);
            }
        }

        private static void DrawFrame(Rect r, string frameRef)
        {
            var frame = SharedImageCache.Get(frameRef);
            if (frame == null)
                return;

            var grow = Theme.S(4f);
            var fr = new Rect(r.x - grow, r.y - grow, r.width + grow * 2f, r.height + grow * 2f);
            var texture = SharedImageCache.CurrentTexture(frame);
            if (texture != null)
                Gfx.Texture(fr, texture, Color.white);
            // No Gfx.Sprite fallback for frames — atlas sprites collage under IMGUI.
        }

        private static void DrawStats(Rect r, FriendRowVm row)
        {
            var iconSize = Theme.S(9f);
            var y = r.y + r.height * 0.5f;
            var x = r.x;

            // Power: stacked bars, echoing the native fist/might glyph.
            Gfx.Fill(new Rect(x, y - iconSize * 0.5f, iconSize, Theme.S(2f)), Theme.TextMuted);
            Gfx.Fill(new Rect(x, y - Theme.S(1f), iconSize, Theme.S(2f)), Theme.TextMuted);
            Gfx.Fill(new Rect(x, y + iconSize * 0.5f - Theme.S(2f), iconSize * 0.6f, Theme.S(2f)), Theme.TextMuted);
            x += iconSize + Theme.S(6f);

            var powerW = Theme.S(74f);
            Gfx.Text(new Rect(x, r.y, powerW, r.height), FormatNum(row.RankPoint), Theme.TextMain, Theme.Stat);
            x += powerW + Theme.S(8f);

            if (x + Theme.S(70f) > r.xMax)
                return;

            Gfx.Diamond(new Vector2(x + iconSize * 0.5f, y), Theme.S(5f), Theme.TextMuted, false);
            x += iconSize + Theme.S(8f);

            Gfx.Text(new Rect(x, r.y, r.xMax - x, r.height), FormatNum(row.ForecastPoint), Theme.TextMain, Theme.Stat);
        }
    }
}
