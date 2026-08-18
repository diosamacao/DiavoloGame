# NetSync：Dedicated Server 分离与落地方案

> 制定：2026-08-17  
> 角色：**NetSync Dedicated Server 分离实施真源**（基于通用网络核心 / ACT 业务层分离方案）  
> 代码基线：`NetSync@3f695f93865a29f92a09fedfe60788a620900419`  
> 相关：  
> - 总开发排期：[`NETSYNC_FRAMEWORK_DEDICATED_MASTER_DEVELOPMENT_PLAN.md`](./NETSYNC_FRAMEWORK_DEDICATED_MASTER_DEVELOPMENT_PLAN.md)（GF4 后启动 Dedicated 主路径；DS6 关闭 DS-Demo）  
> - 通用层 / 业务层分离：[`NETSYNC_GENERIC_CORE_GAMEPLAY_SEPARATION_PLAN.md`](./NETSYNC_GENERIC_CORE_GAMEPLAY_SEPARATION_PLAN.md)  
> - 当前架构分析：[`NETSYNC_ARCHITECTURE_ANALYSIS_AND_FRAMEWORK_COMPARISON.md`](./NETSYNC_ARCHITECTURE_ANALYSIS_AND_FRAMEWORK_COMPARISON.md)  
> - 当前实现真源：[`../2026.8.19/NETSYNC_W5_STAGE_SUMMARY.md`](../2026.8.19/NETSYNC_W5_STAGE_SUMMARY.md)（W5）；[`../2026.8.18/NETSYNC_M1_STAGE_SUMMARY.md`](../2026.8.18/NETSYNC_M1_STAGE_SUMMARY.md)（M1）  
> 目标部署链：`DedicatedServerBootstrap → ServerSession → MatchCoordinator → AuthoritySimulation → ReplicationServer → Transport`  
> **约束：** Dedicated Server 无本地玩家、无 Input System、无 Camera、无动画/VFX/SFX 权威依赖；所有玩家均通过 Connection 加入，服务器只接受 Command / Request / ACK，不接受客户端状态覆盖
> **当前前置状态（2026-08-19）：** DS1/DS2（W5）代码已切：独立 `DedicatedServerRuntime`、N 玩家 Session、每连接 ACK。Editor Headless Play 待确认。权威 World 属 DS3/W6。Listen Host 仍保留为回归路径，直至 W9。

---

## 0. 一句话

把当前：

```text
Listen Host
  = 本地玩家
  + 权威 SimulationWorld
  + UDP 房间
  + 客机座位
  + 敌人 AI
  + 本地表现
```

拆成：

```text
Dedicated Server Process
  = ServerSession
  + Match
  + Authority SimulationWorld
  + N 个 Authority Player Actor
  + Enemy AI / CombatHitPipeline
  + ReplicationServer
  + 无表现的 Server Content

Client Process
  = ClientSession
  + 一个 Autonomous Actor
  + 其余 RemoteProxy
  + Camera / Input / Animation / VFX / HUD
```

Listen Server 最终不再是一套特殊游戏逻辑，而是：

```text
ServerRuntime + LocalClientRuntime + LoopbackConnection
```

Dedicated Server 则只是：

```text
ServerRuntime
```

---

## 1. 为什么当前 Listen Host 不能直接作为 Dedicated Server

### 1.1 当前 Host 假设“服务器本身就是玩家 1”

当前 `ReplicationRoomHost` 存在以下假设：

```text
GuestPlayerId = 2
Host 本地玩家固定存在
HostActor 必须先注册
客机出生点 = HostRoot.position + 2m
客机 CharacterConfig = Host 玩家 CharacterConfig
JoinAccept 携带 HostActorId
Capture 顺序 = Host local + Guest + Enemies
```

Dedicated Server 没有本地玩家，因此：

- 没有 `GetLocalPlayerQuery()` 返回值；
- 没有 Host Root 可计算出生点；
- 没有 Host CharacterConfig 可复制给 Guest；
- 第一名连接玩家不能被称为 Guest；
- PlayerId 不能从固定 2 开始；
- 房间不能依赖 HostActor 创建完成后才 Accept。

### 1.2 当前场景入口混入客户端表现

`CombatWorldController` 当前同时确保：

- `SimulationHost`；
- `HitStopController`；
- Host / Client Room Component；
- EditorPrefs / ParrelSync 角色覆盖；
- 静态碰撞；
- Room HUD。

Dedicated Server 不需要：

- HitStop；
- HUD；
- ParrelSync clone 判断；
- Camera；
- 本机 PlayerController；
- Presentation LateUpdate。

### 1.3 当前 Authority Actor 仍有表现装配

当前 `CharacterActorFactory` 服务于可见角色，往往会同时装配：

- Motor；
- Action；
- Numeric；
- Animation；
- PresentationBridge；
- Notify Consumer；
- Model / Transform。

服务器真正需要：

```text
Input / Locomotion / MotorSim / ActionSim / Numeric
Targeting / Hitbox / Hurtbox / CombatReaction
```

服务器不需要：

```text
PlayableGraph / Animator / ModelPrefab
Camera / Lean / VFX / SFX / HitStop Presentation
```

若不拆分，Dedicated Server 即使使用 `-nographics`，仍会：

- 加载不必要资源；
- 创建无意义表现对象；
- 被 Unity Dedicated Server 的资产裁剪暴露空引用；
- 无法证明战斗权威与表现无关。

### 1.4 当前 Transport / Room 只支持 Host + 一名客机

当前：

- `MaxPlayers = 2`；
- 广播接口没有 ConnectionId；
- 只有一个 `_guest`；
- 输入 ACK 只有一个 `appliedHint`；
- 快照是同一份广播；
- 生命周期围绕单个 Endpoint。

Dedicated Server 至少需要：

```text
ConnectionId → PlayerId → EntityId → CommandQueue → Ack
```

且这些状态必须 per-connection。

### 1.5 当前 Authority Tick 与 Unity 渲染循环绑定

当前权威 Tick 由 MonoBehaviour `Update` 驱动，随后：

- `AfterLogicStep` 打包；
- `LateUpdate` 做表现插值。

Dedicated Server 需要：

- 单调时间源；
- Tick overrun 指标；
- catch-up 上限；
- 无 LateUpdate；
- 无渲染 alpha；
- 可在 `-batchmode` / Server Build 下稳定运行；
- 可检测“服务器已无法维持 60Hz”。

---

## 2. 目标、范围与冻结点

### 2.1 DS-Demo

目标：

- Unity Dedicated Server Build；
- Windows 或 Linux Server；
- 一场比赛一个进程；
- 2～4 个远端客户端；
- 60Hz Authority Simulation；
- 客户端输入上行、状态下行；
- AI 和命中只在服务器；
- 无本地服务器玩家；
- LAN / 直连；
- 基础日志、健康状态和优雅退出。

不包含：

- Matchmaking；
- 云厂商 SDK；
- Account 数据库；
- 跨区迁移；
- Host Migration；
- 战中晚加入；
- 完整断线重连；
- PVP 历史回溯；
- 多场比赛同进程。

### 2.2 DS-Full

在 DS-Demo 基础上增加：

- 认证 Adapter；
- reconnect grace；
- 准备阶段晚加入；
- 内容 Manifest；
- 完整网络通道与公网丢包基线；
- 结构化指标；
- readiness / liveness；
- SIGTERM 优雅停服；
- 容器化；
- CI Server Build 与自动烟测；
- 负载预算；
- 可选 Relay / Allocation 接缝。

### 2.3 不做

- 不让服务器接收 `CharacterStateUpload` 覆盖权威；
- 不把 Dedicated Server 实现成隐藏窗口的普通 Client Build；
- 不在服务器创建 Camera / AudioListener / UI；
- 不把 `#if UNITY_SERVER` 散落进 Action、Locomotion 和 Combat State；
- 不让每个 Gameplay 类自己判断“是否服务器”；
- 不把一场进程扩成多 Room 调度器；
- 不在 DS 分离期间重写战斗规则；
- 不长期保留“Host 本地玩家特殊权威路径”；
- 不将 Matchmaking 和游戏服务器进程混成一个组件。

### 2.4 交付冻结点

| 冻结点 | 条件 | 可宣称 |
|--------|------|--------|
| DS3 | 无本地玩家的 Authority World 可运行 | Headless Authority 核成立 |
| DS6 | Dedicated Build + 2 客户端完整对局 | DS-Demo 完成 |
| DS8 | 认证、重连、指标、部署、压测闭环 | DS-Full 完成 |

---

## 3. 与通用层分离方案的依赖

Dedicated Server 不应在旧 RoomHost 上继续叠加条件分支，应建立在 GF 分层之上。

| GF 阶段 | DS 依赖 | 原因 |
|---------|---------|------|
| GF0 行为冻结 | 必须 | 证明 DS 分离没有改战斗语义 |
| GF1 Core / Id | 必须 | Connection / Player / Entity 稳定身份 |
| GF2 Transport + Session | 必须 | DS 没有 Host 本地玩家，必须是独立 ServerSession |
| GF3 Replication Runtime | 必须 | N 玩家、Spawn/Despawn、per-connection Frame |
| GF4 ACT Adapter | 必须 | ServerRuntime 不能直接依赖客户端表现 |
| GF5 Prediction Runtime | 客户端需要 | DS 自身不预测，但客户端对 DS 需要 |
| GF6 Reliability / Network Time | DS-Demo LAN 可后置；公网必须 | 通道、旧包、jitter buffer |
| GF7 Delta / Relevancy | 可后置 | 小规模 Demo 不阻塞 |
| GF8 包化 | 不阻塞 | DS 可反向成为第二宿主验证 |

推荐交错顺序：

```text
GF0 → GF1 → GF2
                  ├─ DS0 / DS1
GF3 → GF4        ├─ DS2 / DS3 / DS4
GF5 → GF6        ├─ DS5 / DS6
GF7              └─ DS7 / DS8
```

禁止：

```text
旧 ReplicationRoomHost
  + if DedicatedServer
  + if no LocalPlayer
  + if playerCount > 2
  + if no Presentation
```

这种做法会把 RoomHost 继续膨胀成三种宿主的混合控制器。

---

## 4. 目标进程模型

### 4.1 Dedicated Server

```text
DedicatedServerBootstrap
  → ServerLaunchConfig
  → INetTransport.StartServer
  → ServerSession
  → MatchCoordinator
  → ServerSimulationRunner
  → SimulationWorld
  → ActAuthorityReplicationAdapter
  → ReplicationServer
  → ServerHealth / Metrics / Logs
```

进程内不存在：

```text
ClientSession
Local InputSampler
Autonomous CharacterActor
RemoteCharacterProxy
Camera / HUD
Presentation Render
```

### 4.2 Remote Client

```text
ClientBootstrap
  → ClientSession
  → ReplicationClient
  → ActOwnerPredictionAdapter
  → Autonomous CharacterActor
  → RemoteProxy Registry
  → Camera / Input / Presentation / HUD
```

### 4.3 Listen Server

目标不是恢复当前特殊 Host，而是组合：

```text
ListenServerBootstrap
  ├─ ServerRuntime
  │    Authority World
  │    ReplicationServer
  │
  └─ LocalClientRuntime
       ClientSession
       Autonomous Actor
       Presentation

LocalClientRuntime ←→ LoopbackConnection ←→ ServerRuntime
```

Listen Host 本机角色会有两份实例：

- ServerRuntime 内的 Authority Actor；
- LocalClientRuntime 内的 Autonomous / Presentation Actor。

但只有一份权威：

```text
Authority SimulationWorld
```

这不是双权威，也不是两个世界同时裁决战斗；Local Client 只是零网络延迟的普通客户端。

### 4.4 为什么 Listen Server 也应走 Local Client

优点：

- 删除“服务器本机玩家”概念；
- Listen 与 Dedicated 使用同一个 ServerRuntime；
- 房主角色也走 Command / Snapshot / ACK；
- 相机和表现永远属于 ClientRuntime；
- Dedicated Server 不再需要适配 Host 特例；
- 可测试 Host 优势以外的协议一致性。

代价：

- 同进程多一份本机角色表示；
- 需要 Loopback 连接；
- 当前 Host 零预测路径会改变。

迁移时允许暂时保留旧 Listen Host 作为回归对照，但 DS6 前必须切到组合模型或明确只把 Dedicated 作为主服务器产品路径。推荐最终删除特殊 Host 逻辑。

---

## 5. 目标分层

### 5.1 服务器运行时层级

```text
┌─────────────────────────────────────────────────────────────┐
│ Server Host / Operations                                    │
│ CLI / Environment / Health / Metrics / Shutdown / ExitCode  │
├─────────────────────────────────────────────────────────────┤
│ Match                                                       │
│ Lobby / Ready / Spawn / Playing / End / Cleanup             │
├─────────────────────────────────────────────────────────────┤
│ ACT Authority Adapter                                       │
│ Player Command → InputFrame / Actor Capture / Combat Event  │
├─────────────────────────────────────────────────────────────┤
│ Authority Simulation                                        │
│ SimulationWorld / CharacterActor / AI / CombatHitPipeline   │
├─────────────────────────────────────────────────────────────┤
│ Replication Runtime                                         │
│ Entity Registry / Frame / Spawn / Despawn / ACK / Relevancy │
├─────────────────────────────────────────────────────────────┤
│ Session                                                     │
│ Connection / Player / Handshake / Timeout                   │
├─────────────────────────────────────────────────────────────┤
│ Transport                                                   │
│ UDP / LiteNetLib / Unity Transport / Loopback               │
└─────────────────────────────────────────────────────────────┘
```

### 5.2 服务器层职责

| 层 | 负责 | 禁止 |
|----|------|------|
| Bootstrap | 读取配置、装配、启动和退出 | 创建玩家业务对象 |
| ServerSession | 连接、认证、PlayerId、心跳 | CharacterConfig、Spawn Position |
| MatchCoordinator | 房间状态、队伍、出生、结束规则 | Socket、二进制字段 |
| SimulationRunner | 60Hz Tick、overrun、catch-up | Camera、Render |
| Authority Adapter | Command 灌入、Actor Capture、Combat Event | 连接认证 |
| ReplicationServer | per-connection Snapshot / Spawn / ACK | Action 规则 |
| ServerContent | Gameplay Manifest / Collision / AI 数据 | Model、Texture、Audio |
| Operations | Health、metrics、shutdown | 修改战斗状态 |

### 5.3 依赖方向

```text
ServerHost
  → ACTNet.Session / Replication
  → ACTGame.Networking
  → ACTGame.Simulation

ServerHost ──X──► ACTGame.Client
ServerHost ──X──► Cinemachine / InputSystem UI
Simulation ──X──► ServerHost
```

---

## 6. 程序集与目录规划

### 6.1 目标目录

```text
Assets/Scripts/Framework/ACTNet/
  Core/
  Transport/
  Session/
  Replication/
  Prediction/

Assets/Scripts/Domain/
  Simulation/
  Networking/
    Shared/
    Authority/
    Client/

Assets/Scripts/App/
  Client/
    ClientBootstrap
    Presentation/
    Input/
    HUD/
  Server/
    DedicatedServerBootstrap
    ServerLaunchConfig
    ServerSimulationRunner
    MatchCoordinator
    ServerHealthReporter
    ServerShutdownCoordinator
  Listen/
    ListenServerBootstrap
```

### 6.2 目标程序集

```text
ACTNet.Core
ACTNet.Transport
ACTNet.Session
ACTNet.Replication
ACTNet.Prediction

ACTGame.Simulation
ACTGame.Networking.Shared
ACTGame.Networking.Authority
ACTGame.Networking.Client

ACTGame.App.Server
ACTGame.App.Client
ACTGame.App.Listen
```

### 6.3 引用矩阵

| 程序集 | 允许引用 |
|--------|----------|
| `ACTGame.Networking.Shared` | ACTNet、Simulation |
| `ACTGame.Networking.Authority` | Shared、Simulation、Combat |
| `ACTGame.Networking.Client` | Shared、Prediction、Presentation 接口 |
| `ACTGame.App.Server` | Authority、Session、Replication、Unity Server Runtime |
| `ACTGame.App.Client` | Client、Input、Camera、Presentation |
| `ACTGame.App.Listen` | Server + Client + Loopback |

服务器程序集禁止引用：

```text
Cinemachine
Unity Input System
Client HUD
Camera Controller
RemoteCharacterProxy Presentation 实现
VFX / SFX / HitStopController
```

### 6.4 `UNITY_SERVER` 使用政策

允许：

```text
Bootstrap 选择
Build 信息
Server-only 日志 / Crash handler
资源与组件裁剪接缝
```

禁止：

```text
ActionState 内 #if UNITY_SERVER
Locomotion 内 #if UNITY_SERVER
Combat Pipeline 内 #if UNITY_SERVER
每个 Character 方法内 #if UNITY_SERVER
```

架构边界应由程序集和 Adapter 保证，`UNITY_SERVER` 只处理构建宿主差异。

---

## 7. Server Session 与玩家模型

### 7.1 身份链

```text
NetConnectionId
  → NetPlayerId
  → PlayerSlot
  → NetEntityId
  → SimActorId
```

含义：

- ConnectionId：传输连接；
- PlayerId：会话玩家；
- PlayerSlot：比赛槽位 / 队伍；
- EntityId：复制实体；
- SimActorId：模拟世界稳定身份。

不能假设这些 Id 永远相等，但首版 Adapter 可使用稳定映射。

### 7.2 Join 流程

```text
Client Connect
  → JoinRequest
      protocolVersion
      contentFingerprint
      authTicket（Demo 可空）
      requestedCharacterArchetype
      reconnectToken（Demo 可空）
  → ServerSession 校验连接
  → MatchCoordinator 校验槽位 / 队伍 / 角色
  → 分配 PlayerId
  → JoinAccept
      playerId
      sessionEpoch
      serverTick
      matchState
  → Match 在正确阶段 Spawn Authority Actor
  → ReplicationServer 可靠下发 SpawnRecord
```

JoinAccept 不再依赖 `HostActorId`。

### 7.3 多玩家命令队列

每名玩家拥有：

```text
PlayerCommandStream
  lastReceivedCommandSequence
  lastAppliedClientTick
  pendingCommands
  carryForwardState
  rateLimit
  invalidCommandCount
```

服务器每 Tick：

```text
for PlayerId stable order:
  Resolve command for authority tick
  Validate
  Hold if absent
  Write InputFrameBuffer

SimulationWorld.Step
```

ACK 必须 per-player 进入该连接的 ReplicationFrame。

### 7.4 Disconnect 策略

DS-Demo 定案：

```text
连接断开
  → Match 标记离开
  → Authority Actor Despawn
  → 可靠广播 Despawn
  → 若剩余玩家为 0，结束 Match
```

DS-Full：

```text
连接断开
  → ReconnectGrace
  → Actor Hold / AI 接管（二选一定案）
  → token 重连成功则恢复 Connection 映射
  → 超时 Despawn
```

---

## 8. Match 生命周期

### 8.1 状态机

```text
Booting
  → LoadingContent
  → Listening
  → Lobby
  → Starting
  → Playing
  → Ending
  → Draining
  → Shutdown
```

| 状态 | 行为 |
|------|------|
| Booting | 解析配置、初始化日志 |
| LoadingContent | 验证 Manifest、地图和碰撞烘焙 |
| Listening | 绑定端口、Health=Ready |
| Lobby | 接受玩家、选择角色、Ready |
| Starting | 冻结名单、Spawn、确定 startTick |
| Playing | 60Hz Authority Tick |
| Ending | 停止接收 Gameplay Command、广播结果 |
| Draining | 发送结束、等待可靠队列或超时 |
| Shutdown | Dispose、写最终指标、退出码 |

### 8.2 一场一进程

首版定案：

```text
一个 OS Process = 一个 Match = 一个 SimulationWorld = 一个 ServerSession
```

优点：

- 崩溃隔离；
- 内存释放简单；
- 端口和 MatchId 清晰；
- 不需要 World 多租户；
- 避免静态单例污染多房间。

多 Match 单进程不进本方案。

### 8.3 Match 与 Session 分离

Session 关心：

- 谁连接；
- 是否超时；
- 是否通过认证。

Match 关心：

- 谁能入本局；
- 选什么角色；
- 何时 Spawn；
- 队伍；
- 胜负；
- 是否允许重连。

禁止 Session 直接调用 `CharacterActorFactory.Create`。

---

## 9. Authority Simulation 分离

### 9.1 ServerSimulationRunner

职责：

```text
MonotonicClock
  → FixedStepAccumulator
  → Process Commands
  → SimulationWorld.Step
  → Combat Resolve
  → Lifecycle Commit
  → Capture Replication
  → Metrics
```

与当前 `SimulationHost` 的差异：

| 当前 | Dedicated Server |
|------|------------------|
| `Time.deltaTime` | 单调未缩放时间 |
| `LateUpdate Render(alpha)` | 不存在 |
| Local Input Sample | 不存在 |
| `AfterLogicStep` 给 Room | 直接调用 Authority Adapter / Replication |
| 欠账最多追 8 帧 | 可配置，并有 overrun 健康门禁 |
| Unity Scene View | 只有 Authority World |

### 9.2 Tick 过载策略

禁止：

- 静默跳过逻辑 Tick；
- 用大 dt 一步补完；
- 因渲染帧慢改变玩法 dt。

建议：

```text
Accumulator 欠账
  → 每 PlayerLoop 最多追 MaxCatchUpTicks
  → 连续 Overrun 记录指标
  → 超过 UnhealthyThreshold 标记 NotReady
  → 超过 FatalThreshold 优雅结束或退出，让编排器重启
```

### 9.3 Authority Actor

服务器所有玩家均为：

```text
ReplicationSeat.Authority
```

无论玩家来自：

- Connection 1；
- Connection 2；
- Listen local loopback；
- 未来 Bot。

区别只在输入生产者：

```text
RemotePlayerInputProducer
LocalLoopbackInputProducer
AiInputProducer
```

### 9.4 服务器不预测

Dedicated Server：

- 不使用 `PredictedLocomotionDriver`；
- 不使用 `PredictedActionAckQueue`；
- 不使用 `PredictedHitStopConsumer`；
- 不创建 RemoteProxy；
- 不对玩家输入做画面平滑。

服务器只：

```text
应用合法 Command
  → 权威 Step
  → 生成状态与 ACK
```

---

## 10. Headless Character 与表现剥离

### 10.1 两阶段策略

#### DS-Demo：Null Presentation

保留现有 CharacterActor 主体，但工厂增加 Authority Headless 装配：

```text
CharacterActor
  + MotorSim
  + ActionSim
  + Numeric
  + Targeting
  + Hitbox/Hurtbox
  + NullAnimationService
  + NullPresentationBridge
  - ModelPrefab
  - PlayableGraph
  - VFX/SFX
```

要求 Null 实现不能影响 ActionFrame、RootMotion 烘焙位移和 Notify 玩法语义。

#### DS-Full：Simulation / Presentation 明确拆开

目标：

```text
CharacterSimulation
  Motor / Locomotion / Action / Numeric / Combat

CharacterPresentation
  Animation / Model / VFX / SFX / Camera

CharacterActor
  作为客户端组合 Facade；服务器只持有 CharacterSimulation
```

不要求一次性把所有 Character 代码纯化，但服务器构建必须能在无模型、无 Animator 情况下运行完整战斗。

### 10.2 Notify 分类

| Notify | Server |
|--------|--------|
| Movement / RootMotion Bake | 必须 |
| Hitbox | 必须 |
| Resource Cost / Gameplay Event | 必须 |
| VFX | 禁止执行 |
| SFX | 禁止执行 |
| CameraShake | 禁止执行 |
| HitStop Presentation | 禁止执行 |

不能简单删除整条 Timeline；必须按 Gameplay / Presentation 分类。

### 10.3 Transform 政策

理想状态：

- Authority Position 真源是 `CharacterMotorSim`；
- Hurtbox / Hitbox 使用逻辑 Pose；
- Unity Transform 只作可选调试锚点；
- Server 不从 Scene Transform 读回权威位置。

DS-Demo 若仍需要无模型 GameObject 作为生命周期容器，允许存在，但不得成为状态真源。

---

## 11. Server Content 分离

### 11.1 当前内容问题

`CharacterConfig` 同时可能包含：

- Motor / Combat / Numeric；
- ModelPrefab；
- Animation；
- VFX / SFX；
- Presentation 配置。

Dedicated Server Build 会裁剪不需要的图形资源，但不能依赖自动裁剪猜测哪些 Gameplay 数据仍被需要。

### 11.2 Server Gameplay Manifest

```text
ServerContentManifest
  contentFingerprint
  mapId
  staticCollisionBakeId
  characterArchetypes[]
    motorProfile
    combatProfile
    numericProfile
    hurtbox
    actionGameplayData
    aiProfile
  actionIds[]
  graphNodeIds[]
```

不包含：

```text
ModelPrefab
Material / Texture / Shader
AnimationClip（若 Gameplay 已烘焙）
AudioClip
VFX Prefab
Camera Profile
```

### 11.3 动作内容烘焙

若 Action 逻辑仍直接读取 `AnimationClip` 或表现资产，需构建：

```text
ActionGameplayBake
  durationFrames
  segments
  movementCurve / baked delta
  hitboxFrames
  cancelWindows
  resourceCosts
  gameplayNotifies
```

客户端可以额外加载：

```text
ActionPresentationData
  clips
  crossFade
  vfx
  sfx
  camera
```

服务器和客户端必须共享 Gameplay Bake Fingerprint。

### 11.4 Archetype

Join / Spawn 只传：

```text
NetArchetypeId
```

服务器根据 Server Manifest 创建 Authority Simulation；
客户端根据 Client Manifest 创建 Autonomous Actor 或 RemoteProxy。

未知 Archetype：

- 服务器拒绝 Join/Spawn；
- 客户端断开并报告 ContentMismatch；
- 禁止回退第一个敌人配置。

---

## 12. ReplicationServer

### 12.1 每连接 Frame

Dedicated Server 不再构造一份全局字节广播给所有人，而是：

```text
for each connection:
  Resolve commandAck(connection)
  Resolve visible entities(connection)
  Resolve baseline(connection)
  Build ReplicationFrame(connection)
  Send SnapshotUnreliableSequenced
```

即使 DS-Demo 暂时所有连接看到相同 Actor，也必须保留 per-connection Frame 构造入口。

### 12.2 生命周期

```text
Match Spawn Authority Actor
  → ReplicatedEntityRegistry.Register
  → 每个相关连接可靠发送 SpawnRecord
  → 收到确认后允许 delta update

Match Despawn
  → Registry 标记
  → 可靠发送 DespawnRecord
  → grace 后释放 EntityId
```

普通 Snapshot 丢失不得触发 Despawn。

### 12.3 事件

分类：

| 数据 | 通道 |
|------|------|
| Pose / ActionFrame / HP 持久状态 | Snapshot |
| Spawn / Despawn | Reliable Control |
| Join / MatchState | Reliable Control |
| Input Command | Unreliable Redundant |
| Hit Cue / VitalityEdge | Reliable Event 或事件序列冗余 |
| UI 非关键统计 | Snapshot 或低优先级 |

HP 可以由后续状态恢复，但死亡重播、一次性奖励、不可重建 Cue 不能只依赖一个不可靠 Tick。

### 12.4 DS 不接受状态上行

允许：

```text
InputCommand
TargetRequest
AttackClaim（未来 PVP，服务器回溯验证）
SnapshotAck
EventAck
Ready / Leave
Diagnostics
```

禁止直接覆盖：

```text
Position
Health
ActionState
Damage
HitResult
EnemyState
```

客户端状态若上传，只能作为 `Hint / Claim / Diagnostic`，不能成为 Authority Snapshot。

---

## 13. 启动配置与 Build

### 13.1 Unity Build 选择

生产服务器应使用 Unity Dedicated Server Build Target，而不只是普通 Client Build 加：

```text
-batchmode -nographics
```

原因：

- Dedicated Server Target 会针对 CPU、内存和磁盘做服务器裁剪优化；
- 构建时定义 `UNITY_SERVER`；
- 普通 Desktop Headless 只是不初始化图形设备，不包含全部服务器优化。

### 13.2 Build Profile

目标：

```text
Client-Windows
Server-Windows
Server-Linux
```

服务器 Build Profile：

- Dedicated Server subtarget；
- Server Bootstrap Scene；
- 只包含 Server 必要程序集和内容；
- Development Server 与 Release Server 分开；
- 内容 Fingerprint 写入 BuildInfo；
- 输出独立目录。

### 13.3 启动参数

```text
-batchmode
-nographics
-logFile <path>
-serverPort 7777
-bindAddress 0.0.0.0
-maxPlayers 4
-matchId <id>
-mapId <id>
-tickRate 60
-snapshotRate 20
-region <name>
-serverConfig <path>
```

配置优先级定案：

```text
Command Line
  > Environment Variables
  > Config File
  > Safe Defaults
```

Secrets：

- 只能来自环境变量或 Secret Provider；
- 禁止写入 ScriptableObject、日志或仓库。

### 13.4 ServerLaunchConfig

```text
ServerLaunchConfig
  bindAddress
  port
  maxPlayers
  matchId
  mapId
  tickRate
  snapshotRate
  contentFingerprint
  idleShutdownSeconds
  reconnectGraceSeconds
  logLevel
```

解析失败：

- 打印字段名和安全值；
- 返回非零退出码；
- 不以默认端口悄悄启动错误比赛。

---

## 14. 运维生命周期

### 14.1 Health

```text
Liveness
  进程主循环仍在运行

Readiness
  Content 已加载
  Transport 已绑定
  Match 可接入
  Tick 未持续过载
```

状态：

```text
Starting → Ready → Draining → Unhealthy
```

可通过：

- 轻量 HTTP health port；
- 编排器 Adapter；
- 本地状态文件；
- stdout structured event。

具体宿主可替换，Match 不依赖 HTTP。

### 14.2 优雅退出

收到：

- SIGTERM；
- 管理命令；
- Match 完成；
- Fatal Tick Overrun；
- Transport Fatal Error。

流程：

```text
Stop Accepting
  → Match Ending
  → 广播 ServerShutdown / MatchEnd
  → Drain reliable queue（有上限）
  → Flush metrics/log
  → Dispose Session/Transport
  → Exit with code
```

禁止收到停止信号后立即 `Environment.Exit`，导致客户端只看到超时。

### 14.3 退出码

| Exit Code | 含义 |
|-----------|------|
| 0 | Match 正常结束 |
| 10 | 配置错误 |
| 11 | Content mismatch / load failure |
| 12 | Bind 失败 |
| 20 | Fatal simulation failure |
| 21 | Sustained tick overrun |
| 30 | Transport fatal |

精确数字可调整，但必须稳定并写文档。

### 14.4 空房关闭

DS-Demo：

```text
Listening/Lobby 空闲超过阈值
  → 正常退出
```

Playing 中所有玩家离开：

```text
结束 Match
  → grace
  → 退出
```

由外部 Allocation 服务决定是否重新启动新进程。

---

## 15. 安全边界

### 15.1 Session

- 协议版本；
- Content Fingerprint；
- Auth Ticket Adapter；
- Connection rate limit；
- Join payload size limit；
- 同一 ticket 重放限制；
- Room capacity；
- 超时；
- 明确 DisconnectReason。

### 15.2 Command

每名玩家验证：

- Sender 是否拥有该 Actor；
- Sequence 是否递增；
- ClientTick 是否在窗口；
- 每秒 Command 数量；
- Buttons bitset 是否有非法位；
- Move axis 是否在量化范围；
- Yaw 是否规范；
- 单 Tick 最大合并数量；
- 长时间积压处理策略。

### 15.3 Gameplay

服务器独占：

- Position；
- Action 状态；
- Numeric；
- Damage；
- Hitbox；
- AI；
- Spawn / Despawn。

Listen Host 不能作为反作弊服务器；Dedicated Server 才能避免房主直接修改权威内存。

### 15.4 网络

- 不信任 Endpoint 身份本身；
- 不记录 auth secret；
- 限制单包和队列长度；
- 处理 malformed packet 不得崩进程；
- 认证前消息种类白名单；
- 大量非法包触发连接级断开，不污染 Match。

---

## 16. 可观测性

### 16.1 Structured Log

每条关键日志包含：

```text
timestamp
severity
serverBuild
matchId
sessionEpoch
connectionId（如有）
playerId（如有）
serverTick（如有）
eventName
reason
```

禁止只输出：

```text
"Error"
"Client disconnected"
"Something went wrong"
```

### 16.2 Metrics

| 类别 | 指标 |
|------|------|
| Process | uptime、memory、GC、CPU frame time |
| Tick | tick duration、overrun、catch-up、max backlog |
| Session | connections、join/reject、timeout、disconnect reason |
| Transport | packets/bytes、loss、out-of-order、RTT、jitter |
| Replication | entities/frame、bytes/client、spawn/despawn、event backlog |
| Commands | received/applied/dropped/held/invalid |
| Gameplay | actor/enemy count、hit resolve count、match duration |

### 16.3 诊断快照

Debug / Development Server 可输出：

```text
Current MatchState
Current ServerTick
Players / Connections
Entity Registry
Pending Commands
Replication Baselines
Last N Disconnect Reasons
Tick Histogram
```

Release Server 禁止输出敏感 token 和完整用户输入历史。

---

## 17. 分阶段交付（任务 / 验收 / 出口）

> 勾选：未开始 `[ ]`；完成后 `[x]`。  
> DS0～DS6 为 DS-Demo 主路径；DS7～DS8 为 DS-Full。  
> 每阶段必须继续通过 Client / Listen 回归，不允许只在 Server Build 中“看起来能启动”。

### DS0 — Dedicated 前置审计与行为冻结

**任务**

- [ ] 依赖 GF0 的 Codec Golden Bytes 与双进程回归。  
- [x] 列出所有 Host 本地玩家假设：Query、PlayerId、HostActorId、Spawn、Config、HUD。（2026-08-17：见 W0 审计 §4.1）
- [x] 列出 Authority Actor 对 Presentation / Unity Object 的依赖。（2026-08-17：见 W0 审计 §4.2）
- [ ] 记录单人 Listen Host 与双人当前基准。  
- [x] 创建 Server Dependency Guard 测试清单。（2026-08-17：见 W0 审计 §4.4）
- [x] 定案 DS-Demo：一场一进程、2～4 玩家、无重连。

**验收**

- [ ] 能从代码依赖图定位所有 LocalPlayer → Host Room 路径。  
- [ ] 能列出 Dedicated Server 必须保留的 Gameplay Content。  
- [ ] 当前功能回归通过。  

**出口：** DS 分离范围可证明，不靠运行时报 Null 逐个补。→ **未达成**

### DS1 — Process Role 与 Bootstrap

**任务**

- [x] 定义 `NetProcessRole.Client / ListenServer / DedicatedServer`。  
- [x] 创建 `DedicatedServerBootstrap` 与 `ServerLaunchConfig`。  
- [x] Bootstrap 装配 Transport、Session、Match、每连接 Replication。  
- [x] `CombatWorldController` 不再承担 DS 启动。  
- [x] Dedicated 进程不创建 `PlayerController`、HUD、Feedback、Camera。  
- [x] 增加启动失败退出码。  
- [x] **禁止**以 `ReplicationRole.ListenHost` 冒充 Dedicated。

**验收**

- [x] EditMode：不同 ProcessRole 生成正确 Composition。  
- [ ] Headless Play：无本地玩家也可到 Listening。  
- [x] 配置/绑定失败返回明确错误和退出码。  
- [x] Server Bootstrap 程序集不引用 Client HUD/Input/Camera。

**出口：** Dedicated Server 成为独立宿主，而不是 Host 开关。→ **代码已切（2026-08-19）；Editor Play 待确认**

### DS2 — ServerSession 与 N 玩家

**任务**

- [x] 依赖 GF2 ServerSession / ConnectionRegistry / PlayerRegistry。  
- [x] 删除固定 `GuestPlayerId = 2`。  
- [x] 删除单 `_guest`，改 per-player collection。  
- [x] Join 不再等待 Host Local Actor。  
- [x] JoinAccept 删除 HostActorId 依赖（字段可 Invalid）。  
- [x] 每连接独立 CommandStream / ACK / Idle。  
- [x] `MaxPlayers` 改 ServerLaunchConfig。  
- [x] MatchCoordinator 负责角色、队伍和出生点。  
- [x] **删除**“从 Host Root +2m 生成客机”。

**验收**

- [x] Loopback 三个 Client 可分配不同 PlayerId。  
- [x] 无 LocalPlayer 的 Server 可 Accept 第一名玩家。  
- [x] 一名玩家断开不移除其他玩家。  
- [x] 每连接 ACK 不串线。  

**出口：** 服务器身份模型不再等同于“房主 + Guest”。→ **已达成（2026-08-19：EditMode）**

### DS3 — Headless Authority World

**任务**

- [ ] 创建 `ServerSimulationRunner`。  
- [ ] 使用单调时间和固定 60Hz Tick。  
- [ ] 删除服务器 Render / LateUpdate。  
- [ ] 创建 Headless Authority Character 装配。  
- [ ] Gameplay Notify / Presentation Notify 分类。  
- [ ] 服务器不创建 PlayableGraph、VFX、SFX、HitStop Presentation。  
- [ ] AI、Motor、Action、Numeric、Hitbox、Hurtbox 完整运行。  
- [ ] 增加 Tick duration / overrun / backlog 指标。  
- [ ] **禁止**服务器使用 PredictedLocomotionDriver。

**验收**

- [ ] 无 Camera、Animator、Model 的 Authority Actor 可移动、出招、命中、死亡。  
- [ ] 固定输入脚本与普通 Host Authority 的最终 Pose/HP/Action 一致。  
- [ ] 关掉全部 Presentation 后 AI 仍能完成战斗。  
- [ ] 持续运行 10 分钟无逻辑 Tick 漂移或空引用。

**出口：** 服务器权威玩法与表现解耦。→ **未达成**

### DS4 — Server Content Manifest

**任务**

- [ ] 定义 `ServerContentManifest`。  
- [ ] Character 使用 `NetArchetypeId` 创建。  
- [ ] 拆 Gameplay / Presentation 内容引用。  
- [ ] Action Gameplay Bake 包含帧数、位移、Hitbox、Cancel、Cost。  
- [ ] StaticCollisionBake 进入 Server Map Manifest。  
- [ ] 生成 ContentFingerprint。  
- [ ] 未知 Archetype / Action / GraphNode 明确失败。  
- [ ] **删除**客户端/服务器回退 `_enemyConfigs[0]`。

**验收**

- [ ] Server 不加载 Model/Texture/Audio/VFX 即可完整模拟。  
- [ ] Server 与 Client Gameplay Fingerprint 一致才可 Join。  
- [ ] 修改 Gameplay Bake 会改变 Fingerprint。  
- [ ] 修改纯 VFX 不应改变 Gameplay Fingerprint（若产品允许表现热更）。

**出口：** Server Build 内容闭包明确。→ **未达成**

### DS5 — Match / Replication / Client 接入

**任务**

- [ ] 建 Lobby → Starting → Playing → Ending 状态机。  
- [ ] Spawn/Despawn 成为可靠生命周期真源。  
- [ ] ReplicationServer per-connection 构造 Frame。  
- [ ] 每名玩家 Authority Actor 接收自己的 CommandStream。  
- [ ] 客户端 Owner 走 Prediction / Reconcile。  
- [ ] 其他玩家和敌人走 RemoteProxy。  
- [ ] Hit / Death 事件选择可靠事件或冗余序列。  
- [ ] Match End 可靠下发。  
- [ ] Listen Server 改为 ServerRuntime + LocalClientRuntime，或明确排入 DS6。

**验收**

- [ ] 两个独立 Client 连接无本地玩家 Server。  
- [ ] 双方可移动、出招、打同一敌人。  
- [ ] HP、死亡、敌人生命周期最终一致。  
- [ ] 客户端修改本地 HP 会被 Server 覆盖。  
- [ ] 服务器日志能按 Connection/Player/Entity 追踪一条命令。

**出口：** 真 Dedicated Authority 对局成立。→ **未达成**

### DS6 — Unity Dedicated Build 与 DS-Demo 验收

**任务**

- [ ] 安装对应平台 Dedicated Server Build Support。  
- [ ] 创建 Windows Server / Linux Server Build Profile。  
- [ ] 配置 Server Bootstrap Scene。  
- [ ] 使用 Dedicated Server subtarget 构建。  
- [ ] 增加 CLI / Environment / Config 解析。  
- [ ] 增加 Health Ready 与优雅退出。  
- [ ] 增加 CI Server Build + 启动烟测。  
- [ ] 打包后检查不需要的 Client 资产和程序集。  
- [ ] 输出本地启动说明。

**验收**

- [ ] Server Build 在无 GPU 环境启动。  
- [ ] 两个 Client Build 可完成一局。  
- [ ] Server 进程中无 Camera、AudioListener、InputSampler。  
- [ ] Match 结束或空房超时后正常退出码 0。  
- [ ] Bind / Content / Config 错误返回非零退出码。  
- [ ] 人工验收 §19.1 全通过。

**出口：** DS-Demo 完成。→ **未达成**

### DS7 — 公网、安全与重连

**任务**

- [ ] 接入可靠控制 / 不可靠时序 / 事件通道。  
- [ ] 增加 auth ticket Adapter。  
- [ ] 增加 Command rate / tick window / bitset 验证。  
- [ ] 增加 reconnect token 与 grace。  
- [ ] 增加 Snapshot / Event ACK。  
- [ ] 增加 RTT / jitter / loss 模拟。  
- [ ] 增加 graceful drain。  
- [ ] malformed / flood 只断连接，不崩 Match。

**验收**

- [ ] 100ms RTT、20ms jitter、5% 丢包可完成对局。  
- [ ] 非法状态上行不能改服务器 HP / Pose / Action。  
- [ ] 断线 grace 内重连恢复 Owner；超时正确 Despawn。  
- [ ] 认证失败不会创建 Player / Actor。  
- [ ] SIGTERM 时客户端收到结束原因或在 drain 超时后退出。

**出口：** 公网安全和恢复基线成立。→ **未达成**

### DS8 — 运维、容器与负载

**任务**

- [ ] 结构化日志。  
- [ ] Process / Tick / Network / Match metrics。  
- [ ] Liveness / Readiness Adapter。  
- [ ] Linux Server 容器镜像。  
- [ ] 非 root 用户运行。  
- [ ] 资源 limit / request 基线。  
- [ ] 2～4 玩家 + 目标敌人数压测。  
- [ ] 持续 Tick Overrun 转 Unhealthy。  
- [ ] Allocation / Matchmaking 只定义 Adapter，不绑定厂商。  
- [ ] 输出 Runbook：启动、停服、日志、崩溃、版本回滚。

**验收**

- [ ] 容器启动后 Ready，Match 结束后退出。  
- [ ] 负载下 Tick p95 / p99 在预算内。  
- [ ] 内存无随 Match 时间持续增长。  
- [ ] 编排器能区分配置失败、Bind 失败、模拟失败。  
- [ ] 人工验收 §19.2 全通过。

**出口：** DS-Full 完成。→ **未达成**

---

## 18. 自动测试

### 18.1 Bootstrap / Config

- [ ] ProcessRole Composition。  
- [ ] CLI > Env > File > Default 优先级。  
- [ ] 非法端口、人数、TickRate。  
- [ ] Secret 不进入日志。  
- [ ] ExitCode 映射。

### 18.2 Session

- [ ] 无 HostActor 也可 Join。  
- [ ] 1～N PlayerId 分配。  
- [ ] Room full。  
- [ ] version / content / auth reject。  
- [ ] per-connection timeout。  
- [ ] 一人断开不影响其他人。

### 18.3 Match

- [ ] Lobby / Ready / Start / End 状态转移。  
- [ ] Playing 后 Join 策略。  
- [ ] Disconnect 策略。  
- [ ] 空房退出。  
- [ ] Spawn point / team / archetype。

### 18.4 Simulation

- [ ] Headless 与 Host Authority 固定脚本结果一致。  
- [ ] Null Presentation 不改变 ActionFrame。  
- [ ] VFX/SFX Notify 不在 Server 执行。  
- [ ] Gameplay Notify 完整执行。  
- [ ] Tick catch-up / overrun。

### 18.5 Replication

- [ ] N 连接独立 ACK。  
- [ ] Spawn / Despawn 可靠。  
- [ ] 旧 Snapshot 丢弃。  
- [ ] Hit / Death 不丢不重。  
- [ ] Archetype 正确创建 Client View。

### 18.6 Security

- [ ] 非 Owner 控制其他 Actor 被拒。  
- [ ] Position / HP / Damage 状态上行被拒。  
- [ ] Command flood 限制。  
- [ ] malformed packet 不崩进程。  
- [ ] reconnect token 不能跨 Player 使用。

### 18.7 Build Smoke

```text
Build Server
  → Start Process
  → Wait Ready
  → Start Client A/B
  → Join
  → Scripted Move/Attack
  → Assert Server Tick / HP / MatchEnd
  → SIGTERM or Normal End
  → Assert ExitCode / Logs
```

---

## 19. 人工验收

### 19.1 DS-Demo

| ID | 操作 | 通过标准 |
|----|------|----------|
| H-DS-D-1 | 无 GPU 环境启动 Dedicated Build | Ready，监听正确端口 |
| H-DS-D-2 | 两个 Client 加入 | 两者均为远端玩家；服务器无本地玩家 |
| H-DS-D-3 | 双方移动、急停、折返 | Owner 即时；服务器权威 Pose 正确 |
| H-DS-D-4 | 双方连招/闪避/打敌人 | HP、Hit、Death 最终一致 |
| H-DS-D-5 | Client 改本地 HP/Pose | 下一权威状态覆盖；服务器不受影响 |
| H-DS-D-6 | Client A 断开 | A Actor Despawn；B 和 AI 不崩 |
| H-DS-D-7 | Match 正常结束 | 客户端收到结果；Server 正常退出 |
| H-DS-D-8 | 查看 Server 进程 | 无 Camera、Input、VFX、Audio 权威依赖 |
| H-DS-D-9 | 使用不同 Content Build 加入 | 被明确拒绝 |
| H-DS-D-10 | 端口被占用 | Server 非零退出且有明确原因 |

### 19.2 DS-Full

| ID | 操作 | 通过标准 |
|----|------|----------|
| H-DS-F-1 | 100ms RTT + 5% 丢包 | 可完成对局 |
| H-DS-F-2 | 短暂断线后重连 | grace 内恢复同一 Player/Entity |
| H-DS-F-3 | 非法 / 高频 Command | 被丢弃或断开，不影响其他玩家 |
| H-DS-F-4 | 运行中 SIGTERM | 进入 Draining，客户端收到原因 |
| H-DS-F-5 | 人为制造 Tick Overrun | 指标报警并转 Unhealthy |
| H-DS-F-6 | 容器部署 | Ready/Liveness 正确 |
| H-DS-F-7 | 连续多轮启动/结束 | 无端口、内存、静态注册残留 |
| H-DS-F-8 | 查看日志链 | 可由 matchId/playerId/entityId 定位一次命中 |

---

## 20. 保留 / 拆分 / 删除

### 20.1 保留

| 内容 | 用途 |
|------|------|
| `SimulationWorld` | Server Authority World |
| `InputFrame` | 玩家命令正文 |
| `CharacterActor` 逻辑能力 | DS-Demo Headless Authority |
| `CombatHitPipeline` | Server 独占命中 |
| `ActorReplicationSnapshot` 语义 | 迁移为 Character Schema |
| `ActionReplicationCatalog` 稳定 Id 思想 | 演进为 Content Manifest |
| `IReplicationTransport` 的 Adapter 思想 | 由更通用 Transport 端口替换 |

### 20.2 拆分

| 当前 | 目标 |
|------|------|
| `CombatWorldController` | Client Bootstrap / Server Bootstrap / Listen Bootstrap |
| `SimulationHost` | Client Prediction Clock / ServerSimulationRunner |
| `ReplicationRoomHost` | ServerSession / Match / ReplicationServer / Authority Adapter |
| `ReplicationRoomClient` | ClientSession / Prediction / Proxy Presentation |
| `CharacterActorFactory` | Simulation Factory / Client Presentation Factory |
| `CharacterConfig` | Gameplay Manifest / Presentation Config |
| `RoomHudInfo` | NetMetrics + Client HUD Presenter |

### 20.3 删除

| 删除 | 原因 |
|------|------|
| `GuestPlayerId = 2` | DS 所有人都是连接玩家 |
| 单 `_guest` | 必须支持 N Connection |
| Join 依赖 HostActor | DS 无 Host 玩家 |
| Guest Spawn 依赖 Host Root | Spawn 属于 Match |
| Guest Config 复制 Host Config | 角色由 Archetype/Loadout 决定 |
| Authority Server 的 Render/LateUpdate | DS 无表现 |
| Server 的 HitStop/VFX/SFX | 非权威表现 |
| 同一份字节广播所有连接 | ACK/Relevancy/Baseline per-connection |
| Client 状态覆盖 Server 的任何入口 | 破坏权威 |

---

## 21. 风险与对策

| 风险 | 对策 |
|------|------|
| 当前 CharacterActor 无法脱离动画运行 | DS-Demo 先 Null Presentation；DS-Full 再拆 Simulation/Presentation |
| Unity Server 资产裁剪导致动态 Prefab 空引用 | Server Manifest + Build Smoke；不依赖运行时 Find/隐式引用 |
| Listen 组合后本机角色双实例 | 明确 Server Authority / Local Client Presentation；Loopback 测试生命周期 |
| Tick 与 Unity PlayerLoop 抖动 | 单调时间 + 固定 accumulator + overrun 指标 |
| 为 DS 重写一套 Simulation | 禁止；必须复用同一 SimulationWorld/Character 逻辑 |
| Session 继续依赖 LocalPlayer | GF2/DS2 架构守卫，全局搜索门禁 |
| Server Build 能启动但不能打 | Headless 固定输入战斗测试，不以“监听端口成功”为完成 |
| Action Gameplay 依赖 AnimationClip | ActionGameplayBake；Server 不以 Clip 播放推进逻辑 |
| 多玩家 ACK 串线 | per-connection Frame 与测试 |
| 使用 `#if UNITY_SERVER` 掩盖耦合 | 仅 Bootstrap/Build 使用，Domain 禁止 |
| 公网裸 UDP 不可靠 | DS6 LAN 冻结；DS7 前接成熟可靠 UDP/Transport |
| Scope 膨胀到 Matchmaking | 只定义 Allocation/Auth Adapter，不实现平台服务 |
| 一进程多 Match 提前复杂化 | 首版一 Match 一进程 |
| 服务器接受 Client State 以“修正延迟” | 只接受 Command/Claim；Authority State 只由 Server 产生 |

---

## 22. 代码审查门禁

每阶段合并前检查：

1. Server 是否仍能在没有 LocalPlayer 时启动？  
2. 是否新增 Server → Client Presentation 引用？  
3. 是否把 `UNITY_SERVER` 写进 Gameplay State？  
4. 是否出现客户端状态覆盖 Authority 的入口？  
5. Connection / ACK / Baseline 是否 per-connection？  
6. Spawn / Despawn 是否由 Match + Replication Registry 驱动？  
7. Server Tick 是否仍固定 60Hz、无大 dt 和静默跳帧？  
8. Gameplay Notify 是否保留，Presentation Notify 是否剥离？  
9. 新内容是否进入 Content Fingerprint？  
10. 是否同时保留旧 Host 特殊路径和新 ServerRuntime？  
11. Dedicated Build Smoke 是否通过？  
12. 日志是否能定位 match / player / entity / tick？

任一问题无明确答案，不进入下一阶段。

---

## 23. 推荐开工顺序

```text
GF0 + DS0 审计
  ↓
GF1 / GF2 / GF3 / GF4               ★ 网络层分离完成
  ↓
DS1 Bootstrap
  ↓
DS2 无 Host 玩家 Session
  ↓
DS3 Headless Authority
  ↓
DS4 Server Content
  ↓
DS5 Match + Replication
  ↓
DS6 Dedicated Build               ★ DS-Demo
  ↓
ListenServer 组合收敛
  ↓
GF5 / GF6
  ↓
DS7 Public Net / Security / Reconnect
  ↓
GF7 / GF8
  ↓
DS8 Operations / Container / Load ★ DS-Full
```

> 总排期以 [`NETSYNC_FRAMEWORK_DEDICATED_MASTER_DEVELOPMENT_PLAN.md`](./NETSYNC_FRAMEWORK_DEDICATED_MASTER_DEVELOPMENT_PLAN.md) 为准。DS0 仅做只读审计；Dedicated 运行时实现从 GF4 出口关闭后开始。

最小可证明切片：

```text
DedicatedServerBootstrap
  → 无 LocalPlayer 到 Listening
  → Loopback Client Join
  → Server Spawn 一个 Headless Authority Character
  → 固定 InputFrame 推进 120 Tick
  → Client 收到 Snapshot
  → Disconnect 后可靠 Despawn
```

先证明这一条，再接完整动作、AI、命中和 Build。

---

## 24. 完成定义

### DS-Demo

- [ ] Dedicated Server 进程无本地玩家；  
- [ ] ServerRuntime 与 ClientRuntime 程序集分离；  
- [ ] 两个远端客户端可完成一局；  
- [ ] AI、Numeric、Hitbox、死亡只在 Server 权威；  
- [ ] Server 不创建 Camera/Input/VFX/SFX/Animator；  
- [ ] Spawn/Despawn、Command ACK per-connection；  
- [ ] Content mismatch 拒绝；  
- [ ] Unity Dedicated Server Build 可在无 GPU 环境运行；  
- [ ] 正常结束、空房、Bind 失败有正确退出行为。

### DS-Full

- [ ] 可靠控制 / 时序快照 / 关键事件通道；  
- [ ] 认证、命令验证、限流；  
- [ ] reconnect grace；  
- [ ] 结构化日志与 metrics；  
- [ ] readiness / liveness；  
- [ ] SIGTERM graceful drain；  
- [ ] Linux 容器与 CI smoke；  
- [ ] 目标负载下 Tick p95 / p99 达标；  
- [ ] Runbook 和回滚流程完整。

---

## 25. Unity 官方依据

- [Introduction to Dedicated Server](https://docs.unity3d.com/6000.6/Documentation/Manual/dedicated-server-introduction.html)  
  Dedicated Server Build Target 会针对服务器的 CPU、内存和磁盘使用进行优化，并裁剪不必要的图形相关内容。
- [Build your application for Dedicated Server](https://docs.unity3d.com/6000.6/Documentation/Manual/dedicated-server-build.html)  
  可通过 Build Profile、`StandaloneBuildSubtarget.Server` 或命令行 `-standaloneBuildSubtarget Server` 构建；构建时定义 `UNITY_SERVER`。
- [Desktop headless mode](https://docs.unity3d.com/Manual/desktop-headless-mode.html)  
  `-batchmode -nographics` 可无图形运行，但不等同于包含服务器优化的 Dedicated Server Build Target。
- [Dedicated Server package](https://docs.unity3d.com/Packages/com.unity.dedicated-server@1.6/manual/index.html)  
  提供 Multiplayer Roles、Content Selection 与 Server / Client 内容裁剪能力；动态实例化内容仍需项目自行验证。

---

## 26. 变更日志

| 日期 | 说明 |
|------|------|
| 2026-08-17 | 初版：基于 GF 通用层分离方案，定案 Dedicated / Client / Listen 进程模型、Headless Authority、Server Content、DS0～DS8、Build、运维与验收 |
