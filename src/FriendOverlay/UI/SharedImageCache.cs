using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;

namespace FriendOverlay.UI
{
    public sealed class SharedImage
    {
        public Sprite? Sprite;
        public Texture2D? Texture;
        public bool Pending;
        public bool Failed;

        public bool HasImage => AvatarCache.IsUsableTexture(Texture) || AvatarCache.IsUsableSprite(Sprite);
    }

    /// <summary>
    /// Images shared between players: official avatars, avatar frames, the moderation placeholder.
    /// Keyed by the game's image reference. Sprites are game-owned and never destroyed here; textures
    /// come from <see cref="AvatarLoader"/> and are ours to release.
    /// </summary>
    public static class SharedImageCache
    {
        private static readonly Dictionary<string, SharedImage> _byRef =
            new Dictionary<string, SharedImage>(StringComparer.Ordinal);

        private static int _failuresLogged;

        public static SharedImage? Get(string? imageRef)
        {
            if (string.IsNullOrEmpty(imageRef))
                return null;

            return _byRef.TryGetValue(imageRef!, out var entry) && entry.HasImage ? entry : null;
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

            var lookup = GameSpriteResolver.Lookup(imageRef!, out var sprite);
            if (lookup == SpriteLookup.Hit)
            {
                entry.Sprite = sprite;
                return true;
            }

            // Not a miss yet — the sprite manager may not even exist this early in a session. Latching a
            // failure here is what would turn a slow start into letters for the rest of the session.
            if (lookup == SpriteLookup.Retry)
                return true;

            var url = GameSpriteResolver.ToUrl(imageRef!);

            // A reference that is not a URL cannot be downloaded, and pretending otherwise is what
            // produced a "download failed" line for every official avatar. Those go to the game's own
            // sprite loader instead, which reaches assets the local tables do not hold yet. The resolved
            // value is logged because it is the only way to tell a bare sprite name from a URL the
            // game's fixer built and got wrong.
            if (!GameSpriteResolver.IsDownloadable(imageRef!))
            {
                // Set first: the callback can fire before TryStartGameLoad returns.
                entry.Pending = true;
                if (GameSpriteResolver.TryStartGameLoad(imageRef!, sprite =>
                {
                    entry.Pending = false;
                    if (sprite == null)
                    {
                        Fail(entry, imageRef!, "game avatar load failed");
                        return;
                    }

                    entry.Sprite = sprite;
                }))
                {
                    return true;
                }

                // Registry not awake yet — same as Lookup.Retry. Failing here would turn the first
                // open of the panel into permanent letters for every official avatar.
                entry.Pending = false;
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

                if (entry.Texture != null && entry.Texture != texture)
                {
                    try { UnityEngine.Object.Destroy(entry.Texture); }
                    catch { /* already gone */ }
                }

                entry.Texture = texture;
            });

            // Not started means "try again next tick" (capacity), not "give up".
            entry.Pending = started;
            return true;
        }

        public static void Clear()
        {
            foreach (var entry in _byRef.Values)
            {
                if (entry.Texture == null)
                    continue;

                try { UnityEngine.Object.Destroy(entry.Texture); }
                catch { /* already gone */ }
            }

            _byRef.Clear();
            _failuresLogged = 0;
        }

        private static SharedImage GetOrAdd(string imageRef)
        {
            if (_byRef.TryGetValue(imageRef, out var entry))
                return entry;

            entry = new SharedImage();
            _byRef[imageRef] = entry;
            return entry;
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
