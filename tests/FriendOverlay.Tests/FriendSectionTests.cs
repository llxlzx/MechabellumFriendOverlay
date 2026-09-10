using FriendOverlay.Core;
using Xunit;

namespace FriendOverlay.Tests;

public class FriendSectionTests
{
    private static FriendRowVm Row(ulong id, bool online, bool busy) => new FriendRowVm
    {
        UserId = id,
        Name = "P" + id,
        IsOnline = online,
        IsBusy = busy,
        StatusKind = !online
            ? FriendStatusKind.Offline
            : busy
                ? FriendStatusKind.Battle
                : FriendStatusKind.Idle,
    };

    [Fact]
    public void GroupIntoSections_DuplicateSourceRowsForOnePinnedIdStayInOnePlace()
    {
        // Two sources feed the followers list and one player can arrive from both. Whatever the
        // caller hands us, a pinned uid must never be rendered in the pinned section and an
        // availability section at the same time.
        var pinned = new PinnedIds();
        pinned.Add(7);
        var duplicate = Row(7, online: true, busy: false);
        var rows = new[] { duplicate, Row(8, online: true, busy: false), duplicate };

        var sections = FriendQueryPipeline.GroupIntoSections(rows, pinned);

        var elsewhere = sections
            .Where(s => s.Kind != FriendSectionKind.Pinned)
            .SelectMany(s => s.Rows)
            .Select(r => r.UserId);
        Assert.DoesNotContain(7ul, elsewhere);
        Assert.Equal(FriendSectionKind.Pinned, sections[0].Kind);
    }

    [Fact]
    public void GroupIntoSections_OrdersJoinableThenBusyThenOffline()
    {
        var rows = new[]
        {
            Row(1, online: false, busy: false),
            Row(2, online: true, busy: true),
            Row(3, online: true, busy: false),
        };

        var sections = FriendQueryPipeline.GroupIntoSections(rows);

        Assert.Equal(
            new[] { FriendSectionKind.OnlineJoinable, FriendSectionKind.OnlineBusy, FriendSectionKind.Offline },
            sections.Select(s => s.Kind).ToArray());
        Assert.Equal(new ulong[] { 3 }, sections[0].Rows.Select(r => r.UserId).ToArray());
        Assert.Equal(new ulong[] { 2 }, sections[1].Rows.Select(r => r.UserId).ToArray());
        Assert.Equal(new ulong[] { 1 }, sections[2].Rows.Select(r => r.UserId).ToArray());
    }

    [Fact]
    public void GroupIntoSections_OmitsEmptySections()
    {
        var rows = new[] { Row(1, online: true, busy: false), Row(2, online: true, busy: false) };

        var sections = FriendQueryPipeline.GroupIntoSections(rows);

        Assert.Single(sections);
        Assert.Equal(FriendSectionKind.OnlineJoinable, sections[0].Kind);
        Assert.Equal(2, sections[0].Rows.Count);
    }

    [Fact]
    public void GroupIntoSections_PreservesIncomingOrderInsideSection()
    {
        var rows = new[]
        {
            Row(30, online: true, busy: false),
            Row(10, online: true, busy: false),
            Row(20, online: true, busy: false),
        };

        var sections = FriendQueryPipeline.GroupIntoSections(rows);

        Assert.Equal(new ulong[] { 30, 10, 20 }, sections[0].Rows.Select(r => r.UserId).ToArray());
    }

    [Fact]
    public void GroupIntoSections_OfflineWinsOverBusyFlag()
    {
        // Stale presence data can leave IsBusy set on an offline row.
        var rows = new[] { Row(1, online: false, busy: true) };

        var sections = FriendQueryPipeline.GroupIntoSections(rows);

        Assert.Single(sections);
        Assert.Equal(FriendSectionKind.Offline, sections[0].Kind);
    }

    [Fact]
    public void GroupIntoSections_EmptyInputReturnsNoSections()
    {
        Assert.Empty(FriendQueryPipeline.GroupIntoSections(System.Array.Empty<FriendRowVm>()));
    }

    [Fact]
    public void GroupIntoSections_ComposesWithApply()
    {
        var rows = new[]
        {
            Row(1, online: true, busy: true),
            Row(2, online: false, busy: false),
            Row(3, online: true, busy: false),
        };

        var view = FriendQueryPipeline.Apply(rows, string.Empty, FriendFilter.Online, FriendSortKey.Default);
        var sections = FriendQueryPipeline.GroupIntoSections(view);

        Assert.Equal(
            new[] { FriendSectionKind.OnlineJoinable, FriendSectionKind.OnlineBusy },
            sections.Select(s => s.Kind).ToArray());
    }

    [Fact]
    public void GroupIntoSections_PinnedRowsComeFirstAndLeaveTheirSection()
    {
        var rows = new[]
        {
            Row(1, online: true, busy: false),
            Row(2, online: false, busy: false),
            Row(3, online: true, busy: true),
        };
        var pinned = PinnedIds.Parse("2,3");

        var sections = FriendQueryPipeline.GroupIntoSections(rows, pinned);

        Assert.Equal(
            new[] { FriendSectionKind.Pinned, FriendSectionKind.OnlineJoinable },
            sections.Select(s => s.Kind).ToArray());
        Assert.Equal(new ulong[] { 2, 3 }, sections[0].Rows.Select(r => r.UserId).ToArray());
        Assert.Equal(new ulong[] { 1 }, sections[1].Rows.Select(r => r.UserId).ToArray());
    }

    [Fact]
    public void GroupIntoSections_PinnedKeepsIncomingViewOrder()
    {
        var rows = new[]
        {
            Row(30, online: false, busy: false),
            Row(10, online: true, busy: false),
            Row(20, online: true, busy: true),
        };

        var sections = FriendQueryPipeline.GroupIntoSections(rows, PinnedIds.Parse("10,20,30"));

        Assert.Single(sections);
        Assert.Equal(new ulong[] { 30, 10, 20 }, sections[0].Rows.Select(r => r.UserId).ToArray());
    }

    [Fact]
    public void GroupIntoSections_NullPinnedBehavesLikeBefore()
    {
        var rows = new[] { Row(1, online: true, busy: false), Row(2, online: false, busy: false) };

        var sections = FriendQueryPipeline.GroupIntoSections(rows, null);

        Assert.Equal(
            new[] { FriendSectionKind.OnlineJoinable, FriendSectionKind.Offline },
            sections.Select(s => s.Kind).ToArray());
    }

    [Fact]
    public void GroupIntoSections_PinnedIdMissingFromViewIsIgnored()
    {
        var rows = new[] { Row(1, online: true, busy: false) };

        var sections = FriendQueryPipeline.GroupIntoSections(rows, PinnedIds.Parse("999"));

        Assert.Single(sections);
        Assert.Equal(FriendSectionKind.OnlineJoinable, sections[0].Kind);
    }

    /// <summary>A row must never appear twice; the pinned section is a move, not a copy.</summary>
    [Fact]
    public void GroupIntoSections_PinnedRowIsNotDuplicated()
    {
        var rows = new[] { Row(1, online: true, busy: false), Row(2, online: true, busy: false) };

        var sections = FriendQueryPipeline.GroupIntoSections(rows, PinnedIds.Parse("1"));

        var ids = sections.SelectMany(s => s.Rows).Select(r => r.UserId).ToArray();
        Assert.Equal(ids.Length, ids.Distinct().Count());
        Assert.Equal(2, ids.Length);
    }
}
