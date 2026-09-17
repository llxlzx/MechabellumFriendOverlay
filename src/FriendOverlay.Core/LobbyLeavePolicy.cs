using System;

namespace FriendOverlay.Core;

/// <summary>Pure leave-lobby policy for closing the friend overlay.</summary>
public static class LobbyLeavePolicy
{
    public const int PresenceFailFramesToClose = 3;

    /// <summary>
    /// True when we had a lobby scene at Begin and the active scene name changed to something else.
    /// Empty/null new scene is ignored (transient).
    /// </summary>
    public static bool ShouldCloseOnSceneChange(string? lobbySceneName, string? activeSceneName)
    {
        if (string.IsNullOrWhiteSpace(lobbySceneName))
            return false;
        if (string.IsNullOrWhiteSpace(activeSceneName))
            return false;
        var lobby = lobbySceneName!.Trim();
        var active = activeSceneName!.Trim();
        return !string.Equals(lobby, active, StringComparison.Ordinal);
    }

    public static bool ShouldCloseOnPresenceLost(int consecutiveFailFrames) =>
        consecutiveFailFrames >= PresenceFailFramesToClose;

    /// <summary>
    /// Match loading UI (e.g. MainSceneLoadingWindow) can appear while the active scene is still
    /// the lobby MainMenu — close as soon as that loading surface is visible.
    /// </summary>
    public static bool ShouldCloseOnMatchLoadingVisible(bool matchLoadingVisible) =>
        matchLoadingVisible;
}
