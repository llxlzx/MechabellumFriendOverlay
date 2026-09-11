using System;
using System.Collections.Generic;
using Il2CppGameRiver;
using Il2CppGameRiver.Client;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

namespace FriendOverlay.UI
{
    /// <summary>
    /// Native Steam / WeGame badge art, baked to owned textures so IMGUI never samples atlas UVs
    /// (Gfx.Sprite on platform sprites showed solid color blocks).
    /// </summary>
    public static class PlatformIcons
    {
        private static readonly Dictionary<int, Texture2D> _byPlatform = new Dictionary<int, Texture2D>();
        private static int _harvestTries;
        private static bool _logged;
        private static bool? _ignoreSteam;
        private static float _nextTry;

        public static void Reset()
        {
            foreach (var tex in _byPlatform.Values)
            {
                if (tex == null)
                    continue;
                try { UnityEngine.Object.Destroy(tex); } catch { /* ok */ }
            }

            _byPlatform.Clear();
            _harvestTries = 0;
            _logged = false;
            _ignoreSteam = null;
            _nextTry = 0f;
        }

        public static bool IgnoreSteamIcon()
        {
            if (_ignoreSteam.HasValue)
                return _ignoreSteam.Value;

            try
            {
                var platform = GRGame.Instance?.Platform;
                _ignoreSteam = platform != null && platform.ignoreSteamIcon;
            }
            catch
            {
                _ignoreSteam = false;
            }

            return _ignoreSteam.Value;
        }

        public static Texture2D? Get(int platform)
        {
            if (platform == 1 && IgnoreSteamIcon())
                return null;

            if (_byPlatform.TryGetValue(platform, out var tex) && AvatarCache.IsUsableTexture(tex))
                return tex;

            TryHarvest();
            return _byPlatform.TryGetValue(platform, out tex) && AvatarCache.IsUsableTexture(tex)
                ? tex
                : null;
        }

        private static void TryHarvest()
        {
            if (_byPlatform.Count > 0 || _harvestTries >= 12)
                return;

            if (Time.unscaledTime < _nextTry)
                return;

            _nextTry = Time.unscaledTime + 1f;

            try
            {
                FriendCellNode? cell = null;
                try { cell = GameAssets.FindFirstCell(); } catch { cell = null; }

                if (cell == null || cell.platFormObjs == null || cell.platFormObjs.Count == 0)
                {
                    try
                    {
                        var all = Resources.FindObjectsOfTypeAll<FriendCellNode>();
                        if (all != null)
                        {
                            for (var i = 0; i < all.Length; i++)
                            {
                                var c = all[i];
                                if (c == null || c.platFormObjs == null || c.platFormObjs.Count == 0)
                                    continue;
                                cell = c;
                                break;
                            }
                        }
                    }
                    catch
                    {
                        // FindObjectsOfTypeAll unavailable
                    }
                }

                if (cell == null)
                    return;

                _harvestTries++;
                var list = cell.platFormObjs!;
                for (var i = 0; i < list.Count; i++)
                {
                    var para = list[i];
                    if (para == null)
                        continue;

                    var key = (int)para.platForm;
                    var sprite = ReadSprite(para.activeObj);
                    if (!AvatarCache.IsUsableSprite(sprite))
                        continue;

                    var baked = SpriteBake.ToTexture(sprite);
                    if (!AvatarCache.IsUsableTexture(baked))
                        continue;

                    if (_byPlatform.TryGetValue(key, out var old) && old != null && old != baked)
                    {
                        try { UnityEngine.Object.Destroy(old); } catch { /* ok */ }
                    }

                    _byPlatform[key] = baked!;
                }

                if (!_logged && _byPlatform.Count > 0)
                {
                    _logged = true;
                    MelonLogger.Msg("[FriendOverlay] platform icons baked: " + _byPlatform.Count);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[FriendOverlay] platform icon harvest failed: " + ex.Message);
            }
        }

        private static Sprite? ReadSprite(GameObject? root)
        {
            if (root == null)
                return null;

            try
            {
                var images = root.GetComponentsInChildren<Image>(true);
                if (images == null)
                    return null;

                Sprite? best = null;
                var bestDepth = -1;
                for (var i = 0; i < images.Length; i++)
                {
                    var img = images[i];
                    if (img == null || !AvatarCache.IsUsableSprite(img.sprite))
                        continue;

                    var depth = 0;
                    try
                    {
                        var t = img.transform;
                        while (t != null && t != root.transform)
                        {
                            depth++;
                            t = t.parent;
                        }
                    }
                    catch
                    {
                        depth = 0;
                    }

                    if (depth >= bestDepth)
                    {
                        bestDepth = depth;
                        best = img.sprite;
                    }
                }

                return best;
            }
            catch
            {
                return null;
            }
        }
    }
}
