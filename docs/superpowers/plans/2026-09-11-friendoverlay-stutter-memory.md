# FriendOverlay Stutter / Memory Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the friends overlay with aggressive memory release (abort downloads + UnloadUnusedAssets), and open it under a per-frame GPU bake budget with visible-row priority and opt-in official GIF animation.

**Architecture:** Pure logic lives in `FriendOverlay.Core` (`BakeBudget`, `VisibleRowRange`, `OverlayPerfSettings`) so xUnit can lock behavior without Unity. Unity-facing code in `FriendOverlay` consults those types at CaptureRoot / blit / avatar-request / LiveGifHost / session End. Closing still clears caches; Reset also aborts HTTP and schedules one unload.

**Tech Stack:** C# / MelonLoader / Unity IL2CPP interop / xUnit (`FriendOverlay.Tests` → `FriendOverlay.Core`)

**Working branch:** `feature/invite-battle-type-picker`

## Global Constraints

- Spec: `docs/superpowers/specs/2026-09-11-friendoverlay-stutter-memory-design.md`
- Default bake budget: **2** expensive GPU ops per frame
- Visible prefetch margin: **±2** list items (by item index in the flat draw list)
- `AnimatedOfficialAvatars` default: **false** (single-frame official GIF bake)
- Version bump to **0.3.23** (MelonInfo + csproj `<Version>`)
- Do not touch DamageRank or other mods; no disk avatar cache
- Existing uncommitted WIP on this branch may already contain avatar/GIF files — integrate, do not revert unrelated work; only commit files listed in each Commit step

---

### Task 1: BakeBudget (Core)

**Files:**
- Create: `src/FriendOverlay.Core/BakeBudget.cs`
- Create: `tests/FriendOverlay.Tests/BakeBudgetTests.cs`
- Modify: `src/FriendOverlay/FriendOverlay.csproj` — add Link compile for `BakeBudget.cs` next to other Core links

**Interfaces:**
- Produces:
  - `FriendOverlay.Core.BakeBudget` static class
  - `public const int DefaultLimit = 2`
  - `public static int Limit { get; set; }` (tests may lower)
  - `public static void BeginFrame(int frameCount)` — reset counter when `frameCount` changes
  - `public static bool TryConsume()` — true if under limit (increments); false if exhausted
  - `public static int Remaining { get; }`
  - `public static void Reset()` — clear frame id + counter (session teardown)

- [ ] **Step 1: Write the failing test**

```csharp
using FriendOverlay.Core;

namespace FriendOverlay.Tests;

public class BakeBudgetTests
{
    public BakeBudgetTests()
    {
        BakeBudget.Limit = BakeBudget.DefaultLimit;
        BakeBudget.Reset();
    }

    [Fact]
    public void TryConsume_allows_DefaultLimit_then_denies_until_next_frame()
    {
        BakeBudget.BeginFrame(1);
        Assert.True(BakeBudget.TryConsume());
        Assert.True(BakeBudget.TryConsume());
        Assert.False(BakeBudget.TryConsume());
        Assert.Equal(0, BakeBudget.Remaining);

        BakeBudget.BeginFrame(2);
        Assert.True(BakeBudget.TryConsume());
        Assert.Equal(BakeBudget.DefaultLimit - 1, BakeBudget.Remaining);
    }

    [Fact]
    public void BeginFrame_same_frame_does_not_reset()
    {
        BakeBudget.BeginFrame(5);
        Assert.True(BakeBudget.TryConsume());
        Assert.True(BakeBudget.TryConsume());
        BakeBudget.BeginFrame(5);
        Assert.False(BakeBudget.TryConsume());
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/FriendOverlay.Tests/FriendOverlay.Tests.csproj --filter BakeBudgetTests -v n`  
Expected: FAIL (type or members missing)

- [ ] **Step 3: Write minimal implementation**

```csharp
namespace FriendOverlay.Core
{
    public static class BakeBudget
    {
        public const int DefaultLimit = 2;

        private static int _frame = int.MinValue;
        private static int _used;

        public static int Limit { get; set; } = DefaultLimit;

        public static int Remaining
        {
            get
            {
                var lim = Limit < 0 ? 0 : Limit;
                return lim > _used ? lim - _used : 0;
            }
        }

        public static void BeginFrame(int frameCount)
        {
            if (frameCount == _frame)
                return;
            _frame = frameCount;
            _used = 0;
        }

        public static bool TryConsume()
        {
            if (Remaining <= 0)
                return false;
            _used++;
            return true;
        }

        public static void Reset()
        {
            _frame = int.MinValue;
            _used = 0;
        }
    }
}
```

Also add to `FriendOverlay.csproj` ItemGroup Compile Link:

```xml
<Compile Include="..\FriendOverlay.Core\BakeBudget.cs" Link="Core\BakeBudget.cs" />
```

(If `FriendOverlay.Core.csproj` uses wildcard `**/*.cs`, no Core csproj edit needed.)

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/FriendOverlay.Tests/FriendOverlay.Tests.csproj --filter BakeBudgetTests -v n`  
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/FriendOverlay.Core/BakeBudget.cs tests/FriendOverlay.Tests/BakeBudgetTests.cs src/FriendOverlay/FriendOverlay.csproj
git commit -m "feat: add BakeBudget for per-frame GPU bake limit"
```

---

### Task 2: VisibleRowRange (Core)

**Files:**
- Create: `src/FriendOverlay.Core/VisibleRowRange.cs`
- Create: `tests/FriendOverlay.Tests/VisibleRowRangeTests.cs`
- Modify: `src/FriendOverlay/FriendOverlay.csproj` — Link `VisibleRowRange.cs`

**Interfaces:**
- Produces:
  - `VisibleRowRange.Compute(float scrollY, float viewportHeight, IReadOnlyList<float> itemOffsets, IReadOnlyList<float> itemHeights, int marginItems)` → `(int startInclusive, int endExclusive)` over item indices that intersect the expanded window
  - Empty lists → `(0, 0)`

- [ ] **Step 1: Write the failing test**

```csharp
using FriendOverlay.Core;

namespace FriendOverlay.Tests;

public class VisibleRowRangeTests
{
    [Fact]
    public void Compute_returns_intersecting_indices_with_margin()
    {
        // Three 40px rows stacked; viewport 50px at scroll 30 → rows 0..2 partially relevant
        var offsets = new float[] { 0f, 40f, 80f };
        var heights = new float[] { 40f, 40f, 40f };
        var (start, end) = VisibleRowRange.Compute(
            scrollY: 30f,
            viewportHeight: 50f,
            itemOffsets: offsets,
            itemHeights: heights,
            marginItems: 0);
        Assert.Equal(0, start);
        Assert.Equal(3, end);
    }

    [Fact]
    public void Compute_applies_index_margin()
    {
        var offsets = new float[] { 0f, 40f, 80f, 120f, 160f };
        var heights = new float[] { 40f, 40f, 40f, 40f, 40f };
        // Viewport shows only row index 2 (offset 80) when scrollY=80, height=40
        var (start, end) = VisibleRowRange.Compute(80f, 40f, offsets, heights, marginItems: 1);
        Assert.Equal(1, start);
        Assert.Equal(4, end);
    }

    [Fact]
    public void Compute_empty_is_zero_span()
    {
        var (start, end) = VisibleRowRange.Compute(0f, 100f, Array.Empty<float>(), Array.Empty<float>(), 2);
        Assert.Equal(0, start);
        Assert.Equal(0, end);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/FriendOverlay.Tests/FriendOverlay.Tests.csproj --filter VisibleRowRangeTests -v n`  
Expected: FAIL

- [ ] **Step 3: Write minimal implementation**

```csharp
using System;
using System.Collections.Generic;

namespace FriendOverlay.Core
{
    public static class VisibleRowRange
    {
        public static (int startInclusive, int endExclusive) Compute(
            float scrollY,
            float viewportHeight,
            IReadOnlyList<float> itemOffsets,
            IReadOnlyList<float> itemHeights,
            int marginItems)
        {
            if (itemOffsets == null || itemHeights == null)
                return (0, 0);
            var n = Math.Min(itemOffsets.Count, itemHeights.Count);
            if (n <= 0)
                return (0, 0);

            var viewTop = scrollY;
            var viewBottom = scrollY + Math.Max(0f, viewportHeight);
            var first = -1;
            var last = -1;
            for (var i = 0; i < n; i++)
            {
                var top = itemOffsets[i];
                var bottom = top + itemHeights[i];
                if (bottom < viewTop || top > viewBottom)
                    continue;
                if (first < 0)
                    first = i;
                last = i;
            }

            if (first < 0)
                return (0, 0);

            var m = marginItems < 0 ? 0 : marginItems;
            var start = Math.Max(0, first - m);
            var end = Math.Min(n, last + 1 + m);
            return (start, end);
        }
    }
}
```

Link in csproj as in Task 1.

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/FriendOverlay.Tests/FriendOverlay.Tests.csproj --filter "FullyQualifiedName~VisibleRowRangeTests|FullyQualifiedName~BakeBudgetTests" -v n`  
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/FriendOverlay.Core/VisibleRowRange.cs tests/FriendOverlay.Tests/VisibleRowRangeTests.cs src/FriendOverlay/FriendOverlay.csproj
git commit -m "feat: add VisibleRowRange for avatar load priority"
```

---

### Task 3: OverlayPerfSettings + single-frame GIF default (Core + LiveGifHost)

**Files:**
- Create: `src/FriendOverlay.Core/OverlayPerfSettings.cs`
- Create: `tests/FriendOverlay.Tests/OverlayPerfSettingsTests.cs`
- Modify: `src/FriendOverlay/UI/LiveGifHost.cs` — when `!OverlayPerfSettings.AnimatedOfficialAvatars`, after GIF list is ready bake **one** frame via existing capture path then `Finish` with single texture (skip temporal multi-frame loop)
- Modify: `src/FriendOverlay/FriendOverlay.csproj` — Link `OverlayPerfSettings.cs`
- Modify: `src/FriendOverlay/FriendOverlayMod.cs` — MelonPreferences entry `AnimatedOfficialAvatars` default false; load into `OverlayPerfSettings.AnimatedOfficialAvatars`; save on persist

**Interfaces:**
- Produces: `OverlayPerfSettings.AnimatedOfficialAvatars` get/set bool, default false
- Consumes: `GifPlayback.CapFrameCount` (unchanged)

- [ ] **Step 1: Write the failing test**

```csharp
using FriendOverlay.Core;

namespace FriendOverlay.Tests;

public class OverlayPerfSettingsTests
{
    [Fact]
    public void AnimatedOfficialAvatars_defaults_false()
    {
        OverlayPerfSettings.ResetToDefaults();
        Assert.False(OverlayPerfSettings.AnimatedOfficialAvatars);
    }

    [Fact]
    public void TargetGifFrameCount_is_one_when_animation_off()
    {
        OverlayPerfSettings.ResetToDefaults();
        Assert.Equal(1, OverlayPerfSettings.TargetGifFrameCount(12));
        OverlayPerfSettings.AnimatedOfficialAvatars = true;
        Assert.Equal(12, OverlayPerfSettings.TargetGifFrameCount(12));
        Assert.Equal(GifPlayback.MaxFrames, OverlayPerfSettings.TargetGifFrameCount(100));
    }
}
```

- [ ] **Step 2: Run test — expect FAIL**

Run: `dotnet test tests/FriendOverlay.Tests/FriendOverlay.Tests.csproj --filter OverlayPerfSettingsTests -v n`

- [ ] **Step 3: Implement Core + wire LiveGifHost / prefs**

```csharp
namespace FriendOverlay.Core
{
    public static class OverlayPerfSettings
    {
        public static bool AnimatedOfficialAvatars { get; set; }

        public static void ResetToDefaults()
        {
            AnimatedOfficialAvatars = false;
        }

        public static int TargetGifFrameCount(int spriteListCount)
        {
            var capped = GifPlayback.CapFrameCount(spriteListCount);
            if (!AnimatedOfficialAvatars)
                return capped <= 0 ? 0 : 1;
            return capped;
        }
    }
}
```

In `LiveGifHost`, where `pending.TargetCount = GifPlayback.CapFrameCount(list.Count)` is set, replace with:

```csharp
pending.TargetCount = OverlayPerfSettings.TargetGifFrameCount(list.Count);
```

In `FriendOverlayMod.LoadPreferences` / `SavePreferences`, add entry:

```csharp
_prefAnimatedOfficial = _prefs.CreateEntry("AnimatedOfficialAvatars", false, "Bake animated official avatar GIFs (uses more memory)");
OverlayPerfSettings.AnimatedOfficialAvatars = _prefAnimatedOfficial.Value;
// save: _prefAnimatedOfficial.Value = OverlayPerfSettings.AnimatedOfficialAvatars;
```

Call `OverlayPerfSettings.ResetToDefaults()` from `OverlaySession.ResetAvatarPipeline` is **not** required (prefs survive session); only BakeBudget.Reset there.

- [ ] **Step 4: Run Core tests**

Run: `dotnet test tests/FriendOverlay.Tests/FriendOverlay.Tests.csproj --filter OverlayPerfSettingsTests -v n`  
Expected: PASS  
Also build: `dotnet build src/FriendOverlay/FriendOverlay.csproj -c Release`  
Expected: build succeeds (LiveGifHost compiles)

- [ ] **Step 5: Commit**

```bash
git add src/FriendOverlay.Core/OverlayPerfSettings.cs tests/FriendOverlay.Tests/OverlayPerfSettingsTests.cs src/FriendOverlay/UI/LiveGifHost.cs src/FriendOverlay/FriendOverlayMod.cs src/FriendOverlay/FriendOverlay.csproj
git commit -m "feat: default official GIF bake to one frame (opt-in animation)"
```

---

### Task 4: Wire BakeBudget into SpriteCapture / SpriteBake

**Files:**
- Modify: `src/FriendOverlay/UI/SpriteCapture.cs` — at start of `Capture` / `CaptureRoot`, call `BakeBudget.BeginFrame(Time.frameCount)` then `if (!BakeBudget.TryConsume()) return null;`
- Modify: `src/FriendOverlay/UI/SpriteBake.cs` — before `TryBlitCrop` (the expensive path), `BeginFrame` + `TryConsume`; if denied, return null (GetPixels path is cheaper — still consume one budget unit when blit is used only). Spec: both CaptureRoot and blit-crop count. Simplest rule: **every** `CaptureRoot`/`Capture` success path consumes; `TryBlitCrop` consumes; pure `TryGetPixels` success does **not** consume (CPU copy, no RT).
- Modify: `src/FriendOverlay/State/OverlaySession.cs` — in `ResetAvatarPipeline`, call `BakeBudget.Reset()`

**Interfaces:**
- Consumes: `BakeBudget.BeginFrame`, `TryConsume`, `Reset`

No new unit test (Unity). Verify by build + manual later.

- [ ] **Step 1: Implement gate in SpriteCapture.CaptureRoot (and Capture if separate public entry)**

At the top of `CaptureRoot` after null checks, before `_busy = true`:

```csharp
BakeBudget.BeginFrame(Time.frameCount);
if (!BakeBudget.TryConsume())
    return null;
```

Same for `SpriteCapture.Capture(Sprite...)`.

- [ ] **Step 2: Gate SpriteBake.TryBlitCrop**

At the start of `TryBlitCrop`:

```csharp
BakeBudget.BeginFrame(Time.frameCount);
if (!BakeBudget.TryConsume())
    return null;
```

- [ ] **Step 3: Reset on session teardown**

In `ResetAvatarPipeline` after other clears:

```csharp
FriendOverlay.Core.BakeBudget.Reset();
```

- [ ] **Step 4: Build**

Run: `dotnet build src/FriendOverlay/FriendOverlay.csproj -c Release`  
Expected: 0 errors

- [ ] **Step 5: Commit**

```bash
git add src/FriendOverlay/UI/SpriteCapture.cs src/FriendOverlay/UI/SpriteBake.cs src/FriendOverlay/State/OverlaySession.cs
git commit -m "perf: enforce BakeBudget on CaptureRoot and blit-crop"
```

---

### Task 5: Visible-first avatar requests

**Files:**
- Modify: `src/FriendOverlay/UI/OverlayPanel.cs` — during `DrawList`, after computing scroll/inner, build arrays of offsets/heights from `_items`, compute `VisibleRowRange`, collect `UserId`s for row items in `[start, end)`, store in static `HashSet<ulong> _avatarPriorityIds` (clear + refill each draw). Expose `public static bool IsAvatarPriority(ulong userId)` — if set is empty (list not drawn yet), return **true** for first 24 snapshot rows only via separate bootstrap in GameAssets, OR: if empty, return true (allow load until first draw). Prefer: **empty set ⇒ allow all** for the first ticks before Draw; once Draw runs, set becomes authoritative. Add `static int _avatarPriorityFrame` stamped with `Time.frameCount` when updated; if `Time.frameCount - _avatarPriorityFrame > 2`, treat as stale and allow all (avoid stuck empty after tab switch).
- Modify: `src/FriendOverlay/UI/GameAssets.cs` — in `RequestMissingAvatars` loop, skip starting new loads when `!ImguiFriendOverlay.IsAvatarPriority(row.UserId)` (still count letters/cached images for summary). Frame refs for non-priority rows: also skip `SharedImageCache.Request` until priority.

**Interfaces:**
- Consumes: `VisibleRowRange.Compute`
- Produces: `ImguiFriendOverlay.IsAvatarPriority(ulong userId)`

- [ ] **Step 1: Publish priority set from DrawList**

After `scrollY` is known and `_items` non-empty, before the draw loop:

```csharp
var offsets = new float[_items.Count];
var heights = new float[_items.Count];
for (var i = 0; i < _items.Count; i++)
{
    offsets[i] = _items[i].Offset;
    heights[i] = _items[i].Height;
}
var (a, b) = VisibleRowRange.Compute(scrollY, inner.height, offsets, heights, marginItems: 2);
_avatarPriorityIds.Clear();
for (var i = a; i < b; i++)
{
    var row = _items[i].Row;
    if (row != null)
        _avatarPriorityIds.Add(row.UserId);
}
_avatarPriorityFrame = Time.frameCount;
```

```csharp
public static bool IsAvatarPriority(ulong userId)
{
    if (_avatarPriorityIds.Count == 0)
        return true;
    if (Time.frameCount - _avatarPriorityFrame > 2)
        return true;
    return _avatarPriorityIds.Contains(userId);
}
```

Clear the set in `OnSessionEnd`.

- [ ] **Step 2: Filter GameAssets.RequestMissingAvatars**

Inside the for-loop, after reading `row`:

```csharp
if (!ImguiFriendOverlay.IsAvatarPriority(row.UserId))
    continue;
```

(Place before frame/portrait requests so off-screen rows do not enqueue bake.)

- [ ] **Step 3: Build**

Run: `dotnet build src/FriendOverlay/FriendOverlay.csproj -c Release`  
Expected: 0 errors

- [ ] **Step 4: Commit**

```bash
git add src/FriendOverlay/UI/OverlayPanel.cs src/FriendOverlay/UI/GameAssets.cs
git commit -m "perf: load avatars for visible rows only"
```

---

### Task 6: Abort downloads + UnloadUnusedAssets on close

**Files:**
- Modify: `src/FriendOverlay/UI/AvatarLoader.cs` — track `Dictionary<string, UnityWebRequest> _requests` (or list); on Start store request; in `Reset()`, foreach Abort+Dispose, clear dict, then existing generation bump. In Download finally, remove from dict.
- Modify: `src/FriendOverlay/State/OverlaySession.cs` — after `ResetAvatarPipeline()` in `End` (and FailOpen path that clears avatars), schedule unload once:

```csharp
MelonCoroutines.Start(UnloadAfterClose());
```

```csharp
private static System.Collections.IEnumerator UnloadAfterClose()
{
    yield return null;
    try { Resources.UnloadUnusedAssets(); }
    catch { /* ok */ }
}
```

Guard with static `_unloadQueued` so double End in one frame only schedules once; clear flag when coroutine runs.

- [ ] **Step 1: AvatarLoader request tracking + Abort in Reset**

Keep `_generation++` behavior. Before bumping, abort all tracked requests.

- [ ] **Step 2: Schedule UnloadUnusedAssets from End**

Only on actual close (`End` / `FailOpen` avatar reset), not on every mid-session Begin panel swap if that would hitch while reopening — **do** unload on panel-swap reset as well per spec ("every session teardown … and mid-session panel-swap reset"). One hitch on swap is OK.

- [ ] **Step 3: Build**

Run: `dotnet build src/FriendOverlay/FriendOverlay.csproj -c Release`

- [ ] **Step 4: Commit**

```bash
git add src/FriendOverlay/UI/AvatarLoader.cs src/FriendOverlay/State/OverlaySession.cs
git commit -m "perf: abort avatar downloads and UnloadUnusedAssets on close"
```

---

### Task 7: Version 0.3.23 + 玩家说明

**Files:**
- Modify: `src/FriendOverlay/FriendOverlay.csproj` — `<Version>0.3.23</Version>`
- Modify: `src/FriendOverlay/FriendOverlayMod.cs` — MelonInfo version `"0.3.23"`
- Modify: `玩家说明.md` — version header; note default static official avatars, MelonPreferences `AnimatedOfficialAvatars`, closing panel clears avatar cache for match FPS

- [ ] **Step 1: Bump versions and docs**

玩家说明 additions under 说明:

```markdown
- **性能：** 关闭好友面板会释放头像缓存（下次打开需重新加载）。官方动态头像默认只显示静态帧；可在 MelonPreferences → Friend Overlay → AnimatedOfficialAvatars 开启动画。
```

Set version line to `0.3.23`.

- [ ] **Step 2: Build + full Core tests**

Run:

```bash
dotnet test tests/FriendOverlay.Tests/FriendOverlay.Tests.csproj -v n
dotnet build src/FriendOverlay/FriendOverlay.csproj -c Release
```

Expected: all tests PASS, build OK

- [ ] **Step 3: Commit**

```bash
git add src/FriendOverlay/FriendOverlay.csproj src/FriendOverlay/FriendOverlayMod.cs 玩家说明.md
git commit -m "release: FriendOverlay 0.3.23 stutter/memory mitigations"
```

---

## Spec coverage (self-review)

| Spec item | Task |
|-----------|------|
| Abort downloads on Reset | Task 6 |
| UnloadUnusedAssets once per close | Task 6 |
| Idle after close (verify gates) | Task 5 clear + existing `_opened` |
| Bake budget 2/frame | Tasks 1, 4 |
| Visible-first ±2 | Tasks 2, 5 |
| HTTP concurrency unchanged | Task 6 (abort only) |
| AnimatedOfficialAvatars default false | Task 3 |
| Version + 玩家说明 | Task 7 |
| Unit tests budget / GIF target / loader | Tasks 1–3 (loader abort not unit-tested; Unity) |

## Placeholder scan

None intentional. Exact paths and code included.

## Type consistency

- `BakeBudget`, `VisibleRowRange`, `OverlayPerfSettings` / `TargetGifFrameCount` names used consistently across tasks.
