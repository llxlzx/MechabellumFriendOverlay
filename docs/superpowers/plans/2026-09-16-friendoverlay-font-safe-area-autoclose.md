# FriendOverlay 0.3.34 Font / Safe Area / Auto-close Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans or implement inline. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Ship 0.3.34 with YaHei-first UI font, default 900×540 + geometry migration, auto-close overlay when leaving lobby into loading/match.

**Architecture:** Adjust `GameAssets` font resolve; clamp window defaults/migration in prefs + `EnsureWindow`; add `LobbyPresence` scene/presence probes that call `OverlaySession.End`.

**Tech Stack:** MelonLoader, Harmony, Unity IMGUI, xUnit tests in `tests/FriendOverlay.Tests`

**Working branch:** `feature/v0.3.34-font-safe-area-autoclose`

## Global Constraints

- Spec: `docs/superpowers/specs/2026-09-16-friendoverlay-font-safe-area-auto-close-design.md` (self-check amended)
- Default font: YaHei-first; `UseGameFont=true` → game font before YaHei
- Default window: `(60,48,900,540)` scaled; bottom safe band 280px; migrate once via `GeometryMigratedV034`
- Leave-lobby → `OverlaySession.End()`; no auto-reopen
- Version **0.3.34**

---

### Task 1: Font resolve (YaHei-first)

**Files:** `src/FriendOverlay/UI/GameAssets.cs`, tests if extractable pure helper

- [ ] Change `UiFont` so default (`UseGameFont=false`) still **resolves** (not null): YaHei UI → YaHei → game → null
- [ ] When `UseGameFont=true`: game font first, then YaHei chain
- [ ] Update comments; keep `Reset()` clearing resolve cache
- [ ] Commit: `feat: YaHei-first overlay font with game-font pref`

### Task 2: Default window + geometry migration

**Files:** `UI/OverlayPanel.cs`, `FriendOverlayMod.cs`

- [ ] `EnsureWindow` default `Theme.S(900)` × `Theme.S(540)`
- [ ] Pref `GeometryMigratedV034`; on load, clamp oversized bottoms; set migrated
- [ ] Commit: `fix: shorter default friend window and safe-area migration`

### Task 3: Leave-lobby auto-close

**Files:** new `State/LobbyPresence.cs`, `OverlaySession.Begin` capture scene, `FriendOverlayMod.OnUpdate` + `OnSceneWasLoaded`

- [ ] Capture lobby scene name at `Begin`
- [ ] Scene change away from lobby → `End()`
- [ ] Poll: session open + lobby presence lost N frames → `End()`
- [ ] Unit-test pure helpers (scene-name compare / should-close) where possible
- [ ] Commit: `fix: auto-close friend overlay when leaving lobby`

### Task 4: Version + 玩家说明

- [ ] Bump MelonInfo, csproj, 玩家说明 to 0.3.34
- [ ] Commit: `chore: FriendOverlay 0.3.34`
- [ ] `dotnet test tests/FriendOverlay.Tests/FriendOverlay.Tests.csproj`
