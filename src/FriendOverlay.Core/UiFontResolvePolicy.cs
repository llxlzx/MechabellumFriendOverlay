namespace FriendOverlay.Core;

public enum UiFontFaceKind
{
    Skin = 0,
    Game = 1,
    YaHei = 2,
    Noto = 3,
}

/// <summary>
/// Pure ordering for which UI font face to prefer. Actual load happens in Melon UI.
/// </summary>
public static class UiFontResolvePolicy
{
    public static UiFontFaceKind Choose(bool useGameFont, bool notoOk, bool yaheiOk, bool gameOk)
    {
        if (useGameFont)
        {
            if (gameOk) return UiFontFaceKind.Game;
            if (notoOk) return UiFontFaceKind.Noto;
            if (yaheiOk) return UiFontFaceKind.YaHei;
            return UiFontFaceKind.Skin;
        }

        if (notoOk) return UiFontFaceKind.Noto;
        if (yaheiOk) return UiFontFaceKind.YaHei;
        if (gameOk) return UiFontFaceKind.Game;
        return UiFontFaceKind.Skin;
    }
}
