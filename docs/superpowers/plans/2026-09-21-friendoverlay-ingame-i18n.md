# FriendOverlay In-Game Five-Language UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship FriendOverlay 0.3.40 with five-language in-game UI that follows the **game** language (zh-CN / en / ru / ja / de), embedded string tables, and language-aware fonts without embedding extra font files.

**Architecture:** Pure Core owns `LanguageResolver`, `UiCatalog` + `L.T`/`L.Tf`, and `FontSelector` policy. Melon layer injects `ILanguageSource` (prefer game API if already safe, else `Application.systemLanguage`), polls while overlay is visible, rebuilds font only when the language **code** changes, and replaces every user-visible Chinese literal with `L.T`/`L.Tf`.

**Tech Stack:** C# netstandard2.0 Core, MelonLoader IL2CPP Unity IMGUI, xUnit in `tests/FriendOverlay.Tests`

**Working branch:** none (working tree only, no commits)

## Global Constraints

- Spec: `docs/superpowers/specs/2026-09-21-friendoverlay-ingame-i18n-design.md` (REVISED)
- Follow **game** language, not manager UI
- Codes: `zh-CN`, `en`, `ru`, `ja`, `de`; unknown → `en`; TraditionalChinese → `en`
- Missing key: current → `en` → key
- Fonts: keep embedded Noto SC for zh-CN only; ja → Yu Gothic/Meiryo; en/de/ru → Segoe UI/Arial; then SC → Game → Skin
- Rebuild font only when `LanguageCode` changes; destroy old Font
- Version **0.3.40**
- No Commit steps; prefer working tree
- Ship DLL → `D:\gongzuo\独立工作区\MechabellumMods\mods\friend-overlay\FriendOverlay.dll` + update `catalog.json`

## File Structure

| File | Role |
|------|------|
| `src/FriendOverlay.Core/LanguageResolver.cs` | Map signals → code; hold `Current` |
| `src/FriendOverlay.Core/UiCatalog.cs` | Five locale dictionaries + lookup |
| `src/FriendOverlay.Core/L.cs` | `T` / `Tf` thin accessors |
| `src/FriendOverlay.Core/FontSelector.cs` | Probe samples + system face names + resolve order |
| `src/FriendOverlay.Core/UiFontResolvePolicy.cs` | Extend `Choose` for language-aware order |
| `src/FriendOverlay/I18n/ILanguageSource.cs` | Injectable language signal |
| `src/FriendOverlay/I18n/UnityLanguageSource.cs` | `Application.systemLanguage` (+ optional game API) |
| `src/FriendOverlay/I18n/OverlayLanguage.cs` | Poll + apply code + invalidate font |
| Melon UI/Data files | Replace CJK user strings with `L.T` |
| `tests/FriendOverlay.Tests/*` | Resolver, catalog parity/fallback, FontSelector |

---

### Task 1: LanguageResolver + tests

**Files:**
- Create: `src/FriendOverlay.Core/LanguageResolver.cs`
- Create: `tests/FriendOverlay.Tests/LanguageResolverTests.cs`
- Modify: `src/FriendOverlay/FriendOverlay.csproj` (link Core file)

**Interfaces:**
- Produces: `LanguageResolver.Current` (`string`, default `"en"`); `ToCode(string? signal)`; `ToCode(int unitySystemLanguage)` using Unity enum ints (Chinese=6, ChineseSimplified=40, ChineseTraditional=41, English=10, German=15, Japanese=22, Russian=30)

- [ ] **Step 1: Write failing tests**

```csharp
[Theory]
[InlineData("Chinese", "zh-CN")]
[InlineData("ChineseSimplified", "zh-CN")]
[InlineData("zh", "zh-CN")]
[InlineData("ChineseTraditional", "en")]
[InlineData("English", "en")]
[InlineData("Russian", "ru")]
[InlineData("Japanese", "ja")]
[InlineData("German", "de")]
[InlineData("Korean", "en")]
[InlineData(null, "en")]
public void ToCode_maps_signals(string? signal, string expected)
{
    Assert.Equal(expected, LanguageResolver.ToCode(signal));
}

[Theory]
[InlineData(40, "zh-CN")] // ChineseSimplified
[InlineData(41, "en")]    // ChineseTraditional
[InlineData(6, "zh-CN")]  // Chinese
[InlineData(10, "en")]
[InlineData(30, "ru")]
[InlineData(22, "ja")]
[InlineData(15, "de")]
[InlineData(23, "en")]    // Korean
public void ToCode_maps_unity_system_language_int(int value, string expected)
{
    Assert.Equal(expected, LanguageResolver.ToCode(value));
}
```

- [ ] **Step 2: Run** `dotnet test tests/FriendOverlay.Tests --filter LanguageResolverTests` → FAIL (type missing)
- [ ] **Step 3: Implement `LanguageResolver`**
- [ ] **Step 4: Run tests → PASS**

---

### Task 2: UiCatalog + L + parity/fallback tests

**Files:**
- Create: `src/FriendOverlay.Core/UiCatalog.cs`
- Create: `src/FriendOverlay.Core/L.cs`
- Create: `tests/FriendOverlay.Tests/UiCatalogTests.cs`

**Interfaces:**
- Produces: `UiCatalog.T(string key)`, `UiCatalog.Tf(string key, params object[] args)`; `L.T` / `L.Tf` read `LanguageResolver.Current`; `UiCatalog.AllKeys` for tests; five tables with identical key sets

**Key inventory (minimum — all user-facing CJK from UI/Data):**

| Key | zh-CN source |
|-----|--------------|
| `tab.following` | 关注 |
| `tab.followers` | 关注我的人 |
| `btn.close` | 关闭 |
| `btn.native` | 原生面板 |
| `btn.refresh` | 刷新 |
| `btn.cancel` | 取消 |
| `btn.confirm` | 确认 |
| `btn.join` | 加入 |
| `btn.watch` | 观战 |
| `btn.chat` | 私聊 |
| `btn.invite` | 邀请 |
| `btn.inviting` | 邀请中 |
| `btn.invited` | 已邀请 |
| `btn.follow_back` | 回关 |
| `btn.mutual` | ⇌ 已互关 |
| `menu.pin` | 置顶 |
| `menu.unpin` | 取消置顶 |
| `menu.pin_full` | 置顶（已满 {0}） |
| `menu.unfollow` | 取消关注 |
| `menu.blacklist` | 加入黑名单 |
| `search.placeholder` | 输入名称或 ID… |
| `filter.all` / `.online` / `.busy` / `.mutual` / `.offline` | 全部/在线/忙碌/互关/离线 |
| `section.pinned` / `.joinable` / `.busy` / `.offline` | 置顶 / 在线 · 可加入 / 在线 · 对战中 / 离线 |
| `sort.prefix` | 排序 · {0} |
| `sort.name` / `.rank` / `.forecast` / `.status` | 名称 A-Z / 战力 ↓ / 洞察 ↓ / 状态 |
| `count.loaded` | 已加载 {0} |
| `count.following` | 已关注 {0} |
| `status.ready` | 准备中 |
| `status.syncing` | 同步中 |
| `status.syncing_n` | 同步中 {0}/{1} |
| `status.synced_n` | 已同步 {0}/{1} |
| `status.loaded_unknown` | 已加载 {0} · 状态未知 |
| `status.showing` | 显示 {0} / {1} |
| `status.unknown` | 状态未知 |
| `status.state_n` | 状态{0} |
| `empty.loading_following` | 正在拉取关注列表… |
| `empty.loading_followers` | 正在拉取关注我的人… |
| `empty.no_friends` | 没有符合条件的好友 |
| `empty.no_players` | 没有符合条件的玩家 |
| `launcher.open` | 打开叠加面板  ·  {0} |
| `footer.help` | (long tip; shorter OK for non-zh) |
| `footer.letters` |  ·  Ctrl+L 字母模式（诊断） |
| `confirm.unfollow_title` / `_body` | 取消关注 / 确认取消关注 {0} ？ |
| `confirm.blacklist_title` / `_body` | 加入黑名单 / 确认拉黑 {0} ？ |
| `picker.title` | 邀请 {0} 参与 |
| `picker.unavailable` | {0}（暂未开放） |
| `battle.vs1v1` … `battle.rift2v2` | catalog labels |
| `state.idle` … `state.offline` | FriendStatusMapper labels |

- [ ] **Step 1: Failing tests** `AllKeys_have_five_locales`, `Missing_falls_back_to_en`, `Missing_en_returns_key`, sample zh/en lookups
- [ ] **Step 2: Implement tables + L** (full five-locale copy for every key)
- [ ] **Step 3: Tests PASS**

---

### Task 3: FontSelector + UiFontResolvePolicy language order

**Files:**
- Create: `src/FriendOverlay.Core/FontSelector.cs`
- Modify: `src/FriendOverlay.Core/UiFontResolvePolicy.cs`
- Modify: `tests/FriendOverlay.Tests/UiFontResolvePolicyTests.cs`
- Create: `tests/FriendOverlay.Tests/FontSelectorTests.cs`

**Interfaces:**
- Produces: `FontSelector.ProbeSample(code)`; `FontSelector.SystemFontNames(code)`; `UiFontResolvePolicy.Choose(code, useGameFont, notoOk, systemOk, gameOk)` → `Noto` first for zh-CN; `System` first for ja/en/de/ru; game-first when `useGameFont`

- [ ] **Step 1: Failing tests** for probe samples, name lists, choose order per language
- [ ] **Step 2: Implement; keep backward-compatible overload or update all callers
- [ ] **Step 3: Tests PASS**

---

### Task 4: Melon language source + poll + font invalidate

**Files:**
- Create: `src/FriendOverlay/I18n/ILanguageSource.cs`
- Create: `src/FriendOverlay/I18n/UnityLanguageSource.cs`
- Create: `src/FriendOverlay/I18n/OverlayLanguage.cs`
- Modify: `src/FriendOverlay/FriendOverlayMod.cs` — call `OverlayLanguage.Tick()` in `OnUpdate`
- Modify: `src/FriendOverlay/UI/GameAssets.cs` — language-aware `ResolveFont`; `InvalidateFontResolve(bool destroy)` destroy previous Font
- Modify: `src/FriendOverlay/UI/EmbeddedFontLoader.cs` — `TryCreateSystem(string[] names, string probe)`

**Interfaces:**
- `ILanguageSource.ReadSignal()` → string (e.g. `"ChineseSimplified"`)
- `OverlayLanguage.Tick()`: if overlay visible, poll every 2s or on first show; `LanguageResolver.ToCode`; if code changed → set Current + `GameAssets.InvalidateFontForLanguage()`
- Font path uses `FontSelector` + `UiFontResolvePolicy.Choose(LanguageResolver.Current, …)`

- [ ] **Step 1: Implement source + OverlayLanguage**
- [ ] **Step 2: Wire GameAssets / EmbeddedFontLoader**
- [ ] **Step 3: Build Melon project compiles**

---

### Task 5: Replace UI/Data hardcoded Chinese with L.T

**Files:**
- Modify: `OverlayPanel.cs`, `RowCard.cs`, `SearchBox.cs`, `SectionHeader.cs`, `SegmentedChips.cs`, `ImguiConfirm.cs`, `ImguiBattleTypePicker.cs`, `FriendStatusMapper.cs`, `FriendRowMapper.cs`, `FriendListService.cs`, `FansListService.cs`, `BattleType.cs` (labels → keys), `BattleTypeCatalogTests.cs` (assert keys)

- [ ] **Step 1: Replace every user-visible literal** with `L.T` / `L.Tf`
- [ ] **Step 2: Self-check grep for CJK in those UI files (allow font probe + comments only)
- [ ] **Step 3: Update BattleType tests for label keys**

---

### Task 6: Version 0.3.40 + Release DLL + catalog

**Files:**
- Modify: `FriendOverlay.csproj` Version, `FriendOverlayMod.cs` MelonInfo
- Copy DLL → MechabellumMods `mods/friend-overlay/FriendOverlay.dll`
- Modify: `MechabellumMods/catalog.json` version, sha256, size, updatedAt, zh/en/de/ja/ru summaries (UI follows game language, five langs)

- [ ] **Step 1: Bump version strings to 0.3.40**
- [ ] **Step 2: `dotnet build src/FriendOverlay/FriendOverlay.csproj -c Release`**
- [ ] **Step 3: Copy DLL; compute sha256 + size; update catalog**
- [ ] **Step 4: `dotnet test tests/FriendOverlay.Tests -c Release` → all green**

---

## Spec coverage self-check

| Spec item | Task |
|-----------|------|
| Follow game language | Task 4 |
| Five codes + unknown/trad → en | Task 1 |
| Embedded tables + L.T/L.Tf | Task 2 |
| Font policy (no extra embeds) | Task 3–4 |
| Rebuild font on code change only | Task 4 |
| Version 0.3.40 + catalog | Task 6 |
| No CJK user literals | Task 5 |
| Core tests | Tasks 1–3 |

## Execution note

This plan has **no Commit steps** — work stays in the working tree.
