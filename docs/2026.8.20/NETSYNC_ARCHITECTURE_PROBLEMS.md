# NetSync 网络架构搭建问题回顾

> 撰写：2026-08-20  
> 范围：NS0～NS5 + W0～W11（W10/W11 为代码切面，Play 未验收）  
> 角色：**踩坑与合同备忘**，不是排期真源  
> 排期：[`../2026.8.17/NETSYNC_FRAMEWORK_DEDICATED_MASTER_DEVELOPMENT_PLAN.md`](../2026.8.17/NETSYNC_FRAMEWORK_DEDICATED_MASTER_DEVELOPMENT_PLAN.md)  
> 实现阅读：[`../2026.8.23/NETSYNC_FROM_JOIN_TO_HIT.md`](../2026.8.23/NETSYNC_FROM_JOIN_TO_HIT.md)

本文只收录仓库阶段总结、W0 审计与 Play 复验里**实际出现过**的问题。未落地的公网 Play 不写成「已踩坑」。命中 ring / 通用预测 / MTU 门禁已在 W10 代码落地。

---

## 0. 怎么读

| 类型 | 含义 |
|------|------|
| 设计阻塞 | 开工或拆层前审计出来的，不改就无法做 Dedicated / N 人 |
| Play 复验 | Editor / 双进程里打出来的，改合同后才关验收 |
| 反复模式 | 同一类错在多个 Wave 重演 |

每条尽量写：**现象 → 根因 → 处理 → 阶段**。

---

## 1. 阶段一览

| 阶段 | 时间 | 当时主路径 | 本阶段暴露的核心矛盾 |
|------|------|------------|----------------------|
| NS0～NS2 | 2026-08-14 | 找唯一玩家 / 同机幽灵 | 玩法 `FindObjectOfType`；Host 房间兼做一切 |
| NS3～NS5 / UE | 2026-08-15 | Listen 双进程 + 本机预测 | 猜片、50mm 每包硬吸、出招每包 Snap、座位双套 |
| W0 | 2026-08-17/18 | 冻结线格式与帧序 | Listen 假设堵死 Dedicated |
| W1～W4 / M1 | 2026-08-17/18 | ACTNet 分层 + Room 变薄 | 全量 Tick、缺席即销毁、Room 回流 Gameplay |
| W5～W6 | 2026-08-19 | 独立 Runtime + Headless | 无 Host 玩家则不 Accept；工厂必 Instantitate 模型 |
| W7～W8 | 2026-08-19 | Dedicated 可打 + 出包 | Owner 建成 Proxy、命令慢放、Hint 和解、Editor 误 Quit |
| W9 | 2026-08-20 | Listen = Server + LocalClient | 本机按渲染帧预测；Host 本机仍是特殊座位 |
| W10 | 2026-08-20 | Prediction + ChannelMux + 可靠命中 | 代码已切；100ms/5% Play 未打穿 |
| W11 | 2026-08-22 | Delta / Relevancy / GraphNodeKey | 代码已切；远敌裁剪 Play 未打穿 |

---

## 2. 分层与双轨

### 2.1 Room 同时是邮差、Match、Gameplay 和 HUD

- **现象：** `ReplicationRoomHost/Client` 握手、灌输入、创建角色、预测、Proxy、Hit Cue 全写在 Facade。
- **根因：** NS5 以「能打起来」为先，没有通用 Session / Replication。
- **处理：** W2 抽出 `ACTNet.Session`；W3 抽出 `ReplicationFrame`；W4 迁到 Adapter / Service，Room 只 Poll / 收发 / HUD。`RoomArchitectureBoundaryTests` 冻结禁区。
- **阶段：** W0 审计 §4.3；W4 / M1。

### 2.2 线格式双轨与「缺席即销毁」

- **现象：** 下行曾是 `AuthorityTick` 全量数组；没出现在数组里的实体当场销毁。多种敌人回退 `_enemyConfigs[0]`。
- **根因：** 没有显式 Spawn/Update/Despawn，也没有稳定 Archetype。
- **处理：** 单轨 `ReplicationFrame` + Sequence 丢旧；`CharacterSnapshotSchemaV1` + stableKey Catalog；未知 Id 必须失败。
- **阶段：** W3。删除：`AuthorityTick`、`CharacterReplicationCapture` 独立类型、Domain 旧 Proxy Factory。

### 2.3 禁止在旧 Host 上堆 Dedicated 开关

- **现象：** 最便宜的做法是 `if Dedicated` 挂在 `ReplicationRoomHost`。
- **根因：** Dedicated 无本机玩家、无相机、要 Headless、要独立进程生命周期。
- **处理：** `DedicatedServerRuntime` 独立程序集；W9 删除 `ReplicationRoomHost` / `ActHostRoomGameplay`，Listen 只组合同一 Runtime。
- **阶段：** W0 风险表；W5；W9。

### 2.4 同机预览双轨

- **现象：** Host 场景里再挂 ±2m 幽灵 / 预测预览，和真 Client 不是一条链。
- **根因：** 想在单进程里「看见客机」，却开了第二套表现。
- **处理：** 删除 `RemoteGhostViewController` / `PredictedClientPreviewController`。对照只走 ParrelSync / Dedicated + 真 Client。
- **阶段：** NS5 / CA；M1 明确禁止恢复。

---

## 3. 身份、Join、生命周期

### 3.1 玩法查找「场上唯一玩家」

- **现象：** 敌人感知、相机、HUD 直接 `FindObjectOfType<PlayerController>`。
- **根因：** 单人假设。
- **处理：** `ILocalPlayer` + `LocalPlayerService`；感知读花名册根。
- **阶段：** NS0。

### 3.2 Listen 假设堵死 Dedicated Join

W0 审计列出的阻塞（后在 W5～W9 逐条拆掉）：

| 假设 | 后果 |
|------|------|
| 只有 ListenHost / Client | 无 Dedicated 进程角色 |
| 固定 `GuestPlayerId = 2`、单 `_guest` | 不能 N 人、ACK 串线 |
| Join 必须有 Host LocalPlayer | Dedicated 永远不 Accept |
| JoinAccept 必须带 Host Actor | 无房主实体时客户端无法入房 |
| 出生 = Host Root + 2m | 出生不属于 Match |
| UDP 同一字节广播所有人 | 无 per-connection Baseline / ACK |

**处理：** `MatchCoordinator`（槽位 × 2000mm）；`AuthorityEntityId` 允许 Invalid；Join 不等 Host Actor；每连接 `ReplicationServer` + Hint。

### 3.3 JoinAccept 写了假 EntityId

- **现象：** 客户端以为自己的权威 Id 是 Match 槽位占位，World 里是另一套 `SimulationId`。
- **根因：** Accept 太早，Actor 还没进 World。
- **处理：** JoinAccept 必须写 World `SimulationId`。
- **阶段：** W7。

### 3.4 首帧把 Owner 建成 Proxy，客机不能操作

- **现象：** Dedicated 客机加入后 `CanPredict` 不开，人或敌人「看得见但不是自己」。
- **根因：** Drain 复制帧早于 `AcceptJoinIfReady`；同拍首帧 Spawn 时 Owner 身份未绑定。
- **处理：** 先 Accept Join 再 Drain；入房立刻 `PublishImmediateReplication` 发 Spawn。
- **阶段：** W7 Play。

### 3.5 MatchEnd 被同拍 Kick 吃掉

- **现象：** 对局结束客户端收不到可靠结束，或先 Shutdown 再漏包。
- **根因：** Session Kick 与应用 `MatchEnd` 抢同一拍；消息类型还曾和 Kick=7 冲突。
- **处理：** `MatchEnd = 8` + 可靠有序；先 Drain 再按 Session 结束收口。
- **阶段：** W7。

### 3.6 空房 / Editor 误退进程

- **现象：** Editor Play 对局一空或一结束，整份 Unity 退出。
- **根因：** 玩家构建要 `ExitOnMatchEnd`；Editor 若走同一策略会 Quit。
- **处理：** Editor 强制 `ExitOnMatchEnd=false`、空 Lobby 超时 0；玩家构建默认真退出。Playing 空房先 `MatchEnd(EmptyRoom)` 再回 Lobby。
- **阶段：** W8。

---

## 4. 时钟与帧序

### 4.1 客机按渲染帧推预测

- **现象：** 动作变快、连段乱、权威一到就被拉回。
- **根因：** `CharacterActor.Step` 必须跟 60Hz / 追帧。`Update` 每渲染帧 `Step` 会在 120/144Hz 上加速。
- **处理：**
  - 远端 Client：`AfterLogicStep` 发命令再 `StepPrediction`（空 World 只报时）。
  - Listen 本机：W9 初版误用每帧 `SampleSendPredict`；改为 `PeekAdvanceSteps` 对齐即将发生的权威步。
- **阶段：** NS5 问答已写明；W9 Play 复验再踩一次。

### 4.2 Host 必须先灌下一格再 Step

- **现象：** 命令晚一格或本格吃到空输入。
- **根因：** Room `-150` 写 `CurrentFrame+1`，`SimulationHost` `-100` 才消费。Dedicated 则是 `DrainCommands` 后 `Advance`。
- **处理：** 生产顺序测试冻结：Poll → 灌入 → Step → Capture → 发送。
- **阶段：** W0 / W4 / W7。

### 4.3 外部时钟插值比例停在 0

- **现象：** Listen 组合后本机 `Render(alpha)` 一直像贴在上一逻辑 Pose。
- **根因：** `DriveFromExternalClock` 时 `SimulationHost` 自己的 Kernel 不再 `ConsumeSteps`。
- **处理：** Runner 步进后 `PublishExternalInterpolationAlpha`。
- **阶段：** W9（随预测节拍一起修）。

---

## 5. 命令与 ACK

### 5.1 CarryForward 仍下发旧 Hint

- **现象：** 权威本步没吃新命令，客机却用旧预测位姿去和解当前权威帧。
- **根因：** `appliedHint` 被当成「最后一次成功 Hint」一直带着走。
- **处理：** 无新命令 / CarryForward 必须下发 **0**。
- **阶段：** NS5。

### 5.2 冗余批用 newest Hint 和解

- **现象：** Dedicated 连续闪避被整段拉回。
- **根因：** 多条冗余命令 Merge 进同一权威帧后，下行用 newest Hint。客机按「最后一条」的预测脸去对「压缩后的一格权威」。
- **处理：** `LastApplied` 用 newest 去重；下行 `appliedHint` 用本批**第一条**新 Hint。
- **阶段：** W7 Play。**禁止**再做 `DedicatedRemoteCommandQueue` 逐步慢放。

### 5.3 积压 Hint 按 60Hz 逐步灌入

- **现象：** B 要约约 0.3s 才看到 A 出手。
- **根因：** 想「平滑」消化积压命令，结果把瞬时边沿摊成多格。
- **处理：** 到包即 `MergeSample` 进下一权威帧。观察者延迟用快照，不靠命令队列。
- **阶段：** W7 复验（先做错，再删队列）。

### 5.4 非 Owner 命令灌进别人座位

- **现象：** 连接 A 可代打连接 B。
- **根因：** 只按连接收包，不校验 `SenderPlayerId`。
- **处理：** Runtime 过滤，只保留本连接 PlayerId。
- **阶段：** W7。

---

## 6. 预测、纠偏、表现

### 6.1 走跑猜片 / wish 映射 Idle·Walk·Run

- **现象：** 客机松手、折返、Sprint 与 Host 内层机对不上。
- **根因：** 房间用 `ResolveSelfKey` 猜片，没有跑同一套 `LocomotionStateMachine`。
- **处理：** UE1 本机同一 `CharacterActor`；删除猜片。UE2 Restore+Replay。UE3 删 `PredictedLocomotionVisual`。
- **阶段：** NS3～UE3。

### 6.2 50mm 每包 Restore+Replay

- **现象：** 客机走跑每包卡一下。
- **根因：** 内层机与权威常态就有厘米级偏差，阈太小等于每包回滚。
- **处理：** 有 Replay 时硬吸默认 **2m**。无 Replay 的旧单测仍可用 50mm。
- **阶段：** UE1 复验。

### 6.3 出招 / 闪避每包硬切表现

- **现象：** 位移和相机一起跳；连闪 yaw 追权威台阶。
- **根因：** 纠偏后无条件 `SnapPresentationToSimulation`；出招中相机仍跟朝向。
- **处理：** 仅走跑真 Snapped、或权威 Hit/Death 才硬切表现。`IsPresentingAction` 时相机暂停跟朝向。
- **阶段：** NS5 / UE。

### 6.4 权威未起手却 Stop 修正位移招

- **现象：** Branch_02 / 闪避中途被拉回或掐招。
- **根因：** `ActionId==0` 就 `StopAutonomousAction`，随后 Restore+Replay 只重放走跑；吸附窗还走 2m 硬吸。
- **处理：** `ActionMotionReconcileGate` 在 Dodge / 吸附 / 关碰撞 / 烘焙位移期间整段推迟硬吸；这些招权威 Idle 也不掐。
- **阶段：** NS5 穿敌吸附；W7 Dedicated 复验。

### 6.5 Prefill 漏 Directional 变体

- **现象：** 客机侧闪 / 后闪只有位移没有 Clip。
- **根因：** 只预填 Graph `node.Action`，变体哈希 `TryGet` 失败。
- **处理：** Prefill 必须含 `VariantResolver` 六向闪避。
- **阶段：** UE1。

### 6.6 客机走跑每帧 `SyncRootPoseFromSim`

- **现象：** 转向阻尼被清零，绕圈发飘。
- **根因：** 把模拟根每帧贴到表现根。
- **处理：** Replay 外禁止对该路径每帧 Sync。
- **阶段：** UE1。

### 6.7 用权威 FreezeFrames 拖本机时钟

- **现象：** 卡肉晚一截 RTT，本地招已演完又被冻。
- **根因：** 预测端跟权威停帧而不是本机几何重叠。
- **处理：** `PredictedHitStopConsumer` 本机请求卡肉；伤害仍只信下行。
- **阶段：** CA / NS5。

### 6.8 相机跟权威根而不是预测体

- **现象：** 客机镜头慢半拍或来回扯。
- **根因：** 跟随 `PlayerController` 权威 Transform。
- **处理：** 相机跟 `PresentationRoot` / 预测体；Look / CameraLock 不进 Snapshot。
- **阶段：** NS5。

---

## 7. Headless 与 Dedicated 进程

### 7.1 Authority 工厂必造 Model / Animator

- **现象：** 无 GPU / 无模型的 Dedicated 无法创建权威 Actor。
- **根因：** `CharacterActorFactory` 无条件 Instantiate、查 Animator、建 Playable / VFX / SFX。`ReplicationSeat.Authority` 只控制是否 Collect，不是无头。
- **处理：** `CharacterPresentationMode.AuthorityHeadless` + `NullAnimationPlayback`；禁止空 Animator Prefab 或 `#if UNITY_SERVER` 跳过异常。
- **阶段：** W0 §4.2；W6。

### 7.2 Headless `Play` 早退，Capture 永远 Idle

- **现象：** 怪物对峙只平移、远端不播走跑。
- **根因：** 无 Graph 时 `Play` 直接 return，`CurrentKey` 不更新；Capture 读到 Idle。
- **处理：** 无 Graph 仍必须记下逻辑 `CurrentKey`。Capture 读模拟 Locomotion 时钟，不读 Animator。
- **阶段：** W6 合同；W7 Play。

### 7.3 内容不一致仍能 Join

- **现象：** 两端 Action / 碰撞烘焙不同，入房后静默错配。
- **根因：** 只比 `contentVersion` 整数。
- **处理：** `ServerContentManifest` 指纹（版本 / 碰撞 Id / Archetype / Action Id；VFX 名不进指纹）。双方 Valid 且不同 → `ContentMismatch`。
- **阶段：** W6。

### 7.4 Server 程序集沾上 Client 表现

- **现象：** Dedicated 间接引用 `PlayerController` / Input / Camera / HUD。
- **根因：** 权威 World 与 Facade 放在同一程序集。
- **处理：** `ACTGame.Server` 只含 Runtime / Match / 启动配置。Listen 的 `LocalClientRuntime` 必须留在 Assembly-CSharp。守卫测试扫 `App/Server`。
- **阶段：** W5；W9 再确认。

### 7.5 一个 ServerSession 只能绑一条 Transport

- **现象：** Listen 既要收远端 UDP，又要本机「回环连接」。
- **根因：** Session 不支持双 Transport。
- **处理：** 同一 `UdpTransport` 听 `0.0.0.0`；本机 Client 连 `127.0.0.1:实际端口`。EditMode 仍用 `LoopbackNetwork`。
- **阶段：** W9。端口占用则 `WithBindPort(0)` 回退。

---

## 8. Listen 组合（W9）

### 8.1 Host 本机仍是「服务器玩家」

- **现象：** Listen 与 Dedicated 两条权威链；房主 0 预测，客机有预测，手感两套。
- **根因：** 场景 `PlayerController` 进 World 当 Authority；Capture 再单独拍 LocalPlayer。
- **处理：** 房主也 Join。Server 只建 Headless Guest；场景座位只装 Autonomous。Capture 只拍 Guest + 敌人。
- **阶段：** W9。出生改为 Match 槽位，首帧 Snapshot 会吸到槽位 × 2m——这是合同，不是回归失败。

### 8.2 每个渲染帧 `StepPrediction`

- **现象：** ListenHost 下本机连段加速、移动频繁拉回。
- **根因：** `SampleSendPredict()` 放在 `Update`，与 60Hz `Server.Poll` 不对齐。
- **处理：** 每渲染帧只 `SampleRenderInput`；`PeekAdvanceSteps` 几次就 `SendCommandAndPredict` 几次，且必须在 `Poll` 前发出（回环同拍才能灌进本步）。
- **阶段：** W9 Play。

### 8.3 预测根与权威根同时进感知

- **现象：** 同一个人两套根，敌人追预测体。
- **根因：** `LocalPlayerService` 把场景 Player 与 Headless `RemotePlayerSeat` 都列入 `PlayerRoots`。
- **处理：** 重建根时跳过 `IsLocalPredicted`。
- **阶段：** W9。

### 8.4 同进程 TargetSystem 双挂

- **现象：** Listen 上 Headless Hurtbox 与 Observer Proxy 可能同时可被选中。
- **根因：** 一个进程里权威世界和 Local Client 共享 Architecture。
- **处理：** 未加 Host 特判过滤（避免再开双轨）。感知根已排除预测座位。若锁敌异常再单开，不在 Runtime 上 `if Listen`。
- **阶段：** W9 已知限制，验收未挡。

---

## 9. 反复出现的模式

1. **按渲染帧当逻辑帧** — 客机预测、Listen 本机、采样边沿都栽过。采样可以每帧 OR；`Step` / 上行 Hint 必须跟权威步。
2. **用「最后一次成功」去对「当前这一格」** — 旧 `appliedHint`、newest Hint 和解。无新输入就要下发 0；和解用本批第一格。
3. **为平滑而排队慢放** — `DedicatedRemoteCommandQueue` 让观察者晚 0.3s。边沿要到包即 Merge。
4. **阈太小当分叉** — 50mm 走跑、出招每包 Snap。常态偏差不是回滚理由。
5. **座位 / 工厂开第二套** — Runner、猜片、同机预览、Host 本机特殊 Capture。同一 `CharacterActor` + Seat / PresentationMode。
6. **Listen 假设写进协议** — Guest=2、Host Root+2m、必须有 LocalPlayer。Match 分配，Join 不查房主 Actor。
7. **开关冒充新产品** — `if Dedicated` on Host Room。新产品用组合，旧入口同一阶段删掉。
8. **身份未绑定就应用复制** — Owner 变成 Proxy。Join 先于 Drain；首帧必须有自己的 Spawn。

---

## 10. 仍开放（不是本文「已踩坑」）

W10 **代码已落地**、Play 未打穿；其后能力仍开放：

- 100ms RTT / 20ms jitter / 5% 丢包完整对局（W10 出口）
- Play 上验证 2m Gate、连招超前、Hit Cue 只播一次
- Delta / Relevancy（W11 代码已切；Play 未验）
- 超 MTU 拆包（仍拒绝，未做）
- 重连、安全、容器、压测（W12）
- CI 自动出包 + 双 Client 拉起断言（W8 后置）

已从「未做」划出：可靠 Control/Event、通用 CommandHistory / StateHistory / SnapshotTimeline、MTU 拒绝门禁、W7 命中 8 条冗余删除。

W10 出口关闭前只称 **LAN Demo**，不称公网可用。

---

## 11. 证据索引

| 主题 | 文档 |
|------|------|
| 现行调用链 | [`../2026.8.23/NETSYNC_FROM_JOIN_TO_HIT.md`](../2026.8.23/NETSYNC_FROM_JOIN_TO_HIT.md) |
| 排期 / 出口 | [`../2026.8.17/NETSYNC_FRAMEWORK_DEDICATED_MASTER_DEVELOPMENT_PLAN.md`](../2026.8.17/NETSYNC_FRAMEWORK_DEDICATED_MASTER_DEVELOPMENT_PLAN.md) |
| Dedicated 启动 | [`../2026.8.19/DEDICATED_SERVER_LAUNCH.md`](../2026.8.19/DEDICATED_SERVER_LAUNCH.md) |
| 走跑 / 出招合同 | [`../2026.8.15/UE_ALIGNED_CLIENT_PREDICTION_PLAN.md`](../2026.8.15/UE_ALIGNED_CLIENT_PREDICTION_PLAN.md) |
