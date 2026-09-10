using System;
using System.Collections.Generic;
using Il2CppGameRiver.Client;
using Il2CppInterop.Runtime;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

namespace FriendOverlay.UI
{
    /// <summary>
    /// Resolves official avatar / frame keys (<c>Avtr_*</c>, <c>Af_*</c>) through the game's own
    /// prefab registry. These keys are not sprite-table names and not URLs:
    /// <c>SpriteManager.LoadSprite</c> throws <see cref="UriFormatException"/> on them (0.3.7), and
    /// <c>ResourceDataDownloader.DownloadImage</c> only CDN-prefixes them into a 404.
    /// <see cref="GRAvatarManager.getAvatar"/> / <c>getOutLine</c> are the keyed lookup the native
    /// portrait path uses — not a borrow from a FriendCellNode by list position.
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
        private static bool _loggedExtractFail;
        private static int _hitLogs;

        /// <summary>
        /// True when a load is in flight or was started. False means the registry is not ready yet —
        /// the caller must retry, not Fail the entry.
        /// </summary>
        public static bool TryLoad(string imageRef, SpriteManager? sm, Action<Sprite?> done)
        {
            if (string.IsNullOrEmpty(imageRef))
                return false;

            if (_pending.Contains(imageRef))
                return true;

            if (IsHttp(imageRef))
            {
                if (sm == null)
                    return false;

                return TryLoadSprite(imageRef, sm, done);
            }

            var mgr = TryGetManager();
            if (mgr == null)
                return false;

            try
            {
                var prefab = ResolvePrefab(mgr, imageRef);
                if (prefab == null)
                {
                    done(null);
                    return true;
                }

                var sprite = ExtractSprite(prefab);
                if (sprite == null && !_loggedExtractFail)
                {
                    _loggedExtractFail = true;
                    MelonLogger.Warning(
                        "[FriendOverlay] avatar prefab has no usable sprite: " + imageRef +
                        " type=" + prefab.name);
                }

                if (sprite != null && _hitLogs < 6)
                {
                    _hitLogs++;
                    MelonLogger.Msg("[FriendOverlay] game avatar prefab: " + imageRef);
                }

                done(sprite);
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[FriendOverlay] GRAvatarManager lookup failed: " + ex.Message);
                done(null);
                return true;
            }
        }

        public static void Reset()
        {
            _pending.Clear();
            _alive.Clear();
            _loggedNoManager = false;
            _loggedLoadSpriteFail = false;
            _loggedExtractFail = false;
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

        /// <summary>
        /// Avatars and outlines live in separate dictionaries. Try the obvious one first, then the
        /// other, then a GIF's first frame — some keys only exist in one of the three.
        /// </summary>
        private static GameObject? ResolvePrefab(GRAvatarManager mgr, string imageRef)
        {
            GameObject? go = null;

            var preferOutline = imageRef.StartsWith("Af_", StringComparison.OrdinalIgnoreCase) ||
                                imageRef.StartsWith("AF_", StringComparison.Ordinal);

            try
            {
                go = preferOutline ? mgr.getOutLine(imageRef) : mgr.getAvatar(imageRef);
            }
            catch
            {
                go = null;
            }

            if (go != null)
                return go;

            try
            {
                go = preferOutline ? mgr.getAvatar(imageRef) : mgr.getOutLine(imageRef);
            }
            catch
            {
                go = null;
            }

            return go;
        }

        private static Sprite? ExtractSprite(GameObject prefab)
        {
            try
            {
                // Prefab assets are inactive; includeInactive must be true or every child Image is
                // skipped and we report a miss for a registry hit.
                var images = prefab.GetComponentsInChildren<Image>(true);
                if (images != null)
                {
                    for (var i = 0; i < images.Length; i++)
                    {
                        var sprite = images[i]?.sprite;
                        if (AvatarCache.IsUsableSprite(sprite))
                            return sprite;
                    }
                }

                var renderers = prefab.GetComponentsInChildren<SpriteRenderer>(true);
                if (renderers != null)
                {
                    for (var i = 0; i < renderers.Length; i++)
                    {
                        var sprite = renderers[i]?.sprite;
                        if (AvatarCache.IsUsableSprite(sprite))
                            return sprite;
                    }
                }
            }
            catch
            {
                // component strip / prefab shape changed
            }

            return null;
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

                var child = new GameObject("Probe");
                child.transform.SetParent(host.transform, false);

                var image = child.AddComponent<GRImage>();
                image.color = new Color(0f, 0f, 0f, 0f);
                image.raycastTarget = false;
                image.rectTransform.sizeDelta = new Vector2(1f, 1f);

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
