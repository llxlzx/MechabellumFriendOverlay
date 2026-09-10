# Mechabellum FriendOverlay

钢铁指挥官（Mechabellum）好友列表 QoL MelonMod：独立 IMGUI 叠加面板，数据与动作全部走游戏 `FriendProxy`。

## 功能

- 打开好友时默认隐藏原生面板，显示叠加列表
- 一键 / `F8` 切回原生好友窗
- 标题栏两个页签：**关注** / **关注我的人**（后者仅在游戏接口可用时出现）
- 搜索（名称 / UserId）、筛选（全部/在线/忙碌/互关/离线）、排序（状态 / 战力 / 洞察 / 名称），两个页签通用
- 按可用性分区：`在线 · 可加入` / `在线 · 对战中` / `离线`，分区可折叠且状态持久化
- **置顶**（仅「关注」页签）：`⋯` 菜单 置顶 / 取消置顶，最多 20 人，独立 `置顶` 分区排最前；只存本地偏好，不写游戏服务器
- 行卡片默认显示头像 / 名字 / 战力洞察 / 状态 / `⋯`；**悬停**出现 `加入 | 观战 | 私聊 | 邀请`。`⋯` 或行内**右键**打开菜单，取关与拉黑需确认
- **邀请**镜像原生行为：已在房间走 `LobbyProxy.InviteUserJoin`；不在房间弹出战斗类型选择，按所选类型 `CreateRoom(GameMode, MatchMode, isPrivate: true, SessionResponse)`，等到 `JoinedRoom != null && IsHost()` 再发邀请，10 秒等不到则放弃并解锁。全局只允许一个建房流程，期间该行显示 `邀请中`。对方离线或缺少接口时按钮禁用（不隐藏）；同一人 5 秒内不重复发送。组队匹配走 `TeamProxy.RequestTeamInvite`，不建房。七个类型的 `GameMode`/`MatchMode` 取自 `docs/superpowers/plans/2026-09-10-invite-trace-table.md` 的实机采集
- **关注我的人**：`回关`（已互关显示 `⇌ 已互关`）、加入 / 观战 / 私聊 / 邀请 / 拉黑；仅对互关的人提供取消关注；不提供置顶
- 未知在线状态按离线处理：游戏 `EPlayerState.Idle == 0`，所以没人填过的 `State` 会读成「空闲可加入」。「关注我的人」不信任列表自带的 `State`，只认 `RequestOnline` 的回报；拿不到就显示 `状态未知` 并禁用 加入 / 观战 / 邀请（`Core/Presence.cs`，日志 `followers states known=N/total`）
- 该页签计数为 `已加载 N`：`RequestLastFollower` 既无总数也无完成信号，两个数据源合并去重后按已拿到的计
- 头像显示与游戏一致：玩家在游戏里选了官方头像就显示官方头像，选了自定义照片就显示照片；同时绘制头像**边框**；来源解析走游戏自己的 `PlayerRiskInfo → PlayerPortraitInfo`
- 头像加载逐级回退：本地精灵 → URL 下载 → 首字母；先画字母再把图盖上去，任何一步失败都不会留下空框
- 跟随游戏的头像屏蔽设置：游戏判定为屏蔽的账号，叠加面板显示游戏替换后的占位图
- 叠加打开时挂全屏透明 uGUI 挡板，点击与拖动不再穿透到背后大厅
- 拖标题栏移动、右下角拖动缩放，位置尺寸写入偏好（关闭面板即落盘）
- 分页拉全关注列表，同步状态条带脉冲指示
- 逐功能能力探测（`inviteUserJoin / createRoom / teamInvite / followers / portrait`）：缺哪个接口只关哪个功能；整体仍 fail-open
- 失败则 fail-open：不打补丁或异常时恢复原生 UI

不做：Discord 邀请、「关注我的人」置顶、服务器端置顶、动态边框预制体、黑名单 / Discord 页签、本地备注 / 分组、对局内回合比分、强行互关。

## 界面与快捷键

- `F8` 叠加 / 原生互切，`Ctrl+F` 聚焦搜索，`Esc` 清空搜索或关闭 `⋯` 菜单，`R` 刷新当前页签（搜索未聚焦时）
- 标题栏页签「关注 | 关注我的人」点击切换
- `Ctrl+L` 诊断用：强制所有行显示首字母并隐藏边框，再按一次恢复；仅当前会话有效
- 行内右键 = `⋯`，都打开 置顶 / 取关 / 拉黑菜单
- 标题栏「关闭」走游戏原生关窗（`ClosePanelBtnOnClicked`），只收起弹窗，不动大厅好友入口
- 滚轮 / 拖动滚动条 / `PageUp` `PageDown` `Home` `End` 滚动
- 视觉系统集中在 `UI/Theme.cs`（调色板、`GUIStyle` 懒初始化）与 `UI/Gfx.cs`（切角、圆角、托架、扫描线等原语）；
  两者的每个 API 调用都带回退，`GUIStyle` 不可用时退回默认 skin，圆角 `DrawTexture` 不可用时退回扫描线光栅化
- 默认按屏幕高度自动缩放（1080p = 1x，4K = 2x），可用 `UiScale` 覆盖

## 构建

要求：本机已安装 MelonLoader，且生成过 `MelonLoader\Il2CppAssemblies`。

```powershell
dotnet test tests\FriendOverlay.Tests\FriendOverlay.Tests.csproj
dotnet build src\FriendOverlay\FriendOverlay.csproj -c Release

# 构建并部署（先清掉 Mods\ 里所有 FriendOverlay*.dll，再拷新的）
dotnet build src\FriendOverlay\FriendOverlay.csproj -c Release -p:Deploy=true
```

默认 `GameRoot`：`D:\steam\steamapps\common\Mechabellum`  
可用 `-p:GameRoot=你的路径` 覆盖。

## 安装

将 `src\FriendOverlay\bin\Release\FriendOverlay.dll` 复制到 `{GameRoot}\Mods\`（单文件，已内含 Core 查询管线）。

**`Mods\` 里只能有一个 FriendOverlay。** 拷贝前先删掉所有旧的 `FriendOverlay*.dll`（包括带版本号的副本），否则 MelonLoader 会把它们当成两个 mod 分别加载，Harmony 补丁会打两遍。`-p:Deploy=true` 会自动做这件事。

**管理器「已装 Mod」不会自动扫 `Mods\` 文件夹。** 需要任选其一：
1. 管理器 → **导入 DLL**，选中 `FriendOverlay.dll`
2. 或管理器重启后触发「从游戏导入」（把 `Mods\` 里未入库的 DLL 导入本地库）
3. 社区目录上线 `friend-overlay` 后，也可从目录安装

重启游戏。MelonLoader 日志应出现 `FriendOverlay hooks applied`；打开好友时应出现 `clickFriendBtn open=True`。

## 偏好

MelonPreferences 分类 `FriendOverlay`：

- `PreferOverlayDefault` — 打开好友时是否默认叠加
- `SortKey` — 排序枚举值
- `ToggleHotkey` — 默认 `F8`
- `Animations` — 淡入 / 脉冲 / 悬停动效开关
- `UiScale` — UI 缩放，`0` 表示按屏幕高度自动
- `UseGameFont` — 借用游戏内字体（默认关闭，默认字体是唯一确认能渲染中文的）
- `TransparentNativePanel` — 用 `CanvasGroup` 透明化原生面板而非 `SetActive(false)`（这是经过验证的隐藏路径，头像已不依赖它）
- `CollapseJoinable` / `CollapseBusy` / `CollapseOffline` — 分区折叠状态
- `CollapsePinned` — 置顶分区折叠状态
- `PinnedUserIds` — 置顶的 UserId，逗号分隔，最多 20 个，仅本地
- `WindowX` / `WindowY` / `WindowW` / `WindowH` — 窗口位置与尺寸

## 验收清单

1. 冷启动：日志 TypeProbe OK + hooks applied  
2. 打开好友：原生隐藏，叠加出现  
3. 切原生 / 再切叠加  
4. 关闭后再开正常  
5. 大列表分页加载至接近总数  
6. 搜索 / 筛选 / 排序有效，分区计数正确且可折叠  
7. 不悬停也能看到 `⋯`；悬停出现操作簇；离线行的加入/观战为禁用态而非消失  
8. 加入/观战/私聊不崩（服务器仍可能拒绝）  
9. `⋯` 与行内右键都能开菜单，取关/拉黑有确认  
10. 关闭叠加后大厅好友入口仍在，可再次打开（连测 5 次）  
11. 叠加打开时拖动/点击/滚轮不带动背后大厅；切原生后原生可点  
12. 头像：滚到底都没有纯填充空框，每格要么是图要么是首字母；日志摘要 `pending=0` 时 `images + letters` 等于行数，每个字母行都有带原因的 `avatar route` 行  
13. 拖标题栏移动、右下角缩放，重开游戏后位置尺寸保留  
14. 状态条不闪烁：同步中脉冲，完成后静止  
15. 日志无 `DrawWindow failed`、无 `MissingMethodException`  
16. `Ctrl+L` 全列表变首字母（含中文），再按恢复且不重新下载  
17. 游戏内打开「显示被封禁头像」后，被屏蔽的行无需重开面板即恢复真实头像；按 `F8` 切原生，两边显示一致  
18. 去掉 DLL：完全原生  
19. 日志出现 `capabilities inviteUserJoin=True createRoom=True teamInvite=True followers=True portrait=True`  
20. 已在房间时点 `邀请` 直接发出，5 秒内显示 `已邀请`；未进房间时点 `邀请` 弹出战斗类型选择，选一项后自动开房并在房间就绪时发出邀请，期间该行显示 `邀请中`  
20a. 战斗类型窗七行顺序与原生一致，无「暂未开放」；选 `组队匹配` 时好友收到组队邀请且不开房；日志每次成功都有 `InviteFlow: invited <uid> into our room`，失败只应看到 `gave up waiting` 或 `CreateRoom refused`  
21. 「关注我的人」页签：计数与原生被关注页一致，分区正确；非互关行 `回关` 后变 `⇌ 已互关`；菜单无 `置顶`，非互关行无 `取消关注`  
21a. 正对照：确知在线的非互关关注者必须出现在 `在线` 分区。若日志 `followers states known=0/N`，说明 `RequestOnline` 不答非关注 uid，此时该页签只能全离线 + `状态未知`，需按 README「工程结构」里的 `RequestFollowerStatus` 线索另找来源，**不要**改成信任列表自带的 `State`  
22. 置顶 / 取消置顶生效，`置顶` 分区排最前且从原分区移除；第 21 个显示 `置顶（已满 20）`；重启游戏后保留；取关后自动移出  
23. 选了官方头像的玩家显示官方头像，选了照片的显示照片，与 `F8` 原生一致；有边框的玩家显示边框。日志 `portrait parity compared=N agree=N` 必须 `agree == compared` **且**带 `conclusive`（即本次至少各比对到 1 个官方头像行与 1 个照片行）。出现 `INCONCLUSIVE` 或 `gave up` 时本项记为未验证，不得当作通过；出现 `MISMATCH` 说明「非空 avatar URL ⟺ 玩家选了官方头像」这一推断不成立，需先补一个失败测试再改 `PortraitPlanner.Decide`，且不得改回「官方优先于照片」  
24. `Ctrl+L` 同时隐藏头像与边框  

## 工程结构

- `src/FriendOverlay.Core` — 纯查询管线与分区逻辑、置顶集合、邀请规则、在线状态可信度、头像来源决策（均可单测）
- `src/FriendOverlay` — MelonMod / Harmony / IMGUI
  - `UI/Theme.cs`、`UI/Gfx.cs`、`UI/Anim.cs` — 调色板 / 绘制原语 / 动效
  - `Compat/Capabilities.cs` — 逐功能能力探测
  - `Data/GameProxies.cs`、`Data/FansListService.cs`、`Data/PinStore.cs`、`Data/PortraitResolver.cs`、`Data/FriendRowMapper.cs` — LobbyProxy 定位 / 关注我的人 / 置顶 / 头像来源解析 / 行映射
  - `UI/GameAssets.cs`、`UI/AvatarCache.cs`、`UI/AvatarLoader.cs`、`UI/GameSpriteResolver.cs`、`UI/SharedImageCache.cs` — 头像来源路由、按玩家缓存、共享资源（官方头像 / 边框 / 占位图）缓存与下载
  - `Data/FaceBlockPolicy.cs` — 复用游戏自己的判定，识别被屏蔽的头像
  - `UI/InputShield.cs` — 叠加期间的全屏 uGUI 挡板
  - `UI/Widgets/` — SearchBox / SegmentedChips / ScrollBar / RowCard / SectionHeader
  - `UI/OverlayPanel.cs` — 窗口编排
- `tests/FriendOverlay.Tests` — Core 单测
