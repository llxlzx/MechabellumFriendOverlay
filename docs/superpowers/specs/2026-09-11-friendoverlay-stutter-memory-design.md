# FriendOverlay stutter / memory (close-panel release + open budget)

## Context

Players report the game stays stuttery after closing the friends overlay and gets worse over a session. Evidence from a live install: FriendOverlay 0.3.22 dominated MelonLoader traffic; opening ~212 friends triggered hundreds of GPU readbacks (4096 atlas blit / `CaptureRoot`); process private memory ~10 GB after a long session. Other mods were quieter and unchanged that day.

Player preference (approved): **free memory immediately on close so matches stay smooth**; slower avatar reload on next open is acceptable. Approach **B** approved.

## Goal

1. Closing the friends panel releases FriendOverlay-owned GPU/managed avatar resources promptly enough that match play does not keep degrading.
2. Opening the panel no longer does a full-list synchronous bake spike; avatars fill in under a per-frame budget, visible rows first.
3. Official GIF multi-frame bake is **opt-in**; default is a single static frame.

## Non-goals

- Disk / cross-session avatar cache
- Changes to DamageRank or any other mod
- Rewriting the IMGUI panel or native friends UI
- Guaranteeing zero hitch on `Resources.UnloadUnusedAssets` (one short hitch on close is acceptable)

## Relationship to prior specs

Amends default behavior from `2026-09-11-animated-official-avatar-design.md`: multi-frame official GIF bake remains supported but is gated by preference `AnimatedOfficialAvatars` (default `false`). When enabled, existing frame-flip draw path still applies, subject to the bake budget below.

## 1. Close path

On every session teardown (`OverlaySession.End` → `ResetAvatarPipeline`, and the mid-session panel-swap reset):

| Step | Behavior |
|------|----------|
| Existing clears | Keep `SharedImageCache.Clear`, `AvatarCache.Clear`, `LiveGifHost.Clear`, `SpriteCapture.Reset`, resolver/policy resets. |
| Abort downloads | `AvatarLoader.Reset` must abort in-flight `UnityWebRequest`s (not only bump generation). Late completions still destroy undelivered textures (already via generation / `SafeSink`). |
| Unload | After clears, schedule **at most one** `Resources.UnloadUnusedAssets()` per close (same frame or next frame). Do not spam Unload while the panel is open. |
| Idle after close | `FriendListService` / `FansListService` stay gated on `_opened`; `OnGUI` early-outs when `Panel == null`. Re-verify no avatar bake path runs with panel closed. |

## 2. Open budget + visible-first

While the overlay session is open:

1. **Bake budget**: a process-wide counter reset each frame (e.g. max **2** expensive ops: `SpriteCapture.CaptureRoot` and atlas `blit-crop` / equivalent GPU readback). Callers that would exceed the budget leave the request pending for a later frame.
2. **Visible-first**: `GameAssets.RequestMissingAvatars` (or its caller) only starts loads for rows in the current list viewport, plus a small prefetch margin (e.g. ±2 rows). Off-screen unfinished work does not consume bake budget.
3. **HTTP downloads**: keep the existing concurrency cap; separate from bake budget. Still aborted on close.
4. **Placeholders**: rows keep letter / placeholder until bake or download completes; scrolling and invites must not wait on bake.

## 3. GIF / animation preference

| Item | Default | Notes |
|------|---------|--------|
| `AnimatedOfficialAvatars` MelonPreferences entry | `false` | When false, `LiveGifHost` bakes **one** frame and finishes. When true, existing multi-frame temporal bake runs, still under §2 budget. |
| Outline / frames | single frame | Unchanged |
| Steam photo URLs | download path | Unchanged; not GIF bake |
| Close | destroy all owned frames | Same as today via cache clear |

Wire the preference in `FriendOverlayMod` like other overlay prefs. Document in `玩家说明.md`.

## 4. Version / docs

- Bump MelonInfo / assembly version (e.g. `0.3.23`).
- `玩家说明.md`: note default static official avatars, optional animation toggle, and that closing the panel clears the avatar cache.

## Testing

**Unit (where logic is pure):**

- Bake budget: a third expensive op in the same frame is deferred / denied.
- GIF: default path yields a single-frame result; preference on allows multi-frame (cap still applies).
- `AvatarLoader.Reset`: generation / abort path does not hand a texture into a closed session (extend existing generation tests if present).

**Manual smoke:**

1. Open friends → list scrolls / invites immediately; avatars appear over seconds without multi-second freezes.
2. Close friends → enter a match; private memory drops vs peak; stutter does not keep worsening over the match.
3. Default: official avatars static. Enable `AnimatedOfficialAvatars`: motion returns, still budgeted.

## Success criteria

- Close path always aborts downloads and triggers a single UnloadUnusedAssets.
- Open path never unbounded full-list GPU bake in one frame.
- Default memory footprint for official keys is one texture per key (plus outline), not N GIF frames.
