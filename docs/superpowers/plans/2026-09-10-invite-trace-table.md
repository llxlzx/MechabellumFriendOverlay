# 原生「邀请参与的战斗类型」映射表 — 0.3.6 采集

采集于 2026-09-10 21:47–21:48，构建 `CF680662`（含 `Hooks/InviteTrace.cs`），日志 `MelonLoader\Latest.log`，过滤 `[InviteTrace]`。
流程：不在房间时打开原生好友窗 → 点好友邀请 → 原生战斗类型窗 → 七个按钮各点一次；最后进房间再点一次邀请。

## 按钮 → CreateRoom 参数

| 界面按钮 | 处理器 | GameMode(名/值) | MatchMode(名/值) | isPrivate | 日志时间 |
| --- | --- | --- | --- | --- | --- |
| 1对1 | `Btn1v1OnClicked` | `Normal` / 0 | `VS_1_1` / 0 | True | 21:47:45.101 |
| 2对2 | `Btn2v2OnClicked` | `Normal` / 0 | `VS_2_2` / 1 | True | 21:47:50.607 |
| 生存模式 | `BtnSurviveOnClicked` | `Survive` / 3 | `VS_2_2` / 1 | True | 21:47:57.509 |
| 4人混战 | `BtnChaosFactionOnClicked` | `Normal` / 0 | `VS_4_Scuffle` / 2 | True | 21:48:04.735 |
| 时空裂隙 1V1 | `BtnRift1v1OnClicked` | `Rift` / 4 | `VS_1_1` / 0 | True | 21:48:08.253 |
| 时空裂隙 2V2 | `BtnRift2v2OnClicked` | `Rift` / 4 | `VS_2_2` / 1 | True | 21:48:11.536 |
| 混战 2V2 | `BtnChaosFaction2V2OnClicked` | N/A | N/A | N/A | 窗口无此按钮，未触发 |

生存模式是采集前唯一无法推断的一项：`MatchMode` 是 `VS_2_2`(1)，不是 `VS_1_1`。`MatchMode.VS_2_2_Scuffle`(3) 七个按钮都没用到。

## 组队匹配

| 项 | 观测值 |
| --- | --- |
| `MatchBtnOnClicked` 之后的调用序列 | 仅 `TeamProxy.RequestTeamInvite(target)` |
| 是否调用 `TeamProxy.RequestTeamInvite(target)` | 是，21:48:01.489，target 即被邀请者 |
| 是否调用 `MatchMakerProxy.JoinMatch(layout)` | 否 |
| 是否建房 | 否，全程没有 `CreateRoom` |
| 日志片段 | `21:48:01.488 CLICK MatchBtnOnClicked` → `21:48:01.489 TeamProxy.RequestTeamInvite target=…` |

所以组队匹配不是房间类型，`IsTeamMatch=true` + `NoRoomMode` 的建模成立。它是纯 fire-and-forget，没有任何回执可读。

## 已在房间的直接邀请

| 项 | 观测值 |
| --- | --- |
| `InviteRoomBtnOnClicked` 后的调用 | `TryRequestInvite` → 同毫秒内 `InviteUserJoin`，无 `CreateRoom` |
| `TryRequestInvite` 是否出现 | 是，但它只是分流器：在房间就直接转 `InviteUserJoin` |
| `InviteUserJoin` 的 discord 实参 | False |
| `LobbyProxy.CreateRoom` 是否被调用 | 否 |
| 日志片段 | `21:48:19.006 InviteRoomBtnOnClicked` → `.006 TryRequestInvite` → `.007 InviteUserJoin` |

## 建房 → 邀请的时序

| 项 | 观测值 |
| --- | --- |
| `CreateRoom` → `InviteUserJoin` 间隔 | 约 0.49–0.65 秒（6 次样本：604/653/500/492/495/649 ms） |
| `OnResponseCreateRoom` 是否在 `InviteUserJoin` 之前 | 否。`InviteUserJoin` 早 5–11 ms，说明它是从 `OnResponseCreateRoom` **内部**的 `SessionResponse` 回调里发出的 |
| 用的哪个 `CreateRoom` 重载 | `(GameMode, MatchMode, bool isPrivate, SessionResponse)`；`RequestCreateRoom` 重载只在手动开自定义房时出现（21:48:15.344），与邀请流程无关 |

## 结论

- 七行全部可直接进 `BattleTypeCatalog`，六个房间类型 + 组队匹配，`Verified=true`。
- `isPrivate` 恒为 `true`，`InviteFlow.RoomIsPrivate = true` 与原生一致。
- 需要改动计划假设的地方：无。生存模式取 `VS_2_2` 而非 `VS_1_1`，这正是设立采集闸门的原因。
- 组队匹配进 0.3.6：进。调用已确认，但失败不可观测，所以按 fire-and-forget 处理、立即标记冷却。
- `SessionResponse` 回调确实会触发（原生就是在回调里发邀请的），所以本 mod 用它做失败快速回收是可行的；成功判定仍额外要求 `JoinedRoom != null && IsHost()`，以免把玩家中途进的别人房间当成我们建的房。
