# NetSync：通用网络核心 / ACT 业务层分离优化方案

> 制定：2026-08-17  
> 角色：**NetSync 通用化重构实施真源**（先分层、后增强；不改变当前服务器权威状态同步产品路线）  
> 代码基线：`NetSync@3f695f93865a29f92a09fedfe60788a620900419`  
> 相关：  
> - 总开发排期：[`NETSYNC_FRAMEWORK_DEDICATED_MASTER_DEVELOPMENT_PLAN.md`](./NETSYNC_FRAMEWORK_DEDICATED_MASTER_DEVELOPMENT_PLAN.md)（W0～W4 完成 GF0～GF4；W5 起进入 Dedicated）  
> - 当前架构分析：[`NETSYNC_ARCHITECTURE_ANALYSIS_AND_FRAMEWORK_COMPARISON.md`](./NETSYNC_ARCHITECTURE_ANALYSIS_AND_FRAMEWORK_COMPARISON.md)  
> - 当前实现真源：`docs/2026.8.15/NETWORK_SYNC.md`  
> - 客机预测：`docs/2026.8.15/UE_ALIGNED_CLIENT_PREDICTION_PLAN.md`  
> - 统一角色座位：`docs/2026.8.15/UNIFIED_CHARACTER_ACTOR_SEAT_PLAN.md`  
> 目标装配链：`Transport → Session → Replication Runtime → Prediction Runtime → ACT Adapter → Presentation`  
> **约束：** 重构期间保持当前双人 Listen Host 可玩；禁止通用层反向依赖 `CharacterActor`、`ActionDefinition`、`CombatHitPipeline`、Unity 表现对象

---

## 0. 一句话

把当前“通用网络思想 + ACT 专用房间实现”拆成：

```text
ACTNet（可复用）
  连接 / 通道 / 时钟 / 消息 / 实体生命周期 / 快照调度 / 预测历史 / 纠偏协调

ACTGame.Networking（业务适配）
  InputFrame / CharacterActor / ActionFrame / Numeric / CombatHit / RemoteCharacterProxy

App（Unity 装配）
  场景角色 / Host·Client 启动 / CharacterConfig / Prefab / HUD
```

框架目标是**可复用于其他 Unity 固定 Tick 动作游戏**，不是重写 Unreal、FishNet 或任意品类通用 RPC 引擎。

---

## 1. 问题与动机

### 1.1 当前已经做对的边界

```text
SimulationWorld / ActionSim
  不依赖 UDP

CombatHitPipeline
  不依赖 Room

IReplicationTransport
  只收发 byte[]

RoomCodec / ReplicationCodec
  与 Socket 分离

Authority / Autonomous / RemoteProxy
  已形成清晰角色语义
```

这些设计应保留，不能在通用化过程中退回：

- `NetworkBehaviour` 侵入 Domain；
- 技能名 RPC；
- 客户端上报 Transform / Damage；
- `if (isClient)` 散落每个 State；
- Offline / Online 两套玩法入口。

### 1.2 当前主要耦合

`ReplicationRoomHost` 同时负责：

```text
UDP Bind / Pump
  + Join / Heartbeat / Kick
  + PlayerId / ActorId 分配
  + CharacterActor 创建
  + CharacterConfig / EnemyConfig 收集
  + InputFrame 灌入
  + ActionReplicationCatalog
  + ActorSnapshot Capture
  + HitEvent Capture
  + AuthorityTick 广播
  + Guest 生命周期
  + HUD
```

`ReplicationRoomClient` 同时负责：

```text
UDP Connect / Pump
  + Join / Heartbeat
  + 渲染帧输入采样
  + Command 冗余
  + Autonomous CharacterActor.Step
  + Locomotion Reconcile
  + Action Ack
  + RemoteProxy 创建/销毁
  + TargetSystem 注册
  + 软弹开
  + HitStop / Hit Cue
  + HUD
```

这两个类是当前最大的复用阻塞点：它们既是网络 Session，又是 ACT Gameplay Driver，还是 Unity View Controller。

### 1.3 当前契约的业务绑定

| 当前类型 | 绑定内容 | 为什么不能直接放进通用层 |
|----------|----------|--------------------------|
| `InputFrame` | MoveX/Y、按钮 bitset、MoveReferenceYaw | 是 ACT 控制语义，不是所有游戏都有 |
| `ActorReplicationSnapshot` | Locomotion、Action、HP、Target、Freeze | 是角色状态，不是通用网络实体 |
| `ReplicatedHitEvent` | Attacker、Hitbox、ActionId、落点 | 是战斗事件 |
| `ActionReplicationCatalog` | `ActionDefinition` / `CharacterConfig` | 是内容资产映射 |
| `CharacterReplicationCapture` | `CharacterActor` / `Numeric` / `Animation` | 是业务快照采集 |
| `PredictedLocomotionDriver` | `CharacterMotorSim` / Action Gate | 是角色预测策略 |
| `RemoteCharacterProxy` | 动画、Timeline、Targetable、VFX/SFX | 是 ACT 表现适配 |

### 1.4 当前基础设施缺口

当前 `IReplicationTransport` 只有：

```text
SendClientToAuthority
SendAuthorityToClients
Pump
TryDequeueAuthority
TryDequeueClient
```

它无法表达：

- 多连接定向发送；
- ConnectionId；
- 可靠有序 / 不可靠时序通道；
- Disconnect reason；
- Sequence / ACK；
- MTU / fragmentation；
- 发送预算；
- 连接级指标。

当前状态广播和 Session 逻辑因此只能围绕“Host + 一个 UDP Endpoint”生长。

### 1.5 动机

| 目标 | 价值 |
|------|------|
| 新 ACT 项目复用 | 不再重写 UDP 房间、实体生命周期、Snapshot 缓冲和预测历史 |
| 当前项目可维护 | Room 不再承担 Character / Proxy / HUD 全部职责 |
| Dedicated Server | Session 与 Unity 本机玩家解耦后可替换宿主 |
| 规模扩展 | 为 per-connection relevancy、delta、发送预算留出位置 |
| 测试 | 通用层可用 FakeGame / Loopback 独立验证，不启动 Unity 场景 |
| 故障定位 | Transport、Session、Replication、Prediction、Gameplay 指标分开 |

---

## 2. 目标、定位与不做

### 2.1 框架定位

> **面向 Unity 固定 Tick 动作游戏的服务器权威复制框架。**

框架提供：

- Client / Listen Host / Dedicated Server 会话模型；
- Connection / Player / Entity 稳定身份；
- 可靠控制、不可靠命令、不可靠时序快照、可靠事件通道；
- 命令上行、状态下行；
- Entity Spawn / Despawn；
- Authority / Owner / Observer 网络角色；
- Snapshot History 与插值时间线；
- Owner Command History、ACK、Restore / Replay 协调；
- per-connection 可见性和更新预算扩展点；
- Codec、版本、内容指纹和网络指标。

ACT 模块提供：

- 角色输入；
- 角色状态快照；
- 走跑和动作预测；
- 命中与生命事件；
- Action 资产映射；
- Proxy 动画与表现；
- Targeting / CameraLock 本地只读接缝。

### 2.2 复用等级

| 等级 | 范围 | 本方案目标 |
|------|------|------------|
| R0 | 仅当前双人 Demo | 当前已达成 |
| R1 | 当前项目不同角色 / 关卡 | 重构后必须达成 |
| R2 | 其他 Unity 固定 Tick ACT/PVE | **本方案主目标** |
| R3 | 射击 / 赛车等其他预测游戏 | 仅保证核心可扩展，不承诺即插即用 |
| R4 | Unreal/FishNet 级任意品类中间件 | 不做 |

### 2.3 明确不做

- 不做通用 `[SyncVar]`、`[Command]`、`[ClientRpc]` 属性系统；
- 不做任意 MonoBehaviour 自动联网；
- 不做反射扫描网络字段；
- 不做完整 Gameplay Ability 网络层；
- 不做 MMO Replication Graph；
- 不在本轮实现 PVP 历史回溯；
- 不在分层重构中同时改动作、Locomotion 或命中规则；
- 不以“框架化”为理由重写 `SimulationWorld`；
- 不同时保留新旧两套长期 Room 主路径；
- 不把 `byte[]` 到处暴露给 ACT 业务代码。

---

## 3. 设计原则

1. **通用层不知道 ACT**：不得引用 `CharacterActor`、`ActionDefinition`、`CharacterConfig`、`CombatHitPipeline`、`RemoteCharacterProxy`。  
2. **业务层不知道 Socket**：ACT Adapter 不直接 `UdpClient.Send/Receive`。  
3. **App 只装配**：MonoBehaviour 负责生命周期、Inspector 和 View，不实现协议算法。  
4. **协议头通用、正文业务注册**：Session / Entity / Sequence 在通用头；Input / Action / Combat 在 ACT Payload。  
5. **网络角色与玩法能力分离**：`NetReplicaRole` 描述网络视角；`ReplicationSeat` 描述 ACT Actor 能力。  
6. **命令与状态显式建模**：不用任意 RPC 替代 `InputFrame`、Snapshot 和 Event。  
7. **先搬家，后优化**：第一阶段只移动职责，不同时改线格式和玩法结果。  
8. **单一主路径**：每完成一阶段就删除对应旧入口，禁止永久 Adapter 套 Adapter。  
9. **第二用例证明复用**：用无 ACT 依赖的 FakeEntity 测试证明通用层，不要求制作第二款游戏。  
10. **稳定身份**：网络只传 `NetPlayerId` / `NetEntityId` / `NetArchetypeId`，不传 Unity 实例引用。  
11. **连接级状态**：ACK、baseline、relevancy 和预算属于连接，不属于全局广播器。  
12. **可观测性先行**：每个层级必须暴露计数和诊断，不允许只靠 `Debug.Log` 看“能不能玩”。  
13. **兼容当前权威语义**：Host 独占 AI、Numeric、Hitbox Collect 和 Combat Resolve。  
14. **表现不回写权威**：Remote 插值、VFX、CameraLock 永远只读复制结果。  
15. **框架边界可由程序集验证**：不是靠文档约定，而是靠 asmdef 引用方向与测试证明。

---

## 4. 目标架构

### 4.1 六层结构

```text
┌──────────────────────────────────────────────────────────────┐
│ App / Unity Bootstrap                                        │
│ CombatWorldController / NetGameController / HUD / Prefab     │
└─────────────────────────────┬────────────────────────────────┘
                              │ Configure / Bind Views
┌─────────────────────────────▼────────────────────────────────┐
│ ACTGame.Networking                                           │
│ CharacterCommandCodec / CharacterSnapshotSchema              │
│ ActAuthorityAdapter / ActOwnerPredictionAdapter              │
│ ActRemoteProxyFactory / CombatEventAdapter                    │
└─────────────────────────────┬────────────────────────────────┘
                              │ 注册 Payload / Entity Adapter
┌─────────────────────────────▼────────────────────────────────┐
│ ACTNet.Prediction                                            │
│ CommandHistory / StateHistory / Ack / Reconcile / Smoothing  │
├──────────────────────────────────────────────────────────────┤
│ ACTNet.Replication                                           │
│ EntityRegistry / Spawn / Despawn / Snapshot / Relevancy      │
├──────────────────────────────────────────────────────────────┤
│ ACTNet.Session                                               │
│ Handshake / Connection / Player / RoomState / MessageRouter  │
├──────────────────────────────────────────────────────────────┤
│ ACTNet.Transport                                             │
│ Channels / Packet / Sequence / UDP·Loopback Adapter          │
├──────────────────────────────────────────────────────────────┤
│ ACTNet.Core                                                  │
│ IDs / Tick / Result / Buffer / ProtocolVersion / Metrics     │
└──────────────────────────────────────────────────────────────┘
```

### 4.2 依赖方向

```text
ACTNet.Core
    ▲
    ├── ACTNet.Transport
    ▲
    ├── ACTNet.Session
    ▲
    ├── ACTNet.Replication
    ├── ACTNet.Prediction
    ▲
ACTGame.Networking ─────► ACTGame.Simulation
    ▲
App / Unity
```

禁止：

```text
ACTNet.* ──X──► ACTGame.*
ACTNet.* ──X──► UnityEngine
ACTGame.Simulation ──X──► App
Transport ──X──► Snapshot / Character
```

### 4.3 数据流

```text
Client Device
  → ACT InputSampler
  → CharacterCommandCodec
  → ACTNet Command Channel
  → Server Session
  → ActAuthorityAdapter
  → SimulationWorld.InputFrames
  → CharacterActor / CombatHitPipeline
  → CharacterSnapshotSchema
  → ReplicationFrame
  → ACTNet Snapshot Channel
  → Client ReplicationRuntime
      ├─ Owner → PredictionCoordinator → ActOwnerPredictionAdapter
      └─ Observer → ActRemoteProxyFactory → RemoteCharacterProxy
```

### 4.4 网络角色与 ACT Seat

通用层定义：

```text
NetReplicaRole.Authority
NetReplicaRole.Owner
NetReplicaRole.Observer
```

ACT 层映射：

| NetReplicaRole | ACT 结果 |
|----------------|----------|
| Authority | 创建 `ReplicationSeat.Authority` 的 `CharacterActor`，注册 World |
| Owner | 创建 `ReplicationSeat.Autonomous` 的 `CharacterActor`，交 Prediction Adapter |
| Observer | 不创建 `CharacterActor`，由 `ActRemoteProxyFactory` 创建 Proxy |

两者不能合并成一个枚举：

- `NetReplicaRole` 是框架的网络身份；
- `ReplicationSeat` 是本游戏的能力装配；
- 其他游戏可以把 Owner 映射成 Vehicle、Pawn 或其他对象。

---

## 5. 通用层职责与契约

### 5.1 ACTNet.Core

包含：

```text
NetConnectionId
NetPlayerId
NetEntityId
NetArchetypeId
NetTick
NetSequence
NetworkProtocolVersion
ContentFingerprint
NetResult / DisconnectReason
NetBufferReader / NetBufferWriter
NetMetricsSnapshot
```

要求：

- 全部为纯 C#；
- 不持有 Unity Object；
- Id 可比较、可哈希、有 Invalid；
- Writer / Reader 明确小端；
- Reader 必须验证长度和上限；
- ContentFingerprint 不再只是手填 `int contentVersion`。

### 5.2 ACTNet.Transport

#### 端口

目标接口表达：

```text
StartServer(endpoint)
StartClient(endpoint)
Poll()
Send(connectionId, channel, payload)
TryReceive(out packet)
Disconnect(connectionId, reason)
Connections
Metrics
```

`NetChannel` 最少定义：

| 通道 | 语义 | 用途 |
|------|------|------|
| ControlReliableOrdered | 可靠有序 | Join、Accept、Kick、Ready、Spawn 配置 |
| CommandUnreliableRedundant | 不可靠 + 业务冗余 | InputFrame 批次 |
| SnapshotUnreliableSequenced | 不可靠时序 | 最新 ReplicationFrame；旧包丢弃 |
| EventReliableOrdered | 可靠有序或事件序列冗余 | 不可从状态恢复的关键边沿 |

#### 适配器

```text
LoopbackTransport
UdpTransport（当前迁移适配）
后续可选 LiteNetLibTransport / UnityTransportAdapter
```

第一阶段不要求马上替换 UDP 库，但接口必须取消：

```text
SendAuthorityToClients(byte[])
```

改成：

```text
foreach connection:
    Send(connection, channel, packet)
```

因为 delta、relevancy、ACK 都是 per-connection。

### 5.3 ACTNet.Session

包含：

```text
ServerSession
ClientSession
ConnectionRegistry
PlayerRegistry
HandshakeStateMachine
SessionMessageRouter
SessionClock
RoomState
```

Session 只处理：

- 连接建立和断开；
- 协议版本；
- ContentFingerprint；
- PlayerId 分配；
- Room capacity；
- Ready / Playing / Ending；
- 心跳和超时；
- 调用 Gameplay Session Hook。

Session 不处理：

- CharacterConfig；
- PlayerController；
- EnemySpawnController；
- Actor 快照字段；
- Hit Cue；
- TargetSystem；
- Locomotion。

业务接缝：

```text
IGameSessionHandler
  OnPlayerAccepted(playerId, connectionId)
  OnPlayerDisconnected(playerId, reason)
  OnSessionStarted(startTick)
  OnSessionEnded(reason)
```

### 5.4 ACTNet.Replication

包含：

```text
ReplicationServer
ReplicationClient
ReplicatedEntityRegistry
ReplicationFrame
EntityRecord
SpawnRecord
DespawnRecord
ReplicationSchemaRegistry
VisibilityPolicy
UpdatePriorityPolicy
SnapshotHistory
```

#### 通用帧

概念布局：

```text
ReplicationFrame
  serverTick
  frameSequence
  commandAck
  spawns[]
  despawns[]
  entities[]
  eventSequenceRange

EntityRecord
  entityId
  archetypeId
  ownerPlayerId
  roleFlags
  payloadSchemaId
  payloadBytes
```

通用层只理解：

- 实体身份；
- 类型；
- Owner；
- 生命周期；
- Tick；
- Sequence；
- Payload schema；
- 可见性与预算。

通用层不理解：

- ActionId；
- HP；
- Locomotion；
- Hitbox。

#### Schema

业务注册：

```text
IReplicationSchema
  SchemaId
  Capture(authorityEntity, writer)
  Read(reader, snapshot)
  ApplyOwner(snapshot)
  ApplyObserver(snapshot)
```

首轮迁移允许整个 `ActorReplicationSnapshot` 作为一个 ACT Schema Payload，以保持线格式和行为。

第二轮再拆为：

```text
CharacterPoseSchema
CharacterLocomotionSchema
CharacterActionSchema
CharacterVitalitySchema
CharacterTargetSchema
```

禁止在 GF2 首次搬迁时同时上 Delta，避免无法判断回归来自分层还是压缩。

### 5.5 ACTNet.Prediction

通用层负责：

```text
CommandHistory<TCommand>
PredictedStateHistory<TState>
PredictionAckTracker
PredictionCoordinator
SnapshotTimeline<TState>
ReconcileMetrics
```

通用协调流程：

```text
RecordCommand(command)
CapturePredictedState(tick)
ReceiveAuthorityState(ackTick, state)
Compare(authority, predictedAtAck)
DropAcknowledgedCommands()
if correction required:
  Restore(authority)
  Replay(unacknowledged commands)
Publish correction metrics
```

业务适配：

```text
IPredictionModel<TCommand, TState>
  Capture()
  Restore(authorityState)
  Simulate(command)
  MeasureError(authority, predicted)
  ResolvePolicy(authority, context)
```

ACT 专用逻辑继续留在适配器：

- Action / Hit / Death 时禁止普通走跑 Replay；
- TargetAdhesion / Relocate / SoftBodySuppress Gate；
- 2m 硬吸阈；
- ActionAck 的变体与连招超前语义；
- SnapPresentation；
- Predicted HitStop。

不要把上述规则塞进通用 `PredictionCoordinator`。

### 5.6 通用层可观测性

最低指标：

| 层 | 指标 |
|----|------|
| Transport | sent/recv bytes、packet count、drop、out-of-order、RTT、jitter |
| Session | connection count、handshake state、timeout、reject reason |
| Replication | entities/frame、bytes/frame、spawn/despawn、budget drop、largest packet |
| Prediction | pending commands、ack tick、error mm（由业务命名）、snap count、replay count |
| Gameplay Adapter | action cancel、authority hit edge、proxy count、unknown content id |

框架输出结构化指标，HUD 只负责显示。

---

## 6. ACT 业务层职责

### 6.1 ACTGame.Networking

保留：

```text
CharacterCommand
  包装现有 InputFrame

CharacterSnapshot
  由现有 ActorReplicationSnapshot 演进

CharacterReplicationSchema
  Capture / Write / Read / Apply

ActAuthorityReplicationAdapter
  Client Command → InputFrameBuffer
  CharacterActor → Snapshot
  Combat FrameHits → Combat Event

ActOwnerPredictionAdapter
  CharacterActor.Step
  PredictedLocomotionDriver 业务策略
  PredictedActionAckQueue

ActRemoteProxyFactory
  CharacterSnapshot → RemoteCharacterProxy

ActContentRegistry
  CharacterArchetype / Action / Reaction / GraphNode 稳定 Id
```

### 6.2 App / Unity

只负责：

- Inspector 配置；
- 启动 Host / Client / Dedicated；
- CharacterConfig 与 Prefab 绑定；
- Scene 生命周期；
- 注册 `ActGameSessionHandler`；
- 创建本机玩家 View；
- 注册 Proxy View；
- HUD；
- ParrelSync / Editor 启动覆盖。

`CombatWorldController` 最终只做：

```text
Resolve launch settings
Ensure SimulationHost
Build NetCompositionRoot
Start Session
```

不能继续承担协议细节。

### 6.3 业务内容注册

当前 `ActionReplicationCatalog` 已使用稳定哈希，但通用化后需要统一 Content Registry：

```text
ActContentManifest
  contentFingerprint
  characterArchetypes[]
  actions[]
  graphNodes[]
  reactions[]
```

要求：

- 构建期或启动期生成稳定 Id；
- 同名冲突直接失败，不在运行时线性探测后默默换 Id；
- Join 比对完整 Fingerprint；
- 网络不再高频发送 `GraphNodeId` 字符串；
- 未知 Id 必须产生明确断线或内容错误，不静默回退第一个敌人配置。

---

## 7. 消息与协议分层

### 7.1 通用包头

```text
NetPacketHeader
  magic
  protocolVersion
  sessionEpoch
  connectionId
  channel
  messageType
  sequence
  payloadLength
```

通用层读取到 Payload 后，再交注册的业务 Codec。

### 7.2 消息分类

| 分类 | 所属层 | 示例 |
|------|--------|------|
| Transport | Transport | ACK、fragment、keepalive |
| Session | Session | Join、Accept、Reject、Kick、Ready |
| Replication | Replication | Frame、Spawn、Despawn、BaselineAck |
| Gameplay Command | ACT | Character InputFrame |
| Gameplay Event | ACT | Hit Cue、不可恢复战斗边沿 |

### 7.3 Codec 拆分

当前：

```text
RoomCodec
  Join / Heartbeat / Kick
  + ClientCommandBatch
  + AuthorityTickEnvelope

ReplicationCodec
  ClientCommand
  + ActorSnapshot
  + HitEvent
```

目标：

```text
ACTNet.Core
  NetBufferReader / NetBufferWriter

ACTNet.Session
  SessionCodec

ACTNet.Replication
  ReplicationFrameCodec

ACTGame.Networking
  CharacterCommandCodec
  CharacterSnapshotCodec
  CombatEventCodec
```

### 7.4 迁移兼容

第一阶段：

- 保留 `RoomCodecVersion=1`；
- 新 Session/Replication 层内部调用旧 Codec；
- 建 Characterization Tests 固定字节布局。

第二阶段：

- 升级协议版本；
- 切换新包头和通道；
- Host / Client 同一提交切换；
- 不维护跨大版本兼容。

---

## 8. 程序集与目录规划

### 8.1 目标程序集

```text
Assets/Scripts/Framework/ACTNet/
  Core/
    ACTNet.Core.asmdef
  Transport/
    ACTNet.Transport.asmdef
  Session/
    ACTNet.Session.asmdef
  Replication/
    ACTNet.Replication.asmdef
  Prediction/
    ACTNet.Prediction.asmdef

Assets/Scripts/Domain/
  Simulation/
    ACTGame.Simulation.asmdef
  Networking/
    ACTGame.Networking.asmdef

Assets/Scripts/App/
  Networking/
    ACTGame.App.Networking.asmdef
```

### 8.2 引用矩阵

| 程序集 | 可引用 |
|--------|--------|
| `ACTNet.Core` | 无 |
| `ACTNet.Transport` | `ACTNet.Core` |
| `ACTNet.Session` | `ACTNet.Core`、`ACTNet.Transport` |
| `ACTNet.Replication` | `ACTNet.Core`、`ACTNet.Session` |
| `ACTNet.Prediction` | `ACTNet.Core` |
| `ACTGame.Simulation` | 现有纯模拟依赖 |
| `ACTGame.Networking` | `ACTNet.*`、`ACTGame.Simulation`、必要的 ACT Domain |
| `ACTGame.App.Networking` | Unity、App、`ACTGame.Networking` |

### 8.3 Unity 引用政策

| 层 | UnityEngine |
|----|-------------|
| ACTNet.Core | 禁止 |
| ACTNet.Transport | 禁止；仅 .NET Socket 或第三方网库 |
| ACTNet.Session | 禁止 |
| ACTNet.Replication | 禁止 |
| ACTNet.Prediction | 禁止 |
| ACTGame.Networking | 尽量禁止；资产解析可拆 Unity Adapter |
| App.Networking | 允许 |

`RemoteCharacterProxy`、动画、VFX、GameObject Factory 必须留在 Unity 允许层。

### 8.4 测试程序集

```text
Assets/Tests/EditMode/ACTNet/
  ACTNet.Core.Tests
  ACTNet.Transport.Tests
  ACTNet.Session.Tests
  ACTNet.Replication.Tests
  ACTNet.Prediction.Tests

Assets/Tests/EditMode/Networking/
  ACTGame.Networking.Tests

Assets/Tests/PlayMode/Networking/
  ACTGame.Networking.PlayModeTests
```

---

## 9. 分阶段交付（任务 / 验收 / 出口）

> 勾选：未开始 `[ ]`；完成后 `[x]`。  
> 阶段编号 `GF` = Generic Framework。  
> 每阶段都要求现有单人 Listen Host 和双进程联机回归；禁止积累到最后一次性切换。

### GF0 — 行为冻结与依赖审计

**任务**

- [ ] 为当前 `RoomCodec`、`ReplicationCodec` 建 Golden Bytes 测试。  
- [ ] 固定当前 Join、CommandBatch、AuthorityTick 往返测试。  
- [ ] 固定 Host 一帧调用顺序：收输入 → World.Step → Capture → Send。  
- [ ] 固定 Client 一帧调用顺序：收 Tick → Reconcile → 采样 → 预测 → Send。  
- [ ] 记录当前类依赖图和程序集引用。  
- [ ] 建双进程人工回归脚本：移动、出招、连招、受击、死亡、CameraLock、断线。  
- [ ] 记录基准指标：Tick bytes、GC alloc、Proxy count、prediction pending。  
- [ ] **禁止**本阶段修改线格式、阈值和战斗语义。

**验收**

- [ ] EditMode：所有 Codec Golden Bytes 通过。  
- [ ] PlayMode：Host/Client 相同行为脚本最终 Pose/HP/Action 一致。  
- [ ] 人工：现有已验收功能全部通过。  

**出口：** 后续重构有可比较基线。→ **未达成**

### GF1 — ACTNet.Core 与稳定身份

**任务**

- [ ] 创建 `ACTNet.Core` 程序集。  
- [ ] 增加 `NetConnectionId`、`NetPlayerId`、`NetEntityId`、`NetArchetypeId`、`NetTick`。  
- [ ] 增加边界检查的 `NetBufferReader/Writer`。  
- [ ] 定义 `NetworkProtocolVersion` 与 `ContentFingerprint`。  
- [ ] 当前 `SimActorId` 通过 ACT Adapter 映射 `NetEntityId`；首版允许数值相同。  
- [ ] 当前 Codec 改为复用 Core Reader/Writer，但字节结果不得改变。  
- [ ] **删除**各 Codec 内重复的私有 Reader/Writer。  

**验收**

- [ ] `ACTNet.Core` 不引用 Unity 与 ACTGame。  
- [ ] Golden Bytes 与 GF0 完全一致。  
- [ ] 非法长度、负 count、超上限 payload 被拒绝。  
- [ ] Id 的 Invalid / Equality / Hash 测试通过。  

**出口：** 纯 C# 网络基础类型形成。→ **未达成**

### GF2 — Transport 与 Session 分离

**任务**

- [ ] 用 `INetTransport` 替代当前方向固化的 `IReplicationTransport`。  
- [ ] 引入 `NetConnectionId` 和定向 `Send`。  
- [ ] 创建 `ServerSession` / `ClientSession` / `ConnectionRegistry`。  
- [ ] Join、Accept、Reject、Heartbeat、Kick 从 RoomHost/Client 移入 Session。  
- [ ] `ReplicationRoomProtocol.MaxPlayers` 变为 Session 配置。  
- [ ] `CombatWorldController` 通过 Composition Root 注入 Session。  
- [ ] LoopbackTransport 支持至少两条模拟连接。  
- [ ] UDP 适配器保持当前行为；可靠通道可在 GF6 接入成熟网库。  
- [ ] **删除** RoomHost/Client 内 Endpoint 列表和握手 switch。  

**验收**

- [ ] Session 测试不引用 Character、Action、Combat。  
- [ ] FakeGame 可以 Join、Heartbeat、Kick，不创建任何 Unity 对象。  
- [ ] Host 可区分两条 Loopback connection。  
- [ ] 当前双人 UDP 入房行为不变。  

**出口：** 房间连接与 ACT Gameplay 解耦。→ **未达成**

### GF3 — Replication Runtime 与实体生命周期

**任务**

- [ ] 创建 `ReplicatedEntityRegistry`。  
- [ ] 定义 `ReplicationFrame` / `EntityRecord` / `SpawnRecord` / `DespawnRecord`。  
- [ ] 定义 `IReplicationSchema` 与 Schema Registry。  
- [ ] 首版注册一个 `CharacterSnapshotSchemaV1`，内部仍编码完整旧 Snapshot。  
- [ ] Host 通过 `ReplicationServer` 对每连接生成 Frame。  
- [ ] Client 通过 `ReplicationClient` 应用 Frame。  
- [ ] `Spawns/Despawns` 成为生命周期真源；Periodic full list 仅作诊断或恢复。  
- [ ] 加入 `NetArchetypeId`，禁止客机始终取 `_enemyConfigs[0]`。  
- [ ] 增加旧 Tick / 旧 Sequence 丢弃。  
- [ ] **删除** `ApplyRemoteActors` 里“本 Tick 未见即销毁”的生命周期主逻辑。  

**验收**

- [ ] FakeEntity 可 Spawn → Update → Despawn。  
- [ ] 丢一个普通 Snapshot 不会误 Despawn。  
- [ ] 乱序旧 Frame 不会覆盖新状态。  
- [ ] 两种敌人 Archetype 在 Client 创建正确 Proxy。  
- [ ] 当前 Player / Enemy 联机表现不变。  

**出口：** 复制层不再等同于 Character Actors 全量数组。→ **未达成**

### GF4 — ACT Authority / Owner / Observer Adapter

**任务**

- [ ] 创建 `ActGameSessionHandler`，负责玩家加入后生成 Authority Actor。  
- [ ] 创建 `ActAuthorityReplicationAdapter`，封装 InputFrame 灌入、Snapshot Capture、FrameHits。  
- [ ] 创建 `ActOwnerReplicationAdapter`，封装 self Snapshot 应用和 HP 权威覆盖。  
- [ ] 创建 `ActRemoteProxyFactory`，封装 Proxy、TargetSystem、配置和 View 生命周期。  
- [ ] `ActionReplicationCatalog` 迁入 `ActContentRegistry`。  
- [ ] `CharacterReplicationCapture` 迁入 `CharacterSnapshotSchema`。  
- [ ] Hit Cue、PredictedHitStop、CameraLock 接缝留在 ACT / App。  
- [ ] RoomHost/Client 缩成薄 Unity Facade，或由新的 `NetGameController` 取代。  
- [ ] **删除**通用 Session / Replication 对 CharacterConfig、PlayerController、EnemySpawnController 的引用。  

**验收**

- [ ] `ACTNet.*` 全局搜索无 `CharacterActor|ActionDefinition|CharacterConfig|CombatHitPipeline|RemoteCharacterProxy`。  
- [ ] Authority / Owner / Observer 映射测试通过。  
- [ ] Observer 永不创建完整 CharacterActor。  
- [ ] Autonomous 永不注册权威 Hitbox Consumer。  
- [ ] 人工：移动、出招、连招、受击、死亡、CameraLock 全回归。  

**出口：** 通用层与 ACT 业务层完成结构性分离。→ **未达成**

### GF5 — Prediction Runtime 分离

**任务**

- [ ] 提取 `CommandHistory`、`PredictedStateHistory`、ACK 和 Replay 协调。  
- [ ] 创建 `ActCharacterPredictionModel`。  
- [ ] `PredictedLocomotionDriver` 只保留 CharacterMotor / Gate / Error 策略。  
- [ ] `PredictedActionAckQueue` 保留 ACT 变体、连招和 Cancel 语义。  
- [ ] Remote Entity 使用通用 `SnapshotTimeline`，ACT Proxy 只做状态到表现的映射。  
- [ ] 增加 prediction error / snap / replay 指标。  
- [ ] **禁止**把 ActionId、Hit/Death 判断写入通用 PredictionCoordinator。  

**验收**

- [ ] Fake linear entity 可预测、注入分歧、Restore + Replay。  
- [ ] ACT 走跑 2m Gate 与当前行为一致。  
- [ ] Action / Hit / Death 不错误 Replay 走跑。  
- [ ] 连招超前不会被错误取消。  
- [ ] 人工注入 RTT / jitter 后本机仍即时响应。  

**出口：** 预测算法骨架可复用，ACT 决策仍归业务层。→ **未达成**

### GF6 — 通道可靠性与网络时间

**任务**

- [ ] Control 使用可靠有序通道。  
- [ ] Snapshot 使用不可靠时序通道；按 Sequence 丢旧。  
- [ ] Command 保留最近 K 条冗余，并增加服务器消费预算。  
- [ ] 瞬时 Hit / Vitality Event 使用可靠事件序列或最近 N 事件冗余，定案只留一种。  
- [ ] 增加 ServerTime / Tick offset 估计。  
- [ ] Remote SnapshotTimeline 使用 interpolation delay，不再只用本地 `InterpolationAlpha`。  
- [ ] 增加 MTU 门禁、最大 payload 和超限拆包策略。  
- [ ] 评估并定案 LiteNetLib 或 Unity Transport；若使用成熟库，不重复实现可靠 UDP。  

**验收**

- [ ] 100ms RTT、20ms jitter、5% 丢包下 Session 不误断。  
- [ ] AuthorityTick 乱序不会回滚 Proxy 到旧状态。  
- [ ] 丢普通 Snapshot 仍平滑；关键死亡 / Hit Cue 最终到达且只播一次。  
- [ ] 单包不超过配置 MTU；超限有明确分组或拒绝日志。  
- [ ] 传输层测试不引用 ACT。  

**出口：** 通用网络基础设施达到公网 Demo 基线。→ **未达成**

### GF7 — Delta、Relevancy 与发送预算

**任务**

- [ ] Snapshot 频率与 60Hz Simulation 解耦。  
- [ ] 每连接维护 baseline ACK。  
- [ ] Character Schema 增加字段 change mask。  
- [ ] `GraphNodeId` 替换为稳定整数。  
- [ ] 增加 VisibilityPolicy：Always / Owner / Distance / Scene。  
- [ ] 增加 UpdatePriorityPolicy 与 per-connection byte budget。  
- [ ] 关键 Owner 状态和低优先级敌人允许不同更新率。  
- [ ] 增加 full snapshot 恢复路径。  

**验收**

- [ ] 10+ Actor 时平均下行显著低于 GF0 全量 60Hz 基线。  
- [ ] 不相关实体不发送给该连接。  
- [ ] baseline 丢失后可请求/等待 full state，不永久损坏。  
- [ ] Owner 的关键状态不会被低优先级 Actor 饿死。  

**出口：** 复制运行时具备可扩展性，不再是 O(Actor×60Hz) 全量广播。→ **未达成**

### GF8 — 清理、包化与第二用例验证

**任务**

- [ ] 删除旧 `IReplicationTransport`、旧 RoomCodec 混合入口和旧 Actors diff 生命周期。  
- [ ] 删除 RoomHost/Client 中已迁出的 Gameplay 逻辑。  
- [ ] 清理重复 Role / Id / Protocol 常量。  
- [ ] 建最小 FakeActionGame 示例或测试 Fixture：一个可移动 Entity + Owner 预测 + Observer 插值。  
- [ ] 输出程序集依赖图、接入说明、协议说明和调试指南。  
- [ ] 提供“新 ACT 项目接入清单”。  
- [ ] 决定是否做 Unity Package；未稳定前只保持项目内 Framework 目录。  

**验收**

- [ ] FakeActionGame 不引用 `ACTGame.Character` 即可运行 Loopback 测试。  
- [ ] 当前游戏只通过 `ACTGame.Networking` Adapter 接框架。  
- [ ] 不存在新旧双轨 Controller。  
- [ ] 所有 EditMode / PlayMode / 人工验收通过。  

**出口：** R2 级可复用框架形成。→ **未达成**

---

## 10. 依赖顺序与冻结点

```text
GF0 行为冻结
  ↓
GF1 Core / IDs / Buffer
  ↓
GF2 Transport + Session
  ↓
GF3 Replication Runtime
  ↓
GF4 ACT Adapter 分离               ★ 结构分离主交付
  ↓
GF5 Prediction Runtime
  ↓
GF6 Reliability / Network Time     ★ 公网 Demo 基线
  ↓
GF7 Delta / Relevancy / Budget
  ↓
GF8 Cleanup / Second Use Case      ★ R2 可复用框架
```

可并行：

- GF0 的 Golden Bytes、人工脚本、Metrics 基线；
- GF1 的 Core Id 与 Reader/Writer；
- GF4 的 Content Manifest 设计；
- 文档和程序集依赖检查。

禁止并行：

- GF3 生命周期切换与 GF7 Delta；
- GF4 Adapter 搬迁与 Action/Locomotion 玩法重构；
- GF5 Prediction 提取与纠偏阈值重新调参；
- GF6 更换网库与 GF2 Session 首次拆分。

### 推荐冻结点

| 冻结点 | 内容 | 可对外宣称 |
|--------|------|------------|
| GF4 | Core/Session/Replication 与 ACT Adapter 分开 | 架构分层完成 |
| GF6 | 通道、时序、插值时间线、丢包基线 | 可复用网络内核 Demo |
| GF8 | 第二用例、清理、文档 | 可复用 ACT 网络框架 |

---

## 11. 保留 / 移动 / 拆分 / 删除

### 11.1 保留

| 内容 | 原因 |
|------|------|
| `SimulationWorld` 60Hz 权威 | 产品同步模型核心 |
| `CharacterActor` Authority / Autonomous 复用 | Owner 预测正确基础 |
| `CombatHitPipeline` Host 独占 | 战斗权威边界 |
| `InputFrame` 量化与 Merge/CarryForward | ACT 命令契约 |
| `RemoteCharacterProxy` 模式 | Observer 不需要完整 Actor |
| 手写二进制 | 可控、可测；只需分层 |
| 单机即 Listen Host | 避免 Offline / Online 双核 |

### 11.2 移动

| 当前 | 目标 |
|------|------|
| Room Host/Client 的握手 | `ACTNet.Session` |
| UDP Endpoint / Client list | `ACTNet.Transport` |
| Actor 列表与生命周期 | `ACTNet.Replication` |
| Pending / ACK 协调 | `ACTNet.Prediction` |
| Action Catalog | `ACTGame.Networking.Content` |
| Character Capture | `ACTGame.Networking.Schema` |
| Proxy Factory | `ACTGame.App.Networking.Presentation` |

### 11.3 拆分

| 当前类型 | 拆分后 |
|----------|--------|
| `ReplicationRoomHost` | ServerSession + ReplicationServer + ActAuthorityAdapter + Unity Facade |
| `ReplicationRoomClient` | ClientSession + ReplicationClient + PredictionCoordinator + ActOwnerAdapter + Proxy Presenter |
| `RoomCodec` | SessionCodec + Command Envelope + Replication Envelope |
| `ReplicationCodec` | Core Buffer + CharacterCommandCodec + CharacterSnapshotCodec + CombatEventCodec |
| `ActorReplicationSnapshot` | 首先作为一个 Schema；后续 Pose/Action/Vitality/Target fragments |
| `ReplicationRoomProtocol` | TransportConfig + SessionConfig + GameplayReplicationConfig |

### 11.4 删除

| 删除 | 时机 | 原因 |
|------|------|------|
| `SendAuthorityToClients` 广播式端口 | GF2 | 无法支持每连接状态 |
| Client “Actors 本帧未见即销毁” | GF3 | 丢包会误判生命周期 |
| Client 敌人配置固定取第一个 | GF3/GF4 | 缺 ArchetypeId |
| Room 内直接 Find `EnemySpawnController` | GF4 | Session 反向依赖场景 |
| Room 内直接播 Hit Cue / HitStop | GF4 | 网络编排混入表现 |
| Codec 内重复 Reader/Writer | GF1 | 基础设施重复 |
| 高频 `GraphNodeId` string | GF7 | 带宽与内容稳定性 |
| 旧新双 Room 主路径 | GF8 | 长期兼容债 |

---

## 12. 自动测试

### 12.1 Core

- [ ] ID Invalid / Equality / Ordering。  
- [ ] Reader/Writer 全类型 round-trip。  
- [ ] 截断、恶意 count、超 payload。  
- [ ] ProtocolVersion / ContentFingerprint。  

### 12.2 Transport

- [ ] 两连接定向发送不串包。  
- [ ] Channel 语义。  
- [ ] Sequence wrap / old packet drop。  
- [ ] RTT、jitter、loss 注入。  
- [ ] Disconnect reason。  

### 12.3 Session

- [ ] Join Accept / Reject。  
- [ ] Room full。  
- [ ] Version / content mismatch。  
- [ ] Heartbeat timeout。  
- [ ] 两连接 PlayerId 稳定。  

### 12.4 Replication

- [ ] Spawn / Update / Despawn。  
- [ ] 丢 Update 不误 Despawn。  
- [ ] 旧 Frame 不覆盖新 Frame。  
- [ ] 未知 Schema / Archetype 明确失败。  
- [ ] Visibility 与 budget。  
- [ ] baseline full / delta 恢复。  

### 12.5 Prediction

- [ ] 一致状态只 ACK。  
- [ ] 分歧 Restore + Replay。  
- [ ] 超窗硬对齐。  
- [ ] Pending 上限。  
- [ ] SnapshotTimeline 插值与外推上限。  

### 12.6 ACT Adapter

- [ ] InputFrame batch / Merge / CarryForward。  
- [ ] CharacterSnapshot 完整往返。  
- [ ] ActionId / ArchetypeId 内容映射。  
- [ ] Authority Actor 有 Hitbox Consumer。  
- [ ] Autonomous Actor 无 Hitbox Consumer。  
- [ ] Observer 不创建 CharacterActor。  
- [ ] Hit Event 去重与可靠送达。  
- [ ] Action / Hit / Death Reconcile Policy。  

### 12.7 架构守卫

- [ ] `ACTNet.*` 不引用 `UnityEngine`。  
- [ ] `ACTNet.*` 不引用 `ACTGame.*`。  
- [ ] `ACTGame.Simulation` 不引用 `ACTNet`。  
- [ ] Transport 无 `Character|Action|Combat`。  
- [ ] Session 无 `CharacterConfig|PlayerController|EnemySpawnController`。  
- [ ] App Controller 不实现二进制字段读写。  

---

## 13. 人工验收

Agent / 单测不能替代以下项。

### 13.1 结构分离验收（GF4）

| ID | 操作 | 通过标准 |
|----|------|----------|
| H-GF4-1 | 打开 asmdef 依赖图 | `ACTNet.*` 无 Unity / ACT 反向引用 |
| H-GF4-2 | 单人 Play | 与重构前单机一致 |
| H-GF4-3 | ParrelSync 双进程 Join | 正常入房、ActorId/PlayerId 正确 |
| H-GF4-4 | 双方移动/折返/急停 | Owner 即时响应，Observer 正常显示 |
| H-GF4-5 | 连招/闪避/受击/死亡 | Host 权威，双方最终一致 |
| H-GF4-6 | 客机 CameraLock | 可锁定 Proxy，不产生客户端命中权威 |
| H-GF4-7 | 客机断开 | Host 正确 Despawn Guest；Session 回 Listening |

### 13.2 公网基线验收（GF6）

| ID | 操作 | 通过标准 |
|----|------|----------|
| H-GF6-1 | 80～120ms RTT | 本机输入不等待 RTT |
| H-GF6-2 | 5% 丢包 | 不误销毁 Proxy；最终 HP 一致 |
| H-GF6-3 | 20ms jitter | Remote 无明显逐包硬切 |
| H-GF6-4 | 人工乱序 Tick | 旧状态被丢弃 |
| H-GF6-5 | 连续命中/死亡 | Hit Cue 不重复，死亡边沿不丢 |
| H-GF6-6 | 查看网络 HUD | RTT/jitter/bytes/pending/snap 可见 |

### 13.3 可复用验收（GF8）

| ID | 操作 | 通过标准 |
|----|------|----------|
| H-GF8-1 | 运行 FakeActionGame Loopback | 不引用本项目 Character 即可 Owner 预测 + Observer |
| H-GF8-2 | 增加第二个 Archetype | 只注册 Schema/Factory，不改 Session/Transport |
| H-GF8-3 | 替换 Loopback 为 UDP | Gameplay Adapter 不改 |
| H-GF8-4 | Headless Host 启动 | 不依赖本地 PlayerController 也能创建 Session |

---

## 14. 性能与协议预算

### 14.1 预算必须可配置

```text
SimulationHz = 60
SnapshotHz = 20～30（首个优化目标，最终按体验实测）
MaxPacketBytes ≤ Transport MTU Budget
MaxEntitiesPerFrame
MaxReliableEventBacklog
MaxPendingCommands
InterpolationDelayMs
```

### 14.2 首轮基线

当前粗略估算：

```text
ActorSnapshot ≈ 67B + GraphNodeId UTF-8
10 Actor × 60Hz ≈ 45KB/s / Client（未含头和事件）
```

GF0 必须实测，不以估算作为最终指标。

### 14.3 优化顺序

```text
先正确分层
  → 再分离 SnapshotHz
  → 再去字符串
  → 再 change mask
  → 再 baseline/delta
  → 最后 relevancy/budget
```

禁止在没有指标前先做复杂 bit packing。

---

## 15. 风险与对策

| 风险 | 对策 |
|------|------|
| 过度抽象，当前项目反而难懂 | 每个抽象必须被当前 ACT + FakeEntity 两处使用；否则留业务层 |
| 分层和协议同时改，回归难定位 | GF1～GF5 优先保持 Golden Bytes；GF6 才升级通道 |
| 泛型 / interface 造成 GC | 热路径用 struct、预分配 Buffer；Profiler 证明后优化 |
| `payloadBytes` 产生大量数组 | Schema Writer 直写共享 Buffer；首轮可接受，GF7 前收口 |
| 新旧 Room 双轨 | 每阶段列删除项；GF8 前不得发布两个主入口 |
| Prediction 通用化吞掉 ACT 规则 | 通用层只做 history/ack/replay；Gate 和 Cancel 留 Adapter |
| Dedicated 被 Unity View 阻塞 | Session/Replication 不引用 Unity；Headless Fake Host 提前测试 |
| Content Id 碰撞或错配 | Manifest 构建期失败 + Fingerprint Join 门禁 |
| Spawn 丢包导致未知 Entity | Spawn 走可靠控制或重复到确认；Update 不隐式代替 Spawn |
| Snapshot 超 MTU | GF6 前增加 packet size assert；分组优先于 IP fragmentation |
| Reliable Event 堵塞 | 关键事件与大资源分通道；设置 backlog 上限 |
| Scope 膨胀成 Unreal | 坚守 §2.3；不做反射属性、通用 RPC、任意 GameObject |
| 没有第二项目，无法证明复用 | FakeActionGame 只验证框架接缝，不制作完整玩法 |

---

## 16. 代码审查门禁

每个 GF 阶段合并前检查：

1. 是否新增了 `ACTNet → ACTGame/Unity` 反向依赖？  
2. 是否把 Gameplay if/else 搬进通用层而非 Adapter？  
3. 是否同时改变了线格式和玩法语义？  
4. 是否新增第二条长期 Room / Prediction 主路径？  
5. 是否有对应 Characterization / Unit / PlayMode 测试？  
6. 是否能用 FakeEntity 解释该抽象为何通用？  
7. 是否引入任意 RPC 绕开显式 Command/Snapshot/Event？  
8. 是否把 per-connection 状态错误做成全局状态？  
9. 是否处理旧 Tick、未知 Schema、未知 Archetype、恶意长度？  
10. 是否更新人工验收与网络指标？

任一答案不明确，不进入下一阶段。

---

## 17. 推荐开工顺序

```text
第一批：GF0
  Golden Bytes
  调用顺序测试
  双进程回归脚本
  指标基线

第二批：GF1 → GF2
  Core / IDs / Buffer
  Transport / Session

第三批：GF3 → GF4
  Replication Entity Lifecycle
  ACT Adapter
  ★ 完成“通用层 / 业务层分离”

第四批：DS1 → DS6
  Dedicated Bootstrap / Headless Authority
  Match / Replication / Dedicated Build
  ★ 完成 LAN DS-Demo

第五批：GF5 → GF6
  Prediction Runtime
  Channels / Network Time / Jitter Buffer

第六批：GF7 → GF8
  Delta / Relevancy / Budget
  Cleanup / FakeActionGame / 接入文档
```

> GF4 关闭后已完成“网络层分离”，足以让 Dedicated 复用统一 Session / Replication Runtime；GF5～GF8 属于 DS-Demo 后的能力增强。总排期以 [`NETSYNC_FRAMEWORK_DEDICATED_MASTER_DEVELOPMENT_PLAN.md`](./NETSYNC_FRAMEWORK_DEDICATED_MASTER_DEVELOPMENT_PLAN.md) 为准。

最小正确切片不是“先建很多空接口”，而是：

```text
Loopback FakeGame
  → ServerSession 接受连接
  → Spawn 一个 FakeEntity
  → Client 收 Snapshot
  → Disconnect 后 Despawn
```

随后再让当前 Character 通过同一套 Session / Replication 接入。

---

## 18. 完成定义

满足以下全部条件，才可以称为“可复用 ACT 网络框架”：

- [ ] `ACTNet.*` 与 `ACTGame.*` 程序集单向依赖；  
- [ ] Session 不知道 Character，Replication 不知道 Action；  
- [ ] ACT 通过注册 Schema、Adapter、Factory 接入；  
- [ ] FakeEntity 能完成 Join、Spawn、Owner Prediction、Observer、Despawn；  
- [ ] UDP / Loopback 替换不改 Gameplay Adapter；  
- [ ] Listen Host / Dedicated 只差宿主装配；  
- [ ] 现有单人、双人、预测、命中功能无回归；  
- [ ] 乱序、丢包、jitter 有自动与人工验收；  
- [ ] 通用层有结构化 metrics；  
- [ ] 旧 Room 混合职责主路径已删除；  
- [ ] 接入文档能指导第二个固定 Tick ACT 项目完成最小联网。

---

## 19. 变更日志

| 日期 | 说明 |
|------|------|
| 2026-08-17 | 初版：定案 ACTNet Core / Transport / Session / Replication / Prediction 与 ACTGame.Networking / App 分层；定义 GF0～GF8 迁移、验收、删除和风险门禁 |
