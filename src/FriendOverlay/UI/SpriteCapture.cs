using System;
using System.Collections.Generic;
using FriendOverlay.Core;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

namespace FriendOverlay.UI
{
    /// <summary>
    /// Renders uGUI (single sprite or a whole prefab hierarchy) into a small RenderTexture.
    /// Letting Unity's Image/Canvas shaders resolve atlas UVs avoids SpriteBake blit-crop collages.
    /// </summary>
    public static class SpriteCapture
    {
        public const int DefaultSize = 128;

        private const int CaptureLayer = 31;

        private static GameObject? _root;
        private static Camera? _camera;
        private static Canvas? _canvas;
        private static RectTransform? _canvasRect;
        private static RectTransform? _stage;
        private static Image? _image;
        private static RenderTexture? _target;
        private static int _size;
        private static bool _rigFailed;
        private static bool _busy;
        private static bool _loggedSpriteOk;
        private static bool _loggedRootOk;
        private static bool _loggedBlank;
        private static bool _loggedRejectWire;
        private static bool _loggedRejectBlack;
        private static int _emptyCaptures;

        /// <summary>True when the last Capture/CaptureRoot returned null because BakeBudget was exhausted.</summary>
        public static bool LastDeniedByBudget { get; private set; }

        public static void Reset()
        {
            LastDeniedByBudget = false;
            try
            {
                if (_target != null)
                {
                    if (RenderTexture.active == _target)
                        RenderTexture.active = null;
                    _target.Release();
                    UnityEngine.Object.Destroy(_target);
                }
            }
            catch { /* ok */ }

            try
            {
                if (_root != null)
                    UnityEngine.Object.Destroy(_root);
            }
            catch { /* ok */ }

            _target = null;
            _root = null;
            _camera = null;
            _canvas = null;
            _canvasRect = null;
            _stage = null;
            _image = null;
            _size = 0;
            _rigFailed = false;
            _busy = false;
            _loggedSpriteOk = false;
            _loggedRootOk = false;
            _loggedBlank = false;
            _loggedRejectWire = false;
            _loggedRejectBlack = false;
            _emptyCaptures = 0;
        }

        public enum RejectReason
        {
            None = 0,
            NullOrTiny = 1,
            Sparse = 2,
            Wireframe = 3,
            Black = 4,
        }

        /// <summary>
        /// Shared gate for CaptureRoot results. Default kind is Face (rejects hollow/black plates).
        /// Use the overload with <see cref="PortraitBakeKind.Outline"/> for avatar frames.
        /// </summary>
        public static bool IsAcceptable(Texture2D? tex) =>
            IsAcceptable(tex, PortraitBakeKind.Face, out _);

        public static bool IsAcceptable(Texture2D? tex, out RejectReason reason) =>
            IsAcceptable(tex, PortraitBakeKind.Face, out reason);

        public static bool IsAcceptable(Texture2D? tex, PortraitBakeKind kind) =>
            IsAcceptable(tex, kind, out _);

        public static bool IsAcceptable(Texture2D? tex, PortraitBakeKind kind, out RejectReason reason)
        {
            reason = RejectReason.NullOrTiny;
            if (!AvatarCache.IsUsableTexture(tex))
                return false;

            try
            {
                var w = tex!.width;
                var h = tex.height;
                var px = tex.GetPixels32();
                if (px == null || px.Length == 0)
                {
                    reason = RejectReason.NullOrTiny;
                    return false;
                }

                var rgba = new byte[px.Length * 4];
                for (var i = 0; i < px.Length; i++)
                {
                    var p = px[i];
                    var o = i * 4;
                    rgba[o] = p.r;
                    rgba[o + 1] = p.g;
                    rgba[o + 2] = p.b;
                    rgba[o + 3] = p.a;
                }

                var ok = PortraitCaptureAcceptance.Evaluate(w, h, rgba, kind, out var coreReject);
                reason = (RejectReason)(int)coreReject;
                if (!ok)
                    LogRejectOnce(kind, reason);
                return ok;
            }
            catch
            {
                reason = RejectReason.Sparse;
                return false;
            }
        }

        private static void LogRejectOnce(PortraitBakeKind kind, RejectReason reason)
        {
            if (reason == RejectReason.Wireframe)
            {
                if (_loggedRejectWire)
                    return;
                _loggedRejectWire = true;
                MelonLogger.Msg(
                    "[FriendOverlay] reject-wireframe kind=" + kind + " (empty center)");
                return;
            }

            if (reason == RejectReason.Black)
            {
                if (_loggedRejectBlack)
                    return;
                _loggedRejectBlack = true;
                MelonLogger.Msg(
                    "[FriendOverlay] reject-black kind=" + kind + " (near-solid dark)");
            }
        }

        /// <summary>Renders one sprite via a dedicated Image. Owned texture or null.</summary>
        public static Texture2D? Capture(Sprite? sprite, int size = DefaultSize)
        {
            LastDeniedByBudget = false;
            if (_rigFailed || _busy || !AvatarCache.IsUsableSprite(sprite))
                return null;

            if (!Ensure(size))
                return null;

            BakeBudget.BeginFrame(Time.frameCount);
            if (!BakeBudget.TryConsume())
            {
                LastDeniedByBudget = true;
                return null;
            }

            _busy = true;
            var prev = RenderTexture.active;
            try
            {
                if (_image != null)
                {
                    _image.gameObject.SetActive(true);
                    _image.sprite = sprite;
                    _image.enabled = true;
                }

                return RenderReadback(size, "ugui-rt:" + SafeName(sprite), ref _loggedSpriteOk);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[FriendOverlay] sprite capture unavailable: " + ex.Message);
                return null;
            }
            finally
            {
                _busy = false;
                try { RenderTexture.active = prev; } catch { /* ok */ }
                try
                {
                    if (_image != null)
                    {
                        _image.sprite = null;
                        _image.gameObject.SetActive(false);
                    }
                }
                catch { /* ok */ }
            }
        }

        /// <summary>
        /// Renders an entire uGUI hierarchy (SetPlayerPortrait root or avatar/outline prefab).
        /// Temporarily reparents under the capture canvas; restores the original parent afterwards.
        /// </summary>
        public static Texture2D? CaptureRoot(GameObject? subject, int size = DefaultSize)
        {
            LastDeniedByBudget = false;
            if (_rigFailed || _busy || subject == null)
                return null;

            if (!Ensure(size))
                return null;

            BakeBudget.BeginFrame(Time.frameCount);
            if (!BakeBudget.TryConsume())
            {
                LastDeniedByBudget = true;
                return null;
            }

            _busy = true;

            Transform? oldParent = null;
            var oldActive = false;
            var oldPos = Vector3.zero;
            var oldRot = Quaternion.identity;
            var oldScale = Vector3.one;
            var hadParent = false;
            var sibling = -1;
            var canvasRestore = new List<CanvasState>();
            var groupRestore = new List<GroupState>();
            var layerRestore = new List<LayerState>();
            RectSnapshot? rectSnapshot = null;

            var prev = RenderTexture.active;
            try
            {
                oldParent = subject.transform.parent;
                hadParent = true;
                sibling = subject.transform.GetSiblingIndex();
                oldActive = subject.activeSelf;
                oldPos = subject.transform.localPosition;
                oldRot = subject.transform.localRotation;
                oldScale = subject.transform.localScale;
                CaptureLayers(subject.transform, layerRestore);
                rectSnapshot = SnapshotRect(subject);

                if (_image != null)
                    _image.gameObject.SetActive(false);

                subject.SetActive(true);
                subject.transform.SetParent(_stage, false);
                SetLayerRecursive(subject.transform, CaptureLayer);
                FitSubject(subject);
                PrepareVisibility(subject, canvasRestore, groupRestore);
                ForceActive(subject.transform);
                Canvas.ForceUpdateCanvases();

                return RenderReadback(size, "capture-root:" + SafeGoName(subject), ref _loggedRootOk);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[FriendOverlay] root capture unavailable: " + ex.Message);
                return null;
            }
            finally
            {
                _busy = false;
                try { RenderTexture.active = prev; } catch { /* ok */ }

                try
                {
                    RestoreVisibility(canvasRestore, groupRestore);

                    if (hadParent)
                    {
                        subject.transform.SetParent(oldParent, false);
                        if (sibling >= 0)
                        {
                            try { subject.transform.SetSiblingIndex(sibling); } catch { /* ok */ }
                        }

                        // Restore RectTransform layout BEFORE localPosition — FitSubject mutates
                        // anchors/anchoredPosition to screen-center, which is what parked GIF
                        // instances into the lobby middle (1280,720 on 1440p).
                        RestoreRect(subject, rectSnapshot);
                        subject.transform.localPosition = oldPos;
                        subject.transform.localRotation = oldRot;
                        subject.transform.localScale = oldScale;
                        RestoreLayers(layerRestore);
                        subject.SetActive(oldActive);
                    }
                }
                catch { /* ok */ }
            }
        }

        private sealed class LayerState
        {
            public GameObject Go = null!;
            public int Layer;
        }

        private sealed class RectSnapshot
        {
            public bool HasRect;
            public Vector2 AnchorMin;
            public Vector2 AnchorMax;
            public Vector2 Pivot;
            public Vector2 AnchoredPosition;
            public Vector2 SizeDelta;
            public Vector3 LocalScale;
        }

        private static RectSnapshot SnapshotRect(GameObject subject)
        {
            var snap = new RectSnapshot();
            try
            {
                var rt = subject.GetComponent<RectTransform>();
                if (rt == null)
                    return snap;

                snap.HasRect = true;
                snap.AnchorMin = rt.anchorMin;
                snap.AnchorMax = rt.anchorMax;
                snap.Pivot = rt.pivot;
                snap.AnchoredPosition = rt.anchoredPosition;
                snap.SizeDelta = rt.sizeDelta;
                snap.LocalScale = rt.localScale;
            }
            catch { /* ok */ }

            return snap;
        }

        private static void RestoreRect(GameObject subject, RectSnapshot? snap)
        {
            if (snap == null || !snap.HasRect)
                return;

            try
            {
                var rt = subject.GetComponent<RectTransform>();
                if (rt == null)
                    return;

                rt.anchorMin = snap.AnchorMin;
                rt.anchorMax = snap.AnchorMax;
                rt.pivot = snap.Pivot;
                rt.anchoredPosition = snap.AnchoredPosition;
                rt.sizeDelta = snap.SizeDelta;
                rt.localScale = snap.LocalScale;
            }
            catch { /* ok */ }
        }

        private static void CaptureLayers(Transform root, List<LayerState> into)
        {
            try
            {
                into.Add(new LayerState { Go = root.gameObject, Layer = root.gameObject.layer });
                for (var i = 0; i < root.childCount; i++)
                    CaptureLayers(root.GetChild(i), into);
            }
            catch { /* ok */ }
        }

        private static void RestoreLayers(List<LayerState> states)
        {
            for (var i = 0; i < states.Count; i++)
            {
                try
                {
                    var s = states[i];
                    if (s.Go != null)
                        s.Go.layer = s.Layer;
                }
                catch { /* ok */ }
            }
        }

        private sealed class CanvasState
        {
            public Canvas Canvas = null!;
            public RenderMode Mode;
            public Camera? WorldCamera;
        }

        private sealed class GroupState
        {
            public CanvasGroup Group = null!;
            public float Alpha;
            public bool Blocks;
            public bool Interactable;
        }

        private static void PrepareVisibility(
            GameObject subject,
            List<CanvasState> canvasRestore,
            List<GroupState> groupRestore)
        {
            try
            {
                var groups = subject.GetComponentsInChildren<CanvasGroup>(true);
                if (groups != null)
                {
                    for (var i = 0; i < groups.Length; i++)
                    {
                        var g = groups[i];
                        if (g == null)
                            continue;
                        groupRestore.Add(new GroupState
                        {
                            Group = g,
                            Alpha = g.alpha,
                            Blocks = g.blocksRaycasts,
                            Interactable = g.interactable,
                        });
                        g.alpha = 1f;
                        g.blocksRaycasts = false;
                        g.interactable = false;
                    }
                }
            }
            catch { /* ok */ }

            try
            {
                var canvases = subject.GetComponentsInChildren<Canvas>(true);
                if (canvases == null)
                    return;

                for (var i = 0; i < canvases.Length; i++)
                {
                    var c = canvases[i];
                    if (c == null)
                        continue;

                    canvasRestore.Add(new CanvasState
                    {
                        Canvas = c,
                        Mode = c.renderMode,
                        WorldCamera = c.worldCamera,
                    });
                    c.renderMode = RenderMode.WorldSpace;
                    c.worldCamera = _camera;
                }
            }
            catch { /* ok */ }
        }

        private static void RestoreVisibility(List<CanvasState> canvasRestore, List<GroupState> groupRestore)
        {
            for (var i = 0; i < groupRestore.Count; i++)
            {
                try
                {
                    var s = groupRestore[i];
                    if (s.Group == null)
                        continue;
                    s.Group.alpha = s.Alpha;
                    s.Group.blocksRaycasts = s.Blocks;
                    s.Group.interactable = s.Interactable;
                }
                catch { /* ok */ }
            }

            for (var i = 0; i < canvasRestore.Count; i++)
            {
                try
                {
                    var s = canvasRestore[i];
                    if (s.Canvas == null)
                        continue;
                    s.Canvas.renderMode = s.Mode;
                    s.Canvas.worldCamera = s.WorldCamera;
                }
                catch { /* ok */ }
            }
        }

        private static Texture2D? RenderReadback(int size, string via, ref bool loggedFlag)
        {
            Canvas.ForceUpdateCanvases();
            _camera!.Render();

            RenderTexture.active = _target;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.ReadPixels(new Rect(0f, 0f, size, size), 0, 0);

            if (IsBlank(tex))
            {
                try { UnityEngine.Object.Destroy(tex); } catch { /* ok */ }
                // Never latch _rigFailed on blank — callers soft-retry without SpriteBake.
                if (++_emptyCaptures >= 6 && !_loggedBlank)
                {
                    _loggedBlank = true;
                    MelonLogger.Warning(
                        "[FriendOverlay] sprite capture blank (continuing; callers may soft-retry)");
                }

                return null;
            }

            _emptyCaptures = 0;
            Unpremultiply(tex);
            tex.Apply(false, false);

            if (!loggedFlag)
            {
                loggedFlag = true;
                MelonLogger.Msg("[FriendOverlay] sprite capture ok via " + via + " -> " + size + "x" + size);
            }

            return tex;
        }

        private static void FitSubject(GameObject subject)
        {
            var rt = subject.GetComponent<RectTransform>();
            if (rt != null)
            {
                // Contain: center + uniform scale so frame rings keep proportion (stretch made wireframes).
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.localRotation = Quaternion.identity;

                Canvas.ForceUpdateCanvases();

                var w = Mathf.Abs(rt.rect.width);
                var h = Mathf.Abs(rt.rect.height);
                if (w < 2f || h < 2f)
                {
                    w = Mathf.Max(2f, Mathf.Abs(rt.sizeDelta.x));
                    h = Mathf.Max(2f, Mathf.Abs(rt.sizeDelta.y));
                }

                if (w < 2f || h < 2f)
                {
                    rt.sizeDelta = new Vector2(DefaultSize, DefaultSize);
                    Canvas.ForceUpdateCanvases();
                    w = Mathf.Abs(rt.rect.width);
                    h = Mathf.Abs(rt.rect.height);
                    if (w < 2f) w = DefaultSize;
                    if (h < 2f) h = DefaultSize;
                }

                var scale = Mathf.Min(_size / w, _size / h);
                // Prefer slightly smaller than fill so ornaments aren't clipped.
                scale *= 0.92f;
                rt.localScale = new Vector3(scale, scale, 1f);
                return;
            }

            subject.transform.localPosition = Vector3.zero;
            subject.transform.localRotation = Quaternion.identity;
            subject.transform.localScale = Vector3.one;
        }

        private static void SetLayerRecursive(Transform t, int layer)
        {
            try
            {
                t.gameObject.layer = layer;
                for (var i = 0; i < t.childCount; i++)
                    SetLayerRecursive(t.GetChild(i), layer);
            }
            catch { /* ok */ }
        }

        private static void ForceActive(Transform root)
        {
            try
            {
                root.gameObject.SetActive(true);
                for (var i = 0; i < root.childCount; i++)
                    ForceActive(root.GetChild(i));
            }
            catch { /* ok */ }
        }

        private static bool Ensure(int size)
        {
            if (_root != null && _camera != null && _canvas != null && _stage != null &&
                _image != null && _target != null && _size == size)
                return true;

            if (_root != null)
                Reset();

            try
            {
                _size = size;

                var root = new GameObject("FriendOverlaySpriteCapture");
                UnityEngine.Object.DontDestroyOnLoad(root);
                root.layer = CaptureLayer;
                root.transform.position = new Vector3(0f, -20000f, 0f);
                _root = root;

                _target = new RenderTexture(size, size, 0, RenderTextureFormat.ARGB32);
                _target.antiAliasing = 1;
                _target.filterMode = FilterMode.Bilinear;
                _target.wrapMode = TextureWrapMode.Clamp;
                _target.Create();

                var camGo = new GameObject("Cam");
                camGo.transform.SetParent(root.transform, false);
                camGo.transform.localPosition = new Vector3(0f, 0f, -10f);
                camGo.transform.localRotation = Quaternion.identity;
                camGo.layer = CaptureLayer;

                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = size * 0.5f;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 100f;
                cam.cullingMask = 1 << CaptureLayer;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                cam.allowHDR = false;
                cam.allowMSAA = false;
                cam.useOcclusionCulling = false;
                cam.targetTexture = _target;
                cam.aspect = 1f;
                cam.enabled = false;
                _camera = cam;

                var canvasGo = new GameObject("Canvas");
                canvasGo.transform.SetParent(root.transform, false);
                canvasGo.layer = CaptureLayer;
                var canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = cam;
                _canvas = canvas;
                _canvasRect = canvasGo.GetComponent<RectTransform>() ?? canvasGo.AddComponent<RectTransform>();
                _canvasRect.localPosition = Vector3.zero;
                _canvasRect.localRotation = Quaternion.identity;
                _canvasRect.localScale = Vector3.one;
                _canvasRect.sizeDelta = new Vector2(size, size);

                var stageGo = new GameObject("Stage");
                stageGo.transform.SetParent(canvasGo.transform, false);
                stageGo.layer = CaptureLayer;
                _stage = stageGo.GetComponent<RectTransform>() ?? stageGo.AddComponent<RectTransform>();
                _stage.anchorMin = Vector2.zero;
                _stage.anchorMax = Vector2.one;
                _stage.offsetMin = Vector2.zero;
                _stage.offsetMax = Vector2.zero;
                _stage.localScale = Vector3.one;

                var imageGo = new GameObject("SingleSprite");
                imageGo.transform.SetParent(_stage, false);
                imageGo.layer = CaptureLayer;
                imageGo.SetActive(false);
                var image = imageGo.AddComponent<Image>();
                image.raycastTarget = false;
                image.preserveAspect = true;
                image.color = Color.white;
                var imageRect = image.rectTransform;
                imageRect.anchorMin = Vector2.zero;
                imageRect.anchorMax = Vector2.one;
                imageRect.offsetMin = Vector2.zero;
                imageRect.offsetMax = Vector2.zero;
                _image = image;

                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[FriendOverlay] sprite capture rig failed: " + ex.Message);
                Reset();
                _rigFailed = true;
                return false;
            }
        }

        private static bool IsBlank(Texture2D tex)
        {
            try
            {
                var px = tex.GetPixels32();
                if (px == null || px.Length == 0)
                    return true;

                var step = Math.Max(1, px.Length / 4096);
                var sampled = 0;
                var opaque = 0;
                for (var i = 0; i < px.Length; i += step)
                {
                    sampled++;
                    if (px[i].a >= 16)
                        opaque++;
                }

                return sampled == 0 || opaque / (float)sampled < 0.01f;
            }
            catch
            {
                return true;
            }
        }

        private static void Unpremultiply(Texture2D tex)
        {
            try
            {
                var px = tex.GetPixels32();
                if (px == null)
                    return;

                for (var i = 0; i < px.Length; i++)
                {
                    var c = px[i];
                    if (c.a == 0 || c.a == 255)
                        continue;

                    var inv = 255f / c.a;
                    c.r = (byte)Mathf.Min(255f, c.r * inv);
                    c.g = (byte)Mathf.Min(255f, c.g * inv);
                    c.b = (byte)Mathf.Min(255f, c.b * inv);
                    px[i] = c;
                }

                tex.SetPixels32(px);
            }
            catch
            {
                // leave as-is
            }
        }

        private static string SafeName(Sprite? sprite)
        {
            try { return sprite != null ? sprite.name : "(null)"; }
            catch { return "(?)"; }
        }

        private static string SafeGoName(GameObject? go)
        {
            try { return go != null ? go.name : "(null)"; }
            catch { return "(?)"; }
        }
    }
}
