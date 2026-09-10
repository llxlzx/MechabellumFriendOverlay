using System;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace FriendOverlay.UI
{
    public static class ImguiConfirm
    {
        private static bool _open;
        private static string _title = string.Empty;
        private static string _body = string.Empty;
        private static Action? _onYes;
        private static Rect _rect;
        private static readonly GUI.WindowFunction? WindowFn =
            DelegateSupport.ConvertDelegate<GUI.WindowFunction>((Action<int>)DrawModal);

        public static bool IsOpen => _open;

        public static void Ask(string title, string body, Action onYes)
        {
            // Two modal windows at once would both grab input; the picker is the one that can be
            // reopened from the row, so it yields.
            ImguiBattleTypePicker.Close();
            _open = true;
            _title = title;
            _body = body;
            _onYes = onYes;
        }

        public static void Close()
        {
            _open = false;
            _onYes = null;
        }

        public static void Draw()
        {
            if (!_open)
                return;

            Theme.EnsureStyles();

            var w = Theme.S(400f);
            var h = Theme.S(170f);
            _rect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

            var prev = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0f, 0f, 0f, 0f);
            GUI.ModalWindow(92002, _rect, WindowFn!, string.Empty);
            GUI.backgroundColor = prev;
        }

        private static void DrawModal(int id)
        {
            try
            {
                var full = new Rect(0f, 0f, _rect.width, _rect.height);
                Gfx.Fill(full, Theme.Bg0);
                Gfx.ScanLines(full, new Color(1f, 1f, 1f, 0.02f), 3f);

                var pad = Theme.S(18f);
                var titleH = Theme.S(32f);
                Gfx.Fill(new Rect(0f, 0f, _rect.width, titleH), Theme.TitleBar);
                Gfx.Fill(new Rect(0f, titleH - Theme.S(2f), _rect.width, Theme.S(2f)), Theme.Danger);
                Gfx.Text(new Rect(pad, 0f, _rect.width - pad * 2f, titleH), _title, Theme.TextHi, Theme.Section);

                Gfx.Text(
                    new Rect(pad, titleH + Theme.S(18f), _rect.width - pad * 2f, Theme.S(28f)),
                    _body,
                    Theme.TextMain,
                    Theme.Stat);

                var btnW = Theme.S(96f);
                var btnH = Theme.S(32f);
                var btnY = _rect.height - btnH - Theme.S(16f);

                if (Btn(new Rect(_rect.width - btnW * 2f - pad - Theme.S(10f), btnY, btnW, btnH), "取消", Theme.Chip))
                    Close();

                if (Btn(new Rect(_rect.width - btnW - pad, btnY, btnW, btnH), "确认", Theme.Danger))
                {
                    var cb = _onYes;
                    Close();
                    cb?.Invoke();
                }

                Gfx.PanelDouble(full, new Color(0f, 0f, 0f, 0f), Theme.Danger, Theme.LineDim);
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Warning("[FriendOverlay] Confirm: " + ex.Message);
                Close();
            }
        }

        private static bool Btn(Rect r, string text, Color bg)
        {
            var hover = Gfx.Hover(r);
            var fill = hover
                ? new Color(Mathf.Clamp01(bg.r + 0.14f), Mathf.Clamp01(bg.g + 0.14f), Mathf.Clamp01(bg.b + 0.14f), bg.a)
                : bg;

            Gfx.Chamfer(r, fill, Theme.S(5f));
            Gfx.ChamferBorder(r, hover ? Theme.Accent : Theme.LineDim, Theme.S(5f));
            Gfx.Text(r, text, Theme.TextHi, Theme.Button);
            return Gfx.Hit(r);
        }
    }
}
