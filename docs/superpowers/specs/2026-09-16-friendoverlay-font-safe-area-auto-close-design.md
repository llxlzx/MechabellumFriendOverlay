# FriendOverlay 0.3.34 — Font, invite safe area, auto-close on match — Design

**Date:** 2026-09-16  
**Status:** Approved for implementation (self-check amendments applied 2026-09-16)  
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

**Self-check (code fact):** Today `UseGameFont=false` forces `UiFont=null` (inherit `GUI.skin`) because an old comment claimed only the skin is CJK-safe. `UseGameFont=true` resolves **game font first**, then YaHei — so enabling the pref can still look “game-ugly”. Players who never touch prefs never get YaHei.

**Locked semantics for 0.3.34:**

- **Default path (always resolve a preferred UI font):**
  1. `Microsoft YaHei UI`
  2. `Microsoft YaHei` / `微软雅黑`
  3. Borrow game `FriendCellNode.nameLabel.font` if available
  4. `null` → inherit `GUI.skin.font` (CJK fail-open)
- Pref `UseGameFont` (keep name): when **true**, prefer game `nameLabel.font` **before** YaHei (power users who want exact game look). Default remains **false** → YaHei-first path above.
- If `CreateDynamicFontFromOSFont` returns null or throws → fall through; never leave the overlay unreadable.
- Invalidate/rebuild `Theme` styles when the resolved font changes (existing EnsureStyles path).
- Keep size ladder and `UiScale` / 1080p scale.

## 2. Invite / bottom chrome occlusion

**Files:** `UI/OverlayPanel.cs` (`ImguiFriendOverlay.EnsureWindow`), prefs load/migrate in `FriendOverlayMod.cs`, `UI/InputShield.cs` (behavior keep)

**Root cause:** Default rect ~`(60, 48, 900×740)` at 1080p covers a large left slab; invite copy / actions sit mid-bottom; InputShield mirrors the window (fullscreen only while drag/resize/modals).

**Changes (concrete):**

- New default rect at scale 1: **`(60, 48, 900, 540)`** via `Theme.S` → bottom at ~588 on 1080p (~492px free) so invite line + bottom-left profile stay clear.
- Min size unchanged in spirit (existing ~720×420 scaled clamp).
- One-shot migration pref e.g. `GeometryMigratedV034` (bool, default false):
  - If not migrated and persisted window bottom (`Y+H`) enters the bottom **280px** band of the screen (or height ≥ 700 at scale 1 equivalent), clamp height (and Y if needed) so bottom stays above that band; keep X when valid; log once; set migrated=true.
  - Fresh installs (`WindowW/H ≤ 1`) just get the new default; still set migrated=true.
- `InputShield` stays panel-sized in steady state; fullscreen only for drag/resize/confirm/picker; on mouse-up, immediately `SyncRect` to window.

## 3. Auto-close when entering loading / match

**Files:** `FriendOverlayMod` (`OnUpdate` + `OnSceneWasLoaded` if available), small helper e.g. `State/LobbyPresence.cs`, `State/OverlaySession.End`

**Behavior:** When overlay session is active and we detect **local player left lobby into loading / match**, call `OverlaySession.End()` (closes IMGUI, destroys InputShield, restores native layer as today).

**Self-check (code fact):** `Panel == null` alone is **not** enough — player screenshots show the overlay still drawing on the **match loading** screen, so the FriendPanel session can survive past lobby. Must add positive leave-lobby detection.

**Detection (locked order):**

1. Keep / strengthen fail-safe: `Panel == null` → destroy stray `InputShield`; hooks that already `End` on Hide/Release/Close stay.
2. **Primary:** Melon `OnSceneWasLoaded` / active-scene change while a session is open → if new scene is not the multiplayer lobby HUD scene, `End()`. Record lobby scene name when `OverlaySession.Begin` succeeds so comparison is concrete.
3. **Secondary (poll in OnUpdate, cheap):** if session open and lobby presence probe fails for N consecutive frames (e.g. 3) — e.g. friend-button / lobby canvas inactive, or known loading/battle root appears — `End()`.
4. **False-positive guard:** do **not** close solely because party queue text / “等待队长” changes; do **not** close on F8 native toggle; only leave-lobby / loading / battle.

If scene names prove unstable in testing, fall back to secondary probe as primary and document the chosen object/scene markers in the plan.

**Explicitly not done:** reopen on return to lobby; corner chip in match.

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

- Leave-lobby false positive → close while still in lobby: dual signals + lobby scene captured at Begin.
- Persisted huge window: one-time clamp may surprise power users — log once when migrating.
- OS without YaHei / OS font CJK issues in IMGUI: fall back to game font then `GUI.skin` (do not ship a broken blank-glyph default).
- Scene-name instability across game patches: secondary presence probe required.

## Spec self-check log (2026-09-16)

| Check | Result |
|-------|--------|
| Font vs current `UseGameFont` semantics | **Amended** — YaHei-first by default; pref true = game-first |
| Default geometry ambiguity 520–560 | **Amended** — lock **900×540** |
| Migration without wiping X/Y | OK + `GeometryMigratedV034` |
| Auto-close relying only on Panel null | **Amended** — insufficient; scene + presence probes |
| Invite-only close / reopen / chip | Still non-goals |
| Version / docs | OK |

## Implementation next

writing-plans → implement on feature branch (not `main`/`master`); TDD where practical (geometry clamp, font resolve order, leave-lobby End).
