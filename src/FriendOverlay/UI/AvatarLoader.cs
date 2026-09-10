using System;
using System.Collections;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using UnityEngine.Networking;

namespace FriendOverlay.UI
{
    /// <summary>
    /// Primary avatar source: fetches the friend's face URL and decodes it into a texture we own.
    ///
    /// Uses Get + DownloadHandlerBuffer + ImageConversion.LoadImage because this game's IL2CPP
    /// bindings do not expose DownloadHandlerTexture(Boolean), which UnityWebRequestTexture needs.
    /// </summary>
    public static class AvatarLoader
    {
        private const int MaxConcurrent = 4;
        private const int TimeoutSeconds = 10;
        private const int MaxFailureWarnings = 30;

        // Keyed by string because per-player photos and shared assets share the concurrency budget:
        // "u:<uid>" for a player's own photo, "url:<url>" for an asset many players share.
        private static readonly HashSet<string> _inFlight = new HashSet<string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> _failedUrls = new Dictionary<string, string>(StringComparer.Ordinal);

        private static int _active;
        private static int _generation;
        private static int _failureWarnings;
        private static bool _loggedSampleUrl;
        private static bool _loggedNormalizeError;

        public static bool Enabled { get; set; } = true;

        public static int FailedUrlCount => _failedUrls.Count;

        /// <summary>
        /// True when this URL is still worth waiting on. In-flight and at-capacity both count as
        /// "try again later", not "give up" — the caller uses this to decide whether to fall back to
        /// the letter, and falling through too early would strand a download that was about to run.
        /// </summary>
        public static bool WillAttempt(string? rawUrl)
        {
            if (!Enabled || string.IsNullOrEmpty(rawUrl))
                return false;

            var url = Normalize(rawUrl!);
            return !string.IsNullOrEmpty(url) && !_failedUrls.ContainsKey(url);
        }

        /// <summary>
        /// Why this URL was given up on. Recorded for every failure regardless of the warning cap,
        /// so a route line can name its own cause instead of relying on a separately capped warning.
        /// </summary>
        public static bool TryGetFailure(string? rawUrl, out string reason)
        {
            reason = string.Empty;
            if (string.IsNullOrEmpty(rawUrl))
                return false;

            var url = Normalize(rawUrl!);
            if (string.IsNullOrEmpty(url) || !_failedUrls.TryGetValue(url, out var stored))
                return false;

            reason = stored;
            return true;
        }

        /// <summary>
        /// Runs the game's own portrait URL fixer. Falls back to the raw value when the helper is
        /// unavailable, so a binding change degrades instead of killing avatars outright.
        /// </summary>
        private static string Normalize(string url)
        {
            try
            {
                var fixedUrl = Il2CppGameRiver.Utility.TryFixPortraitURL(url);
                if (!string.IsNullOrEmpty(fixedUrl))
                    return fixedUrl;
            }
            catch (Exception ex)
            {
                // WillAttempt runs per row per frame, so this must never become per-frame spam.
                if (!_loggedNormalizeError)
                {
                    _loggedNormalizeError = true;
                    MelonLogger.Warning("[FriendOverlay] TryFixPortraitURL unavailable: " + ex.Message);
                }
            }

            return url;
        }

        public static void Request(ulong userId, string? rawUrl)
        {
            if (!Enabled || userId == 0 || string.IsNullOrEmpty(rawUrl))
                return;

            // The game never fetches a raw FaceUrl; it normalizes first, so a partial path would
            // otherwise 404 and get this friend blacklisted for the session.
            var url = Normalize(rawUrl!);
            if (string.IsNullOrEmpty(url))
                return;

            if (!AvatarCache.NeedsImage(userId) || AvatarCache.IsBlocked(userId))
                return;

            Start("u:" + userId, url, texture =>
            {
                if (texture != null)
                    AvatarCache.PutTexture(userId, texture);
            });
        }

        /// <summary>
        /// Downloads an asset that is not tied to one player (official avatar, frame, placeholder).
        /// <paramref name="onDone"/> receives null on failure and owns the texture on success.
        /// Returns false when the download could not start now (capacity, duplicate, failed URL).
        /// </summary>
        public static bool RequestShared(string? rawUrl, Action<Texture2D?> onDone)
        {
            if (!Enabled || string.IsNullOrEmpty(rawUrl) || onDone == null)
                return false;

            var url = Normalize(rawUrl!);
            if (string.IsNullOrEmpty(url))
                return false;

            return Start("url:" + url, url, onDone);
        }

        private static bool Start(string key, string url, Action<Texture2D?> sink)
        {
            if (_active >= MaxConcurrent || _inFlight.Contains(key) || _failedUrls.ContainsKey(url))
                return false;

            _inFlight.Add(key);
            _active++;

            if (!_loggedSampleUrl)
            {
                _loggedSampleUrl = true;
                MelonLogger.Msg("[FriendOverlay] avatar url sample: " + url);
            }

            var generation = _generation;
            try
            {
                MelonCoroutines.Start(Download(key, url, generation, sink));
                return true;
            }
            catch (Exception ex)
            {
                Finish(key, generation);
                Disable("avatar download disabled: " + ex.Message);
                return false;
            }
        }

        public static void Reset()
        {
            // Coroutines from the previous session finish later; bump the generation so their
            // results are discarded. _active keeps counting them, otherwise the new session could
            // run four more downloads on top of the ones still in flight.
            _generation++;
            _inFlight.Clear();
            _failedUrls.Clear();

            // Re-arm. A one-off failure that killed the loader must not leave every later session
            // without avatars until the game is restarted; the cost of being wrong is one retry
            // and one warning per session.
            Enabled = true;
            _failureWarnings = 0;
            _loggedSampleUrl = false;
            _loggedNormalizeError = false;
        }

        private static IEnumerator Download(string key, string url, int generation, Action<Texture2D?> sink)
        {
            UnityWebRequest? request = null;
            var started = false;

            try
            {
                request = UnityWebRequest.Get(url);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = TimeoutSeconds;
                request.SendWebRequest();
                started = true;
            }
            catch (Exception ex)
            {
                // MissingMethod / broken interop is infrastructure — kill the loader, do not
                // poison every URL in the friends list for this session.
                if (IsInfrastructureFailure(ex))
                    Disable("avatar download disabled: " + ex.Message);
                else
                    Fail(url, "request failed: " + ex.Message, generation);
            }

            if (!started || request == null)
            {
                Dispose(request);
                Finish(key, generation);
                SafeSink(sink, null, generation);
                yield break;
            }

            while (!IsDone(request))
                yield return null;

            Complete(key, url, request, generation, sink);
        }

        private static void Dispose(UnityWebRequest? request)
        {
            try
            {
                request?.Dispose();
            }
            catch
            {
                // ignore
            }
        }

        private static bool IsDone(UnityWebRequest request)
        {
            try
            {
                return request.isDone;
            }
            catch
            {
                return true;
            }
        }

        private static void Complete(string key, string url, UnityWebRequest request, int generation, Action<Texture2D?> sink)
        {
            // Held outside the try so a throw inside LoadImage cannot strand the allocation.
            Texture2D? texture = null;
            var delivered = false;

            try
            {
                // A result from a finished session must not touch the new session's cache,
                // failed-URL set, or textures.
                if (generation != _generation)
                    return;

                var error = request.error;
                if (!string.IsNullOrEmpty(error))
                {
                    Fail(url, "http: " + error, generation);
                    return;
                }

                var handler = request.downloadHandler;
                if (handler == null)
                {
                    Fail(url, "no download handler", generation);
                    return;
                }

                var bytes = handler.data;
                if (bytes == null || bytes.Length == 0)
                {
                    Fail(url, "empty body", generation);
                    return;
                }

                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(texture, bytes, true))
                {
                    Fail(url, "decode failed (" + bytes.Length + " bytes)", generation);
                    return;
                }

                // The sink owns it now, including on its own refusal paths.
                var handoff = texture;
                texture = null;
                delivered = true;
                SafeSink(sink, handoff, generation);
            }
            catch (Exception ex)
            {
                if (IsInfrastructureFailure(ex))
                    Disable("avatar download disabled: " + ex.Message);
                else
                    Fail(url, "decode threw: " + ex.Message, generation);
            }
            finally
            {
                if (texture != null)
                {
                    try { UnityEngine.Object.Destroy(texture); }
                    catch { /* already gone */ }
                }

                Dispose(request);
                Finish(key, generation);
                if (!delivered)
                    SafeSink(sink, null, generation);
            }
        }

        /// <summary>
        /// Hands a result to its owner. A result from a finished session must not touch the new
        /// session's caches, so it is dropped and the texture destroyed rather than leaked.
        /// </summary>
        private static void SafeSink(Action<Texture2D?> sink, Texture2D? texture, int generation)
        {
            if (generation != _generation)
            {
                if (texture != null)
                {
                    try { UnityEngine.Object.Destroy(texture); }
                    catch { /* already gone */ }
                }

                return;
            }

            try
            {
                sink(texture);
            }
            catch (Exception ex)
            {
                // Ownership never transferred, so this texture has no owner left to destroy it.
                if (texture != null)
                {
                    try { UnityEngine.Object.Destroy(texture); }
                    catch { /* already gone */ }
                }

                MelonLogger.Warning("[FriendOverlay] avatar sink threw: " + ex.Message);
            }
        }

        /// <summary>Marks a URL permanently failed for this session and reports it, up to a cap.</summary>
        private static void Fail(string url, string reason, int generation)
        {
            if (generation != _generation)
                return;

            if (_failedUrls.ContainsKey(url))
                return;

            _failedUrls[url] = reason;

            if (_failureWarnings >= MaxFailureWarnings)
                return;

            _failureWarnings++;
            MelonLogger.Warning("[FriendOverlay] avatar url-failed: " + reason + " url=" + url);
            if (_failureWarnings == MaxFailureWarnings)
                MelonLogger.Warning("[FriendOverlay] avatar url-failed: warning cap reached, further failures counted only");
        }

        private static bool IsInfrastructureFailure(Exception ex) =>
            ex is MissingMethodException or MissingFieldException or TypeLoadException ||
            ex.Message.IndexOf("Method not found", StringComparison.OrdinalIgnoreCase) >= 0;

        private static void Disable(string message)
        {
            if (!Enabled)
                return;

            Enabled = false;
            MelonLogger.Warning("[FriendOverlay] " + message);
        }

        private static void Finish(string key, int generation)
        {
            if (_active > 0)
                _active--;

            if (generation == _generation)
                _inFlight.Remove(key);
        }
    }
}
