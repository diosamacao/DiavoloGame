# NetSync W11 阶段性说明（Delta / Relevancy / FakeActionGame）

> 撰写：2026-08-22  
> 角色：**W11 / GF7+GF8 代码切面备忘**（10+ Actor Play 与 W10 公网 Play **均未**用户验收）  
> 排期：[`../2026.8.17/NETSYNC_FRAMEWORK_DEDICATED_MASTER_DEVELOPMENT_PLAN.md`](../2026.8.17/NETSYNC_FRAMEWORK_DEDICATED_MASTER_DEVELOPMENT_PLAN.md)

---

## 0. 一句话

复制不再每连接每 Tick 全量 Update。通用层按 **载荷未变跳过 + 兴趣裁剪 + 30Hz 节拍 + Update 预算** 构帧；baseline 丢失走 `ReplicationRecover` 全量 Spawn。`GraphNodeKey` 以稳定整数替换线上 UTF-8 节点名。FakeActionGame 只引用 ACTNet，不引用 ACT Character。

W10 出口仍未关。本切面不得称公网可用，也不得称 R2 框架已完成。

---

## 1. 本阶段代码交付

| 项 | 入口 |
|----|------|
| 未变跳过 | `ReplicationServer` 对比上次已发送 payload |
| 节拍 | `ReplicationBuildOptions.Compact`：`SnapshotIntervalTicks=2`；Owner 优先不受间隔限制 |
| 预算 | Compact `MaxUpdateBytes=1200`；装不下的实体保持脏、下帧重试 |
| 兴趣 | `ReplicationInterest`：Owner/玩家 Always；敌人默认 40m |
| 恢复 | `ResetBaseline` + `RoomMessageKind.ReplicationRecover=10` |
| Graph 节点 | `GraphNodeKey.FromStableName`；快照线格式改为 int32 |
| FakeActionGame | `Assets/Tests/EditMode/ACTNet/FakeActionGame/` |

**未验收**

- 10+ Actor 实机平均下行相对 W0 全量 60Hz
- Play 上远敌不出现、Owner 不被饿死
- W10：100ms / 20ms / 5% 对局

---

## 2. 明确不做

- 字段级 change mask 分包（实体级 payload 相等即 mask=0）
- 超 MTU 拆包（仍拒绝）
- 把 `RoomCodec` / `ReplicationCodec` 迁出 Simulation
- 合并 `ReplicationRole` 与 `NetProcessRole`
- Unity Package

---

## 3. 数据流

```
CaptureAuthorityActors
  → CopyRelevantStates(observer, 40m)
  → ReplicationServer.BuildFrame(Compact)
      Spawn / 脏且到期的 Update / Despawn
Client Rejected
  → ResetReplicationForRecovery
  → ReplicationRecover
  → Server ResetBaseline → 下一帧全量 Spawn
```

---

## 4. 测试

- `ReplicationDeltaTests`
- `FakeActionGameLoopbackTests`
- 既有 Replication / Snapshot / Dedicated Runtime 单测应保持绿
