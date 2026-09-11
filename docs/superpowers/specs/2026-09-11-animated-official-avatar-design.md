# Animated official avatars / frames (IMGUI frame flip)

## Goal

Official GIF keys (`Avtr_G_*` and animated frames) loop in the friend list while scrolling, using the same IMGUI `Gfx.Texture` path as static portraits.

## Approved approach (A)

Bake all usable GIF frames to owned `Texture2D[]` in `SharedImageCache`. Draw with a shared unscaled clock:

`index = (int)(Time.unscaledTime / FrameSeconds) % Frames.Length`

Same `imageRef` stays in sync across rows. Default `FrameSeconds = 1/12`; override if `GifPara` exposes an interval later.

## Data

- `SharedImage.Frames` + `FrameSeconds`; static path keeps single `Texture`.
- `HasImage` true if `Texture` or any usable frame exists.
- Soft cap: 32 frames per key.
- `Clear()` destroys every owned frame texture.

## Load

1. `getGif` / `gifInfos` / `gifParas` → bake all frames (skip failed bakes; 0 frames = fail).
2. Else `SetPlayerPortrait` compose (static).
3. Log `game avatar gif: <key> frames=N` (rate-limited).

## Draw

`SharedImageCache.CurrentTexture(entry)` picks the clock frame (or static `Texture`). `RowCard.DrawAvatar` / `DrawFrame` both use it. Platform badge unchanged.

## Out of scope

Native uGUI GIF overlay; hover-only playback; removing platform badge; exact native millisecond timing.
