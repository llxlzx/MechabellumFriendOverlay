# 原生「邀请参与的战斗类型」映射表 — 0.3.6 采集

采集方式：装载带 `Hooks/InviteTrace.cs` 的 FriendOverlay 构建，在游戏内**不在房间**时打开原生好友窗，点某个好友的邀请按钮，
弹出原生战斗类型窗口后**依次各点一次**七个按钮（每次点完退出/取消房间再点下一个），然后另开一局：先进入房间再点邀请，走直接邀请路径。
日志在 `MelonLoader\Latest.log`，过滤 `[InviteTrace]`。

## 待填：按钮 → CreateRoom 参数

| 界面按钮 | 处理器 | GameMode(名/值) | MatchMode(名/值) | isPrivate | 备注 |
| --- | --- | --- | --- | --- | --- |
| 1对1 | `Btn1v1OnClicked` | | | | |
| 2对2 | `Btn2v2OnClicked` | | | | |
| 生存模式 | `BtnSurviveOnClicked` | | | | **本行是必须靠采集确定的两项之一** |
| 4人混战 | `BtnChaosFactionOnClicked` | | | | |
| 混战 2V2 | `BtnChaosFaction2V2OnClicked` | | | | 若界面无此按钮记 N/A |
| 时空裂隙 1V1 | `BtnRift1v1OnClicked` | | | | |
| 时空裂隙 2V2 | `BtnRift2v2OnClicked` | | | | |

## 待填：组队匹配

| 项 | 观测值 |
| --- | --- |
| `MatchBtnOnClicked` 之后出现的调用序列 | |
| 是否调用 `TeamProxy.RequestTeamInvite(target)` | |
| 是否调用 `MatchMakerProxy.JoinMatch(layout)` | |
| 是否根本没建房 | |
| 完整日志片段 | |

## 待填：已在房间的直接邀请

| 项 | 观测值 |
| --- | --- |
| `FriendBtnListWindow.InviteRoomBtnOnClicked` 后的调用 | |
| `TryRequestInvite` 是否出现 | |
| `InviteUserJoin(userid, discord)` 的 discord 实参 | |
| `LobbyProxy.CreateRoom` 是否被调用 | |

## 待填：建房 → 邀请的时序

| 项 | 观测值 |
| --- | --- |
| `CreateRoom` 与 `InviteUserJoin` 的间隔（帧/秒，看日志时间戳） | |
| `OnResponseCreateRoom` 是否在 `InviteUserJoin` 之前 | |
| `CreateRoom` 用的是 `(GameMode, MatchMode, bool, SessionResponse)` 还是 `RequestCreateRoom` 重载 | |

## 结论（填完上面再写）

- 已确认可直接进 `BattleTypeCatalog` 的行：
- 需要改动计划假设的地方：
- 组队匹配是否进 0.3.6：
