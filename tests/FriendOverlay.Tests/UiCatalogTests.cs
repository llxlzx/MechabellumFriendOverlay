using System.Linq;
using FriendOverlay.Core;

namespace FriendOverlay.Tests;

public class UiCatalogTests
{
    private static readonly string[] Locales = { "zh-CN", "en", "ru", "ja", "de" };

    [Fact]
    public void AllKeys_have_five_locales()
    {
        var keys = UiCatalog.AllKeys.ToArray();
        Assert.NotEmpty(keys);

        foreach (var key in keys)
        {
            foreach (var locale in Locales)
            {
                var text = UiCatalog.Lookup(locale, key);
                Assert.False(string.IsNullOrEmpty(text), $"missing {locale}/{key}");
                Assert.NotEqual(key, text);
            }
        }
    }

    [Fact]
    public void Missing_falls_back_to_en()
    {
        var previous = LanguageResolver.Current;
        try
        {
            LanguageResolver.Current = "ja";
            // Force a key only present in en by using Lookup path: current miss → en
            // Simulate via internal: if ja table lacked a key, T would return en.
            // We verify the fallback contract with a deliberately absent key:
            Assert.Equal("Ready", UiCatalog.Lookup("xx-missing-locale", "status.ready"));
        }
        finally
        {
            LanguageResolver.Current = previous;
        }
    }

    [Fact]
    public void Missing_en_returns_key()
    {
        Assert.Equal("no.such.key.ever", UiCatalog.Lookup("en", "no.such.key.ever"));
        Assert.Equal("no.such.key.ever", UiCatalog.Lookup("zh-CN", "no.such.key.ever"));
    }

    [Fact]
    public void L_T_uses_Current_language()
    {
        var previous = LanguageResolver.Current;
        try
        {
            LanguageResolver.Current = "zh-CN";
            Assert.Equal("关闭", L.T("btn.close"));
            LanguageResolver.Current = "en";
            Assert.Equal("Close", L.T("btn.close"));
        }
        finally
        {
            LanguageResolver.Current = previous;
        }
    }

    [Fact]
    public void L_Tf_formats_args()
    {
        var previous = LanguageResolver.Current;
        try
        {
            LanguageResolver.Current = "en";
            Assert.Equal("Loaded 12", L.Tf("count.loaded", 12));
        }
        finally
        {
            LanguageResolver.Current = previous;
        }
    }

    [Fact]
    public void Zh_tab_labels_match_legacy()
    {
        Assert.Equal("关注", UiCatalog.Lookup("zh-CN", "tab.following"));
        Assert.Equal("关注我的人", UiCatalog.Lookup("zh-CN", "tab.followers"));
    }
}
