# NetSync W9 阶段性说明（Listen 组合收敛）

> 撰写：2026-08-20  
> 角色：**W9 / Listen 组合备忘**（Editor Play 已于 2026-08-20 用户验收）  
> 排期：[`../2026.8.17/NETSYNC_FRAMEWORK_DEDICATED_MASTER_DEVELOPMENT_PLAN.md`](../2026.8.17/NETSYNC_FRAMEWORK_DEDICATED_MASTER_DEVELOPMENT_PLAN.md)

---

## 0. 一句话

Listen 不再是特殊服务器。本机进程组合同一 `DedicatedServerRuntime` 与 `LocalClientRuntime`（`127.0.0.1` UDP）。房主在 Server 是 Authority Guest，在本机是 Owner/Presentation，并走 Command / Snapshot / ACK。`ReplicationRoomHost` / `ActHostRoomGameplay` 与 Host 本机 Capture 已删除。本机预测按 `PeekAdvanceSteps` 对齐 60Hz，禁止每个渲染帧 `StepPrediction`。

---

## 1. 本阶段代码交付

| 项 | 入口 |
|----|------|
| 组合 | `ListenServerBootstrap`：ServerRuntime + LocalClient；不挂 `DedicatedServerBootstrap` |
| 本机 Client | `LocalClientRuntime`；远端 `ReplicationRoomClient` 复用同一 Runtime |
| 回环 | 本机连 `127.0.0.1:实际绑定端口`；端口占用回退系统端口 |
| 帧序 | Poll/采样 → 按 `PeekAdvanceSteps` 发命令预测 → `Server.Poll` → 再 Drain |
| 座位 | Listen/Client `PlayerController` 只装 Autonomous；Dedicated 仍禁用 |
| 敌人 | Listen / Dedicated 权威 `AuthorityHeadless`；可见体走 Observer |
| Capture | 只拍 Guest + 敌人 |
| 感知 | `LocalPlayerService` 不把预测座位列入 PlayerRoots |

**已验收（2026-08-20）**

- Listen 单人：走跑 / 连段速度回到 60Hz，不再整段加速后被快照拉回
- 房主也走 Command / Snapshot / ACK
- 与 Dedicated 同一 `DedicatedServerRuntime`

---

## 2. 组合

```
CombatWorldController.Awake（ListenHost）
  → DedicatedAuthorityWorld
  → ListenServerBootstrap.Configure（ExitOnMatchEnd=false）
       DedicatedServerRuntime.TryStart
       Start：LocalClient → 127.0.0.1
Update：PollAndApply → SampleRenderInput → 按 PeekAdvanceSteps 发命令预测 → Server.Poll → PollAndApply
```

权威玩家（含房主）只由 Join 创建 Headless Guest。场景 `PlayerController` 不进 `SimulationWorld`。

---

## 3. 测试

- `ListenFrame_ProductionSource_PreservesLocalSendThenServerPollThenApply`
- `LocalClientDisconnect_DoesNotDestroyRemainingGuest`
- `DeletedHostPath_IsRemoved`
- `ListenUpdate_RunsBeforeSimulationHostUpdate`
- `PeekSteps_DoesNotMutateAndMatchesConsume`
- `PeekAdvanceSteps_MatchesNextAdvanceWithoutStepping`

---

## 4. 与 W8 的关系

W8 Dedicated 启动/出包合同不变。W9 只删 Listen 特殊 Host 实现，Dedicated 主路径不改。下一联网切面为 W10。
