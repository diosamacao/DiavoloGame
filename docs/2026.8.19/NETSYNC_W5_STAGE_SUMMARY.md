# NetSync W5 阶段性说明（DS1 + DS2）

> 撰写：2026-08-19  
> 角色：**W5 代码落地后的实现备忘**（对照代码；Editor Play 仍待确认）  
> 排期真源：[`../2026.8.17/NETSYNC_FRAMEWORK_DEDICATED_MASTER_DEVELOPMENT_PLAN.md`](../2026.8.17/NETSYNC_FRAMEWORK_DEDICATED_MASTER_DEVELOPMENT_PLAN.md)  
> 专项：[`../2026.8.17/NETSYNC_DEDICATED_SERVER_SEPARATION_PLAN.md`](../2026.8.17/NETSYNC_DEDICATED_SERVER_SEPARATION_PLAN.md) DS1 / DS2  
> M1 阅读入口：[`../2026.8.18/NETSYNC_M1_STAGE_SUMMARY.md`](../2026.8.18/NETSYNC_M1_STAGE_SUMMARY.md)

---

## 0. 一句话

Dedicated 是**独立运行时**，不是 `ReplicationRoomHost` 上的开关。Listen Host 仍是当前可玩基线；Dedicated 已能 Listening、按连接分配身份并隔离 ACK，**尚未**步进权威 `SimulationWorld`（W6）。

---

## 1. 本阶段交付

| 项 | 入口 |
|----|------|
| 进程角色 | `NetProcessRole.Client / ListenServer / DedicatedServer` |
| Dedicated 程序集 | `Assets/Scripts/App/Server/`（`ACTGame.Server`，不引用 HUD / Input / Camera） |
| 启动 | `DedicatedServerBootstrap` → `DedicatedServerRuntime.TryStart` |
| 配置 / 退出码 | `ServerLaunchConfig`、`ServerExitCode`（ConfigFailed=10，BindFailed=20） |
| 身份与出生 | `MatchCoordinator`：PlayerId / EntityId / Team / Spawn（槽位 × 2000mm X，不再 Host Root +2m） |
| 每连接 | `DedicatedPlayerRuntime`：独立 `ReplicationServer` + Hint ACK |
| JoinAccept | `AuthorityEntityId` 可为 Invalid（线格式 0） |
| Listen N 客 | `ActHostRoomGameplay._guests` + 每连接构帧；Join 不等 Host Actor |
| 场景入口 | `CombatWorldController` Dedicated 只 `EnsureDedicatedBootstrap()`，不挂 Room / Feedback |
| 菜单 | `ACTGame/Room/Use Dedicated Server` |

**明确没做（属 W6+）**

- Dedicated 权威 World / Headless Actor / 无 Animator 步进
- Dedicated 刷怪、命中 Collect、向客户端发 `ReplicationFrame`
- Unity Dedicated Server Build Profile、云、匹配

---

## 2. 组合

```
CombatWorldController.Awake
  Dedicated → DedicatedServerBootstrap.Configure
               DedicatedServerRuntime（UDP + ServerSession + Match）
  ListenHost → ReplicationRoomHost + ActHostRoomGameplay（N Guest）
  Client → ReplicationRoomClient（不变）
```

Listen Host 仍可回归双人；W9 之前不删 Listen 主路径。

---

## 3. 测试

- `DedicatedServerRuntimeTests`：配置失败、绑定失败、无 LocalPlayer Accept、三客户端互异 Id、断开隔离、ACK 不串线
- `SessionCodecTests.JoinAccept_InvalidAuthorityEntity_RoundTrips`
- `NetIdentityTests.NetProcessRole_HasDistinctDedicatedValue`
- `RoomArchitectureBoundaryTests.DedicatedServerSources_DoNotReferenceClientPresentationTypes`
- `ReplicationProductionOrderTests`：N Guest / 每连接 `replication.BuildFrame`

Editor Play：菜单 Dedicated 无本机玩家即可 Listening，仍待用户确认。
