using System.Collections.Generic;
using System.Linq;
using FriendOverlay.Core;
using Xunit;

namespace FriendOverlay.Tests;

/// <summary>
/// Regression cover for the hang traced on 2026-09-10: asking the server about all 211 followed ids
/// every 2.5s produced one `tcp session write buffer too long[1914]` per request and eventually wedged
/// the game. The payload has to stay small and the total rate bounded, while still covering everyone.
/// </summary>
public class OnlinePollTests
{
    private static List<ulong> Ids(int count)
    {
        var ids = new List<ulong>(count);
        for (var i = 0; i < count; i++)
            ids.Add((ulong)(i + 1));
        return ids;
    }

    [Fact]
    public void Ctor_ClampsChunkSizeToAtLeastOne()
    {
        Assert.Equal(1, new OnlinePoll(0).ChunkSize);
        Assert.Equal(1, new OnlinePoll(-5).ChunkSize);
    }

    /// <summary>A list that fits in one request keeps the old 2.5s freshness; only big lists rotate.</summary>
    [Fact]
    public void Next_ListThatFitsIsAskedInFull_EveryTime()
    {
        var poll = new OnlinePoll(32);
        var ids = Ids(20);

        Assert.Equal(ids, poll.Next(ids));
        Assert.Equal(ids, poll.Next(ids));
    }

    [Fact]
    public void Next_ChunkNeverExceedsChunkSize()
    {
        var poll = new OnlinePoll(32);
        var ids = Ids(211);

        for (var i = 0; i < 20; i++)
            Assert.True(poll.Next(ids).Count <= 32);
    }

    [Fact]
    public void Next_FullCycleCoversEveryIdExactlyOnce()
    {
        var poll = new OnlinePoll(32);
        var ids = Ids(211);
        var seen = new List<ulong>();

        // 211 / 32 rounds up to 7 requests.
        for (var i = 0; i < 7; i++)
            seen.AddRange(poll.Next(ids));

        Assert.Equal(211, seen.Count);
        Assert.Equal(ids, seen);
    }

    [Fact]
    public void Next_WrapsBackToTheStart()
    {
        var poll = new OnlinePoll(2);
        var ids = Ids(4);

        Assert.Equal(new ulong[] { 1, 2 }, poll.Next(ids));
        Assert.Equal(new ulong[] { 3, 4 }, poll.Next(ids));
        Assert.Equal(new ulong[] { 1, 2 }, poll.Next(ids));
    }

    /// <summary>
    /// The cycle is latched at its start, so paging in more followers mid-cycle cannot make one cycle
    /// grow without bound; the newcomers ride the next one.
    /// </summary>
    [Fact]
    public void Next_IdAddedMidCycleIsAskedAfterTheWrap()
    {
        var poll = new OnlinePoll(2);
        var ids = Ids(4);

        Assert.Equal(new ulong[] { 1, 2 }, poll.Next(ids));

        ids.Add(5);
        Assert.Equal(new ulong[] { 3, 4 }, poll.Next(ids));
        Assert.Equal(new ulong[] { 1, 2 }, poll.Next(ids));
        Assert.Equal(new ulong[] { 3, 4 }, poll.Next(ids));
        Assert.Equal(new ulong[] { 5 }, poll.Next(ids));
    }

    [Fact]
    public void Next_IdRemovedMidCycleIsSkipped()
    {
        var poll = new OnlinePoll(2);
        var ids = Ids(4);

        Assert.Equal(new ulong[] { 1, 2 }, poll.Next(ids));

        ids.Remove(3);
        Assert.Equal(new ulong[] { 4 }, poll.Next(ids));
    }

    [Fact]
    public void Next_EmptyListReturnsEmptyChunk()
    {
        Assert.Empty(new OnlinePoll(32).Next(new List<ulong>()));
    }

    /// <summary>
    /// Without an immediate re-latch the tick would burn its whole interval on an empty request every
    /// time the tail of a cycle went away.
    /// </summary>
    [Fact]
    public void Next_WhenEveryLatchedIdVanishedRelatchesImmediately()
    {
        var poll = new OnlinePoll(2);
        var ids = Ids(4);

        poll.Next(ids);

        ids.RemoveAll(id => id == 3 || id == 4);
        Assert.Equal(new ulong[] { 1, 2 }, poll.Next(ids));
    }

    [Fact]
    public void Reset_StartsANewCycle()
    {
        var poll = new OnlinePoll(2);
        var ids = Ids(4);

        poll.Next(ids);
        poll.Reset();

        Assert.Equal(new ulong[] { 1, 2 }, poll.Next(ids));
    }

    /// <summary>
    /// The number that mattered: 211 ids in one request is what the server complained about, and the
    /// rotation has to cut both the per-request size and the ids-per-second rate.
    /// </summary>
    [Fact]
    public void Next_LargeListIsSplitIntoBoundedRequests()
    {
        var poll = new OnlinePoll(OnlinePoll.DefaultChunkSize);
        var ids = Ids(211);
        var requests = 0;
        var total = 0;

        while (total < 211)
        {
            var chunk = poll.Next(ids);
            Assert.True(chunk.Count <= OnlinePoll.DefaultChunkSize);
            total += chunk.Count;
            requests++;
            Assert.True(requests <= 211, "rotation failed to make progress");
        }

        Assert.True(requests > 1, "a 211 id list must not go out in a single request");
    }
}
