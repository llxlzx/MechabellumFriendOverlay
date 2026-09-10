using System.Linq;
using FriendOverlay.Core;
using Xunit;

namespace FriendOverlay.Tests;

/// <summary>
/// Every assertion here is about shape, not about which enum value a button carries. The concrete
/// (GameMode, MatchMode) cells are observations of the native window captured in
/// docs/superpowers/plans/2026-09-10-invite-trace-table.md, so they live in exactly one place:
/// Catalog_ModeMappingMatchesTheCapturedTrace. A trace result edits that theory and nothing else.
/// </summary>
public class BattleTypeCatalogTests
{
    private static readonly BattleTypeKind[] WindowOrder =
    {
        BattleTypeKind.Vs1v1,
        BattleTypeKind.Vs2v2,
        BattleTypeKind.Survive,
        BattleTypeKind.TeamMatch,
        BattleTypeKind.Scuffle4,
        BattleTypeKind.Rift1v1,
        BattleTypeKind.Rift2v2,
    };

    [Fact]
    public void Catalog_ListsTheNativeButtonsInWindowOrder()
    {
        Assert.Equal(WindowOrder, BattleTypeCatalog.Entries.Select(e => e.Kind).ToArray());
        Assert.Equal(
            new[] { "1对1", "2对2", "生存模式", "组队匹配", "4人混战", "时空裂隙 1V1", "时空裂隙 2V2" },
            BattleTypeCatalog.Entries.Select(e => e.Label).ToArray());
    }

    [Fact]
    public void Catalog_KindsAreUnique()
    {
        var kinds = BattleTypeCatalog.Entries.Select(e => e.Kind).ToArray();

        Assert.Equal(kinds.Length, kinds.Distinct().Count());
    }

    [Theory]
    [InlineData(BattleTypeKind.Vs1v1)]
    [InlineData(BattleTypeKind.Vs2v2)]
    [InlineData(BattleTypeKind.Survive)]
    [InlineData(BattleTypeKind.TeamMatch)]
    [InlineData(BattleTypeKind.Scuffle4)]
    [InlineData(BattleTypeKind.Rift1v1)]
    [InlineData(BattleTypeKind.Rift2v2)]
    public void Catalog_ByKindReturnsTheMatchingEntry(BattleTypeKind kind)
    {
        var entry = BattleTypeCatalog.ByKind(kind);

        Assert.NotNull(entry);
        Assert.Equal(kind, entry!.Kind);
    }

    [Fact]
    public void Catalog_ByKindOfAnUnknownKindReturnsNull()
    {
        Assert.Null(BattleTypeCatalog.ByKind((BattleTypeKind)99));
    }

    [Fact]
    public void Catalog_VerifiedRoomEntriesCarryRealModes()
    {
        Assert.All(
            BattleTypeCatalog.Entries.Where(e => !e.IsTeamMatch && e.Verified),
            e =>
            {
                Assert.True(e.GameMode >= 0);
                Assert.True(e.MatchMode >= 0);
            });
    }

    /// <summary>
    /// Pairs with the row above into a two-way invariant that holds under either trace outcome: a later
    /// edit that hands 组队匹配 real modes while leaving IsTeamMatch set fails here.
    /// </summary>
    [Fact]
    public void Catalog_TeamMatchEntriesCarryNoRoomMode()
    {
        Assert.All(
            BattleTypeCatalog.Entries.Where(e => e.IsTeamMatch),
            e =>
            {
                Assert.Equal(BattleTypeCatalog.NoRoomMode, e.GameMode);
                Assert.Equal(BattleTypeCatalog.NoRoomMode, e.MatchMode);
            });
    }

    /// <summary>
    /// An unverified entry must be incapable of reaching CreateRoom, so guessing a mode and forgetting
    /// to flip Verified cannot ship a wrong room type.
    /// </summary>
    [Fact]
    public void Catalog_UnverifiedEntriesCarryNoRoomMode()
    {
        Assert.All(
            BattleTypeCatalog.Entries.Where(e => !e.Verified),
            e =>
            {
                Assert.Equal(BattleTypeCatalog.NoRoomMode, e.GameMode);
                Assert.Equal(BattleTypeCatalog.NoRoomMode, e.MatchMode);
            });
    }

    /// <summary>
    /// Transcribed from the 2026-09-10 21:47–21:48 capture in the trace table. These are observations of
    /// the native window, not derivations, so this theory is the only place they appear.
    /// </summary>
    [Theory]
    [InlineData(BattleTypeKind.Vs1v1, 0, 0, false)]     // Btn1v1OnClicked        Normal, VS_1_1
    [InlineData(BattleTypeKind.Vs2v2, 0, 1, false)]     // Btn2v2OnClicked        Normal, VS_2_2
    [InlineData(BattleTypeKind.Survive, 3, 1, false)]   // BtnSurviveOnClicked    Survive, VS_2_2
    [InlineData(BattleTypeKind.TeamMatch, -1, -1, true)] // MatchBtnOnClicked     RequestTeamInvite, no room
    [InlineData(BattleTypeKind.Scuffle4, 0, 2, false)]  // BtnChaosFactionOnClicked Normal, VS_4_Scuffle
    [InlineData(BattleTypeKind.Rift1v1, 4, 0, false)]   // BtnRift1v1OnClicked    Rift, VS_1_1
    [InlineData(BattleTypeKind.Rift2v2, 4, 1, false)]   // BtnRift2v2OnClicked    Rift, VS_2_2
    public void Catalog_ModeMappingMatchesTheCapturedTrace(
        BattleTypeKind kind,
        int gameMode,
        int matchMode,
        bool isTeamMatch)
    {
        var entry = BattleTypeCatalog.ByKind(kind);

        Assert.NotNull(entry);
        Assert.Equal(gameMode, entry!.GameMode);
        Assert.Equal(matchMode, entry.MatchMode);
        Assert.Equal(isTeamMatch, entry.IsTeamMatch);
    }

    /// <summary>
    /// The whole catalog is captured, so nothing should still be rendering as 暂未开放. A future game
    /// version adding a button would land here first.
    /// </summary>
    [Fact]
    public void Catalog_EveryEntryIsTraceVerified()
    {
        Assert.All(BattleTypeCatalog.Entries, e => Assert.True(e.Verified));
    }

    /// <summary>Catches a mode pair copy-pasted from a neighbouring button.</summary>
    [Fact]
    public void Catalog_NoTwoVerifiedRoomEntriesShareTheSameModePair()
    {
        var pairs = BattleTypeCatalog.Entries
            .Where(e => !e.IsTeamMatch && e.Verified)
            .Select(e => e.GameMode + ":" + e.MatchMode)
            .ToArray();

        Assert.Equal(pairs.Length, pairs.Distinct().Count());
    }
}
