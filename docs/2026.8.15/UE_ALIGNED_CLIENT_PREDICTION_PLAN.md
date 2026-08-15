# 客机预测向 UE AutonomousProxy 对齐 — 实施方案

> 制定：2026-08-15  
> 角色：**NS5 之后客机本机预测/表现的结构真源（先文档，后实现）**；房间、权威 World、命中契约仍以 [`TEAM_PVE_NARAKA_STYLE_STATE_SYNC_PLAN.md`](../2026.8.13/TEAM_PVE_NARAKA_STYLE_STATE_SYNC_PLAN.md) 为准  
> 相关：  
> - 组队 PVE 联网（NS0～NS5 已关闭）：[`../2026.8.13/TEAM_PVE_NARAKA_STYLE_STATE_SYNC_PLAN.md`](../2026.8.13/TEAM_PVE_NARAKA_STYLE_STATE_SYNC_PLAN.md)  
> - 内层走跑真源：`LocomotionStateMachine` + [`../2026.8.9/LOCOMOTION_GAIT_POLICY_PLAN.md`](../2026.8.9/LOCOMOTION_GAIT_POLICY_PLAN.md)  
> - 外部对照：Lyra / `UCharacterMovementComponent`（AutonomousProxy + Saved Move）；出招对照 GAS 预测（本方案 UE4）  
> 装配链：`InputFrame → AutonomousLocomotionRunner（内层机 + MotorSim 副本）→ 权威 Snapshot 纠偏重放 → RemoteProxy 只呈现`

---

## 0. 一句话

客机本机按 UE **AutonomousProxy** 跑与 Host **同一套** `LocomotionStateMachine`（位移 + 选片），权威只纠正结果并重放未确认输入；禁止再猜 Idle/Walk/Run，禁止客机 `CharacterActorFactory` / Collect，禁止锁步双轨，禁止把他人角色改成第二套预测。

---

## 1. 问题与动机

### 1.1 现状基线（NS5 关闭后）

```text
客机座位（PlayerController.BuildClientSeat）
  → 只采样 InputReader，不建 CharacterActor
  → ReplicationRoomClient.OnAfterLogicStep
       InputFrame 上行（命令批冗余）
       PredictedLocomotionDriver.Predict（wish / FollowInput）
         或 PredictAlignedToSnapshot（出招 / Start / Stop / Pivot）
       TickPredictedGait（本地再跑一份 GaitPolicy）
       PredictedLocomotionVisual.ResolveSelfKey（猜片）
       RemoteCharacterProxy.ApplySnapshot（Seek / CrossFade）

Host
  → CharacterActor.Step
       InputManager.IngestFrame
       LocomotionStateMachine.Tick   // 唯一走跑真源
       HitboxFrameConsumer.Collect
```

| 点 | 现状 |
|----|------|
| 权威 | Listen Host `SimulationWorld`；命中只在 Host Collect |
| 房间 | UDP `ReplicationRoomHost` / `Client`；NS5 已验收 |
| 客机位移 | `CharacterMotorSim` 副本 + wish；烘焙相位贴快照 |
| 客机选片 | `PredictedLocomotionVisual` 启发式 |
| 客机内层机 | **无**；`LocomotionStateMachine` 只挂在权威 `CharacterActorFactory` |
| 他人 | `RemoteCharacterProxy` 吃 Snapshot 相位/归一化时间（保持） |
| Listen Host 本地 | 不预测（保持） |

NS3 曾写「预测不重跑 Locomotion FSM」——那是当时为先走路做的范围裁剪，**本方案废止该条**。

### 1.2 痛点

1. Host 用内层机出 Start/Stop/Pivot/Sprint；客机用 wish + 猜片，手感永远差一截 RTT，停步会乱切。  
2. `PredictedLocomotionVisual` 与 `TickPredictedGait` 正在长成第二套走跑真源。  
3. 烘焙根位移无法用 wish 预测，只能贴齐，本机急停/折返「等权威」。  
4. 纠偏只吸毫米坐标，不恢复相位，重放又走 FollowInput，和内层机对不齐。

### 1.3 目标

| 目标 | 说明 |
|------|------|
| 结构 | 客机本机 = AutonomousProxy：同一套内层机写 MotorSim + Animation |
| 纠偏 | 超阈：吸权威 Pose + 相位/步态/烘焙游标，用内层机重放未确认 `InputFrame` |
| 手感 | 客机松手立刻进 Stop；按住满秒进 Sprint；起步不先等快照 |
| 边界 | 命中 / HP / 硬直 / 敌人 BT 仍只在 Host |
| 不做 | 见 §1.4 |

### 1.4 明确不做

| 不做 | 原因 |
|------|------|
| 客机 `CharacterActorFactory` / `HitboxFrameConsumer` **写入 Pipeline** | 双算伤害；NS4 已禁。几何申报口留给日后 `NS-PVP`（与 PVE 同一条链），本方案不实现 |
| 锁步 L5 / 全世界回滚 | 组队方案已否决 |
| Listen Host 本地再套预测 | 与 CMC 一致：Authority 就是本机 |
| 他人（SimulatedProxy）改成本地再跑内层机或 Lyra 加速度 AnimBP | 远端已有相位 + 归一化时间；另开题目 |
| 把 CMC / Mover / GAS 框架搬进 Unity | 学角色分工，不搬引擎 |
| 抽第二份无 Unity `LocomotionSim` 作为本方案必达 | 先复用现有内层机；分叉再另开 |
| 同时保留 wish 走跑核 + 内层机 | 零双轨 |
| Dedicated、匹配、第三人、Host 迁移 | 仍不在本方案 |

---

## 2. 设计原则

1. **学 UE 的角色分工，不学复制框架**：本机 Autonomous 先演同一套移动；服务器重演；错了纠偏重放。他人继续吃状态。  
2. **走跑只有一套码**：`LocomotionStateMachine` 是 Host 与客机本机的唯一相位/选片/烘焙位移入口。  
3. **装配用座位，不用身份 if**：权威座位走 `CharacterActorFactory`；本机预测座位走 `AutonomousLocomotionRunner`；他人走 `RemoteCharacterProxy`。禁止 `if (isClient) 猜片`。  
4. **预测核不得 Collect、不得进 `SimulationWorld`、不得写 Numeric**。  
5. **出招与走跑分轨**（对齐 CMC vs GAS）：走跑由 Runner；出招仍是只读预测，权威可取消。UE4 再加深，不在 UE1 把 ActionSim 和内层机焊成一个 Actor。  
6. **纠偏只改预测电机与 Runner 状态**，禁止把表现插值 Pose 写回 MotorSim。  
7. **零长期兼容**：UE3 出口前删掉猜片与 FollowInput 走跑主路径。  
8. **锁步边界不变**：权威仍是 `SimulationWorld` + `InputFrame`；Runner 只在客机本机逻辑步跑，不旁路 Host。

---

## 3. 目标架构

### 3.1 对照 UE（只留一种读法）

| UE / Lyra | 本项目终态 |
|-----------|------------|
| `ROLE_Authority` | Host `CharacterActor.Step` |
| `ROLE_AutonomousProxy` | 客机 `AutonomousLocomotionRunner` + 预测 MotorSim |
| `ROLE_SimulatedProxy` | 他人 / 敌人 `RemoteCharacterProxy`（Snapshot） |
| `UCharacterMovementComponent.PerformMovement` | `LocomotionStateMachine.Tick` |
| `FSavedMove_Character` | pending `InputFrame` + `LocomotionSavedState` |
| 服务器重演 + Correction | 已有 `PredictedLocomotionDriver.Reconcile`，改为经 Runner 重放 |
| 本机 AnimBP 吃本机速度 | 本机 Animation 由内层机 Play，不再 `ResolveSelfKey` |
| 远端复制加速度驱动 AnimBP | **不学**；远端继续相位 + `LocomotionNormalizedMilli` |
| GAS 技能预测、服务器取消 | UE4：只读 `ActionSim`；现 `PredictedActionDriver` 为过渡 |

### 3.2 数据流

```text
【客机本机 · Autonomous】
  渲染帧 MergeLocalSample
  AfterLogicStep:
    InputFrame → 上行 ClientCommand
    若无活动预测招 / 权威未出招:
         AutonomousLocomotionRunner.Tick(input)
           InputManager.IngestFrame
           LocomotionStateMachine.Tick
           写出 MotorSim + Animation + Lean
         记下 SavedMove(input, pose, LocomotionSavedState)
    若出招/受击:
         Runner.Exit（停走跑）
         PredictedActionDriver / 权威 Action 字段 → Proxy Seek
    表现：走跑不经 Proxy 再 Play Locomotion；只插值 Motor 表现根

  收到 AuthorityTick.actor[self]:
    误差 ≤ 阈 → Ack，丢弃更旧 SavedMove
    误差 > 阈 → 吸权威 Pose + 恢复相位/步态/烘焙帧
                 用 Runner 重放其后未确认 InputFrame
                 出招贴齐帧不重放 wish

【Host 看客机 / 客机看 Host · Simulated】
  不变：RemoteCharacterProxy.ApplySnapshot
```

### 3.3 关键契约

```text
Input  : InputFrame（与上行同一份）
Tick   : AutonomousLocomotionRunner.Tick(input, dt)
         → 改变预测 CharacterMotorSim + 本机 Animation
Output : Motor 毫米位姿、LocomotionPhase、Gait、Clip 归一化、Lean
Saved  : frame, InputFrame, pose, LocomotionSavedState
Reconcile(authoritySnapshot):
         比 pending[authorityFrame].pose
         超阈 → Restore(authority) + Replay(unacked via Runner)
禁止   : Collect、World.Register、写 HP、猜 AnimationKey
```

`LocomotionSavedState` 至少包含：`Phase`、`Gait`、`RunHoldSeconds`、`GaitInputGapSeconds`、`RootMotionKey`、`RootMotionFrame`、`RootMotionBasisYawMilli`、`FootCycle` 可恢复量、`ClipNormalizedMilli`。权威下行若缺烘焙帧，UE2 可用 `LocomotionNormalizedMilli` 对齐后重放；不够再给 Snapshot 加 `locomotionMotionFrame`（只加必要字段）。

### 3.4 边界

| 层 | 职责 | 不负责 |
|----|------|--------|
| `AutonomousLocomotionRunner` | 本机走跑相位、位移、选片 | 命中、AI、房间 |
| `PredictedLocomotionDriver` | SavedMove 队列、阈值、编排 Restore+Replay | 自己算 wish 走跑 |
| `RemoteCharacterProxy` | 他人 Seek；本机出招/受击呈现 | 本机走跑选片 |
| `PredictedActionDriver` | 出招 Clip 帧（UE4 前） | 走跑 |
| `ReplicationRoom*` / Codec | 房间与字节 | 内层机 |
| Host `CharacterActor` | 权威真源 + Collect | 客机座位 |

---

## 4. 范围声明

| 阶段 | 包含 | 不包含 |
|------|------|--------|
| UE1 | 客机装配 Runner；走跑由内层机驱动；删猜片主路径 | 相位级重放 |
| UE2 | SavedState + 纠偏经 Runner 重放；删 FollowInput 走跑重放 | 出招回放 |
| UE3 | 删剩余启发式；改约定与同机预览 | 远端 Anim 模型 |
| UE4 | 只读 ActionSim 预测 Cancel/Clip | Collect、技能回滚世界 |

---

## 5. 分阶段交付（任务 / 验收 / 出口）

> 勾选：未开始 `[ ]`；完成后 `[x]` 并在出口注明日期。

### UE1 — 客机装配 AutonomousLocomotionRunner

**任务**

- [ ] 新增 `AutonomousLocomotionRunner`：在预测体上装配 `InputManager` + `LocomotionStateMachine` + 现有 `CharacterMotor` / `CharacterAnimationService` / `CharacterLocomotionProfile` / `LocomotionFootstepPlayer`  
- [ ] 扩展 `RemoteCharacterProxyFactory`（或并列 `AutonomousLocomotionFactory`）：本机座位注入可 `IngestFrame` 的意图源，**禁止**再绑 `IdleMoveIntentSource`；**禁止**走 `CharacterActorFactory`  
- [ ] `ReplicationRoomClient` 逻辑步：无出招时 `Runner.Tick(input)`；有预测招或权威 `ActionId != 0` 或 Vitality Hit/Death 时 `Runner.Exit`，走现有出招/受击呈现  
- [ ] 本机走跑：Proxy **不得**再 `Play`/`Seek` Locomotion；`ApplyPredictedVisual` 只同步 Motor 表现位姿与 Lean  
- [ ] **删除**本机路径对 `PredictedLocomotionVisual.ResolveSelfKey`、`TickPredictedGait` 的调用  
- [ ] `PredictedLocomotionDriver.Predict` 不再作为走跑主路径（UE1 可暂留 API，但房间不得调用）；位移以 Runner 写出的 MotorSim 为准  
- [ ] Listen Host 本地仍不创建 Runner  

**验收**

- [ ] `rg "ResolveSelfKey" Assets/Scripts` 无房间/预览调用  
- [ ] `rg "CharacterActorFactory.Create" Assets/Scripts/App/Controllers/Gameplay/ReplicationRoomClient.cs` 无匹配  
- [ ] `rg "hitPipeline.Collect" Assets/Scripts` 仍仅权威装配  
- [ ] Play（ParrelSync）：客机按住走，本机立刻播 Start→Walk/Run，不必等 Host 快照才起步  
- [ ] Play：客机松手本机立刻进 Stop（`StopL`/`StopR`），不再 Run→Idle→`run_end`  
- [ ] Play：客机 Run 保持满 `sprintAfterRunSeconds` 后播 Sprint 片，速度与片一致  
- [ ] Play：Host 上看客机仍只靠 Snapshot，伤害不双算  
- [ ] Unity 编译 / EditMode 在 Editor 确认通过  

**出口：** 客机本机走跑由内层机驱动，猜片主路径已断。→ **未达成**

### UE2 — Saved Move 相位纠偏与 Runner 重放

**任务**

- [ ] 新增 `LocomotionSavedState`（Capture / Restore）。`LocomotionStateMachine` / `LocomotionContext` / `LocomotionRootMotionPlayer` 提供可恢复接口；禁止为纠偏复制一套相位袋  
- [ ] `PredictedLocomotionDriver` 每步保存 `(InputFrame, pose, LocomotionSavedState)`；`Reconcile` 超阈时 Restore 权威对齐态，再对未确认输入调用 `Runner.Tick`，**禁止**对走跑步 `PredictedLocomotionMath.ApplyInput`  
- [ ] 权威快照已有 `LocomotionPhase` / `Gait` / `LocomotionNormalizedMilli`。Restore 以这些为真源；若烘焙游标对不齐，给 Snapshot/Codec **只加** `locomotionMotionFrame`（或等价），并补往返单测  
- [ ] 出招/受击 pending 仍标 `Aligned`：纠偏后贴权威位姿，不重放烘焙招式位移  
- [ ] **删除**走跑路径上的 `PredictAlignedToSnapshot`（出招/受击贴齐可留在 Driver 的 Snap API）  
- [ ] 更新 `PredictedLocomotionReconcileTests`：超阈后最终 pose/相位等于「权威 Restore + 后续输入经同一 Tick」  

**验收**

- [ ] EditMode：`PredictedLocomotionReconcileTests`（或继任类）超阈重放后 pose 对齐；走跑重放路径无 `ApplyInput`  
- [ ] `rg "PredictAlignedToSnapshot" Assets/Scripts` 无走跑调用（出招 Snap 除外）  
- [ ] Play：客机贴墙/错位后回拉一次，回拉后本机相位不是 Idle 硬切再起步  
- [ ] Play：RTT 可见时，客机连续走跑+急停，Host 与客机停步落点误差可被纠偏收住，无每步 10Hz 吸附感  
- [ ] Unity 编译 / Test Runner 在 Editor 确认通过  

**出口：** 纠偏与 CMC 同构——吸权威态，用同一套移动码重放未确认输入。→ **未达成**

### UE3 — 清扫启发式与约定

**任务**

- [ ] **删除** `PredictedLocomotionVisual.ResolveSelfKey`、`LoopKeyFromGait`（若已无引用）、客机 `TickPredictedGait` / `_predictedGait` / `_runHoldSeconds`  
- [ ] `PredictedLocomotionVisual` 仅保留他人 Proxy 仍需要的 `IsTransitionPhase` / `ShouldHardCut` / `TryReadPhase`；若可迁到 `ReplicationPresentationAlign` 则删空类  
- [ ] `PredictedClientPreviewController` 改走同一 Runner（同机预览不得再 `Predict` + 猜片）  
- [ ] 改 CONVENTIONS：废止「预测不重跑 Locomotion FSM」「稳态 FollowInput、过渡贴齐」；改为「本机 Autonomous 跑内层机；纠偏 Restore+Replay；他人仍 Snapshot」  
- [ ] 改 TECHNICAL 客机预测节；`TEAM_PVE` §3.4 指向本文，删除过时伪代码  
- [ ] `rg "TickPredictedGait|ResolveSelfKey" Assets/Scripts` 无匹配  

**验收**

- [ ] 上述 `rg` 无业务调用  
- [ ] 同机 `previewPredictedClient`：左侧预览起步/急停/Sprint 与中间 Host 同相位族（允许 Loopback 延迟）  
- [ ] 两人 Play：走跑手感与 UE1/UE2 验收一致，无回归双伤  
- [ ] 架构文档与代码一致  

**出口：** 猜片与 wish 走跑核从仓库消失；约定与实现同一条。→ **未达成**

### UE4 — 出招预测向 GAS 靠拢

**任务**

- [ ] 客机本机增加**只读** `ActionSim`（解析 + 推帧 + Cancel 窗），仅驱动 Clip/VFX/Cancel 手感  
- [ ] **删除**仅记 ActionId/帧的 `PredictedActionDriver` 主路径（迁完即删类，或缩成薄封装且无第二语义）  
- [ ] 权威 `ActionId==0` 或 Vitality Hit/Death：取消本地预测招并 `Runner` 恢复 Locomotion  
- [ ] 禁止 Action 预测路径调用 `HitboxFrameConsumer`、写 Numeric、跑 `ActionMotionResolver` 吸附权威  
- [ ] Listen Host 本地仍不预测出招  

**验收**

- [ ] `rg "class PredictedActionDriver" Assets/Scripts` 无主路径残留（或仅测试夹具且注明）  
- [ ] `rg "hitPipeline.Collect" Assets/Scripts` 仍仅权威  
- [ ] Play：客机连招下一段在本机 Cancel 窗立刻起手；权威未起手则取消，无双伤  
- [ ] Play：客机出招中受击，本地招取消并跟权威受击  
- [ ] Unity 编译 / 相关 EditMode 在 Editor 确认通过  

**出口：** 出招手感对齐 GAS「先演、服务器可取消」；命中仍只认 Host。→ **未达成**

---

## 6. 迁移与兼容

### 6.1 保留 / 迁入

| 保留 | 用法 |
|------|------|
| NS0～NS5 房间 / UDP / Snapshot / 命中复制 | 不动 |
| Host `CharacterActor` + 内层机 | 权威真源 |
| `RemoteCharacterProxy` | 他人；本机出招呈现 |
| `PredictedLocomotionDriver` | 改为 SavedMove 编排，不再算 wish 走跑 |
| `InputFrame` 上行与命令批 | 不变 |
| `PredictedLocomotionVisual` 过渡相位判断 | UE3 前供他人 Proxy；之后能并则并 |

### 6.2 明确删除

| 删除 | 阶段 | 原因 |
|------|------|------|
| 本机 `ResolveSelfKey` / `TickPredictedGait` | UE1～UE3 | 第二套选片 |
| 走跑 `Predict()` / `ApplyInput` / `PredictAlignedToSnapshot` | UE1～UE2 | 与内层机双轨 |
| 「预测不重跑 Locomotion FSM」约定 | UE3 | 已被本方案废止 |
| `PredictedActionDriver` 主路径 | UE4 | 被只读 ActionSim 取代 |

禁止保留「猜片 fallback，SM 失败再猜」。

### 6.3 与组队方案的关系

- **NS0～NS5**：房间与权威契约，已关闭。  
- **§3.4 预测伪代码**：自本文生效日起作废，改指向 UE1～UE2。  
- **L0～L2 模拟核**：不变。  
- **L5 锁步联网**：仍取消。

---

## 7. 目录与文件预期（增量）

```text
Assets/Scripts/Domain/Character/Replication/AutonomousLocomotionRunner.cs
Assets/Scripts/Domain/Character/Replication/LocomotionSavedState.cs
Assets/Scripts/Domain/Character/Replication/AutonomousLocomotionFactory.cs   // 或扩 RemoteCharacterProxyFactory
Assets/Scripts/Domain/Simulation/Prediction/PredictedLocomotionDriver.cs     // 改为编排 Restore+Replay
Assets/Scripts/App/Controllers/Gameplay/ReplicationRoomClient.cs
Assets/Scripts/App/Controllers/Gameplay/PredictedClientPreviewController.cs
Assets/Tests/EditMode/Simulation/PredictedLocomotionReconcileTests.cs
Assets/Tests/EditMode/Replication/AutonomousLocomotionRunnerTests.cs         // 能单测的 Capture/Restore
docs/2026.8.15/UE_ALIGNED_CLIENT_PREDICTION_PLAN.md
```

不改 `Assets/Data/**`、Prefab、非 Shader 美术。Runner 复用现有 `CharacterLocomotionProfile` 引用（从 `CharacterConfig` 读，与工厂相同）。

---

## 8. 风险与对策

| 风险 | 对策 |
|------|------|
| 内层机绑 `CharacterMotor` / Animator，与无引擎 MotorSim 不完全同构 | Runner 必须用与 Proxy 同一 Motor（内含 MotorSim）；禁止再 new 一份只给 wish 用的电机 |
| Capture 漏字段导致重放分叉 | SavedState 与权威 Snapshot 对拍；缺烘焙帧再加 `locomotionMotionFrame` |
| Runner 与 Proxy 同帧 Tick 两次 Clip | 本机走跑禁止 Proxy Play Locomotion；出招时 Runner.Exit |
| FaceTarget 客机无权威 Targeting | UE1 允许 FollowMove；选敌朝向跟 Snapshot `SelectedTargetId` 只读查询，不在客机跑完整 Targeting 权威 |
| 软分离只在 Host | 与现网相同，靠纠偏收；不在客机跑 `SoftBodySeparation` |
| 把工厂「顺便」挂上 Hitbox | 验收用 `rg Collect`；Factory 注释写死禁止 |
| UE4 把 ActionSim 做成完整 Actor | 任务写明只读；无 Pipeline、无 Numeric 写入 |

---

## 9. Editor 人工步骤

1. 打开工程，等编译通过。  
2. 无需新建 Prefab / Input Actions / Locomotion 资产；客机 Runner 读现有 `CharacterConfig`。  
3. **UE1 Play（ParrelSync）**：原工程 Host，克隆 Client；客机起步/急停/Sprint 看本机，Host 上看对方跟快照。  
4. **UE2 Play**：走墙或故意延迟，确认回拉后相位连续。  
5. **UE3**：勾选 `CombatWorldController.previewPredictedClient`，对照左侧预览与中间 Host。  
6. Test Runner：`PredictedLocomotionReconcileTests` 及本方案新增测试类。  
7. AnimationProfile 未绑 Sprint 时两端仍会回退 Run（与单机相同，不在本方案修资产）。

---

## 10. 推荐开工顺序

```text
UE1 装配内层机并切断猜片
  → UE2 SavedState + Runner 重放
  → UE3 删启发式、改约定与同机预览
  → UE4 只读 ActionSim（UE3 出口后）
```

**最小可感切片：** UE1——客机松手立刻播急停，不必等权威 `run_end`。

未要求「按方案实现」前不改业务代码。

---

## 11. 变更日志

| 日期 | 说明 |
|------|------|
| 2026-08-15 | 初版：NS5 之后客机预测对齐 UE AutonomousProxy；废止「预测不重跑 FSM」；他人仍 Snapshot |
