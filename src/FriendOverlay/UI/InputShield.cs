using System;
using UnityEngine;
using UnityEngine.UI;

namespace FriendOverlay.UI
{
    /// <summary>
    /// Full-screen transparent uGUI blocker. The overlay is legacy IMGUI, which the EventSystem
    /// knows nothing about, so without this every click and drag also reaches the lobby behind it.
    /// Lives on its own canvas so the native panel's own raycast state stays untouched.
    /// </summary>
    public static class InputShield
    {
        private const int SortingOrder = 32760;

        private static GameObject? _root;
        private static bool _failed;

        public static bool Active => _root != null;

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

                var rect = image.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;

                _root = root;
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
        }
    }
}
