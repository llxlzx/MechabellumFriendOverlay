namespace FriendOverlay.Core
{
    /// <summary>Thin accessors over <see cref="UiCatalog"/> using <see cref="LanguageResolver.Current"/>.</summary>
    public static class L
    {
        public static string T(string key) => UiCatalog.T(key);

        public static string Tf(string key, params object[] args) => UiCatalog.Tf(key, args);
    }
}
