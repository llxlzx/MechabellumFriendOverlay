using System;

namespace FriendOverlay.Core;

/// <summary>
/// Pure helpers for default overlay geometry and one-shot safe-area migration.
/// Screen coords: origin top-left (IMGUI).
/// </summary>
public static class OverlayGeometry
{
    public const float DefaultX = 60f;
    public const float DefaultY = 48f;
    public const float DefaultW = 900f;
    public const float DefaultH = 540f;
    public const float BottomSafeBand = 280f;

    /// <summary>
    /// If the window bottom enters the bottom <see cref="BottomSafeBand"/> of the screen,
    /// shrink height (and nudge Y up if needed) so the bottom stays clear. Keeps X.
    /// </summary>
    public static bool TryClampToBottomSafeArea(
        float x,
        float y,
        float w,
        float h,
        float screenW,
        float screenH,
        out float outX,
        out float outY,
        out float outW,
        out float outH)
    {
        outX = x;
        outY = y;
        outW = w;
        outH = h;

        if (screenH <= 1f || w <= 1f || h <= 1f)
            return false;

        var maxBottom = screenH - BottomSafeBand;
        if (maxBottom < 80f)
            maxBottom = screenH * 0.7f;

        var bottom = y + h;
        if (bottom <= maxBottom)
            return false;

        var maxH = maxBottom - y;
        if (maxH < 420f)
        {
            outY = Math.Max(0f, maxBottom - 420f);
            outH = Math.Min(h, maxBottom - outY);
        }
        else
        {
            outH = maxH;
        }

        if (outW > screenW - 20f)
            outW = Math.Max(320f, screenW - 20f);

        return !NearlyEqual(outY, y) || !NearlyEqual(outH, h) || !NearlyEqual(outW, w);
    }

    static bool NearlyEqual(float a, float b) => Math.Abs(a - b) < 0.5f;
}
