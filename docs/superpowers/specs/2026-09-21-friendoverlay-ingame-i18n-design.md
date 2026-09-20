# FriendOverlay 游戏内 UI 五语设计

**Status:** Approved + post-review revision (2026-09-21)  
**Date:** 2026-09-21  
**Repo:** MechabellumFriendOverlay → ships via MechabellumMods `friend-overlay`  
**Version target:** 0.3.40

## 已锁定决策

| 项 | 选择 |
|----|------|
| 语言跟谁 | **游戏语言**（不跟管理器 UI） |
| 语言集合 | `zh-CN` / `en` / `ru` / `ja` / `de` |
| 未知语言 | → `en` |
| 缺译 | 当前语缺键 → `en` → 再缺返回 key |
| 繁中 | → `en`（不加繁中表） |
| 字符串载体 | Core 嵌入式字典 + `L.T` / `L.Tf` |
| 字体 | 按语言切换；**非 zh-CN 优先系统字体**，仅保留现有嵌入 Noto Sans SC（避免再塞 JP/Sans 撑爆 DLL） |
| 非目标 | MelonPreferences 本地化；管理器语言同步；运行时下载字体 |

## 自检修订摘要（相对初稿）

| 风险 | 修订（最优自动项） |
|------|-------------------|
| 再嵌入 Noto JP + Noto Sans 会使 ~8.5MB DLL 再涨数 MB～数十 MB | **只保留已有 SC 嵌入**；`ja`→Yu Gothic/Meiryo；`en/de/ru`→Segoe UI/Arial；失败再 SC→Game→Skin |
| 每帧重建 Font 泄漏 / 卡顿 | 仅当 `LanguageCode` **变化**时重建；销毁旧 Font |
| 游戏本地化 API 不稳定 | `ILanguageSource`：先试游戏 API，失败用 `Application.systemLanguage`；测试可注入 |
| §3 繁中表述曾歧义 | 明确 **TraditionalChinese → en** |
| 战斗状态名与游戏内文案可能不一致 | 仍用自有五语表（可控）；不调用可能缺失的游戏翻译 API |
| 页脚超长提示在窄语言下溢出 | 允许按语言用略短文案；键仍同一套 |

## 1. 目标

游戏 UI 为中/英/俄/日/德时，叠加面板文案与字体跟语言一致。

## 2. 架构

```
ILanguageSource (game API | systemLanguage | test stub)
        │
        ▼
  LanguageResolver.ToCode(...)  →  zh-CN|en|ru|ja|de|→en
        │
        ├── UiCatalog (5 dicts, parity-tested) → L.T / L.Tf
        └── FontSelector(code) → EmbeddedFontLoader / system names
                 │
                 └── GameAssets.UiFont (rebuild on code change only)
```

- UI 层禁止硬编码用户可见中文（注释除外）。
- `L` 读 `LanguageResolver.Current`；无状态静态访问，便于现有 `const` 替换为属性/方法调用。

## 3. 语言映射

| 信号 | 码 |
|------|-----|
| Chinese / ChineseSimplified / zh / hans / cn / sg | `zh-CN` |
| ChineseTraditional / hant / tw / hk / mo | `en` |
| English / en* | `en` |
| Russian / ru* | `ru` |
| Japanese / ja* | `ja` |
| German / de* | `de` |
| 其它 | `en` |

轮询：在 Overlay 可见时每 **2s** 或打开面板时解析一次；码变化才刷字体。

## 4. 文案

键集合从源码扫出，五语齐；Core 测试：`AllKeys_have_five_locales`、`Missing_falls_back_to_en`、`Resolver_maps_*`。

## 5. 字体

| 码 | 顺序 |
|----|------|
| `zh-CN` | Noto SC（嵌入）→ YaHei → Game → Skin |
| `ja` | Yu Gothic UI / Yu Gothic / Meiryo → Noto SC → Game → Skin |
| `en`/`de`/`ru` | Segoe UI / Arial → Noto SC → Game → Skin |

探针：`zh`「钢铁ABC」；`ja`「あア漢字」；`en/de`「Ag」；`ru`「Ру」.

## 6–8. 测试 / 发版 / 验收

同前：Core 单测 + 冒烟；发 **0.3.40** DLL → catalog（五语 summary 含 UI 五语说明）→ Release → COS。

## 9. 非目标

不变。
