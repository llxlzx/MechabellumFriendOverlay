# FriendOverlay 0.3.35 — Embedded Noto Sans SC + edge/corner resize — Design

**Date:** 2026-09-17  
**Status:** Approved for planning (brainstorming complete; spike gate passed)  
**Current version:** 0.3.34 → target **0.3.35**  
**Repo:** `MechabellumFriendOverlay` (IMGUI overlay; MelonLoader / IL2CPP)

## Goal

Ship a player-facing update that:

1. Makes overlay CJK text clearly better-looking with **few missing glyphs**, using **Noto Sans SC** when load succeeds.
2. Lets players resize the overlay by dragging **edges and corners** (not only the bottom-right grip), while keeping title-bar move.

## Non-goals

- Re-validating leave-lobby auto-close (0.3.34 feature; out of this release’s required proof).
- Rewriting IMGUI → uGUI / TextMeshPro.
- Forcing bottom safe-area clamp while the user manually resizes taller (invite occlusion may return if they choose).
- Using `CreateDynamicFontFromOSFont` or `Internal_CreateFontFromPath` on the production resolve path (spike proved them unusable here).

## Spike gate (2026-09-16 in-game)

Logged as `[FontSpike]` on 0.3.34 build:

| API | Result |
|-----|--------|
| `CreateDynamicFontFromOSFont` | Fail — `Method unstripping failed` |
| `Internal_CreateFontFromPath` (e.g. `msyh.ttc`) | Object named but `mat=null`, `HasCharacter` **0/5** for `钢铁ABC` — unusable |
| `Internal_CreateFont` | Same dead font |
| **`Internal_CreateDynamicFont`** | **Pass** — YaHei UI, `dynamic=True`, glyphs **5/5** |
| `GetPathsToOSFonts` | Pass (paths available) |

**Locked load strategy:** family-name dynamic font via `Font.Internal_CreateDynamicFont`. Embedded Noto must be **registered with Windows** (`AddFontResourceEx`, private) then created by family name. Path-only `Internal_CreateFontFromPath` is not a production path.

## Approach (locked)

**Scheme 1 (amended by spike):** Embed Noto Sans SC → extract → private font register → `Internal_CreateDynamicFont` → YaHei dynamic fallback → game font → `GUI.skin`. Plus edge/corner resize hit zones on `ImguiFriendOverlay`.

---

## 1. Font

**Files:** `UI/GameAssets.cs` (resolve), optional small helper e.g. `UI/EmbeddedFontLoader.cs`, prefs in `FriendOverlayMod.cs`, `Theme.cs` invalidate on font change, packaging/resources, `OFL.txt`.

**Asset:**

- Ship **Noto Sans SC Regular** (OTF or TTF; full CJK, size unlimited per product choice).
- Embed as assembly resource **or** content file copied beside the DLL; either way first resolve extracts/copies to a stable writable path under Melon user data or mod folder (idempotent; log path once).
- Ship **`OFL.txt`** (SIL Open Font License) with the package / extract dir.

**Resolve order (default, `UseGameFont=false`):**

1. Ensure Noto file on disk → `AddFontResourceEx(path, FR_PRIVATE, 0)` (P/Invoke `gdi32`) → `new Font()` + `Font.Internal_CreateDynamicFont(font, names, size)` with names starting `Noto Sans SC` (and documented aliases if the face reports differently).
2. If step 1 fails: `Internal_CreateDynamicFont` with `Microsoft YaHei UI`, `Microsoft YaHei`, `微软雅黑` (spike-proven).
3. Borrow game `FriendCellNode.nameLabel.font` if available.
4. `null` → inherit `GUI.skin.font` (fail-open; never blank/crash).

**`UseGameFont=true`:** prefer game `nameLabel.font` first, then the same Noto/YaHei dynamic chain.

**Lifecycle:**

- Do **not** call stripped `CreateDynamicFontFromOSFont` on the production path.
- Do **not** treat `Internal_CreateFontFromPath` success-without-glyphs as success.
- If first resolve runs before lobby cells exist, **re-resolve once** when the overlay session opens / friend panel is available.
- On process exit or mod unload if practical: `RemoveFontResourceEx` for the private register (best-effort; ignore failures).
- Invalidate/rebuild `Theme` styles when the resolved font instance changes.
- Keep existing size ladder and `UiScale`.

**Cleanup:** Remove `UI/FontLoadSpike.cs` and its `OnUpdate` tick before shipping 0.3.35.

---

## 2. Edge / corner resize

**Files:** `UI/OverlayPanel.cs` (`CaptureChromeDrag`, `FollowDrag`, footer copy); optionally pure helpers in `FriendOverlay.Core` for hit-test / apply-delta (unit-testable).

**Behavior:**

- Keep **title-bar drag** (existing exclusion of right-side chrome buttons / tab strip).
- Keep **bottom-right grip** as SE resize (visual unchanged or minor).
- Add hit strips for **N, S, E, W** and corners **NE, NW, SE, SW** (~**6–8px**, via `Theme.S`).
- While resizing from **W / NW / SW**: adjust `x` and `width` together. From **N / NE / NW**: adjust `y` and `height` together. From **E / S / SE**: width/height only (current SE math).
- Min size remains ~**720×420** scaled; continue `ClampWindowToScreen` after each move.
- During drag/resize, existing fullscreen `InputShield` behavior stays.
- Persist window rect on prefs save as today.
- Footer hint: `拖动标题栏移动  ·  拖边框/四角缩放  ·  …` (replace “仅右下角缩放” wording).

**Safe area:** One-shot 0.3.34 geometry migration stays as-is. **Manual resize does not re-apply** the bottom 280px safe band (player may cover invite chrome again).

**Hit priority:** corner zones win over edge zones; resize grips win over title drag; tab strip / buttons still excluded from drag start.

---

## 3. Version, tests, release

**Version:** MelonInfo + `FriendOverlay.csproj` → **0.3.35**.

**Tests:**

- Core (or small pure helper) tests: edge/corner hit classification; apply mouse delta → expected rect (including west/north origin shifts); min-size clamp.
- Font loader tests with **injected** register/create delegates: success path picks Noto; register fail falls to YaHei create; all fail → null/borrow stub.
- Full existing suite green; remove spike-only code from Release.

**Manual / in-game:**

- Log once which face won (`Noto Sans SC` / YaHei / game / skin).
- Confirm CJK in title, rows, footer look sharper when Noto or YaHei dynamic wins.
- Drag each edge and corner; title move still works; prefs survive reopen.

**Release:**

- Build Release `FriendOverlay.dll`.
- Update local/community package metadata (version **0.3.35**, notes: embedded Noto + edge resize; OFL).
- README / package summary mention font + border resize briefly.

---

## Spec self-check log (2026-09-17)

1. **Placeholder scan:** No TBD left for load API — locked to `Internal_CreateDynamicFont` + private GDI register for Noto.
2. **Consistency:** Product choice “embed Noto” kept; spike forbids path-create as the load mechanism — register-then-dynamic bridges both.
3. **Scope:** Font + resize only; auto-close re-proof explicitly out.
4. **Ambiguity:** Safe-area during manual resize = **do not clamp** (explicit). Noto family name string locked to try `Noto Sans SC` first; if the shipped file’s OS name differs, loader logs actual face and accepts documented aliases in implementation without changing this goal.

## Working branch

Prefer `feature/v0.3.35-noto-edge-resize` (or continue from current feature branch with version bump). Implementation plan follows after user reviews this file.
