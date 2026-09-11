using System;
using System.Collections.Generic;
using FriendOverlay.Core;
using Il2CppGameRiver.Client;
using Il2CppInterop.Runtime;
using MelonLoader;
using UnityEngine;

namespace FriendOverlay.UI
{
    /// <summary>
    /// Resolves official avatar / frame keys the way the native list does.
    /// <list type="number">
    /// <item><c>getGif</c> — animated keys such as <c>Avtr_G_*</c> already store <c>Sprite[]</c>.</item>
    /// <item><c>GRImage.SetPlayerPortrait</c> — instantiates the prefab under <c>avatarObj</c> /
    /// <c>outlineObj</c>; we then read a drawable sprite off that instance (not off the asset).</item>
    /// </list>
    /// Bare keys are not URLs and are not in the untyped sprite tables; <c>LoadSprite</c> /
    /// <c>DownloadImage</c> paths stay reserved for real http(s) refs only.
    /// </summary>
    public static class GameSpriteLoader
    {
        private static readonly HashSet<string> _pending = new HashSet<string>(StringComparer.Ordinal);
        private static readonly List<object> _alive = new List<object>();

        private static GameObject? _host;
        private static GRImage? _image;
        private static bool _hostFailed;
        private static bool _loggedNoManager;
        private static bool _loggedLoadSpriteFail;
        private static bool _loggedPortraitFail;
        private static bool _loggedOutlineMiss;
        private static int _hitLogs;

        /// <summary>
        /// True when a load finished or is in flight. False means the registry is not ready —
        /// the caller must retry, not Fail the entry. Official keys are returned as owned
        /// <see cref="Texture2D"/> arrays (length 1 for static, N for GIF); the caller destroys them.
        /// </summary>
        public static bool TryLoad(string imageRef, SpriteManager? sm, Action<Texture2D[]?> done)
        {
            if (string.IsNullOrEmpty(imageRef))
                return false;

            if (_pending.Contains(imageRef))
                return true;

            if (IsHttp(imageRef))
            {
                if (sm == null)
                    return false;

                return TryLoadSprite(imageRef, sm, sprite => done(SingleOrNull(SpriteBake.ToTexture(sprite))));
            }

            var mgr = TryGetManager();
            if (mgr == null)
                return false;

            try
            {
                LogGifDiagOnce(mgr);

                // Battlefield/emote sheets (bf-*) only — Avtr_G_* is NOT in gifInfos.
                if (!OfficialImageKeys.IsLikelyGifKey(imageRef) &&
                    !imageRef.StartsWith("Avtr_", StringComparison.OrdinalIgnoreCase) &&
                    !OfficialImageKeys.IsOutlineKey(imageRef) &&
                    TryGifBake(mgr, imageRef, out var sheetFrames))
                {
                    LiveGifHost.LastFrameSeconds = GifPlayback.DefaultFrameSeconds;
                    LogHit("game avatar gif-sheet: " + imageRef + " frames=" + sheetFrames!.Length);
                    done(sheetFrames);
                    return true;
                }

                // Animated portraits (Avtr_G_*) and animated frames (Af_*) both come from a prefab that
                // may carry GRGif. Try the prefab first for either: compose flattens one arbitrary
                // moment and, for frames, was latching a sparse partial ring as a success.
                if (OfficialImageKeys.IsLikelyGifKey(imageRef) || OfficialImageKeys.IsOutlineKey(imageRef))
                {
                    var result = LiveGifHost.TryBakeFrames(mgr, imageRef, out var fromPrefab);
                    if (result == LiveGifHost.PrefabResult.Ready && fromPrefab != null && fromPrefab.Length > 0)
                    {
                        string tag;
                        if (fromPrefab.Length > 1)
                            tag = OfficialImageKeys.IsOutlineKey(imageRef) ? "outline-gif-root" : "prefab-gif-root";
                        else
                            tag = OfficialImageKeys.IsOutlineKey(imageRef) ? "outline-static-root" : "portrait-static-root";
                        LogHit("game avatar " + tag + ": " + imageRef + " frames=" + fromPrefab.Length);
                        done(fromPrefab);
                        return true;
                    }

                    if (result == LiveGifHost.PrefabResult.Pending)
                    {
                        done(null);
                        return true;
                    }

                    // Budget deferred: do not fall through to compose (would steal SoftAttempts).
                    if (LiveGifHost.LastBudgetDeferred)
                    {
                        done(null);
                        return true;
                    }
                }

                if (TryPortraitCompose(imageRef, out var composed))
                {
                    var tag = OfficialImageKeys.IsOutlineKey(imageRef) ? "outline-static" : "portrait";
                    LogHit("game avatar " + tag + " capture-root: " + imageRef + " frames=1");
                    done(SingleOrNull(composed));
                    return true;
                }

                if (OfficialImageKeys.IsOutlineKey(imageRef))
                    LogOutlineMissOnce(mgr, imageRef);

                done(null);
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[FriendOverlay] GRAvatarManager lookup failed: " + ex.Message);
                done(null);
                return true;
            }
        }

        private static Texture2D[]? SingleOrNull(Texture2D? tex) =>
            AvatarCache.IsUsableTexture(tex) ? new[] { tex! } : null;

        public static void Reset()
        {
            _pending.Clear();
            _alive.Clear();
            _loggedNoManager = false;
            _loggedLoadSpriteFail = false;
            _loggedPortraitFail = false;
            _loggedOutlineMiss = false;
            _loggedGifDiag = false;
            _hitLogs = 0;

            try
            {
                if (_host != null)
                    UnityEngine.Object.Destroy(_host);
            }
            catch
            {
                // scene already tearing down
            }

            _host = null;
            _image = null;
            _hostFailed = false;
        }

        private static GRAvatarManager? TryGetManager()
        {
            try
            {
                var mgr = GRAvatarManager.Instance;
                if (mgr != null)
                    return mgr;
            }
            catch
            {
                // binding / not awake
            }

            if (!_loggedNoManager)
            {
                _loggedNoManager = true;
                MelonLogger.Warning("[FriendOverlay] GRAvatarManager not ready yet");
            }

            return null;
        }

        private static bool _loggedGifDiag;

        private static bool TryGifBake(GRAvatarManager mgr, string imageRef, out Texture2D[]? frames)
        {
            frames = null;
            try
            {
                foreach (var key in OfficialImageKeys.ExactVariants(imageRef))
                {
                    Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Sprite>? sprites = null;
                    try { sprites = mgr.getGif(key); } catch { sprites = null; }

                    if ((sprites == null || sprites.Length == 0) && mgr.gifInfos != null)
                        sprites = LookupGifFrames(mgr, key);

                    if ((sprites == null || sprites.Length == 0) && mgr._gifInfo != null)
                        sprites = LookupGifPara(mgr._gifInfo, key);

                    if (sprites == null || sprites.Length == 0)
                        continue;

                    var cap = OverlayPerfSettings.TargetGifFrameCount(sprites.Length);
                    var list = new List<Texture2D>(cap);
                    for (var i = 0; i < cap; i++)
                    {
                        if (!AvatarCache.IsUsableSprite(sprites[i]))
                            continue;

                        var baked = SpriteBake.ToTexture(sprites[i]);
                        if (baked != null)
                            list.Add(baked);
                    }

                    if (list.Count == 0)
                        continue;

                    frames = list.ToArray();
                    return true;
                }
            }
            catch
            {
                // key not in gif tables
            }

            return false;
        }

        private static Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Sprite>? LookupGifFrames(
            GRAvatarManager mgr,
            string imageRef)
        {
            try
            {
                var dict = mgr.gifInfos;
                if (dict == null)
                    return null;

                if (dict.ContainsKey(imageRef))
                    return dict[imageRef];

                foreach (var key in dict.Keys)
                {
                    if (key != null && OfficialImageKeys.ExactEquals(key, imageRef))
                        return dict[key];
                }
            }
            catch
            {
                // dictionary shape changed
            }

            return null;
        }

        private static Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Sprite>? LookupGifPara(
            GifInfo info,
            string imageRef)
        {
            try
            {
                var list = info.gifParas;
                if (list == null)
                    return null;

                for (var i = 0; i < list.Count; i++)
                {
                    var para = list[i];
                    if (para == null || string.IsNullOrEmpty(para.name))
                        continue;

                    if (OfficialImageKeys.ExactEquals(para.name, imageRef))
                        return para.sprites;
                }
            }
            catch
            {
                // GifPara shape changed
            }

            return null;
        }

        private static void LogGifDiagOnce(GRAvatarManager mgr)
        {
            if (_loggedGifDiag)
                return;

            _loggedGifDiag = true;
            try
            {
                var count = 0;
                var sample = string.Empty;
                if (mgr.gifInfos != null)
                {
                    count = mgr.gifInfos.Count;
                    foreach (var key in mgr.gifInfos.Keys)
                    {
                        if (key == null)
                            continue;
                        if (sample.Length == 0 || OfficialImageKeys.IsLikelyGifKey(key))
                            sample = key;
                        if (OfficialImageKeys.IsLikelyGifKey(key))
                            break;
                    }
                }

                MelonLogger.Msg(
                    "[FriendOverlay] gifInfos count=" + count +
                    " sample=" + (string.IsNullOrEmpty(sample) ? "(none)" : sample) +
                    " (portrait GIFs use getAvatar, not gifInfos)");

                try
                {
                    var probe = mgr.getAvatar("Avtr_G_01-01A");
                    MelonLogger.Msg(
                        "[FriendOverlay] getAvatar(Avtr_G_01-01A)=" +
                        (probe != null ? probe.name : "null"));
                }
                catch (Exception ex)
                {
                    MelonLogger.Msg("[FriendOverlay] getAvatar diag failed: " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg("[FriendOverlay] gifInfos diag failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Instantiates via SetPlayerPortrait, then Camera+RT captures the whole avatarObj/outlineObj
        /// hierarchy. Per-layer SpriteBake stamp was the source of multi-tile collage avatars.
        /// </summary>
        private static bool TryPortraitCompose(string imageRef, out Texture2D? texture)
        {
            texture = null;
            var image = EnsureImage();
            if (image == null)
                return false;

            var outlineKey = IsOutlineKey(imageRef) ? imageRef : string.Empty;
            var avatarKey = outlineKey.Length == 0 ? imageRef : string.Empty;

            try
            {
                try { image.SetPlayerPortrait(string.Empty, string.Empty, string.Empty); } catch { /* ok */ }

                image.SetPlayerPortrait(string.Empty, outlineKey, avatarKey);
                var root = outlineKey.Length > 0 ? image.outlineObj : image.avatarObj;
                var kind = outlineKey.Length > 0 ? PortraitBakeKind.Outline : PortraitBakeKind.Face;
                texture = CapturePortraitRoot(root, kind);
                if (AvatarCache.IsUsableTexture(texture))
                    return true;

                if (texture != null)
                {
                    try { UnityEngine.Object.Destroy(texture); } catch { /* ok */ }
                    texture = null;
                }

                return false;
            }
            catch (Exception ex)
            {
                if (!_loggedPortraitFail)
                {
                    _loggedPortraitFail = true;
                    MelonLogger.Warning("[FriendOverlay] SetPlayerPortrait unavailable: " + ex.Message);
                }

                if (texture != null)
                {
                    try { UnityEngine.Object.Destroy(texture); } catch { /* ok */ }
                }

                texture = null;
                return false;
            }
        }

        private static bool IsOutlineKey(string imageRef) => OfficialImageKeys.IsOutlineKey(imageRef);

        private static Texture2D? CapturePortraitRoot(GameObject? root, PortraitBakeKind kind)
        {
            if (root == null)
                return null;

            try { root.SetActive(true); } catch { /* ok */ }
            var tex = SpriteCapture.CaptureRoot(root);
            if (SpriteCapture.IsAcceptable(tex, kind))
                return tex;

            if (tex != null)
            {
                try { UnityEngine.Object.Destroy(tex); } catch { /* ok */ }
            }

            return null;
        }

        private static void LogOutlineMissOnce(GRAvatarManager mgr, string imageRef)
        {
            if (_loggedOutlineMiss)
                return;

            _loggedOutlineMiss = true;
            var detail = string.Empty;
            foreach (var key in OfficialImageKeys.ExactVariants(imageRef))
            {
                var state = "null";
                try
                {
                    var prefab = mgr.getOutLine(key);
                    if (prefab != null)
                    {
                        var hasGif = false;
                        try { hasGif = prefab.GetComponentInChildren<GRGif>(true) != null; }
                        catch { /* component type unavailable */ }
                        state = prefab.name + (hasGif ? " +GRGif" : " no-GRGif");
                    }
                }
                catch (Exception ex)
                {
                    state = "throw:" + ex.Message;
                }

                detail += " " + key + "=" + state;
            }

            MelonLogger.Warning("[FriendOverlay] outline key unresolved: " + imageRef + " ->" + detail);
        }

        private static void LogHit(string message)
        {
            if (_hitLogs >= 32)
                return;

            _hitLogs++;
            MelonLogger.Msg("[FriendOverlay] " + message);
        }

        private static bool TryLoadSprite(string imageRef, SpriteManager sm, Action<Sprite?> done)
        {
            var image = EnsureImage();
            if (image == null)
                return false;

            try
            {
                _pending.Add(imageRef);

                Action<Sprite> onOk = sprite =>
                {
                    _pending.Remove(imageRef);
                    done(sprite);
                };

                Action onFail = () =>
                {
                    _pending.Remove(imageRef);
                    done(null);
                };

                _alive.Add(onOk);
                _alive.Add(onFail);
                var ok = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<Sprite>>(onOk);
                var fail = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(onFail);

                sm.LoadSprite(image, imageRef, ok, fail);
                return true;
            }
            catch (Exception ex)
            {
                _pending.Remove(imageRef);
                if (!_loggedLoadSpriteFail)
                {
                    _loggedLoadSpriteFail = true;
                    MelonLogger.Warning("[FriendOverlay] SpriteManager.LoadSprite unavailable: " + ex.Message);
                }

                return false;
            }
        }

        private static bool IsHttp(string value) =>
            value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

        private static GRImage? EnsureImage()
        {
            if (_image != null)
                return _image;

            if (_hostFailed)
                return null;

            GameObject? host = null;
            try
            {
                host = new GameObject("FriendOverlaySpriteLoader");
                UnityEngine.Object.DontDestroyOnLoad(host);

                var canvas = host.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = -32760;
                var group = host.AddComponent<CanvasGroup>();
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;

                var child = new GameObject("Probe");
                child.transform.SetParent(host.transform, false);

                var image = child.AddComponent<GRImage>();
                image.color = new Color(0f, 0f, 0f, 0f);
                image.raycastTarget = false;
                image.rectTransform.sizeDelta = new Vector2(1f, 1f);
                image.rectTransform.anchoredPosition = new Vector2(-4000f, -4000f);

                _host = host;
                _image = image;
                return _image;
            }
            catch (Exception ex)
            {
                _hostFailed = true;
                MelonLogger.Warning("[FriendOverlay] game sprite host unavailable: " + ex.Message);

                try
                {
                    if (host != null)
                        UnityEngine.Object.Destroy(host);
                }
                catch
                {
                    // nothing else to do
                }

                return null;
            }
        }
    }
}
