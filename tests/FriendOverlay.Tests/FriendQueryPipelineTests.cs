using FriendOverlay.Core;
using Xunit;

namespace FriendOverlay.Tests;

public class FriendQueryPipelineTests
{
    // Raw EPlayerState ids. Only their distinctness and relative order matter to the pipeline.
    private const int IdleState = 0;
    private const int Battle1V1 = 2;
    private const int Battle2V2 = 3;
    private const int Watch = 20;

    private static FriendRowVm Row(
        ulong id,
        string name,
        bool online = false,
        bool mutual = false,
        bool busy = false,
        int rank = 0,
        int forecast = 0,
        FriendStatusKind kind = FriendStatusKind.Offline,
        int state = -1) =>
        new FriendRowVm
        {
            UserId = id,
            Name = name,
            IsOnline = online,
            IsMutual = mutual,
            IsBusy = busy,
            RankPoint = rank,
            ForecastPoint = forecast,
            State = state >= 0 ? state : (busy ? 1 : 0),
            StatusKind = kind,
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

    /// <summary>Rows sharing a status still fall through to the social and score tiebreaks.</summary>
    [Fact]
    public void Apply_DefaultSort_OnlineThenMutualThenRankThenName()
    {
        var src = new[]
        {
            Row(1, "zeta", online: false, mutual: true, rank: 999),
            Row(2, "beta", online: true, mutual: false, rank: 50, kind: FriendStatusKind.Idle, state: IdleState),
            Row(3, "alpha", online: true, mutual: true, rank: 10, kind: FriendStatusKind.Idle, state: IdleState),
            Row(4, "gamma", online: true, mutual: false, rank: 100, kind: FriendStatusKind.Idle, state: IdleState),
            Row(5, "delta", online: true, mutual: false, rank: 100, forecast: 5, kind: FriendStatusKind.Idle, state: IdleState),
        };

        var result = FriendQueryPipeline.Apply(src, "", FriendFilter.All, FriendSortKey.Default);

        Assert.Equal(new ulong[] { 3, 5, 4, 2, 1 }, result.Select(r => r.UserId).ToArray());
    }

    /// <summary>
    /// The 2026-09-10 report: under 「排序 · 状态」 the 在线 · 对战中 section showed 观战中 and 1V1对战
    /// interleaved, because Default ranked by score and never looked at the status at all.
    /// </summary>
    [Fact]
    public void Apply_DefaultSort_GroupsBusyRowsByStatusBeforeScore()
    {
        var src = new[]
        {
            Row(1, "watchHigh", online: true, busy: true, rank: 269092, kind: FriendStatusKind.Pve, state: Watch),
            Row(2, "battleLow", online: true, busy: true, rank: 216267, kind: FriendStatusKind.Battle, state: Battle1V1),
            Row(3, "watchLow", online: true, busy: true, rank: 220707, kind: FriendStatusKind.Pve, state: Watch),
            Row(4, "battleHigh", online: true, busy: true, rank: 258486, kind: FriendStatusKind.Battle, state: Battle1V1),
        };

        var result = FriendQueryPipeline.Apply(src, "", FriendFilter.All, FriendSortKey.Default);

        Assert.Equal(new ulong[] { 1, 3, 4, 2 }, result.Select(r => r.UserId).ToArray());
    }

    /// <summary>Status outranks score, so a top-ranked player in a match cannot head the list.</summary>
    [Fact]
    public void Apply_DefaultSort_IdleComesBeforeBusyDespiteLowerScore()
    {
        var src = new[]
        {
            Row(1, "busyStar", online: true, busy: true, rank: 900, kind: FriendStatusKind.Battle, state: Battle1V1),
            Row(2, "idleRookie", online: true, rank: 1, kind: FriendStatusKind.Idle, state: IdleState),
        };

        var result = FriendQueryPipeline.Apply(src, "", FriendFilter.All, FriendSortKey.Default);

        Assert.Equal(new ulong[] { 2, 1 }, result.Select(r => r.UserId).ToArray());
    }

    /// <summary>
    /// Two battle modes share one colour bucket, so the raw state has to break the tie or 1V1对战 and
    /// 2V2对战 would still mix inside the same section.
    /// </summary>
    [Fact]
    public void Apply_DefaultSort_SplitsOneKindByRawState()
    {
        var src = new[]
        {
            Row(1, "twos", online: true, busy: true, rank: 10, kind: FriendStatusKind.Battle, state: Battle2V2),
            Row(2, "onesHigh", online: true, busy: true, rank: 99, kind: FriendStatusKind.Battle, state: Battle1V1),
            Row(3, "onesLow", online: true, busy: true, rank: 5, kind: FriendStatusKind.Battle, state: Battle1V1),
        };

        var result = FriendQueryPipeline.Apply(src, "", FriendFilter.All, FriendSortKey.Default);

        Assert.Equal(new ulong[] { 2, 3, 1 }, result.Select(r => r.UserId).ToArray());
    }

    /// <summary>Offline rows keep their own block regardless of the status they last reported.</summary>
    [Fact]
    public void Apply_DefaultSort_OfflineStaysLastWhateverItsStatus()
    {
        var src = new[]
        {
            Row(1, "offlineIdleLike", online: false, rank: 999, kind: FriendStatusKind.Idle, state: IdleState),
            Row(2, "onlineBusy", online: true, busy: true, rank: 1, kind: FriendStatusKind.Battle, state: Battle1V1),
        };

        var result = FriendQueryPipeline.Apply(src, "", FriendFilter.All, FriendSortKey.Default);

        Assert.Equal(new ulong[] { 2, 1 }, result.Select(r => r.UserId).ToArray());
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
