using System;
using FriendOverlay.Core;
using MelonLoader;
using UnityEngine;

namespace FriendOverlay.UI
{
    /// <summary>
    /// Turns a game UI <see cref="Sprite"/> into an owned <see cref="Texture2D"/> that IMGUI can draw.
    /// Official faces/frames live in large non-readable atlases; we blit the atlas once and ReadPixels
    /// the sprite rect. textureRect uses bottom-left origin — matching ReadPixels after Graphics.Blit
    /// on this build (flipY caused the multi-tile collage / garbage avatars in 0.3.16).
    /// </summary>
    public static class SpriteBake
    {
        private static bool _loggedFail;
        private static bool _loggedOk;
        private static bool _loggedGetPixelsFail;

        public static Texture2D? ToTexture(Sprite? sprite)
        {
            if (!AvatarCache.IsUsableSprite(sprite))
                return null;

            try
            {
                var src = sprite!.texture as Texture2D;
                if (src == null)
                    return null;

                var tr = sprite.textureRect;
                var x = Mathf.Clamp(Mathf.FloorToInt(tr.x), 0, src.width - 1);
                var y = Mathf.Clamp(Mathf.FloorToInt(tr.y), 0, src.height - 1);
                var w = Mathf.Clamp(Mathf.RoundToInt(tr.width), 1, src.width - x);
                var h = Mathf.Clamp(Mathf.RoundToInt(tr.height), 1, src.height - y);

                var tex = TryGetPixels(src, x, y, w, h, sprite.name);
                if (tex == null)
                    tex = TryBlitCrop(src, x, y, w, h, sprite.name);

                return tex;
            }
            catch (Exception ex)
            {
                LogFail(ex.Message);
                return null;
            }
        }

        public static void Reset()
        {
            _loggedFail = false;
            _loggedOk = false;
            _loggedGetPixelsFail = false;
        }

        private static Texture2D? TryGetPixels(Texture2D src, int x, int y, int w, int h, string name)
        {
            try
            {
                var pixels = src.GetPixels(x, y, w, h);
                if (pixels == null || pixels.Length != w * h)
                    return null;

                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                tex.SetPixels(pixels);
                tex.Apply(false, false);
                LogOk(name, w, h, src.width, src.height, "getpixels");
                return tex;
            }
            catch (Exception ex)
            {
                if (!_loggedGetPixelsFail)
                {
                    _loggedGetPixelsFail = true;
                    MelonLogger.Msg("[FriendOverlay] sprite GetPixels unavailable, using blit crop: " + ex.Message);
                }

                return null;
            }
        }

        private static Texture2D? TryBlitCrop(Texture2D src, int x, int y, int w, int h, string name)
        {
            BakeBudget.BeginFrame(Time.frameCount);
            if (!BakeBudget.TryConsume())
                return null;

            RenderTexture? full = null;
            RenderTexture? prev = null;
            Texture2D? a = null;
            Texture2D? b = null;
            try
            {
                full = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32);
                prev = RenderTexture.active;
                Graphics.Blit(src, full);
                RenderTexture.active = full;

                // Prefer bottom-origin (matches textureRect). If that crop is mostly empty, try flipY.
                a = ReadCrop(full, x, y, w, h);
                var scoreA = Score(a);
                var scoreB = -1f;
                var flipY = src.height - y - h;
                if (flipY >= 0 && flipY != y)
                {
                    b = ReadCrop(full, x, flipY, w, h);
                    scoreB = Score(b);
                }

                Texture2D chosen;
                string via;
                if (b != null && scoreB > scoreA * 1.15f)
                {
                    if (a != null)
                    {
                        try { UnityEngine.Object.Destroy(a); } catch { /* ok */ }
                        a = null;
                    }

                    chosen = b;
                    b = null;
                    via = "blit-crop-flipY";
                }
                else
                {
                    if (b != null)
                    {
                        try { UnityEngine.Object.Destroy(b); } catch { /* ok */ }
                        b = null;
                    }

                    if (a == null)
                        return null;

                    chosen = a;
                    a = null;
                    via = "blit-crop";
                }

                LogOk(name, w, h, src.width, src.height, via);
                return chosen;
            }
            catch (Exception ex)
            {
                if (a != null)
                {
                    try { UnityEngine.Object.Destroy(a); } catch { /* ok */ }
                }

                if (b != null)
                {
                    try { UnityEngine.Object.Destroy(b); } catch { /* ok */ }
                }

                LogFail(ex.Message);
                return null;
            }
            finally
            {
                try { RenderTexture.active = prev; } catch { /* ok */ }
                if (full != null)
                {
                    try { RenderTexture.ReleaseTemporary(full); } catch { /* ok */ }
                }
            }
        }

        private static Texture2D? ReadCrop(RenderTexture full, int x, int y, int w, int h)
        {
            Texture2D? tex = null;
            try
            {
                tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                tex.ReadPixels(new Rect(x, y, w, h), 0, 0);
                tex.Apply(false, false);
                return tex;
            }
            catch
            {
                if (tex != null)
                {
                    try { UnityEngine.Object.Destroy(tex); } catch { /* ok */ }
                }

                return null;
            }
        }

        /// <summary>Opaque, non-flat pixels — empty/wrong crops score near zero; real faces/frames score higher.</summary>
        private static float Score(Texture2D? tex)
        {
            if (tex == null)
                return -1f;

            try
            {
                var pixels = tex.GetPixels32();
                if (pixels == null || pixels.Length == 0)
                    return 0f;

                var step = Math.Max(1, pixels.Length / 256);
                var opaque = 0;
                long r = 0, g = 0, b = 0;
                for (var i = 0; i < pixels.Length; i += step)
                {
                    var p = pixels[i];
                    if (p.a < 16)
                        continue;
                    opaque++;
                    r += p.r;
                    g += p.g;
                    b += p.b;
                }

                if (opaque < 4)
                    return opaque;

                // Penalize near-solid single-color crops (wrong atlas tile / badge plate).
                var inv = 1.0 / opaque;
                var ar = r * inv;
                var ag = g * inv;
                var ab = b * inv;
                double var = 0;
                for (var i = 0; i < pixels.Length; i += step)
                {
                    var p = pixels[i];
                    if (p.a < 16)
                        continue;
                    var dr = p.r - ar;
                    var dg = p.g - ag;
                    var db = p.b - ab;
                    var += dr * dr + dg * dg + db * db;
                }

                return opaque + (float)(var / opaque) * 0.01f;
            }
            catch
            {
                return 0f;
            }
        }

        private static void LogOk(string name, int w, int h, int atlasW, int atlasH, string via)
        {
            if (_loggedOk)
                return;

            _loggedOk = true;
            MelonLogger.Msg(
                "[FriendOverlay] sprite bake ok via " + via + ": " + name +
                " " + w + "x" + h + " from " + atlasW + "x" + atlasH);
        }

        private static void LogFail(string message)
        {
            if (_loggedFail)
                return;

            _loggedFail = true;
            MelonLogger.Warning("[FriendOverlay] sprite bake unavailable: " + message);
        }
    }
}
