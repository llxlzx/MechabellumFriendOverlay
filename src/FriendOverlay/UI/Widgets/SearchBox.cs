using System;
using System.Runtime.InteropServices;
using FriendOverlay.Core;
using UnityEngine;

namespace FriendOverlay.UI.Widgets
{
    /// <summary>
    /// Hand-rolled text field. GUI.TextField throws on this IL2CPP build, so typing is polled from
    /// Input in OnUpdate (once per frame) and only rendered here. An open IME composition is drawn
    /// but not committed; when it ends, the previous composition is committed unless inputString
    /// already carried the same suffix.
    /// </summary>
    public sealed class SearchBox
    {
        private static string Placeholder => L.T("search.placeholder");

        private float _caretBase;
        private string _composition = string.Empty;

        public string Text { get; private set; } = string.Empty;

        public bool Focused { get; private set; }

        public void Focus()
        {
            Focused = true;
            _caretBase = Anim.Now;
            Input.imeCompositionMode = IMECompositionMode.On;
        }

        public void Blur()
        {
            Focused = false;
            _composition = string.Empty;
            Input.imeCompositionMode = IMECompositionMode.Auto;
        }

        public void Clear() => Text = string.Empty;

        /// <summary>Called once per frame from OnUpdate; OnGUI can run several times per frame.</summary>
        public void HandleInput()
        {
            if (Input.GetKeyDown(KeyCode.F) && CtrlHeld())
            {
                Focus();
                return;
            }

            if (!Focused)
                return;

            Input.imeCompositionMode = IMECompositionMode.On;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (Text.Length > 0)
                    Clear();
                else
                    Blur();
                return;
            }

            if (CtrlHeld() && Input.GetKeyDown(KeyCode.Backspace))
            {
                Clear();
                return;
            }

            if (CtrlHeld() && (Input.GetKeyDown(KeyCode.V) || Input.GetKeyDown(KeyCode.Insert)))
            {
                Commit(SearchText.Apply(Text, pasted: ReadClipboard()));
                return;
            }

            // Shortcuts must not also insert the letter. Composition is handled in Advance.
            if (CtrlHeld())
                return;

            var frame = SearchText.Advance(Text, _composition, Input.compositionString, Input.inputString);
            _composition = frame.Composition;
            Commit(frame.Text);
        }

        public void Draw(Rect r)
        {
            var hovered = Gfx.Hover(r);
            Gfx.Fill(r, Theme.Bg1);
            Gfx.Border(r, Focused ? Theme.Accent : hovered ? Theme.Line : Theme.LineDim);
            if (Focused)
                Gfx.Brackets(r, Theme.Accent, Theme.S(9f), Theme.S(2f));

            var pad = Theme.S(10f);
            var iconR = new Rect(r.x + pad, r.y + r.height * 0.5f - Theme.S(6f), Theme.S(12f), Theme.S(12f));
            Gfx.Border(new Rect(iconR.x, iconR.y, iconR.width, iconR.height), Focused ? Theme.Accent : Theme.TextMuted);
            Gfx.Fill(new Rect(iconR.xMax - 1f, iconR.yMax - 1f, Theme.S(5f), Theme.S(2f)),
                Focused ? Theme.Accent : Theme.TextMuted);

            var shown = Text;
            if (Focused && _composition.Length > 0)
                shown += _composition;

            var textR = new Rect(iconR.xMax + pad, r.y, r.width - iconR.width - pad * 3f - Theme.S(28f), r.height);
            if (shown.Length == 0)
            {
                Gfx.Text(textR, Placeholder, Theme.TextMuted, Theme.Stat);
            }
            else
            {
                Gfx.Text(textR, shown, Theme.TextHi, Theme.Stat);
            }

            if (Focused)
            {
                var caretX = textR.x + MeasureWidth(shown);
                caretX = Mathf.Min(caretX, textR.xMax - 2f);
                // compositionCursorPos is bottom-left screen space; IMGUI y grows downward.
                Input.compositionCursorPos = new Vector2(caretX, Screen.height - (r.y + r.height * 0.5f));
                if (Mathf.Repeat(Anim.Now - _caretBase, 1f) < 0.5f)
                    Gfx.Fill(new Rect(caretX + 1f, r.y + Theme.S(6f), Theme.S(2f), r.height - Theme.S(12f)), Theme.Accent);
            }

            if (Text.Length > 0)
            {
                var clearR = new Rect(r.xMax - Theme.S(26f), r.y + Theme.S(5f), Theme.S(20f), r.height - Theme.S(10f));
                var clearHover = Gfx.Hover(clearR);
                Gfx.Text(clearR, "×", clearHover ? Theme.TextHi : Theme.TextMuted, Theme.Button);
                if (Gfx.Hit(clearR))
                {
                    Clear();
                    Focus();
                }
            }

            if (Event.current != null && Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                if (r.Contains(Event.current.mousePosition))
                    Focus();
                else
                    Blur();
            }
        }

        private void Commit(string next)
        {
            if (next == Text)
                return;

            Text = next;
            _caretBase = Anim.Now;
        }

        private static bool CtrlHeld() =>
            Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

        private static string? ReadClipboard()
        {
            try
            {
                var gui = GUIUtility.systemCopyBuffer;
                if (!string.IsNullOrEmpty(gui))
                    return gui;
            }
            catch
            {
                // IL2CPP often leaves systemCopyBuffer empty; fall through to Win32.
            }

            return ReadWin32Clipboard();
        }

        private static string? ReadWin32Clipboard()
        {
            if (!OpenClipboard(IntPtr.Zero))
                return null;

            try
            {
                var handle = GetClipboardData(13); // CF_UNICODETEXT
                if (handle == IntPtr.Zero)
                    return null;

                var ptr = GlobalLock(handle);
                if (ptr == IntPtr.Zero)
                    return null;

                try
                {
                    return Marshal.PtrToStringUni(ptr);
                }
                finally
                {
                    GlobalUnlock(handle);
                }
            }
            catch
            {
                return null;
            }
            finally
            {
                CloseClipboard();
            }
        }

        private static float MeasureWidth(string text)
        {
            var style = Theme.Stat;
            if (style != null)
            {
                try
                {
                    return style.CalcSize(new GUIContent(text)).x;
                }
                catch
                {
                    // CalcSize can be stripped; fall back to an estimate.
                }
            }

            var width = 0f;
            foreach (var ch in text)
                width += ch > 0x2E80 ? Theme.S(13f) : Theme.S(7f);
            return width;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetClipboardData(uint uFormat);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalUnlock(IntPtr hMem);
    }
}
