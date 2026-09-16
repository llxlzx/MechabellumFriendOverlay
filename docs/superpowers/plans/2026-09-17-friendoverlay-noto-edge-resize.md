# FriendOverlay 0.3.35 Noto Font + Edge Resize Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship 0.3.35 with embedded Noto Sans SC (register + `Internal_CreateDynamicFont`) and four-edge/corner window resize.

**Architecture:** Pure Core helpers for resize hit-test/apply-delta and font-resolve policy; Melon UI wires GDI `AddFontResourceEx` + Unity `Internal_CreateDynamicFont`, embeds Noto bytes, and extends `CaptureChromeDrag`/`FollowDrag`.

**Tech Stack:** MelonLoader IL2CPP, Unity IMGUI, P/Invoke gdi32, xUnit in `tests/FriendOverlay.Tests`

**Working branch:** `feature/v0.3.35-noto-edge-resize`

## Global Constraints

- Spec: `docs/superpowers/specs/2026-09-17-friendoverlay-noto-font-edge-resize-design.md`
- Production font path: **never** `CreateDynamicFontFromOSFont` / treat path-create as success; use `Internal_CreateDynamicFont` after private register for Noto
- Fallback: YaHei dynamic → game font → `GUI.skin`
- Resize: edges+corners ~6–8px (`Theme.S`); W/N adjust origin; min ~720×420 scaled; no safe-area re-clamp on manual resize
- Remove `FontLoadSpike` before ship
- Version **0.3.35**; ship `OFL.txt`
- Commit only on this feature branch (no push/PR in plan)

---

### Task 1: Core resize geometry helpers + tests

**Files:**
- Create: `src/FriendOverlay.Core/WindowResizeMath.cs`
- Create: `tests/FriendOverlay.Tests/WindowResizeMathTests.cs`
- Modify: `src/FriendOverlay/FriendOverlay.csproj` (link new Core file if needed — Core project may already include all `*.cs`)

**Interfaces:**
- Produces: `WindowResizeEdge` enum (`None,N,S,E,W,NE,NW,SE,SW`); `HitTest(float localX, float localY, float w, float h, float thickness)` → edge; `ApplyDelta(edge, x,y,w,h, dx,dy, minW,minH)` → `(x,y,w,h)`

- [ ] **Step 1: Add failing tests** for corner-over-edge priority, west/north origin shift, min clamp
- [ ] **Step 2: Run tests → expect FAIL**
- [ ] **Step 3: Implement `WindowResizeMath`**
- [ ] **Step 4: Run tests → PASS**
- [ ] **Step 5: Commit** `feat: window edge/corner resize math`

---

### Task 2: Wire edge/corner resize into OverlayPanel

**Files:**
- Modify: `src/FriendOverlay/UI/OverlayPanel.cs` (`CaptureChromeDrag`, `FollowDrag`, footer)
- Consumes: `WindowResizeMath`

- [ ] **Step 1: Replace single SE grip flag with edge enum**; title drag unchanged; grip = SE
- [ ] **Step 2: FollowDrag applies `ApplyDelta` using screen mouse delta (account for IMGUI Y flip like today)
- [ ] **Step 3: Update footer string to 拖边框/四角缩放**
- [ ] **Step 4: Build Release**; commit `feat: friend overlay edge and corner resize`

---

### Task 3: Font resolve policy helper + tests

**Files:**
- Create: `src/FriendOverlay.Core/UiFontResolvePolicy.cs`
- Create: `tests/FriendOverlay.Tests/UiFontResolvePolicyTests.cs`

**Interfaces:**
- Produces: `UiFontResolvePolicy.Choose(useGameFont, notoOk, yaheiOk, gameOk)` → enum `Noto, YaHei, Game, Skin` (ordered per spec)

- [ ] **Step 1: Failing tests** for default vs UseGameFont order
- [ ] **Step 2: Implement policy**
- [ ] **Step 3: Tests PASS**; commit `feat: UI font resolve policy for Noto/YaHei/game`

---

### Task 4: Embed Noto + GDI register + GameAssets resolve

**Files:**
- Create: `src/FriendOverlay/UI/EmbeddedFontLoader.cs` (extract resource, AddFontResourceEx/RemoveFontResourceEx, Internal_CreateDynamicFont wrappers)
- Modify: `src/FriendOverlay/UI/GameAssets.cs` (`ResolveFont` per policy; remove OS `CreateDynamicFontFromOSFont` production use)
- Modify: `src/FriendOverlay/FriendOverlay.csproj` — EmbeddedResource for font + copy OFL
- Create: `src/FriendOverlay/Fonts/OFL.txt`
- Create: `src/FriendOverlay/Fonts/NotoSansSC-Regular.otf` (download official Noto Sans SC Regular)
- Modify: `FriendOverlayMod.cs` — remove FontLoadSpike tick; optional re-resolve on session open hook if easy
- Delete: `src/FriendOverlay/UI/FontLoadSpike.cs`

- [ ] **Step 1: Download Noto Sans SC Regular + OFL** into `Fonts/`
- [ ] **Step 2: Implement loader + GameAssets resolve**; log winning face once
- [ ] **Step 3: Build Release**; commit `feat: embed Noto Sans SC via private GDI + Internal_CreateDynamicFont`

---

### Task 5: Version bump + 玩家说明 + package notes

**Files:** MelonInfo, csproj Version, `玩家说明.md` / README as existing 0.3.34 docs

- [ ] **Step 1: Bump to 0.3.35**; mention Noto + edge resize
- [ ] **Step 2: `dotnet test` full suite green**
- [ ] **Step 3: Commit** `chore: FriendOverlay 0.3.35`
- [ ] **Step 4: Deploy Release DLL** (`-p:Deploy=true`) for player verify
