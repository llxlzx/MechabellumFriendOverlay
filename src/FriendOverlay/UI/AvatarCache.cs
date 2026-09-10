using System.Collections.Generic;
using UnityEngine;

namespace FriendOverlay.UI
{
    /// <summary>
    /// One avatar image per friend. Textures come from <see cref="AvatarLoader"/> and are owned by
    /// us; sprites come from the game's own SpriteManager and are owned by the game, so they are
    /// never destroyed here.
    /// </summary>
    public sealed class AvatarEntry
    {
        public Sprite? Sprite;
        public Texture2D? Texture;

        /// <summary>Unused since the native-cell borrow tier was retired; kept for API stability.</summary>
        public bool Weak;

        /// <summary>The game hides this friend's face, so nothing may be stored or drawn for them.</summary>
        public bool Blocked;

        public bool HasImage => Sprite != null || Texture != null;
    }

    public static class AvatarCache
    {
        private const int Capacity = 512;
        private const int MinSize = 2;

        private static readonly Dictionary<ulong, AvatarEntry> _byUser = new Dictionary<ulong, AvatarEntry>();
        private static readonly Queue<ulong> _order = new Queue<ulong>();

        public static int Count => _byUser.Count;

        /// <summary>Returns an entry only when it has something we are willing to draw.</summary>
        public static AvatarEntry? Get(ulong userId) =>
            _byUser.TryGetValue(userId, out var entry) && IsDrawable(entry) ? entry : null;

        /// <summary>True when a drawable image would still be an upgrade.</summary>
        public static bool NeedsImage(ulong userId) =>
            !_byUser.TryGetValue(userId, out var entry) || !IsDrawable(entry);

        /// <summary>True when this uid is currently refusing images because the game hides its face.</summary>
        public static bool IsBlocked(ulong userId) =>
            _byUser.TryGetValue(userId, out var entry) && entry.Blocked;

        /// <summary>
        /// Drops whatever we hold for a moderation-blocked friend and refuses future writes, so a
        /// download that was already in flight cannot re-populate the entry behind our back.
        /// </summary>
        public static void Block(ulong userId)
        {
            if (userId == 0)
                return;

            var entry = Reserve(userId);
            if (entry == null)
                return;

            DestroyTexture(entry);
            entry.Sprite = null;
            entry.Weak = false;
            entry.Blocked = true;
        }

        /// <summary>
        /// Re-arms a previously blocked uid. Without this the row would stay a letter forever and
        /// re-request its URL every frame once the game's moderation setting is turned off.
        /// </summary>
        public static void Unblock(ulong userId)
        {
            if (_byUser.TryGetValue(userId, out var entry))
                entry.Blocked = false;
        }

        public static bool IsDrawable(AvatarEntry? entry)
        {
            if (entry == null || entry.Blocked)
                return false;

            if (IsUsableTexture(entry.Texture))
                return true;

            return IsUsableSprite(entry.Sprite);
        }

        public static bool IsUsableSprite(Sprite? sprite)
        {
            if (sprite == null)
                return false;

            try
            {
                var tex = sprite.texture;
                if (tex == null || tex.width < MinSize || tex.height < MinSize)
                    return false;

                var tr = sprite.textureRect;
                return tr.width >= MinSize && tr.height >= MinSize;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsUsableTexture(Texture2D? texture)
        {
            if (texture == null)
                return false;

            try
            {
                return texture.width >= MinSize && texture.height >= MinSize;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Stores a game-owned sprite. Refused sprites are never destroyed.</summary>
        public static void PutSprite(ulong userId, Sprite sprite, bool weak)
        {
            if (sprite == null || userId == 0 || !IsUsableSprite(sprite))
                return;

            if (IsBlocked(userId))
                return;

            var entry = Reserve(userId);
            if (entry == null || entry.Blocked)
                return;

            entry.Sprite = sprite;
            entry.Weak = weak;
            DestroyTexture(entry);
        }

        /// <summary>Takes ownership of <paramref name="texture"/>; every refusal path destroys it.</summary>
        public static void PutTexture(ulong userId, Texture2D texture)
        {
            if (texture == null)
                return;

            if (userId == 0 || !IsUsableTexture(texture) || IsBlocked(userId))
            {
                Destroy(texture);
                return;
            }

            var entry = Reserve(userId);
            if (entry == null || entry.Blocked)
            {
                Destroy(texture);
                return;
            }

            DestroyTexture(entry);
            entry.Texture = texture;
            entry.Sprite = null;
            entry.Weak = false;
        }

        public static void Clear()
        {
            foreach (var entry in _byUser.Values)
                DestroyTexture(entry);

            _byUser.Clear();
            _order.Clear();
        }

        private static AvatarEntry? Reserve(ulong userId)
        {
            if (_byUser.TryGetValue(userId, out var existing))
                return existing;

            while (_order.Count >= Capacity)
            {
                var evicted = _order.Dequeue();
                if (_byUser.TryGetValue(evicted, out var old))
                {
                    DestroyTexture(old);
                    _byUser.Remove(evicted);
                }
            }

            var entry = new AvatarEntry();
            _byUser[userId] = entry;
            _order.Enqueue(userId);
            return entry;
        }

        private static void DestroyTexture(AvatarEntry entry)
        {
            if (entry.Texture == null)
                return;

            Destroy(entry.Texture);
            entry.Texture = null;
        }

        private static void Destroy(Texture2D texture)
        {
            try
            {
                UnityEngine.Object.Destroy(texture);
            }
            catch
            {
                // already gone
            }
        }
    }
}
