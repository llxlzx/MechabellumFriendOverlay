using System;
using System.Collections.Generic;
using Il2CppGameRiver;
using Il2CppGameRiver.Client;
using MelonLoader;
using UnityEngine;

namespace FriendOverlay.UI
{
    /// <summary>
    /// Turns a game image reference (sprite name or relative URL) into either a local sprite from the
    /// game's SpriteManager or a downloadable URL. Keyed by reference, not by player, because official
    /// avatars, frames and the moderation placeholder are shared assets.
    /// </summary>
    public static class GameSpriteResolver
    {
        private const float SpriteManagerRetrySeconds = 1f;

        private static readonly HashSet<string> _localMiss = new HashSet<string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> _urls = new Dictionary<string, string>(StringComparer.Ordinal);

        private static SpriteManager? _spriteManager;
        private static bool _spriteManagerFailed;
        private static float _spriteManagerNextTry;
        private static bool _loggedNoSpriteManager;
        private static bool _loggedLocalSpriteError;

        /// <summary>
        /// True with a usable, non-placeholder sprite. A miss the SpriteManager actually answered is
        /// memoized; a miss caused by the manager being unavailable is not, so it retries.
        /// </summary>
        public static bool TryGetLocalSprite(string imageRef, out Sprite? sprite)
        {
            sprite = null;
            if (string.IsNullOrEmpty(imageRef) || _localMiss.Contains(imageRef))
                return false;

            var sm = GetSpriteManager();
            if (sm == null)
                return false;

            try
            {
                // Fail closed: without the manager's placeholder id we cannot tell a real icon from
                // the "missing asset" sprite, and drawing that is exactly the blank-box bug.
                Sprite? fallback = null;
                try { fallback = sm.defaultSprite; } catch { fallback = null; }
                if (fallback == null)
                    return false;

                var defaultId = fallback.GetInstanceID();

                Sprite? candidate = null;
                try { candidate = sm.GetSprite(imageRef); } catch { /* miss */ }
                if (!IsUsable(candidate, defaultId))
                {
                    try { candidate = sm.GetDynamicSprite(imageRef); } catch { /* miss */ }
                }

                if (!IsUsable(candidate, defaultId))
                {
                    _localMiss.Add(imageRef);
                    return false;
                }

                sprite = candidate;
                return true;
            }
            catch (Exception ex)
            {
                if (!_loggedLocalSpriteError)
                {
                    _loggedLocalSpriteError = true;
                    MelonLogger.Warning("[FriendOverlay] local sprite lookup failed: " + ex.Message);
                }

                return false;
            }
        }

        /// <summary>Runs the game's portrait URL fixer once per reference.</summary>
        public static string ToUrl(string imageRef)
        {
            if (string.IsNullOrEmpty(imageRef))
                return string.Empty;

            if (_urls.TryGetValue(imageRef, out var cached))
                return cached;

            var url = imageRef;
            try
            {
                var fixedUrl = Utility.TryFixPortraitURL(imageRef);
                if (!string.IsNullOrEmpty(fixedUrl))
                    url = fixedUrl!;
            }
            catch
            {
                // keep raw
            }

            _urls[imageRef] = url;
            return url;
        }

        private static bool IsUsable(Sprite? sprite, int defaultId)
        {
            if (!AvatarCache.IsUsableSprite(sprite))
                return false;

            try
            {
                return sprite!.GetInstanceID() != defaultId;
            }
            catch
            {
                return false;
            }
        }

        private static SpriteManager? GetSpriteManager()
        {
            if (_spriteManagerFailed)
                return null;

            if (_spriteManager != null)
                return _spriteManager;

            // This runs per reference per frame while downloads are queued, so a missing manager must
            // not turn into a full scene scan every frame.
            if (Time.unscaledTime < _spriteManagerNextTry)
                return null;

            _spriteManagerNextTry = Time.unscaledTime + SpriteManagerRetrySeconds;

            try
            {
                var ui = UnityEngine.Object.FindObjectOfType<GRUIManager>();
                var sm = ui?.spriteManager ?? ui?.GetSpriteManager();
                if (sm != null)
                {
                    _spriteManager = sm;
                    return _spriteManager;
                }

                if (!_loggedNoSpriteManager)
                {
                    _loggedNoSpriteManager = true;
                    MelonLogger.Warning("[FriendOverlay] local sprite route unavailable: no SpriteManager yet");
                }
            }
            catch (Exception ex)
            {
                _spriteManagerFailed = true;
                MelonLogger.Warning("[FriendOverlay] SpriteManager unavailable: " + ex.Message);
            }

            return null;
        }

        public static void Reset()
        {
            _spriteManager = null;
            _spriteManagerFailed = false;
            _spriteManagerNextTry = 0f;
            _localMiss.Clear();
            _urls.Clear();
            _loggedNoSpriteManager = false;
            _loggedLocalSpriteError = false;
        }
    }
}
