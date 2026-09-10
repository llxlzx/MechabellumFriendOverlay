using UnityEngine;

namespace FriendOverlay.UI.Widgets
{
    /// <summary>
    /// Hand-rolled text field. GUI.TextField throws on this IL2CPP build, so typing is polled from
    /// Input in OnUpdate (once per frame) and only rendered here.
    /// </summary>
    public sealed class SearchBox
    {
        private const string Placeholder = "输入名称或 ID…";

        private float _caretBase;

        public string Text { get; private set; } = string.Empty;

        public bool Focused { get; private set; }

        public void Focus()
        {
            Focused = true;
            _caretBase = Anim.Now;
        }

        public void Blur() => Focused = false;

        public void Clear() => Text = string.Empty;

        /// <summary>Called once per frame from OnUpdate; OnGUI can run several times per frame.</summary>
        public void HandleInput()
        {
            if (Input.GetKeyDown(KeyCode.F) &&
                (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
            {
                Focus();
                return;
            }

            if (!Focused)
                return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (Text.Length > 0)
                    Clear();
                else
                    Blur();
                return;
            }

            if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) &&
                Input.GetKeyDown(KeyCode.Backspace))
            {
                Clear();
                return;
            }

            var typed = Input.inputString;
            if (string.IsNullOrEmpty(typed))
                return;

            var text = Text;
            foreach (var ch in typed)
            {
                if (ch == '\b')
                {
                    if (text.Length > 0)
                        text = text.Substring(0, text.Length - 1);
                }
                else if (ch != '\n' && ch != '\r' && !char.IsControl(ch))
                {
                    if (text.Length < 48)
                        text += ch;
                }
            }

            if (text != Text)
            {
                Text = text;
                _caretBase = Anim.Now;
            }
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

            var textR = new Rect(iconR.xMax + pad, r.y, r.width - iconR.width - pad * 3f - Theme.S(28f), r.height);
            if (Text.Length == 0)
            {
                Gfx.Text(textR, Placeholder, Theme.TextMuted, Theme.Stat);
            }
            else
            {
                Gfx.Text(textR, Text, Theme.TextHi, Theme.Stat);
            }

            if (Focused && Mathf.Repeat(Anim.Now - _caretBase, 1f) < 0.5f)
            {
                var caretX = textR.x + MeasureWidth(Text);
                caretX = Mathf.Min(caretX, textR.xMax - 2f);
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
    }
}
