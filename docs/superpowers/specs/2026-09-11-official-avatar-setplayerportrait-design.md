# Official avatar / frame load via GRAvatarManager + SetPlayerPortrait (0.3.9)

## Goal

Show official `Avtr_*` faces and `Af_*` frames in the IMGUI overlay the same way the native list does, without borrowing FriendCellNode sprites by list position.

## Approved approach (A + C)

1. **GIF first:** `GRAvatarManager.getGif(key)` → if non-empty, use `sprites[0]`.
2. Else **native portrait apply:** hidden `GRImage.SetPlayerPortrait("", outline, avatar)`:
   - Face key only: `("", "", key)` → read `avatarObj`
   - Frame key only (`Af_*` / `AF_*`): `("", key, "")` → read `outlineObj`
3. From the instantiated obj, pick a usable `Image`/`SpriteRenderer` sprite into `SharedImageCache`.
4. Manager missing → Retry (no Fail). Definitive miss → letter.

Photos stay on HTTP `AvatarLoader`. `PortraitPlanner` unchanged.

## Out of scope

Positional cell harvest; CDN/`LoadSprite` for bare keys; animating GIF beyond first frame.
