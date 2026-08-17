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
- **本机玩家入口**：玩法与相机通过 `LocalPlayerService` / `GetLocalPlayerQuery` / `GetPlayerRootsQuery` 取玩家；禁止 `FindObjectOfType<PlayerController>()`（仅 Editor Gizmo 可留）。Listen Host 的 `IsLocalPredicted` 恒为 false
- **客机相机跟朝向**：`CameraManager.ApplyFollowFacingYaw` 必须读 `ILocalPlayer.HasMoveIntent` 与 `PresentationRoot`。客机有 Autonomous `CharacterActor`，`PresentationRoot` 来自 Actor。他人/敌人 `RemoteCharacterProxy` 只读登记 `TargetSystem`；`OnHit` 空操作，禁止 Collect
- **朝向调试箭头**：`CharacterFacingDebugVisualizer` 只绑 `ICharacterFacingDebugTarget`（本机 Actor 或 RemoteProxy）；禁止再 Bind `PlayerController`。幽灵 wish 必须与对应 Tick 成对，禁止用当前帧本机输入画延迟模型
- **复制契约**：当前上下行结构体与 Codec 仍在 `ACTGame.Simulation/Replication`，但小端字节读写唯一复用零依赖 `ACTNet.Core.NetBufferReader/Writer`；Codec 禁止再内置私有 Reader/Writer。稳定网络身份使用 `Net*Id`，`SimActorId ↔ NetEntityId` 只在 ACT Adapter 显式映射。传输唯一入口为 `ACTNet.Transport.INetTransport`，必须按 `NetConnectionId` 定向 `Send`；禁止恢复 Client→Authority / Authority→Clients 方向固化接口。禁止把 CameraLock/Look/Lean 写入 Snapshot；禁止 ClientCommand 带 HP/坐标/招式名。房间上行是最近 N 条命令批；Host 只合并未应用 Hint 的边沿。`appliedClientFrameHint` 仅本步真正灌入远端命令时非 0，CarryForward 必须下发 0。装配用 `ReplicationSeat`，禁止 `if (isClient)` 开第二套 Actor
- **RemoteProxy**：他人/敌人幽灵只应用 Snapshot（`Domain/Character/Replication/`），禁止 `CharacterActorFactory`、`HitboxFrameConsumer`、`EnemyBrain.Step`。可按 ActionFrame 过点派发 VFX/SFX，禁止派发 Hitbox/MotionCommand。Host 用 `AfterLogicStep` 打包，不得只在渲染帧漏步发送。禁止再挂 Host 同机 ±2m 预览（`RemoteGhostViewController` / `PredictedClientPreviewController` 已删）
- **幽灵 Locomotion（他人）**：切 `AnimationKey` 时一次性相位硬切并可 Seek；Idle↔走跑冲刺用 Profile 默认 CrossFade。同键只 `Tick`
- **客机本机走跑**：真源 [`docs/2026.8.15/UNIFIED_CHARACTER_ACTOR_SEAT_PLAN.md`](../../docs/2026.8.15/UNIFIED_CHARACTER_ACTOR_SEAT_PLAN.md)。本机 Autonomous `CharacterActor` 跑同一套 `LocomotionStateMachine`；纠偏 Restore+Replay；他人仍 Snapshot。禁止猜片 / 摇杆硬映射 Idle/Walk/Run。已废止 Runner/CreateAutonomous
- **客机表现节拍**：本机 Clip/VFX 由 `CharacterActor.Step` + `CharacterActionPresentationBridge` 推进。禁止对自角色 `ApplySnapshot` Seek。权威 Tick 只更新纠偏与 HP/受击边沿。禁止同一逻辑帧 Tick 两次 Clip。走跑 Replay 外禁止每帧 `SyncRootPoseFromSim`（会清零转向阻尼）
- **复制目录 Prefill**：必须收录 Graph 节点 `Action` 与 `VariantResolver` 变体（六向闪避）。只预填 `node.Action` 时客机侧/后闪 `TryGet` 失败，只有位移没有 Clip
- **预测位移**：`PredictedLocomotionDriver` 只做 SavedMove 队列与纠偏编排。走跑带 `IPredictedLocomotionReplay` 时默认硬吸阈 `AutonomousHardSnapMm`（2m），禁止房间再传 50mm：内层机与 Host 常态偏差就会每包 Restore+Replay+表现硬切，客机走跑卡顿。无 replay 的旧 Predict 单测仍用 50mm。超 2m：`RestoreFromAuthority` + `ReplayTick`，禁止对走跑步 `ApplyInput`。Listen Host 本地禁止把预测写进 `CharacterActor.Step`。纠偏可改预测电机，禁止把表现 Pose 写回权威 Motor。Lean 不进 Snapshot
- **纠偏后表现**：仅走跑真正 `Snapped`（≥ 2m）或权威 **Hit/Death** 才 `SnapPresentationToSimulation`。出招/闪避禁止每包硬切表现，否则插值被掐死、位移和相机一起跳。刚吸附后 8 包内 ≤ 150mm 只 Ack
- **出招中相机**：`CameraManager` 在 `ILocalPlayer.IsPresentingAction` 时暂停 L-DIR5 跟朝向，避免连闪 yaw 追权威朝向台阶
- **闪避回走跑**：Host `ActionState.Exit` 写 `SprintAfterDodge`；客机 Runner 再 Enter 必须 `LocomotionResumeRequest.AfterAction`，禁止 `Enter(default)` 从 Idle 重计 Sprint
- **预测出招**：客机本机 Autonomous `CharacterActor` 跑 `ActionSim` + 表现桥（Graph 起手 + Cancel 窗 + 推帧 + 烘焙位移 + Adhesion/Relocate）。禁止 Collect、进 World。Adhesion 读只读 Proxy 逻辑 Pose。卡肉由 `PredictedHitStopConsumer` 几何重叠后 `RequestHitStop`，禁止用延迟权威 `FreezeFrames` 再拖本机时钟。伤害只信权威下行。Ack 用 `PredictedActionAckQueue`：同招不 Seek 回旧帧；权威 `ActionId==0` 或 Hit/Death 则 `StopAutonomousAction`。本机已连到下一招、权威还停在上一招：只 Ack，不 Cancel。穿敌吸附 / SoftBodySuppress 窗或权威卡肉：`ActionMotionReconcileGate` 禁止 2m 硬吸位姿。Listen Host 本地仍不预测出招
- **命中复制**：`CombatHitPipeline` 只在权威 Actor 收集；下行 `ReplicatedHitEvent` + `VitalityReplicationEdge`。幽灵/预测不得再跑命中或扣血。边沿由 `CharacterVitality` 记一帧，`CharacterActor.Step` 开头清空
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
- 联网（定案，2026-08-13；规范补全 2026-08-14）：组队 PVE 为 Host/DS 权威状态同步；上行量化 `InputFrame`，下行 `ActorReplicationSnapshot`；命中只在权威 `CombatHitPipeline`。禁止全端同构输入广播作为产品主路径，禁止客户端上报伤害结果，禁止以齐帧停等作为手感模型。锁步 L0～L2 模拟核仍适用。服务器写法见下方「服务器 / 权威进程」。真源：`docs/2026.8.13/TEAM_PVE_NARAKA_STYLE_STATE_SYNC_PLAN.md`
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
- Orbit 用 SmoothDamp 追 CameraRoot；VCam Follow/LookAt 走平滑 Orbit（非直接硬锁角色 Transform）
- 灵敏度、Clamp 等 tunable 值 SerializeField 暴露
- `CameraLockEnabled` 只属于本地表现；通过 `ILocalCameraTargetSource` 只读 SelectedTarget，范围内无目标时不得开启

## 服务器 / 权威进程

权威进程 = 独跑 `SimulationWorld` 的 Listen Host 或日后 Dedicated，不是另一套战斗工程。对照：Valve Source `game/shared` + `CUserCmd`、守望 command frame、Unity Netcode Entities Ghost、以及 `D:\Projects\DemoServer` 的 Handler/Room。细则与「学 / 不学」表见方案 §13。

### 共享模拟核

- **一份战斗逻辑**：`ACTGame.Simulation`（`Assets/Scripts/Domain/Simulation/ACTGame.Simulation.asmdef`，`noEngineReferences`）等价 Source 的 `game/shared`。Listen Host、Dedicated、客户端预测位移必须调用同一 `CharacterMotorSim` / `ActionSim`，禁止 `ServerMotor` / `ClientMotor` 双份
- **输入即命令**：`InputFrame` 等价 Source `CUserCmd`（`game/shared/usercmd.h`）。权威 `InputFrameBuffer.SetRemote` 后 `SimulationWorld.Step`；禁止把「放 LightAttack1」RPC 当战斗上行
- **状态下行**：`CharacterReplicationCapture` + `ReplicationSnapshotBuilder` 从权威 Actor 填 `ActorReplicationSnapshot`（`Domain/Simulation/Replication/`）。客户端 PredictedLocal 纠偏、RemoteProxy 插值；禁止再广播全员输入让各端重演
- **命中只在权威**：`CombatHitPipeline` 仅 Authority 装配 Collect。客户端刀光不得改 Numeric / Vitality

### 两套管线（不可混）

| 管线 | 频率 | 载荷 | 写法 |
|------|------|------|------|
| **战斗流** | 逻辑帧 | `ClientCommand` ↔ `AuthorityTick` | `ReplicationAuthority` 收命令、Step、打包；无逐技能 Handler |
| **元数据 RPC** | 点一次 | 登录、背包、领奖（NS5 后另开） | 可学 DemoServer `Req*_Handler` / `RpcHandler`；**禁止**改 HP、硬直、`ActionSim` |

### Handler 与房间（学 DemoServer 的壳，不学它的战斗权威）

- Handler 只做：Session → `playerId`、校验房间、把 `InputFrame` 入队或调用元数据 Service。示例对照：`DemoServer/ZZZServer/Handler/Account/ReqLogin_Handler.cs`（薄 Handler）
- Handler **禁止**：`MotorSim.Step`、`ActionSim.Step`、`CombatHitPipeline.Collect`、写 HP、写世界坐标
- 房间时钟 = `SimulationWorld.Step`（60Hz 整数帧），不是 `Room.Update(float dt)` 里手写怪 AI。AI 仍走权威端 `EnemyBrain`（`Assets/Scripts/Domain/Enemy/EnemyBrain.cs`）
- 广播走 `AuthorityTick` 全员同一份；不要为攻击者单独发「你打中了」作为唯一血量通道。`hits[]` 只补边沿，HP 在 Snapshot 里

### 线程与入队

- 网络收包线程只入队 `ClientCommand`；`SimulationWorld.Step` 与 Snapshot 打包在**同一模拟线程**
- 断线回调不得直接改 World；入队到权威帧再剔除（对照 DemoServer `NetMsgProcessor.EnqueueTask`，语义保留、实现自有）
- Listen Host 本地玩家不预测，直接吃权威（0 RTT）；远端客户端才 PredictedLocal

### 身份与配置

- 战斗身份只有 `SimActorId`。进房时 `playerId` → Actor 映射一次；禁止 `GetInstanceID()`、Uid 字符串进 HitKey / Snapshot 排序
- 关卡与招式配置：Host 仍读现有 ScriptableObject。Dedicated 无头进程另开数据烘焙，不在战斗流里用 Mongo 玩家档当 Motor 状态（DemoServer `Player` 混持久化与 `NSync` 的做法不搬进 PVE 房间）

### 目录（实现时必须落这里）

```
Domain/Simulation/Replication/   # Snapshot / Tick / Command / PoseApplier，无 Unity
Domain/Simulation/Prediction/    # PredictedLocomotionDriver，无 Unity
Domain/Character/Replication/    # Catalog / Capture / RemoteProxy（有 Unity，无 Collect）
Domain/Net/                      # ACTGame 身份/内容 Adapter；不得重新放通用 Transport
Framework/ACTNet/Core/           # 零依赖 Id / Tick / Version / Result / Metrics / 有界小端 Buffer
Framework/ACTNet/Transport/      # INetTransport / LoopbackTransport / UdpTransport；只引用 Core
Infrastructure/Net/              # 预留 Unity Transport；不得被 ACTGame.Simulation 引用
App/Controllers/Gameplay/        # ReplicationRoomHost / Client / RemotePlayerSeat
```

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
