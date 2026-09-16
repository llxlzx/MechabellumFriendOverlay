# FriendOverlay 0.3.34 — Font, invite safe area, auto-close on match — Design

**Date:** 2026-09-16  
**Status:** Approved for planning (brainstorming complete)  
**Current version:** 0.3.33 → target **0.3.34**  
**Repo:** `MechabellumFriendOverlay` (IMGUI overlay; MelonLoader)

## Goal

Ship a player-facing update that:

1. Uses a clearer, more polished CJK font across the entire friend overlay.
2. Stops the large overlay from covering invite prompts / bottom profile chrome.
3. **Auto-closes** the overlay when the local player leaves the lobby into loading / match (option A).

## Non-goals

- Auto-reopen when returning to lobby (rejected B).
- Minimize-to-chip while in match (rejected C).
- Rewrite the overlay from IMGUI to uGUI.
- Close the overlay solely because a friend sent an invite (not selected).

## Approach (locked)

**Scheme 1:** YaHei-family default font + shorter default window with bottom safe area + leave-lobby → `OverlaySession.End()`.

## 1. Font

**Files:** `UI/Theme.cs`, `UI/GameAssets.cs`, prefs in `FriendOverlayMod.cs`

- Default UI font resolution order:
  1. `Microsoft YaHei UI`
  2. `Microsoft YaHei` / `微软雅黑`
  3. Existing borrow of game `FriendCellNode.nameLabel.font` (when available)
  4. `null` → inherit `GUI.skin.font` (CJK fail-open)
- All `Theme` text styles continue to assign `GameAssets.UiFont` in `Make(...)`.
- Prefer shipping so players see YaHei without hunting prefs; keep a pref to force “game font only” if already present, defaulting to the new resolution path.
- Keep existing size ladder (Title / Tab / Name / Stat / Meta / Button / …) and `UiScale` / 1080p scale.

## 2. Invite / bottom chrome occlusion

**Files:** `UI/OverlayPanel.cs` (`ImguiFriendOverlay.EnsureWindow`), prefs load/migrate in `FriendOverlayMod.cs`, `UI/InputShield.cs` (behavior keep)

**Root cause:** Default rect ~`(60, 48, 900×740)` at 1080p covers a large left slab; invite copy / actions sit mid-bottom; InputShield mirrors the window (fullscreen only while drag/resize/modals).

**Changes:**

- New default size: width ~`900`, height ~`520–560` (scaled via `Theme.S`), top-left anchor unchanged in spirit (`~60, 48`).
- Bottom safe area: ensure default bottom edge leaves room for invite strip + bottom-left self profile (target: bottom margin roughly ≥ ~280–360px at 1080p depending on final height pick — validate against lobby screenshot layout).
- Migration: if persisted `WindowW/H` (or Y+H) clearly overflows the safe area (e.g. covers bottom ~280px band), clamp once on upgrade to 0.3.34; do not wipe a player’s preferred X/Y if still valid.
- `InputShield` stays panel-sized in steady state; fullscreen only for drag/resize/confirm/picker; on release, immediately resync to window rect.

## 3. Auto-close when entering loading / match

**Files:** `FriendOverlayMod.OnUpdate` (or small helper e.g. `State/LobbyPresence.cs`), `State/OverlaySession.cs` (`End`), possibly light Harmony if polling is insufficient

**Behavior:** When overlay session is active and we detect **local player left lobby UI into loading/match**, call `OverlaySession.End()` (closes IMGUI window, destroys InputShield, restores native friend layer state as today).

**Detection (implementation order):**

1. Strengthen existing fail-safe: `Panel == null` / friend panel torn down → `End`.
2. Add leave-lobby signals suitable for Mechabellum (scene change away from lobby HUD, and/or battle/loading UI presence, and/or game state proxies already used elsewhere). Prefer read-only probes + polling in `OnUpdate` first; add Harmony only if probes are unreliable.
3. Guard against false positives while still on the multiplayer lobby friend screen (must not close merely because party queue text changes).

**Explicitly not done:** reopen on return to lobby; keep overlay during match as a corner chip.

## Version & docs

- Bump `MelonInfo` / csproj / `玩家说明.md` to **0.3.34**.
- Player notes: font default, shorter window / safe area, auto-close on enter match.

## Success criteria

- [ ] Overlay text uses YaHei UI (or documented fallback) on a typical Windows CN install.
- [ ] Fresh install default window does not cover the invite line / bottom-left profile on 1080p lobby.
- [ ] With overlay open, starting a match (loading screen) clears the overlay within one or two frames of leave-lobby detection; no leftover InputShield.
- [ ] Returning to lobby does not auto-popup overlay; friend button still opens as today.
- [ ] Version strings read 0.3.34.

## Risks

- Leave-lobby false positive → close while still in lobby: mitigate with dual signals / whitelist lobby presence.
- Persisted huge window: one-time clamp may surprise power users — log once when migrating.
- OS without YaHei: must fall back silently to game/skin font.

## Implementation next

After user confirms this written spec: writing-plans (multi-task: font, geometry, auto-close, version) then implement on a feature branch off current FriendOverlay tip.
