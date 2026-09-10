using System;
using UnityEngine;

namespace FriendOverlay.UI.Widgets
{
    /// <summary>
    /// Vertical scrollbar. GUI.BeginScrollView is unusable on this build, so the offset is owned
    /// here and applied by the caller when it positions rows.
    /// </summary>
    public sealed class ScrollBar
    {
        private const float WheelStep = 40f;

        private bool _dragging;
        private float _dragStartMouseY;
        private float _dragStartValue;

        public float Value { get; private set; }

        public float Max { get; private set; }

        public bool Dragging => _dragging;

        public void ScrollBy(float delta) => Value = Mathf.Clamp(Value + delta, 0f, Max);

        public void ScrollTo(float value) => Value = Mathf.Clamp(value, 0f, Max);

        public void Reset()
        {
            Value = 0f;
            _dragging = false;
        }

        /// <summary>
        /// Handles wheel input over <paramref name="viewport"/>, then draws the track inside
        /// <paramref name="track"/>. Returns the clamped scroll offset.
        /// </summary>
        public float Draw(Rect track, Rect viewport, float contentHeight)
        {
            Max = Mathf.Max(0f, contentHeight - viewport.height);

            var e = Event.current;
            if (e != null && e.type == EventType.ScrollWheel && viewport.Contains(e.mousePosition))
            {
                ScrollBy(e.delta.y * WheelStep * 0.35f);
                e.Use();
            }

            Value = Mathf.Clamp(Value, 0f, Max);

            if (Max <= 0.01f)
            {
                Gfx.Fill(track, new Color(Theme.LineDim.r, Theme.LineDim.g, Theme.LineDim.b, 0.35f));
                return Value;
            }

            var hoveredTrack = Gfx.Hover(track);
            var width = hoveredTrack || _dragging ? track.width : Mathf.Max(2f, track.width * 0.5f);
            var visual = new Rect(track.xMax - width, track.y, width, track.height);
            Gfx.Fill(visual, new Color(Theme.LineDim.r, Theme.LineDim.g, Theme.LineDim.b, 0.5f));

            var ratio = Mathf.Clamp01(viewport.height / contentHeight);
            var thumbH = Mathf.Max(Theme.S(28f), track.height * ratio);
            var travel = track.height - thumbH;
            var t = Max <= 0f ? 0f : Value / Max;
            var thumb = new Rect(visual.x, track.y + travel * t, visual.width, thumbH);

            var thumbHover = Gfx.Hover(thumb);
            Gfx.Fill(thumb, _dragging || thumbHover ? Theme.Accent : Theme.Line);

            if (e != null && e.type == EventType.MouseDown && e.button == 0 && track.Contains(e.mousePosition))
            {
                if (thumb.Contains(e.mousePosition))
                {
                    _dragging = true;
                    _dragStartMouseY = Input.mousePosition.y;
                    _dragStartValue = Value;
                }
                else
                {
                    var page = e.mousePosition.y < thumb.y ? -viewport.height : viewport.height;
                    ScrollBy(page);
                }

                e.Use();
            }

            if (_dragging)
            {
                if (!Input.GetMouseButton(0))
                {
                    _dragging = false;
                }
                else if (travel > 0.01f)
                {
                    // Input y grows upwards, GUI y grows downwards.
                    var deltaGui = _dragStartMouseY - Input.mousePosition.y;
                    ScrollTo(_dragStartValue + deltaGui / travel * Max);
                }
            }

            return Value;
        }

        public void HandleKeys(float viewportHeight)
        {
            if (Input.GetKeyDown(KeyCode.PageUp))
                ScrollBy(-viewportHeight);
            if (Input.GetKeyDown(KeyCode.PageDown))
                ScrollBy(viewportHeight);
            if (Input.GetKeyDown(KeyCode.Home))
                ScrollTo(0f);
            if (Input.GetKeyDown(KeyCode.End))
                ScrollTo(float.MaxValue);
        }
    }
}
