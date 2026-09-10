using FriendOverlay.Core;
using Xunit;

namespace FriendOverlay.Tests;

public class FriendQueryPipelineTests
{
    private static FriendRowVm Row(
        ulong id,
        string name,
        bool online = false,
        bool mutual = false,
        bool busy = false,
        int rank = 0,
        int forecast = 0) =>
        new FriendRowVm
        {
            UserId = id,
            Name = name,
            IsOnline = online,
            IsMutual = mutual,
            IsBusy = busy,
            RankPoint = rank,
            ForecastPoint = forecast,
            State = busy ? 1 : 0,
            FaceUrl = string.Empty,
        };

    [Fact]
    public void Apply_NameQuery_KeepsCaseInsensitiveSubstringMatches()
    {
        var src = new[]
        {
            Row(1, "Alpha"),
            Row(2, "betaWarrior"),
            Row(3, "Gamma"),
        };

        var result = FriendQueryPipeline.Apply(src, "BETA", FriendFilter.All, FriendSortKey.Default);

        Assert.Equal(new ulong[] { 2 }, result.Select(r => r.UserId).ToArray());
    }

    [Fact]
    public void Apply_UserIdQuery_MatchesSubstring()
    {
        var src = new[]
        {
            Row(10042, "A"),
            Row(99, "B"),
            Row(4200, "C"),
        };

        var result = FriendQueryPipeline.Apply(src, "42", FriendFilter.All, FriendSortKey.Default);

        Assert.Equal(new ulong[] { 10042, 4200 }, result.Select(r => r.UserId).ToArray());
    }

    [Fact]
    public void Apply_BlankQuery_ReturnsAll()
    {
        var src = new[] { Row(1, "A"), Row(2, "B") };

        var empty = FriendQueryPipeline.Apply(src, "", FriendFilter.All, FriendSortKey.Default);
        var spaces = FriendQueryPipeline.Apply(src, "   ", FriendFilter.All, FriendSortKey.Default);

        Assert.Equal(2, empty.Count);
        Assert.Equal(2, spaces.Count);
    }

    [Fact]
    public void Apply_FilterOnline_KeepsOnlineOnly()
    {
        var src = new[] { Row(1, "A", online: true), Row(2, "B", online: false) };
        var result = FriendQueryPipeline.Apply(src, "", FriendFilter.Online, FriendSortKey.Default);
        Assert.Equal(new ulong[] { 1 }, result.Select(r => r.UserId).ToArray());
    }

    [Fact]
    public void Apply_FilterOffline_KeepsOfflineOnly()
    {
        var src = new[] { Row(1, "A", online: true), Row(2, "B", online: false) };
        var result = FriendQueryPipeline.Apply(src, "", FriendFilter.Offline, FriendSortKey.Default);
        Assert.Equal(new ulong[] { 2 }, result.Select(r => r.UserId).ToArray());
    }

    [Fact]
    public void Apply_FilterMutual_KeepsMutualOnly()
    {
        var src = new[] { Row(1, "A", mutual: true), Row(2, "B", mutual: false) };
        var result = FriendQueryPipeline.Apply(src, "", FriendFilter.Mutual, FriendSortKey.Default);
        Assert.Equal(new ulong[] { 1 }, result.Select(r => r.UserId).ToArray());
    }

    [Fact]
    public void Apply_FilterBusy_KeepsBusyOnly()
    {
        var src = new[] { Row(1, "A", busy: true, online: true), Row(2, "B", busy: false, online: true) };
        var result = FriendQueryPipeline.Apply(src, "", FriendFilter.Busy, FriendSortKey.Default);
        Assert.Equal(new ulong[] { 1 }, result.Select(r => r.UserId).ToArray());
    }

    [Fact]
    public void Apply_DefaultSort_OnlineThenMutualThenRankThenName()
    {
        var src = new[]
        {
            Row(1, "zeta", online: false, mutual: true, rank: 999),
            Row(2, "beta", online: true, mutual: false, rank: 50),
            Row(3, "alpha", online: true, mutual: true, rank: 10),
            Row(4, "gamma", online: true, mutual: false, rank: 100),
            Row(5, "delta", online: true, mutual: false, rank: 100, forecast: 5),
        };

        var result = FriendQueryPipeline.Apply(src, "", FriendFilter.All, FriendSortKey.Default);

        Assert.Equal(new ulong[] { 3, 5, 4, 2, 1 }, result.Select(r => r.UserId).ToArray());
    }

    [Fact]
    public void Apply_SearchThenFilter_Composes()
    {
        var src = new[]
        {
            Row(1, "SamOnline", online: true),
            Row(2, "SamOffline", online: false),
            Row(3, "Other", online: true),
        };

        var result = FriendQueryPipeline.Apply(src, "sam", FriendFilter.Online, FriendSortKey.Default);

        Assert.Equal(new ulong[] { 1 }, result.Select(r => r.UserId).ToArray());
    }

    [Fact]
    public void Apply_DoesNotMutateSource()
    {
        var src = new List<FriendRowVm>
        {
            Row(2, "B", online: false),
            Row(1, "A", online: true),
        };
        var snapshot = src.Select(r => r.UserId).ToArray();

        _ = FriendQueryPipeline.Apply(src, "", FriendFilter.All, FriendSortKey.Default);

        Assert.Equal(snapshot, src.Select(r => r.UserId).ToArray());
    }
}
