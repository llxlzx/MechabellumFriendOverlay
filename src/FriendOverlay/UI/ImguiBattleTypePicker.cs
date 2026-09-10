using System;
using FriendOverlay.Core;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace FriendOverlay.UI
{
    /// <summary>
    /// The overlay's stand-in for the native 「邀请参与的战斗类型」 window: picking a row creates that kind of
    /// room and invites once it exists. Rows whose native call has not been captured yet render as
    /// 暂未开放 rather than guessing a room type.
    /// </summary>
    public static class ImguiBattleTypePicker
    {
        private static bool _open;
        private static ulong _userId;
        private static string _name = string.Empty;
        private static Rect _rect;
        private static readonly GUI.WindowFunction? WindowFn =
            DelegateSupport.ConvertDelegate<GUI.WindowFunction>((Action<int>)DrawModal);

        public static bool IsOpen => _open;

        public static void Open(ulong userId, string name)
        {
            // Two modal windows at once would both grab input; the confirm dialog loses.
            ImguiConfirm.Close();
            _open = true;
            _userId = userId;
            _name = string.IsNullOrEmpty(name) ? "#" + userId : name;
        }

        public static void Close()
        {
            _open = false;
            _userId = 0UL;
            _name = string.Empty;
        }

        public static void Draw()
        {
            if (!_open)
                return;

            Theme.EnsureStyles();

            var rowH = Theme.S(34f);
            var gap = Theme.S(6f);
            var w = Theme.S(360f);
            var h = Theme.S(32f) + Theme.S(16f) + BattleTypeCatalog.Entries.Count * (rowH + gap) +
                    Theme.S(10f) + rowH + Theme.S(16f);
            _rect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

            var prev = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0f, 0f, 0f, 0f);
            GUI.ModalWindow(92003, _rect, WindowFn!, string.Empty);
            GUI.backgroundColor = prev;
        }

        private static void DrawModal(int id)
        {
            try
            {
                var full = new Rect(0f, 0f, _rect.width, _rect.height);
                Gfx.Fill(full, Theme.Bg0);
                Gfx.ScanLines(full, new Color(1f, 1f, 1f, 0.02f), 3f);

                var pad = Theme.S(16f);
                var titleH = Theme.S(32f);
                Gfx.Fill(new Rect(0f, 0f, _rect.width, titleH), Theme.TitleBar);
                Gfx.Fill(new Rect(0f, titleH - Theme.S(2f), _rect.width, Theme.S(2f)), Theme.Accent);
                Gfx.Text(
                    new Rect(pad, 0f, _rect.width - pad * 2f, titleH),
                    "邀请 " + _name + " 参与",
                    Theme.TextHi,
                    Theme.Section);

                var rowH = Theme.S(34f);
                var gap = Theme.S(6f);
                var y = titleH + Theme.S(16f);

                var entries = BattleTypeCatalog.Entries;
                for (var i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    var enabled = IsOffered(entry);
                    var label = entry.Verified ? entry.Label : entry.Label + "（暂未开放）";

                    if (Btn(new Rect(pad, y, _rect.width - pad * 2f, rowH), label, Theme.Chip, enabled))
                        Pick(entry);

                    y += rowH + gap;
                }

                y += Theme.S(4f);
                if (Btn(new Rect(_rect.width - pad - Theme.S(96f), y, Theme.S(96f), rowH), "取消", Theme.Chip, true))
                    Close();

                Gfx.PanelDouble(full, new Color(0f, 0f, 0f, 0f), Theme.Accent, Theme.LineDim);
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Warning("[FriendOverlay] BattleTypePicker: " + ex.Message);
                Close();
            }
        }

        private static bool IsOffered(BattleType entry)
        {
            if (!entry.Verified)
                return false;

            return entry.IsTeamMatch
                ? Compat.Capabilities.TeamInvite
                : Compat.Capabilities.CreateRoom;
        }

        private static void Pick(BattleType entry)
        {
            var userId = _userId;
            Close();

            // The window can sit open for a while, and the player may have landed in a room meanwhile.
            // Creating a second one then would be wrong, so fall back to the direct invite.
            if (Data.GameProxies.IsInRoom())
            {
                if (Actions.FriendActions.InviteUserJoin(userId))
                    ImguiFriendOverlay.NoteInviteSent(userId);
                return;
            }

            if (entry.IsTeamMatch)
            {
                // Fire and forget: the game reports nothing back, so the row is marked as soon as the
                // call did not throw.
                if (Actions.FriendActions.InviteTeam(userId))
                    ImguiFriendOverlay.NoteInviteSent(userId);
                return;
            }

            // Marking happens when the invite actually goes out, which is several seconds later.
            Actions.InviteFlow.Start(userId, entry);
        }

        private static bool Btn(Rect r, string text, Color bg, bool enabled)
        {
            var hover = enabled && Gfx.Hover(r);
            var fill = hover
                ? new Color(Mathf.Clamp01(bg.r + 0.14f), Mathf.Clamp01(bg.g + 0.14f), Mathf.Clamp01(bg.b + 0.14f), bg.a)
                : bg;

            if (!enabled)
                fill = new Color(fill.r, fill.g, fill.b, fill.a * 0.4f);

            Gfx.Chamfer(r, fill, Theme.S(5f));
            Gfx.ChamferBorder(r, enabled ? (hover ? Theme.Accent : Theme.LineDim) : new Color(Theme.LineDim.r, Theme.LineDim.g, Theme.LineDim.b, 0.4f), Theme.S(5f));
            Gfx.Text(
                r,
                text,
                enabled ? Theme.TextHi : new Color(Theme.TextMuted.r, Theme.TextMuted.g, Theme.TextMuted.b, 0.5f),
                Theme.Button);

            return enabled && Gfx.Hit(r);
        }
    }
}
