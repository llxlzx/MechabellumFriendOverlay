using System;
using System.Collections.Generic;
using FriendOverlay.Core;
using MelonLoader;
using UnityEngine;

namespace FriendOverlay.UI
{
    public sealed class SharedImage
    {
        public Sprite? Sprite;
        public Texture2D? Texture;
        public Texture2D[]? Frames;
        public float FrameSeconds = GifPlayback.DefaultFrameSeconds;
        public int SoftAttempts;
        public float NextSoftTry;
        public bool Pending;
        public bool Failed;

        public bool HasImage =>
            AvatarCache.IsUsableTexture(Texture) ||
            AvatarCache.IsUsableSprite(Sprite) ||
            HasFrames;

        public bool HasFrames
        {
            get
            {
                if (Frames == null || Frames.Length == 0)
                    return false;

                for (var i = 0; i < Frames.Length; i++)
                {
                    if (AvatarCache.IsUsableTexture(Frames[i]))
                        return true;
                }

                return false;
            }
        }
    }

    /// <summary>
    /// Images shared between players: official avatars, avatar frames, the moderation placeholder.
    /// Keyed by the game's image reference. Sprites are game-owned and never destroyed here; textures
    /// come from <see cref="AvatarLoader"/> / GIF bake and are ours to release.
    /// </summary>
    public static class SharedImageCache
    {
        private const int SoftAttemptLimit = 40;
        private const float SoftAttemptSpacing = 0.5f;

        private static readonly Dictionary<string, SharedImage> _byRef =
            new Dictionary<string, SharedImage>(StringComparer.Ordinal);

        private static int _failuresLogged;

        public static SharedImage? Get(string? imageRef)
        {
            if (string.IsNullOrEmpty(imageRef))
                return null;

            return _byRef.TryGetValue(imageRef!, out var entry) && entry.HasImage ? entry : null;
        }

        /// <summary>
        /// Current drawable texture for IMGUI. GIF entries advance via <see cref="GifPlayback"/> and
        /// <see cref="Time.unscaledTime"/> so every row sharing the key stays in sync.
        /// </summary>
        public static Texture2D? CurrentTexture(SharedImage? entry)
        {
            if (entry == null)
                return null;

            if (entry.Frames != null && entry.Frames.Length > 0)
            {
                var i = GifPlayback.FrameIndex(Time.unscaledTime, entry.FrameSeconds, entry.Frames.Length);
                var frame = entry.Frames[i];
                if (AvatarCache.IsUsableTexture(frame))
                    return frame;

                for (var j = 0; j < entry.Frames.Length; j++)
                {
                    if (AvatarCache.IsUsableTexture(entry.Frames[j]))
                        return entry.Frames[j];
                }
            }

            return AvatarCache.IsUsableTexture(entry.Texture) ? entry.Texture : null;
        }

        /// <summary>True while the reference is drawable or still loading; false once it is a dead end.</summary>
        public static bool Request(string? imageRef)
        {
            if (string.IsNullOrEmpty(imageRef))
                return false;

            var entry = GetOrAdd(imageRef!);
            if (entry.HasImage || entry.Pending)
                return true;
            if (entry.Failed)
                return false;

            if (Time.unscaledTime < entry.NextSoftTry)
                return true;

            // Official Avtr_*/Af_* must not take SpriteManager → SpriteBake (atlas collage).
            // Route them through CaptureRoot / LiveGifHost instead.
            var officialKey =
                OfficialImageKeys.IsOutlineKey(imageRef!) ||
                OfficialImageKeys.IsLikelyGifKey(imageRef!) ||
                imageRef!.StartsWith("Avtr_", StringComparison.OrdinalIgnoreCase);

            if (!officialKey)
            {
                var lookup = GameSpriteResolver.Lookup(imageRef!, out var sprite);
                if (lookup == SpriteLookup.Hit)
                {
                    ApplySprite(entry, sprite);
                    return true;
                }

                if (lookup == SpriteLookup.Retry)
                    return true;
            }

            var url = GameSpriteResolver.ToUrl(imageRef!);

            if (!GameSpriteResolver.IsDownloadable(imageRef!))
            {
                entry.Pending = true;
                if (GameSpriteResolver.TryStartGameLoad(imageRef!, frames =>
                {
                    // Prefab GRGif is still filling spriteList — do not burn SoftAttempts.
                    // Pending stays false; NextSoftTry spaces the next LiveGifHost poll.
                    if (frames == null && LiveGifHost.IsPending(imageRef!))
                    {
                        entry.Pending = false;
                        entry.NextSoftTry = Time.unscaledTime + SoftAttemptSpacing;
                        return;
                    }

                    entry.Pending = false;
                    if (frames != null && frames.Length > 0)
                    {
                        var dt = LiveGifHost.LastFrameSeconds > 0f
                            ? LiveGifHost.LastFrameSeconds
                            : GifPlayback.DefaultFrameSeconds;
                        ApplyFrames(entry, frames, dt);
                        return;
                    }

                    NoteSoftMiss(entry, imageRef!, "game avatar load failed");
                }))
                {
                    return true;
                }

                entry.Pending = false;
                // Registry not ready (TryLoad returned false) — retry without burning SoftAttempts.
                entry.NextSoftTry = Time.unscaledTime + SoftAttemptSpacing;
                return true;
            }

            if (!AvatarLoader.WillAttempt(url))
            {
                Fail(entry, imageRef!, "no local sprite and url rejected");
                return false;
            }

            var started = AvatarLoader.RequestShared(url, texture =>
            {
                entry.Pending = false;
                if (texture == null)
                {
                    Fail(entry, imageRef!, "download failed");
                    return;
                }

                ApplyTexture(entry, texture);
            });

            entry.Pending = started;
            return true;
        }

        public static void Clear()
        {
            foreach (var entry in _byRef.Values)
                DestroyOwned(entry);

            _byRef.Clear();
            _failuresLogged = 0;
            SpriteBake.Reset();
            LiveGifHost.Clear();
            PlatformIcons.Reset();
        }

        private static void ApplySprite(SharedImage entry, Sprite? sprite)
        {
            if (sprite == null)
                return;

            var baked = SpriteBake.ToTexture(sprite);
            if (baked != null)
            {
                ApplyTexture(entry, baked);
                return;
            }

            DestroyFrames(entry);
            entry.Sprite = sprite;
        }

        private static void ApplyTexture(SharedImage entry, Texture2D texture)
        {
            DestroyFrames(entry);
            if (entry.Texture != null && entry.Texture != texture)
            {
                try { UnityEngine.Object.Destroy(entry.Texture); }
                catch { /* already gone */ }
            }

            entry.Texture = texture;
            entry.Sprite = null;
            entry.Failed = false;
        }

        private static void ApplyFrames(SharedImage entry, Texture2D[] frames, float frameSeconds)
        {
            DestroyOwned(entry);
            entry.Frames = frames;
            entry.FrameSeconds = frameSeconds > 0f ? frameSeconds : GifPlayback.DefaultFrameSeconds;
            entry.Texture = null;
            entry.Sprite = null;
            entry.Failed = false;
        }

        private static void DestroyOwned(SharedImage entry)
        {
            if (entry.Texture != null)
            {
                try { UnityEngine.Object.Destroy(entry.Texture); } catch { /* ok */ }
                entry.Texture = null;
            }

            DestroyFrames(entry);
        }

        private static void DestroyFrames(SharedImage entry)
        {
            if (entry.Frames == null)
                return;

            for (var i = 0; i < entry.Frames.Length; i++)
            {
                var tex = entry.Frames[i];
                if (tex == null)
                    continue;

                try { UnityEngine.Object.Destroy(tex); } catch { /* ok */ }
            }

            entry.Frames = null;
        }

        private static SharedImage GetOrAdd(string imageRef)
        {
            if (_byRef.TryGetValue(imageRef, out var entry))
                return entry;

            entry = new SharedImage();
            _byRef[imageRef] = entry;
            return entry;
        }

        private static void NoteSoftMiss(SharedImage entry, string imageRef, string reason)
        {
            entry.SoftAttempts++;
            entry.NextSoftTry = Time.unscaledTime + SoftAttemptSpacing;
            if (entry.SoftAttempts >= SoftAttemptLimit)
                Fail(entry, imageRef, reason);
        }

        private static void Fail(SharedImage entry, string imageRef, string reason)
        {
            entry.Failed = true;
            if (_failuresLogged >= 10)
                return;

            _failuresLogged++;
            MelonLogger.Warning("[FriendOverlay] shared image failed (" + reason + "): " + imageRef);
        }
    }
}
