# ACTGame 编码规范

> 从现有代码归纳；随项目演进由 actgame-architecture skill 维护。

## 命名

| 类型 | 约定 | 示例 |
|------|------|------|
| 类/文件 | PascalCase，文件名与类名一致 | `PlayerController.cs` |
| 私有字段 | `_camelCase` | `_player`, `_machine` |
| 序列化字段 | `[SerializeField]` + camelCase | `walkSpeed` |
| 常量 | PascalCase 或 UPPER（Core 内 const 用小写 camel 亦可，与邻代码一致） | `MoveInputThreshold` |
| 枚举 | PascalCase 成员 | `CharacterStateType.Locomotion` |
| 接口 | `I` 前缀 | `ICharacterStateMachine` |

## 目录与类放置

- **一个 public 类一个文件**；辅助私有类可同文件
- **按职责分目录**，不用 C# namespace 区分模块
- 新 MonoBehaviour 放在对应 gameplay 目录（Player/Enemy/Camera），共享逻辑放 Character/ 或 Core/
- 占位目录保留 `.gitkeep`，实现后删除 `.gitkeep`
- 角色装配配置集中在 `CharacterConfig`；Scene 玩家入口只挂 `PlayerController` 并引用配置资产
- 玩家根对象运行时禁止挂载业务脚本；除 `PlayerController` 与 Unity 必需组件（如 `CharacterController`）外，角色业务必须是纯 C# runtime/service
- `CharacterController` 仅作表现代理（半径/高度配置与根跟随）；逻辑位移/重力/着地权威在 `CharacterMotorSim`，禁止 `CharacterController.Move` 进逻辑路径
- 新架构代码遵循 `Controller / System / Model / Command / Event / Query / Actor / Executor / Service` 后缀语义；禁止新增泛化 `Runtime` 后缀业务类
- `Controller` 仅用于 `App/Controllers` 下的 Unity 入口 `MonoBehaviour`，并继承 `AppControllerBase` 或实现 `IArchitectureController`
- `System` 后缀仅用于 `App/Systems` 下注册进 `ACTGameArchitecture` 的架构系统，并继承 `ArchitectureSystemBase` 或实现 `IArchitectureSystem`
- `Domain` 纯 C# 业务类优先使用 `Service` / `Actor` / `Executor` / `Resolver` / `Detector` / `Consumer`，不得直接访问 `ACTGameArchitecture.Interface`
- **动作系统目录分层**：`Combat/Actions/` 下按职责分四个子目录——`Definitions/`（动作数据 Schema，含 `Definitions/Timeline/`）、`Resolution/`（输入 → 动作选招）、`Execution/`（播放/帧推进/输入路由/旋转）、`Frames/`（Logic Tick 帧上下文契约）；核心脚本不散落在 `Actions/` 根目录
- **动作时间轴数据**：帧相关配置以 `ActionDefinition.Timeline` 为唯一真源；点事件用 `ActionNotify`（Event/VFX/SFX），区间窗口用 `ActionNotifyState`（Phase/Hitbox/Hurtbox/Cancel/Movement/Rotation）；禁止在 `ActionDefinition` 重新引入独立 `phases[]` 等双轨帧数据
- 跨系统通信使用 `ACTGameArchitecture` 的 Command / Query / Event；动作帧由 `ActionSimEvent` 经角色表现桥派发，但 Hitbox 消费者只能 Collect，禁止通过 App Command 同步修改目标
- 架构事件必须实现 `IArchitectureEvent`；架构查询继承 `ArchitectureQueryBase<TResult>` 或实现 `IArchitectureQuery<TResult>`
- **固定帧唯一入口**：角色业务只实现 `ISimulationActor.Step`，由 `SimulationHost → SimulationWorld` 以 60Hz 推进；Controller 禁止新增 `Update → Actor.Tick` 旁路
- **Action 单次推进**：每个 World 帧只允许 `CharacterActor` 调用一次 `ActionSim.Step`；Action/Hit/Death State 禁止自行再次推进会话
- **Action 整数帧权威**：`ActionSim.CurrentFrame` 是唯一时间权威；Action 内容固定 60Hz，禁止恢复 `Advance(dt)`、`FrameAt(elapsed)` 或 30Hz Runtime fallback
- **Action 逻辑/表现边界**：Sim 只输出 `ActionSimSnapshot` / `ActionSimEvent`；动画、Timeline 与位移只读消费，禁止回写帧、Graph 或命中确认
- **命中几何**：运行时 OBB 由 `SimCombatPose`（MotorSim）构建；挂点 Transform 只提供相对根局部；去重/自身排除用 `SimActorId`，禁止 `GetInstanceID`
- **Action 帧查询**：Runtime 表现和 Editor Scrub 共用 `ActionFrameQuery`；Editor 禁止执行 Runtime Step 或维护第二套窗口/段算法
- **帧边界切招**：Cancel、Recovery Entry 与 Graph 自动衔接只在当前帧排队，目标动作 frame 0 必须到下一 World 帧提交；禁止同一步递归推进多招
- **模拟身份**：World Actor 使用会话内单调 `SimActorId` 排序；禁止用 Unity `GetInstanceID()` 作为模拟顺序、命中身份或未来网络身份
- **启停生命周期**：Controller 在 `OnEnable` 注册 World、`OnDisable/OnDestroy` 对称注销；禁用 GameObject 不得继续被模拟
- **渲染输入汇聚**：本地设备 Actor 通过 `IRenderFrameSampler` 缓存渲染帧边沿，逻辑 Step 不直接依赖 Unity 渲染帧是否恰好发生。客机房间同样每渲染帧 `MergeLocalSample` 到下一 FrameHint，禁止只在 `AfterLogicStep` 里 `Sample`（会丢掉无逻辑步的 `WasPressedThisFrame`）
- **量化输入格式**：玩家设备、回放与未来网络输入使用 `InputFrame`；Move 为 sbyte、按钮为固定 bitset，禁止恢复 float/string `PlayerInputFrame`。AI 控制命令使用 Desire / Entry Request，不伪装设备输入
- **本机玩家入口**：玩法与相机通过 `LocalPlayerService` / `GetLocalPlayerQuery` / `GetPlayerRootsQuery` 取玩家；禁止 `FindObjectOfType<PlayerController>()`（仅 Editor Gizmo 可留）。Listen / Client 本机 `PlayerController.IsLocalPredicted` 为 true，敌人感知必须跳过该根、改跟权威 `RemotePlayerSeat`
- **客机相机跟朝向**：`CameraManager.ApplyFollowFacingYaw` 必须读 `ILocalPlayer.HasMoveIntent`、`MoveInput` 与 `PresentationRoot`。相机相对后退（`MoveInput.y` 为负）不跟朝向，避免与后退 wish 互追转圈。客机有 Autonomous `CharacterActor`，`PresentationRoot` 来自 Actor。他人/敌人 `RemoteCharacterProxy` 只读登记 `TargetSystem`；`OnHit` 空操作，禁止 Collect
- **朝向调试箭头**：`CharacterFacingDebugVisualizer` 只绑 `ICharacterFacingDebugTarget`（本机 Actor 或 RemoteProxy）；禁止再 Bind `PlayerController`。幽灵 wish 必须与对应 Tick 成对，禁止用当前帧本机输入画延迟模型
- **复制契约**：上行唯一为 `ClientCommand` 命令批；下行唯一为 `ACTNet.Replication.ReplicationFrame`。`ReplicationServer` 从权威 full set 生成显式 Spawn/Update/Despawn，`ReplicationClient` 原子应用并丢弃旧 Sequence；禁止恢复 `AuthorityTick` 全量数组、缺席即销毁或双轨 Codec。`ActorReplicationSnapshotCodec` 是角色快照字段布局唯一真源；`CharacterSnapshotSchemaV1` 只做 Schema 适配。`ActReplicationApplicationPayloadCodec` 只承载本步 applied hint，生产路径 hits 为空；命中走 `RoomMessageKind.ReplicationEvent`。Tick 由 Frame 承载。Session 信封、Join、Heartbeat、Kick 的唯一真源是 `ACTNet.Session`；禁止在 Room/App 恢复控制消息 switch 或 Endpoint/IdleTracker 状态。稳定网络身份使用 `Net*Id`；Character Archetype 由明确 stableKey 经 Catalog 映射，未知 Id 必须失败，禁止默认取首个敌人配置。传输唯一入口为 `INetTransport`（Session 外包 `ChannelMuxTransport`）。禁止把 CameraLock/Look/Lean 写入 Snapshot；禁止 ClientCommand 带 HP/坐标/招式名。`appliedClientFrameHint` 仅本步真正灌入远端命令时非 0，CarryForward 必须下发 0。装配用 `ReplicationSeat`，禁止 `if (isClient)` 开第二套 Actor
- **RemoteProxy**：他人/敌人幽灵只应用 Snapshot（`Domain/Character/Replication/`），禁止 `CharacterActorFactory`、`HitboxFrameConsumer`、`EnemyBrain.Step`。可按 ActionFrame 过点派发 VFX/SFX，禁止派发 Hitbox/MotionCommand。Host 用 `AfterLogicStep` 打包，不得只在渲染帧漏步发送。禁止再挂 Host 同机 ±2m 预览（`RemoteGhostViewController` / `PredictedClientPreviewController` 已删）
- **幽灵 Locomotion（他人）**：切 `AnimationKey` 时一次性相位硬切并可 Seek；Idle↔走跑冲刺用 Profile 默认 CrossFade。同键只 `Tick`
- **客机本机走跑**：本机 Autonomous `CharacterActor` 跑同一套 `LocomotionStateMachine`；纠偏合同见 [`docs/2026.8.15/UE_ALIGNED_CLIENT_PREDICTION_PLAN.md`](../../docs/2026.8.15/UE_ALIGNED_CLIENT_PREDICTION_PLAN.md)。他人仍 Snapshot。禁止猜片 / 摇杆硬映射 Idle/Walk/Run。已废止 Runner/CreateAutonomous
- **客机表现节拍**：本机 Clip/VFX 由 `CharacterActor.Step` + `CharacterActionPresentationBridge` 推进。禁止对自角色 `ApplySnapshot` Seek。权威 Tick 只更新纠偏与 HP/受击边沿。禁止同一逻辑帧 Tick 两次 Clip。走跑 Replay 外禁止每帧 `SyncRootPoseFromSim`（会清零转向阻尼）
- **复制目录 Prefill**：必须收录 Graph 节点 `Action` 与 `VariantResolver` 变体（六向闪避）。只预填 `node.Action` 时客机侧/后闪 `TryGet` 失败，只有位移没有 Clip
- **网络 Room 薄 Facade**：`ListenServerBootstrap` / `ReplicationRoomClient` 只做组合或 Session 调度与 HUD；CharacterConfig/PlayerController/EnemySpawnController/RemoteProxy/Hit Cue/HitStop 实现必须留在 `App/Networking` Service/Adapter，禁止迁回 Facade 或恢复 `ReplicationRoomHost`
- **预测位移**：通用历史与 Restore+Replay 在 `PredictionCoordinator`。`ActCharacterPredictionModel` 持电机与 2m/宽限/出招受击策略，禁止把 ActionId 写进 Coordinator。走跑带 `IPredictedLocomotionReplay` 时默认硬吸阈 `AutonomousHardSnapMm`（2m），禁止房间再传 50mm。无 replay 的旧 Predict 单测仍用 50mm。超 2m：`RestoreFromAuthority` + `ReplayTick`，禁止对走跑步 `ApplyInput`。Listen 本机与远端客机同一套 Owner 预测。纠偏可改预测电机，禁止把表现 Pose 写回权威 Motor。Lean 不进 Snapshot
- **纠偏后表现**：仅走跑真正 `Snapped`（≥ 2m）或权威 **Hit/Death** 才 `SnapPresentationToSimulation`。出招/闪避禁止每包硬切表现，否则插值被掐死、位移和相机一起跳。刚吸附后 8 包内 ≤ 150mm 只 Ack
- **出招中相机**：`CameraManager` 在 `ILocalPlayer.IsPresentingAction` 时暂停 L-DIR5 跟朝向，避免连闪 yaw 追权威朝向台阶
- **闪避回走跑**：Host `ActionState.Exit` 写 `SprintAfterDodge`；客机 Runner 再 Enter 必须 `LocomotionResumeRequest.AfterAction`，禁止 `Enter(default)` 从 Idle 重计 Sprint
- **预测出招**：本机 Autonomous `CharacterActor` 跑 `ActionSim` + 表现桥（Graph 起手 + Cancel 窗 + 推帧 + 烘焙位移 + Adhesion/Relocate）。禁止 Collect、进 World。Adhesion 读只读 Proxy 逻辑 Pose。卡肉由 `PredictedHitStopConsumer` 几何重叠后 `RequestHitStop`，禁止用延迟权威 `FreezeFrames` 再拖本机时钟。伤害只信权威下行。Ack 用 `PredictedActionAckQueue`：同招不 Seek 回旧帧；权威 `ActionId==0` 或 Hit/Death 则 `StopAutonomousAction`。本机已连到下一招、权威还停在上一招：只 Ack，不 Cancel。穿敌吸附 / SoftBodySuppress 窗、权威卡肉、或本机/权威正在 Dodge：`ActionMotionReconcileGate` 禁止 2m 硬吸位姿。Listen 本机也走同一套 Owner 预测
- **命中复制**：`CombatHitPipeline` 只在权威 Actor 收集；下行可靠 `RoomMessageKind.ReplicationEvent` + 快照里的 `VitalityReplicationEdge`。禁止再做 W7 最近 8 条帧内冗余。幽灵/预测不得再跑命中或扣血。边沿由 `CharacterVitality` 记一帧，`CharacterActor.Step` 开头清空
- **移动参考闭包**：相机相对移动只消费 `InputFrame.MoveReferenceYawQuantized`；CameraManager 只能 staged yaw，禁止把 PlanarBasis/Camera Transform 直接写入 Motor
- **输入阶段先于 Actor**：World 每帧先调用 `ISimulationInputProducer`，再按 Id 执行 Actor；AI Brain 在该阶段写通用命令槽并为统一时序提交空 `InputFrame`
- **命中延迟结算**：Hitbox 几何检测只写共享 `CombatHitPipeline`；全体 Actor Step 完成后按 `SimHitKey` 排序，再统一伤害、Reaction 与命中确认
- **PostCombat 收尾**：依赖本帧命中结果的 OnHitConfirm/OnWhiff 与动作自然结束只在 `ISimulationPostCombatActor` 执行；不得恢复攻击者 Step 内即时目标回调
- **App 只读结果**：`PublishAttackHitCommand` / `AttackHitEvent` 只发布整帧已结算结果；Event Handler 禁止暂停 `ActionSim`、修改 HP/状态或生成 Sim 输入
- **边沿展开**：同一目标帧的多次渲染采样只对 Pressed/Released 做 OR；本地追帧可延续 Move/Held，但禁止从 Held 推导或重复边沿
- **模拟/表现 Pose 分离**：权威根只在 `SimulationWorld.Step` 改变；模型与相机通过 `CharacterPresentationBridge` 插值，Render 禁止回写碰撞、命中或 Hash 状态

## Unity 组件模式

```csharp
[RequireComponent(typeof(SomeComponent))]
public class MyBehaviour : MonoBehaviour
{
    [Header("Group Name")]
    [SerializeField] float someValue = 1f;

    SomeComponent _dependency;

    void Awake() => _dependency = GetComponent<SomeComponent>();
}
```

- 依赖用 `GetComponent` 在 Awake 解析，避免每帧 Find
- 需要运行时装配的业务逻辑优先做纯 C# 构造函数注入；不要用 `AddComponent` 伪装装配
- 可选引用用 `[SerializeField]` + null 回退（如 `Camera.main`）
- 缺失关键引用：`Debug.LogError` + `enabled = false`（见 InputReader）

## 状态机约定

1. **Core 层**保持泛型，不出现 `UnityEngine` 引用
2. **Character 层**：
   - Context 只存数据与组件引用，不写复杂逻辑
   - State 的 `Tick` 写状态内逻辑；`CanTransitionTo` 写转换条件
   - 新 State 在 `CharacterStateMachine.RegisterStates()` 或子类 override 中注册
3. **子类 StateMachine**（如 Player）只负责 `UpdateContext()` 填充 Context，不重复 Tick 逻辑
4. 状态切换对外用 `TryChangeState`；内部强制切换才用 `force: true`
5. **Locomotion 内层机**：顶层保持 `CharacterStateType.Locomotion`；相位用嵌套 `LocomotionStateMachine`（`LocomotionPhase`）。相位 `CanTransitionTo` 默认全开，由各态 `Tick` 主动切；`Tick` 只做转换，`ExecuteFrame` 做 Motor/动画（由宿主在转换后调用）。禁止再引入手写 `_phase` 袋式 `LocomotionService`

## 动画约定

- 逻辑层使用 `AnimationKey`，Profile 映射到 `AnimationClip`（不映射 Animator 状态名）
- 播放走纯 C# `CharacterAnimationService`；后端为 `IAnimationPlayback`（当前 `PlayableAnimationPlayback`，可换 Animancer）
- 需要独占时 `SetLocked(true)`；卡肉用 `SetSpeed(0)`，禁止业务直写 `Animator.speed`
- Locomotion：`applyRootMotion = false`，水平位移经 `CharacterMotor` → `CharacterMotorSim`；Transform/CC 跟随 XZ
- 角色互撞（定案）：逻辑圆盘软弹开，按 `softBodyMass` 分配推力；大体型勾 `softBodyImmovable`；禁止 Unity Physics/CC 互撞权威；静态障碍烘焙硬挡
- 联网（定案）：组队 PVE 为 Dedicated 权威状态同步（Listen = 同进程再开 LocalClient）；上行量化 `InputFrame`，下行 `ReplicationFrame`；命中只在权威 `CombatHitPipeline`。禁止全端同构输入广播作为产品主路径，禁止客户端上报伤害结果，禁止以齐帧停等作为手感模型。锁步 L0～L2 模拟核仍适用。服务器写法见下方「服务器 / 权威进程」。阅读：`docs/2026.8.23/NETSYNC_FROM_JOIN_TO_HIT.md`
- Action：烘焙表就绪时查表写 MotorSim；未烘焙且 `UseRootMotion` 时由 `CharacterRootMotionDriver` 经 Motor 写入；否则可用 `MovementNotifyState` 脚本位移
- 同 key 不重复 Play（门面 `_currentKey` 去重）；无 Animator Controller 业务依赖
- 角色销毁时 `CharacterActor.Dispose()` 释放 PlayableGraph

## 输入约定

- 使用 Input System + `.inputactions` 资产；Action Map 命名 `Player`
- **采集**：玩家设备边界实现 `ILocalInputSampler` 并写下一逻辑帧槽；敌人 Brain 提交 `LocomotionDesire` + `ActionEntryRequest`，玩家路径不变
- **原始中枢**：`InputManager` 由 `CharacterActor` 持有；只摄入量化 Move 与 Pressed/Held/Released bitset，不承担 AI Move 覆盖或动作缓冲
- **相机 Look**：渲染帧 Look 不进入玩法 InputFrame，由 `PlayerController.LookInput` 直接提供给 CameraManager
- **切敌与镜头锁分离**：TargetSwitch 是 InputFrame gameplay 边沿；CameraLock 是本地表现输入。锁定键禁止选择目标或写 Character/ActionSim
- **设备映射**：`GameplayIntentProfile` 是 InputActionReference、长按阈值与上下文映射的**项目唯一**配置源，经 `GameplayIntentSettings` 加载；**禁止**再挂到 `CharacterConfig`
- **语义生产**：`GameplayIntentProducer` 在 `InputManager.IngestFrame` 后输出 `GameplayIntentType`
- **上下文意图**：SprintAttack / DodgeAttack 由 `GameplayIntentProfile` 条件映射产生；闪避攻击使用 `IsDodging + Attack Pressed`，禁止在 Driver 中按键名特判
- **选招层**：`ActionResolverService` → 当前模式 `ActionGraph`（多 Entry × Intent 起手 + Cancel 边）；`DirectionalActionResolver` 可作为节点 `VariantResolver`
- **节点 Intent**：`GameplayIntentType` 只保存在 `ActionGraphNode.Intent`；`ActionDefinition` 禁止声明输入或选招字段
- **连招图**：一张 `ActionGraph` 可同时含攻击/闪避等多个 Entry；边按 `CancelWindowType.Normal / Perfect` 解析，重复的「同来源 Intent + 同类型 + 同意图」使用 `ActionGraphSharedRoute`
- **流程唯一真源**：自动衔接、是否消费 SelectedTarget 和起手副作用在 `ActionGraphNode`；目标选择唯一归 `CharacterTargetingState`，禁止节点/ActionSim/Presentation 复制目标生命周期
- **Graph 策略编辑**：普通节点与顺序组子节点都在 Graph 节点内部展开策略编辑；新增节点必须显式清空数组槽继承的旧策略
- **顺序组**：组内 Action 按行顺序自动生成 Normal Cancel 链；每行保留独立 In；组级 Normal / Perfect 出口分别展开到配置对应窗口的全部子节点
- **变体节点**：Directional 等 Resolver 只改变实际播放 Action，不改变逻辑 Graph 节点；同语义六向变体禁止复制节点和出边
- **线性连招**：`ComboActionResolver` / `ComboLeafPolicy` 已删除；`ActionGraph` 是唯一连招拓扑真源
- **战斗模式**：`CombatModeProfile` + `CombatModeService`；**mode 直挂 `ActionGraph`**（已删除 PlayerActionSet 壳）
- **缓冲**：招式中 `Buffer(GameplayIntentType)`；`ActionSim` 经 `IActionInputBuffer` 在 `CancelWindow` 内消费
- **Locomotion 边界**：连续 Move 不枚举化；Action→Locomotion 特殊恢复使用一次性 `LocomotionResumeRequest`
- **后摇窗口**：Timeline 的 `ActionPhaseNotifyState(Recovery)` 同时配置 `allowMovementCancel` 与 `allowEntryRestart`；禁止创建 Recovery CancelWindow、独立 phases 或回根显式边
- **CancelWindow**：每个 Action 必须且只能有一个 Normal，可选一个 Perfect；两个窗口可重叠，同一 Intent 始终优先 Perfect；禁止重新引入分割帧、槽 Id 或同类型多窗口
- 其它系统不直接读 `InputReader` 做玩法判断（移动执行在 `CharacterMotor` / State）
- **玩家装配**：`InputActionAsset` / `GameplayIntentProfile` 均为项目全局（`GameInputSettings` / `GameplayIntentSettings`）；不在 Prefab / CharacterConfig 重复配置
- **Locomotion 单一挂点**：`CharacterLocomotionProfile` 内含 `AnimationProfile` + 相位/落脚/烘焙；仅 `CombatModeEntry` 挂 Loco；`CharacterConfig` 不配 Locomotion
- **敌人木桩**：`EnemyBrainProfile.enableCombatActions = false` 关闭追打，保留受击/死亡；不以空 Graph / aggro=0 定义木桩
- **AI 命令**：BT Task 只写黑板；Brain 写通用 `LocomotionDesireBuffer` / `ActionEntryRequestBuffer`；Character / Combat 仅依赖只读接口；**禁止**节点 `TryStart` / 改 Numeric
- **AI 招式冷却**：`CooldownGate` 持有成功 CD id/frames 并暂存；Brain 只确认/丢弃暂存，失败写独立 `action_entry_retry`；禁止 Brain 与节点写同一成功 CD id
- **AI 攻击拓扑**：`WaitWhileInAction` 必须放在 `IsCharacterState(Locomotion)` 子树外；禁止用运行时 fallback 掩盖错误树，交由 Validator 阻止保存
- **分层门禁**：`Domain/Character` 与 `Domain/Combat` 禁止引用 `Domain/Enemy` 声明的具体类型；由 `EnemyActorFactory` 在构造阶段注入接口，禁止恢复两阶段 Enemy Bind
- **输入计时**：Hold、Action Buffer 与 AI 攻击/重试/刷新冷却只使用整数逻辑帧；禁止重新引入秒制输入 TTL
- **AI 移动**：`LocomotionDesire`（本地轴意图 + FaceTarget）通过 `IMoveIntentSource` 直接喂 Locomotion；敌人 `InputFrame.move` 必须保持非权威

## 伤害与受击约定

- 伤害、`HitReactionId`、镜头震动与卡肉统一由 `HitboxNotifyState.Payload` 持有；反馈系统禁止从 `ActionDefinition` 反查
- 命中去重身份使用 ActionTimeline 中的 Hitbox 窗口下标；同一窗口对同一目标一次，不以可重复的显示字符串作为运行时键
- 命中目标身份必须是 `SimActorId`；排序使用 `SimHitKey(frame, attackerId, actionInstanceId, hitboxIndex, targetId)`，禁止 `GetInstanceID()` 进入去重、Snapshot 或 Hash
- 受击反应由“有效命中”驱动，不得依赖最终伤害必须大于 0；扣血与 Hit 状态是两条职责
- 玩家和敌人都使用 `CharacterHurtboxTarget` 注册到 `TargetSystem`；HitDetector 在几何检测前排除自身、同阵营与死亡目标
- 自身过滤必须覆盖角色根与全部父子层级，禁止模型子节点上的 Hurtbox 生成自击事件
- 玩家阵营由 `CharacterConfig.Combat.teamId` 持有；敌人阵营由 `EnemyDefinition.teamId` 独立持有，禁止从可复用身体配置隐式继承
- 受击/死亡映射统一由 `CharacterConfig.Combat.Reactions` 持有；`EnemyDefinition` 禁止重复声明 Action 引用
- `CharacterReactionService` 是玩家/敌人 Health 事件到 Actor 的唯一桥接；Controller 只负责构造并注入 Resolver，禁止各自重复订阅受击/死亡事件
- `CharacterReactionResolver` 直接产出 `CharacterReactionRequest`；默认硬直时长只保存在 `CharacterReactionSet`，State、BrainProfile 与 ActionDefinition 禁止重复配置
- 每次非致命有效命中都可强制重入 HitState；死亡状态是唯一不可被后续受击覆盖的反应终态
- Hit 无反应动作时只使用 `DurationFrames`；Hit/Death 有动作时按稳定 Action Instance 结束标记收尾，禁止读取动画播放状态或秒倒计时
- 死亡产生于统一 Combat Resolve；PostCombat 后的 Commit 先注销 `TargetSystem / CombatActorSystem`，死亡表现完成后再 Despawn
- 静态测试木桩若未注册 SimulationWorld，不得进入权威命中；需要可攻击目标时必须提供正式 `SimActorId`
- 卡肉 Event 仅允许冻结动画/VFX 表现；逻辑冻结必须由后续 Sim `freezeFrames` 实现，禁止表现计时回写动作会话

## 相机约定

- 运行时由 CameraManager 搭建层级（CameraRoot → Orbit → Pitch）
- `CameraRig` 是 FollowAnchor / FollowHold / Orbit/Pitch 的唯一写入器；`CameraManager` 只管 Look、装配与 staged yaw
- Orbit 用 SmoothDamp 追 CameraRoot；VCam Follow/LookAt 走平滑 Orbit（非直接硬锁角色 Transform）
- 灵敏度、Clamp 等 tunable 值 SerializeField 暴露
- `CameraLockEnabled` 只由 `CameraDirector` 持有；通过 `ILocalCameraTargetSource` 只读 SelectedTarget，范围内无目标时不得开启
- 招式镜头唯一数据真源为 `ActionDefinition.timeline.cameraShotStates`；禁止新增 `CameraShotSequence` / Preset SO 双入口
- `CameraShotPlayer` 只读 `ActionSim.CurrentFrame`；Camera 窗不得进入 `ActionTimeline.EnumerateStates()` 或 Sim Runner
- SkillShot 位置唯一来自内嵌官方 `UnityEngine.Splines.Spline`；禁止恢复 `localOffset` / Dolly / authored VCamKey 双轨
- 常规路径优先使用 `CameraSplineCurveRule` 的 Linear/上下左右 Arc，只编辑首尾端点；任意中间 Knot/Tangent 只允许在 Custom 规则下作者化
- 项目保持 Cinemachine 2；Action 不引用 CM3 `CinemachineSplineDolly` 或场景 `SplineContainer`，Director 内部固定 A/B VCam
- 模型部位只经 `CameraTransformBinding.AnchorId` + `CameraAnchorProvider` 解析；空 Id 为 Root，禁止恢复 Face/Chest 固定枚举或失败回退
- Scene 预览与 Runtime 必须共用 `CameraSplineEvaluator` / `CameraShotPoseResolver`；Handles 只写当前 Camera 窗的 Knot/Tangent
- SceneView 快速取景把 Scene Camera 世界位置反解到选中 Knot，并用其 forward 更新 `lookAtLocalPosition`；它不得改写 LookAt Binding 的 Source/Space/AnchorId，也不得把 Knot Rotation 重新解释为最终相机朝向
- Scene 默认显示 Spline/Knot/Tangent 与当前帧视锥，并始终保留自由导航；实际构图只通过独立 `ActionEditorCameraView` 的隐藏 Camera + RenderTexture 预览，禁止恢复 `SceneView.LookAtDirect` 接管或常驻 Anchor Gizmo

## 服务器 / 权威进程

权威进程 = 独跑 `SimulationWorld` 的 `DedicatedServerRuntime`（Listen 只组合同一 Runtime + 本机 `LocalClientRuntime`），不是另一套战斗工程。对照：Valve Source `game/shared` + `CUserCmd`、守望 command frame、Unity Netcode Entities Ghost、以及 `D:\Projects\DemoServer` 的 Handler/Room。细则与「学 / 不学」表见方案 §13。

### 共享模拟核

- **一份战斗逻辑**：`ACTGame.Simulation`（`Assets/Scripts/Domain/Simulation/ACTGame.Simulation.asmdef`，`noEngineReferences`）等价 Source 的 `game/shared`。Listen 权威 Guest、Dedicated、客户端预测位移必须调用同一 `CharacterMotorSim` / `ActionSim`，禁止 `ServerMotor` / `ClientMotor` 双份
- **输入即命令**：`InputFrame` 等价 Source `CUserCmd`（`game/shared/usercmd.h`）。权威 `InputFrameBuffer.SetRemote` 后 `SimulationWorld.Step`；禁止把「放 LightAttack1」RPC 当战斗上行
- **状态下行**：`ActCharacterSnapshotSchema.Capture` + `ReplicationSnapshotBuilder` 从权威 Actor 填 `ActorReplicationSnapshot`；客户端 Owner 纠偏、RemoteProxy 插值；禁止恢复已删除的独立 `CharacterReplicationCapture`，也禁止广播全员输入让各端重演
- **命中只在权威**：`CombatHitPipeline` 仅 Authority 装配 Collect。客户端刀光不得改 Numeric / Vitality

### 两套管线（不可混）

| 管线 | 频率 | 载荷 | 写法 |
|------|------|------|------|
| **战斗流** | 逻辑帧 | `ClientCommand` ↔ `ReplicationFrame` | Authority 收命令、Step、按连接构帧；无逐技能 Handler |
| **元数据 RPC** | 点一次 | 登录、背包、领奖（NS5 后另开） | 可学 DemoServer `Req*_Handler` / `RpcHandler`；**禁止**改 HP、硬直、`ActionSim` |

### Handler 与房间（学 DemoServer 的壳，不学它的战斗权威）

- Handler 只做：Session → `playerId`、校验房间、把 `InputFrame` 入队或调用元数据 Service。示例对照：`DemoServer/ZZZServer/Handler/Account/ReqLogin_Handler.cs`（薄 Handler）
- Handler **禁止**：`MotorSim.Step`、`ActionSim.Step`、`CombatHitPipeline.Collect`、写 HP、写世界坐标
- 房间时钟 = `SimulationWorld.Step`（60Hz 整数帧），不是 `Room.Update(float dt)` 里手写怪 AI。AI 仍走权威端 `EnemyBrain`（`Assets/Scripts/Domain/Enemy/EnemyBrain.cs`）
- 下行走 `ReplicationFrame`；命中走可靠事件通道，HP 在 Character Snapshot 里。不要为攻击者单独发「你打中了」作为唯一血量通道
- 复制 Update 默认可跳过未变 payload；敌人按兴趣半径裁剪；Owner 优先占预算。禁止再每 Tick 全量广播
- Graph 节点线上只发 `GraphNodeKey` 整数，禁止再写 UTF-8 节点名
- 复制帧被拒绝时请求 `ReplicationRecover` 并重置 Client Registry，禁止直接结束房间
- 远端动画同键只 Tick、禁止每份快照 Seek。Observer 用 `TickAnimation(deltaTime)` 连续走表；快照 `simulationTicks=0`，禁止隔步一次 `Tick(n/60)`
- Observer 播放头只插值模型锚点。判定盒、受击、VFX/SFX Notify 在快照到达时立即 `ApplySnapshot`，禁止等播放头
- 出招或 Vitality 边沿必须 `Urgent`，不受 SnapshotInterval 节拍限制
- Observer 播放头用 `RemotePlaybackClock` 按真实时间单调推进，钳在 `latest-delay`。禁止再用会每逻辑步清零的 `InterpolationAlpha` 当远端取样比例

### 线程与入队

- 网络收包线程只入队 `ClientCommand`；`SimulationWorld.Step` 与 Snapshot 打包在**同一模拟线程**
- 断线回调不得直接改 World；入队到权威帧再剔除（对照 DemoServer `NetMsgProcessor.EnqueueTask`，语义保留、实现自有）
- Listen 本机与远端 Client 都走 Owner 预测；权威只在 Headless Guest / 敌人

### 身份与配置

- 战斗身份只有 `SimActorId`。进房时 `playerId` → Actor 映射一次；禁止 `GetInstanceID()`、Uid 字符串进 HitKey / Snapshot 排序
- 关卡与招式配置：权威仍读现有 ScriptableObject。Dedicated 无头进程另开数据烘焙，不在战斗流里用 Mongo 玩家档当 Motor 状态（DemoServer `Player` 混持久化与 `NSync` 的做法不搬进 PVE 房间）

### 目录（实现时必须落这里）

```
Domain/Simulation/Replication/   # Snapshot / Tick / Command / PoseApplier，无 Unity
Domain/Simulation/Prediction/    # ActCharacterPredictionModel / Driver，无 Unity
Domain/Character/Replication/    # Catalog / Capture / RemoteProxy（有 Unity，无 Collect）
Domain/Net/                      # ACTGame 身份/内容 Adapter；不得重新放通用 Transport
Domain/Networking/               # Character Schema / Archetype Catalog；纯 C#，依赖 Simulation + ACTNet
Framework/ACTNet/Core/           # 零依赖 Id / Tick / Version / Result / Metrics / 有界小端 Buffer
Framework/ACTNet/Transport/      # INetTransport / Udp / Loopback / ChannelMux / MTU；只引用 Core
Framework/ACTNet/Prediction/     # Coordinator / Timeline / Clock；只引用 Core；禁止 ACT 类型
Framework/ACTNet/Session/        # ServerSession / ClientSession / Registry / SessionCodec；纯 C#
Framework/ACTNet/Replication/    # Frame / Schema / Entity Registry / Server / Client；只引用 Core
Infrastructure/Net/              # 预留 Unity Transport；不得被 ACTGame.Simulation 引用
App/Controllers/Gameplay/        # ListenServerBootstrap / ReplicationRoomClient / RemotePlayerSeat
App/Networking/Services/         # ACT 内容扫描、LocalClient / Authority World 编排；Facade 不实现具体 Gameplay
App/Server/                      # Dedicated 独立运行时（ACTGame.Server）；禁止引用 HUD/Input/Camera/Listen Facade
```

- **Dedicated 不是 Listen 开关**：禁止恢复 `ReplicationRoomHost` 或在 Runtime 上堆 `if Dedicated`。Listen 由 `ListenServerBootstrap` 组合同一 `DedicatedServerRuntime`；Dedicated 由 `DedicatedServerBootstrap` 单独启动
- **N 玩家身份**：`MatchCoordinator` 分配 PlayerId / EntityId / Spawn；删除固定 `GuestPlayerId=2` 与 Host Root +2m 出生
- **JoinAccept 无房主**：Dedicated 的 `AuthorityEntityId` 为 Invalid；客户端不得依赖该字段入房
- **Headless 装配**：权威无头走 `CharacterPresentationMode.AuthorityHeadless` + `NullAnimationPlayback`；禁止第二套 Dedicated Actor 工厂。`CharacterAnimationService.Play` 在无 Graph 时仍必须记下 `CurrentKey`，供 Capture 复制走跑相位；禁止因 `IsValid==false` 丢掉逻辑键导致远端只平移
- **Dedicated / Listen 命令合并**：冗余批 `MergeSample` 进下一权威帧，保证 Attack 边沿到包即起手。`LastApplied` 用 newest 去重；下行 `appliedHint` 必须用本批**第一条**新 Hint，禁止用 newest 对压缩后的权威步和解（否则闪避/吸附超 2m 硬吸）
- **修正位移纠偏**：本机或权威处于 Dodge / 吸附/关碰撞招 / 烘焙位移时，`ActionMotionReconcileGate` 整段推迟 2m 硬吸；权威 `ActionId==0` 也不得 `StopAutonomousAction` 这些招
- **外部时钟**：Dedicated 设 `SimulationHost.DriveFromExternalClock`，由 `ServerSimulationRunner` 调 `StepOnce`；禁止再写一套 Step 顺序
- **Gameplay 指纹**：`ServerContentManifest` 只哈希版本 / 碰撞 Id / Archetype / Action Id；VFX 名不进指纹
- **Dedicated 构帧**：`DedicatedAuthorityWorld` 在 `AfterLogicStep` 按连接 `ReplicationServer.BuildFrame`；Runtime 只发送。禁止再写一套 Host Room 构帧
- **MatchEnd**：应用消息类型 8（避开 Session Kick=7）；先 Drain 再 Sync Session，避免 Kick 丢掉同拍 MatchEnd
- **JoinAccept 实体**：Dedicated 必须写 World `SimulationId`，禁止只回 Match 槽位占位 Id
- **启动覆盖**：Dedicated 只认 `ServerLaunchConfigResolver`，优先级 CLI > Env > File > Default；密钥键忽略且不得打进日志
- **进程退出**：玩家 Dedicated 构建可 `ExitOnMatchEnd` / 空 Lobby 超时后 `Application.Quit(0)`；**Editor 不得 Quit**，且强制 `ExitOnMatchEnd=false`，空房后回 Lobby 可再入
- **READY**：监听成功只打 `DedicatedServerBootstrap: READY port=… role=DedicatedServer。`，烟测按此匹配

### 明确不搬进本项目的 DemoServer 战斗写法

| DemoServer | 路径 | 本项目 |
|------------|------|--------|
| 客户端上报位姿 | `Player.UpdateFromAutonomous` | 禁止；位姿由权威 MotorSim 算 |
| 客户端上报伤害 | `MsgMonsterTakeDamage_Handler` | 禁止；只认权威逻辑盒 |
| 技能名字符串 RPC | `MsgRolePlayAction` | 禁止；用 InputFrame 边沿 + 权威 ActionId/帧 |
| 秒制 Room.Update | `Room.Update(float dt)` | 禁止作为战斗权威时钟 |

## 注释与语言

- 用户可见 Debug 信息可用中文
- 代码标识符英文
- **Agent 新增/改动代码**：须满足 `.cursor/rules/lint-after-code-changes.mdc`（类型顶、public/protected 成员、非 obvious 逻辑均要有注释）
- **人工维护的旧代码**：仅在 non-obvious 逻辑处补简短中文/英文注释即可
- 不写「此文件由 AI 生成」类元注释

## 禁止 / 避免

| 避免 | 原因 |
|------|------|
| Agent 修改美术资产、ScriptableObject 或 Prefab（`Assets/Art/**`、`Assets/Data/**`、`Assets/Prefabs/**`、`.asset`、`.prefab` 等） | 由用户在 Unity Editor 中人工操作；**例外**：Agent 可创建/编写 Shader 源码（`.shader` 等）；见 `no-art-asset-edits.mdc` |
| 改代码后不跑 linter、带着相关报错结束任务 | 必须工具调用 `ReadLints` 并在回复附「代码收尾」清单；见 `lint-after-code-changes.mdc` |
| 改代码后不补充注释就结束任务 | 须满足该规则中的最低注释要求 + 「代码收尾」清单；见 `lint-after-code-changes.mdc` |
| 声称「已完成」但未附「代码收尾」清单 | 有代码改动时清单为强制项；见 `lint-after-code-changes.mdc` |
| 重写逻辑/重构框架时保留旧兼容层（Legacy/Fallback 双轨） | 默认直接切新并删除旧路径；仅用户明确要求才允许阶段迁移；见 `no-legacy-compatibility.mdc` |
| Core → Character/Player 引用 | 破坏分层 |
| 在 State 里直接读 InputReader | 应经 Context 快照，便于 AI/回放测试 |
| 静态 Service Locator 式 Input/Singleton | 与当前组件化方向不一致 |
| 在 Controller 与 State 重复同一业务判断 | 职责漂移；选一处为 source of truth |
| 大范围命名空间重构（未经计划） | 全项目 diff 噪声 |
| 为单个角色运行时业务堆多个必须手工挂载的脚本 | 优先用 `CharacterConfig` + Bootstrap / Facade 装配，跨角色逻辑迁到 System |
| 在 `PlayerController.Awake` 中为业务类 `AddComponent` | 这只是把 Prefab 堆脚本改成运行时堆脚本，必须改为纯 C# service |
| 新增 static event / static registry 作为跨系统业务入口 | 破坏 QFramework 风格架构边界；应使用 Command / Event / System |
| 在 `Domain/**` 中直接调用 `ACTGameArchitecture.Interface` | 破坏下层不依赖上层的分层规则；应由 App 层通过 Query、Command 或构造注入提供依赖 |
| 为 Dedicated 重写一套 Motor/Action/Hit 逻辑 | 必须复用 `ACTGame.Simulation`；Host/DS 只换宿主与传输 |
| 战斗上行用技能名 / 伤害 / 世界坐标 RPC | 破坏 InputFrame 命令流与权威 Hitbox；元数据 RPC 另管线 |
| 客户端 `HitboxFrameConsumer` 写入 `CombatHitPipeline` | 多端各算各的刀；Collect 仅 Authority |
| 传输实现引用进 `ACTGame.Simulation` | Snapshot 契约必须无引擎、可单测；UDP 放 `Infrastructure/Net/` |

## 已废弃模式

（暂无；出现旧模式迁移后在此记录）
