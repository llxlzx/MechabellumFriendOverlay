using System;
using UnityEngine;

namespace FriendOverlay.UI
{
    /// <summary>
    /// Absolute-rect drawing primitives built only on GUI.DrawTexture / GUI.Label / GUI.Button.
    /// Rounded and chamfered shapes are rasterized as 1px scanline strips so they never depend on
    /// IMGUI features that IL2CPP may have stripped.
    /// </summary>
    public static class Gfx
    {
        /// <summary>Multiplied into every color, used for panel fade-in.</summary>
        public static float GlobalAlpha { get; set; } = 1f;

        /// <summary>True when the extended DrawTexture overload with border radii did not throw.</summary>
        public static bool NativeRoundingProbed { get; private set; }

        public static bool NativeRoundingAvailable { get; private set; }

        public static void ProbeOnce()
        {
            if (NativeRoundingProbed)
                return;

            NativeRoundingProbed = true;
            try
            {
                GUI.DrawTexture(
                    new Rect(-8f, -8f, 1f, 1f),
                    Texture2D.whiteTexture,
                    ScaleMode.StretchToFill,
                    true,
                    0f,
                    new Color(0f, 0f, 0f, 0f),
                    Vector4.zero,
                    new Vector4(2f, 2f, 2f, 2f));
                NativeRoundingAvailable = true;
            }
            catch (Exception ex)
            {
                NativeRoundingAvailable = false;
                MelonLoader.MelonLogger.Msg("[FriendOverlay] rounded DrawTexture unavailable, using raster fallback: " + ex.Message);
            }
        }

        private static Color Apply(Color c) =>
            GlobalAlpha >= 0.999f ? c : new Color(c.r, c.g, c.b, c.a * GlobalAlpha);

        public static void Fill(Rect r, Color color)
        {
            if (r.width <= 0f || r.height <= 0f)
                return;

            var prev = GUI.color;
            GUI.color = Apply(color);
            var tex = Texture2D.whiteTexture;
            if (tex != null)
            {
                GUI.DrawTexture(r, tex);
            }
            else
            {
                var bg = GUI.backgroundColor;
                GUI.backgroundColor = Apply(color);
                GUI.Box(r, string.Empty);
                GUI.backgroundColor = bg;
            }

            GUI.color = prev;
        }

        public static void HLine(float x, float y, float w, Color color, float thickness = 1f) =>
            Fill(new Rect(x, y, w, thickness), color);

        public static void VLine(float x, float y, float h, Color color, float thickness = 1f) =>
            Fill(new Rect(x, y, thickness, h), color);

        public static void Border(Rect r, Color color, float thickness = 1f)
        {
            Fill(new Rect(r.x, r.y, r.width, thickness), color);
            Fill(new Rect(r.x, r.yMax - thickness, r.width, thickness), color);
            Fill(new Rect(r.x, r.y + thickness, thickness, r.height - thickness * 2f), color);
            Fill(new Rect(r.xMax - thickness, r.y + thickness, thickness, r.height - thickness * 2f), color);
        }

        public static void Panel(Rect r, Color fill, Color line, float thickness = 1f)
        {
            Fill(r, fill);
            Border(r, line, thickness);
        }

        /// <summary>Mechabellum-style double outline: dim outer ring plus a brighter inner edge.</summary>
        public static void PanelDouble(Rect r, Color fill, Color inner, Color outer)
        {
            Fill(r, fill);
            Border(r, outer);
            Border(new Rect(r.x + 1f, r.y + 1f, r.width - 2f, r.height - 2f), inner);
        }

        /// <summary>Filled rect with 45 degree cuts at top-left and bottom-right.</summary>
        public static void Chamfer(Rect r, Color color, float cut)
        {
            if (r.width <= 0f || r.height <= 0f)
                return;

            cut = Mathf.Clamp(cut, 0f, Mathf.Min(r.width, r.height) * 0.5f);
            if (cut < 1f)
            {
                Fill(r, color);
                return;
            }

            var steps = Mathf.RoundToInt(cut);
            var body = new Rect(r.x, r.y + steps, r.width, Mathf.Max(0f, r.height - steps * 2f));
            Fill(body, color);

            for (var i = 0; i < steps; i++)
            {
                var inset = steps - i;
                Fill(new Rect(r.x + inset, r.y + i, r.width - inset, 1f), color);
                Fill(new Rect(r.x, r.yMax - 1f - i, r.width - inset, 1f), color);
            }
        }

        public static void ChamferBorder(Rect r, Color color, float cut, float thickness = 1f)
        {
            cut = Mathf.Clamp(cut, 0f, Mathf.Min(r.width, r.height) * 0.5f);
            var c = Mathf.RoundToInt(cut);

            Fill(new Rect(r.x + c, r.y, r.width - c, thickness), color);
            Fill(new Rect(r.x, r.yMax - thickness, r.width - c, thickness), color);
            Fill(new Rect(r.x, r.y + c, thickness, r.height - c), color);
            Fill(new Rect(r.xMax - thickness, r.y, thickness, r.height - c), color);

            for (var i = 0; i < c; i++)
            {
                Fill(new Rect(r.x + c - i - 1f, r.y + i, thickness + 1f, thickness), color);
                Fill(new Rect(r.xMax - c + i - thickness, r.yMax - i - thickness, thickness + 1f, thickness), color);
            }
        }

        public static void RoundRect(Rect r, Color color, float radius)
        {
            if (r.width <= 0f || r.height <= 0f)
                return;

            radius = Mathf.Clamp(radius, 0f, Mathf.Min(r.width, r.height) * 0.5f);
            if (radius < 1f)
            {
                Fill(r, color);
                return;
            }

            if (NativeRoundingAvailable)
            {
                try
                {
                    GUI.DrawTexture(
                        r,
                        Texture2D.whiteTexture,
                        ScaleMode.StretchToFill,
                        true,
                        0f,
                        // Apply, not color: this path bypasses Fill, so without it rounded fills ignore
                        // the fade-in and pop in at full opacity while everything else is still fading.
                        Apply(color),
                        Vector4.zero,
                        new Vector4(radius, radius, radius, radius));
                    return;
                }
                catch
                {
                    NativeRoundingAvailable = false;
                }
            }

            var steps = Mathf.RoundToInt(radius);
            Fill(new Rect(r.x, r.y + steps, r.width, Mathf.Max(0f, r.height - steps * 2f)), color);

            for (var i = 0; i < steps; i++)
            {
                var dy = radius - i - 0.5f;
                var dx = radius - Mathf.Sqrt(Mathf.Max(0f, radius * radius - dy * dy));
                var w = r.width - dx * 2f;
                if (w <= 0f)
                    continue;

                Fill(new Rect(r.x + dx, r.y + i, w, 1f), color);
                Fill(new Rect(r.x + dx, r.yMax - i - 1f, w, 1f), color);
            }
        }

        /// <summary>Diamond marker; hollow variant is used for offline presence.</summary>
        public static void Diamond(Vector2 center, float radius, Color color, bool filled)
        {
            var steps = Mathf.Max(1, Mathf.RoundToInt(radius));
            for (var i = -steps; i <= steps; i++)
            {
                var half = steps - Mathf.Abs(i);
                if (half <= 0f)
                    continue;

                var y = center.y + i;
                if (filled || Mathf.Abs(i) >= steps - 1)
                {
                    Fill(new Rect(center.x - half, y, half * 2f, 1f), color);
                }
                else
                {
                    Fill(new Rect(center.x - half, y, 1f, 1f), color);
                    Fill(new Rect(center.x + half - 1f, y, 1f, 1f), color);
                }
            }
        }

        /// <summary>Four corner brackets, used for hover and focus affordances.</summary>
        public static void Brackets(Rect r, Color color, float len, float thickness = 2f)
        {
            if (len <= 0f)
                return;

            len = Mathf.Min(len, Mathf.Min(r.width, r.height) * 0.5f);

            Fill(new Rect(r.x, r.y, len, thickness), color);
            Fill(new Rect(r.x, r.y, thickness, len), color);

            Fill(new Rect(r.xMax - len, r.y, len, thickness), color);
            Fill(new Rect(r.xMax - thickness, r.y, thickness, len), color);

            Fill(new Rect(r.x, r.yMax - thickness, len, thickness), color);
            Fill(new Rect(r.x, r.yMax - len, thickness, len), color);

            Fill(new Rect(r.xMax - len, r.yMax - thickness, len, thickness), color);
            Fill(new Rect(r.xMax - thickness, r.yMax - len, thickness, len), color);
        }

        public static void Glow(Rect r, Color color, int layers = 3)
        {
            for (var i = 1; i <= layers; i++)
            {
                var a = color.a * (1f - i / (float)(layers + 1));
                var c = new Color(color.r, color.g, color.b, a);
                Border(new Rect(r.x - i, r.y - i, r.width + i * 2f, r.height + i * 2f), c);
            }
        }

        public static void ScanLines(Rect r, Color color, float spacing = 3f)
        {
            if (spacing < 1f)
                spacing = 1f;

            for (var y = r.y; y < r.yMax; y += spacing)
                Fill(new Rect(r.x, y, r.width, 1f), color);
        }

        /// <summary>Horizontal line that fades out towards the right.</summary>
        public static void FadeLine(Rect r, Color color, int segments = 12)
        {
            if (segments < 1 || r.width <= 0f)
                return;

            var segW = r.width / segments;
            for (var i = 0; i < segments; i++)
            {
                var a = color.a * (1f - i / (float)segments);
                Fill(new Rect(r.x + segW * i, r.y, segW + 1f, r.height),
                    new Color(color.r, color.g, color.b, a));
            }
        }

        public static void Text(Rect r, string text, Color color, GUIStyle? style)
        {
            if (string.IsNullOrEmpty(text))
                return;

            var prev = GUI.contentColor;
            GUI.contentColor = Apply(color);
            if (style != null)
                GUI.Label(r, text, style);
            else
                GUI.Label(r, text);
            GUI.contentColor = prev;
        }

        /// <summary>Transparent click target; visuals are drawn by the caller.</summary>
        public static bool Hit(Rect r)
        {
            var style = Theme.Invisible;
            if (style != null)
                return GUI.Button(r, string.Empty, style);

            try
            {
                return GUI.Button(r, string.Empty, GUIStyle.none);
            }
            catch
            {
                // Last resort: the default skin draws a visible box, but the control still works.
                return GUI.Button(r, string.Empty);
            }
        }

        public static bool Hover(Rect r)
        {
            var e = Event.current;
            if (e == null)
                return false;

            return r.Contains(e.mousePosition);
        }

        /// <summary>Returns false when nothing was submitted, so callers can log the miss.</summary>
        public static bool Sprite(Rect r, Sprite? sprite, Color tint)
        {
            if (sprite == null)
                return false;

            var prev = GUI.color;
            try
            {
                var tex = sprite.texture;
                if (tex == null)
                    return false;

                var tr = sprite.textureRect;
                var coords = new Rect(
                    tr.x / tex.width,
                    tr.y / tex.height,
                    tr.width / tex.width,
                    tr.height / tex.height);

                GUI.color = Apply(tint);
                GUI.DrawTextureWithTexCoords(r, tex, coords);
                return true;
            }
            catch
            {
                // Sprite atlases can be unloaded mid-session; skip silently.
                return false;
            }
            finally
            {
                // A throw between the two assignments would otherwise tint the rest of the frame.
                GUI.color = prev;
            }
        }

        /// <summary>Returns false when nothing was submitted, so callers can log the miss.</summary>
        public static bool Texture(Rect r, Texture2D? tex, Color tint)
        {
            if (tex == null)
                return false;

            var prev = GUI.color;
            try
            {
                GUI.color = Apply(tint);
                GUI.DrawTexture(r, tex);
                return true;
            }
            catch
            {
                // texture destroyed between frames
                return false;
            }
            finally
            {
                GUI.color = prev;
            }
        }
    }
}
