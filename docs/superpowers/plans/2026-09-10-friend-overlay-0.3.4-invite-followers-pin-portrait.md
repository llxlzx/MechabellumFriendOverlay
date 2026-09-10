# FriendOverlay 0.3.4 — Invite · 关注我的人 · Pin · Portrait+Frame Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add room-invite, a second「关注我的人」tab, local pinning, and game-faithful avatar + avatar-frame rendering to the Mechabellum FriendOverlay MelonMod, each behind its own capability gate, and ship as 0.3.4.

**Architecture:** Approach B — data-layer direct + IMGUI self-draw. Pure decision logic (pin set, invite gating/cooldown, portrait planning, pinned sectioning) lives in `FriendOverlay.Core` (netstandard2.0, xunit-tested). Game access goes through thin static services in `src/FriendOverlay/Data` (`GameProxies`, `FansListService`, `PinStore`, `PortraitResolver`) that wrap every IL2CPP call in try/catch. A new `Compat/Capabilities` probe sets three independent flags (`Invite`, `Followers`, `Portrait`); a missing surface degrades one feature (disabled button / hidden tab / legacy avatar route), never the mod.

**Tech Stack:** .NET 6 MelonLoader mod (Il2CppInterop bindings over `Il2CppGRClient`, `Il2CppGRCore`, `Il2CppBinNetwork`, `Il2CppGRUtility`), HarmonyLib, Unity IMGUI absolute-rect drawing (`GUI.DrawTexture` / `GUI.Label` / `GUI.Button` only), netstandard2.0 Core, xunit 2.5 tests on net8.0.

## Global Constraints

- Repo root: `D:\gongzuo\独立工作区\MechabellumFriendOverlay`. All paths below are relative to it.
- **No git repo exists yet.** Before Task 1 run `git init` in the repo root and commit the current tree as `chore: baseline 0.3.3` (`.gitignore` already excludes `bin/ obj/`). Every task ends with a commit.
- Version: bump `0.3.3` → `0.3.4` in exactly two places (`src/FriendOverlay/FriendOverlay.csproj` `<Version>` and `[assembly: MelonInfo(...)]` in `src/FriendOverlay/FriendOverlayMod.cs`) plus the 「当前版本」 line in `玩家说明.md`. Done in Task 6 only.
- Approach B only: read game data via proxies, draw with IMGUI. No uGUI borrowing. **Do NOT resurrect `BorrowAvatars`** or any native-cell texture borrowing tier.
- Per-feature capability gating: `Capabilities.Invite`, `Capabilities.Followers`, `Capabilities.Portrait`. A false flag disables one feature; `TypeProbe` (whole-mod fail-open) is unchanged.
- Invite: `LobbyProxy.TryRequestInvite(uid, name, discord:false)`, fallback `LobbyProxy.InviteUserJoin(uid, false)`. No `TeamProxy` unless the Task 2 trace step proves the native button uses it. Enabled only when `Capabilities.Invite && JoinedRoom != null && row.IsOnline`; otherwise **disabled, not hidden**. Local 5 s「已邀请」cooldown per row.
- The second tab is named **「关注我的人」** everywhere (UI, docs, code comments). Never 「粉丝」. Title tabs read「关注 | 关注我的人」.
- 「关注我的人」 uses `FriendProxy.followerBaseInfoList` + `RequestLastFollower()`. Do **not** call `RequestFollowerStatus` (probe shows `ResponseFollowStatus.Following` — it pages *following* status, not fans). Has the same 可加入 / 对战中 / 离线 sections. No pin on this tab. Hide the tab when the APIs are missing.
- Pin: MelonPreferences `PinnedUserIds` (comma-separated ulong) + `CollapsePinned`; save on change; **max 20**; following list only; pinned rows leave their availability section. **Never writes to the game server.**
- Avatar: display what the player selected in-game (official avatar vs custom photo) using the game's own `PlayerRiskInfo → PlayerPortraitInfo` resolution, not a forced FaceId-over-FaceUrl. Draw the avatar **frame** (`GetAvatarOutLineURL`) as an overlay. BlockFace follows game policy: blocked rows show what native shows (the game-substituted placeholder). Letter-first draw retained; `Ctrl+L` hides images **and** frames.
- Out of scope: team invite, Discord invite, pin on 关注我的人, server-side pin, animated frame prefabs, blacklist/discord tabs.
- IL2CPP IMGUI rules (already in force): absolute rects only, no `GUILayout`, no `GUI.BeginScrollView`, no `Texture2D.SetPixel`. Every game call inside try/catch; every log line prefixed `[FriendOverlay]`; never log per-row per-frame.
- Core project must stay netstandard2.0-compatible (no `init` accessors, no `record`). New Core files must be added to **both** `src/FriendOverlay/FriendOverlay.csproj` `<Compile Include>` (linked) and are picked up automatically by `FriendOverlay.Core.csproj`.
- Test command: `dotnet test tests\FriendOverlay.Tests\FriendOverlay.Tests.csproj`. Build: `dotnet build src\FriendOverlay\FriendOverlay.csproj -c Release`. Deploy: append `-p:Deploy=true` (deletes all `Mods\FriendOverlay*.dll` then copies).
- In-game checks require the game with MelonLoader; log file is `D:\steam\steamapps\common\Mechabellum\MelonLoader\Latest.log`.

---

## File Structure

**Create**

| Path | Responsibility |
|---|---|
| `src/FriendOverlay.Core/InviteRules.cs` | `InviteRules.CanInvite(...)` gate + `InviteCooldown` (injected clock) |
| `src/FriendOverlay.Core/PinnedIds.cs` | Ordered, capped (20) set of pinned ids; CSV parse/serialize |
| `src/FriendOverlay.Core/PortraitPlan.cs` | `PortraitKind`, `PortraitPlan`, `PortraitPlanner.Decide(...)` |
| `src/FriendOverlay/Compat/Capabilities.cs` | Optional-surface probe → `Invite/Followers/Portrait` flags |
| `src/FriendOverlay/Data/GameProxies.cs` | Locates `LobbyProxy` via `GameFacade`; `IsInRoom()` |
| `src/FriendOverlay/Data/FriendRowMapper.cs` | `FriendBaseInfo` → `FriendRowVm` (shared by both list services) |
| `src/FriendOverlay/Data/FansListService.cs` | 关注我的人 snapshot/refresh/online batch |
| `src/FriendOverlay/Data/PinStore.cs` | Preference-backed wrapper around `PinnedIds` |
| `src/FriendOverlay/Data/PortraitResolver.cs` | Builds game `PlayerRiskInfo` → `PortraitPlan`, cached per uid |
| `src/FriendOverlay/UI/GameSpriteResolver.cs` | Local `SpriteManager` lookup + URL normalization by image ref (replaces `FaceIconResolver.cs`) |
| `src/FriendOverlay/UI/SharedImageCache.cs` | Ref-keyed cache for official avatars, dark placeholder, frames |
| `tests/FriendOverlay.Tests/InviteRulesTests.cs` | |
| `tests/FriendOverlay.Tests/PinnedIdsTests.cs` | |
| `tests/FriendOverlay.Tests/PortraitPlannerTests.cs` | |

**Modify**

| Path | Change |
|---|---|
| `src/FriendOverlay.Core/FriendRowVm.cs` | + `Portrait`, `PortraitRef`, `FrameRef`; − `FaceId` |
| `src/FriendOverlay.Core/FriendSection.cs` | + `FriendSectionKind.Pinned` |
| `src/FriendOverlay.Core/FriendQueryPipeline.cs` | `GroupIntoSections(rows, PinnedIds? pinned)` overload |
| `src/FriendOverlay/FriendOverlay.csproj` | link new Core files; version |
| `src/FriendOverlay/FriendOverlayMod.cs` | `Capabilities.Probe()`, pin prefs, `FansListService.Tick()`, version |
| `src/FriendOverlay/State/OverlaySession.cs` | reset `GameProxies`, `SharedImageCache`, `PortraitResolver`, `GameSpriteResolver` |
| `src/FriendOverlay/Hooks/FriendPanelHooks.cs` | `FansListService` open/close; `OnResponseLastFollower` postfix |
| `src/FriendOverlay/Data/FriendListService.cs` | use `FriendRowMapper` |
| `src/FriendOverlay/Actions/FriendActions.cs` | + `Invite`, `FollowBack`; unfollow/blacklist unpin + refresh fans |
| `src/FriendOverlay/UI/Widgets/RowCard.cs` | `RowContext`, `FriendListTab`, invite/回关 buttons, tab-aware menu, frame overlay |
| `src/FriendOverlay/UI/Widgets/SectionHeader.cs` | Pinned title/color |
| `src/FriendOverlay/UI/OverlayPanel.cs` | title tabs, source switch, pin/collapse, invite cooldown, new actions |
| `src/FriendOverlay/UI/GameAssets.cs` | route by `PortraitKind`; request frames |
| `src/FriendOverlay/UI/AvatarLoader.cs` | `RequestShared(url, sink)`; string in-flight keys |
| `tests/FriendOverlay.Tests/FriendSectionTests.cs` | pinned-section tests |
| `README.md`, `玩家说明.md` | features, prefs, acceptance, version |

**Delete**

- `src/FriendOverlay/UI/FaceIconResolver.cs` (Task 5, replaced by `GameSpriteResolver.cs` + `SharedImageCache.cs`)

---

### Task 1: Capabilities probe + GameProxies (LobbyProxy locator)

**Files:**
- Create: `src/FriendOverlay/Compat/Capabilities.cs`
- Create: `src/FriendOverlay/Data/GameProxies.cs`
- Modify: `src/FriendOverlay/FriendOverlayMod.cs:33-56`
- Modify: `src/FriendOverlay/State/OverlaySession.cs:38-49, 70-106`
- Modify: `src/FriendOverlay/Hooks/FriendPanelHooks.cs:41-49`

**Interfaces:**
- Consumes: existing `TypeProbe.Probe()`, `OverlaySession.Begin/End`.
- Produces: `Capabilities.Invite : bool`, `Capabilities.Followers : bool`, `Capabilities.Portrait : bool`, `Capabilities.Probe()`, `Capabilities.Summary : string`; `GameProxies.Lobby : LobbyProxy?`, `GameProxies.IsInRoom() : bool`, `GameProxies.Reset()`.

- [ ] **Step 1: Initialise git (once)**

```powershell
cd D:\gongzuo\独立工作区\MechabellumFriendOverlay
git init
git add -A
git commit -m "chore: baseline 0.3.3"
```

- [ ] **Step 2: Create `Capabilities.cs`**

```csharp
// src/FriendOverlay/Compat/Capabilities.cs
using System;
using HarmonyLib;
using Il2CppGameRiver;
using Il2CppGameRiver.Client;
using MelonLoader;
using GameRiskInfo = Il2CppGameRiver.PlayerRiskInfo;

namespace FriendOverlay.Compat
{
    /// <summary>
    /// Optional game surfaces. Each flag gates exactly one feature; none of them can take the mod
    /// down. TypeProbe decides whether the overlay runs at all, this decides what it offers.
    /// </summary>
    public static class Capabilities
    {
        public static bool Invite { get; private set; }
        public static bool Followers { get; private set; }
        public static bool Portrait { get; private set; }

        public static string Summary => "invite=" + Invite + " followers=" + Followers + " portrait=" + Portrait;

        public static void Probe()
        {
            Invite = Has(() =>
                AccessTools.Property(typeof(GameFacade), "Instance") != null &&
                AccessTools.Property(typeof(LobbyProxy), "JoinedRoom") != null &&
                (AccessTools.Method(typeof(LobbyProxy), "TryRequestInvite", new[] { typeof(ulong), typeof(string), typeof(bool) }) != null ||
                 AccessTools.Method(typeof(LobbyProxy), "InviteUserJoin", new[] { typeof(ulong), typeof(bool) }) != null));

            Followers = Has(() =>
                AccessTools.Property(typeof(FriendProxy), "followerBaseInfoList") != null &&
                AccessTools.Method(typeof(FriendProxy), "RequestLastFollower") != null &&
                AccessTools.Method(typeof(FriendProxy), "RequestFollowUser", new[] { typeof(ulong) }) != null);

            Portrait = Has(() =>
                AccessTools.Method(typeof(GameRiskInfo), "GetPortraitInfo") != null &&
                AccessTools.Method(typeof(GameRiskInfo), "GetPortrait") != null &&
                AccessTools.Method(typeof(PlayerPortraitInfo), "GetAvatarURL") != null &&
                AccessTools.Method(typeof(PlayerPortraitInfo), "GetAvatarOutLineURL") != null);

            MelonLogger.Msg("[FriendOverlay] capabilities " + Summary);
        }

        private static bool Has(Func<bool> check)
        {
            try
            {
                return check();
            }
            catch (Exception ex)
            {
                MelonLogger.Msg("[FriendOverlay] capability probe threw: " + ex.Message);
                return false;
            }
        }
    }
}
```

- [ ] **Step 3: Create `GameProxies.cs`**

```csharp
// src/FriendOverlay/Data/GameProxies.cs
using System;
using Il2CppGameRiver.Client;
using MelonLoader;
using UnityEngine;
using MvcFacade = Il2CppPureMVC.Patterns.Facade.Facade;

namespace FriendOverlay.Data
{
    /// <summary>
    /// Locates proxies the FriendPanel does not hand us. Lookups are retried on a timer instead of
    /// being memoized forever, because a proxy can be registered after the friends panel opens.
    /// </summary>
    public static class GameProxies
    {
        private const float RetrySeconds = 2f;

        private static LobbyProxy? _lobby;
        private static float _nextLobbyTry;
        private static bool _loggedLobbyMiss;

        public static LobbyProxy? Lobby
        {
            get
            {
                if (_lobby != null)
                    return _lobby;

                if (Time.unscaledTime < _nextLobbyTry)
                    return null;

                _nextLobbyTry = Time.unscaledTime + RetrySeconds;
                _lobby = ResolveLobby();
                return _lobby;
            }
        }

        /// <summary>True only while the local player sits in a custom room (invite target).</summary>
        public static bool IsInRoom()
        {
            var lobby = Lobby;
            if (lobby == null)
                return false;

            try
            {
                return lobby.JoinedRoom != null;
            }
            catch
            {
                return false;
            }
        }

        public static void Reset()
        {
            _lobby = null;
            _nextLobbyTry = 0f;
            _loggedLobbyMiss = false;
        }

        private static LobbyProxy? ResolveLobby()
        {
            try
            {
                var facade = GameFacade.Instance;
                if (facade == null)
                    return Miss("GameFacade.Instance is null");

                LobbyProxy? proxy = null;
                try
                {
                    proxy = facade.RetrieveProxy<LobbyProxy>(Proxy<LobbyProxy>.NAME);
                }
                catch (Exception ex)
                {
                    MelonLogger.Msg("[FriendOverlay] generic RetrieveProxy<LobbyProxy> unavailable: " + ex.Message);
                }

                if (proxy == null)
                {
                    try
                    {
                        // PureMVC default proxy name is the type name; GetProxyDefaultName<T>() uses the same rule.
                        var raw = ((MvcFacade)facade).RetrieveProxy("LobbyProxy");
                        proxy = raw?.TryCast<LobbyProxy>();
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Msg("[FriendOverlay] RetrieveProxy(\"LobbyProxy\") unavailable: " + ex.Message);
                    }
                }

                if (proxy == null)
                    return Miss("LobbyProxy not registered");

                MelonLogger.Msg("[FriendOverlay] LobbyProxy resolved");
                return proxy;
            }
            catch (Exception ex)
            {
                return Miss(ex.Message);
            }
        }

        private static LobbyProxy? Miss(string why)
        {
            if (!_loggedLobbyMiss)
            {
                _loggedLobbyMiss = true;
                MelonLogger.Warning("[FriendOverlay] LobbyProxy unavailable, invite disabled for now: " + why);
            }

            return null;
        }
    }
}
```

- [ ] **Step 4: Wire probe into `FriendOverlayMod.OnInitializeMelon`**

Replace lines 38-55 of `src/FriendOverlay/FriendOverlayMod.cs` with:

```csharp
            var probe = TypeProbe.Probe();
            if (!probe.Ok)
            {
                OverlaySession.Degraded = true;
                LoggerInstance.Warning("FriendOverlay disabled (fail-open). Native friends UI untouched. " + probe.Message);
                return;
            }

            // Optional surfaces are probed before the hooks so hook installation can consult them.
            Capabilities.Probe();

            try
            {
                FriendPanelHooks.Apply(HarmonyInstance);
                LoggerInstance.Msg("FriendOverlay hooks applied. Overlay default=" + OverlaySession.PreferOverlayDefault);
            }
            catch (System.Exception ex)
            {
                OverlaySession.Degraded = true;
                LoggerInstance.Error("Harmony patch failed; fail-open. " + ex);
            }
```

- [ ] **Step 5: Reset `GameProxies` with the session**

In `src/FriendOverlay/State/OverlaySession.cs`, inside `Begin(...)` the `if (Panel != panel)` block, add after `ResetAvatarPipeline();`:

```csharp
                Data.GameProxies.Reset();
```

In `End(...)`, after `ResetAvatarPipeline();` add:

```csharp
            Data.GameProxies.Reset();
```

- [ ] **Step 6: Log room state once per open (diagnostic for Task 2)**

In `src/FriendOverlay/Hooks/FriendPanelHooks.cs` `ClickFriendBtnPostfix`, inside `if (open)` after `FriendListService.OnPanelOpened();` add:

```csharp
                    MelonLogger.Msg("[FriendOverlay] inRoom=" + Data.GameProxies.IsInRoom() + " " + Compat.Capabilities.Summary);
```

- [ ] **Step 7: Build, deploy, verify in game**

Run: `dotnet build src\FriendOverlay\FriendOverlay.csproj -c Release -p:Deploy=true`
Expected: `Build succeeded.` and `Deployed FriendOverlay.dll v0.3.3 to ...\Mods`.

Start game, open friends. In `MelonLoader\Latest.log` expect:
- `[FriendOverlay] capabilities invite=True followers=True portrait=True`
- `[FriendOverlay] inRoom=False invite=True ...` (from the lobby) and either `LobbyProxy resolved` or one `LobbyProxy unavailable ...` warning (not repeated per frame).
- Create a 自定义房间, open friends again: `inRoom=True`.

- [ ] **Step 8: Commit**

```bash
git add src/FriendOverlay/Compat/Capabilities.cs src/FriendOverlay/Data/GameProxies.cs src/FriendOverlay/FriendOverlayMod.cs src/FriendOverlay/State/OverlaySession.cs src/FriendOverlay/Hooks/FriendPanelHooks.cs
git commit -m "feat: per-feature capability probe and LobbyProxy locator"
```

---

### Task 2: Room invite (Core rules + action + row button)

**Files:**
- Create: `src/FriendOverlay.Core/InviteRules.cs`
- Create: `tests/FriendOverlay.Tests/InviteRulesTests.cs`
- Modify: `src/FriendOverlay/FriendOverlay.csproj:19-26`
- Modify: `src/FriendOverlay/Actions/FriendActions.cs`
- Modify: `src/FriendOverlay/UI/Widgets/RowCard.cs`
- Modify: `src/FriendOverlay/UI/OverlayPanel.cs`

**Interfaces:**
- Consumes: `Capabilities.Invite`, `GameProxies.Lobby`, `GameProxies.IsInRoom()` (Task 1).
- Produces: `InviteRules.CanInvite(bool capability, bool inRoom, bool online) : bool`; `InviteCooldown(double seconds)` with `Mark(ulong, double now)`, `IsActive(ulong, double now) : bool`, `Clear()`; `FriendActions.Invite(FriendRowVm) : bool`; `RowAction.Invite`; `struct RowContext { bool CanInvite; bool InviteRecent; }`; `RowCard.Draw(Rect, FriendRowVm, RowContext, bool interactive, bool menuOpen, float hoverT)`; `RowCard.DrawMenu(Rect, FriendRowVm, RowContext)`; `RowCard.MenuHeightFor(FriendRowVm, RowContext) : float`; `ImguiFriendOverlay.BuildContext(FriendRowVm)` (private).

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/FriendOverlay.Tests/InviteRulesTests.cs
using FriendOverlay.Core;
using Xunit;

namespace FriendOverlay.Tests;

public class InviteRulesTests
{
    [Theory]
    [InlineData(true, true, true, true)]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, false)]
    public void CanInvite_RequiresCapabilityRoomAndOnline(bool capability, bool inRoom, bool online, bool expected)
    {
        Assert.Equal(expected, InviteRules.CanInvite(capability, inRoom, online));
    }

    [Fact]
    public void Cooldown_IsActiveInsideWindowOnly()
    {
        var c = new InviteCooldown(5.0);
        c.Mark(7, 100.0);

        Assert.True(c.IsActive(7, 100.0));
        Assert.True(c.IsActive(7, 104.9));
        Assert.False(c.IsActive(7, 105.0));
    }

    [Fact]
    public void Cooldown_UnknownUserIsInactive()
    {
        Assert.False(new InviteCooldown(5.0).IsActive(1, 0.0));
    }

    [Fact]
    public void Cooldown_IsPerUser()
    {
        var c = new InviteCooldown(5.0);
        c.Mark(1, 0.0);
        Assert.False(c.IsActive(2, 1.0));
    }

    [Fact]
    public void Cooldown_ReMarkExtendsWindow()
    {
        var c = new InviteCooldown(5.0);
        c.Mark(1, 0.0);
        c.Mark(1, 4.0);
        Assert.True(c.IsActive(1, 8.0));
    }

    [Fact]
    public void Cooldown_ClearForgetsEverything()
    {
        var c = new InviteCooldown(5.0);
        c.Mark(1, 0.0);
        c.Clear();
        Assert.False(c.IsActive(1, 1.0));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests\FriendOverlay.Tests\FriendOverlay.Tests.csproj --filter "FullyQualifiedName~InviteRulesTests"`
Expected: build error `The type or namespace name 'InviteRules' could not be found`.

- [ ] **Step 3: Implement `InviteRules.cs`**

```csharp
// src/FriendOverlay.Core/InviteRules.cs
using System.Collections.Generic;

namespace FriendOverlay.Core
{
    public static class InviteRules
    {
        /// <summary>
        /// Invite is offered only when the game exposes it, the local player sits in a custom room
        /// and the friend is online. Callers render a disabled button otherwise, never hide it.
        /// </summary>
        public static bool CanInvite(bool capability, bool inRoom, bool online) =>
            capability && inRoom && online;
    }

    /// <summary>
    /// Per-row "已邀请" window so a double click cannot fire two invites. The clock is injected so
    /// tests never sleep; the UI passes Time.unscaledTime.
    /// </summary>
    public sealed class InviteCooldown
    {
        private readonly double _seconds;
        private readonly Dictionary<ulong, double> _markedAt = new Dictionary<ulong, double>();

        public InviteCooldown(double seconds)
        {
            _seconds = seconds;
        }

        public void Mark(ulong userId, double now) => _markedAt[userId] = now;

        public bool IsActive(ulong userId, double now) =>
            _markedAt.TryGetValue(userId, out var at) && now - at < _seconds;

        public void Clear() => _markedAt.Clear();
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests\FriendOverlay.Tests\FriendOverlay.Tests.csproj --filter "FullyQualifiedName~InviteRulesTests"`
Expected: `Passed! - Failed: 0, Passed: 9`.

- [ ] **Step 5: Link the Core file into the mod project**

In `src/FriendOverlay/FriendOverlay.csproj`, inside the first `<ItemGroup>` add:

```xml
    <Compile Include="..\FriendOverlay.Core\InviteRules.cs" Link="Core\InviteRules.cs" />
```

- [ ] **Step 6: Add `FriendActions.Invite`**

In `src/FriendOverlay/Actions/FriendActions.cs` add `using FriendOverlay.Data;` and insert after `Chat(...)`:

```csharp
        /// <summary>
        /// Mirrors the native FriendBtnListWindow invite button. Returns true only when a request
        /// went out, so the caller starts the 已邀请 cooldown for real sends only.
        /// </summary>
        public static bool Invite(FriendRowVm row)
        {
            var lobby = GameProxies.Lobby;
            if (lobby == null)
            {
                MelonLogger.Warning("[FriendOverlay] Invite: LobbyProxy unavailable");
                return false;
            }

            try
            {
                lobby.TryRequestInvite(row.UserId, row.Name ?? string.Empty, false);
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[FriendOverlay] TryRequestInvite failed, falling back to InviteUserJoin: " + ex.Message);
            }

            try
            {
                lobby.InviteUserJoin(row.UserId, false);
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[FriendOverlay] Invite: " + ex.Message);
                return false;
            }
        }
```

- [ ] **Step 7: Extend `RowCard` with `RowContext` and the invite button**

In `src/FriendOverlay/UI/Widgets/RowCard.cs`:

Replace the `RowAction` enum with:

```csharp
    public enum RowAction
    {
        None = 0,
        Join = 1,
        Watch = 2,
        Chat = 3,
        ToggleMenu = 4,
        Unfollow = 5,
        Blacklist = 6,
        Invite = 7,
    }

    /// <summary>Per-row facts the card cannot derive from the row itself.</summary>
    public struct RowContext
    {
        public bool CanInvite;
        public bool InviteRecent;
    }
```

Remove the `MenuHeight` property and add:

```csharp
        public static float MenuHeightFor(FriendRowVm row, RowContext ctx) => Theme.S(34f) * 2f + Theme.S(8f);
```

Change the `Draw` signature and the two lines that use actions width / draw actions:

```csharp
        public static RowAction Draw(Rect r, FriendRowVm row, RowContext ctx, bool interactive, bool menuOpen, float hoverT)
```

```csharp
            var actionsW = ActionsWidth(ctx);
```

```csharp
                var hoverAction = DrawActions(actionsR, row, ctx, menuOpen);
```

Replace `DrawMenu`, `ActionsWidth` and `DrawActions` with:

```csharp
        /// <summary>Overflow menu, drawn after the list so it stacks above other rows.</summary>
        public static RowAction DrawMenu(Rect r, FriendRowVm row, RowContext ctx)
        {
            Gfx.Fill(r, Theme.Bg1);
            Gfx.Border(r, Theme.Line);

            var itemH = Theme.S(34f);
            var unfollow = new Rect(r.x + Theme.S(4f), r.y + Theme.S(4f), r.width - Theme.S(8f), itemH);
            var blacklist = new Rect(unfollow.x, unfollow.yMax, unfollow.width, itemH);

            if (MenuItem(unfollow, "取消关注", Theme.TextMain, true))
                return RowAction.Unfollow;
            if (MenuItem(blacklist, "加入黑名单", Theme.DangerHover, true))
                return RowAction.Blacklist;

            return RowAction.None;
        }

        private static bool MenuItem(Rect r, string label, Color color, bool enabled)
        {
            var hovered = enabled && Gfx.Hover(r);
            if (hovered)
                Gfx.Fill(r, Theme.ChipHover);

            var text = enabled ? color : new Color(Theme.TextMuted.r, Theme.TextMuted.g, Theme.TextMuted.b, 0.5f);
            Gfx.Text(new Rect(r.x + Theme.S(10f), r.y, r.width - Theme.S(20f), r.height), label, text, Theme.Button);
            return enabled && Gfx.Hit(r);
        }

        private static float ActionsWidth(RowContext ctx)
        {
            var w = Theme.S(52f);
            var gap = Theme.S(5f);
            return w * 4f + MoreWidth + gap * 4f;
        }

        private static RowAction DrawActions(Rect r, FriendRowVm row, RowContext ctx, bool menuOpen)
        {
            var action = RowAction.None;
            var w = Theme.S(52f);
            var gap = Theme.S(5f);
            var x = r.x;

            var canJoin = row.IsOnline && !row.IsBusy;
            var canWatch = row.IsOnline && row.IsBusy;

            if (Button(new Rect(x, r.y, w, r.height), "加入", Theme.Chip, canJoin, true))
                action = RowAction.Join;
            x += w + gap;

            if (Button(new Rect(x, r.y, w, r.height), "观战", Theme.Chip, canWatch, false))
                action = RowAction.Watch;
            x += w + gap;

            if (Button(new Rect(x, r.y, w, r.height), "私聊", Theme.Chip, true, false))
                action = RowAction.Chat;
            x += w + gap;

            // Disabled rather than hidden so the player learns inviting needs a custom room.
            var inviteLabel = ctx.InviteRecent ? "已邀请" : "邀请";
            if (Button(new Rect(x, r.y, w, r.height), inviteLabel, Theme.Chip, ctx.CanInvite && !ctx.InviteRecent, false))
                action = RowAction.Invite;
            x += w + gap;

            var moreR = new Rect(x, r.y, MoreWidth, r.height);
            if (Button(moreR, "⋯", menuOpen ? Theme.ChipHover : Theme.Chip, true, false))
                action = RowAction.ToggleMenu;

            return action;
        }
```

Delete the old private `MenuItem(Rect, string, Color)` (three-argument) method.

- [ ] **Step 8: Wire the panel**

In `src/FriendOverlay/UI/OverlayPanel.cs`:

Add usings `using FriendOverlay.Compat;` (Core/Data already imported). Add fields next to `_lastViewportH`:

```csharp
        private static readonly InviteCooldown _inviteCooldown = new InviteCooldown(5.0);
        private static bool _inRoom;
```

Add the private helper after `TrackHover`:

```csharp
        private static RowContext BuildContext(FriendRowVm row) => new RowContext
        {
            CanInvite = InviteRules.CanInvite(Capabilities.Invite, _inRoom, row.IsOnline),
            InviteRecent = _inviteCooldown.IsActive(row.UserId, Time.unscaledTime),
        };
```

In `EnsureView`, after `_viewFrame = Time.frameCount;` add:

```csharp
            // One proxy read per frame; the room can be created or left while the panel is open.
            _inRoom = Capabilities.Invite && GameProxies.IsInRoom();
```

In `DrawList`, replace the row block from `var hoverT = ...` through the `_menuRect = ...` assignment with:

```csharp
                var ctx = BuildContext(row);
                var hoverT = TrackHover(row.UserId, interactive && Gfx.Hover(rect));
                var action = RowCard.Draw(rect, row, ctx, interactive, isMenuOwner, hoverT);
                if (action != RowAction.None)
                    Invoke(action, row);

                // Recomputed after Draw so a menu opened this frame is already anchored correctly.
                if (_menuRowId == row.UserId)
                {
                    menuOwnerVisible = true;
                    var menuH = RowCard.MenuHeightFor(row, ctx);
                    _menuRect = new Rect(
                        rect.xMax - RowCard.MenuWidth - Theme.S(6f),
                        Mathf.Min(rect.yMax - Theme.S(4f), listRect.yMax - menuH - Theme.S(4f)),
                        RowCard.MenuWidth,
                        menuH);
                }
```

In `DrawOverflowMenu` replace `var action = RowCard.DrawMenu(_menuRect);` with:

```csharp
            var action = RowCard.DrawMenu(_menuRect, row, BuildContext(row));
```

In `Invoke`, add a case:

```csharp
                case RowAction.Invite:
                    if (FriendActions.Invite(row))
                        _inviteCooldown.Mark(row.UserId, Time.unscaledTime);
                    break;
```

In `OnSessionEnd`, after `_menuRowId = 0;` add:

```csharp
            _inviteCooldown.Clear();
```

In `DrawFooter` change the hint string to:

```csharp
                "拖动标题栏移动  ·  右下角缩放  ·  ⋯/右键 更多操作  ·  邀请需先进入自定义房间  ·  " + HotkeyName + " 原生面板  ·  Ctrl+F 搜索  ·  R 刷新  ·  Esc 关闭菜单" +
```

- [ ] **Step 9: Build**

Run: `dotnet build src\FriendOverlay\FriendOverlay.csproj -c Release`
Expected: `Build succeeded.` with 0 errors (warnings about unused `row`/`ctx` parameters are acceptable).

- [ ] **Step 10: Native-parity trace (temporary, then delete)**

Create `src/FriendOverlay/Hooks/InviteTrace.cs`:

```csharp
// TEMPORARY DIAGNOSTIC — delete before committing.
using HarmonyLib;
using Il2CppGameRiver.Client;
using MelonLoader;

namespace FriendOverlay.Hooks
{
    public static class InviteTrace
    {
        public static void Apply(HarmonyLib.Harmony h)
        {
            h.Patch(AccessTools.Method(typeof(LobbyProxy), nameof(LobbyProxy.TryRequestInvite)),
                prefix: new HarmonyMethod(typeof(InviteTrace), nameof(TryRequestInvitePrefix)));
            h.Patch(AccessTools.Method(typeof(LobbyProxy), nameof(LobbyProxy.InviteUserJoin)),
                prefix: new HarmonyMethod(typeof(InviteTrace), nameof(InviteUserJoinPrefix)));
            h.Patch(AccessTools.Method(typeof(TeamProxy), nameof(TeamProxy.RequestTeamInvite)),
                prefix: new HarmonyMethod(typeof(InviteTrace), nameof(TeamInvitePrefix)));
        }

        public static void TryRequestInvitePrefix(ulong userid, string userName, bool discord) =>
            MelonLogger.Msg("[trace] TryRequestInvite " + userid + " " + userName + " discord=" + discord);

        public static void InviteUserJoinPrefix(ulong userid, bool discord) =>
            MelonLogger.Msg("[trace] InviteUserJoin " + userid + " discord=" + discord);

        public static void TeamInvitePrefix(ulong target) =>
            MelonLogger.Msg("[trace] RequestTeamInvite " + target);
    }
}
```

In `FriendOverlayMod.OnInitializeMelon` temporarily add `Hooks.InviteTrace.Apply(HarmonyInstance);` right after `FriendPanelHooks.Apply(HarmonyInstance);`. Build with `-p:Deploy=true`, start game, create a 自定义房间, press `F8` (native panel), click an online friend → native 邀请 button.

Expected log: `[trace] TryRequestInvite <uid> <name> discord=False` (possibly followed by `[trace] InviteUserJoin ...` from inside it). If **only** `[trace] RequestTeamInvite` appears, change `FriendActions.Invite` to call `TeamProxy.RequestTeamInvite(row.UserId)` via a `GameProxies.Team` locator built like `Lobby`, and record the deviation in the commit message.

Then delete `src/FriendOverlay/Hooks/InviteTrace.cs` and the `InviteTrace.Apply` line.

- [ ] **Step 11: In-game verification of the overlay button**

Rebuild with `-p:Deploy=true`. Checks:
1. In lobby, not in a room: hover an online row → `邀请` present but disabled (dim); hover an offline row → disabled.
2. Create a 自定义房间, open friends: online rows show `邀请` enabled; click it → the friend receives an invite; button reads `已邀请` and is disabled for ~5 s, then re-enables.
3. Log contains no `Invite:` warnings and no `DrawWindow failed`.
4. Rename the DLL to simulate a missing surface is not possible; instead confirm `capabilities invite=True` — the disabled path was exercised in check 1.

- [ ] **Step 12: Commit**

```bash
git add src/FriendOverlay.Core/InviteRules.cs tests/FriendOverlay.Tests/InviteRulesTests.cs src/FriendOverlay/FriendOverlay.csproj src/FriendOverlay/Actions/FriendActions.cs src/FriendOverlay/UI/Widgets/RowCard.cs src/FriendOverlay/UI/OverlayPanel.cs
git commit -m "feat: room invite button with capability gate and 5s per-row cooldown"
```

---

### Task 3: Pin (置顶) — Core set, pipeline section, preferences, menu

**Files:**
- Create: `src/FriendOverlay.Core/PinnedIds.cs`
- Create: `tests/FriendOverlay.Tests/PinnedIdsTests.cs`
- Create: `src/FriendOverlay/Data/PinStore.cs`
- Modify: `src/FriendOverlay.Core/FriendSection.cs:6-11`
- Modify: `src/FriendOverlay.Core/FriendQueryPipeline.cs:72-108`
- Modify: `tests/FriendOverlay.Tests/FriendSectionTests.cs`
- Modify: `src/FriendOverlay/FriendOverlay.csproj`
- Modify: `src/FriendOverlay/FriendOverlayMod.cs`
- Modify: `src/FriendOverlay/Actions/FriendActions.cs:26-56`
- Modify: `src/FriendOverlay/UI/Widgets/SectionHeader.cs:10-22`
- Modify: `src/FriendOverlay/UI/Widgets/RowCard.cs`
- Modify: `src/FriendOverlay/UI/OverlayPanel.cs`

**Interfaces:**
- Consumes: `RowContext`, `RowCard.DrawMenu/MenuHeightFor`, `BuildContext` (Task 2); `OverlaySession.PersistPreferences`.
- Produces: `PinnedIds` (`Max = 20`, `Count`, `IsFull`, `Items`, `Contains`, `Add`, `Remove`, `Toggle(ulong, out bool nowPinned) : bool`, `static Parse(string?)`, `ToCsv()`); `FriendSectionKind.Pinned`; `FriendQueryPipeline.GroupIntoSections(rows, PinnedIds? pinned)`; `PinStore.Pins`, `PinStore.Load(string?)`, `PinStore.Csv`, `PinStore.Contains(ulong)`, `PinStore.IsFull`, `PinStore.Toggle(ulong) : bool`, `PinStore.Remove(ulong)`; `RowAction.TogglePin`; `RowContext.IsPinned`, `RowContext.PinFull`; `ImguiFriendOverlay.CollapsedPinned`.

- [ ] **Step 1: Write the failing `PinnedIds` tests**

```csharp
// tests/FriendOverlay.Tests/PinnedIdsTests.cs
using FriendOverlay.Core;
using Xunit;

namespace FriendOverlay.Tests;

public class PinnedIdsTests
{
    [Fact]
    public void Parse_IgnoresJunkWhitespaceAndDuplicates()
    {
        var pins = PinnedIds.Parse(" 1, 2,x,2 ,,3,-4,0");
        Assert.Equal(new ulong[] { 1, 2, 3 }, pins.Items.ToArray());
    }

    [Fact]
    public void Parse_NullOrBlankIsEmpty()
    {
        Assert.Equal(0, PinnedIds.Parse(null).Count);
        Assert.Equal(0, PinnedIds.Parse("   ").Count);
    }

    [Fact]
    public void Parse_TruncatesToMax()
    {
        var csv = string.Join(",", Enumerable.Range(1, PinnedIds.Max + 5).Select(i => i.ToString()));
        var pins = PinnedIds.Parse(csv);
        Assert.Equal(PinnedIds.Max, pins.Count);
        Assert.True(pins.IsFull);
        Assert.Equal((ulong)PinnedIds.Max, pins.Items[pins.Count - 1]);
    }

    [Fact]
    public void Add_RefusesZeroDuplicateAndOverflow()
    {
        var pins = new PinnedIds();
        Assert.False(pins.Add(0));
        Assert.True(pins.Add(5));
        Assert.False(pins.Add(5));
        for (ulong i = 100; pins.Count < PinnedIds.Max; i++)
            Assert.True(pins.Add(i));
        Assert.False(pins.Add(9999));
    }

    [Fact]
    public void Toggle_AddsThenRemovesAndReportsState()
    {
        var pins = new PinnedIds();
        Assert.True(pins.Toggle(7, out var nowPinned));
        Assert.True(nowPinned);
        Assert.True(pins.Contains(7));

        Assert.True(pins.Toggle(7, out nowPinned));
        Assert.False(nowPinned);
        Assert.False(pins.Contains(7));
    }

    [Fact]
    public void Toggle_WhenFullDoesNotChange()
    {
        var pins = PinnedIds.Parse(string.Join(",", Enumerable.Range(1, PinnedIds.Max)));
        Assert.False(pins.Toggle(777, out var nowPinned));
        Assert.False(nowPinned);
        Assert.Equal(PinnedIds.Max, pins.Count);
    }

    [Fact]
    public void Remove_ReturnsFalseWhenAbsent()
    {
        var pins = new PinnedIds();
        Assert.False(pins.Remove(1));
    }

    [Fact]
    public void ToCsv_RoundTripsInOrder()
    {
        var pins = PinnedIds.Parse("30,10,20");
        Assert.Equal("30,10,20", pins.ToCsv());
        Assert.Equal(string.Empty, new PinnedIds().ToCsv());
    }
}
```

- [ ] **Step 2: Write the failing pipeline tests**

Append to `tests/FriendOverlay.Tests/FriendSectionTests.cs` inside the class:

```csharp
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
        Assert.Equal(new ulong[] { 3, 2 }.OrderBy(x => x).ToArray(), sections[0].Rows.Select(r => r.UserId).OrderBy(x => x).ToArray());
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
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests\FriendOverlay.Tests\FriendOverlay.Tests.csproj`
Expected: build errors `'PinnedIds' could not be found` and `'FriendSectionKind' does not contain a definition for 'Pinned'`.

- [ ] **Step 4: Implement `PinnedIds.cs`**

```csharp
// src/FriendOverlay.Core/PinnedIds.cs
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FriendOverlay.Core
{
    /// <summary>
    /// Ordered set of pinned user ids, capped so the pinned section can never swallow the list.
    /// Pure data: persistence and the game are the caller's business.
    /// </summary>
    public sealed class PinnedIds
    {
        public const int Max = 20;

        private readonly List<ulong> _ids = new List<ulong>();

        public int Count => _ids.Count;

        public bool IsFull => _ids.Count >= Max;

        public IReadOnlyList<ulong> Items => _ids;

        public bool Contains(ulong id) => _ids.Contains(id);

        public bool Add(ulong id)
        {
            if (id == 0 || IsFull || _ids.Contains(id))
                return false;

            _ids.Add(id);
            return true;
        }

        public bool Remove(ulong id) => _ids.Remove(id);

        /// <summary>Returns false when nothing changed (pinning while full).</summary>
        public bool Toggle(ulong id, out bool nowPinned)
        {
            if (Remove(id))
            {
                nowPinned = false;
                return true;
            }

            nowPinned = Add(id);
            return nowPinned;
        }

        public static PinnedIds Parse(string? csv)
        {
            var set = new PinnedIds();
            if (string.IsNullOrWhiteSpace(csv))
                return set;

            foreach (var part in csv!.Split(','))
            {
                if (ulong.TryParse(part.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var id))
                    set.Add(id);
            }

            return set;
        }

        public string ToCsv()
        {
            var sb = new StringBuilder();
            for (var i = 0; i < _ids.Count; i++)
            {
                if (i > 0)
                    sb.Append(',');
                sb.Append(_ids[i].ToString(CultureInfo.InvariantCulture));
            }

            return sb.ToString();
        }
    }
}
```

- [ ] **Step 5: Add `FriendSectionKind.Pinned` and the pipeline overload**

`src/FriendOverlay.Core/FriendSection.cs` enum becomes:

```csharp
    public enum FriendSectionKind
    {
        OnlineJoinable = 0,
        OnlineBusy = 1,
        Offline = 2,
        Pinned = 3,
    }
```

In `src/FriendOverlay.Core/FriendQueryPipeline.cs` replace `GroupIntoSections` with:

```csharp
        public static IReadOnlyList<FriendSection> GroupIntoSections(IReadOnlyList<FriendRowVm> rows) =>
            GroupIntoSections(rows, null);

        /// <summary>
        /// Splits an already filtered/sorted view into availability sections, preserving order
        /// inside each section. Pinned rows form their own leading section and are removed from
        /// the availability sections. Empty sections are omitted.
        /// </summary>
        public static IReadOnlyList<FriendSection> GroupIntoSections(IReadOnlyList<FriendRowVm> rows, PinnedIds? pinned)
        {
            if (rows == null)
                throw new ArgumentNullException(nameof(rows));

            var pinnedRows = new List<FriendRowVm>();
            var joinable = new List<FriendRowVm>();
            var busy = new List<FriendRowVm>();
            var offline = new List<FriendRowVm>();

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null)
                    continue;

                if (pinned != null && pinned.Contains(row.UserId))
                    pinnedRows.Add(row);
                else if (!row.IsOnline)
                    offline.Add(row);
                else if (row.IsBusy)
                    busy.Add(row);
                else
                    joinable.Add(row);
            }

            var sections = new List<FriendSection>(4);
            if (pinnedRows.Count > 0)
                sections.Add(new FriendSection(FriendSectionKind.Pinned, pinnedRows));
            if (joinable.Count > 0)
                sections.Add(new FriendSection(FriendSectionKind.OnlineJoinable, joinable));
            if (busy.Count > 0)
                sections.Add(new FriendSection(FriendSectionKind.OnlineBusy, busy));
            if (offline.Count > 0)
                sections.Add(new FriendSection(FriendSectionKind.Offline, offline));

            return sections;
        }
```

- [ ] **Step 6: Run all tests**

Run: `dotnet test tests\FriendOverlay.Tests\FriendOverlay.Tests.csproj`
Expected: `Passed!` — all existing tests plus 8 `PinnedIdsTests` and 4 new section tests green.

- [ ] **Step 7: Link Core file, create `PinStore`**

Add to `src/FriendOverlay/FriendOverlay.csproj` first `<ItemGroup>`:

```xml
    <Compile Include="..\FriendOverlay.Core\PinnedIds.cs" Link="Core\PinnedIds.cs" />
```

Create `src/FriendOverlay/Data/PinStore.cs`:

```csharp
// src/FriendOverlay/Data/PinStore.cs
using System;
using FriendOverlay.Core;
using FriendOverlay.State;
using MelonLoader;

namespace FriendOverlay.Data
{
    /// <summary>
    /// Preference-backed pin set. Purely local: the game server never learns about pins. Every
    /// change persists immediately because a crash must not lose a pin the player just made.
    /// </summary>
    public static class PinStore
    {
        private static PinnedIds _pins = new PinnedIds();

        public static PinnedIds Pins => _pins;

        public static string Csv => _pins.ToCsv();

        public static bool IsFull => _pins.IsFull;

        public static void Load(string? csv)
        {
            _pins = PinnedIds.Parse(csv);
            MelonLogger.Msg("[FriendOverlay] pins loaded: " + _pins.Count);
        }

        public static bool Contains(ulong userId) => _pins.Contains(userId);

        /// <summary>Returns true when the set changed (false when pinning while full).</summary>
        public static bool Toggle(ulong userId)
        {
            if (!_pins.Toggle(userId, out _))
                return false;

            Persist();
            return true;
        }

        public static void Remove(ulong userId)
        {
            if (_pins.Remove(userId))
                Persist();
        }

        private static void Persist()
        {
            try
            {
                OverlaySession.PersistPreferences?.Invoke();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[FriendOverlay] pin persist: " + ex.Message);
            }
        }
    }
}
```

- [ ] **Step 8: Preferences in `FriendOverlayMod`**

Add fields:

```csharp
        private MelonPreferences_Entry<string>? _prefPinned;
        private MelonPreferences_Entry<bool>? _prefCollapsePinned;
```

In `LoadPreferences`, after `_prefCollapseOffline = ...` add:

```csharp
            _prefCollapsePinned = _prefs.CreateEntry("CollapsePinned", false, "Collapse the pinned section");
            _prefPinned = _prefs.CreateEntry("PinnedUserIds", string.Empty, "Comma separated pinned user ids (max 20, local only)");
```

After `ImguiFriendOverlay.CollapsedOffline = ...` add:

```csharp
            ImguiFriendOverlay.CollapsedPinned = _prefCollapsePinned.Value;
            PinStore.Load(_prefPinned.Value);
```

In `SavePreferences`, after `_prefCollapseOffline!.Value = ...` add:

```csharp
            _prefCollapsePinned!.Value = ImguiFriendOverlay.CollapsedPinned;
            _prefPinned!.Value = PinStore.Csv;
```

- [ ] **Step 9: Unpin on unfollow / blacklist**

In `src/FriendOverlay/Actions/FriendActions.cs`, `Unfollow` becomes:

```csharp
        public static void Unfollow(FriendRowVm row) => Safe(() =>
        {
            OverlaySession.Proxy?.RequestCancelFollow(row.UserId);
            PinStore.Remove(row.UserId);
            FriendOverlay.Data.FriendListService.ForceRefresh();
        }, "Unfollow");
```

In `Blacklist`, before `FriendOverlay.Data.FriendListService.ForceRefresh();` add:

```csharp
            PinStore.Remove(row.UserId);
```

- [ ] **Step 10: Section header for Pinned**

In `src/FriendOverlay/UI/Widgets/SectionHeader.cs`:

```csharp
        public static string TitleOf(FriendSectionKind kind) => kind switch
        {
            FriendSectionKind.Pinned => "置顶",
            FriendSectionKind.OnlineJoinable => "在线 · 可加入",
            FriendSectionKind.OnlineBusy => "在线 · 对战中",
            _ => "离线",
        };

        public static Color ColorOf(FriendSectionKind kind) => kind switch
        {
            FriendSectionKind.Pinned => Theme.Accent,
            FriendSectionKind.OnlineJoinable => Theme.StIdle,
            FriendSectionKind.OnlineBusy => Theme.StBattle,
            _ => Theme.StOffline,
        };
```

- [ ] **Step 11: RowCard menu with pin item**

In `src/FriendOverlay/UI/Widgets/RowCard.cs`:

Add `TogglePin = 8,` to `RowAction`. `RowContext` becomes:

```csharp
    public struct RowContext
    {
        public bool CanInvite;
        public bool InviteRecent;
        public bool IsPinned;
        public bool PinFull;
    }
```

Change `MenuWidth` to `Theme.S(150f)`. Replace `MenuHeightFor` and `DrawMenu`:

```csharp
        public static float MenuHeightFor(FriendRowVm row, RowContext ctx) => Theme.S(34f) * 3f + Theme.S(8f);

        /// <summary>Overflow menu, drawn after the list so it stacks above other rows.</summary>
        public static RowAction DrawMenu(Rect r, FriendRowVm row, RowContext ctx)
        {
            Gfx.Fill(r, Theme.Bg1);
            Gfx.Border(r, Theme.Line);

            var itemH = Theme.S(34f);
            var item = new Rect(r.x + Theme.S(4f), r.y + Theme.S(4f), r.width - Theme.S(8f), itemH);

            var pinLabel = ctx.IsPinned ? "取消置顶" : ctx.PinFull ? "置顶（已满 " + Core.PinnedIds.Max + "）" : "置顶";
            if (MenuItem(item, pinLabel, Theme.TextMain, ctx.IsPinned || !ctx.PinFull))
                return RowAction.TogglePin;
            item.y += itemH;

            if (MenuItem(item, "取消关注", Theme.TextMain, true))
                return RowAction.Unfollow;
            item.y += itemH;

            if (MenuItem(item, "加入黑名单", Theme.DangerHover, true))
                return RowAction.Blacklist;

            return RowAction.None;
        }
```

- [ ] **Step 12: Panel: collapse state, pinned grouping, toggle action**

In `src/FriendOverlay/UI/OverlayPanel.cs`:

Add property next to `CollapsedOffline`:

```csharp
        public static bool CollapsedPinned { get; set; }
```

`BuildContext` becomes:

```csharp
        private static RowContext BuildContext(FriendRowVm row) => new RowContext
        {
            CanInvite = InviteRules.CanInvite(Capabilities.Invite, _inRoom, row.IsOnline),
            InviteRecent = _inviteCooldown.IsActive(row.UserId, Time.unscaledTime),
            IsPinned = PinStore.Contains(row.UserId),
            PinFull = PinStore.IsFull,
        };
```

In `BuildItems` replace `var sections = FriendQueryPipeline.GroupIntoSections(view);` with:

```csharp
            var sections = FriendQueryPipeline.GroupIntoSections(view, PinStore.Pins);
```

Replace `IsCollapsed` and `ToggleCollapsed`:

```csharp
        private static bool IsCollapsed(FriendSectionKind kind) => kind switch
        {
            FriendSectionKind.Pinned => CollapsedPinned,
            FriendSectionKind.OnlineJoinable => CollapsedJoinable,
            FriendSectionKind.OnlineBusy => CollapsedBusy,
            _ => CollapsedOffline,
        };

        private static void ToggleCollapsed(FriendSectionKind kind)
        {
            InvalidateView();
            switch (kind)
            {
                case FriendSectionKind.Pinned:
                    CollapsedPinned = !CollapsedPinned;
                    break;
                case FriendSectionKind.OnlineJoinable:
                    CollapsedJoinable = !CollapsedJoinable;
                    break;
                case FriendSectionKind.OnlineBusy:
                    CollapsedBusy = !CollapsedBusy;
                    break;
                default:
                    CollapsedOffline = !CollapsedOffline;
                    break;
            }
        }
```

Add to `Invoke`:

```csharp
                case RowAction.TogglePin:
                    _menuRowId = 0;
                    if (PinStore.Toggle(row.UserId))
                        InvalidateView();
                    break;
```

- [ ] **Step 13: Build, deploy, verify in game**

Run: `dotnet build src\FriendOverlay\FriendOverlay.csproj -c Release -p:Deploy=true`
Expected: `Build succeeded.`

In game:
1. `⋯` on a row → menu shows `置顶 / 取消关注 / 加入黑名单`. Click `置顶` → row moves into a new `置顶` section at the top (accent colour) and disappears from its availability section.
2. `⋯` on the pinned row → `取消置顶` → row returns to its section.
3. Pin section header click collapses; reopen panel → collapse state kept.
4. Pin 20 rows → 21st row's menu shows `置顶（已满 20）` disabled.
5. Close panel, check `UserData\MelonPreferences.cfg` contains `PinnedUserIds = "..."` with the ids; restart game → pins survive.
6. Unfollow a pinned row → it is gone from `PinnedUserIds`.
7. Search / filter / sort still apply inside the pinned section (pinned rows follow the current sort).

- [ ] **Step 14: Commit**

```bash
git add src/FriendOverlay.Core/PinnedIds.cs src/FriendOverlay.Core/FriendSection.cs src/FriendOverlay.Core/FriendQueryPipeline.cs tests/FriendOverlay.Tests/PinnedIdsTests.cs tests/FriendOverlay.Tests/FriendSectionTests.cs src/FriendOverlay/FriendOverlay.csproj src/FriendOverlay/Data/PinStore.cs src/FriendOverlay/FriendOverlayMod.cs src/FriendOverlay/Actions/FriendActions.cs src/FriendOverlay/UI/Widgets/SectionHeader.cs src/FriendOverlay/UI/Widgets/RowCard.cs src/FriendOverlay/UI/OverlayPanel.cs
git commit -m "feat: local pin section (max 20) persisted in MelonPreferences"
```

---

### Task 4: 「关注我的人」 tab — FansListService, title tabs, 回关

**Files:**
- Create: `src/FriendOverlay/Data/FriendRowMapper.cs`
- Create: `src/FriendOverlay/Data/FansListService.cs`
- Modify: `src/FriendOverlay/Data/FriendListService.cs:202-288`
- Modify: `src/FriendOverlay/Hooks/FriendPanelHooks.cs`
- Modify: `src/FriendOverlay/FriendOverlayMod.cs:58-73`
- Modify: `src/FriendOverlay/Actions/FriendActions.cs`
- Modify: `src/FriendOverlay/UI/Widgets/RowCard.cs`
- Modify: `src/FriendOverlay/UI/OverlayPanel.cs`

**Interfaces:**
- Consumes: `Capabilities.Followers` (Task 1); `RowContext`, `BuildContext`, `PinStore` (Tasks 2–3); `FriendRowVm`, `FaceBlockPolicy`, `FriendStatusMapper`.
- Produces: `FriendRowMapper.Map(FriendBaseInfo, Il2CppSystem.Collections.Generic.Dictionary<ulong, FriendStatus>?) : FriendRowVm`; `FansListService.Available/Active/IsLoading/Snapshot/StatusText`, `OnPanelOpened()`, `OnPanelClosed()`, `Tick()`, `ForceRefresh()`, `OnResponse(ResponseLastFollower?)`; `FriendActions.FollowBack(FriendRowVm)`; `enum FriendListTab { Following, Followers }`; `RowContext.Tab`; `RowAction.FollowBack`; `ImguiFriendOverlay.Tab`.

- [ ] **Step 1: Extract `FriendRowMapper` (no behaviour change)**

Create `src/FriendOverlay/Data/FriendRowMapper.cs`:

```csharp
// src/FriendOverlay/Data/FriendRowMapper.cs
using FriendOverlay.Core;
using Il2CppProtos.Friend;

namespace FriendOverlay.Data
{
    /// <summary>One place that turns a game FriendBaseInfo into a row, shared by both lists.</summary>
    public static class FriendRowMapper
    {
        public static FriendRowVm Map(FriendBaseInfo info, Il2CppSystem.Collections.Generic.Dictionary<ulong, FriendStatus>? stateDic)
        {
            var state = info.State;
            try
            {
                if (stateDic != null && stateDic.ContainsKey(info.Userid))
                {
                    var st = stateDic[info.Userid];
                    if (st != null)
                        state = st.State;
                }
            }
            catch
            {
                // keep info.State
            }

            var name = string.Empty;
            var face = string.Empty;
            try
            {
                if (info.RiskInfo != null)
                {
                    name = info.RiskInfo.Name ?? string.Empty;
                    face = info.RiskInfo.FaceUrl ?? string.Empty;
                }
            }
            catch
            {
                // ignore
            }

            return new FriendRowVm
            {
                UserId = info.Userid,
                Name = name,
                FaceUrl = face,
                FaceId = info.FaceId,
                FaceBlocked = FaceBlockPolicy.IsBlocked(info),
                RankPoint = info.RankPoint,
                ForecastPoint = info.ForecastPoint,
                State = state,
                IsMutual = info.IsMutual,
                IsOnline = FriendStatusMapper.IsOnline(state),
                IsBusy = FriendStatusMapper.IsBusy(state),
                Platform = info.Platform,
                StatusLabel = FriendStatusMapper.ToLabel(state),
                StatusKind = FriendStatusMapper.ToKind(state),
            };
        }
    }
}
```

In `src/FriendOverlay/Data/FriendListService.cs` delete the private `MapRow` method and change the call in `RefreshSnapshot` to:

```csharp
                _snapshot.Add(FriendRowMapper.Map(info, stateDic));
```

Run: `dotnet build src\FriendOverlay\FriendOverlay.csproj -c Release` → `Build succeeded.`

- [ ] **Step 2: Create `FansListService`**

```csharp
// src/FriendOverlay/Data/FansListService.cs
using System;
using System.Collections.Generic;
using FriendOverlay.Compat;
using FriendOverlay.Core;
using FriendOverlay.State;
using Il2CppProtos.Friend;
using MelonLoader;
using UnityEngine;
using Il2CppListUlong = Il2CppSystem.Collections.Generic.List<ulong>;

namespace FriendOverlay.Data
{
    /// <summary>
    /// 「关注我的人」: players who follow the local player. Source is FriendProxy.followerBaseInfoList,
    /// refreshed through RequestLastFollower(). RequestFollowerStatus is deliberately unused — its
    /// reply (ResponseFollowStatus.Following) pages the *following* status, not this list.
    /// Only ticks while its tab is visible so the extra RequestOnline traffic stays opt-in.
    /// </summary>
    public static class FansListService
    {
        private static readonly List<FriendRowVm> _snapshot = new List<FriendRowVm>();
        private static readonly List<FriendBaseInfo> _captured = new List<FriendBaseInfo>();

        private static bool _opened;
        private static bool _requested;
        private static float _nextSnapshotAt;
        private static float _nextOnlineAt;

        public static IReadOnlyList<FriendRowVm> Snapshot => _snapshot;

        public static bool Available => Capabilities.Followers;

        /// <summary>Set by the panel when the tab is showing; gates Tick.</summary>
        public static bool Active { get; set; }

        public static bool IsLoading { get; private set; }

        public static string StatusText { get; private set; } = string.Empty;

        public static void OnPanelOpened()
        {
            if (!Available)
                return;

            _opened = true;
            _requested = false;
            _nextSnapshotAt = 0f;
            _nextOnlineAt = 0f;
        }

        public static void OnPanelClosed()
        {
            _opened = false;
            Active = false;
            IsLoading = false;
            _requested = false;
            _snapshot.Clear();
            _captured.Clear();
            StatusText = string.Empty;
        }

        public static void Tick()
        {
            if (!_opened || !Active || OverlaySession.Degraded || OverlaySession.Proxy == null)
                return;

            try
            {
                if (!_requested)
                {
                    _requested = true;
                    RequestList();
                }

                if (Time.unscaledTime >= _nextSnapshotAt)
                {
                    RefreshSnapshot();
                    _nextSnapshotAt = Time.unscaledTime + 0.75f;
                }

                if (Time.unscaledTime >= _nextOnlineAt)
                {
                    RequestOnlineBatch();
                    _nextOnlineAt = Time.unscaledTime + 2.5f;
                }

                UI.GameAssets.RequestMissingAvatars(_snapshot);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[FriendOverlay] FansListService.Tick: " + ex.Message);
            }
        }

        public static void ForceRefresh()
        {
            if (!_opened || !Available)
                return;

            RequestList();
            RefreshSnapshot();
            RequestOnlineBatch();
        }

        /// <summary>
        /// Fed by the OnResponseLastFollower postfix. Keeps the tab working even if the proxy
        /// stores the reply somewhere we do not read; the proxy list stays the primary source.
        /// </summary>
        public static void OnResponse(ResponseLastFollower? msg)
        {
            if (msg == null)
                return;

            _captured.Clear();
            var followers = msg.Follower;
            var count = 0;
            if (followers != null)
            {
                for (var i = 0; i < followers.Count; i++)
                {
                    FriendBaseInfo? info = null;
                    try { info = followers[i]; } catch { continue; }
                    if (info != null)
                        _captured.Add(info);
                }

                count = followers.Count;
            }

            var newCount = 0;
            try { newCount = msg.New?.Count ?? 0; } catch { newCount = 0; }

            IsLoading = false;
            _nextSnapshotAt = 0f;
            MelonLogger.Msg("[FriendOverlay] followers response count=" + count + " new=" + newCount);
        }

        private static void RequestList()
        {
            var proxy = OverlaySession.Proxy;
            if (proxy == null)
                return;

            try
            {
                proxy.RequestLastFollower();
                IsLoading = true;
                if (_snapshot.Count == 0)
                    StatusText = "同步中";
            }
            catch (Exception ex)
            {
                IsLoading = false;
                MelonLogger.Warning("[FriendOverlay] RequestLastFollower: " + ex.Message);
            }
        }

        private static void RefreshSnapshot()
        {
            var proxy = OverlaySession.Proxy;
            if (proxy == null)
                return;

            Il2CppSystem.Collections.Generic.List<FriendBaseInfo>? list = null;
            try { list = proxy.followerBaseInfoList; } catch { list = null; }

            Il2CppSystem.Collections.Generic.Dictionary<ulong, FriendStatus>? stateDic = null;
            try { stateDic = proxy.friendBasePlayerStateDic; } catch { stateDic = null; }

            FaceBlockPolicy.BeginSnapshot();
            _snapshot.Clear();
            var seen = new HashSet<ulong>();

            if (list != null)
            {
                for (var i = 0; i < list.Count; i++)
                {
                    FriendBaseInfo? info = null;
                    try { info = list[i]; } catch { continue; }
                    if (info == null || !seen.Add(info.Userid))
                        continue;

                    _snapshot.Add(FriendRowMapper.Map(info, stateDic));
                }
            }

            if (_snapshot.Count == 0)
            {
                foreach (var info in _captured)
                {
                    if (seen.Add(info.Userid))
                        _snapshot.Add(FriendRowMapper.Map(info, stateDic));
                }
            }

            StatusText = IsLoading && _snapshot.Count == 0
                ? "同步中"
                : "关注我的人 " + _snapshot.Count;
        }

        private static void RequestOnlineBatch()
        {
            var proxy = OverlaySession.Proxy;
            if (proxy == null || _snapshot.Count == 0)
                return;

            try
            {
                var ids = new Il2CppListUlong();
                foreach (var row in _snapshot)
                    ids.Add(row.UserId);
                proxy.RequestOnline(ids);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[FriendOverlay] followers RequestOnline: " + ex.Message);
            }
        }
    }
}
```

- [ ] **Step 3: Hooks — open/close + response postfix**

In `src/FriendOverlay/Hooks/FriendPanelHooks.cs`:

Add `using FriendOverlay.Compat;` and `using Il2CppProtos.Friend;`. At the end of `Apply(...)` add:

```csharp
            if (Capabilities.Followers)
            {
                var lastFollower = AccessTools.Method(typeof(FriendProxy), nameof(FriendProxy.OnResponseLastFollower));
                if (lastFollower != null)
                    harmony.Patch(lastFollower, postfix: new HarmonyMethod(typeof(FriendPanelHooks), nameof(LastFollowerPostfix)));
            }
```

Add the postfix method (parameter name `follower` must match the game's signature `OnResponseLastFollower(object sender, ResponseLastFollower follower)`):

```csharp
        public static void LastFollowerPostfix(ResponseLastFollower follower)
        {
            try
            {
                FansListService.OnResponse(follower);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[FriendOverlay] LastFollowerPostfix: " + ex.Message);
            }
        }
```

Add `FansListService.OnPanelOpened();` directly after **each** `FriendListService.OnPanelOpened();` (2 sites) and `FansListService.OnPanelClosed();` directly after **each** `FriendListService.OnPanelClosed();` (4 sites).

In `src/FriendOverlay/FriendOverlayMod.cs` `OnUpdate`, after `FriendListService.Tick();` add:

```csharp
            FansListService.Tick();
```

- [ ] **Step 4: Actions — 回关 and cross-list refresh**

In `src/FriendOverlay/Actions/FriendActions.cs` add after `Invite`:

```csharp
        /// <summary>Follow back someone in 关注我的人. Mirrors the native BeFollowCellNode follow button.</summary>
        public static void FollowBack(FriendRowVm row) => Safe(() =>
        {
            OverlaySession.Proxy?.RequestFollowUser(row.UserId);
            FansListService.ForceRefresh();
            FriendListService.ForceRefresh();
        }, "FollowBack");
```

In `Unfollow` and `Blacklist`, immediately after each `FriendOverlay.Data.FriendListService.ForceRefresh();` add:

```csharp
            FansListService.ForceRefresh();
```

- [ ] **Step 5: RowCard — tab awareness, 回关, tab-specific menu**

In `src/FriendOverlay/UI/Widgets/RowCard.cs`:

Add `FollowBack = 9,` to `RowAction`. Add above `RowContext`:

```csharp
    public enum FriendListTab
    {
        Following = 0,
        Followers = 1,
    }
```

`RowContext` becomes:

```csharp
    public struct RowContext
    {
        public FriendListTab Tab;
        public bool CanInvite;
        public bool InviteRecent;
        public bool IsPinned;
        public bool PinFull;
    }
```

Replace `MenuHeightFor`, `DrawMenu`, `ActionsWidth`, `DrawActions`:

```csharp
        private static float FollowBackWidth => Theme.S(72f);

        private static int MenuItemCount(FriendRowVm row, RowContext ctx) =>
            ctx.Tab == FriendListTab.Followers ? (row.IsMutual ? 2 : 1) : 3;

        public static float MenuHeightFor(FriendRowVm row, RowContext ctx) =>
            Theme.S(34f) * MenuItemCount(row, ctx) + Theme.S(8f);

        /// <summary>
        /// Overflow menu, drawn after the list so it stacks above other rows. 关注我的人 never offers
        /// pin, and only offers unfollow for people the player actually follows (mutual).
        /// </summary>
        public static RowAction DrawMenu(Rect r, FriendRowVm row, RowContext ctx)
        {
            Gfx.Fill(r, Theme.Bg1);
            Gfx.Border(r, Theme.Line);

            var itemH = Theme.S(34f);
            var item = new Rect(r.x + Theme.S(4f), r.y + Theme.S(4f), r.width - Theme.S(8f), itemH);

            if (ctx.Tab == FriendListTab.Following)
            {
                var pinLabel = ctx.IsPinned ? "取消置顶" : ctx.PinFull ? "置顶（已满 " + Core.PinnedIds.Max + "）" : "置顶";
                if (MenuItem(item, pinLabel, Theme.TextMain, ctx.IsPinned || !ctx.PinFull))
                    return RowAction.TogglePin;
                item.y += itemH;
            }

            if (ctx.Tab == FriendListTab.Following || row.IsMutual)
            {
                if (MenuItem(item, "取消关注", Theme.TextMain, true))
                    return RowAction.Unfollow;
                item.y += itemH;
            }

            if (MenuItem(item, "加入黑名单", Theme.DangerHover, true))
                return RowAction.Blacklist;

            return RowAction.None;
        }

        private static float ActionsWidth(RowContext ctx)
        {
            var w = Theme.S(52f);
            var gap = Theme.S(5f);
            var width = w * 4f + MoreWidth + gap * 4f;
            if (ctx.Tab == FriendListTab.Followers)
                width += FollowBackWidth + gap;
            return width;
        }

        private static RowAction DrawActions(Rect r, FriendRowVm row, RowContext ctx, bool menuOpen)
        {
            var action = RowAction.None;
            var w = Theme.S(52f);
            var gap = Theme.S(5f);
            var x = r.x;

            if (ctx.Tab == FriendListTab.Followers)
            {
                var followR = new Rect(x, r.y, FollowBackWidth, r.height);
                if (row.IsMutual)
                    Button(followR, "⇌ 已互关", Theme.Chip, false, false);
                else if (Button(followR, "回关", Theme.Chip, true, true))
                    action = RowAction.FollowBack;
                x += FollowBackWidth + gap;
            }

            var canJoin = row.IsOnline && !row.IsBusy;
            var canWatch = row.IsOnline && row.IsBusy;

            if (Button(new Rect(x, r.y, w, r.height), "加入", Theme.Chip, canJoin, ctx.Tab == FriendListTab.Following))
                action = RowAction.Join;
            x += w + gap;

            if (Button(new Rect(x, r.y, w, r.height), "观战", Theme.Chip, canWatch, false))
                action = RowAction.Watch;
            x += w + gap;

            if (Button(new Rect(x, r.y, w, r.height), "私聊", Theme.Chip, true, false))
                action = RowAction.Chat;
            x += w + gap;

            // Disabled rather than hidden so the player learns inviting needs a custom room.
            var inviteLabel = ctx.InviteRecent ? "已邀请" : "邀请";
            if (Button(new Rect(x, r.y, w, r.height), inviteLabel, Theme.Chip, ctx.CanInvite && !ctx.InviteRecent, false))
                action = RowAction.Invite;
            x += w + gap;

            var moreR = new Rect(x, r.y, MoreWidth, r.height);
            if (Button(moreR, "⋯", menuOpen ? Theme.ChipHover : Theme.Chip, true, false))
                action = RowAction.ToggleMenu;

            return action;
        }
```

- [ ] **Step 6: Panel — title tabs and source switching**

In `src/FriendOverlay/UI/OverlayPanel.cs`:

Add fields:

```csharp
        private static Rect _tabStrip;

        public static FriendListTab Tab { get; private set; } = FriendListTab.Following;

        private static IReadOnlyList<FriendRowVm> Source =>
            Tab == FriendListTab.Followers ? FansListService.Snapshot : FriendListService.Snapshot;
```

Add helpers after `BuildContext`:

```csharp
        private static void SwitchTab(FriendListTab tab)
        {
            if (Tab == tab)
                return;

            Tab = tab;
            _menuRowId = 0;
            _scroll.ScrollTo(0f);
            InvalidateView();
            FansListService.Active = tab == FriendListTab.Followers;
        }

        private static bool TitleTab(Rect r, string label, bool active)
        {
            var hovered = Gfx.Hover(r);
            var cut = Theme.S(8f);
            Gfx.Chamfer(r, active ? Theme.TitleBarHi : hovered ? Theme.ChipHover : Theme.TitleBar, cut);
            Gfx.ChamferBorder(r, active ? Theme.Accent : Theme.Line, cut);
            if (active)
                Gfx.Fill(new Rect(r.x + cut, r.yMax - Theme.S(2f), r.width - cut * 2f, Theme.S(2f)), Theme.Accent);
            Gfx.Text(r, label, active ? Theme.TextHi : hovered ? Theme.TextMain : Theme.TextMuted, Theme.Tab);
            return Gfx.Hit(r);
        }

        private static void RefreshActive()
        {
            if (Tab == FriendListTab.Followers)
                FansListService.ForceRefresh();
            else
                FriendListService.ForceRefresh();
        }
```

`BuildContext` becomes:

```csharp
        private static RowContext BuildContext(FriendRowVm row) => new RowContext
        {
            Tab = Tab,
            CanInvite = InviteRules.CanInvite(Capabilities.Invite, _inRoom, row.IsOnline),
            InviteRecent = _inviteCooldown.IsActive(row.UserId, Time.unscaledTime),
            IsPinned = Tab == FriendListTab.Following && PinStore.Contains(row.UserId),
            PinFull = PinStore.IsFull,
        };
```

In `DrawTitleBar`, replace everything from `var tab = new Rect(markX + Theme.S(20f), ...` through the `"已关注 " + Math.Max(total, shown)` `Gfx.Text(...)` call with:

```csharp
            var x = markX + Theme.S(20f);
            var tabY = bar.y + Theme.S(8f);
            var tabH = bar.height - Theme.S(10f);
            var followingW = Theme.S(96f);
            var followersW = Theme.S(132f);
            var tabGap = Theme.S(6f);

            if (TitleTab(new Rect(x, tabY, followingW, tabH), "关注", Tab == FriendListTab.Following))
                SwitchTab(FriendListTab.Following);
            x += followingW + tabGap;

            if (FansListService.Available)
            {
                if (TitleTab(new Rect(x, tabY, followersW, tabH), "关注我的人", Tab == FriendListTab.Followers))
                    SwitchTab(FriendListTab.Followers);
                x += followersW + tabGap;
            }

            // Recorded so the chrome drag never steals the click that lands on a tab.
            _tabStrip = new Rect(markX + Theme.S(20f), tabY, x - markX - Theme.S(20f), tabH);

            var countText = Tab == FriendListTab.Followers
                ? "共 " + Math.Max(FansListService.Snapshot.Count, shown)
                : "已关注 " + Math.Max(FriendListService.TotalFollowCount, shown);
            Gfx.Text(new Rect(x + Theme.S(8f), bar.y, Theme.S(200f), bar.height), countText, Theme.TextMuted, Theme.Meta);

            var btnW = Theme.S(76f);
```

(keep the existing `var btnH = ...` and button code below; delete the old `var btnW = Theme.S(76f);` line so it is not declared twice). Change the refresh button to call `RefreshActive();`.

In `CaptureChromeDrag`, before the `var titleH = Theme.S(52f);` line add:

```csharp
            if (_tabStrip.Contains(e.mousePosition))
                return;
```

In `UpdateInput`, change `FriendListService.ForceRefresh();` (the `R` key) to `RefreshActive();`.

In `DrawWindow`, change `var src = FriendListService.Snapshot;` to `var src = Source;`.

In `DrawList`, replace the empty-list message:

```csharp
                var msg = Tab == FriendListTab.Followers
                    ? (FansListService.IsLoading || totalLoaded == 0 ? "正在拉取关注我的人…" : "没有符合条件的玩家")
                    : (totalLoaded == 0 ? "正在拉取关注列表…" : "没有符合条件的好友");
```

In `DrawStatusBar`, replace the first two statements and the `status` line:

```csharp
            var paging = Tab == FriendListTab.Followers ? FansListService.IsLoading : FriendListService.IsPaging;
            var color = paging ? Theme.StWaiting : Theme.StIdle;
            var alpha = paging ? 0.35f + 0.65f * Anim.Pulse(1.2f) : 1f;
```

```csharp
            var text = Tab == FriendListTab.Followers ? FansListService.StatusText : FriendListService.StatusText;
            var status = string.IsNullOrEmpty(text) ? "准备中" : text;
```

In `BuildItems`:

```csharp
            var sections = FriendQueryPipeline.GroupIntoSections(view, Tab == FriendListTab.Following ? PinStore.Pins : null);
```

In `FindRow`, replace `var snapshot = FriendListService.Snapshot;` with `var snapshot = Source;`.

In `Invoke`, add:

```csharp
                case RowAction.FollowBack:
                    FriendActions.FollowBack(row);
                    break;
```

In `OnSessionEnd`, after `_inviteCooldown.Clear();` add:

```csharp
            Tab = FriendListTab.Following;
            FansListService.Active = false;
```

- [ ] **Step 7: Build, deploy, verify**

Run: `dotnet build src\FriendOverlay\FriendOverlay.csproj -c Release -p:Deploy=true`
Expected: `Build succeeded.`

In game:
1. Title bar shows「关注」「关注我的人」. Dragging the title outside the tabs still moves the window; clicking a tab switches without dragging.
2. Click「关注我的人」→ log `[FriendOverlay] followers response count=N new=M`; status bar `关注我的人 N`; rows grouped `在线 · 可加入 / 在线 · 对战中 / 离线`; no `置顶` section even if a follower is pinned on the other tab.
3. `F8` → native panel → native 关注 tab (被关注) shows the same names/count.
4. Hover a non-mutual follower: `回关 | 加入 | 观战 | 私聊 | 邀请 | ⋯`. Click `回关` → after refresh the button reads `⇌ 已互关` disabled, and the player appears in「关注」.
5. `⋯` on a mutual follower → `取消关注 / 加入黑名单`; on a non-mutual → only `加入黑名单`. No `置顶` in either.
6. Search/filter/sort work on the tab; `R` refreshes the active tab.
7. Close and reopen: panel opens on「关注」.
8. Log has no `FansListService.Tick` warnings and no `DrawWindow failed`.

- [ ] **Step 8: Commit**

```bash
git add src/FriendOverlay/Data/FriendRowMapper.cs src/FriendOverlay/Data/FansListService.cs src/FriendOverlay/Data/FriendListService.cs src/FriendOverlay/Hooks/FriendPanelHooks.cs src/FriendOverlay/FriendOverlayMod.cs src/FriendOverlay/Actions/FriendActions.cs src/FriendOverlay/UI/Widgets/RowCard.cs src/FriendOverlay/UI/OverlayPanel.cs
git commit -m "feat: 关注我的人 tab with follow-back, sections and tab-aware row actions"
```

---

### Task 5: Game-faithful portrait + avatar frame

**Files:**
- Create: `src/FriendOverlay.Core/PortraitPlan.cs`
- Create: `tests/FriendOverlay.Tests/PortraitPlannerTests.cs`
- Create: `src/FriendOverlay/Data/PortraitResolver.cs`
- Create: `src/FriendOverlay/UI/GameSpriteResolver.cs`
- Create: `src/FriendOverlay/UI/SharedImageCache.cs`
- Delete: `src/FriendOverlay/UI/FaceIconResolver.cs`
- Modify: `src/FriendOverlay.Core/FriendRowVm.cs`
- Modify: `src/FriendOverlay/FriendOverlay.csproj`
- Modify: `src/FriendOverlay/Data/FriendRowMapper.cs`
- Modify: `src/FriendOverlay/UI/AvatarLoader.cs`
- Modify: `src/FriendOverlay/UI/GameAssets.cs:54-210`
- Modify: `src/FriendOverlay/UI/Widgets/RowCard.cs` (`DrawAvatar`)
- Modify: `src/FriendOverlay/State/OverlaySession.cs:113-120`

**Interfaces:**
- Consumes: `Capabilities.Portrait` (Task 1); `FriendRowMapper.Map` (Task 4); `FaceBlockPolicy.IsBlocked`; `AvatarCache`; `RowCard.ForceLetters`.
- Produces: `enum PortraitKind { Letter, Photo, Official, Blocked }`; `PortraitPlan { Kind, ImageRef, FrameRef }`; `PortraitPlanner.Decide(bool blocked, string? portrait, string? avatarUrl, string? outlineUrl) : PortraitPlan`; `FriendRowVm.Portrait/PortraitRef/FrameRef`; `PortraitResolver.Resolve(FriendBaseInfo, bool blocked) : PortraitPlan`, `PortraitResolver.Reset()`; `GameSpriteResolver.TryGetLocalSprite(string imageRef, out Sprite? sprite) : bool`, `GameSpriteResolver.ToUrl(string imageRef) : string`, `GameSpriteResolver.Reset()`; `SharedImageCache.Get(string?) : SharedImage?`, `SharedImageCache.Request(string?) : bool`, `SharedImageCache.Clear()`; `AvatarLoader.RequestShared(string rawUrl, Action<Texture2D?> onDone) : bool`.

- [ ] **Step 1: Write the failing planner tests**

```csharp
// tests/FriendOverlay.Tests/PortraitPlannerTests.cs
using FriendOverlay.Core;
using Xunit;

namespace FriendOverlay.Tests;

public class PortraitPlannerTests
{
    [Fact]
    public void Decide_OfficialAvatarWinsOverPhoto()
    {
        var plan = PortraitPlanner.Decide(false, "https://cdn/photo.jpg", "ui/avatar_07", "ui/frame_02");

        Assert.Equal(PortraitKind.Official, plan.Kind);
        Assert.Equal("ui/avatar_07", plan.ImageRef);
        Assert.Equal("ui/frame_02", plan.FrameRef);
    }

    [Fact]
    public void Decide_PhotoWhenNoAvatar()
    {
        var plan = PortraitPlanner.Decide(false, "https://cdn/photo.jpg", "", null);

        Assert.Equal(PortraitKind.Photo, plan.Kind);
        Assert.Equal("https://cdn/photo.jpg", plan.ImageRef);
        Assert.Equal(string.Empty, plan.FrameRef);
    }

    [Fact]
    public void Decide_LetterWhenNothingToShow()
    {
        var plan = PortraitPlanner.Decide(false, "", null, "ui/frame_02");

        Assert.Equal(PortraitKind.Letter, plan.Kind);
        Assert.Equal(string.Empty, plan.ImageRef);
        Assert.Equal("ui/frame_02", plan.FrameRef);
    }

    [Fact]
    public void Decide_BlockedPhotoUsesGameSubstitutedPortrait()
    {
        // The game already swapped the URL for its dark placeholder; we show exactly that.
        var plan = PortraitPlanner.Decide(true, "ui/dark_avatar", null, null);

        Assert.Equal(PortraitKind.Blocked, plan.Kind);
        Assert.Equal("ui/dark_avatar", plan.ImageRef);
    }

    [Fact]
    public void Decide_BlockedWithoutPortraitFallsBackToLetter()
    {
        var plan = PortraitPlanner.Decide(true, "", null, null);
        Assert.Equal(PortraitKind.Letter, plan.Kind);
    }

    [Fact]
    public void Decide_BlockedDoesNotHideOfficialAvatar()
    {
        var plan = PortraitPlanner.Decide(true, "ui/dark_avatar", "ui/avatar_07", null);

        Assert.Equal(PortraitKind.Official, plan.Kind);
        Assert.Equal("ui/avatar_07", plan.ImageRef);
    }

    [Fact]
    public void Decide_NullInputsAreSafe()
    {
        var plan = PortraitPlanner.Decide(false, null, null, null);

        Assert.Equal(PortraitKind.Letter, plan.Kind);
        Assert.Equal(string.Empty, plan.ImageRef);
        Assert.Equal(string.Empty, plan.FrameRef);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests\FriendOverlay.Tests\FriendOverlay.Tests.csproj --filter "FullyQualifiedName~PortraitPlannerTests"`
Expected: build error `'PortraitPlanner' could not be found`.

- [ ] **Step 3: Implement `PortraitPlan.cs` and extend `FriendRowVm`**

```csharp
// src/FriendOverlay.Core/PortraitPlan.cs
namespace FriendOverlay.Core
{
    public enum PortraitKind
    {
        /// <summary>Nothing to load; draw the initial only.</summary>
        Letter = 0,

        /// <summary>Custom photo URL; per-user download.</summary>
        Photo = 1,

        /// <summary>Official in-game avatar asset; shared across players.</summary>
        Official = 2,

        /// <summary>Game hides this face; show the placeholder the game substituted.</summary>
        Blocked = 3,
    }

    public sealed class PortraitPlan
    {
        public PortraitPlan(PortraitKind kind, string imageRef, string frameRef)
        {
            Kind = kind;
            ImageRef = imageRef ?? string.Empty;
            FrameRef = frameRef ?? string.Empty;
        }

        public PortraitKind Kind { get; }

        /// <summary>URL or sprite path of the main image; empty for Letter.</summary>
        public string ImageRef { get; }

        /// <summary>URL or sprite path of the avatar frame; empty when the player has none.</summary>
        public string FrameRef { get; }
    }

    /// <summary>
    /// Mirrors GRImage.SetPlayerPortrait(portraitURL, outline, avatar): the avatar layer sits above
    /// the photo layer, so a non-empty official avatar is what other players actually see.
    /// Moderation only touches the photo layer, and the game has already swapped that URL for its
    /// placeholder before we read it, so a blocked row simply shows the substituted portrait.
    /// </summary>
    public static class PortraitPlanner
    {
        public static PortraitPlan Decide(bool blocked, string? portrait, string? avatarUrl, string? outlineUrl)
        {
            var frame = outlineUrl ?? string.Empty;

            if (!string.IsNullOrEmpty(avatarUrl))
                return new PortraitPlan(PortraitKind.Official, avatarUrl!, frame);

            var photo = portrait ?? string.Empty;
            if (photo.Length == 0)
                return new PortraitPlan(PortraitKind.Letter, string.Empty, frame);

            return new PortraitPlan(blocked ? PortraitKind.Blocked : PortraitKind.Photo, photo, frame);
        }
    }
}
```

`src/FriendOverlay.Core/FriendRowVm.cs` becomes:

```csharp
namespace FriendOverlay.Core
{
    public sealed class FriendRowVm
    {
        public ulong UserId { get; set; }
        public string Name { get; set; } = string.Empty;

        /// <summary>Raw custom-photo URL from the proto. Kept for blacklist config parity.</summary>
        public string FaceUrl { get; set; } = string.Empty;

        /// <summary>The game itself hides this friend's face.</summary>
        public bool FaceBlocked { get; set; }

        /// <summary>What the game would draw for this player, as decided by <see cref="PortraitPlanner"/>.</summary>
        public PortraitKind Portrait { get; set; } = PortraitKind.Letter;

        /// <summary>URL or sprite path for <see cref="Portrait"/>; empty for Letter.</summary>
        public string PortraitRef { get; set; } = string.Empty;

        /// <summary>URL or sprite path of the avatar frame; empty when none.</summary>
        public string FrameRef { get; set; } = string.Empty;

        public int RankPoint { get; set; }
        public int ForecastPoint { get; set; }
        public int State { get; set; }
        public bool IsMutual { get; set; }
        public bool IsOnline { get; set; }
        public bool IsBusy { get; set; }
        public int Platform { get; set; }
        public string StatusLabel { get; set; } = string.Empty;
        public FriendStatusKind StatusKind { get; set; } = FriendStatusKind.Offline;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests\FriendOverlay.Tests\FriendOverlay.Tests.csproj`
Expected: `Passed!` including 7 `PortraitPlannerTests`.

- [ ] **Step 5: Link Core file**

Add to `src/FriendOverlay/FriendOverlay.csproj` first `<ItemGroup>`:

```xml
    <Compile Include="..\FriendOverlay.Core\PortraitPlan.cs" Link="Core\PortraitPlan.cs" />
```

- [ ] **Step 6: Create `PortraitResolver`**

```csharp
// src/FriendOverlay/Data/PortraitResolver.cs
using System;
using System.Collections.Generic;
using FriendOverlay.Compat;
using FriendOverlay.Core;
using Il2CppGameRiver;
using Il2CppProtos.Friend;
using MelonLoader;
using GameRiskInfo = Il2CppGameRiver.PlayerRiskInfo;

namespace FriendOverlay.Data
{
    /// <summary>
    /// Asks the game which image it would show for a friend. Rebuilds the same PlayerRiskInfo the
    /// native cell builds and reads portrait / avatar / outline from it, then lets
    /// <see cref="PortraitPlanner"/> pick. Cached per uid on the inputs the answer depends on.
    /// Binding failures disable the resolver for the session and fall back to the photo-only route.
    /// </summary>
    public static class PortraitResolver
    {
        private readonly struct Key
        {
            private readonly string _faceUrl;
            private readonly int _faceId;
            private readonly int _faceBorder;
            private readonly bool _blocked;

            public Key(string faceUrl, int faceId, int faceBorder, bool blocked)
            {
                _faceUrl = faceUrl;
                _faceId = faceId;
                _faceBorder = faceBorder;
                _blocked = blocked;
            }

            public bool Matches(in Key other) =>
                _faceId == other._faceId &&
                _faceBorder == other._faceBorder &&
                _blocked == other._blocked &&
                string.Equals(_faceUrl, other._faceUrl, StringComparison.Ordinal);
        }

        private sealed class Cached
        {
            public Key Key;
            public PortraitPlan Plan = new PortraitPlan(PortraitKind.Letter, string.Empty, string.Empty);
        }

        private static readonly Dictionary<ulong, Cached> _cache = new Dictionary<ulong, Cached>();
        private static bool _disabled;
        private static int _samplesLogged;
        private static bool _loggedError;

        public static PortraitPlan Resolve(FriendBaseInfo info, bool blocked)
        {
            if (info == null)
                return new PortraitPlan(PortraitKind.Letter, string.Empty, string.Empty);

            if (!Capabilities.Portrait || _disabled)
                return Legacy(info, blocked);

            ulong uid;
            Il2CppProtos.Common.PlayerRiskInfo? risk;
            int faceId;
            int faceBorder;
            try
            {
                uid = info.Userid;
                risk = info.RiskInfo;
                faceId = info.FaceId;
                faceBorder = info.FaceBorder;
            }
            catch
            {
                return Legacy(info, blocked);
            }

            if (risk == null)
                return Legacy(info, blocked);

            var faceUrl = string.Empty;
            try { faceUrl = risk.FaceUrl ?? string.Empty; } catch { faceUrl = string.Empty; }

            var key = new Key(faceUrl, faceId, faceBorder, blocked);
            if (_cache.TryGetValue(uid, out var cached) && cached.Key.Matches(key))
                return cached.Plan;

            try
            {
                var game = new GameRiskInfo(
                    risk.Name ?? string.Empty,
                    faceUrl,
                    risk.BlockName,
                    risk.BlockFace,
                    faceId,
                    faceBorder);

                var portraitInfo = game.GetPortraitInfo();
                var portrait = game.GetPortrait();
                var avatar = portraitInfo != null ? portraitInfo.GetAvatarURL() : game.GetAvatarURL();
                var outline = portraitInfo != null ? portraitInfo.GetAvatarOutLineURL() : game.GetAvatarOutLineURL();

                var plan = PortraitPlanner.Decide(blocked, portrait, avatar, outline);

                if (_samplesLogged < 3)
                {
                    _samplesLogged++;
                    MelonLogger.Msg(
                        "[FriendOverlay] portrait sample uid=" + uid +
                        " faceId=" + faceId + " faceBorder=" + faceBorder + " blocked=" + blocked +
                        " -> " + plan.Kind + " image=" + plan.ImageRef + " frame=" + plan.FrameRef);
                }

                _cache[uid] = new Cached { Key = key, Plan = plan };
                return plan;
            }
            catch (Exception ex) when (IsInfrastructureFailure(ex))
            {
                _disabled = true;
                _cache.Clear();
                MelonLogger.Warning("[FriendOverlay] portrait resolver disabled, photo-only route: " + ex.Message);
                return Legacy(info, blocked);
            }
            catch (Exception ex)
            {
                if (!_loggedError)
                {
                    _loggedError = true;
                    MelonLogger.Warning("[FriendOverlay] portrait resolve failed for one row: " + ex.Message);
                }

                return Legacy(info, blocked);
            }
        }

        /// <summary>
        /// Pre-0.3.4 behaviour: custom photo URL, else the FaceId prop icon, no frame. Blocked rows
        /// stay letters because without the game's substitution we would show the real photo.
        /// </summary>
        private static PortraitPlan Legacy(FriendBaseInfo info, bool blocked)
        {
            if (blocked)
                return new PortraitPlan(PortraitKind.Letter, string.Empty, string.Empty);

            var url = string.Empty;
            try { url = info.RiskInfo?.FaceUrl ?? string.Empty; } catch { url = string.Empty; }
            if (url.Length > 0)
                return new PortraitPlan(PortraitKind.Photo, url, string.Empty);

            var icon = LegacyIcon(info);
            return icon.Length > 0
                ? new PortraitPlan(PortraitKind.Official, icon, string.Empty)
                : new PortraitPlan(PortraitKind.Letter, string.Empty, string.Empty);
        }

        private static string LegacyIcon(FriendBaseInfo info)
        {
            try
            {
                var faceId = info.FaceId;
                if (faceId == 0)
                    return string.Empty;

                var config = UnitUtility.config;
                var prop = config?.getPropConfig(faceId);
                if (prop == null)
                    return string.Empty;

                var icon = prop.GetIcon();
                return string.IsNullOrEmpty(icon) ? (prop.icon ?? string.Empty) : icon;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool IsInfrastructureFailure(Exception ex) =>
            ex is MissingMethodException or MissingFieldException or TypeLoadException
                or EntryPointNotFoundException or Il2CppInterop.Runtime.Il2CppException ||
            ex.Message.IndexOf("Method not found", StringComparison.OrdinalIgnoreCase) >= 0;

        public static void Reset()
        {
            _cache.Clear();
            _disabled = false;
            _samplesLogged = 0;
            _loggedError = false;
        }
    }
}
```

- [ ] **Step 7: Mapper fills the new fields**

In `src/FriendOverlay/Data/FriendRowMapper.cs`, replace the `return new FriendRowVm { ... }` block with:

```csharp
            var blocked = FaceBlockPolicy.IsBlocked(info);
            var plan = PortraitResolver.Resolve(info, blocked);

            return new FriendRowVm
            {
                UserId = info.Userid,
                Name = name,
                FaceUrl = face,
                FaceBlocked = blocked,
                Portrait = plan.Kind,
                PortraitRef = plan.ImageRef,
                FrameRef = plan.FrameRef,
                RankPoint = info.RankPoint,
                ForecastPoint = info.ForecastPoint,
                State = state,
                IsMutual = info.IsMutual,
                IsOnline = FriendStatusMapper.IsOnline(state),
                IsBusy = FriendStatusMapper.IsBusy(state),
                Platform = info.Platform,
                StatusLabel = FriendStatusMapper.ToLabel(state),
                StatusKind = FriendStatusMapper.ToKind(state),
            };
```

- [ ] **Step 8: Replace `FaceIconResolver` with `GameSpriteResolver`**

Delete `src/FriendOverlay/UI/FaceIconResolver.cs`. Create:

```csharp
// src/FriendOverlay/UI/GameSpriteResolver.cs
using System;
using System.Collections.Generic;
using Il2CppGameRiver;
using Il2CppGameRiver.Client;
using MelonLoader;
using UnityEngine;

namespace FriendOverlay.UI
{
    /// <summary>
    /// Turns a game image reference (sprite name or relative URL) into either a local sprite from
    /// the game's SpriteManager or a downloadable URL. Keyed by reference, not by player, because
    /// official avatars, frames and the moderation placeholder are shared assets.
    /// </summary>
    public static class GameSpriteResolver
    {
        private const float SpriteManagerRetrySeconds = 1f;

        private static readonly HashSet<string> _localMiss = new HashSet<string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> _urls = new Dictionary<string, string>(StringComparer.Ordinal);

        private static SpriteManager? _spriteManager;
        private static bool _spriteManagerFailed;
        private static float _spriteManagerNextTry;
        private static bool _loggedNoSpriteManager;
        private static bool _loggedLocalSpriteError;

        /// <summary>
        /// True with a usable, non-placeholder sprite. A miss answered by the SpriteManager is
        /// memoized; a miss caused by the manager being unavailable is not, so it retries.
        /// </summary>
        public static bool TryGetLocalSprite(string imageRef, out Sprite? sprite)
        {
            sprite = null;
            if (string.IsNullOrEmpty(imageRef) || _localMiss.Contains(imageRef))
                return false;

            var sm = GetSpriteManager();
            if (sm == null)
                return false;

            try
            {
                // Fail closed: without the manager's placeholder id we cannot tell a real icon from
                // the "missing asset" sprite, and drawing that is exactly the blank-box bug.
                Sprite? fallback = null;
                try { fallback = sm.defaultSprite; } catch { fallback = null; }
                if (fallback == null)
                    return false;

                var defaultId = fallback.GetInstanceID();

                Sprite? candidate = null;
                try { candidate = sm.GetSprite(imageRef); } catch { /* miss */ }
                if (!IsUsable(candidate, defaultId))
                {
                    try { candidate = sm.GetDynamicSprite(imageRef); } catch { /* miss */ }
                }

                if (!IsUsable(candidate, defaultId))
                {
                    _localMiss.Add(imageRef);
                    return false;
                }

                sprite = candidate;
                return true;
            }
            catch (Exception ex)
            {
                if (!_loggedLocalSpriteError)
                {
                    _loggedLocalSpriteError = true;
                    MelonLogger.Warning("[FriendOverlay] local sprite lookup failed: " + ex.Message);
                }

                return false;
            }
        }

        /// <summary>Runs the game's portrait URL fixer once per reference.</summary>
        public static string ToUrl(string imageRef)
        {
            if (string.IsNullOrEmpty(imageRef))
                return string.Empty;

            if (_urls.TryGetValue(imageRef, out var cached))
                return cached;

            var url = imageRef;
            try
            {
                var fixedUrl = Utility.TryFixPortraitURL(imageRef);
                if (!string.IsNullOrEmpty(fixedUrl))
                    url = fixedUrl!;
            }
            catch
            {
                // keep raw
            }

            _urls[imageRef] = url;
            return url;
        }

        private static bool IsUsable(Sprite? sprite, int defaultId)
        {
            if (!AvatarCache.IsUsableSprite(sprite))
                return false;

            try
            {
                return sprite!.GetInstanceID() != defaultId;
            }
            catch
            {
                return false;
            }
        }

        private static SpriteManager? GetSpriteManager()
        {
            if (_spriteManagerFailed)
                return null;

            if (_spriteManager != null)
                return _spriteManager;

            if (Time.unscaledTime < _spriteManagerNextTry)
                return null;

            _spriteManagerNextTry = Time.unscaledTime + SpriteManagerRetrySeconds;

            try
            {
                var ui = UnityEngine.Object.FindObjectOfType<GRUIManager>();
                var sm = ui?.spriteManager ?? ui?.GetSpriteManager();
                if (sm != null)
                {
                    _spriteManager = sm;
                    return _spriteManager;
                }

                if (!_loggedNoSpriteManager)
                {
                    _loggedNoSpriteManager = true;
                    MelonLogger.Warning("[FriendOverlay] local sprite route unavailable: no SpriteManager yet");
                }
            }
            catch (Exception ex)
            {
                _spriteManagerFailed = true;
                MelonLogger.Warning("[FriendOverlay] SpriteManager unavailable: " + ex.Message);
            }

            return null;
        }

        public static void Reset()
        {
            _spriteManager = null;
            _spriteManagerFailed = false;
            _spriteManagerNextTry = 0f;
            _localMiss.Clear();
            _urls.Clear();
            _loggedNoSpriteManager = false;
            _loggedLocalSpriteError = false;
        }
    }
}
```

- [ ] **Step 9: `AvatarLoader.RequestShared` (string in-flight keys, sink callback)**

In `src/FriendOverlay/UI/AvatarLoader.cs`:

Replace the `_inFlight` field:

```csharp
        private static readonly HashSet<string> _inFlight = new HashSet<string>(StringComparer.Ordinal);
```

Replace `Request(ulong userId, string? rawUrl)` with:

```csharp
        public static void Request(ulong userId, string? rawUrl)
        {
            if (!Enabled || userId == 0 || string.IsNullOrEmpty(rawUrl))
                return;

            // The game never fetches a raw FaceUrl; it normalizes first, so a partial path would
            // otherwise 404 and get this friend blacklisted for the session.
            var url = Normalize(rawUrl!);
            if (string.IsNullOrEmpty(url))
                return;

            if (!AvatarCache.NeedsImage(userId) || AvatarCache.IsBlocked(userId))
                return;

            Start("u:" + userId, url, texture =>
            {
                if (texture != null)
                    AvatarCache.PutTexture(userId, texture);
            });
        }

        /// <summary>
        /// Downloads an asset that is not tied to one player (official avatar, frame, placeholder).
        /// <paramref name="onDone"/> receives null on failure and owns the texture on success.
        /// Returns false when the download could not start now (capacity, duplicate, failed URL).
        /// </summary>
        public static bool RequestShared(string? rawUrl, Action<Texture2D?> onDone)
        {
            if (!Enabled || string.IsNullOrEmpty(rawUrl) || onDone == null)
                return false;

            var url = Normalize(rawUrl!);
            if (string.IsNullOrEmpty(url))
                return false;

            return Start("url:" + url, url, onDone);
        }

        private static bool Start(string key, string url, Action<Texture2D?> sink)
        {
            if (_active >= MaxConcurrent || _inFlight.Contains(key) || _failedUrls.ContainsKey(url))
                return false;

            _inFlight.Add(key);
            _active++;

            if (!_loggedSampleUrl)
            {
                _loggedSampleUrl = true;
                MelonLogger.Msg("[FriendOverlay] avatar url sample: " + url);
            }

            var generation = _generation;
            try
            {
                MelonCoroutines.Start(Download(key, url, generation, sink));
                return true;
            }
            catch (Exception ex)
            {
                Finish(key, generation);
                Disable("avatar download disabled: " + ex.Message);
                return false;
            }
        }
```

Change `Download` signature and its two `Finish`/`Complete` calls:

```csharp
        private static IEnumerator Download(string key, string url, int generation, Action<Texture2D?> sink)
```

```csharp
            if (!started || request == null)
            {
                Dispose(request);
                Finish(key, generation);
                SafeSink(sink, null, generation);
                yield break;
            }

            while (!IsDone(request))
                yield return null;

            Complete(key, url, request, generation, sink);
```

Change `Complete` signature, its success branch, and its `finally`:

```csharp
        private static void Complete(string key, string url, UnityWebRequest request, int generation, Action<Texture2D?> sink)
        {
            Texture2D? texture = null;
            var delivered = false;

            try
            {
                if (generation != _generation)
                    return;

                var error = request.error;
                if (!string.IsNullOrEmpty(error))
                {
                    Fail(url, "http: " + error, generation);
                    return;
                }

                var handler = request.downloadHandler;
                if (handler == null)
                {
                    Fail(url, "no download handler", generation);
                    return;
                }

                var bytes = handler.data;
                if (bytes == null || bytes.Length == 0)
                {
                    Fail(url, "empty body", generation);
                    return;
                }

                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(texture, bytes, true))
                {
                    Fail(url, "decode failed (" + bytes.Length + " bytes)", generation);
                    return;
                }

                // The sink owns it now, including on its own refusal paths.
                var handoff = texture;
                texture = null;
                delivered = true;
                SafeSink(sink, handoff, generation);
            }
            catch (Exception ex)
            {
                if (IsInfrastructureFailure(ex))
                    Disable("avatar download disabled: " + ex.Message);
                else
                    Fail(url, "decode threw: " + ex.Message, generation);
            }
            finally
            {
                if (texture != null)
                {
                    try { UnityEngine.Object.Destroy(texture); }
                    catch { /* already gone */ }
                }

                Dispose(request);
                Finish(key, generation);
                if (!delivered)
                    SafeSink(sink, null, generation);
            }
        }

        private static void SafeSink(Action<Texture2D?> sink, Texture2D? texture, int generation)
        {
            // A result from a finished session must not touch the new session's caches.
            if (generation != _generation)
            {
                if (texture != null)
                {
                    try { UnityEngine.Object.Destroy(texture); }
                    catch { /* already gone */ }
                }

                return;
            }

            try
            {
                sink(texture);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[FriendOverlay] avatar sink threw: " + ex.Message);
            }
        }
```

Change `Finish`:

```csharp
        private static void Finish(string key, int generation)
        {
            if (_active > 0)
                _active--;

            if (generation == _generation)
                _inFlight.Remove(key);
        }
```

- [ ] **Step 10: Create `SharedImageCache`**

```csharp
// src/FriendOverlay/UI/SharedImageCache.cs
using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;

namespace FriendOverlay.UI
{
    public sealed class SharedImage
    {
        public Sprite? Sprite;
        public Texture2D? Texture;
        public bool Pending;
        public bool Failed;

        public bool HasImage => AvatarCache.IsUsableTexture(Texture) || AvatarCache.IsUsableSprite(Sprite);
    }

    /// <summary>
    /// Images shared between players: official avatars, avatar frames, the moderation placeholder.
    /// Keyed by the game's image reference. Sprites are game-owned and never destroyed here;
    /// textures come from <see cref="AvatarLoader"/> and are ours.
    /// </summary>
    public static class SharedImageCache
    {
        private static readonly Dictionary<string, SharedImage> _byRef = new Dictionary<string, SharedImage>(StringComparer.Ordinal);
        private static int _failuresLogged;

        public static SharedImage? Get(string? imageRef)
        {
            if (string.IsNullOrEmpty(imageRef))
                return null;

            return _byRef.TryGetValue(imageRef!, out var entry) && entry.HasImage ? entry : null;
        }

        /// <summary>True while the reference is drawable or still loading; false once it is a dead end.</summary>
        public static bool Request(string? imageRef)
        {
            if (string.IsNullOrEmpty(imageRef))
                return false;

            var entry = GetOrAdd(imageRef!);
            if (entry.HasImage || entry.Pending)
                return true;
            if (entry.Failed)
                return false;

            if (GameSpriteResolver.TryGetLocalSprite(imageRef!, out var sprite))
            {
                entry.Sprite = sprite;
                return true;
            }

            var url = GameSpriteResolver.ToUrl(imageRef!);
            if (!AvatarLoader.WillAttempt(url))
            {
                Fail(entry, imageRef!, "no local sprite and url rejected");
                return false;
            }

            var started = AvatarLoader.RequestShared(url, texture =>
            {
                entry.Pending = false;
                if (texture == null)
                {
                    Fail(entry, imageRef!, "download failed");
                    return;
                }

                if (entry.Texture != null && entry.Texture != texture)
                {
                    try { UnityEngine.Object.Destroy(entry.Texture); }
                    catch { /* already gone */ }
                }

                entry.Texture = texture;
            });

            // Not started means "try again next tick" (capacity), not "give up".
            entry.Pending = started;
            return true;
        }

        public static void Clear()
        {
            foreach (var entry in _byRef.Values)
            {
                if (entry.Texture == null)
                    continue;

                try { UnityEngine.Object.Destroy(entry.Texture); }
                catch { /* already gone */ }
            }

            _byRef.Clear();
            _failuresLogged = 0;
        }

        private static SharedImage GetOrAdd(string imageRef)
        {
            if (_byRef.TryGetValue(imageRef, out var entry))
                return entry;

            entry = new SharedImage();
            _byRef[imageRef] = entry;
            return entry;
        }

        private static void Fail(SharedImage entry, string imageRef, string reason)
        {
            entry.Failed = true;
            if (_failuresLogged >= 10)
                return;

            _failuresLogged++;
            MelonLogger.Warning("[FriendOverlay] shared image failed (" + reason + "): " + imageRef);
        }
    }
}
```

- [ ] **Step 11: Route by `PortraitKind` in `GameAssets`**

In `src/FriendOverlay/UI/GameAssets.cs`, replace `RequestMissingAvatars`, `LogTerminalRoute`, `TerminalReason` and `LogSummary` with:

```csharp
        /// <summary>
        /// Routes every row to exactly one image source and records where rows ended up.
        /// Photo rows use the per-user cache; official avatars, the moderation placeholder and
        /// frames go through the shared, reference-keyed cache.
        /// </summary>
        public static void RequestMissingAvatars(IReadOnlyList<Core.FriendRowVm> rows)
        {
            var images = 0;
            var letters = 0;
            var blocked = 0;
            var pending = 0;
            var frames = 0;

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];

                if (!string.IsNullOrEmpty(row.FrameRef) && SharedImageCache.Request(row.FrameRef))
                    frames++;

                switch (row.Portrait)
                {
                    case Core.PortraitKind.Letter:
                        letters++;
                        LogRouteOnce(row, "no portrait");
                        continue;

                    case Core.PortraitKind.Blocked:
                        // Refuse per-user photos for as long as the game hides this face, and show
                        // the placeholder the game substituted instead.
                        AvatarCache.Block(row.UserId);
                        blocked++;
                        if (SharedImageCache.Get(row.PortraitRef) != null)
                            images++;
                        else if (SharedImageCache.Request(row.PortraitRef))
                            pending++;
                        else
                        {
                            letters++;
                            LogRouteOnce(row, "blocked, placeholder unavailable");
                        }
                        continue;

                    case Core.PortraitKind.Official:
                        if (SharedImageCache.Get(row.PortraitRef) != null)
                        {
                            images++;
                            Announce(row, "official (cached)");
                        }
                        else if (SharedImageCache.Request(row.PortraitRef))
                        {
                            pending++;
                            Announce(row, "official");
                        }
                        else
                        {
                            letters++;
                            LogRouteOnce(row, "official-failed: " + row.PortraitRef);
                        }
                        continue;
                }

                // Photo. The moderation setting can be turned off mid-session; without this the row
                // would refuse every image for the rest of the session.
                if (AvatarCache.IsBlocked(row.UserId))
                {
                    AvatarCache.Unblock(row.UserId);
                    _loggedRoutes.Remove(row.UserId);
                    _announce.Add(row.UserId);
                }

                if (!AvatarCache.NeedsImage(row.UserId))
                {
                    images++;
                    Announce(row, "photo (cached)");
                    continue;
                }

                if (AvatarLoader.WillAttempt(row.PortraitRef))
                {
                    AvatarLoader.Request(row.UserId, row.PortraitRef);
                    pending++;
                    Announce(row, "photo");
                    continue;
                }

                letters++;
                LogTerminalRoute(row);
            }

            LogSummary(images, letters, blocked, pending, frames);
        }

        /// <summary>
        /// Same as <see cref="LogRouteOnce"/>, but the reason costs a URL normalization to build, so
        /// it is only computed for the one frame that actually logs.
        /// </summary>
        private static void LogTerminalRoute(Core.FriendRowVm row)
        {
            _announce.Remove(row.UserId);

            if (!_loggedRoutes.Add(row.UserId))
                return;

            Emit(row, TerminalReason(row));
        }

        private static string TerminalReason(Core.FriendRowVm row)
        {
            if (!AvatarLoader.Enabled)
                return "loader-disabled";

            return AvatarLoader.TryGetFailure(row.PortraitRef, out var error)
                ? "photo-failed: " + error
                : "photo-failed";
        }

        private static void LogSummary(int images, int letters, int blocked, int pending, int frames)
        {
            if (Anim.Now < _nextSummaryAt)
                return;

            _nextSummaryAt = Anim.Now + SummaryInterval;

            // images + letters equals the row count once pending reaches zero; blocked rows are
            // counted inside images when their placeholder loaded, inside letters otherwise.
            var summary =
                "images=" + images +
                " letters=" + letters +
                " blocked=" + blocked +
                " pending=" + pending +
                " frames=" + frames +
                " failedUrls=" + AvatarLoader.FailedUrlCount;

            if (string.Equals(summary, _lastSummary, StringComparison.Ordinal))
                return;

            _lastSummary = summary;
            MelonLoader.MelonLogger.Msg("[FriendOverlay] avatars " + summary);
        }
```

Add a one-shot native parity trace to `GameAssets` (permanent diagnostic, called from `RequestMissingAvatars`'s first line as `LogNativeParityOnce(rows);`):

```csharp
        private static bool _parityLogged;

        /// <summary>
        /// Once per session, compare our plan with what the hidden native cells actually loaded.
        /// This is the positive control for the Official-over-Photo rule.
        /// </summary>
        private static void LogNativeParityOnce(IReadOnlyList<Core.FriendRowVm> rows)
        {
            if (_parityLogged)
                return;

            var panel = State.OverlaySession.Panel;
            if (panel == null || rows.Count == 0)
                return;

            try
            {
                var layout = panel.friendListContentLayout;
                var root = layout?.transform;
                if (root == null || root.childCount == 0)
                    return;

                var logged = 0;
                for (var c = 0; c < root.childCount && logged < 3; c++)
                {
                    var cell = root.GetChild(c)?.GetComponent<FriendCellNode>();
                    var label = cell?.nameLabel;
                    if (cell == null || label == null)
                        continue;

                    var name = label.text ?? string.Empty;
                    Core.FriendRowVm? row = null;
                    for (var i = 0; i < rows.Count; i++)
                    {
                        if (string.Equals(rows[i].Name, name, StringComparison.Ordinal))
                        {
                            row = rows[i];
                            break;
                        }
                    }

                    if (row == null)
                        continue;

                    var img = cell.playerImage;
                    if (img == null)
                        continue;

                    var avatarActive = false;
                    var outlineActive = false;
                    try { avatarActive = img.avatarObj != null && img.avatarObj.activeSelf; } catch { }
                    try { outlineActive = img.outlineObj != null && img.outlineObj.activeSelf; } catch { }

                    MelonLoader.MelonLogger.Msg(
                        "[FriendOverlay] portrait parity uid=" + row.UserId +
                        " ours=" + row.Portrait + "|" + row.PortraitRef + "|" + row.FrameRef +
                        " native.spriteUrl=" + (img.spriteUrl ?? string.Empty) +
                        " native.avatar=" + (img._avatar ?? string.Empty) + " active=" + avatarActive +
                        " native.outline=" + (img._outline ?? string.Empty) + " active=" + outlineActive);
                    logged++;
                }

                if (logged > 0)
                    _parityLogged = true;
            }
            catch (Exception ex)
            {
                _parityLogged = true;
                MelonLoader.MelonLogger.Msg("[FriendOverlay] portrait parity unavailable: " + ex.Message);
            }
        }
```

Add `_parityLogged = false;` to `GameAssets.Reset()`.

- [ ] **Step 12: Draw image + frame in `RowCard`**

Replace `DrawAvatar` in `src/FriendOverlay/UI/Widgets/RowCard.cs`:

```csharp
        private static void DrawAvatar(Rect r, FriendRowVm row, Color statusColor)
        {
            Gfx.Fill(r, Theme.Bg1);

            Texture2D? texture = null;
            Sprite? sprite = null;
            if (!ForceLetters)
            {
                if (row.Portrait == PortraitKind.Photo)
                {
                    var entry = GameAssets.GetAvatar(row.UserId);
                    if (entry != null)
                    {
                        texture = AvatarCache.IsUsableTexture(entry.Texture) ? entry.Texture : null;
                        sprite = texture == null && AvatarCache.IsUsableSprite(entry.Sprite) ? entry.Sprite : null;
                    }
                }
                else if (row.Portrait == PortraitKind.Official || row.Portrait == PortraitKind.Blocked)
                {
                    var shared = SharedImageCache.Get(row.PortraitRef);
                    if (shared != null)
                    {
                        texture = AvatarCache.IsUsableTexture(shared.Texture) ? shared.Texture : null;
                        sprite = texture == null && AvatarCache.IsUsableSprite(shared.Sprite) ? shared.Sprite : null;
                    }
                }
            }

            var hasImage = texture != null || sprite != null;

            // Letter first, image on top. An opaque avatar hides it; a fully transparent or
            // zero-pixel image leaves it showing. Dimension checks cannot tell those apart, and a
            // washed-out glyph behind a cosmetic icon beats an empty box.
            var glyph = hasImage
                ? new Color(Theme.TextMuted.r, Theme.TextMuted.g, Theme.TextMuted.b, Theme.TextMuted.a * 0.5f)
                : Theme.TextMuted;
            Gfx.Text(r, string.IsNullOrEmpty(row.Name) ? "#" : row.Name.Substring(0, 1), glyph, Theme.Avatar);

            var inner = new Rect(r.x + 1f, r.y + 1f, r.width - 2f, r.height - 2f);
            if (texture != null)
                Gfx.Texture(inner, texture, Color.white);
            else if (sprite != null)
                Gfx.Sprite(inner, sprite, Color.white);

            Gfx.Border(r, statusColor);

            // Frame sits above the status border like the native outline layer; Ctrl+L hides it too.
            if (!ForceLetters)
                DrawFrame(r, row.FrameRef);

            var badge = Theme.S(12f);
            var badgeR = new Rect(r.xMax - badge, r.yMax - badge, badge, badge);
            Gfx.Fill(badgeR, Theme.PlatformColor(row.Platform));
            Gfx.Border(badgeR, Theme.Bg0);
        }

        private static void DrawFrame(Rect r, string frameRef)
        {
            var frame = SharedImageCache.Get(frameRef);
            if (frame == null)
                return;

            var grow = Theme.S(4f);
            var fr = new Rect(r.x - grow, r.y - grow, r.width + grow * 2f, r.height + grow * 2f);
            if (AvatarCache.IsUsableTexture(frame.Texture))
                Gfx.Texture(fr, frame.Texture, Color.white);
            else
                Gfx.Sprite(fr, frame.Sprite, Color.white);
        }
```

- [ ] **Step 13: Reset the new caches with the session**

In `src/FriendOverlay/State/OverlaySession.cs` replace `ResetAvatarPipeline`:

```csharp
        private static void ResetAvatarPipeline()
        {
            UI.AvatarLoader.Reset();
            UI.GameSpriteResolver.Reset();
            UI.SharedImageCache.Clear();
            Data.PortraitResolver.Reset();
            Data.FaceBlockPolicy.Reset();
            UI.AvatarCache.Clear();
            UI.GameAssets.Reset();
        }
```

- [ ] **Step 14: Build, run all tests**

Run: `dotnet build src\FriendOverlay\FriendOverlay.csproj -c Release`
Expected: `Build succeeded.` (no references to `FaceIconResolver` or `FaceId` remain — `Grep` for both must return nothing under `src/`).

Run: `dotnet test tests\FriendOverlay.Tests\FriendOverlay.Tests.csproj`
Expected: `Passed!`

- [ ] **Step 15: In-game verification and the parity decision**

Deploy with `-p:Deploy=true`, open friends, scroll the whole list, then read the log:

1. Three `portrait sample uid=... -> Official|Photo|Blocked|Letter image=... frame=...` lines.
2. `portrait parity uid=...` lines. **Decision rule:** for each line, `native.avatar` non-empty **and** `active=True` must coincide with `ours=Official`; `native.avatar` empty must coincide with `ours=Photo`/`Blocked`/`Letter`. If they disagree on any row, edit `PortraitPlanner.Decide` so the avatar layer wins only when the native `avatarObj` would be active (e.g. require `avatarUrl` non-empty **and** portrait empty, or invert), re-run the planner tests (update expectations), rebuild, and re-check parity until every logged row agrees. Record the final rule in the commit body.
3. Rows whose player picked an official avatar show that avatar (not their Steam photo); players with a custom photo show the photo. Compare 5 rows against `F8` native.
4. Players with an avatar frame show the frame ring around the avatar in both modes.
5. Enable the in-game 「显示被封禁头像」 toggle off/on: blocked rows show the dark placeholder (not the real photo, not a bare letter unless the placeholder failed), and flip back without reopening.
6. `Ctrl+L`: every avatar and every frame disappears, letters remain; second press restores without new downloads (`failedUrls` unchanged, no new `avatar url sample`).
7. Summary line reaches `pending=0` and `images + letters == row count`; no `DrawWindow failed`, no `MissingMethodException`.
8. Reopen the panel three times: no texture leak warnings, frames still drawn (shared cache refilled).

- [ ] **Step 16: Commit**

```bash
git add -A src/FriendOverlay.Core/PortraitPlan.cs src/FriendOverlay.Core/FriendRowVm.cs tests/FriendOverlay.Tests/PortraitPlannerTests.cs src/FriendOverlay/FriendOverlay.csproj src/FriendOverlay/Data/PortraitResolver.cs src/FriendOverlay/Data/FriendRowMapper.cs src/FriendOverlay/UI/GameSpriteResolver.cs src/FriendOverlay/UI/SharedImageCache.cs src/FriendOverlay/UI/FaceIconResolver.cs src/FriendOverlay/UI/AvatarLoader.cs src/FriendOverlay/UI/GameAssets.cs src/FriendOverlay/UI/Widgets/RowCard.cs src/FriendOverlay/State/OverlaySession.cs
git commit -m "feat: draw the portrait the game shows (official avatar vs photo) plus avatar frame"
```

---

### Task 6: Release 0.3.4 — version, docs, deploy, acceptance

**Files:**
- Modify: `src/FriendOverlay/FriendOverlay.csproj:10`
- Modify: `src/FriendOverlay/FriendOverlayMod.cs:10`
- Modify: `README.md`
- Modify: `玩家说明.md`

**Interfaces:**
- Consumes: everything above.
- Produces: `FriendOverlay.dll` v0.3.4 in `Mods\`, git tag `v0.3.4`.

- [ ] **Step 1: Bump version**

`src/FriendOverlay/FriendOverlay.csproj` line 10: `<Version>0.3.4</Version>`
`src/FriendOverlay/FriendOverlayMod.cs` line 10: `[assembly: MelonInfo(typeof(FriendOverlay.FriendOverlayMod), "FriendOverlay", "0.3.4", "MechabellumFriendOverlay")]`

- [ ] **Step 2: README.md**

Replace the `## 功能` list with:

```markdown
## 功能

- 打开好友时默认隐藏原生面板，显示叠加列表
- 一键 / `F8` 切回原生好友窗
- 标题栏两个页签：**关注** / **关注我的人**（后者仅在游戏接口可用时出现）
- 搜索（名称 / UserId）、筛选（全部/在线/忙碌/互关/离线）、排序（状态 / 战力 / 洞察 / 名称），两个页签通用
- 按可用性分区：`在线 · 可加入` / `在线 · 对战中` / `离线`，分区可折叠且状态持久化
- **置顶**（仅「关注」页签）：`⋯` 菜单 置顶 / 取消置顶，最多 20 人，独立 `置顶` 分区排最前；只存本地偏好，不写游戏服务器
- 行卡片默认显示头像 / 名字 / 战力洞察 / 状态 / `⋯`；**悬停**出现 `加入 | 观战 | 私聊 | 邀请`。`⋯` 或行内**右键**打开菜单，取关与拉黑需确认
- **邀请**走游戏 `LobbyProxy.TryRequestInvite`：需要自己已在自定义房间且对方在线，否则按钮禁用（不隐藏）；同一人 5 秒内不重复发送
- **关注我的人**：`回关`（已互关显示 `⇌ 已互关`）、加入 / 观战 / 私聊 / 邀请 / 拉黑；仅对互关的人提供取消关注；不提供置顶
- 头像显示与游戏一致：玩家选了官方头像就显示官方头像，选了自定义照片就显示照片；同时绘制头像**边框**；来源解析走游戏自己的 `PlayerRiskInfo → PlayerPortraitInfo`
- 头像加载逐级回退：本地精灵 → URL 下载 → 首字母；先画字母再把图盖上去，任何一步失败都不会留下空框
- 跟随游戏的头像屏蔽设置：游戏判定为屏蔽的账号，叠加面板显示游戏替换后的占位图
- 叠加打开时挂全屏透明 uGUI 挡板，点击与拖动不再穿透到背后大厅
- 拖标题栏移动、右下角拖动缩放，位置尺寸写入偏好（关闭面板即落盘）
- 分页拉全关注列表，同步状态条带脉冲指示
- 逐功能能力探测（`invite / followers / portrait`）：缺哪个接口只关哪个功能；整体仍 fail-open

不做：组队邀请、Discord 邀请、「关注我的人」置顶、服务器端置顶、动态边框预制体、黑名单 / Discord 页签、本地备注 / 分组。
```

In `## 界面与快捷键` add a bullet: `- \`Ctrl+L\` 诊断用：强制所有行显示首字母并隐藏边框，再按一次恢复；仅当前会话有效` (replacing the old Ctrl+L line) and `- 标题栏页签「关注 | 关注我的人」点击切换；\`R\` 刷新当前页签`.

In `## 偏好` add:

```markdown
- `PinnedUserIds` — 置顶的 UserId，逗号分隔，最多 20 个，仅本地
- `CollapsePinned` — 置顶分区折叠状态
```

Append to `## 验收清单`:

```markdown
19. 日志出现 `capabilities invite=True followers=True portrait=True`  
20. 未进房间时 `邀请` 为禁用态；进入自定义房间后对在线好友可用，点击后 5 秒内显示 `已邀请`，对方收到邀请  
21. 「关注我的人」页签：计数与原生被关注页一致，分区正确；非互关行 `回关` 后变 `⇌ 已互关`；菜单无 `置顶`，非互关行无 `取消关注`  
22. 置顶 / 取消置顶生效，`置顶` 分区排最前且从原分区移除；第 21 个显示 `置顶（已满 20）`；重启游戏后保留；取关后自动移出  
23. 选了官方头像的玩家显示官方头像，选了照片的显示照片，与 `F8` 原生一致；有边框的玩家显示边框；日志 `portrait parity` 各行 ours/native 一致  
24. `Ctrl+L` 同时隐藏头像与边框  
```

In `## 工程结构` update the UI/Data bullets:

```markdown
  - `Compat/Capabilities.cs` — 逐功能能力探测
  - `Data/GameProxies.cs`、`Data/FansListService.cs`、`Data/PinStore.cs`、`Data/PortraitResolver.cs`、`Data/FriendRowMapper.cs` — LobbyProxy 定位 / 关注我的人 / 置顶 / 头像来源解析 / 行映射
  - `UI/GameAssets.cs`、`UI/AvatarCache.cs`、`UI/AvatarLoader.cs`、`UI/GameSpriteResolver.cs`、`UI/SharedImageCache.cs` — 头像来源路由、按玩家缓存、共享资源（官方头像 / 边框 / 占位图）缓存与下载
```

and under `src/FriendOverlay.Core` add `、置顶集合、邀请规则、头像来源决策（均可单测）`.

- [ ] **Step 3: 玩家说明.md**

Change `**当前版本：0.3.3**` → `**当前版本：0.3.4**`.

Replace the `## 怎么用` table rows for 悬停 and `⋯` and append rows:

```markdown
| 悬停好友行 | 显示 **加入 · 观战 · 私聊 · 邀请** |
| 点 `⋯` 或右键行 | 置顶 / 取消关注 / 拉黑（后两项需确认） |
| 标题栏「关注 / 关注我的人」 | 切换列表；「关注我的人」里可 **回关** |
| 邀请 | 你需要先建好自定义房间，且对方在线；发出后 5 秒内显示「已邀请」 |
```

After the sections paragraph add:

```markdown
「关注」页签支持 **置顶**（最多 20 人），置顶的人单独排在最上面；置顶只保存在你本机，不会通知对方。
```

Replace `## 头像说明` with:

```markdown
## 头像说明

- 对方在游戏里选了官方头像就显示官方头像，选了自己的照片就显示照片，和原生一致；有头像边框也会一起显示
- 没图或加载失败会显示名字首字母，不会出现空白框
- 游戏里被设为屏蔽头像的账号，这里显示与游戏相同的占位图
```

Append to `## 常见问题`:

```markdown
**「邀请」按钮是灰的**  
邀请只能在你已经进入自定义房间、且对方在线时使用。

**没有「关注我的人」页签**  
说明当前游戏版本缺少对应接口，其它功能不受影响；等待 Mod 更新。

**置顶没有同步到别的电脑**  
置顶只存在本机偏好文件里，属正常现象。
```

- [ ] **Step 4: Full verification**

Run: `dotnet test tests\FriendOverlay.Tests\FriendOverlay.Tests.csproj`
Expected: `Passed!` (all suites).

Run: `dotnet build src\FriendOverlay\FriendOverlay.csproj -c Release -p:Deploy=true`
Expected: `Deployed FriendOverlay.dll v0.3.4 to D:\steam\steamapps\common\Mechabellum\Mods` and `Get-ChildItem D:\steam\steamapps\common\Mechabellum\Mods\FriendOverlay*.dll` lists exactly one file.

Start the game and walk README 验收清单 items 1–24. Log must show `FriendOverlay v0.3.4` in the MelonLoader mod banner, `TypeProbe OK`, `capabilities ...`, `hooks applied`, no `DrawWindow failed`, no `MissingMethodException`.

- [ ] **Step 5: Commit and tag**

```bash
git add src/FriendOverlay/FriendOverlay.csproj src/FriendOverlay/FriendOverlayMod.cs README.md 玩家说明.md
git commit -m "release: 0.3.4 — invite, 关注我的人 tab, pin, game-faithful portraits and frames"
git tag v0.3.4
```

Deploy notes for the manager/community catalog: ship `src\FriendOverlay\bin\Release\FriendOverlay.dll` only; users must delete any older `FriendOverlay*.dll` from `Mods\` (the `-p:Deploy=true` target does this locally). No new dependencies; preferences category unchanged (`FriendOverlay`) with two new keys `PinnedUserIds`, `CollapsePinned` that default to empty/false.

---

## Appendix: verifying interop signatures at implement time

The IL2CPP interop stubs cannot be decompiled for bodies, but their signatures can be dumped without running the game. This was used to write the plan and is the fastest way to re-check a name before editing:

```powershell
mkdir $env:TEMP\ApiProbe; cd $env:TEMP\ApiProbe
dotnet new console --framework net8.0 --force | Out-Null
dotnet add package System.Reflection.MetadataLoadContext | Out-Null
```

`Program.cs`:

```csharp
using System.Reflection;
using System.Text.RegularExpressions;
var dir = @"D:\steam\steamapps\common\Mechabellum\MelonLoader\Il2CppAssemblies";
var paths = new List<string>();
foreach (var d in new[] { dir, @"D:\steam\steamapps\common\Mechabellum\MelonLoader\net6", @"C:\Program Files\dotnet\shared\Microsoft.NETCore.App\6.0.36" })
    paths.AddRange(Directory.GetFiles(d, "*.dll"));
using var mlc = new MetadataLoadContext(new PathAssemblyResolver(paths), "System.Private.CoreLib");
var asms = new[] { "Il2CppGRClient", "Il2CppBinNetwork", "Il2CppGRCore", "Il2CppGRUtility" }
    .Select(n => mlc.LoadFromAssemblyPath(Path.Combine(dir, n + ".dll"))).ToList();
IEnumerable<Type> All(Assembly a) { try { return a.GetTypes(); } catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null)!; } }
var typeName = args[0]; var filter = args.Length > 1 ? new Regex(args[1], RegexOptions.IgnoreCase) : null;
var t = asms.SelectMany(All).FirstOrDefault(x => x.FullName == typeName) ?? throw new Exception("not found: " + typeName);
var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
foreach (var m in t.GetMethods(flags).Where(m => filter == null || filter.IsMatch(m.Name)).Where(m => !m.Name.StartsWith("Native")))
    Console.WriteLine($"M {m.ReturnType} {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType + " " + p.Name))})");
foreach (var p in t.GetProperties(flags).Where(p => filter == null || filter.IsMatch(p.Name)))
    Console.WriteLine($"P {p.PropertyType} {p.Name}");
```

Usage: `dotnet run -- Il2CppGameRiver.Client.LobbyProxy "Invite|JoinedRoom"`. Verified for this plan: `LobbyProxy.TryRequestInvite(UInt64, String, Boolean)`, `LobbyProxy.InviteUserJoin(UInt64, Boolean)`, `LobbyProxy.JoinedRoom : LobbyRoom`, `GameFacade.Instance`, `GameFacade.RetrieveProxy<T>(String)`, `Proxy<T>.NAME`, `FriendProxy.followerBaseInfoList : List<FriendBaseInfo>`, `FriendProxy.RequestLastFollower()`, `FriendProxy.RequestFollowUser(UInt64)`, `FriendProxy.OnResponseLastFollower(Object sender, ResponseLastFollower follower)`, `ResponseLastFollower.Follower : RepeatedField<FriendBaseInfo>`, `ResponseLastFollower.New : RepeatedField<UInt64>`, `ResponseFollowStatus.Following` (hence `RequestFollowerStatus` is not a fans API), `Il2CppGameRiver.PlayerRiskInfo(String, String, …, Int32 faceId, Int32 faceBorder)` with `GetPortraitInfo()/GetPortrait()/GetAvatarURL()/GetAvatarOutLineURL()`, `PlayerPortraitInfo.GetAvatarURL()/GetAvatarOutLineURL()/GetAvatarID()/GetAvatarOutLineID()`, `GRImage.SetPlayerPortrait(String portraitURL, String outline, String avatar)`, `GRImage.avatarObj/outlineObj/spriteUrl/_avatar/_outline`, `FriendCellNode.playerImage/nameLabel`, `SpriteManager.GetSprite/GetDynamicSprite/defaultSprite`, `Utility.TryFixPortraitURL(String)`.