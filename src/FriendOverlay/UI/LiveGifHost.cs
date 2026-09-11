using System;
using System.Collections.Generic;
using FriendOverlay.Core;
using Il2CppGameRiver.Client;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

namespace FriendOverlay.UI
{
    /// <summary>
    /// Official animated portraits live in <see cref="GRAvatarManager.avatars"/> (and frames in
    /// <c>outlines</c>) as prefabs with <see cref="GRGif"/> — not in <c>gifInfos</c>.
    /// Frames are captured by rendering the whole prefab hierarchy via <see cref="SpriteCapture.CaptureRoot"/>.
    /// GIF baking is temporal (one CaptureRoot per soft-poll) so quality-material shine advances.
    /// </summary>
    public static class LiveGifHost
    {
        public enum PrefabResult
        {
            NoGif,
            Pending,
            Ready,
        }

        private const int SettleAttempts = 12;
        private const int MaxEmptyMisses = 6;

        private sealed class Pending
        {
            public GameObject? Instance;
            public int Attempts;
            public bool Baking;
            public int NextIndex;
            public int TargetCount;
            public int EmptyMisses;
            public float FrameSeconds = GifPlayback.DefaultFrameSeconds;
            public List<Texture2D>? Baked;
        }

        private static readonly Dictionary<string, Pending> _pending =
            new Dictionary<string, Pending>(StringComparer.OrdinalIgnoreCase);

        private static bool _logged;
        private static bool _loggedStatic;
        private static bool _loggedFail;
        private static bool _loggedReject;
        private static GameObject? _sharedRoot;

        public static float LastFrameSeconds { get; set; } = GifPlayback.DefaultFrameSeconds;

        public static void Clear()
        {
            foreach (var p in _pending.Values)
                DestroyInstance(p);

            _pending.Clear();
            DestroyRoot();
            SpriteCapture.Reset();
            _logged = false;
            _loggedStatic = false;
            _loggedFail = false;
            _loggedReject = false;
            LastFrameSeconds = GifPlayback.DefaultFrameSeconds;
        }

        public static bool IsPending(string imageRef) =>
            _pending.TryGetValue(imageRef, out var p) && p.Instance != null;

        public static PrefabResult TryBakeFrames(GRAvatarManager mgr, string imageRef, out Texture2D[]? frames)
        {
            frames = null;
            if (string.IsNullOrEmpty(imageRef) || mgr == null)
                return PrefabResult.NoGif;

            try
            {
                if (!_pending.TryGetValue(imageRef, out var pending) || pending.Instance == null)
                {
                    var prefab = ResolvePrefab(mgr, imageRef);
                    if (prefab == null)
                    {
                        // Don't poison — registry may still be filling.
                        return PrefabResult.NoGif;
                    }

                    var root = EnsureRoot();
                    if (root == null)
                        return PrefabResult.NoGif;

                    var instance = UnityEngine.Object.Instantiate(prefab, root.transform);
                    instance.name = "Pending_" + imageRef;
                    instance.SetActive(true);
                    ForceActive(instance.transform);

                    // No GRGif: capture the whole static prefab once this tick (CaptureRoot only).
                    if (instance.GetComponentInChildren<GRGif>(true) == null)
                    {
                        var kind = BakeKind(imageRef);
                        var staticTex = CaptureRootOnly(instance, kind);
                        try { UnityEngine.Object.Destroy(instance); } catch { /* ok */ }

                        if (!IsUsableCapture(staticTex, kind))
                        {
                            if (staticTex != null)
                            {
                                try { UnityEngine.Object.Destroy(staticTex); } catch { /* ok */ }
                            }

                            return PrefabResult.NoGif;
                        }

                        if (!_loggedStatic)
                        {
                            _loggedStatic = true;
                            MelonLogger.Msg(
                                "[FriendOverlay] avatar prefab static root: " + imageRef + " frames=1");
                        }

                        frames = new[] { staticTex! };
                        LastFrameSeconds = GifPlayback.DefaultFrameSeconds;
                        return PrefabResult.Ready;
                    }

                    pending = new Pending { Instance = instance, Attempts = 0 };
                    _pending[imageRef] = pending;
                    return PrefabResult.Pending;
                }

                pending.Attempts++;
                var gif = pending.Instance.GetComponentInChildren<GRGif>(true);
                var list = gif != null ? gif.spriteList : null;
                if (list == null || list.Count == 0)
                {
                    if (pending.Attempts < SettleAttempts)
                        return PrefabResult.Pending;

                    // Timed out — single CaptureRoot only, then give up GIF (no SpriteBake).
                    var kind = BakeKind(imageRef);
                    var fallback = CaptureRootOnly(pending.Instance, kind);
                    DestroyInstance(pending);
                    _pending.Remove(imageRef);

                    if (IsUsableCapture(fallback, kind))
                    {
                        frames = new[] { fallback! };
                        return PrefabResult.Ready;
                    }

                    if (fallback != null)
                    {
                        try { UnityEngine.Object.Destroy(fallback); } catch { /* ok */ }
                    }

                    return PrefabResult.NoGif;
                }

                if (!pending.Baking)
                {
                    pending.Baking = true;
                    pending.NextIndex = 0;
                    pending.TargetCount = OverlayPerfSettings.TargetGifFrameCount(list.Count);
                    pending.EmptyMisses = 0;
                    pending.Baked = new List<Texture2D>(pending.TargetCount);
                    pending.FrameSeconds = GifPlayback.DefaultFrameSeconds;
                    try
                    {
                        var first = list[0];
                        if (first.Item1 > 0.001f && first.Item1 < 2f)
                            pending.FrameSeconds = first.Item1;
                    }
                    catch { /* ok */ }
                }

                return CaptureTemporalStep(imageRef, pending, gif!, list, out frames);
            }
            catch (Exception ex)
            {
                if (_pending.TryGetValue(imageRef, out var orphan))
                {
                    DestroyInstance(orphan);
                    _pending.Remove(imageRef);
                }

                if (!_loggedFail)
                {
                    _loggedFail = true;
                    MelonLogger.Warning("[FriendOverlay] avatar prefab bake failed: " + ex.Message);
                }

                return PrefabResult.NoGif;
            }
        }

        private static PrefabResult CaptureTemporalStep(
            string imageRef,
            Pending pending,
            GRGif gif,
            Il2CppSystem.Collections.Generic.List<Il2CppSystem.ValueTuple<float, Sprite>> list,
            out Texture2D[]? frames)
        {
            frames = null;
            var baked = pending.Baked ?? (pending.Baked = new List<Texture2D>());
            var i = pending.NextIndex;
            if (i < 0)
                i = 0;

            if (i >= pending.TargetCount || i >= list.Count)
                return FinishTemporalBake(imageRef, pending, out frames);

            Sprite? sp = null;
            try
            {
                sp = list[i].Item2;
            }
            catch
            {
                pending.NextIndex++;
                pending.EmptyMisses++;
                return pending.EmptyMisses >= MaxEmptyMisses
                    ? FinishTemporalBake(imageRef, pending, out frames)
                    : PrefabResult.Pending;
            }

            ApplyGifFrame(gif, sp, i);
            try { gif.Update(); } catch { /* private Update may throw on some builds */ }

            ForceActive(pending.Instance!.transform);
            var kind = BakeKind(imageRef);
            var tex = CaptureRootOnly(pending.Instance, kind);
            pending.NextIndex = i + 1;

            if (tex != null)
            {
                baked.Add(tex);
                pending.EmptyMisses = 0;
            }
            else
            {
                pending.EmptyMisses++;
            }

            if (pending.NextIndex >= pending.TargetCount || pending.EmptyMisses >= MaxEmptyMisses)
                return FinishTemporalBake(imageRef, pending, out frames);

            return PrefabResult.Pending;
        }

        private static PrefabResult FinishTemporalBake(
            string imageRef,
            Pending pending,
            out Texture2D[]? frames)
        {
            frames = null;
            var baked = pending.Baked;
            LastFrameSeconds = pending.FrameSeconds > 0f
                ? pending.FrameSeconds
                : GifPlayback.DefaultFrameSeconds;

            // Detach baked list before DestroyInstance so we keep the textures.
            pending.Baked = null;
            DestroyInstance(pending);
            _pending.Remove(imageRef);

            if (baked == null || baked.Count == 0)
                return PrefabResult.NoGif;

            if (!_logged)
            {
                _logged = true;
                MelonLogger.Msg(
                    "[FriendOverlay] gif-temporal-bake frames=" + baked.Count +
                    " dt=" + LastFrameSeconds.ToString("0.###") + " key=" + imageRef);
            }

            frames = baked.ToArray();
            return PrefabResult.Ready;
        }

        private static void ApplyGifFrame(GRGif gif, Sprite? sp, int index)
        {
            Image? image = null;
            try { image = gif.GetComponent<Image>() ?? gif.GetComponentInChildren<Image>(true); }
            catch { image = null; }

            if (AvatarCache.IsUsableSprite(sp))
            {
                try
                {
                    if (image != null)
                        image.sprite = sp;
                    gif.sprite = sp;
                }
                catch
                {
                    // some builds expose sprite only via Image
                }
            }

            try { gif.mCurFrame = index; } catch { /* ok */ }
            // Do not reset mTime — let GRGif / quality materials advance across polls.
        }

        private static GameObject? ResolvePrefab(GRAvatarManager mgr, string imageRef)
        {
            foreach (var key in OfficialImageKeys.ExactVariants(imageRef))
            {
                try
                {
                    if (OfficialImageKeys.IsOutlineKey(key))
                    {
                        var outline = mgr.getOutLine(key);
                        if (outline != null)
                            return outline;
                    }
                    else
                    {
                        var avatar = mgr.getAvatar(key);
                        if (avatar != null)
                            return avatar;
                    }
                }
                catch
                {
                    // key miss
                }
            }

            return null;
        }

        private static PortraitBakeKind BakeKind(string imageRef) =>
            OfficialImageKeys.IsOutlineKey(imageRef)
                ? PortraitBakeKind.Outline
                : PortraitBakeKind.Face;

        /// <summary>
        /// GIF / official prefab bake: CaptureRoot only. Never blit-crop (avoids atlas garbage).
        /// </summary>
        private static Texture2D? CaptureRootOnly(GameObject instance, PortraitBakeKind kind)
        {
            var tex = SpriteCapture.CaptureRoot(instance);
            if (IsUsableCapture(tex, kind))
                return tex;

            if (tex != null)
            {
                LogRejectOnce(kind);
                try { UnityEngine.Object.Destroy(tex); } catch { /* ok */ }
            }

            return null;
        }

        private static void LogRejectOnce(PortraitBakeKind kind)
        {
            if (_loggedReject)
                return;
            _loggedReject = true;
            MelonLogger.Msg(
                "[FriendOverlay] capture-root rejected kind=" + kind + " (retry without SpriteBake)");
        }

        private static bool IsUsableCapture(Texture2D? tex, PortraitBakeKind kind) =>
            SpriteCapture.IsAcceptable(tex, kind);

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

        private static GameObject? EnsureRoot()
        {
            if (_sharedRoot != null)
            {
                try
                {
                    var existing = _sharedRoot.GetComponent<CanvasGroup>();
                    if (existing != null)
                        existing.alpha = 1f;
                }
                catch { /* ok */ }

                return _sharedRoot;
            }

            try
            {
                var host = new GameObject("FriendOverlayAvatarPrefabRoot");
                UnityEngine.Object.DontDestroyOnLoad(host);
                var canvas = host.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = -32760;
                var group = host.AddComponent<CanvasGroup>();
                // alpha=1 so GRGif.Update / quality materials keep advancing while offscreen.
                group.alpha = 1f;
                group.blocksRaycasts = false;
                group.interactable = false;
                host.transform.position = new Vector3(-5000f, -5000f, 0f);
                _sharedRoot = host;
                return _sharedRoot;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[FriendOverlay] avatar prefab root unavailable: " + ex.Message);
                return null;
            }
        }

        private static void DestroyRoot()
        {
            if (_sharedRoot == null)
                return;

            try { UnityEngine.Object.Destroy(_sharedRoot); } catch { /* ok */ }
            _sharedRoot = null;
        }

        private static void DestroyInstance(Pending pending)
        {
            if (pending.Baked != null)
            {
                for (var i = 0; i < pending.Baked.Count; i++)
                {
                    try
                    {
                        if (pending.Baked[i] != null)
                            UnityEngine.Object.Destroy(pending.Baked[i]);
                    }
                    catch { /* ok */ }
                }

                pending.Baked = null;
            }

            if (pending.Instance == null)
                return;

            try { UnityEngine.Object.Destroy(pending.Instance); } catch { /* ok */ }
            pending.Instance = null;
        }
    }
}
