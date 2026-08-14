# 组队 PVE · 永劫式状态同步 — 技术方案

> 制定：2026-08-13  
> 角色：**组队 PVE 网络同步的结构真源（先文档，后实现）**；覆盖「谁跑 Sim、上下行各传什么、本地预测与命中权威」  
> 相关：  
> - 模拟核（仍有效）：[`../ACTION_SYSTEM_LOCKSTEP_REFACTOR_PLAN.md`](../ACTION_SYSTEM_LOCKSTEP_REFACTOR_PLAN.md)（L0～L2 时钟 / ActionSim / MotorSim；**L5 输入广播联网被本文取代**）  
> - 输入与选敌：[`CAMERA_AUTHORITY_AND_TARGETING_REFACTOR_PLAN.md`](./CAMERA_AUTHORITY_AND_TARGETING_REFACTOR_PLAN.md)  
> - 排期总表：[`../2026.8.6/MASTER_IMPLEMENTATION_PLAN.md`](../2026.8.6/MASTER_IMPLEMENTATION_PLAN.md)  
> - 架构：`.cursor/skills/actgame-architecture/`（ROADMAP / CONVENTIONS「服务器 / 权威进程」）  
> - 外部对照：`D:\Projects\DemoServer`（Handler/Room 壳；战斗权威不照搬）  
> 装配链：`本地 InputFrame → 权威 SimulationWorld.Step → ActorReplicationSnapshot → 本地预测纠偏 / 远端插值`

---

## 0. 一句话

组队 PVE 采用 **永劫无间同族的服务器（或 Listen Host）权威状态同步**：玩家只上行量化 `InputFrame`，权威端独跑现有 `SimulationWorld`，下行复制角色状态；本地预测自己的移动与出招表现，**命中 / HP / 硬直只认权威 Hitbox**。禁止全端同构重演作为联网主路径，禁止客户端上报伤害结果，禁止把永劫「攻击方客户端 Hitbox」作为首版近战权威，禁止 Offline/Online 两套模拟核长期并存。

---

## 1. 问题与动机

### 1.1 现状基线

```text
CombatWorldController
  → SimulationHost.Update
       SampleRenderFrame（本地 InputReader → InputFrameBuffer）
       accumulator → SimulationWorld.Step × N
         ProduceInput（敌人 Empty / Desire 另轨）
         全体 CharacterActor.Step（输入、选敌、ActionSim、Hitbox 收集、Motor）
         SoftBodySeparation
         CombatHitPipeline.Resolve / CompleteFrame
  → LateUpdate World.Render（前后 Pose 插值）

PlayerController = 场上唯一玩家（相机 / 刷怪 / 敌人感知 FindObjectOfType）
网络 / 房间 / 复制协议 = 无
L3 世界 Snapshot 无损恢复 = 未做
L5 FramePacket 广播 = 未做
```

| 点 | 现状 |
|----|------|
| 时钟 | 单机 `SimulationHost` 60Hz；权威在本机 `SimulationWorld` |
| 输入 | 量化 `InputFrame`（Move、按钮、`MoveReferenceYawQuantized`、TargetSwitch） |
| 动作 | `ActionSim` 整数帧；表现桥只读 Snapshot/Event |
| 位移 | `CharacterMotorSim` 毫米；软弹开在 World 帧末 |
| 命中 | `HitboxFrameConsumer` 在本地 `ApplyStep` 收集 → `CombatHitPipeline` 帧末结算 |
| 角色 | 每个 `CharacterActor` 都是完整 `ISimulationActor` |
| 玩家数 | 1 个 `PlayerController`；多处 `FindObjectOfType<PlayerController>()` |
| 选敌 | 每角色 `CharacterTargetingState.SelectedTargetId`；CameraLock 纯表现 |
| 原联网定案 | 锁步方案 L5：广播全员输入 + 客户端完整预测回滚 |

### 1.2 痛点

1. **锁步 L5 不匹配组队 PVE 产品**：2～4 人打怪需要晚加入、掉线、AI 只在权威端跑；全端 bit-identical 回滚成本高，且 L3 尚未具备。  
2. **永劫能跑 60 人近战**，靠的是专用服务器当裁判 + 本地先演动作，不是 15Hz 操作帧，也不是 1v1 GGPO。  
3. 当前 `CharacterActor` 把「权威步进」和「表现」绑在同一实例上，无法区分 Local / Remote / Authority。  
4. 命中发生在每个客户端自己的 Timeline 消费里；若直接多人开局，各端会各算各的刀。  
5. 场景入口假设唯一玩家，组队前就必须拆身份，否则相机/AI/刷怪无法接第二人。

### 1.3 目标

| 目标 | 说明 |
|------|------|
| 同步模型 | 权威端独跑 Sim；客户端跟状态；上行只有命令 |
| 手感 | 本地移动 / 出招动画零等待；伤害与受击以权威结果纠偏 |
| 人数 | 首版 **2 人 Listen Host 打同一波怪**；契约按 2～4 人设计 |
| 复用 | 保留 L0～L2 模拟核、招式资产、InputFrame、表现桥 |
| 可测 | NS1 起即可在无真实网络下用 Loopback 验证复制 |
| 不做 | 见 §1.4 |

### 1.4 明确不做

| 不做 | 原因 |
|------|------|
| 锁步 L5：广播全员 `FramePacket` 让各端同构 Step | 被本文取代；L0～L2 核保留 |
| 街霸式整世界 rollback 作为联网主路径 | 角色与 AI 数量不适合 8～16 帧全重演 |
| 永劫「攻击方客户端 Hitbox、防守方被拉回」作为 P0 | PVE 不需要低 ping 优势；首版服务器逻辑盒 |
| 客户端上报 HP / 伤害 / 「我打中了」为最终结果 | 反外挂与一致性 |
| 60 人圈地、搜刮、相关性裁剪到大逃杀规模 | 组队 PVE 不需要 |
| Photon/镜/自研 UDP 选型绑定本方案出口 | 先契约 + Loopback；传输可替换 |
| Offline 与 Online 两套 `CharacterActor.Step` | 单机 = 本地 Host |
| Agent 改 `.asset` / Prefab / Input Actions | 只列 Editor 步骤 |
| 完整匹配、排位、跨区、Host 迁移 | NS5 只做最小房间；其余另开 |

---

## 2. 设计原则

1. **权威只有一份**：`SimulationWorld` 只在 Authority 进程推进；Client 不得把本地 `ActionSim` 的命中写入 Numeric。  
2. **InputFrame 是上行命令，不是世界**：玩家设备 → 量化帧 → 权威 `ResolveLocal`；敌人仍走 `LocomotionDesire` / `ActionEntryRequest`，且只在权威端生产。  
3. **状态下行**：复制 `ActorReplicationSnapshot`（pose、动作、资源、受击事件），禁止常规下行 Transform 当唯一权威却不带动作帧。  
4. **预测只覆盖可逆表现**：移动与出招 Clip/VFX 可预测；HP、Death、HitStop、硬直、吸附是否生效以权威为准。  
5. **命中在权威逻辑盒**：沿用 `CombatHitPipeline` + `SimHitKey`，收集点改到权威 `CharacterActor.Step`；客户端 Hitbox 只可做刀光，不可 `Collect`。  
6. **角色三分角色，不许 if (isEnemy) 成网关**：差异用装配（AuthorityActor / PredictedLocalActor / RemoteProxy），禁止在 State 里写网角色分支。  
7. **单机即 Host**：Listen Server；一人进关也走 Authority World，避免日后双轨。  
8. **表现可丢**：CameraLock、Look、Lean、Impulse 不进复制。  
9. **零长期兼容**：NS4 出口后删除「每个客户端完整 Step 并本地结算命中」的联网幻想路径；不得保留 `LockstepNetworkHost` 与 `StateSyncHost` 双主路径。  
10. **结构优先**：传输是 `IReplicationTransport`；Host / Dedicated 只换传输与进程布局，不换 Snapshot 字段。

---

## 3. 目标架构

```text
┌──────────── Client ────────────┐     ┌──────── Authority (Host/DS) ────────┐
│ InputReader.Sample             │     │ InputFrameBuffer.SetRemote(player)  │
│  → 预测 Motor / 出招动画        │     │ EnemyBrain 只在此 Tick              │
│  → 发送 InputFrame             │────▶│ SimulationWorld.Step 60Hz           │
│                                │     │  CharacterActor（全员权威）          │
│ RemoteProxy 插值播队友/敌人     │◀────│  CombatHitPipeline 帧末结算         │
│ PredictedLocal 收权威纠偏       │     │  打包 ActorReplicationSnapshot[]    │
│ Camera / UI 只读 Local         │     └─────────────────────────────────────┘
└────────────────────────────────┘
```

### 3.1 三种角色运行时

| 装配 | 谁创建 | 跑什么 | 不跑什么 |
|------|--------|--------|----------|
| **AuthorityActor** | Host/DS | 完整现有 `CharacterActor.Step` + 命中收集 | 本地设备采样（远端玩家用收到的 InputFrame） |
| **PredictedLocalActor** | 拥有者客户端 | 预测位移、预测 Action 表现、采样 InputFrame | `CombatHitPipeline.Collect`、权威 Numeric 写入 |
| **RemoteProxy** | 所有客户端（含 Host 看别人） | 应用 Snapshot：pose 插值、Action Seek、受击表现 | `ActionSim.Step` 权威、BT、Hitbox Collect |

Host 本机玩家：**AuthorityActor + 本机预测层可合并为「Authority 上直接 Step，表现仍插值」**，避免 Host 自己再预测一套。定案：

- **Listen Host 本地玩家**：不预测，直接吃权威（本机 0 RTT）。  
- **远端客户端本地玩家**：PredictedLocal + 权威纠偏。  
- **所有人看到的他人 / 敌人**：RemoteProxy。

### 3.2 关键契约

```text
上行 ClientCommand
  frameHint / senderPlayerId
  InputFrame（MoveX/Y, Buttons*, MoveReferenceYawQuantized）
  不含 HP、命中、ActionName、世界坐标

下行 AuthorityTick
  authorityFrame
  actors[]: ActorReplicationSnapshot（按 SimActorId 排序）
  hits[]: ReplicatedHitEvent（可选独立通道，须带 frame + SimHitKey）
  spawns/despawns

ActorReplicationSnapshot（最小集）
  actorId, teamId, kind (Player/Enemy)
  posXMm, posZMm, posYMm, facingMilliDeg
  moveVxMm, moveVzMm                 // 远端插值用
  locomotionPhase, gait, cardinal    // 可后置；P0 可用 action 空闲时的 facing+speed
  actionId, graphNodeId, actionFrame, freezeFrames
  selectedTargetId                   // 仅复制给 Owner；他人可空
  healthMilli, flagsPacked
  vitalityEdge (None/Hit/Death)      // 边沿，防漏播
```

`InputFrame` 继续走现有 Merge/Carry 语义；权威端对迟到包：超过窗口丢弃，缺帧按现 `CarryForward`（延续 Move/Held，**不伪造 Pressed**）。

### 3.3 命中定案（相对永劫的裁剪）

```text
【永劫常见】攻击方客户端盒 → 申报命中 → 服务器认可 → 防守方拉回
【本方案 P0】权威 MotorSim 逻辑盒 → CombatHitPipeline → 复制 Hit/HP/硬直
【客户端】刀光与预受击可播；若权威未确认则取消或改播 Whiff
```

PVE 下怪物不会告你外挂；玩家之间也不做「谁 ping 低谁说了算」。若日后 PVP 再开 `NS-PVP` 讨论攻击方申报，不进本方案出口。

### 3.4 预测与纠偏（移动）

```text
PredictedLocal 每逻辑帧：
  1. 采样 InputFrame，立即 MotorSim.Step（与现 ResolveWorldMoveDirection 相同）
  2. 缓存 (frame, input, pose)
  3. 发送 InputFrame

收到 AuthorityTick.actor[self]：
  若 pose 与缓存对应 frame 误差 ≤ 阈值（建议水平 50mm）→ Ack，丢弃更旧缓存
  否则：吸附到权威 pose，重放其后未确认 InputFrame（仅移动，不重放命中）
```

出招预测（NS4）：本地可 `ActionSim` 推进 **仅用于 Clip/VFX/Cancel 手感**；权威 ActionId/帧到达后 Seek 对齐。若权威未起手（资源不足/硬直），本地取消预测招并播恢复。

### 3.5 边界

| 层 | 职责 | 不负责 |
|----|------|--------|
| `IReplicationTransport` | 可靠/不可靠发送字节 | 模拟、选敌、动画 |
| `ReplicationAuthority` | 收命令、Step World、发 Snapshot | 相机、本地预测 |
| `ReplicationClient` | 发命令、预测、应用 Snapshot | 敌人 BT、命中结算 |
| `RemoteProxy` | 插值与 Seek | 输入、AI |
| Camera / HUD | 只跟 LocalPlayer | 选敌权威、伤害 |

---

## 4. 范围声明

| 阶段 | 包含 | 不包含 |
|------|------|--------|
| NS0 | LocalPlayer 身份；删除唯一玩家假设 | 网络 |
| NS1 | Snapshot 结构、Loopback 权威→幽灵 | UDP、房间 |
| NS2 | 同机双视图：Host 走路/出招，Ghost 跟状态 | 预测、命中复制 |
| NS3 | 远端客户端预测位移 + 纠偏 | 出招预测 |
| NS4 | 出招预测；权威 Hitbox；受击复制 | 攻击方客户端盒 |
| NS5 | 2 人进关、生成同步、最小房间 | 匹配、排位、Host 迁移 |
| 以后 | Dedicated Server 进程、PVP 申报命中 | 本方案不承诺日期 |

---

## 5. 分阶段交付（任务 / 验收 / 出口）

> 勾选：未开始 `[ ]`；完成后 `[x]` 并在出口注明日期。

### NS0 — LocalPlayer 与场景入口

**任务**

- [x] 引入 `ILocalPlayer` / `LocalPlayerService`：当前拥有输入与相机的 `CharacterActor` 显式注入，禁止玩法再 `FindObjectOfType<PlayerController>()`  
- [x] 改 `CameraManager`、`EnemySpawnController`、`EnemyController` 感知、`CombatDebugHudController` 走注入或 Query  
- [x] `PlayerController` 增加 `IsLocalPredicted` 预留（NS0 单机恒为 Host 本地，值为 false）  
- [x] 敌人仇恨目标改为「感知范围内的玩家 Actor 列表」，不得写死唯一玩家 Transform  

**验收**

- [x] `rg "FindObjectOfType<PlayerController>" Assets/Scripts` 仅剩 Editor Gizmo 或已改为 LocalPlayer Query  
- [x] Play：单人进关、相机跟随、刷怪、敌人追打与改前一致  
- [x] Unity 编译通过  

**出口：** 场景不再假设全场只有一个玩家脚本。→ **已达成（2026-08-14）**

### NS1 — 复制快照契约（Loopback）

**任务**

- [x] 在 `ACTGame.Simulation` 增加无 Unity 依赖的 `ActorReplicationSnapshot` / `AuthorityTick` / `ClientCommand`  
- [x] `ReplicationSnapshotBuilder`：从 `CharacterMotorSim` + `ActionSim.Snapshot` + Numeric/Vitality 填最小集  
- [x] `IReplicationTransport` + `LoopbackReplicationTransport`（同进程队列，可模拟延迟/丢包）  
- [x] EditMode：序列化往返字段一致；`SimActorId` 排序稳定  

**验收**

- [x] `ActorReplicationSnapshotTests`：往返 Equals；缺字段默认安全  
- [x] Loopback 延迟 0 时 Host 连续 60 帧 Snapshot 的 `authorityFrame` 单调 +1  
- [x] 快照不含 CameraLock / Look / Lean  

**出口：** 无网络也能把权威状态编码成可应用的 Tick。→ **已达成（2026-08-14）**

### NS2 — RemoteProxy：同机第二视图跟状态

**任务**

- [x] 新增 `RemoteCharacterProxy`（或 `CharacterActor` 的 Remote 装配）：只读 Tick 应用 pose + `CharacterActionPresentationBridge` Seek  
- [x] 调试场景 / 第二 Camera：Host 控玩家 A，Ghost 显示 A 的复制体（可先同角色）  
- [x] **删除** Remote 路径上的 `HitboxFrameConsumer.Collect` 与 `EnemyBrain.Step`  
- [x] Host 仍用现有 `SimulationHost` 作为 Authority  

**验收**

- [ ] Play：Host 行走、出招，Ghost 在设定的 Loopback 延迟下跟动作帧与位置，不跑第二份命中  
- [x] Ghost 进程（或视图）`CombatHitPipeline` 无 Collect  
- [x] 延迟 100ms 时 Ghost 平滑插值，无权威 Pose 每帧瞬移（可用现有 Render 插值）  

**出口：** 他人角色可以只靠 Snapshot 播出来。→ **代码已落地（2026-08-14）；Play 待 Editor 确认**

### NS3 — 远端客户端预测位移

**任务**

- [ ] `PredictedLocomotionDriver`：用本地 `InputFrame` 推进 `CharacterMotorSim` 副本  
- [ ] 权威 pose 和解：阈值内忽略，超阈重放未确认移动  
- [ ] Listen Host 本地玩家 **不走预测**（直接权威）  
- [ ] 纠偏时表现层允许 SmoothDamp，禁止把表现 Pose 写回权威 Motor  

**验收**

- [ ] Loopback RTT=0：预测路径与 Host 本地路径位置误差 ≤ 1mm（同输入脚本）  
- [ ] Loopback RTT=100ms：本地 strafing 无「等服务器才动」；撞墙后会回拉一次  
- [ ] 单测：超阈重放后最终 pose 等于权威 + 后续输入  

**出口：** 非 Host 玩家走路手感接近单机，权威仍能纠正穿墙。→ **未达成**

### NS4 — 出招预测与权威命中

**任务**

- [ ] PredictedLocal 可推进只读 Action 表现（Clip/VFX）；**禁止**调用权威 `CombatHitPipeline.Collect`  
- [ ] Authority 继续现有 `HitboxFrameConsumer` → Pipeline → Numeric / Reaction  
- [ ] 下行 `ReplicatedHitEvent` / Vitality 边沿；客户端 `CharacterReactionService` 只消费复制事件  
- [ ] 权威未确认的预测招：取消或 Seek 到权威 ActionId/帧  
- [ ] 吸附 / Relocate 只在 Authority `ActionMotionResolver` 执行，客户端跟 pose，不本地算吸附权威  
- [ ] **删除**客户端 Numeric 因本地 Hitbox 扣血的任何路径  

**验收**

- [ ] Play 双视图：Host 砍木桩，Ghost 见受击与掉血，且只出现一次  
- [ ] 预测起手但权威硬直：本地招被取消，无双倍伤害  
- [ ] `rg "hitPipeline.Collect" Assets/Scripts` 仅出现在 Authority 装配  
- [ ] EditMode：权威结算与单机现行 Pipeline 对同一 Input 脚本伤害一致  

**出口：** 伤害与硬直以服务器逻辑盒为准；本地仍能立刻看到自己出招。→ **未达成**

### NS5 — 最小 2 人 PVE 房间

**任务**

- [ ] 最小房间：Host 创建、第二人加入、双方 `SimActorId` 分配、关卡内容版本校验  
- [ ] 第二玩家生成 `PredictedLocal` + 对方 `RemoteProxy`；敌人只在 Host 生成 AuthorityActor  
- [ ] 传输第二实现可先 UDP 或 Unity Transport，但必须走 `IReplicationTransport`  
- [ ] 掉线：该玩家 Actor 权威侧待机或 AI 接管 **二选一，本阶段定案为「待机 10s 后剔除」**  
- [ ] HUD 显示延迟与 `authorityFrame`  

**验收**

- [ ] 两台编辑器或 Host+Client：同一关打同一只怪，伤害不双算，怪只死一次  
- [ ] 第二人切敌 / 攻击使用各自 `SelectedTargetId`，互不影响镜头  
- [ ] 一人掉线后另一人可继续或房间结束（与剔除定案一致）  
- [ ] 无 `FindObjectOfType<PlayerController>` 玩法残留  

**出口：** 两人可进同一 PVE 关并完成击杀，同步模型闭合。→ **未达成**

---

## 6. 迁移与兼容

### 6.1 保留 / 迁入

| 保留 | 用法 |
|------|------|
| `SimulationWorld` / `SimulationHost` | Authority 时钟；Client 侧 Host 组件降级或拆成 `AuthorityHost` |
| `InputFrame` / `InputFrameBuffer` | 上行；权威 `Set` 远端玩家帧 |
| `ActionSim` + 表现桥 | Authority 真源；Remote Seek；Predicted 只读副本 |
| `CharacterMotorSim` | Authority + 预测副本 |
| `CombatHitPipeline` | **仅 Authority** |
| `CharacterTargetingState` | 每玩家权威一份；Owner 可复制 Id 给 UI |
| Locomotion / Graph / 烘焙表 | 不变 |

### 6.2 明确删除（联网终态，NS4～NS5）

| 删除 | 原因 |
|------|------|
| 锁步方案 L5 作为产品联网主路径 | 与状态下行冲突 |
| 客户端 `HitboxFrameConsumer` 写入 Pipeline | 双算伤害 |
| 玩法 `FindObjectOfType<PlayerController>` | 多玩家 |
| 客户端 `EnemyBrain.Step` | AI 只在权威端 |
| 为联网保留的第二套 `AimYaw` / PlanarBasis 旁路 | C-AT 已删，禁止回流 |
| `LockstepRoom` / 全员输入广播 Host（若实现中途出现） | 零双轨 |

单机 Play 在 NS5 前可继续现 `SimulationHost`；NS5 起单机必须等于 Listen Host（一人房间），不得留 `if (!network) 旧Host`。

### 6.3 对原锁步文档的关系

- **L0～L2**：仍是模拟核真源。  
- **L3 无损世界回滚**：降级为「能导出 ActorReplicationSnapshot」；不为 GGPO 服务。  
- **L5 FramePacket 广播 + 完整回滚**：产品联网 **取消**；历史章节保留作对照，实施以本文为准。

---

## 7. 目录与文件预期（增量）

```text
Assets/Scripts/Domain/Simulation/Replication/
  ActorReplicationSnapshot.cs
  AuthorityTick.cs
  ClientCommand.cs
  ReplicationSnapshotBuilder.cs

Assets/Scripts/Domain/Net/
  IReplicationTransport.cs
  LoopbackReplicationTransport.cs
  ReplicationAuthority.cs
  ReplicationClient.cs

Assets/Scripts/Domain/Character/Replication/
  ActionReplicationCatalog.cs
  CharacterReplicationCapture.cs
  RemoteCharacterProxy.cs
  RemoteCharacterProxyFactory.cs
  PredictedLocomotionDriver.cs        // NS3

Assets/Scripts/App/Controllers/Gameplay/
  LocalPlayerService.cs
  RemoteGhostViewController.cs
  AuthoritySimulationHost.cs          // 可由 SimulationHost 演进改名

Assets/Tests/EditMode/Simulation/
  ActorReplicationSnapshotTests.cs
  PredictedLocomotionReconcileTests.cs

docs/2026.8.13/TEAM_PVE_NARAKA_STYLE_STATE_SYNC_PLAN.md
```

传输第二实现（UDP）放 `Assets/Scripts/Infrastructure/Net/`，不得引用进 `ACTGame.Simulation`。

元数据 RPC（登录/背包）**不进 NS0～NS5**；若日后单开，Handler 放独立目录，禁止与 `ReplicationAuthority` 抢 `SimulationWorld`。

---

## 8. 风险与对策

| 风险 | 对策 |
|------|------|
| 出招预测与权威帧对不齐导致吞招 | NS4 先做「硬直则取消预测」；Cancel 窗以权威 ActionFrame 为准 |
| 远端动画抖动 | 插值读 `moveV*` + actionFrame；Seek 不每渲染帧硬切除非边沿 |
| 吸附在客户端先拉再被权威拉回 | 吸附只在 Authority；客户端跟 pose |
| Host 玩家 0 延迟、客机有延迟不公平 | PVE 可接受；Listen 模式写进已知限制；日后 Dedicated 拉齐 |
| 传输选型拖延 | NS1～NS4 只用 Loopback；NS5 才允许第二个 Transport |
| 与锁步 L5 文档打架 | ROADMAP / 本文 / 锁步文头互指；禁止两套 Host 实现 |
| 命中偷偷学永劫攻击方盒 | 验收 `Collect` 仅 Authority；PVP 另开方案 |
| Snapshot 膨胀 | P0 不做全效果列表；只 replicat 战斗必要字段 + 周期全量 |

---

## 9. Editor 人工步骤

1. 不改招式 `.asset`。  
2. NS0 后：在场景或架构里绑定 `LocalPlayerService` 到现有 Player Prefab（人工拖引用）。  
3. NS5：增加独立 Client 启动场景或同编辑器 ParrelSync / 第二实例；Input Actions 无需为网络新增（沿用现 Player Map）。  
4. 若用 ParrelSync：人工安装，Agent 不改工程第三方。  
5. Prefab 上若需挂 `RemoteCharacterProxy`，只改脚本并列出拖拽字段，不直接改 `.prefab`。  
6. **NS2 Play：** `CombatWorldController` 在 Editor 默认勾选 `previewRemoteGhost`（现有场景无需改 Prefab）。进入 Play 后角色右侧约 2m 出现幽灵，默认 Loopback 100ms。可在 Inspector 调 `remoteGhostWorldOffset` / `remoteGhostLatencyMs`，或取消勾选关闭预览。幽灵不进花名册、不跑命中。

---

## 10. 推荐开工顺序

```text
NS0 身份
  → NS1 Snapshot + Loopback
  → NS2 Ghost 跟动作
  → NS3 预测走路
  → NS4 权威命中 + 出招预测
  → NS5 两人进关
```

**最小可感切片：** NS2——同一个 Host 世界，第二视图只靠 Snapshot 看到角色走路和出招，零真实网络。

单机内容制作在 NS5 前不必停；新玩法仍写进 `SimulationWorld`，不要写进 RemoteProxy。

---

## 11. 与永劫 / 王者 / 街霸的对照（实施时勿混）

| | 本方案 | 永劫 | 王者 | 街霸 6 |
|--|--------|------|------|--------|
| 权威 | Host/DS 的 `SimulationWorld` | 专用服务器 | 各端同构 + 15Hz 输入帧 | 双方同构 60Hz |
| 上行 | `InputFrame` | 操作命令 | 操作 | 输入 |
| 下行 | 状态 Snapshot | 状态 | 操作帧 | 对方输入 |
| 近战盒 | **权威逻辑盒** | 偏攻击方客户端 | 各端同一逻辑 | 各端同一逻辑 |
| 本地手感 | 预测移动/出招 | 先演再纠 | 等逻辑帧 | 回滚重演 |
| 人数假设 | 2～4 PVE | 60 BR | 10 | 2 |

学永劫的是 **上下行分工和服务器裁判**；不学它的攻击方 Hitbox 与大逃杀规模。

---

## 12. 变更日志

| 日期 | 说明 |
|------|------|
| 2026-08-13 | 初版：组队 PVE 改永劫式状态同步；命中定案为权威逻辑盒；L5 输入广播降为历史对照 |
| 2026-08-14 | 补 §13 服务器代码规范：对照 Source / 守望 / NetCode Ghosts 与 DemoServer；写入 CONVENTIONS |
| 2026-08-14 | NS0 代码：ILocalPlayer / LocalPlayerService / 感知最近玩家；玩法删除 FindObjectOfType&lt;PlayerController&gt; |
| 2026-08-14 | NS1 代码：复制 Snapshot/Tick/Command + Loopback 传输；EditMode 往返与 60 帧单调 |
| 2026-08-14 | NS1 验收关闭；NS2 代码：RemoteProxy + Loopback 同机幽灵；Play 待确认 |

---

## 13. 服务器代码规范（权威进程怎么写）

> 落地约定同步在 `.cursor/skills/actgame-architecture/CONVENTIONS.md`「服务器 / 权威进程」。本节说明对照来源与本项目取舍。

### 13.1 开源 / 工业界：战斗服务器在写什么

这些项目的**战斗服不是技能 Handler 列表**，而是「收命令 → 跑与客户端同构的移动/射击码 → 下发快照」。

| 来源 | 服务器怎么写 | 本项目对应 |
|------|----------------|------------|
| Valve Source SDK 2013：`game/shared/usercmd.h`、`game/client/prediction.cpp`；[Source Multiplayer Networking](https://developer.valvesoftware.com/wiki/Source_Multiplayer_Networking) | `CUserCmd`（按钮、wish 方向、视角）上行；`game/shared` 移动码 Host/Client 共用；服务器 Snapshot；客户端预测错了回退再重放未确认 cmd | `InputFrame` = usercmd；`ACTGame.Simulation` = shared；NS3 纠偏重放移动 |
| Timothy Ford, GDC 2017 *Overwatch Gameplay Architecture and Netcode* | 约 16ms command frame；专用服跑完整模拟；客户端预测自己（含技能）；下行实体快照；Hitscan 可倒带 | 60Hz `Step` + Snapshot；P0 **不做** Hitscan 倒带 |
| Unity Netcode for Entities（Ghosts） | Ghost 分 Predicted（拥有者）与 Interpolated（他人）；命令只从 Owner 来 | `PredictedLocalActor` / `RemoteProxy` |
| Glenn Fiedler, *Snapshot Interpolation* | 下行是状态，不是输入回放 | `AuthorityTick`，禁止锁步 L5 |
| Mirror / Fish-Net / NGO `ServerRpc` | 适合开门、买东西；**不适合**每帧近战 | 元数据 RPC 可学；战斗流禁止逐招 Rpc |

Dedicated 与 Listen 的差别只是**进程里有没有本地玩家**。Source / 守望都是同一份 sim；Unity Dedicated Server 只是去掉渲染。本项目 Dedicated 不得重写 Motor/Action。

### 13.2 DemoServer（`D:\Projects\DemoServer`）怎么写、哪些能学

独立 C# 进程（`ZZZServer/Program.cs`）：Kirara 收包 → Handler → Service/Model；`RoomService.Update` 驱动房间；Protobuf 消息。这是典型 **MMO 大厅 + 房间广播**，不是 60Hz 输入驱动 ACT。

```text
DemoServer 战斗实际路径（不要当 ACT 权威）
  客户端上报位姿  MsgUpdateFromAutonomous → Player.UpdateFromAutonomous
  客户端上报技能名 MsgRolePlayAction → 转播 NotifyOtherRolePlayAction
  客户端上报伤害  MsgMonsterTakeDamage → monster.hp -= msg.Damage → Broadcast
  房间每拍       Room.Update(dt) 清怪 AI + Broadcast NSyncPlayer / NSyncMonster
```

| 学 | DemoServer 位置 | 用在本项目 |
|----|-----------------|------------|
| Handler 保持薄 | `Handler/Account/ReqLogin_Handler.cs` | 元数据 RPC；战斗 Handler 若有，只入队 `InputFrame` |
| 房间生命周期 | `Service/Room.cs` Add/Remove/Broadcast | NS5 房间；广播体是 `AuthorityTick` 不是 `NSync*` |
| 快照字段与持久化分离 | `Role.NSyncRole` vs `NRole` | 复制用 Snapshot；存档另开，禁止 Player 档写 Motor |
| 断线入队再改房间 | `EnqueueTask` → `RemovePlayer` | 断线不得从网络线程直接 `World` 删 Actor |
| 配置与协议生成 | Luban / Proto | P0 不引入；Snapshot 先手写 struct + EditMode 往返 |

| 不学 | 原因 |
|------|------|
| `UpdateFromAutonomous` 信任客户端坐标 | 与 MotorSim 权威冲突 |
| `MsgMonsterTakeDamage` 信任 `msg.Damage` | 方案已禁客户端报伤；永劫攻击方盒也不做 P0 |
| `MsgRolePlayAction` 字符串招式名 | 对不齐 `ActionSim` 整数帧 / Cancel |
| `Room.Update(float dt)` 当战斗钟 | 必须 60Hz `SimulationWorld.Step` |
| `MonsterCtrl` 自建秒制状态机 | 敌人已有 `EnemyBrain` + `CharacterActor` |
| `Player` 同时是 Mongo 档和同步体 | 进房映射 `playerId → SimActorId` 即可 |

### 13.3 权威进程伪代码（唯一写法）

```text
网络线程
  收包 → 反序列化 ClientCommand → 入队（不 Step）

模拟线程（60Hz）
  出队 InputFrame → InputFrameBuffer.SetRemote(playerId)
  缺帧 CarryForward（延续 Move/Held，不伪造 Pressed）
  EnemyBrain 只在此进程 Produce Desire / EntryRequest
  SimulationWorld.Step
    CharacterActor.Step（权威装配，含 Hitbox Collect）
    SoftBodySeparation
    CombatHitPipeline.Resolve
  ReplicationSnapshotBuilder.Build → AuthorityTick
  IReplicationTransport.SendToAll(tick)

客户端
  自己：预测 Motor / 出招表现；Tick 到达则纠偏 / Seek
  他人与怪：RemoteProxy 插值
  禁止 Collect
```

Listen Host 本地玩家跳过预测，直接权威。Dedicated 上所有玩家都是远端，一律 PredictedLocal。

### 13.4 实现检查（NS1 起每次 PR）

- [ ] 新战斗规则写在 `SimulationWorld` / Actor / Pipeline，不写在 Handler
- [ ] 新上行字段只能进 `InputFrame` / `ClientCommand`，不能是 Damage、HP、世界坐标、ActionName
- [ ] `rg "hitPipeline.Collect"` 仅 Authority 装配
- [ ] `ACTGame.Simulation` 无 Unity、无 `Infrastructure/Net` 引用
- [ ] 无 `LockstepNetworkHost` 与 `StateSyncHost` 双主路径
