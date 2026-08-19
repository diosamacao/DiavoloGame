# NetSync W7 阶段性说明（DS5）

> 撰写：2026-08-19  
> 角色：**W7 代码落地备忘**（Editor Play 已于 2026-08-19 用户验收）  
> 排期：[`../2026.8.17/NETSYNC_FRAMEWORK_DEDICATED_MASTER_DEVELOPMENT_PLAN.md`](../2026.8.17/NETSYNC_FRAMEWORK_DEDICATED_MASTER_DEVELOPMENT_PLAN.md)

---

## 0. 一句话

Dedicated 在权威步末按连接构 `ReplicationFrame`（复用 Listen 的 Capture / `ReplicationServer` / Owner 预测），并带 Lobby→Playing→Ending Match 状态机与可靠 `MatchEnd`。Unity Dedicated Build / CLI / 进程退出属 **W8**（见 [`NETSYNC_W8_STAGE_SUMMARY.md`](./NETSYNC_W8_STAGE_SUMMARY.md)）。

---

## 1. 交付

| 项 | 入口 |
|----|------|
| Match 阶段 | `DedicatedMatchPhase`：Lobby → Starting → Playing → Ending → Cleanup → Lobby |
| Join 策略 | Lobby / Starting / Playing 可加入；Ending 之后 `GameRejected` |
| JoinAccept 实体 | 使用 World `SimulationId`，不再用 Match 槽位占位 Id |
| 命令归属 | 只灌 `SenderPlayerId ==` 该连接 PlayerId 的命令 |
| 每连接构帧 | `DedicatedAuthorityWorld.OnAfterLogicStep` Capture + 差分；Runtime `FlushReplication` |
| 命中冗余 | 最近 8 条 `SimHitKey` 去重重发；客户端既有 128 窗口去重 |
| MatchEnd | `RoomMessageKind.MatchEnd = 8`（避开 Session Kick=7）；`ControlReliableOrdered` |
| Client | 先 `AcceptJoinIfReady` 再 Drain；Kick/超时在 Drain 之后收口，避免同拍首帧把 Owner 建成 Proxy |

**明确后置**

- Unity Dedicated Server Build / CLI / 空房退出进程（W8）
- 可靠事件通道替换命中冗余（W10）
- Listen 改为 ServerRuntime + LocalClient（W9）

---

## 2. 组合

```
DedicatedServerRuntime.Poll
  → DrainJoins / DrainCommands（Merge 进下一权威帧）
  → Authority.Advance → Host.StepOnce
  → AfterLogicStep：Capture + 每连接 BuildFrame（appliedHint=FirstAppliedHint）
  → FlushReplication（SnapshotUnreliableSequenced）
  → 空房或 RequestMatchEnd → MatchEnd + Kick → Lobby
```

---

## 3. Editor Play 复验修复（2026-08-19）

| 现象 | 原因 | 处理 |
|------|------|------|
| 连续闪避容易被拉回 | 冗余批合成一帧后用 newest Hint 和解 | 下行 `appliedHint` 改本批第一条 Hint；Dodge/吸附整段推迟 2m 硬吸 |
| 怪物对峙只平移不播走跑 | Headless `Play` 早退，Capture 永远 Idle | `CharacterAnimationService.Play` 无 Graph 仍记 `CurrentKey` |
| B 要约 0.3s 才看到 A 出手 | 逐步灌入把积压 Hint 按 60Hz 慢放 | 删除 `DedicatedRemoteCommandQueue`；到包即 Merge 进下一权威帧 |
| Branch_02 中途偏/被拉回 | 权威未起手时 Stop + Restore+Replay 只重放走跑 | 修正位移招不掐；Gate 整段推迟硬吸 |
