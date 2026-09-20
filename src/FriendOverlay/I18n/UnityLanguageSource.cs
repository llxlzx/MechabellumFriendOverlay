using UnityEngine;

namespace FriendOverlay.I18n
{
    /// <summary>
    /// Uses <see cref="Application.systemLanguage"/>. No separate game localization API is wired
    /// elsewhere in this mod, so system language is the stable signal.
    /// </summary>
    public sealed class UnityLanguageSource : ILanguageSource
    {
        public string ReadSignal() => Application.systemLanguage.ToString();
    }
}
