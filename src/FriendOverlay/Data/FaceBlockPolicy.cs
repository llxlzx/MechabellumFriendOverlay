using System;
using System.Collections.Generic;
using Il2CppGameRiver;
using Il2CppProtos.Friend;
using MelonLoader;
using GameRiskInfo = Il2CppGameRiver.PlayerRiskInfo;

namespace FriendOverlay.Data
{
    /// <summary>
    /// Decides whether the game itself hides a friend's portrait, so the overlay can hide it too.
    ///
    /// The verdict is the game's own: we rebuild the object it builds from the same proto fields and
    /// ask whether it resolves to the dark placeholder. The decoded form of that rule is computed
    /// alongside for diagnostics only, because the key domain of BlockFace cannot be proven from the
    /// interop stubs and a wrong guess must never decide anything.
    ///
    /// Failure direction is tiered on purpose. A row we cannot evaluate falls back to the letter,
    /// which is the safe answer for that one friend. A binding-level failure disables the whole
    /// policy and shows avatars again, because "letter" applied session-wide would be a total
    /// regression of the feature this exists to support.
    /// </summary>
    public static class FaceBlockPolicy
    {
        private const int CeilingMinSamples = 10;
        private const float CeilingRatio = 0.5f;

        /// <summary>
        /// Everything the game's own verdict is derived from. Keying the cache on the face URL alone
        /// would keep serving an "allowed" verdict after the server changed the moderation map for a
        /// friend whose portrait URL never changed. Reading the map here is invalidation only — the
        /// verdict still comes from the game.
        /// </summary>
        private readonly struct Fingerprint
        {
            private readonly string _faceUrl;
            private readonly string _name;
            private readonly int _faceId;
            private readonly int _faceBorder;
            private readonly int _blockCount;
            private readonly int _blockAtLevel;

            public Fingerprint(string faceUrl, string name, int faceId, int faceBorder, int blockCount, int blockAtLevel)
            {
                _faceUrl = faceUrl;
                _name = name;
                _faceId = faceId;
                _faceBorder = faceBorder;
                _blockCount = blockCount;
                _blockAtLevel = blockAtLevel;
            }

            public bool Matches(in Fingerprint other) =>
                _faceId == other._faceId &&
                _faceBorder == other._faceBorder &&
                _blockCount == other._blockCount &&
                _blockAtLevel == other._blockAtLevel &&
                string.Equals(_faceUrl, other._faceUrl, StringComparison.Ordinal) &&
                string.Equals(_name, other._name, StringComparison.Ordinal);
        }

        private sealed class Verdict
        {
            public bool Blocked;
            public Fingerprint Source;

            /// <summary>Evaluation threw for this friend; do not retry it every snapshot.</summary>
            public bool Failed;
        }

        private static readonly Dictionary<ulong, Verdict> _verdicts = new Dictionary<ulong, Verdict>();
        private static readonly HashSet<ulong> _counted = new HashSet<ulong>();
        private static readonly HashSet<ulong> _loggedDisagreements = new HashSet<ulong>();

        private static bool _disabled;
        private static bool _loggedSession;
        private static bool _loggedS2Error;
        private static bool _loggedEvaluationError;
        private static int _evaluated;
        private static int _blockedCount;

        private static bool _settingsKnown;
        private static int _level;
        private static bool _displayBanAvatar;

        /// <summary>
        /// Re-reads the game's moderation settings once per snapshot and drops every cached verdict
        /// when they changed, so toggling the in-game switch takes effect without reopening.
        /// </summary>
        public static void BeginSnapshot()
        {
            if (_disabled)
                return;

            // Read the two independently. A null filter only makes the level unknown; folding both
            // into one early return would leave _settingsKnown false forever and silently stop the
            // cache from ever being invalidated when the user flips the in-game switch.
            var level = _level;
            try
            {
                var filter = Utility.dirtyWordsFilter;
                if (filter != null)
                    level = filter.GetPortraitRiskLevel();
            }
            catch
            {
                // Unknown, not changed: comparing against a guess would flush every snapshot.
            }

            var displayBan = _displayBanAvatar;
            try
            {
                displayBan = Utility.displayBanAvatar;
            }
            catch
            {
                // Same reasoning; the verdict itself does not depend on this.
            }

            if (_settingsKnown && level == _level && displayBan == _displayBanAvatar)
                return;

            _settingsKnown = true;
            _level = level;
            _displayBanAvatar = displayBan;
            _verdicts.Clear();
            _counted.Clear();
            _loggedDisagreements.Clear();
            _evaluated = 0;
            _blockedCount = 0;

            if (!_loggedSession)
            {
                _loggedSession = true;
                MelonLogger.Msg("[FriendOverlay] blockface: level=" + level + " displayBan=" + displayBan);
            }
        }

        public static bool IsBlocked(FriendBaseInfo info)
        {
            if (_disabled || info == null)
                return false;

            ulong uid;
            try { uid = info.Userid; }
            catch { return false; }

            if (uid == 0)
                return false;

            bool blocked;
            var source = default(Fingerprint);
            var failed = false;

            try
            {
                var risk = info.RiskInfo;
                source = Describe(risk, info);

                if (_verdicts.TryGetValue(uid, out var cached) &&
                    (cached.Failed || cached.Source.Matches(source)))
                    return cached.Blocked;

                blocked = Evaluate(uid, risk, info);

                // Evaluate can trip the session kill; do not cache a verdict from a dead policy.
                if (_disabled)
                    return false;
            }
            catch (Exception ex) when (IsInfrastructureFailure(ex))
            {
                Disable("blockface disabled: " + ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                // One friend we cannot evaluate is a letter for that row. It still has to be cached
                // and counted: a game-side throw that hits every row would otherwise letter the
                // whole list with nothing recorded and the ceiling never reached.
                if (!_loggedEvaluationError)
                {
                    _loggedEvaluationError = true;
                    MelonLogger.Warning("[FriendOverlay] blockface evaluation failed: " + ex.Message);
                }

                blocked = true;
                failed = true;
            }

            Record(uid, source, blocked, failed);
            return !_disabled && blocked;
        }

        private static Fingerprint Describe(Il2CppProtos.Common.PlayerRiskInfo? risk, FriendBaseInfo info)
        {
            if (risk == null)
                return new Fingerprint(string.Empty, string.Empty, info.FaceId, info.FaceBorder, -1, -1);

            var blockFace = risk.BlockFace;
            var count = -1;
            var atLevel = -1;
            if (blockFace != null)
            {
                count = blockFace.Count;
                if (_settingsKnown && _level > 0 && blockFace.ContainsKey(_level))
                    atLevel = blockFace[_level] ? 1 : 0;
            }

            return new Fingerprint(
                risk.FaceUrl ?? string.Empty,
                risk.Name ?? string.Empty,
                info.FaceId,
                info.FaceBorder,
                count,
                atLevel);
        }

        private static void Record(ulong uid, in Fingerprint source, bool blocked, bool failed)
        {
            _verdicts[uid] = new Verdict { Blocked = blocked, Source = source, Failed = failed };

            // Count each friend once, so re-evaluating one row after its URL changed cannot walk
            // the ceiling up on its own.
            if (!_counted.Add(uid))
                return;

            _evaluated++;
            if (blocked)
                _blockedCount++;

            // Wrong semantics, or a throw on every row, shows up as an implausible share of the
            // list being hidden. Fall back to showing avatars rather than lettering everyone.
            if (_evaluated >= CeilingMinSamples && _blockedCount > _evaluated * CeilingRatio)
                Disable("blockface disabled: " + _blockedCount + "/" + _evaluated + " rows blocked, semantics suspect");
        }

        private static bool Evaluate(ulong uid, Il2CppProtos.Common.PlayerRiskInfo? risk, FriendBaseInfo info)
        {
            if (risk == null)
                return true;

            var blockFace = risk.BlockFace;
            if (blockFace == null)
                return true;

            var dark = PlayerPortraitInfo.DefaultDarkAvatar;
            if (string.IsNullOrEmpty(dark))
            {
                // Session-wide, not per row: lettering the whole list over this would be worse than
                // not applying the policy at all.
                Disable("blockface disabled: PlayerPortraitInfo.DefaultDarkAvatar is empty");
                return false;
            }

            var portrait = new GameRiskInfo(
                risk.Name ?? string.Empty,
                risk.FaceUrl ?? string.Empty,
                risk.BlockName,
                blockFace,
                info.FaceId,
                info.FaceBorder).GetPortrait();

            if (string.IsNullOrEmpty(portrait))
                return true;

            var blocked = string.Equals(portrait, dark, StringComparison.Ordinal);
            LogDisagreement(uid, blocked, blockFace);
            return blocked;
        }

        /// <summary>
        /// The decoded reading of the same data. Diagnostics only: it can never change a verdict,
        /// and its failures are swallowed.
        /// </summary>
        private static void LogDisagreement(ulong uid, bool s1, Il2CppGoogle.Protobuf.Collections.MapField<int, bool> blockFace)
        {
            if (!_settingsKnown)
                return;

            try
            {
                var s2 = !_displayBanAvatar && _level > 0 && blockFace.ContainsKey(_level) && blockFace[_level];
                if (s2 == s1 || !_loggedDisagreements.Add(uid))
                    return;

                MelonLogger.Msg(
                    "[FriendOverlay] blockface disagreement uid=" + uid +
                    " s1=" + s1 + " s2=" + s2 + " keys=" + blockFace.Count);
            }
            catch (Exception ex)
            {
                if (_loggedS2Error)
                    return;

                _loggedS2Error = true;
                MelonLogger.Msg("[FriendOverlay] blockface diagnostic unavailable: " + ex.Message);
            }
        }

        /// <summary>
        /// Binding-level failure, as opposed to bad data for one friend. Il2CppException is what a
        /// game-side throw actually surfaces as, so leaving it out would send every such failure to
        /// the per-row path and letter the entire list.
        /// </summary>
        private static bool IsInfrastructureFailure(Exception ex) =>
            ex is MissingMethodException or MissingFieldException or TypeLoadException
                or EntryPointNotFoundException or Il2CppInterop.Runtime.Il2CppException ||
            ex.Message.IndexOf("Method not found", StringComparison.OrdinalIgnoreCase) >= 0;

        private static void Disable(string message)
        {
            if (_disabled)
                return;

            _disabled = true;
            _verdicts.Clear();
            _counted.Clear();
            _loggedDisagreements.Clear();
            MelonLogger.Warning("[FriendOverlay] " + message);
        }

        public static void Reset()
        {
            _verdicts.Clear();
            _counted.Clear();
            _loggedDisagreements.Clear();
            _disabled = false;
            _loggedSession = false;
            _loggedS2Error = false;
            _loggedEvaluationError = false;
            _evaluated = 0;
            _blockedCount = 0;
            _settingsKnown = false;
            _level = 0;
            _displayBanAvatar = false;
        }
    }
}
