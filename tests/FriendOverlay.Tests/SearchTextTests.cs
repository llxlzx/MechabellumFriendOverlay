using FriendOverlay.Core;
using Xunit;

namespace FriendOverlay.Tests;

public class SearchTextTests
{
    [Fact]
    public void Apply_appends_typed_characters()
    {
        Assert.Equal("ab中", SearchText.Apply("a", "b中"));
    }

    [Fact]
    public void Apply_backspace_deletes_one_character()
    {
        Assert.Equal("中", SearchText.Apply("中文", "\b"));
    }

    [Fact]
    public void Apply_paste_keeps_chinese_and_drops_newlines()
    {
        Assert.Equal("ab你好", SearchText.Apply("ab", pasted: "你\r\n好"));
    }

    [Fact]
    public void Apply_ignores_control_characters()
    {
        Assert.Equal("a", SearchText.Apply("a", "\u0003\u0016"));
    }

    [Fact]
    public void Apply_stops_at_max_length()
    {
        var full = new string('x', SearchText.MaxLength);
        Assert.Equal(full, SearchText.Apply(full, "y", "中"));
        Assert.Equal(SearchText.MaxLength, SearchText.Apply("a", pasted: new string('中', 80)).Length);
    }

    [Fact]
    public void Advance_ignores_input_while_composition_is_open()
    {
        var frame = SearchText.Advance("ab", previousComposition: "", compositionNow: "你", inputString: "n");
        Assert.Equal("ab", frame.Text);
        Assert.Equal("你", frame.Composition);
    }

    [Fact]
    public void Advance_commits_previous_composition_when_it_ends()
    {
        var frame = SearchText.Advance("ab", previousComposition: "你好", compositionNow: "", inputString: "");
        Assert.Equal("ab你好", frame.Text);
        Assert.Equal("", frame.Composition);
    }

    [Fact]
    public void Advance_does_not_append_composition_twice_when_input_already_has_it()
    {
        var frame = SearchText.Advance("ab", previousComposition: "你好", compositionNow: "", inputString: "你好");
        Assert.Equal("ab你好", frame.Text);
        Assert.Equal("", frame.Composition);
    }

    [Fact]
    public void Advance_without_composition_still_applies_keys()
    {
        Assert.Equal("ac", SearchText.Advance("ab", "", "", "\bc").Text);
        Assert.Equal("", SearchText.Advance("ab", "", "", "\bc").Composition);
    }
}
