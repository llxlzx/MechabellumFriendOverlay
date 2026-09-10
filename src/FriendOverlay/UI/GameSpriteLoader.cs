using System;
using System.Collections.Generic;
using Il2CppGameRiver.Client;
using Il2CppInterop.Runtime;
using MelonLoader;
using UnityEngine;

namespace FriendOverlay.UI
{
    /// <summary>
    /// Asks the game to load a sprite the way the game itself does. Official avatars and frames are not
    /// all present in the local sprite tables and their references are not URLs either, so the only
    /// reliable route is <c>SpriteManager.LoadSprite</c>, which owns the game's own downloader and cache.
    /// It insists on a <c>GRImage</c> to hang the request on, hence the hidden one-pixel host below; we
    /// only ever read the sprite out of the success callback.
    /// </summary>
    public static class GameSpriteLoader
    {
        private static readonly HashSet<string> _pending = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// IL2CPP holds only the native side of a converted delegate, so the managed ones have to be
        /// rooted here or the GC can collect a callback that has not fired yet.
        /// </summary>
        private static readonly List<object> _alive = new List<object>();

        private static GameObject? _host;
        private static GRImage? _image;
        private static bool _failed;

        /// <summary>
        /// Starts a load and returns true when one is now in flight (or already was). False means this
        /// route is unavailable and the caller should fall back.
        /// </summary>
        public static bool TryLoad(string imageRef, SpriteManager sm, Action<Sprite?> done)
        {
            if (_failed || string.IsNullOrEmpty(imageRef))
                return false;

            if (_pending.Contains(imageRef))
                return true;

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

                var ok = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<Sprite>>(onOk);
                var fail = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(onFail);
                _alive.Add(onOk);
                _alive.Add(onFail);

                sm.LoadSprite(image, imageRef, ok, fail);
                return true;
            }
            catch (Exception ex)
            {
                _pending.Remove(imageRef);
                _failed = true;
                MelonLogger.Warning("[FriendOverlay] game sprite load unavailable: " + ex.Message);
                return false;
            }
        }

        public static void Reset()
        {
            _pending.Clear();
            _alive.Clear();

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
            _failed = false;
        }

        private static GRImage? EnsureImage()
        {
            if (_image != null)
                return _image;

            GameObject? host = null;
            try
            {
                // A GRImage is a uGUI Image, so it wants a Canvas ancestor. This one is pushed behind
                // everything and the image itself is transparent and one pixel wide: it exists to be a
                // request handle, not to be seen.
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
                _failed = true;
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
