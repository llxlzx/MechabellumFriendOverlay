using FriendOverlay.Core;
using FriendOverlay.State;
using FriendOverlay.UI;
using UnityEngine;

namespace FriendOverlay.I18n
{
    /// <summary>
    /// Polls the game language while the overlay is visible and invalidates the UI font when the
    /// resolved code changes.
    /// </summary>
    public static class OverlayLanguage
    {
        private const float PollInterval = 2f;

        private static ILanguageSource _source = new UnityLanguageSource();
        private static float _nextPollAt;
        private static string _appliedCode = string.Empty;
        private static bool _wasVisible;

        public static void SetSource(ILanguageSource source)
        {
            _source = source ?? new UnityLanguageSource();
            _appliedCode = string.Empty;
            _nextPollAt = 0f;
        }

        public static void Tick()
        {
            var visible = OverlaySession.OverlayVisible;
            if (!visible)
            {
                _wasVisible = false;
                return;
            }

            var now = Time.unscaledTime;
            var opened = !_wasVisible;
            _wasVisible = true;

            if (!opened && now < _nextPollAt)
                return;

            _nextPollAt = now + PollInterval;
            ApplyFromSource();
        }

        public static void ApplyFromSource()
        {
            var code = LanguageResolver.ToCode(_source.ReadSignal());
            if (string.Equals(code, _appliedCode, System.StringComparison.Ordinal)
                && string.Equals(code, LanguageResolver.Current, System.StringComparison.Ordinal))
                return;

            LanguageResolver.Current = code;
            _appliedCode = code;
            GameAssets.InvalidateFontForLanguage();
        }
    }
}
