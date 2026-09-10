using System;
using UnityEngine;
using UnityEngine.UI;

namespace FriendOverlay.UI
{
    /// <summary>
    /// Transparent uGUI blocker sized to the overlay. The overlay is legacy IMGUI, which the
    /// EventSystem knows nothing about, so without this every click and drag also reaches the lobby
    /// behind it. It only covers the panel: a full-screen blocker also swallowed the lobby's own
    /// buttons, so 「开始游戏」 could not be clicked while the friend list was open.
    /// Lives on its own canvas so the native panel's own raycast state stays untouched.
    /// </summary>
    public static class InputShield
    {
        private const int SortingOrder = 32760;

        private static GameObject? _root;
        private static Canvas? _canvas;
        private static RectTransform? _blocker;
        private static bool _failed;
        private static Rect _applied;
        private static bool _appliedFullScreen;

        /// <summary>
        /// A shield whose rect cannot be driven is not a shield: it would sit frozen on the panel's old
        /// footprint, swallowing lobby clicks there and letting clicks through the panel's new one. So
        /// the blocker counts, not just the root.
        /// </summary>
        public static bool Active => _root != null && _blocker != null;

        public static void Ensure()
        {
            if (_failed || _root != null)
                return;

            GameObject? root = null;
            try
            {
                root = new GameObject("FriendOverlayInputShield");
                UnityEngine.Object.DontDestroyOnLoad(root);

                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = SortingOrder;
                root.AddComponent<GraphicRaycaster>();

                var blocker = new GameObject("Blocker");
                blocker.transform.SetParent(root.transform, false);

                var image = blocker.AddComponent<Image>();
                image.color = new Color(0f, 0f, 0f, 0f);
                image.raycastTarget = true;

                // Anchored to the bottom-left corner so the rect can be driven in raw pixels; a
                // stretched anchor would fight every SyncRect call.
                var rect = image.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.pivot = Vector2.zero;

                _blocker = rect;
                _canvas = canvas;
                _root = root;

                // Full screen until the panel reports its rect, because a shield that is briefly too
                // small would let a click reach the lobby through the overlay.
                SyncFullScreen();
            }
            catch (Exception ex)
            {
                _failed = true;
                MelonLoader.MelonLogger.Warning("[FriendOverlay] input shield unavailable: " + ex.Message);

                // A half-built shield is still a full-screen raycast blocker; drop it explicitly
                // because _root was never assigned.
                _root = root;
                Destroy();
            }
        }

        /// <summary>
        /// Covers just this rect, given in IMGUI coordinates (origin top-left). uGUI measures from the
        /// bottom-left, so the y axis is flipped here rather than at every call site.
        /// </summary>
        public static void SyncRect(Rect guiRect)
        {
            if (guiRect.width <= 1f || guiRect.height <= 1f)
            {
                SyncFullScreen();
                return;
            }

            Apply(
                new Rect(guiRect.x, Screen.height - guiRect.yMax, guiRect.width, guiRect.height),
                fullScreen: false);
        }

        /// <summary>Modal behaviour: a confirm or picker owns every click until it closes.</summary>
        public static void SyncFullScreen() =>
            Apply(new Rect(0f, 0f, Screen.width, Screen.height), fullScreen: true);

        public static void Destroy()
        {
            try
            {
                if (_root != null)
                    UnityEngine.Object.Destroy(_root);
            }
            catch
            {
                // scene already tearing down
            }

            _root = null;
            _canvas = null;
            _blocker = null;
            _applied = new Rect(0f, 0f, 0f, 0f);
            _appliedFullScreen = false;
        }

        /// <summary>
        /// OnGUI runs several times per frame, so an unchanged rect must not cost an interop write on
        /// every pass.
        /// </summary>
        private static void Apply(Rect screenRect, bool fullScreen)
        {
            if (_blocker == null)
                return;

            if (_appliedFullScreen == fullScreen && _applied == screenRect)
                return;

            try
            {
                // Pixels to canvas units. Our canvas has no CanvasScaler so this is 1, but a frozen
                // shield is bad enough that it is worth not assuming.
                var scale = 1f;
                try
                {
                    var s = _canvas?.scaleFactor ?? 1f;
                    if (s > 0.01f)
                        scale = s;
                }
                catch
                {
                    // canvas gone; the assignments below will throw and be handled
                }

                _blocker.anchoredPosition = new Vector2(screenRect.x / scale, screenRect.y / scale);
                _blocker.sizeDelta = new Vector2(screenRect.width / scale, screenRect.height / scale);
                _applied = screenRect;
                _appliedFullScreen = fullScreen;
            }
            catch (Exception ex)
            {
                // A shield stuck on a stale rect is worse than none: it would swallow lobby clicks where
                // the panel used to be and pass clicks through where it is now. Hand the UI back instead,
                // which is the same contract Ensure follows when it cannot build the shield at all.
                MelonLoader.MelonLogger.Warning("[FriendOverlay] input shield resize failed: " + ex.Message);
                Destroy();
                State.OverlaySession.FailOpen("input shield resize failed");
            }
        }
    }
}
