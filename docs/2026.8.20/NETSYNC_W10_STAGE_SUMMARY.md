# NetSync W10 阶段性说明（预测骨架 / 可靠通道 / 网络时间）

> 撰写：2026-08-20  
> 角色：**W10 / GF5+GF6 代码切面备忘**（Editor Play **尚未**用户验收；不得称公网可用）  
> 排期：[`../2026.8.17/NETSYNC_FRAMEWORK_DEDICATED_MASTER_DEVELOPMENT_PLAN.md`](../2026.8.17/NETSYNC_FRAMEWORK_DEDICATED_MASTER_DEVELOPMENT_PLAN.md)

---

## 0. 一句话

通用预测只做 Command/State 历史与 Restore+Replay；ACT 的 2m Gate、连招超前、Hit/Death、Action Cancel 留在 `ActCharacterPredictionModel` / `PredictedActionAckQueue`。Control/Event 由 `ChannelMuxTransport` 做可靠有序；Command 仍不可靠冗余；Snapshot 不可靠时序丢旧。W7 命中 8 条冗余已删，改走 `RoomMessageKind.ReplicationEvent` 单轨。传输库**定案不换**：不引入 LiteNetLib / Unity Transport（后者会破坏 `ACTNet.Transport` 的 `noEngineReferences`）。

---

## 1. 本阶段代码交付

| 项 | 入口 |
|----|------|
| 通用预测 | `ACTNet.Prediction`：`CommandHistory` / `PredictedStateHistory` / `PredictionCoordinator` / `SnapshotTimeline` / `NetworkTimeEstimator` |
| ACT 模型 | `ActCharacterPredictionModel`；`PredictedLocomotionDriver` 只保留电机与 Gate |
| 出招 Ack | `PredictedActionAckQueue` 未迁入 Coordinator |
| 远端插值 | Observer `SnapshotTimeline` + interpolation delay；Owner 仍用本地 `InterpolationAlpha` |
| 通道 | Session 包装 `ChannelMuxTransport`；Control/Event 可靠有序重传 |
| 命中 | `DedicatedAuthorityWorld` 只发本帧事件；Client `ApplyReplicationEvents` + `SimHitKey` 只播一次 |
| MTU | `TransportMtuGate` 默认 1400；超限拒绝并计数，不拆包（W11） |
| HUD | F3 增加 jitter / loss‰ / delay / snap / replay |

**未验收（不得勾选 W10 出口）**

- 100ms RTT、20ms jitter、5% 丢包下完整对局
- Play 上 2m Gate / 连招超前 / Hit Cue 只播一次

---

## 2. 传输定案

```
继续 UdpTransport + ChannelMuxTransport
不换 LiteNetLib
不换 Unity Transport（会把 ACTNet.Transport 绑上 Unity 包）
```

可靠 UDP 只覆盖 Control / Event。Command / Snapshot 不走可靠重传。W12 若公网压测证明不够，再单开换库，不与预测提取混在同一风险面。

---

## 3. 数据流

```
Owner Predict
  ActCharacterPredictionModel.ApplyInput / Record
  PredictionCoordinator.Record
权威 Snapshot
  ActCharacterPredictionModel.ResolvePolicy（2m / 宽限 / 出招受击）
  PredictionCoordinator.ReceiveAuthority → Restore + Replay
Observer
  SnapshotTimeline.TryPush（旧 Tick 拒绝）
  Render：TrySample(delayTicks) → ApplySnapshot → proxy.Render(alpha)
Hit
  CopyHits(本帧) → ActReplicationEventCodec → EventReliableOrdered
  Client PlayReplicatedHits（128 Key 去重）
```

---

## 4. 测试

- `FakeLinearEntityPredictionTests`
- `SnapshotTimelineTests`
- `NetworkTimeEstimatorTests`
- `ChannelMuxTransportTests`（乱序可靠交付、丢包重传、旧 Snapshot 丢弃、超 MTU 拒绝）
- `TransportMtuGateTests`
- `ActReplicationEventCodecTests`
- 既有 `PredictedLocomotionReconcileTests` / `PredictedActionReconcileTests` / Session / Dedicated Runtime

---

## 5. 与 W9 / W11 的关系

W9 Listen 组合与 60Hz 预测帧序不变。W10 不改 2m 阈值，不恢复 Host Room。下一切面 W11：Delta / Relevancy / 预算 / 超限拆包。
