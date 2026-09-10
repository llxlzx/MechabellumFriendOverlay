using System;
using System.Collections.Generic;
using FriendOverlay.Compat;
using FriendOverlay.Core;
using Il2CppGameRiver;
using Il2CppProtos.Friend;
using MelonLoader;
using GameRiskInfo = Il2CppGameRiver.PlayerRiskInfo;

namespace FriendOverlay.Data
{
    /// <summary>
    /// Asks the game which image it would show for a friend. Rebuilds the same PlayerRiskInfo the
    /// native cell builds and reads portrait / avatar / outline from it, then lets
    /// <see cref="PortraitPlanner"/> pick. Cached per uid on the inputs the answer depends on.
    /// Binding failures disable the resolver for the session and fall back to the photo-only route.
    /// </summary>
    public static class PortraitResolver
    {
        private readonly struct Key
        {
            private readonly string _faceUrl;
            private readonly int _faceId;
            private readonly int _faceBorder;
            private readonly bool _blocked;

            public Key(string faceUrl, int faceId, int faceBorder, bool blocked)
            {
                _faceUrl = faceUrl;
                _faceId = faceId;
                _faceBorder = faceBorder;
                _blocked = blocked;
            }

            public bool Matches(in Key other) =>
                _faceId == other._faceId &&
                _faceBorder == other._faceBorder &&
                _blocked == other._blocked &&
                string.Equals(_faceUrl, other._faceUrl, StringComparison.Ordinal);
        }

        private sealed class Cached
        {
            public Key Key;
            public PortraitPlan Plan = Empty;
        }

        private static readonly PortraitPlan Empty = new PortraitPlan(PortraitKind.Letter, string.Empty, string.Empty);

        private static readonly Dictionary<ulong, Cached> _cache = new Dictionary<ulong, Cached>();
        private static bool _disabled;
        private static int _samplesLogged;
        private static bool _loggedError;

        public static PortraitPlan Resolve(FriendBaseInfo info, bool blocked)
        {
            if (info == null)
                return Empty;

            if (!Capabilities.Portrait || _disabled)
                return Legacy(info, blocked);

            ulong uid;
            Il2CppProtos.Common.PlayerRiskInfo? risk;
            int faceId;
            int faceBorder;
            try
            {
                uid = info.Userid;
                risk = info.RiskInfo;
                faceId = info.FaceId;
                faceBorder = info.FaceBorder;
            }
            catch
            {
                return Legacy(info, blocked);
            }

            if (risk == null)
                return Legacy(info, blocked);

            var faceUrl = string.Empty;
            try { faceUrl = risk.FaceUrl ?? string.Empty; } catch { faceUrl = string.Empty; }

            var key = new Key(faceUrl, faceId, faceBorder, blocked);
            if (_cache.TryGetValue(uid, out var cached) && cached.Key.Matches(key))
                return cached.Plan;

            try
            {
                var game = new GameRiskInfo(
                    risk.Name ?? string.Empty,
                    faceUrl,
                    risk.BlockName,
                    risk.BlockFace,
                    faceId,
                    faceBorder);

                var portraitInfo = game.GetPortraitInfo();
                var portrait = game.GetPortrait();
                var avatar = portraitInfo != null ? portraitInfo.GetAvatarURL() : game.GetAvatarURL();
                var outline = portraitInfo != null ? portraitInfo.GetAvatarOutLineURL() : game.GetAvatarOutLineURL();

                var plan = PortraitPlanner.Decide(blocked, portrait, avatar, outline);

                if (_samplesLogged < 3)
                {
                    _samplesLogged++;
                    MelonLogger.Msg(
                        "[FriendOverlay] portrait sample uid=" + uid +
                        " faceId=" + faceId + " faceBorder=" + faceBorder + " blocked=" + blocked +
                        " -> " + plan.Kind + " image=" + plan.ImageRef + " frame=" + plan.FrameRef);
                }

                _cache[uid] = new Cached { Key = key, Plan = plan };
                return plan;
            }
            catch (Exception ex) when (IsInfrastructureFailure(ex))
            {
                _disabled = true;
                _cache.Clear();
                // The legacy route prefers the photo, the game-backed route prefers whatever the
                // player selected. Say so here, or a later parity MISMATCH looks like a wrong rule
                // rather than a route that had to be abandoned.
                MelonLogger.Warning(
                    "[FriendOverlay] portrait resolver disabled, falling back to the photo-first " +
                    "legacy route; portrait parity mismatches after this line are expected: " + ex.Message);
                return Legacy(info, blocked);
            }
            catch (Exception ex)
            {
                if (!_loggedError)
                {
                    _loggedError = true;
                    MelonLogger.Warning("[FriendOverlay] portrait resolve failed for one row: " + ex.Message);
                }

                return Legacy(info, blocked);
            }
        }

        /// <summary>
        /// Pre-0.3.4 behaviour: custom photo URL, else the FaceId prop icon, no frame. Blocked rows
        /// stay letters because without the game's substitution we would be showing the real photo.
        /// </summary>
        private static PortraitPlan Legacy(FriendBaseInfo info, bool blocked)
        {
            if (blocked)
                return Empty;

            var url = string.Empty;
            try { url = info.RiskInfo?.FaceUrl ?? string.Empty; } catch { url = string.Empty; }
            if (url.Length > 0)
                return new PortraitPlan(PortraitKind.Photo, url, string.Empty);

            var icon = LegacyIcon(info);
            return icon.Length > 0
                ? new PortraitPlan(PortraitKind.Official, icon, string.Empty)
                : Empty;
        }

        private static string LegacyIcon(FriendBaseInfo info)
        {
            try
            {
                var faceId = info.FaceId;
                if (faceId == 0)
                    return string.Empty;

                var config = UnitUtility.config;
                var prop = config?.getPropConfig(faceId);
                if (prop == null)
                    return string.Empty;

                var icon = prop.GetIcon();
                return string.IsNullOrEmpty(icon) ? (prop.icon ?? string.Empty) : icon;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool IsInfrastructureFailure(Exception ex) =>
            ex is MissingMethodException or MissingFieldException or TypeLoadException
                or EntryPointNotFoundException or Il2CppInterop.Runtime.Il2CppException ||
            ex.Message.IndexOf("Method not found", StringComparison.OrdinalIgnoreCase) >= 0;

        public static void Reset()
        {
            _cache.Clear();
            _disabled = false;
            _samplesLogged = 0;
            _loggedError = false;
        }
    }
}
