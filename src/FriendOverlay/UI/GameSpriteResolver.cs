using System;
using System.Collections.Generic;
using Il2CppGameRiver;
using Il2CppGameRiver.Client;
using MelonLoader;
using UnityEngine;

namespace FriendOverlay.UI
{
    /// <summary>
    /// Outcome of a local sprite lookup. The distinction between Retry and Miss is the whole point:
    /// callers used to treat "the sprite manager does not exist yet" the same as "the manager says it
    /// does not have this", and latched a dead end on the first frame of a session.
    /// </summary>
    public enum SpriteLookup
    {
        Hit = 0,
        Retry = 1,
        Miss = 2,
    }

    /// <summary>
    /// Turns a game image reference (sprite name or relative URL) into either a local sprite from the
    /// game's SpriteManager or a downloadable URL. Keyed by reference, not by player, because official
    /// avatars, frames and the moderation placeholder are shared assets.
    /// </summary>
    public static class GameSpriteResolver
    {
        private const float SpriteManagerRetrySeconds = 1f;

        /// <summary>
        /// How many answered misses, spaced by <see cref="MissRetrySeconds"/>, before a reference is
        /// written off. GetDynamicSprite in particular can answer null on the call that starts the load,
        /// so one miss proves nothing.
        /// </summary>
        private const int MissAttemptsBeforeGivingUp = 3;

        private const float MissRetrySeconds = 1f;

        /// <summary>Diagnostic lines per session, so a broken asset table names itself once.</summary>
        private const int MaxRouteLogs = 6;

        private static readonly HashSet<string> _localMiss = new HashSet<string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, MissState> _misses =
            new Dictionary<string, MissState>(StringComparer.Ordinal);

        private static readonly Dictionary<string, string> _urls = new Dictionary<string, string>(StringComparer.Ordinal);

        private static SpriteManager? _spriteManager;
        private static bool _spriteManagerFailed;
        private static float _spriteManagerNextTry;
        private static bool _loggedNoSpriteManager;
        private static bool _loggedLocalSpriteError;
        private static int _hitLogs;
        private static int _missLogs;

        private sealed class MissState
        {
            public int Attempts;
            public float NextTry;
        }

        /// <summary>
        /// Looks the reference up in the game's local sprite tables. Only a manager that was reachable
        /// and answered "no" repeatedly produces <see cref="SpriteLookup.Miss"/>; everything else is
        /// <see cref="SpriteLookup.Retry"/>, because a session-long letter is a much worse outcome than
        /// one more lookup next frame.
        /// </summary>
        public static SpriteLookup Lookup(string imageRef, out Sprite? sprite)
        {
            sprite = null;
            if (string.IsNullOrEmpty(imageRef))
                return SpriteLookup.Miss;

            if (_localMiss.Contains(imageRef))
                return SpriteLookup.Miss;

            var sm = GetSpriteManager();
            if (sm == null)
                return SpriteLookup.Retry;

            if (_misses.TryGetValue(imageRef, out var pending) && Time.unscaledTime < pending.NextTry)
                return SpriteLookup.Retry;

            try
            {
                // Fail closed: without the manager's placeholder id we cannot tell a real icon from
                // the "missing asset" sprite, and drawing that is exactly the blank-box bug.
                Sprite? fallback = null;
                try { fallback = sm.defaultSprite; } catch { fallback = null; }
                if (fallback == null)
                    return SpriteLookup.Retry;

                var defaultId = fallback.GetInstanceID();

                var via = string.Empty;

                Sprite? candidate = null;
                try { candidate = sm.GetSprite(imageRef); } catch { /* miss */ }
                if (IsUsable(candidate, defaultId))
                    via = "name";

                if (via.Length == 0)
                {
                    try { candidate = sm.GetDynamicSprite(imageRef); } catch { /* miss */ }
                    if (IsUsable(candidate, defaultId))
                        via = "dynamic";
                }

                // Portraits may live in the SpriteType.PlayerPortrait table, which the untyped lookups
                // above do not search. Note there is no PlayerPortraitSpriteSelector in this build's
                // metadata (only CommanderSkill / Technology / Mech / Officer), so this may well answer
                // nothing — the miss log names every candidate so one run settles it.
                var names = PortraitNames(sm, imageRef);
                if (via.Length == 0 && TryPortraitSprite(sm, names, imageRef, defaultId, out var typed, out var typedVia))
                {
                    candidate = typed;
                    via = typedVia;
                }

                if (via.Length == 0)
                    return RecordMiss(imageRef, names);

                _misses.Remove(imageRef);
                LogHit("local sprite via " + via + ": " + imageRef);
                sprite = candidate;
                return SpriteLookup.Hit;
            }
            catch (Exception ex)
            {
                if (!_loggedLocalSpriteError)
                {
                    _loggedLocalSpriteError = true;
                    MelonLogger.Warning("[FriendOverlay] local sprite lookup failed: " + ex.Message);
                }

                return SpriteLookup.Retry;
            }
        }

        private static SpriteLookup RecordMiss(string imageRef, List<string> names)
        {
            if (!_misses.TryGetValue(imageRef, out var miss))
            {
                miss = new MissState();
                _misses[imageRef] = miss;
            }

            miss.Attempts++;
            miss.NextTry = Time.unscaledTime + MissRetrySeconds;
            if (miss.Attempts < MissAttemptsBeforeGivingUp)
                return SpriteLookup.Retry;

            _localMiss.Add(imageRef);
            LogMiss("local sprite miss: " + imageRef + " tried=" + string.Join("|", names.ToArray()));
            return SpriteLookup.Miss;
        }

        /// <summary>
        /// Looks the reference up in the portrait sprite table, trying the name the caller gave us and
        /// whatever name the game's own selector maps it to. The setting name is not always the sprite
        /// name, so both are attempted before declaring a miss.
        /// </summary>
        private static bool TryPortraitSprite(
            SpriteManager sm,
            List<string> names,
            string imageRef,
            int defaultId,
            out Sprite? sprite,
            out string via)
        {
            sprite = null;
            via = string.Empty;

            foreach (var name in names)
            {
                if (string.IsNullOrEmpty(name))
                    continue;

                Sprite? candidate = null;

                // useType 0 is the plain variant; the game passes a quality level here for props.
                try { candidate = sm.GetSprite(name, SpriteType.PlayerPortrait, 0); } catch { /* miss */ }
                if (IsUsable(candidate, defaultId))
                {
                    sprite = candidate;
                    via = "portrait:" + name;
                    return true;
                }

                if (!string.Equals(name, imageRef, StringComparison.Ordinal))
                {
                    try { candidate = sm.GetSprite(name); } catch { /* miss */ }
                    if (IsUsable(candidate, defaultId))
                    {
                        sprite = candidate;
                        via = "selector:" + name;
                        return true;
                    }
                }
            }

            return false;
        }

        private static List<string> PortraitNames(SpriteManager sm, string imageRef)
        {
            var names = new List<string>(4) { imageRef };

            try
            {
                if (sm.TryGetSpriteSettingName(imageRef, SpriteType.PlayerPortrait, out var setting) &&
                    !string.IsNullOrEmpty(setting))
                    names.Add(setting!);
            }
            catch
            {
                // binding absent; the plain name is still worth trying
            }

            try
            {
                if (sm.TryGetSpriteSelector(SpriteType.PlayerPortrait, out var selector) && selector != null)
                {
                    var settingName = selector.GetSpriteSettingName(imageRef);
                    if (!string.IsNullOrEmpty(settingName))
                        names.Add(settingName!);

                    var spriteName = selector.GetSpriteName(imageRef, 0);
                    if (!string.IsNullOrEmpty(spriteName))
                        names.Add(spriteName!);
                }
            }
            catch
            {
                // selector shape changed; fall through with what we have
            }

            return names;
        }

        /// <summary>
        /// Hands the reference to the game's keyed avatar registry (or LoadSprite for real URLs).
        /// Returns false when the registry is not ready yet — the caller must retry, not Fail.
        /// Official keys are delivered as owned <see cref="Texture2D"/> arrays (caller destroys).
        /// </summary>
        public static bool TryStartGameLoad(string imageRef, Action<Texture2D[]?> done)
        {
            var sm = GetSpriteManager();
            if (IsDownloadable(imageRef) && sm == null)
                return false;

            return GameSpriteLoader.TryLoad(imageRef, sm, frames =>
            {
                if (frames != null && frames.Length > 0)
                {
                    var any = false;
                    for (var i = 0; i < frames.Length; i++)
                    {
                        if (AvatarCache.IsUsableTexture(frames[i]))
                        {
                            any = true;
                            break;
                        }
                    }

                    if (any)
                    {
                        _localMiss.Remove(imageRef);
                        _misses.Remove(imageRef);
                        LogHit("game avatar loaded: " + imageRef + " frames=" + frames.Length);
                        done(frames);
                        return;
                    }
                }

                if (frames != null)
                {
                    for (var i = 0; i < frames.Length; i++)
                    {
                        if (frames[i] == null)
                            continue;
                        try { UnityEngine.Object.Destroy(frames[i]); } catch { /* ok */ }
                    }
                }

                // GRGif settle window: not a miss — SharedImageCache polls again via IsPending.
                if (LiveGifHost.IsPending(imageRef))
                {
                    done(null);
                    return;
                }

                LogMiss("game avatar load failed: " + imageRef);
                done(null);
            });
        }

        /// <summary>
        /// Whether the downloader could actually fetch this reference. A bare sprite name such as
        /// Avtr_05-16A is not a URL, and handing it over only recorded a bogus download failure and
        /// blacklisted it for the session.
        /// </summary>
        public static bool IsDownloadable(string imageRef)
        {
            var url = ToUrl(imageRef);
            return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
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

        /// <summary>
        /// Hits and misses have separate budgets on purpose: a shared cap let a handful of early misses
        /// eat it and hide the one success line that acceptance item 27 asks the tester to look for.
        /// </summary>
        private static void LogHit(string message)
        {
            if (_hitLogs >= MaxRouteLogs)
                return;

            _hitLogs++;
            MelonLogger.Msg("[FriendOverlay] " + message);
        }

        private static void LogMiss(string message)
        {
            if (_missLogs >= MaxRouteLogs)
                return;

            _missLogs++;
            MelonLogger.Msg("[FriendOverlay] " + message);
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
            GameSpriteLoader.Reset();
            _spriteManager = null;
            _spriteManagerFailed = false;
            _spriteManagerNextTry = 0f;
            _localMiss.Clear();
            _misses.Clear();
            _urls.Clear();
            _loggedNoSpriteManager = false;
            _loggedLocalSpriteError = false;
            _hitLogs = 0;
            _missLogs = 0;
        }
    }
}
