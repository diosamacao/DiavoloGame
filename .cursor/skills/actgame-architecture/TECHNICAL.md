# ACTGame 技术文档

> Last updated: 2026-09-03（受击档由冲击力对韧性裁定；已删 desiredReaction）
> 说明：记录**已实现功能**及其**实现方案**。架构分层见 [ARCHITECTURE.md](ARCHITECTURE.md)；编码约定见 [CONVENTIONS.md](CONVENTIONS.md)。

## 功能索引

| 功能 | 状态 | 入口 / 核心类 | 关键资源 |
|------|------|---------------|----------|
| 三人阵容 / 单键换人 | 🟡 P-SW1 运行时/权威代码完成，Editor 验收待办 | `PartyLoadout`、`PartyCombatCoordinator`、`ActGameGuest` | 空格已进 Input Actions；需各角色 Graph 配 `SwitchIn/SwitchOut` Entry |
| Wave4 位移（Adhesion / SoftBody / Relocate） | ✅ 已实现（吸附已验收；Relocate 已接线） | `ActionMotionAdhesion` + `ActionMotionResolver` + Bridge | Branch_02 吸附已配；Relocate 按需加 MotionCommand 轨；相机不在本 Wave |
| 命中受击 Cue（VFX/SFX） | ✅ 已实现（A2 打击感验收 2026-08-09） | `HitImpactController` + `HitFeedbackSettings` | 接触点落点 + 随机旋转；普攻 Cue 已验 |
| 逻辑 Hurtbox 调试线框 | ✅ 已实现 | `CombatHurtboxDebugSettings` + `CombatHurtboxDebugVisualizer` | F4 开关（F3 HUD 显示状态） |
| Playable Additive 探针 | 🟡 P-HR0 Play 已确认 Additive | `PlayableAnimationPlayback.PlayAdditive` + F6 | Listen 无头敌人打 Observer Proxy；HUD 拖 `Hit_Shake` |
| 受击档位裁定 | 🟡 冲击力对韧性已接；韧性/冲击数字待 Editor 填 | `CharacterReactionService` + `HitFlinchPlaybackController` | 杂兵韧性 1；精英 3；Attack01 冲击 2 |
| 固定帧模拟宿主 | ✅ L0A 已实现 | `SimulationHost`、`SimulationWorld`、`SimActorId` | 60Hz，无资产 |
| Wave0 动作审计 / 锚点可视化 / Debug HUD | ✅ 已实现 | `ActionDefinitionAuditUtility`、`CharacterAnchorGizmoDrawer`、`CombatDebugHudController` | 菜单 `ACTGame/Action/Validate Motion Sources`；场景挂 HUD |
| 角色朝向调试箭头 | ✅ Play 实心箭 | `CharacterFacingDebugVisualizer` + `ICharacterFacingDebugTarget` | 本体 / 客机他人幽灵各一份；黄=wish 品红=模型 |
| Wave1 位移止血 / BaseMotionMode / 相机滤左右 | ✅ 已实现 | `ForwardSigned`、`ActionBaseMotionMode`、`CameraManager.lateralFollowFactor` | Attack 需以 ForwardSigned 重烘焙；菜单 Migrate Base Motion Mode |
| Wave2 视觉残差 / VisualMotionRoot | ✅ 已实现（含 2.5） | `CharacterVisualMotionBridge`、`TryGetVisualResidualMm` | ForwardSigned：Motor 无横摆，模型在 VisualRoot 摆；BlendToZero 期间跳过逻辑贴帧，避免回 Idle 抖动 |
| Wave3 玩法资源 / 同键 EX | 🟡 资产待绑；运行时已迁 Numeric | `NumericCostGate`、`ActionResourceSpec`、`ActionEnergyFormSelector` | Spec 填表；Graph 双 Entry |
| GAS-lite 数值重构 | ✅ G0～G5 完成 | `NumericSystem`、`DamageNumericCalculator`、`CharacterVitality` | Effect SO 壳 |
| 完美闪避反击（Wave 3.4） | ✅ 代码路由完成 | `PerfectDodgeAttack`、Pipeline 武装、Begin 清缓冲 | Graph Counter Entry（Editor） |
| 第三人称移动 | ✅ 已实现 | `PlayerController` + `CharacterActor` + `CharacterConfig` | Scene Empty + CharacterConfig |
| 输入（量化帧 + 语义意图） | ✅ L0B + C-AT0 代码已实现 | `InputFrameBuffer`、`InputReader`、`InputManager`、`GameplayIntentProducer` | MoveReferenceYaw 已闭包；Input Actions 待人工绑 TargetSwitch |
| 组队 PVE 状态同步 / 权威进程 | 🟡 W11 代码切面 / Play 未验收 | `ReplicationBuildOptions` + `GraphNodeKey` + FakeActionGame | 先读 `docs/2026.8.23/NETSYNC_FROM_JOIN_TO_HIT.md`；W10 出口仍待 Clumsy Play；W11 R2 未关 |
| 敌人木桩 AI 开关 | ✅ 已实现并验收 | `EnemyBrainProfile.enableCombatActions` + `Monster_EDF` | 2026-08-08 Play：Hit_Shake / 高 HP / 不追打 |
| CombatMode→Graph | ✅ Phase B | `CombatModeEntry.actionGraph` / `ActiveGraph` | 已删 PlayerActionSet；Editor 迁移菜单 |
| 全局 Input + Locomotion 收敛 | ✅ B2/B3 | `GameInputSettings`；Mode→`LocomotionProfile`（内含 Anim） | Config 不再挂 Input/Locomotion |
| 状态机框架 | ✅ 已实现 | `StateMachine<,>`、`CharacterStateMachine` | — |
| 架构通信框架 | ✅ 已实现 | `ACTGameArchitecture`、`ArchitectureSystemBase`、`AppControllerBase`、Command / Query / Event | — |
| Locomotion 动画驱动 | ✅ 已实现 | `LocomotionStateMachine` + `LocomotionState` | AnimationProfile + `CharacterLocomotionProfile` |
| Locomotion 起步/急停/转身 | ✅ Play 2026-08-12 | 内层相位 + L-DIR1～5 + Pivot 两段式 | 旧 Phase D 减速曲线不做 |
| Sprint 倾身 / 相机跟朝向 | ✅ Play 2026-08-12 | `SprintLeanModel` + `CameraManager` Follow Facing | 出招时暂停跟朝向 |
| 第三人称相机 | 🟡 SkillShot Spline 代码完成，C1 构图待做 | `CameraManager` + `CameraDirector` | CM2 保留；Unity Splines 2.8.4；Editor/Play 待验 |
| 唯一战斗目标 | 🟡 代码完成、输入资产与 Play 待验 | `CharacterTargetingState` + `DeterministicTargetResolver` | 自动最近、滞回保持、Action 中 TargetSwitch |
| 动作系统（整数帧 / 选招 / 取消 / 连段 / 高优打断 / 战斗模式） | ✅ L1B 已实现（Play Mode 待回归） | `ActionSim` + `CharacterActionPresentationBridge` + `ActionFrameQuery` | 60Hz Action + `ActionGraph` |
| Action Editor（时间轴编辑） | 🟡 骨架/部分 | `ActionEditorWindow` + `ActionTimeline` 手动加轨/窗口 | Menu：`ACT/Action Editor` |
| 攻击 / 战斗判定 | ✅ L0C 延迟结算已实现 | `CombatHitPipeline` + `CombatDamageCalculator` + `CharacterReactionService` | SimHitKey；HitPayload；Hit/Death 状态 |
| 敌人 AI / 行为树 | ✅ 8.10 + 对峙已验收 | Runner + GraphEditor；Desire/Request；节点时间填秒 | 待优化：`docs/2026.8.11/ENEMY_BEHAVIOR_TREE_BACKLOG_PLAN.md` |
| UI | ⬜ 未实现 | — | `UI/` 占位 |

状态图例：✅ 可玩可用 · 🟡 有类/占位但未接完 · ⬜ 未开始

---

## 0.0 三人阵容 / 单键换人（P-SW0 + P-SW1）

### 功能说明

玩家座位由 `PartyLoadout` 声明最多三个角色；空格按槽序切到下一名可用角色。新角色落到旧角色局部右侧 0.6m，立即接管输入并请求 `SwitchIn`；旧角色空闲时立即播放 `SwitchOut`，已有 Action 时在首次 Recovery 停止原招并转入 `SwitchOut`，最终只在 `SwitchOut` 自身 Recovery 隐藏。

### 实现方案

| 项 | 方案 |
|----|------|
| 角色身份 | `CharacterId`；Ordinal 字符串值 |
| 角色定义 | `CharacterDefinition` 引用现有 `CharacterConfig`，并声明 `CharacterAssistStyle` |
| 阵容 | `PartyLoadout` 保存 1～3 个 Definition 引用和 `StartingSlot`；空槽合法、Id 不可重复 |
| 输入 | `InputButton.SwitchCharacter` 固定 bit 9；`InputReader` 可选采样同名 Input Action |
| 顺序选择 | `PartySlotSelector` 从 Active 后一槽正序绕回，只接受 `Inactive` |
| 普通切裁定 | `PartyCombatCoordinator.TryResolveSwitchIn` 输出 `DualPresence`，旧槽 Active→Exiting，新槽 Inactive→Active |
| 普通切落点 | `PartySwitchPlacement` 按旧角色 Motor 朝向取局部右侧 600mm；`CharacterActor.PlaceForNormalSwitchFrom` 从旧位置经新角色静态碰撞世界解析后落地 |
| 退场时序 | `CharacterActor.BeginPartyExit/AdvancePartyExitAfterPostCombat`：空闲立即注入 `SwitchOut`；切人输入到达时已在 Recovery 则在下一次 Action Step 前立即交接，否则到首次 Recovery 停止并排队 `SwitchOut`；`IsPartyExitReady` 只认 SwitchOut 实例的 Recovery |
| 运行时槽 | `PlayerController` / `ActGameGuest` 均按非空槽创建独立 `CharacterActor`；Inactive 空输入且不参与软碰撞/受击 |
| 稳定身份 | 每槽独立 `SimActorId` / `NetEntityId`；禁止单 Actor 热换 Config |
| Owner 复制 | V2 应用载荷下发槽 ActorId、ActiveSlot、累计命令 ACK；自有后台槽不会创建 Observer Proxy |
| 状态复制 | `PartyMemberState` 编入角色快照 `FlagsPacked` 低三位；Observer 仅显示 Active / Exiting |
| 内容预填 | Client / Server 登记 Loadout 全部角色 Archetype 与动作 |

### 运行时流程

```text
InputReader.Sample → InputFrame.SwitchCharacter
  → ClientCommand（仍是一座位一条输入流）
  → DedicatedAuthorityWorld 预合并未应用命令
  → ActGameGuest.TryResolveSwitch
      → from.BeginPartyExit；to = Active
      → from 空闲：QueueExternalIntent(SwitchOut)
      → from 有招：首次 Recovery 后 Stop 原招并 QueueExternalIntent(SwitchOut)
      → SwitchOut 首次 Recovery：CompletePartyExit
      → to 从旧槽逻辑根沿旧角色局部右向偏移 600mm（静态碰撞解析）
      → to 优先朝向 SelectedTarget
      → to.QueueExternalIntent(SwitchIn)
  → 输入写入新 Active ActorId
  → SimulationWorld.Step（Active + Exiting + Inactive 独立 Actor）
  → ReplicationFrame（全部槽快照 + Owner ActiveSlot）

客户端同帧：
ActClientRoomGameplay.StepPrediction
  → PlayerController.StepPartyPrediction
  → Active 收输入；Exiting/Inactive 收空输入
  → 累计 ACK 未覆盖预测切人帧时，不用旧快照撤销切人
```

### 已知限制

- `SwitchIn/SwitchOut` 只提供代码意图；每个角色 ActionGraph 仍需 Editor 人工配置同名 Entry/Action。
- 原 Action 没有 Recovery Phase 时，等其自然结束后再切 `SwitchOut`，避免在 Startup/Active 中硬掐。
- `SwitchOut` 必须配置 Recovery Phase；缺失时角色不会被静默隐藏，便于暴露资产错误。
- 本轮已通过解决方案编译；Unity Test Runner 与 Listen Play 尚未验收，因此功能状态仍为 🟡。
- P-SW2 金光、支援点、招架/回避支援尚未实现。

### 相关文件

- `Assets/Scripts/Domain/Party/*`
- `Assets/Scripts/Domain/Simulation/Party/*`
- `Assets/Scripts/App/Controllers/Gameplay/PlayerController.cs`
- `Assets/Scripts/App/Networking/Services/ActContentPrefillService.cs`
- `Assets/Scripts/App/Networking/Content/ActServerContentProbe.cs`
- `Assets/Scripts/App/Networking/Adapters/ActGameSessionHandler.cs`
- `Assets/Scripts/App/Networking/Services/DedicatedAuthorityWorld.cs`
- `Assets/Scripts/Domain/Networking/ActReplicationApplicationPayload*.cs`
- `Assets/Scripts/Domain/Simulation/Input/InputButton.cs`
- `Assets/Scripts/Infrastructure/Input/InputReader.cs`
- `docs/2026.8.30/PARTY_SWITCH_ASSIST_PLAN.md`

---

## 0. 架构通信框架

### 功能说明

参考 QFramework 的分层方式，项目通过 `ACTGameArchitecture` 统一管理跨系统通信；进入 IOC 的对象必须实现对应契约或基类。

### 实现方案

| 项 | 方案 |
|----|------|
| 架构入口 | `ACTGameArchitecture.Interface` 懒加载注册默认 System |
| System | `ArchitectureSystemBase` + `IArchitectureSystem`，通过 `RegisterSystem` 进入 IOC |
| Controller | `AppControllerBase` + `IArchitectureController`，Unity 表现入口通过能力方法访问架构层 |
| Command | `ArchitectureCommandBase` + `IArchitectureCommand`，表达一次会改变状态的业务行为 |
| Query | `ArchitectureQueryBase<TResult>` + `IArchitectureQuery<TResult>`，表达无副作用读取 |
| Event | `IArchitectureEvent` 标记接口，限制可分发事件类型 |
| Editor 校验 | `ArchitectureBoundaryValidator` 检查 App/Systems、App/Controllers、App/Events 与 Domain 单例访问 |

### 运行时流程

```
AppControllerBase
  → SendCommand / SendQuery / RegisterEvent
  → ACTGameArchitecture
      → ArchitectureSystemBase / ArchitectureCommandBase / ArchitectureQueryBase
      → IArchitectureEvent
```

### 已知限制

- `Domain/Simulation` 已拆为无 Unity 引用的 `ACTGame.Simulation` asmdef；其余业务仍处于 `Assembly-CSharp`。
- Model / Utility 容器已具备 API，但当前暂无业务 Model / Utility 注册。

### 相关文件

- `Assets/Scripts/App/Architecture/*`
- `Assets/Scripts/Editor/Architecture/ArchitectureBoundaryValidator.cs`

---

## 0.1 固定帧模拟宿主

### 功能说明

玩家与敌人不再由各自 Controller 的 `Update` 分散推进；场景唯一 `SimulationHost` 将渲染帧时间累积为 60Hz 固定逻辑帧，并由 `SimulationWorld` 按稳定 `SimActorId` 顺序 Step。

### 实现方案

| 项 | 方案 |
|----|------|
| Unity 入口 | `CombatWorldController` 自动确保同物体存在一个 `SimulationHost` |
| 固定频率 | `SimulationConfig.DefaultLogicHz = 60` |
| 追帧 | `FixedStepAccumulator` 单渲染帧最多 8 Step；超额欠账保留，不丢逻辑时间 |
| Actor 身份 | World 从 1 单调分配 `SimActorId`，会话内不复用 |
| Actor 顺序 | `CharacterActor` / `EnemyHandle` 实现 `ISimulationActor`，按注册 Id 升序执行 |
| 渲染输入 | `IRenderFrameSampler` 每渲染帧汇聚设备边沿；无逻辑 Step 时 Pressed/Released 保留到下一 Step |
| 输入帧 | `InputFrame` 使用 sbyte Move、MoveReferenceYaw、稳定按钮 bitset、frame 与 SimActorId；World 持有 `InputFrameBuffer` 历史 |
| 输入阶段 | 每帧先调用 `ISimulationInputProducer`；AI 基于 Actor Step 前的 N-1 已提交状态写 N 帧输入 |
| 命中阶段 | 全体 Actor 只 Collect；`CombatHitPipeline` 按 `SimHitKey` 排序后统一 Resolve |
| PostCombat | `ISimulationPostCombatActor` 在结算后处理 OnHitConfirm/OnWhiff 与自然结束 |
| Commit | 当前死亡目标注销与敌人 Despawn 固定在 Combat/PostCombat 后执行 |
| 表现插值 | 模型位于运行时 `CharacterPresentationRoot`；Host LateUpdate 按 accumulator alpha 插值前后逻辑 Pose |
| 相机跟随 | `CameraManager` 跟随玩家表现锚点，不直接追阶梯式权威 Transform |
| 生命周期 | Controller 在 OnEnable 注册、OnDisable/OnDestroy 注销；禁用对象不会继续模拟 |
| 测试 | `ACTGame.Simulation.EditModeTests` 覆盖 Id、accumulator/alpha、注册/注销、Step 与 Render 转发 |

### 运行时流程

```
SimulationHost.Update
  → SimulationWorld.SampleRenderFrame
      → InputReader 量化并合并到 CurrentFrame + 1
  → FixedStepAccumulator.ConsumeSteps(Time.deltaTime)
  → 重复 N 次 SimulationWorld.Step
      → ISimulationInputProducer.ProduceInput（AI）
      → InputFrameBuffer.ResolveLocal
      → CharacterActor.Step / EnemyHandle.Step（Control / Motion / Hit Collect）
  → CombatHitPipeline.ResolveBeforePostCombat（稳定排序、伤害、Reaction、ConfirmHit）
  → SimulationWorld.ResolvePostCombat（自动 Transition / 动作结束）
  → CombatHitPipeline.CompleteFrame（Transition frame 0 命中 + 只读 App 结果）
  → CommitEnemyLifecycle（死亡注销与 Despawn Command）
SimulationHost.LateUpdate
  → SimulationWorld.Render(alpha)
  → CharacterPresentationBridge 插值模型锚点
  → CameraManager.LateUpdate 跟随同一表现帧
```

### 已知限制

- L0B 已切换量化输入与整数帧 Hold/Buffer/AI 冷却；完整脱设备玩法回放仍需 Play Mode 确认。
- L0C 已删除同步 `ApplyHitCommand` 与 `GetInstanceID()` 去重；真实多命中、互杀及交换注册顺序仍需 Play Mode 验收。
- L1B：动作权威在纯 `ActionSim`；全部 ActionDefinition 已为 60Hz。剩余为 Play Mode / Test Runner 人工验收；Player 占位 Action 无动画段时 `IsSimulationReady=false`。
- L2/M0–M1：运动表烘焙 + 运行时查表。`bakeStatus=Ok` 时表现桥按帧取本地 Δ 经 MotorSim 移动；Wave 2.5 已删除 Animator RM 回退。
- L2 HitStop：`hitStopFrames` 经 Pipeline 写入 `ActionSim.freezeFrames`；冻结期间不推进动作帧/位移；骨骼由表现桥读 Snapshot，VFX 由 `SimulationLogicStepEvent` 递减。
- L2 Locomotion：Stop/Pivot 根位移按 `ActionSim.LogicHz` 整数帧取轨，不再用 `NormalizedTime`。
- L2 MotorSim：水平+竖直毫米权威；`TickVertical` 整数重力/着地；逻辑路径不再 `CharacterController.Move`；CC 保持禁用（禁止 Sync 后 re-enable，否则 PhysX 挤出地面呈悬空）。
- L2 静态碰撞：`StaticCollisionBake`（菜单 `ACTGame/Collision/Bake Static From Scene...`）→ `SimStaticCollisionWorld` AABB 滑墙；`CombatWorldController` 绑定资产，未绑定则 `OpenField`。地面薄板/名含 Floor·Ground·Terrain 只写 GroundY，不进水平硬挡；墙体才投影 AABB。Mesh 墙仍用包围盒（保守）；无斜坡。
- L2/M2：`Bake All` / `Bake Dirty Only` + Inspector Dirty 黄条 + `ACTGame/Motion/Validate Motion Dirty`。
- L2 软弹开：`SimulationWorld` 帧末按 Id 序对 `ISimSoftBodyParticipant` 执行 `SoftBodySeparation`（默认 factor=500‰、迭代 3）；按 `softBodyMass` 分配推力，`softBodyImmovable` 像墙；死亡不参与。
- L2 命中：`SimCombatPose` 从 MotorSim 取水平根；Hitbox 挂点只提供相对根局部 TRS；Hurtbox 用 `GetLogicalHurtbox`；自身排除用 `SimActorId`。
- 联网定案：Dedicated 权威状态同步；上行 `InputFrame`，下行 `ReplicationFrame`；命中只在权威 Pipeline。锁步 L5 已取消。阅读：[`docs/2026.8.23/NETSYNC_FROM_JOIN_TO_HIT.md`](../../docs/2026.8.23/NETSYNC_FROM_JOIN_TO_HIT.md)。纠偏合同：[`docs/2026.8.15/UE_ALIGNED_CLIENT_PREDICTION_PLAN.md`](../../docs/2026.8.15/UE_ALIGNED_CLIENT_PREDICTION_PLAN.md)。

### 相关文件

- `Assets/Scripts/Domain/Simulation/*`
- `Assets/Scripts/App/Controllers/Gameplay/SimulationHost.cs`
- `Assets/Scripts/App/Controllers/Combat/CombatWorldController.cs`
- `Assets/Scripts/Domain/Character/Presentation/CharacterPresentationBridge.cs`
- `Assets/Tests/EditMode/Simulation/*`

---

## 组队 PVE · NS0 LocalPlayer

### 功能说明

场景不再假设全场只有一个 `PlayerController`；相机、HUD、刷怪与敌人感知通过花名册查询本机玩家或全部玩家根。

### 实现方案

| 项 | 方案 |
|----|------|
| 本机入口 | `ILocalPlayer`：客机 `PlayerController` 是预测输入/相机拥有者，权威侧 `RemotePlayerSeat` 表示每个连接的阵容 |
| 登记 | `LocalPlayerService`（Architecture System）；预测座位不进入敌人感知根列表，权威 `RemotePlayerSeat` 进入 |
| 查询 | `GetLocalPlayerQuery` / `GetPlayerRootsQuery` |
| 仇恨 | `EnemyPerception` 在玩家根列表中取水平最近；`RemotePlayerSeat` 暴露稳定感知锚点，换人时锚点重挂到当前 Active 槽位根 |
| 禁止 | 玩法 `FindObjectOfType<PlayerController>()`（仅 Editor Gizmo） |

### 关键参数

无新 SerializeField 必填项。`EnemySpawnController.target` / `EnemyController.target` 为空即走花名册。

### 运行时流程

```
Client PlayerController.Awake → LocalPlayerService.Register(this, isLocalOwner: true)
Authority ActGameSessionHandler.TryCreateGuest → Register(RemotePlayerSeat)
ActGameGuest.TryResolveSwitch → RemotePlayerSeat.Bind(to.Actor, to.Root)
CameraManager / HUD → GetLocalPlayerQuery
EnemyPerception.Capture → GetPlayerRootsQuery → 最近根
```

### 已知限制

- 2026-08-18 Unity 编译、Test Runner 与双进程 Play 已确认
- 客机预测座位不进入敌人权威感知；Dedicated/Host 由每连接 `RemotePlayerSeat` 提供当前 Active 槽感知根

### 相关文件

- `Assets/Scripts/Domain/Character/ILocalPlayer.cs`
- `Assets/Scripts/App/Systems/Player/LocalPlayerService.cs`
- `Assets/Scripts/App/Queries/Player/GetLocalPlayerQuery.cs`
- `Assets/Scripts/App/Queries/Player/GetPlayerRootsQuery.cs`
- `Assets/Scripts/Domain/Enemy/EnemyPerception.cs`
- `Assets/Tests/Editor/Enemy/EnemyPerceptionTests.cs`

---

## 组队 PVE · NS1 复制快照

### 功能说明

权威世界把完整角色状态差分为 `ReplicationFrame`；实体生命周期由显式 Spawn/Update/Despawn 表达。

### 实现方案

| 项 | 方案 |
|----|------|
| 下行 | `ReplicationFrame`：`ActCharacterSnapshotSchema` 生产记录（线格式委托 `CharacterSnapshotSchemaV1`）+ 显式生命周期 + Sequence |
| 上行 | `ClientCommand`（frameHint + playerId + `InputFrame`） |
| 组装 | `ReplicationSnapshotBuilder.FromAuthority`（Motor + Action 快照 + 传入 healthMilli） |
| 字节 | `ActorReplicationSnapshotCodec` 是 Snapshot 字段布局唯一真源；`ReplicationFrameCodec` 编码实体记录，`ReplicationCodec` / `RoomCodec` 仅保留上行命令 |
| W3/W4 业务适配 | `ActCharacterSnapshotSchema` 接入生产 Schema Registry 并复用纯 C# V1 线格式；`ActContentRegistry` 持有 Archetype、配置与动作 Catalog |
| W4 权威适配 | `ActAuthorityReplicationAdapter` 独占远端输入灌入、Gameplay Actor Capture 与 FrameHits ActionId 映射；RoomHost 只调度并构建/发送 Frame |
| W4 加入适配 | `ActGameSessionHandler` 创建/销毁 Guest Authority Actor；RoomHost 注入 App 注册委托并独占 `ServerSession.Accept/Reject` |
| W4 Owner 适配 | `ActOwnerReplicationAdapter` 独占 Owner HP、Action Ack、Locomotion Reconcile、Hit/Death 硬吸和预测历史；Client Room 只转发快照 |
| W4 Observer 适配 | `ActObserverReplicationAdapter` 独占 Schema/Archetype 校验、Proxy Spawn/Update/Despawn、TargetSystem 与 View 生命周期；`ActRemoteProxyFactory` 是唯一装配入口 |
| W4 内容真源 | `ActContentRegistry` 唯一持有 Action Catalog、Character Archetype 与 Unity 配置映射；Room/Adapter 禁止另建动作目录 |
| W4 Capture 真源 | `ActCharacterSnapshotSchema.Capture` 统一 CharacterActor → Snapshot 与 V1 编解码；独立 `CharacterReplicationCapture` 已删除 |
| W4 Room 边界 | `ListenServerBootstrap` / `ReplicationRoomClient` 仅做组合或 Session 调度与 HUD；不再引用 Character/配置/Proxy/Hit Cue 具体类型 |
| W4 Gameplay Service | `DedicatedAuthorityWorld` 承接 Guest/Input/Capture；`ActClientRoomGameplay` 承接 Owner 预测、Observer、Hit Cue/HitStop/软碰撞；`ActContentPrefillService` 是场景内容扫描唯一入口 |
| 通用身份 | `NetConnectionId` / `NetPlayerId` / `NetEntityId` / `NetArchetypeId`；`SimActorNetIdAdapter` 显式映射 Simulation Actor |
| 版本基础 | `NetworkProtocolVersion` + 128 位 `ContentFingerprint` 已定义；握手切换留在 Content Manifest Wave |
| 传输 | `INetTransport` / `LoopbackTransport` / `UdpTransport`（`ACTNet.Transport`，按 ConnectionId 定向） |

### 关键参数

Loopback `LatencyMs` 默认 0。`actionId` 由 `ActionReplicationCatalog` 按资产名稳定哈希。

### 运行时流程

```
ActAuthorityReplicationAdapter
  → RoomRemoteInputMerge → InputFrameBuffer
  → ActCharacterSnapshotSchema.Capture/Encode → ReplicationEntityState full set
  → FrameHits + Snapshot ActionId → ActReplicationApplicationPayload
  → ReplicationServer.BuildFrame → ReplicationFrameCodec
  → Session/Transport → ReplicationClient.ApplyFrame
  → ActClientRoomGameplay → Owner reconcile / Observer Proxy / ACT 表现
```

### 已知限制

- 水平速度 P0 可为 0；空闲相位由 Capture 填 `AnimationKey`
- NS5 已单轨切到 `UdpTransport`；`LoopbackTransport` 支持一服多客和确定性延迟，不再挂 Host 预览
- W0～W4、GF0～GF4 与 M1 已于 2026-08-18 完成 Test Runner、架构守卫和双进程回归
- W5 Dedicated Bootstrap 已于 2026-08-19 用户验收
- W6 Headless Authority / Content Fingerprint 已于 2026-08-19 用户验收
- W7 Match / 每连接 Replication 已于 2026-08-19 用户验收
- W8 Dedicated 启动覆盖 / READY / 出包已于 2026-08-19 用户验收；M2 关闭

### 相关文件

- `Assets/Scripts/Domain/Simulation/Replication/*`
- `Assets/Scripts/Domain/Networking/*`
- `Assets/Scripts/Framework/ACTNet/Core/*`
- `Assets/Scripts/Domain/Net/Identity/SimActorNetIdAdapter.cs`
- `Assets/Scripts/Framework/ACTNet/Transport/*`
- `Assets/Tests/EditMode/Simulation/ActorReplicationSnapshotTests.cs`
- `Assets/Tests/EditMode/ACTNet/Transport/LoopbackTransportTests.cs`

---

## 组队 PVE · NS2 RemoteProxy

### 功能说明

客机用 `RemoteCharacterProxy` 播他人与敌人：只跟 `ReplicationFrame` 的 Character 记录 Seek，不跑第二份命中。Host 同机 ±2m 预览已删除。

### 实现方案

| 项 | 方案 |
|----|------|
| 捕获 | `ActCharacterSnapshotSchema.Capture` + `ActContentRegistry.Actions` |
| 传输 | 房间 `UdpTransport`；Loopback 仅单测 |
| 应用 | `RemoteCharacterProxy`：位姿写 Motor；招式切段 Seek；Locomotion 硬切 + `SeekLocomotionNormalized`；关掉 Animator RM |
| 朝向调试 | 客机幽灵挂同一套黄/品红箭；wish 走快照 `moveV*`，与延迟位姿成对 |
| 插值 | 复用 `CharacterPresentationBridge.Render(alpha)` |
| 装配 | `ActRemoteProxyFactory`，**不**走 `CharacterActorFactory` |
| 入口 | `ReplicationRoomClient` 收帧后交 `ActClientRoomGameplay.ApplyReplicationFrame`，再由 Observer Adapter 应用 |

### 关键参数

无 Host 预览 SerializeField。房间延迟即 UDP RTT，不另加 Loopback。

### 运行时流程

```
Host.Step → AfterLogicStep
  → Capture full set → ReplicationServer.BuildFrame → UDP
客机 Pump → RemoteProxy.ApplySnapshot
LateUpdate → proxy.Render(Host.InterpolationAlpha)
```

### 已知限制

- PivotTurn 根朝向仍只跟快照 facing（不在幽灵侧重跑 AnimAuth）；Clip 已按权威归一化时间 Seek
- Catalog 已改为资产名稳定 Id（NS5）
- 幽灵不进权威花名册、无 Hurtbox Collect；同一动作按前后 ActionFrame 补齐 VFX/SFX；新动作或帧回绕只派发当前跨帧。本机 `CharacterActor` 与远端 Proxy 在阵容成员隐藏前都由 `IActionVisibilityResetConsumer` 回收仍挂在角色下的 VFX；远端重新显形时另清空旧 `SnapshotTimeline` 并重置插值双端
- 多种敌人通过 `NetArchetypeId` 精确解析各自配置；未知 Archetype 明确拒绝，不做首敌回退

### 相关文件

- `Assets/Scripts/Domain/Character/Replication/*`
- `Assets/Scripts/App/Networking/Services/ActClientRoomGameplay.cs`
- `Assets/Scripts/App/Controllers/Gameplay/SimulationHost.cs`
- `Assets/Tests/EditMode/Simulation/ReplicationPoseApplierTests.cs`
- `Assets/Tests/Editor/Replication/ActionReplicationCatalogTests.cs`
- `Assets/Tests/Editor/Replication/RemoteCharacterProxyTests.cs`
- `Assets/Tests/Editor/Replication/ReplicationPresentationAlignTests.cs`

---

## 组队 PVE · NS3 预测位移

### 功能说明

Listen 本机与远端客机都用本地 `InputFrame` 立刻推进位移。**房间走跑由 Autonomous `CharacterActor.Step` 写 MotorSim**；`Predict`/`ApplyInput` 仅留单测。历史与 Restore+Replay 由 `PredictionCoordinator` 编排，2m Gate 在 `ActCharacterPredictionModel`。已删除 Runner / 猜片 / Host 同机预览。

### 实现方案

| 项 | 方案 |
|----|------|
| 走跑步进 | Autonomous `CharacterActor.Step`（同一套内层机） |
| 互撞 | 不进 World；`AutonomousSoftBodySolver` 把本机从只读幽灵圆盘推出 |
| 出招位移 | 表现桥烘焙 + TargetAdhesion / Relocate（WorldQuery 读只读 Proxy Pose） |
| 缓存 | `PredictionCoordinator.Record` → CommandHistory + StateHistory |
| 和解 | 模型算策略，Coordinator 执行 Ack / Restore / Replay。≤ 2m 只 Ack；无 replay：≤ 50mm；刚吸附 8 包内 ≤ 150mm 也只 Ack |
| Listen / Client | 场景 `PlayerController` 为 Autonomous；权威玩家在 Headless Guest |

### 关键参数

| 参数 | 默认 | 说明 |
|------|------|------|
| 走/跑/冲刺 | 4000 / 7000 / 9000 mm/s | 与 Motor 默认一致；纠偏阈用毫米 |
| `rotationSmoothTimeSeconds` | 0.2 | 与 `CharacterMotorConfig` FollowInput 同参 |
| `runThresholdMilli` | 600 | 输入幅度 0.6 |
| `reconcileThresholdMm` | 50 | 仅无 replay / 单测；房间走跑不用 |
| `AutonomousHardSnapMm` | 2000 | 走跑+Runner 默认硬吸阈；50mm 每包重放会卡顿 |
| `SnapGraceMaxErrorMm` / 宽限包数 | 150 / 8 | 吸附后避免立刻连吸 |

### 运行时流程

```
本机 InputFrame
  → 走跑：Runner.Tick + RecordAutonomous
  → 出招/受击：Runner.Exit + PredictAligned
权威 Tick
  → Reconcile：走跑 ≤2m 或宽限内 Ack / 超 2m RestoreFromAuthority + ReplayTick
  → 仅走跑 Snapped 或 Hit/Death 后 SnapPresentationToSimulation
  → 出招/闪避：只 Exit Runner，位姿由 AfterLogicStep ApplySnapshot 插值
表现：走跑 SyncAutonomousLocomotion；出招 ApplySnapshot
```

### 已知限制

- Lean 不进 Snapshot，仅本机从 Actor 倾身模型推进
- 客机本机对他人/敌人幽灵做只读软弹开（幽灵不可推动）；不进 `SimulationWorld`
- 出招预测见下一节 NS4 / 方案 UE4
- 客机相机绕圈依赖 `HasMoveIntent`（设备采样），不得读空的 `ILocalPlayer.Input`
- 客机出招/闪避由只读 ActionSim 本机起手；位移仍跟快照插值（不跑 ActionMotionResolver）

### 相关文件

- `Assets/Scripts/Framework/ACTNet/Prediction/*`
- `Assets/Scripts/Domain/Simulation/Prediction/*`
- `Assets/Scripts/App/Networking/Services/ActClientRoomGameplay.cs`
- `Assets/Tests/EditMode/ACTNet/Prediction/FakeLinearEntityPredictionTests.cs`
- `Assets/Tests/EditMode/Simulation/PredictedLocomotionReconcileTests.cs`

---

## 组队 PVE · NS4 出招预测与权威命中

### 功能说明

本地预测出招只播 Clip；伤害、硬直、HP 只认权威 `CombatHitPipeline`。客机他人/敌人 `RemoteCharacterProxy` 跟延迟 Snapshot，受击只出现一次。

### 实现方案

| 项 | 方案 |
|----|------|
| 出招预测 | Autonomous `CharacterActor` + `ActionSim` + 表现桥；Ack 用 `PredictedActionAckQueue` |
| 取消 | 该帧权威 ActionId=0，或 Vitality Hit/Death → `StopAutonomousAction` / `EnterHit`；连招超前不 Cancel |
| 表现所有权 | 本机 Clip 由 Actor 桥推进；禁止对自 `ApplySnapshot`；仅受击走 `EnterHit` |
| 卡肉 | 客机 `PredictedHitStopConsumer` 几何重叠后 `RequestHitStop`；禁止用延迟权威 Freeze 再拖时钟。伤害只信权威下行 |
| 跟招 | 已删除 `FollowAuthorityAction`；本机 Clip 只跟本地 ActionSim |
| 命中下行 | 本帧 `ReplicatedHitEvent` 走 `RoomMessageKind.ReplicationEvent` 可靠通道；Snapshot 应用载荷不再带 hits。`VitalityReplicationEdge` 仍在角色快照 |
| 他人/敌人 | `RemoteCharacterProxy` 跟 `SnapshotTimeline` 延迟取样；`VitalityEdge.Hit` 或动作帧回绕时硬切重播受击 |
| Listen | 本机也走 Owner 预测；`HitboxFrameConsumer` 只挂权威工厂 |

### 关键参数

无 Host 预览参数。卡肉与 Ack 见客机 `PredictedHitStopConsumer` / `PredictedActionAckQueue`。

### 运行时流程

```
权威 Step → Pipeline.Collect/Resolve → Vitality 边沿
AfterLogicStep → Capture 全员 → Snapshot UDP；本帧 CopyHits → FlushEvents
客机：本机 Actor.Step + Ack；他人/敌人 Timeline 取样 + RemoteProxy
```

### 已知限制

- 客机本机跑 ActionSim；仍不 Collect、不写 Numeric
- Clip 与 VFX/SFX 由本机 `CharacterActionPresentationBridge` 派发
- 受击火花走复制落点 + Hitbox Feedback
- 本机招打完后不得再用延迟快照重播同一招的 Clip/VFX
- Relocate/Adhesion 客机读只读 Proxy 逻辑 Pose；不 Collect

### 相关文件

- `Assets/Scripts/Domain/Character/CharacterActor.cs`
- `Assets/Scripts/Domain/Simulation/Prediction/PredictedActionAckQueue.cs`
- `Assets/Scripts/App/Networking/Services/ActClientRoomGameplay.cs`
- `Assets/Tests/EditMode/Simulation/PredictedActionReconcileTests.cs`

---

## 组队 PVE · NS5 最小 2 人房间

### 功能说明

Listen 与 Dedicated 共用 `DedicatedServerRuntime`。Listen 另加本机 `LocalClientRuntime`（127.0.0.1 UDP）；房主场景座位是 Autonomous，权威玩家只在 Guest 座位。客机预测自己、用 RemoteProxy 看队友与敌人；敌人与命中只在权威世界。

### 实现方案

| 项 | 方案 |
|----|------|
| 角色 | 默认 Listen Host；ParrelSync 克隆自动 Client；菜单可切 Dedicated（`Use Dedicated Server`） |
| 传输 / Session | `UdpTransport` 按 `NetConnectionId` 定向收发；`ServerSession/ClientSession` 独占信封、Join、Heartbeat、Kick，`RoomCodec` 只编 ACT 应用正文 |
| Listen | `ListenServerBootstrap` = `DedicatedServerRuntime` + `LocalClientRuntime`；本机也走 Command / Snapshot / ACK |
| Dedicated | `DedicatedServerBootstrap` → 同一 `DedicatedServerRuntime`；`MatchCoordinator` 分配身份与出生；JoinAccept 无房主实体 |
| Client | 薄 `ReplicationRoomClient` 驱动 `LocalClientRuntime`；`ActClientRoomGameplay` 每渲染帧合并输入、本机 `CharacterActor.Step`、他人 Proxy Seek |
| 动作 Id | `ActionReplicationCatalog` 按资产名稳定哈希，两端 Prefill Graph 节点、`VariantResolver` 变体与反应 |
| 掉线 | `ServerSession.ConnectionRegistry` 按连接记录活动时刻；10s 超时仅 Kick 对应连接 |
| HUD | F3 Room 行：角色 / 状态 / authorityFrame / RTT / jitter；Net 行追加 Tick/Command 字节、Proxy、pending、loss‰、delay、snap、replay |

### 关键参数

| 参数 | 默认 | 说明 |
|------|------|------|
| `listenPort` | 7777 | Host 绑定 / Client 连接 |
| `contentVersion` | 1 | 双方必须一致，否则拒收 |
| 空闲超时 | 10000ms | 定案：待机 10s 后剔除 |
| 迟到窗口 | 8 逻辑帧 | 更旧的 FrameHint 丢弃（不与权威帧比较） |
| 输入冗余 | 3 条/包 | 最近 FrameHint 重发；Host 跳过已应用 Hint |

### 运行时流程

完整往返（入房、每帧序、客机攻击、线格式）见 [`docs/2026.8.23/NETSYNC_FROM_JOIN_TO_HIT.md`](../../docs/2026.8.23/NETSYNC_FROM_JOIN_TO_HIT.md)。

```
Listen：LocalClient Poll/采样 → 按 PeekAdvanceSteps 发命令预测 → DedicatedServerRuntime.Poll → LocalClient 再 Drain 同拍快照
Client：ReplicationRoomClient Poll → LocalClient 采样 → 逻辑步构命令发送 → Actor.Step → 收帧 Restore+Replay/Proxy
```

### 已知限制

- 客机连招下一段在本机 Cancel 窗起手；权威未起手则 Stop
- 客机 CameraLock：Proxy 只读进 TargetSystem，范围内自动选中后可开；2026-08-18 双进程 Play 已验收
- 多种敌人按稳定 Archetype 精确生成对应幽灵，不使用首敌配置回退
- Dedicated Editor Play 与玩家 Dedicated Build 均已验收（M2）
- Listen 本机从场景出生点会被首帧 Snapshot 吸到 Match 槽位（槽位 × 2000mm）
- 同进程 TargetSystem 可能同时看到 Headless 权威 Hurtbox 与 Observer Proxy；感知根已排除预测座位
- 未做匹配、排位、Host 迁移
- UDP 仍不可靠；冗余 3 条降低丢边沿，不能保证 0 丢包
- 客机刀光/音效由本机表现桥按预测帧派发；跟权威卡肉招时禁止重派点事件
- UE2：走跑超阈 Restore+Replay；烘焙 Stop/Pivot 游标用归一化时间近似，未加 `locomotionMotionFrame`
- 客机闪避由 Actor ActionSim + Directional 本机起手；结束时按 `SprintAfterDodge` 接片；烘焙位移会跑，Relocate 不跑

### 相关文件

- `Assets/Scripts/Domain/Character/Replication/ReplicationSeat.cs`
- `Assets/Scripts/Domain/Character/Replication/LocomotionSavedState.cs`
- `Assets/Scripts/Domain/Simulation/Prediction/IPredictedLocomotionReplay.cs`
- `Assets/Scripts/Framework/ACTNet/Transport/UdpTransport.cs`
- `Assets/Scripts/Domain/Simulation/Replication/RoomCodec.cs`
- `Assets/Scripts/Domain/Simulation/Replication/RoomRemoteInputMerge.cs`
- `Assets/Scripts/Domain/Character/Replication/ReplicationPresentationAlign.cs`
- `Assets/Scripts/App/Controllers/Gameplay/ListenServerBootstrap.cs`
- `Assets/Scripts/App/Controllers/Gameplay/ReplicationRoomClient.cs`
- `Assets/Scripts/App/Networking/Services/LocalClientRuntime.cs`
- `Assets/Scripts/App/Networking/Services/ActClientRoomGameplay.cs`
- `Assets/Scripts/App/Networking/Services/ActContentPrefillService.cs`
- `Assets/Scripts/Domain/Combat/VFX/HitImpactCuePlayer.cs`
- `Assets/Scripts/Editor/Net/ReplicationRoomMenu.cs`
- `Assets/Tests/EditMode/Simulation/RoomCodecTests.cs`
- `Assets/Tests/EditMode/ACTNet/Session/SessionIntegrationTests.cs`
- `Assets/Tests/EditMode/ACTNet/Transport/UdpTransportTests.cs`
- `docs/2026.8.23/NETSYNC_FROM_JOIN_TO_HIT.md`

---

## 组队 PVE · W5 Dedicated Bootstrap

### 功能说明

无本地玩家的 Dedicated 进程可 Listening 并 Accept 远端玩家；身份与出生由 Match 分配，不再依赖房主 Actor。

### 实现方案

| 项 | 方案 |
|----|------|
| 进程角色 | `NetProcessRole.DedicatedServer`；`ReplicationRole.DedicatedServer` 仅作场景入口枚举 |
| 程序集 | `ACTGame.Server`：不引用 PlayerController / InputReader / Camera / HUD / Room Facade |
| 启动 | `CombatWorldController` 只 `EnsureDedicatedBootstrap()`；先 `ServerLaunchConfigResolver` 再 `TryStart` |
| 退出码 | `ServerExitCode.ConfigFailed=10`、`BindFailed=20`；玩家构建 `Application.Quit` |
| 身份 | `MatchCoordinator` 分配 PlayerId / EntityId / Team / Spawn（槽位 × 2000mm X） |
| 每连接 | `DedicatedPlayerRuntime` 持 Hint ACK；`ReplicationServer` 在 `DedicatedAuthorityWorld` 按连接持有 |
| JoinAccept | `AuthorityEntityId` 可为 Invalid；线格式 0 |

### 关键参数

| 参数 | 默认 | 说明 |
|------|------|------|
| `MaxRemotePlayers` | 4 | Listen 本机也占一席；Dedicated / Listen 同一容量 |
| `firstPlayerId` | 1 | 不再预留 Guest=2 |
| 出生间距 | 2000mm | X 轴；不读 Host Root |

### 运行时流程

```
CombatWorldController.Awake（Dedicated）
  → DedicatedServerBootstrap.Configure
  → DedicatedServerRuntime.TryStart(UdpTransport, ServerLaunchConfig)
  → Update：Poll Session → Match Accept → 每连接 ACK
```

### 已知限制

- W5 当时不步进 World；W6 已步进，W7 已下发 Frame
- 特殊 Listen Host Room 已删；权威只走 `DedicatedServerRuntime`

### 相关文件

- `Assets/Scripts/App/Server/DedicatedServerRuntime.cs`
- `Assets/Scripts/App/Server/DedicatedServerBootstrap.cs`
- `Assets/Scripts/App/Server/MatchCoordinator.cs`
- `Assets/Tests/EditMode/ACTGame/Server/DedicatedServerRuntimeTests.cs`

---

## 组队 PVE · W7 Dedicated Match / Replication

### 功能说明

无本地玩家的 Dedicated 进入 Playing 后，按连接下发与 Listen 相同的 `ReplicationFrame`；Owner 预测、Observer Proxy 复用 W4 Client Adapter。对局可结束并回到 Lobby；玩家构建在 `ExitOnMatchEnd` 时接着退出进程。

### 实现方案

| 项 | 方案 |
|----|------|
| Match | `DedicatedMatchPhase`：Lobby → Starting → Playing → Ending → Cleanup → Lobby |
| Join | Playing 可晚加入；Ending 之后拒收 |
| 实体 Id | JoinAccept 写 World `SimulationId`，供 Client `CanPredict` 对齐 |
| 命令 | 只灌本连接 PlayerId；冗余批合并进下一权威帧；下行 appliedHint=本批第一条 Hint |
| 构帧 | `AfterLogicStep` Capture + 每连接 `ReplicationServer.BuildFrame`；Runtime 发送 Snapshot |
| 命中 | 本帧事件走 `EventReliableOrdered`；Client `SimHitKey` 去重只播一次 |
| 结束 | `RoomMessageKind.MatchEnd=8` + Kick；Client 先 Drain 再 Sync Session |

### 关键参数

| 参数 | 默认 | 说明 |
|------|------|------|
| MatchEnd 类型 | 8 | 避开 Session Kick=7 |
| ReplicationEvent 类型 | 9 | 可靠命中事件包 |

### 运行时流程

```
DedicatedServerRuntime.Poll
  → DrainJoins / DrainCommands（Merge 进下一权威帧）
  → Advance → StepOnce → AfterLogicStep Capture/构帧（appliedHint=FirstAppliedHint）
  → FlushReplication + FlushEvents
  → 空房或 RequestMatchEnd → MatchEnd + Kick → Lobby
```

### 已知限制

- Dedicated Build 已验收；命中已改可靠事件单轨。100ms/5% 公网 Play 仍待 W10 出口验收

### 相关文件

- `Assets/Scripts/App/Server/DedicatedServerRuntime.cs`
- `Assets/Scripts/App/Networking/Services/DedicatedAuthorityWorld.cs`
- `Assets/Scripts/Domain/Simulation/Replication/RoomCodec.cs`
- `Assets/Tests/EditMode/ACTGame/Server/DedicatedServerRuntimeTests.cs`

---

## 组队 PVE · W8 Dedicated 启动与进程生命周期

### 功能说明

Dedicated 进程按 CLI / 环境变量 / 配置文件覆盖监听与生命周期；监听成功打 READY；空 Lobby 超时或对局结束后可退出。Editor Play 不退出 Unity，并保持回 Lobby 再入房。

### 实现方案

| 项 | 方案 |
|----|------|
| 覆盖 | `ServerLaunchConfigResolver`：CLI > Env > File > Default |
| 空房 | `EmptyLobbyTimeoutMs`；仅从未有人加入的 Lobby；0=不超时 |
| 对局结束 | `ExitOnMatchEnd` 时 EmptyRoom / Completed 后 `ShouldExit` |
| Ready | `IsReady`；日志 `READY port=… role=DedicatedServer` |
| Editor | `CombatWorldController` 强制超时 0 且不退出进程 |
| 玩家构建 | 默认 `ExitOnMatchEnd=true`；Bootstrap `Application.Quit` |

### 关键参数

| 参数 | 默认 | 说明 |
|------|------|------|
| `EmptyLobbyTimeoutMs` | 0 | 无人到访才计时 |
| `ExitOnMatchEnd` | Editor false / 玩家构建 true | Editor 不可被 CLI 打开 |
| 退出码 | 0 / 10 / 20 | 正常 / 配置 / 绑定 |

### 运行时流程

```
CreateDefault → TryResolve → Bootstrap.TryStart → READY
Poll → 空房超时或 ExitOnMatchEnd → ShouldExit
玩家构建 Application.Quit(0)；Editor 只 Dispose
```

### 已知限制

- CI 自动出包、脚本化双 Client MatchEnd 烟测后置，不挡 M2
- 内容指纹仍由场景扫描；CLI 改 `contentVersion` 后会重算指纹
- Listen 已改为同一 `DedicatedServerRuntime` + `LocalClientRuntime`（W9 用户验收 2026-08-20）

### 相关文件

- `Assets/Scripts/App/Server/ServerLaunchConfigResolver.cs`
- `Assets/Scripts/App/Server/DedicatedServerRuntime.cs`
- `Assets/Scripts/App/Server/DedicatedServerBootstrap.cs`
- `Assets/Tests/EditMode/ACTGame/Server/ServerLaunchConfigResolverTests.cs`
- `docs/2026.8.19/DEDICATED_SERVER_LAUNCH.md`

---

## 组队 PVE · W9 Listen 组合收敛

### 功能说明

Listen 不再有特殊 Host 本机玩家。本机进程组合同一 `DedicatedServerRuntime` 与 `LocalClientRuntime`；房主在 Server 是 Authority Guest，在本机是 Owner/Presentation。

### 实现方案

| 项 | 方案 |
|----|------|
| 组合 | `ListenServerBootstrap` 拥有 Runtime + LocalClient；不挂 `DedicatedServerBootstrap` |
| 回环 | 本机 `ClientSession` 连 `127.0.0.1:实际绑定端口` |
| 帧序 | Poll/采样 → 按 `PeekAdvanceSteps` 发命令预测 → `Server.Poll` → 再 Drain |
| 座位 | `PlayerController` Listen/Client 只装 Autonomous；Dedicated 禁用 |
| 敌人 | Listen / Dedicated 权威 `AuthorityHeadless`；可见体走 Observer |
| Capture | 只拍 Guest + 敌人，不再拍场景 LocalPlayer |

### 关键参数

| 参数 | 默认 | 说明 |
|------|------|------|
| `MaxRemotePlayers` | 4 | 含本机 Join |
| `ExitOnMatchEnd` | false | Listen 不因对局结束退 Editor |
| Bootstrap 执行序 | -210 | 先于 `SimulationHost` -100 |

### 运行时流程

```
CombatWorldController.Awake（ListenHost）
  → DedicatedAuthorityWorld + ListenServerBootstrap.Configure
  → TryStart DedicatedServerRuntime
  → Start：LocalClient 连 127.0.0.1
Update：PollAndApply → SampleRenderInput → 按 PeekAdvanceSteps 发命令预测 → Server.Poll → PollAndApply
```

### 已知限制

- 本机预测必须按权威步数，禁止每个渲染帧 `StepPrediction`（否则连段加速、移动被快照拉回）
- 同进程 TargetSystem 可能同时登记 Headless Hurtbox 与 Observer Proxy
- 本机出生先被 Snapshot 吸到 Match 槽位

### 相关文件

- `Assets/Scripts/App/Controllers/Gameplay/ListenServerBootstrap.cs`
- `Assets/Scripts/App/Networking/Services/LocalClientRuntime.cs`
- `Assets/Scripts/App/Server/DedicatedServerRuntime.cs`

---

## 组队 PVE · W10 通用预测 / 可靠通道 / 网络时间

### 功能说明

预测算法骨架可复用；ACT 2m Gate / 连招 / Hit-Death 仍归业务层。Control/Event 可靠有序，命中不再用帧内 8 条冗余。远端 Proxy 按插值延迟取样。公网 Play 未验收。

### 实现方案

| 项 | 方案 |
|----|------|
| 通用协调 | `PredictionCoordinator` + Command/State History；不读 ActionId |
| ACT 策略 | `ActCharacterPredictionModel.ResolvePolicy` |
| 远端 | `SnapshotTimeline` 丢旧 Tick；`NetworkTimeEstimator` 算 delayTicks |
| 通道 | Session 包装 `ChannelMuxTransport`；定案不换 LiteNetLib / Unity Transport |
| 命中 | `ActReplicationEventCodec` + `EventReliableOrdered` |
| MTU | 默认 1400；超限拒绝 |

### 关键参数

| 参数 | 默认 | 说明 |
|------|------|------|
| `TransportMtuGate.DefaultMaxDatagramBytes` | 1400 | 含 9 字节通道头 |
| Mux 重传间隔 | 50ms | Control/Event |
| 插值延迟 | RTT/2 + jitter + 16ms | 钳 16～150ms，至少 1 Tick |

### 运行时流程

```
Owner：Record → PeekError → ResolvePolicy → ReceiveAuthority
Observer：TryPush → Render(sample delay) → ApplySnapshot → proxy.Render(alpha)
Hit：CopyHits(本帧) → FlushEvents → ApplyReplicationEvents → 去重播放
```

### 已知限制

- W10 出口未关：100ms / 20ms jitter / 5% 丢包 Play 未做
- 超 MTU 只拒绝不拆包（W11）
- 不得称公网可用

### 相关文件

- `Assets/Scripts/Framework/ACTNet/Prediction/*`
- `Assets/Scripts/Framework/ACTNet/Transport/ChannelMuxTransport.cs`
- `Assets/Scripts/Framework/ACTNet/Transport/TransportMtuGate.cs`
- `Assets/Scripts/Domain/Simulation/Prediction/ActCharacterPredictionModel.cs`
- `Assets/Scripts/Domain/Networking/ActReplicationEventCodec.cs`
- `Assets/Scripts/App/Server/DedicatedEventSend.cs`

---

## 组队 PVE · W11 Delta / Relevancy / FakeActionGame

### 功能说明

复制不再每连接每 Tick 全量 Update。未变实体跳过；敌人按 40m 兴趣裁剪；Update 有字节预算；Owner 优先刷新。Graph 节点线上改为稳定整数。FakeActionGame 证明框架可不引用 ACT Character。

### 实现方案

| 项 | 方案 |
|----|------|
| 未变跳过 | `ReplicationServer` 对比上次已发送 payload |
| 节拍 / 预算 | `ReplicationBuildOptions.Compact`：间隔 2 Tick、1200 字节、Owner 优先 |
| 兴趣 | `ReplicationInterest`：Owner/玩家 Always；敌人平面距离 |
| 恢复 | `ReplicationRecover` → `ResetBaseline` → 全量 Spawn |
| 节点 | `GraphNodeKey.FromStableName`（FNV-1a） |
| 第二用例 | `Assets/Tests/EditMode/ACTNet/FakeActionGame/` |

### 关键参数

| 参数 | 默认 | 说明 |
|------|------|------|
| `SnapshotIntervalTicks` | 2 | 非 Owner 刷新间隔 |
| `MaxUpdateBytes` | 1200 | 仅约束 Update |
| `DefaultRadiusMm` | 40000 | 敌人兴趣半径 |
| `GraphNodeKey` | int32 | 空名=0 |

### 运行时流程

```
Capture → CopyRelevantStates → BuildFrame(Compact)
Rejected → ResetReplicationForRecovery → ReplicationRecover → ResetBaseline
ApplyUpdates → ApplySnapshot(立即写判定/受击/Notify)
Observer.Render → RemotePlaybackClock → SetPresentationBracket → TickAnimation → Render(alpha)
```

### 已知限制

- W10 / W11 Play 均未用户验收
- 无字段级 change mask、无超 MTU 拆包
- `RoomCodec` 仍在 Simulation；未宣称只经 Networking Adapter
- 远端隔步快照：播放头只插值锚点；出招/受击 `Urgent` 每 Tick 下发；Notify 随快照到达立即派发
- 不得称 R2 完成或公网可用

### 相关文件

- `Assets/Scripts/Framework/ACTNet/Replication/ReplicationBuildOptions.cs`
- `Assets/Scripts/Framework/ACTNet/Replication/ReplicationInterest.cs`
- `Assets/Scripts/Framework/ACTNet/Prediction/RemotePlaybackClock.cs`
- `Assets/Scripts/Domain/Character/Replication/RemoteCharacterProxy.cs`
- `Assets/Scripts/Domain/Simulation/Replication/GraphNodeKey.cs`
- `docs/2026.8.23/NETSYNC_FROM_JOIN_TO_HIT.md`

---

## 1. 第三人称移动

### 功能说明

玩家通过 WASD 相对**相机朝向**移动；摇杆/键盘输入幅度影响移动速度；角色平滑转向移动方向；含简易重力与贴地。

### 实现方案

| 项 | 方案 |
|----|------|
| 碰撞体 | `CharacterController`（非 Rigidbody） |
| 位移执行 | `LocomotionStateMachine` 各相位 → `CharacterMotor.ApplyLocomotion` |
| 方向计算 | InputFrame 本地 Vector2 + `MoveReferenceYawQuantized` → 世界 XZ wish；Motor 不读 Camera Transform |
| 速度 | `moveInputMagnitude × speed`；幅度 > `runThreshold` 用 `runSpeed`，否则 `walkSpeed` |
| 旋转 | `SmoothDampAngle` 显式传入固定 `1/60s`，绕 Y 轴对齐移动方向 |
| 重力 | `CharacterMotorSim.TickVertical`（mm/s² ÷ logicHz）；着地钳 `GroundYMm` |

### 关键参数（Prefab 默认）

| 字段 | 默认值 | 含义 |
|------|--------|------|
| `walkSpeed` | 4 | 走速 |
| `runSpeed` | 7 | 跑速 |
| `runThreshold` | 0.6 | 输入幅度超过此值视为跑 |
| `rotationSmoothTime` | 0.12 | 转向平滑时间 |
| `gravity` | -20 | 重力加速度 |
| `groundedGravity` | -2 | 着地时 Y 速度 |

### 运行时流程

```
SimulationWorld.Step
  → InputFrameBuffer.ResolveLocal
  → InputManager.IngestFrame
  → ResolveWorldMoveDirection(localMove, MoveReferenceYawQuantized)
  → 有方向：SmoothDamp 旋转 + Move(水平)
  → ApplyGravity：Move(垂直)
```

### 对外暴露（供状态机）

- `MoveInputMagnitude`、`RunThreshold`、`IsGrounded` — 由当前 State 从 `CharacterMotor` 同步到 `CharacterContext`

### 已知限制

- Locomotion 水平移动由内层相位 State → `ApplyLocomotion` 拥有；重力仍由 `CharacterActor` 每帧统一推进
- 玩家 Orbit yaw 在渲染采样边界 staged 到下一 InputFrame；追帧/回放只读已记录 yaw
- AI `LocomotionDesire` 显式携带 reference yaw，不伪装玩家相机

### 相关文件

- `Assets/Scripts/App/Controllers/Gameplay/PlayerController.cs`
- `Assets/Prefabs/Player/Player_KatanaGirl.prefab`

---

## 2. 输入系统

### 功能说明

使用 Unity **Input System** 在玩家设备边界采样，立即量化为带逻辑帧与 SimActorId 的 `InputFrame`，再由 `GameplayIntentProducer` 转换为设备无关意图；AI 移动 / 出招走独立命令源。

### 实现方案

| 项 | 方案 |
|----|------|
| 资产 | `GameInputActions.inputactions` |
| 形态 | `InputReader` 实现 `ILocalInputSampler`；AI 通过 `IMoveIntentSource` / `IActionEntryRequestSource` 注入，不伪装设备 |
| 绑定 | Move 从 Player Map 读取并量化为 sbyte；Orbit yaw 量化为 MoveReferenceYaw；TargetSwitch 映射固定按钮；Look/CameraLock 仅供相机表现 |
| 生命周期 | OnEnable/OnDisable 启用/禁用整个 Asset |
| 输入历史 | `InputFrameBuffer` 按 `(frame, actorId)` 保存；多渲染样本边沿 OR、连续状态取最后值 |
| 追帧展开 | 缺少下一设备样本时只延续 Move/Held；Pressed/Released 不重复、不从 Held 推导 |
| 原始中枢 | `InputManager` 摄入量化帧，提供移动反解值与 Pressed/Held/Released bit 查询 |
| 语义生产 | `GameplayIntentProducer`：SprintAttack、DodgeAttack、PressedThenLong；Hold 按整数帧累计 |
| 语义缓冲 | `GameplayIntentBuffer`：当帧事件 + 整数帧 TTL 的 Action Cancel 缓冲 |
| 消费方 | `CharacterActionDriver` 消费动作意图；Locomotion 继续消费连续 Move 快照 |

### 绑定摘要

| Action | 类型 | 主要绑定 |
|--------|------|----------|
| Move | Vector2 | WASD 复合键；Gamepad 左 Stick |
| Look | Vector2 | 鼠标 Delta；Gamepad 右 Stick |
| Attack | Button | Pressed→Attack（Sprint 时 SprintAttack；Dodge Action 中为 DodgeAttack）；HoldReached→LongPressedAttack；Released→AttackRelease |
| Dodge | Button | Pressed→Dodge |
| TargetSwitchLeft / Right | Button | Pressed→InputFrame 固定 bit；Locomotion/Action 中均可切换 SelectedTarget |
| CameraLock | Button | 本地表现开关；无 SelectedTarget 时无效，不进入 InputFrame |

### 错误处理

未分配 `inputActions`（玩家）或全局 `GameplayIntentProfile` 未就绪时校验/工厂失败。意图经 `GameplayIntentSettings`（Resources `ACT/GameplayIntentProfile`，菜单可迁移）。木桩：Brain `enableCombatActions=false`。L0B 帧阈值：Intent 缓冲常见 60；EnemyBrainProfile 建议攻击冷却 72、失败重试 12、朝向刷新 6。

### 相关文件

- `Assets/Scripts/Domain/Simulation/Input/*`
- `Assets/Scripts/Infrastructure/Input/InputReader.cs`
- `Assets/Scripts/Domain/Input/GameplayIntent*.cs`
- `Assets/Scripts/Domain/Character/Commands/*`
- `Assets/Scripts/Input/GameInputActions.inputactions`

---

## 3. 角色状态机

### 功能说明

状态机驱动角色逻辑；角色侧通过 `CharacterActor.Step` 在固定 60Hz 逻辑帧摄入输入并 Tick 当前 State。

### 实现方案

**Core 层（无 Unity 依赖）**

```
StateMachine<TStateId, TContext>
  RegisterState → Initialize(context, initial) → Tick / TryChangeState
```

- `StateBase` 默认 `CanTransitionTo`：仅允许转到**枚举值更大**的状态（Locomotion=10 → Action=60 → Hit=80 → Death=100）
- 同 ID 或转换被拒时 `TryChangeState` 返回 false

**Character 层**

- `CharacterStateMachine` 是纯 C# 宿主：构造时组装 `CharacterContext`，注册 State，初始 `Locomotion`
- 每次 `CharacterActor.Step` 调 `_machine.Tick(1/60f)`

**Player 层**

- `CharacterActor`：采集输入、处理动作路由、推进重力，再 Tick `CharacterStateMachine`

### 已注册状态

| State | Id | Enter | Tick | Exit |
|-------|-----|-------|------|------|
| `LocomotionState` | 10 | `LocomotionStateMachine.Enter` | `LocomotionStateMachine.Tick` | `LocomotionStateMachine.Exit` |
| `ActionState` | 60 | `Animation.SetLocked(true)` | `ActionRotationDriver.Tick`；不重复推进 Action | Unlock + ResetPlaybackState |

### 运行时流程（玩家）

```
SimulationWorld.Step
  → CharacterActor.Step
  → InputFrameBuffer.ResolveLocal → InputManager.IngestFrame
  → GameplayIntentProducer.Step
  → CharacterActionDriver.ProcessGameplayInput
  → CharacterMotor.TickGravity
  → ActionSim.Step（若会话激活；每 World 帧唯一一次）
  → CharacterStateMachine.Tick
      → LocomotionState.Tick → LocomotionStateMachine（转换→ExecuteFrame）→ Motor + Animation
      → ActionState.Tick → ActionRotationDriver.Tick
```

### 相关文件

- `Assets/Scripts/Core/StateMachine/*`
- `Assets/Scripts/Domain/Character/StateMachine/*`
- `Assets/Scripts/Domain/Character/CharacterActor.cs`

---

## 4. Locomotion 动画与相位

### 功能说明

顶层仍为 `Locomotion` 状态；内部由 `LocomotionStateMachine` 驱动 Idle / Start / Gait / PivotTurn / Stop。升档与 Pivot 许可由 **`LocomotionGaitPolicy`**（嵌在 Profile）求值；播片经 **`ILocomotionAnimResolver`**（Walk 横移可解析 `WalkLeft`/`WalkRight`）。敌我差异靠不同 Profile 资产，State 内无身份分支。

### 实现方案

| 项 | 方案 |
|----|------|
| 内层机 | `LocomotionStateMachine` + `LocomotionContext`；Tick = 转换后 `ExecuteFrame` |
| 步态策略 | `LocomotionGaitPolicy`：MaxGait / AllowPivot / SprintAfterRunSeconds |
| 选片 | `DefaultLocomotionAnimResolver`：gait + `MoveIntent` → `AnimationKey` |
| 相位 State | `Idle/Start/Gait/PivotTurn/StopLocomotionState` |
| 逻辑键 | Idle/Walk/WalkLeft/WalkRight/WalkStart/WalkStartLeft/WalkStartRight/Run/Sprint/Start/StartEnd/PivotTurn/StopL/StopR |
| 移动朝向 | Profile.`FacingMode`：`FollowMove`（玩家）/ `FaceCamera`（八向敌）；经 `ResolveMotorRotationMode` |
| 选片 | `LocomotionAnimSet`（Loop/Start×cardinal→Key）+ `DirectionModel` + Gait cardinal 滞回；Clip 在 AnimationProfile |
| 起步 | Start 闩 `ActiveStartGait`/`ActiveStartCardinal`；升档/降档不认 WalkStart* Key 族 |
| FaceTarget | 仅 Profile 声明时读 `LocomotionFacingTargetSource`（SelectedTarget）；玩家 FollowMove 不因自动选敌升格；Motor `FaceTarget`；选片 wish→本地；Pivot 关 |
| FollowInput 位移 | 沿**当前朝向**；朝向以 `CharacterConfig.RotationSmoothTime` 追 wish（单参控制 W→WD 转向时长） |
| 起步选片 | Walk 横向 → `WalkStartLeft/Right`（缺则 `WalkStart`→`Start`）；正向 `WalkStart`；Run → `Start` |
| 映射 | `CharacterAnimationProfile` → `AnimationClip` |
| 相位参数 | `CharacterLocomotionProfile`（阈值、落脚、GaitPolicy、脚步音） |
| 脚步 | `LocomotionFootCycle` 按 `NormalizedTime` 采样标记 |
| 门面 | `CharacterAnimationService.Play`（兼 `ILocomotionAnimClipQuery`） |
| Root Motion | StartEnd/Stop/Pivot 烘焙轨；Pivot：**AnimAuth**（bake pos+yaw）→ **InputAuth**（FollowInput，同 Gait） |
| Pivot handoff | `CharacterLocomotionProfile.pivotAnimAuthNormalized`（默认 0.5） |

### 相位规则（摘要）

```
Idle + 有输入                         → Start（必经）
Start 播完                            → Gait(Walk|Run)，受 MaxGait 钳制
Gait：Policy.Evaluate（跑输入累计）   → Run→Sprint（仅 MaxGait≥Sprint）
Gait 播片                             → AnimResolver（Walk+横向 → WalkLeft/Right）
Start 松输入                          → Stop（StartEnd / StopL/R）
Pivot：Policy.AllowsPivot(Sprint) + |yaw|≥pivotAngle
Stop 任意时刻再输入                  → Start
Dodge 恢复                            → Gait（PendingGait 经 MaxGait 钳制）
```

### 关键参数（LocomotionProfile 默认）

| 字段 | 默认 | 含义 |
|------|------|------|
| `idleInputThreshold` | 0.01 | 静止判定 |
| `stopMinSpeedFactor` | 0.5 | Gait→Stop 相对 runSpeed |
| `pivotAngleDegrees` | 135 | Pivot 夹角 |
| `gaitPolicy.maxGait` | Sprint | 玩家 Full；敌人近战建议 Run |
| `gaitPolicy.allowPivot` | true | 仅 Sprint 可 Pivot |
| `gaitPolicy.sprintAfterRunSeconds` | 3 | Run→Sprint 累计（真源在 Policy） |
| `gaitInputGapGraceSeconds` | 0.15 | Gait 松手宽限 |
| Motor `sprintSpeed` | 9 | 冲刺水平速度 |
| `sprintLean.maxLeanDeg` | 8 | L-DIR4 Visual 倾身；FaceCamera 路径不启用 |
| `sprintLean.leanEngageSmoothTime` | 0.22 | 切入满倾平滑（秒） |
| `sprintLean.leanRecoverSmoothTime` | 0.28 | 回正到 0 平滑（秒） |
| Camera `cameraFollowFacingSmoothTime` | 0.35 | L-DIR5 绕圈；越大弯越缓 |
| Camera `followFacingBackwardDeadzone` | 0.2 | 相机相对 Move.y 低于 -此值则不跟朝向 |

### Profile 配置（Katana / 敌人）

| AnimationKey | 说明 |
|--------------|------|
| Idle / Walk / Run | 基础循环 |
| WalkLeft / WalkRight | 对峙横移（敌人战斗 Profile 必绑） |
| Start / StartEnd / PivotTurn / StopL / StopR | 玩家相位；StartEnd=Run_Start_End |

资产：`Assets/Data/CharacterLocomotion/`（AnimationProfile）；LocomotionProfile 在 CharacterConfig 上引用（可空，运行时默认阈值）。

### Action 状态下的动画锁

进入 `ActionState` 时 `SetLocked(true)`；`LocomotionStateMachine.Exit` 冻结落脚采样。Exit Action 后回 Locomotion 从 Idle 再起（可消费 Resume）。

### 已知限制

- 急停减速曲线等旧 Phase D：**明确不做**（2026-08-12）；Stop/Pivot 靠烘焙根位移
- Start/Stop/Pivot Clip 与落脚标记需人工配置

### 相关文件

- `Assets/Scripts/Domain/Character/Locomotion/*`
- `Assets/Scripts/Domain/Character/Animation/*`
- `Assets/Scripts/Domain/Character/StateMachine/States/LocomotionState.cs`

---

## 5. 第三人称相机

### 功能说明

Cinemachine 2 第三人称跟随；鼠标控制 yaw/pitch；碰撞遮挡；启动时锁定光标。`CameraRig` 对 `CameraRoot` 做滤左右 / SmoothDamp，并支持 Action Camera 窗的 `FollowHold`。`CameraDirector` 持有 CameraLock 与 SkillShot 优先级栈；`CameraShotPlayer` 按本机逻辑动作帧求值内嵌官方 Spline，并在 Director 内部 A/B VCam 间切段。

### 实现方案

**层级结构（运行时创建或复用）**

```
Player
  └── CameraRoot (y = 1.4)     ← 角色跟随目标（硬绑角色）

CameraManager + CameraRig + CameraDirector + CameraShotPlayer（场景对象）
  └── CameraOrbitPivot         ← CameraRig 写 SmoothDamp / FollowHold
        └── CameraPitchPivot   ← pitch 旋转
              └── CM ThirdPerson (CinemachineVirtualCamera)
                    Follow = pitchPivot, LookAt = orbitPivot
```

**Virtual Camera 组件**

- `CinemachineTransposer`：后方 `-followDistance`，LockToTarget，无 damping（平滑在 Orbit 层完成）
- `CinemachineHardLookAt`：注视平滑后的 `orbitPivot`
- `CinemachineCollider`：Default 层遮挡，PreserveCameraHeight

**跟随平滑 / 演出镜头**

- `CameraManager.LateUpdate`：先跟朝向，再 `CameraRig.Sync`，最后 `StageMoveReferenceYaw`
- 首帧、`followSmoothTime <= 0`、或距离超过 `SnapDistance`(3) 时直接吸附
- Active 角色表现根切换后 0.2s 内改用 0.04s SmoothDamp，并完整吸收横向位移；结束后恢复日常滤左右参数
- 对外提供 `SnapFollowToTarget()` 供传送等硬重置
- Action Timeline 的 `cameraShotStates` 是唯一镜头窗真源；`CameraShotSequence` / Preset SO 不存在
- `CameraShotPlayer` 只读 `ActionSimSnapshot.CurrentFrame`；Camera 窗不进入 `EnumerateStates()`，因此 Sim Runner 不执行
- `holdFollow` 钉住进入窗时的 FollowAnchor；窗口退出后从钉点平滑追回
- `CameraShotNotifyState.positionSpline`（官方 `UnityEngine.Splines.Spline`）是机位位置唯一真源；恒速模式按各 Bezier 段 `GetCurveLength` 累计弧长，再通过 `GetCurveInterpolation` 定位段内进度，禁止使用计算首尾直线距离的 `GetPointAtLinearDistance`
- `splineCurveRule` 提供 Linear / ArcUp / ArcDown / ArcLeft / ArcRight 端点预设；由 `CameraSplineCurveRuleUtility` 把首尾点编译为两 Knot Spline；Custom 才保留任意 Knot/Tangent
- `CameraTransformBinding` 只提供 Character / SelectedTarget / World 根来源；空 `AnchorId` 为 Root，自定义部位经 `CameraAnchorProvider` 映射
- Dynamic Binding 逐帧读取 Transform；Snapshot Binding 只在窗口进入时捕获；解析失败不回退固定节点名
- `CameraDirector` 固定复用两台无 Body 的 CM2 SkillShot VCam；换段 Ping-Pong Blend，并更新世界 Pose / FOV / 可选 Impulse
- Action Editor Scene 的预设规则只显示并编辑首尾端点，提供明确起点/终点选择；Custom 才开放全部 Knot、E 旋转、Tangent、插入/删除/平滑；`Scene 构图 → 选中点` 会把 Scene Camera 位置反解到 Reference Binding，并沿 Scene forward 以当前焦距生成 LookAt Binding 局部观察点；FOV 可选择 Keep Shot、Scene View 或 Custom，后两者在当前预览帧写入/覆盖 FOV Curve Key
- Camera 窗选中时按当前预览帧求值出的 Position/LookAt/FOV 绘制视锥；Knot Rotation 仍只控制切线局部朝向
- `ActionEditorCameraView` 是实际构图唯一预览入口：菜单 `ACT/Action Camera View` 或 Scene 浮窗打开；隐藏 Camera 复用 Main Camera 设置并渲染当前场景到缓存 RenderTexture，自动跟随 Shot/Frame/Position/LookAt/FOV，SceneView 保持自由导航
- Camera Inspector 只展示 Spline Knot 数量与 Closed，不再暴露未使用的 Int/Float/Float4/Object 扩展数据；旧 `Debug Scene Camera` 字段与 `SceneView.LookAtDirect` 接管路径已删除；Clipboard 仍递归复制 Spline 隐藏 MetaData
- 旧 `CameraDebugAnchorVisualizer`、`CameraDebugGizmoDrawer` 及常驻 Frustum/LookAt 定位图形已删除，不再干扰 Spline 编辑

**输入 / 跟朝向**

- `CameraManager` 引用玩家 `PlayerController`，通过 `PlayerController.LookInput` 获取非权威视角输入
- Update 累加 yaw/pitch；有 Look 时启动 `lookOverrideResumeDelay` 暂停跟朝向
- 前进/侧移且无抢权、且未在播招：`SmoothDampAngle(yaw → PresentationRoot yaw)`；**禁止**写 Motor 朝向
- 是否在移动：读 `ILocalPlayer.HasMoveIntent`。客机无 Actor，`Input` 为空，禁止再判 `local.Input.HasMoveIntent`
- 是否后退：读本机设备 `ILocalPlayer.MoveInput.y`；低于 `followFacingBackwardDeadzone`（默认 -0.2）则暂停跟朝向，避免后退 wish 与镜头互追转圈
- 出招/闪避/受击：读 `IsPresentingAction`，暂停跟朝向，避免连闪甩镜头
- 朝向源优先 `PresentationRoot`（客机预测体插值锚点）；空座位 `transform` 不转，跟它无法 A/D 绕圈
- `CameraLock` 只请求 `CameraDirector.CameraLockEnabled`；SelectedTarget 无效时不能开启/自动关闭，不写 Targeting/Action/InputFrame

**初始化**

- 确保 Main Camera 有 `CinemachineBrain`
- 按 Tag `Player` 查找 followTarget（若未指定）
- 销毁 legacy `CinemachineFreeLook`（若存在）

### 关键参数（Inspector 默认）

| 字段 | 典型值 | 含义 |
|------|--------|------|
| `cameraRootHeight` | 1.4 | 锚点高度 |
| `followDistance` | 4 | 相机距离 |
| `followSmoothTime` | 0.1 | Orbit 追 CameraRoot 的平滑时间 |
| `switchFollowSmoothTime` | 0.04 | 换人后的快速跟随平滑时间 |
| `switchFollowDuration` | 0.2 | 快速跟随保持时间；期间横向跟随系数临时为 1 |
| `initialPitch` | 15 | 初始俯角 |
| `horizontalSensitivity` | 0.15 | 水平灵敏度 |
| `verticalSensitivity` | 0.15 | 垂直灵敏度 |
| `topClamp` / `bottomClamp` | 70 / -60 | 俯角限制 |
| `invertY` | true | Y 轴反转 |
| `lockCursorOnStart` | true | 启动锁定鼠标 |

### 与移动的协作

`CameraManager` 只把最终 Orbit yaw 提交到 `InputReader` staged 槽；`InputReader.Sample` 固化为 `MoveReferenceYawQuantized`，Motor 仅消费该输入字段。

### 已知限制

- 平滑仅抹平位置顿挫；未按 Action/Locomotion 切换不同 `followSmoothTime`（可后续做方案 C）
- LookAt 已切到 `orbitPivot`，角色急速冲刺时镜头会略滞后于角色身体
- Camera C1 的 LockOn VCam、TargetGroup 与切敌短 Blend尚未实现；Director 当前保留 LockOn 栈槽
- SkillShot Spline / FollowHold / Scene 预览已通过 Unity 脚本编译；Test Runner 与 Play 仍待人工验收
- 自定义 `AnchorId` 需要用户在角色 Prefab 配置 `CameraAnchorProvider`；解析失败时 Shot 不抢权
- LookAt 首版仍是 `lookAtBinding + lookAtLocalPosition`，未提供第二条观察点 Spline 或 Roll 曲线

### 相关文件

- `Assets/Scripts/App/Controllers/Camera/CameraManager.cs`
- `Assets/Scripts/App/Controllers/Camera/CameraRig.cs`
- `Assets/Scripts/App/Controllers/Camera/CameraDirector.cs`
- `Assets/Scripts/App/Controllers/Camera/CameraShotPlayer.cs`
- `Assets/Scripts/App/Controllers/Camera/CameraAnchorProvider.cs`
- `Assets/Scripts/Domain/Camera/CameraSplineEvaluator.cs`
- `Assets/Scripts/Domain/Camera/CameraShotPoseResolver.cs`
- `Assets/Scripts/Domain/Combat/Actions/Definitions/Timeline/CameraShotNotifyState.cs`
- `Assets/Scripts/Editor/Combat/ActionEditor/ActionEditorCameraShotPreview.cs`

### 5.1 唯一战斗目标

**功能说明：** 角色在范围内自动维护一个 `SelectedTargetId`；玩家可在 Locomotion/Action 中左右切换，动作后续旋转、吸附和重定位立即读取新目标。

**实现方案：**

| 项 | 方案 |
|----|------|
| 角色状态 | `CharacterTargetingState` 唯一持有 SelectedTargetId，在 Action 路由前 Step |
| 纯解析 | `DeterministicTargetResolver` 只读整数位置、team/alive 与 SimActorId |
| 自动选择 | 当前无效时最近距离；等距取较小 SimActorId |
| 保持 | 当前目标在 retainRange 内保持，不被新近敌人抢走 |
| 切换 | `TargetSwitchLeft/Right` Pressed + MoveReferenceYaw 环绕排序；Action 中同样生效 |
| 消费 | ActionRotation、TargetAdhesion、MotionCommand、Camera/UI 共读；Locomotion FaceTarget 仅 Profile 声明时消费 |
| 相机 | `ILocalCameraTargetSource` 只映射 Id→ITargetable；CameraLock 不写回 |

**关键参数：** `CharacterCombatConfig.TargetAcquireRangeMeters` 默认/旧资产回退 12m；`TargetRetainRangeMeters` 默认/非法值回退 Acquire+1.5m。

**运行时流程：**

```
InputFrame → CharacterTargetingState.Step
  → DeterministicTargetResolver → SelectedTargetId
  → Action/Locomotion/Motion 本逻辑帧只读
  → Camera/UI 只读表现映射
```

**已知限制：** Input Actions 仍需人工新增 `TargetSwitchLeft/Right` 与 `CameraLock`；Camera C1 的锁定构图/Blend 未实现；完整 Snapshot Restore 归 L3。

**相关文件：**

- `Assets/Scripts/Domain/Simulation/Targeting/*`
- `Assets/Scripts/Domain/Combat/Targeting/CharacterTargetingState.cs`
- `Assets/Scripts/Domain/Combat/Targeting/ILocalCameraTargetSource.cs`

---

## 6. 玩家角色装配

### 功能说明

Scene 中创建 Empty GameObject，挂载 `PlayerController` 并指定 `CharacterConfig` 后，运行时创建模型、`CharacterController` 与纯 C# runtime 服务图。

### CharacterConfig

| 字段 | 作用 |
|------|------|
| `ModelPrefab` | 实例化为玩家根节点子物体，要求子层级能找到 Animator |
| `DefaultLocomotionProfile` | 默认 Idle/Walk/Run 动画映射 |
| `InputActions` | Player ActionMap 输入资产 |
| `Motor` | 移动速度、重力、CharacterController 高度/半径/中心 |
| `CombatProfile` | 战斗模式、出招表与技能入口 |
| `Combat` | teamId、Hitbox/VFX 挂点名、索敌起点名 |

### 运行时装配

- `PlayerController.Awake` 校验 `CharacterConfig`，创建 `InputReader`，调用 `CharacterActorFactory.Create`
- 实例化模型 Prefab，查找 Animator
- Player 根只补齐 Unity 必需的 `CharacterController`
- 构造纯 C# `InputReader`、`CharacterAnimationService`、`CombatModeService`、`ActionResolverService`、`ActionSim`、`CharacterActionDriver`、`CharacterStateMachine`
- 注册纯 C# `HitboxFrameConsumer` 为 Logic Tick 消费者；注册 `ActionVfxPlayer` 为 `IActionNotifyConsumer`
- `PlayerController.OnEnable` 向 `SimulationHost` 注册 `CharacterActor`，OnDisable 对称注销
- `CharacterActor.Step` 统一输入采集、动作路由、重力和状态机；状态自身调度 Locomotion 移动或 Action 旋转

### Editor 操作

在 Unity Editor 中创建 `CharacterConfig` 资产，填写模型 Prefab、输入资产、Locomotion Profile 与 CombatModeProfile；Scene 内只需要 Empty + `PlayerController` + 该配置引用。

---

## 7. 动作系统

> 运行时细节以本节与 [ACTION_SYSTEM_LOCKSTEP_REFACTOR_PLAN.md](../../docs/ACTION_SYSTEM_LOCKSTEP_REFACTOR_PLAN.md) 为准；排期见 MASTER。

### 功能说明

多战斗模式下，玩家通过 `ActionGraph` Entry×Intent 起手攻击/闪避；输入、索敌、起手行为、Cancel 与自动衔接统一由 Graph 节点/边描述。L1B 后 `ActionSim.CurrentFrame` 为纯模拟权威，表现桥只读 Snapshot/Event，Action 内容固定为 60Hz。

### 实现方案

| 项 | 方案 |
|----|------|
| 起手 / 缓冲 | `GameplayIntentBuffer` → `CharacterActionDriver` → `ActionResolverService.TryResolveStart` → `ActionSim.TryStart` |
| 节点 Intent | `ActionGraphNode.Intent = GameplayIntentType`；ActionDefinition 不保存输入语义 |
| 选招策略 | `ActionGraph` Entry / Normal 与 Perfect CancelWindow 边 / `ActionGraphSharedRoute`；顺序组按类型聚合子节点 |
| 六向闪避 | `DirectionalActionResolver` 统一解析前、后、左前、左后、右前、右后；前后扇区半角默认 `30°`，纯左/右输入偏向前侧变体 |
| Cancel 下一招 | 每招一个 Normal、可选一个 Perfect；窗口重叠且同 Intent 时 Perfect 优先 |
| 自动衔接 | `ActionGraphNode.AutomaticTransitions`，目标为节点 Id，支持 AnimationEnd / AtFrame / OnHitConfirm / OnWhiff |
| 高优硬打断 | Action 态：`TryResolveStart(PriorityInterrupt)` → `ActionSim.TryInterrupt`（候选 `interruptPriority` 严格大于当前，且 `IsInterruptibleAtFrame`） |
| 时间轴数据 | `ActionDefinition.Timeline`：`ActionNotify` 点事件（Event/VFX/SFX）+ `ActionNotifyState` 区间窗口 |
| 移动取消 | `CharacterActionDriver` + `CancelWindowNotifyState(Movement)` |
| 招式旋转 | `ActionRotationDriver` + `RotationNotifyState`；节点只声明是否消费 SelectedTarget/平滑覆盖，目标由角色逐帧提供 |
| Runtime Logic Tick | `SimulationWorld` → `CharacterActor.Step` → 唯一 `ActionSim.Step`；窗口、Graph 与结束只读整数帧 |
| 逻辑 / 表现边界 | `ActionSimSnapshot` / `ActionSimEvent` → `CharacterActionPresentationBridge` → Clip Seek / Timeline |
| 命中回流 | `HitboxFrameConsumer` Collect → `CombatHitPipeline` 帧末 Resolve → `IActionHitReceiver.NotifyHit` |
| 命中去重 | 单次动作会话内按 `(HitboxIndex, TargetSimActorId)` 去重；排序键为纯模拟 `SimHitKey` |
| 自动衔接 | 普通 Tick 不再提前解析无输入 Transition；结算后 PostCombat 保持 OnHitConfirm 同帧生效 |
| 帧边界切招 | Cancel / Recovery / 自动衔接在判定帧只排队；下一 World 帧提交目标 frame 0 |
| 卡肉边界 | `AttackHitEvent` 只冻结动画/VFX 表现；禁止 Event Handler 回写 `ActionSim` |
| Graph 策略编辑 | Graph Editor 在普通节点和顺序组子节点内嵌策略折叠区，直接编辑 Intent、索敌、起手行为、战斗模式切换与自动衔接 |
| Motor | `CharacterMotor`（Locomotion 位移）+ `CharacterActor`（重力调度） |

### 关键参数（打断）

| 参数 | 默认 | 说明 |
|------|------|------|
| `ActionExecutionPolicy.interruptPriority` | `0` | 越大越优先；同级不互硬打断 |
| `ActionPhaseNotifyState.interruptible` | `true` | Startup/Active/Recovery 覆盖时参与硬打断；Invincible/SuperArmor 标签不参与 |
| Recovery `allowMovementCancel` | `true` | 有移动输入时退出 Action 返回 Locomotion |
| Recovery `allowEntryRestart` | `true` | 有效动作缓冲按当前 Graph Entry 重开 |
| 无 Phase 覆盖帧 | — | `IsInterruptibleAtFrame` 返回 `true`（默认可硬打断） |
| `GameplayIntentProfile.actionBufferDurationFrames` | `60` | Action 内预输入有效逻辑帧数；过期后不再于 Recovery/收招误触发 |

### 运行时流程（高优打断 + Logic Tick）

```
CharacterActionDriver.ProcessGameplayInput（Action 态）
  → TryPriorityInterrupt(intent)
      → ActionResolverService.TryResolveStart(Origin=PriorityInterrupt)  // Graph Entry
      → ActionSim.TryInterrupt
  → 失败则 Buffer(intent)  // 留给 CancelWindow

CharacterActor.Step
  → 唯一调用 ActionSim.Step
      → CurrentFrame + 1 → ActionSimEvent
  → CharacterActionPresentationBridge.ApplyStep
          → HitboxFrameConsumer.OnCombatFrameAdvanced（只 Collect）
          → ActionTimelineRunner.Dispatch
              → PlayVfxNotify 点触发 → ActionVfxPlayer.OnActionNotify（Resolve attachPointId + 显式 playbackSpeed）
              → PlaySfxNotify 点触发 → ActionSfxPlayer.OnActionNotify（pitch = playbackSpeed）
              → 其他 ActionNotifyState Enter/Tick/Exit
      → CancelWindow / Recovery Entry
          → CancelWindow：同一意图先 Perfect 后 Normal，成功后只排队
          → Recovery Phase：按窗口开关排队 Graph Entry 软重开
SimulationHost 帧末
  → CombatHitPipeline 稳定排序并统一伤害/Reaction/ConfirmHit
  → CharacterActor.ResolvePostCombat
      → Graph 自动衔接排队；自然结束按 TotalFrames 停止
      → NotifyActionEnded → ActionSfxPlayer.OnActionEnded → 0.1s 音量淡出后 Stop
  → PublishAttackHitCommand → AttackHitEvent（仅表现）
      → HitImpactController：Feedback 受击 VFX/SFX（完美吞伤跳过）
      → CameraShakeController / HitStopController（既有）
```

### 7.1 命中受击 Cue（A2）

**功能说明：** Confirm 命中后在逻辑接触点播特效与音效；挥空不播；完美闪避吞伤不播受击 Cue。

**实现方案：**

| 层 | 组件 |
|----|------|
| 接触点 | `HitboxMath.EstimateContactPointOnHurtbox`（攻击盒中心→受击盒最近点） |
| 配置 | `HitFeedbackSettings`：VFX/SFX、相对接触点偏移、随机欧拉范围 |
| 事件 | `AttackHitEvent.HitPoint` / `AbsorbedByPerfectDodge` |
| App | `HitImpactController`：落点=接触点+Offset；`LookRotation * Random.Euler` |
| 调试 | F4 → `CombatHurtboxDebugSettings.ShowHurtboxes` 画逻辑 Hurtbox |
| 卡肉 | 火花 `VfxPooledInstance.SetSpawnOwner(attacker)`，与刀光同窗暂停 |

**关键参数：** `hitImpactWorldOffset` 默认 `0`（相对接触点）；`randomizeImpactRotation` 默认开，Y `0～360`；SFX Volume `0～1`。

**已知限制：** 单 Hurtbox/角色，无多部位表；新招仍须人工绑 Feedback Prefab/Clip。A2 打击感验收 ✅ 2026-08-09。

### 7.2 受击档位裁定（冲击力 × 韧性）

**功能说明：** 档位只由本刀冲击力对目标当前韧性算出。不足则 Flinch（扣血不断招、叠 Additive）；压过则 LightStun 起进 Hit。Shake 不 Play 走跑/出招主轨，不 `SetLocked`。

**实现方案：**

| 项 | 方案 |
|----|------|
| 档位 | `HitReactionKind`：None / Flinch / LightStun / HeavyStun / Launch / Death |
| 裁定 | `CharacterReactionResolver.ResolveKind(冲击力, 韧性)` → `HitReactionCommand` |
| 冲击力 | `HitPayload.interruptLevel` |
| 韧性 | `CharacterCombatConfig.baseInterruptResist` + 当前帧 Phase `interruptResistBonus` |
| SuperArmor | 非 Death 最多 Flinch |
| 执行 | `CharacterReactionService`：Flinch 不 `EnterHit` / 不 `NotifyHit`；Stun+ 旧路径 |
| 表现 | `CharacterActor.IssueFlinch` → `HitFlinchPlaybackController` → `PlayAdditive` |
| 边沿 | Flinch 把 `VitalityEdge` 清成 None，避免幽灵把底轨当受击重播 |
| 删除 | `desiredReaction` 不再参与裁定，避免同一刀被锁死在 Flinch |
| 旧入口 | `ResolveHit` 仅快照硬吸（Stun 边沿） |

**关键参数：**

| 参数 | 默认 | 含义 |
|------|------|------|
| `HitPayload.interruptLevel` | 1 | 冲击力 |
| `CharacterCombatConfig.baseInterruptResist` | 1 | 站立韧性；精英填 3 |
| `ActionPhaseNotifyState.interruptResistBonus` | 0 | 出招窗韧性加成 |
| `HeavyStunExcess` | 2 | 冲击 − 韧性 ≥ 2 → HeavyStun |
| `LaunchExcess` | 4 | 冲击 − 韧性 ≥ 4 → Launch |
| Flinch 键 | `AnimationKey.HitShake` | Additive；不进 Locomotion |

**运行时流程：**

```
CombatHitPipeline.OnHit → Vitality.HitReceived
  → CharacterReactionService
       Resolve(冲击力 vs 韧性 + SuperArmor)
       ConfirmHitReaction
       Flinch → Actor.IssueFlinch → HitFlinchPlaybackController.PlayAdditive
       Stun+  → NotifyHit + EnterHit
       None   → 只保留已结算伤害
```

**已知限制：** 杂兵/精英韧性与各刀冲击力须 Editor 填；Listen 客机 Shake 待 P-HR4。方案：`docs/2026.9.3/HIT_REACTION_IMPLEMENTATION_PLAN.md`。轻击 Play 已验收 2026-09-03。

**相关文件：**

- `Assets/Scripts/Domain/Character/Reactions/HitReactionKind.cs`
- `Assets/Scripts/Domain/Character/Reactions/HitReactionCommand.cs`
- `Assets/Scripts/Domain/Character/Reactions/HitReactionResolveQuery.cs`
- `Assets/Scripts/Domain/Character/Reactions/CharacterReactionResolver.cs`
- `Assets/Scripts/Domain/Character/Reactions/CharacterReactionService.cs`
- `Assets/Scripts/Domain/Character/CharacterConfig.cs`（`baseInterruptResist`）
- `Assets/Scripts/Domain/Combat/Actions/Definitions/Timeline/ActionPhaseNotifyState.cs`
- `Assets/Scripts/Editor/Combat/HitReactionDefaultsMigrator.cs`
- `Assets/Scripts/App/Controllers/Combat/HitFlinchPlaybackController.cs`
- `Assets/Scripts/App/Events/Combat/HitFlinchEvent.cs`
- `Assets/Tests/Editor/Combat/HitReactionResolverTests.cs`

VFX 生命周期：`ActionVfxPlayer` 在招式结束 / 连招切招时**不**强制 Despawn；池化实例由 `VfxPooledInstance` 按粒子与 Animator clip 的较长自然时长（含 `playbackSpeed` / 卡肉冻结）自行回池。Spawn 时 `Rebind`+从头 `Play` Animator；卡肉同步 `Animator.speed=0`。无 `VFXManager` 时回退 `Destroy(lifetime)`。C1 展示包源资产位于 `Assets/Resources/Effect-C1/EffectPackage/`（Prefab 经 `PlayVfxNotify` 引用，非 `Resources.Load` 硬编码路径）。

SFX 生命周期：`ActionSfxPlayer` 使用 `ActionSfx` 下多声道 `AudioSource`（与脚步声隔离）；`OnActionEnded` 对仍在播的声道做 **0.1s（unscaled）** 淡出；连招新 `PlaySfx` 走空闲声道，不 Cancel 正在淡出的旧声道。

编辑器 Scrub 使用 `ActionEditorPreviewSession` 做 Pose/VFX 预览，并与 Runtime 共用无副作用 `ActionFrameQuery` 的段映射、窗口与点事件规则；不执行 `ActionSim.Step`。

**编辑器交互（2026-08-04）**：

- VFX/SFX/Event 点事件在时间轴上绘制为**菱形**（热区按轨高，不随 1 帧条宽缩小）
- Timeline 顶栏 **Zoom**（1×–16×）+ Ctrl/Cmd+滚轮；放大后横向滚动以精确拖帧
- Scrub / 播放 / 工具栏改帧时，playhead 超出 Zoom 可视区会**自动平移**时间轴视图
- Scene Hitbox 线框与 VFX Prefab 按 **Preview Frame** 驱动：粒子 `Simulate` + **Animator Clip 同时间采样**（对齐 showcase 的 playOnAwake + Animator）；Hitbox 仅在窗口激活时可见；无需选中时间轴窗口
- Scene 烘焙根运动：`ActionMotionTrajectorySceneDrawing` 画 Full/Gameplay/Residual 轨迹 + 当前帧落点；`BaseMotionMode=BakedMotion` 时 `ActionEditorPreviewSession` 按表挪动预览根并写 VisualMotionRoot 残差（离开预览还原）
- Hitbox / VFX 均支持 `parentToAttachPoint`：勾选跟随挂点；取消则在进入/触发帧冻结世界空间（运行时 `HitboxFrameConsumer` 缓存 OBB）
- 右侧 Inspector：`ActionNotifySelectionDrawer` 纵向 ScrollView，Hitbox 长表单可滚到底部编辑
- Create：选角色文件夹（如 Unagi），自动保存到其子目录 `ActionDefinition`（无则创建；已有旧名 `ActioniDefinition` 则复用）；默认名可改；左侧列表按文件夹分组
- 时间轴多选：Ctrl 点选 / Shift 同轨范围选；Ctrl+C/V 复制粘贴（可跨 Action，按预览帧对齐）；Delete 删多选
- 同类型多选：右侧改任一字段（含 Hit Payload / VFX Prefab 等）批量写回全部选中窗口；混合类型仅改主选中项
- 拖拽框选：轨道路面空白拖拽矩形多选窗口；Ctrl/Cmd 叠加；单击空白清空

### ActionEditor 对齐状态（2026-08-02）

| 对齐度 | 项 |
|--------|-----|
| ✅ | Runtime `UpdateFrame` 已删除；`ICombatFrameConsumer`、`ActionTimelineRunner`、`ActionNotify` / `ActionNotifyState` 保持整数帧 Schema |
| ✅ | 命中回流、`OnHitConfirm` / `OnWhiff` Transition 条件 |
| ✅ | `CharacterActionDriver` 角色无关输入路由 |
| ✅ | Hitbox/VFX/Cancel/Movement/Rotation 已收敛到 `ActionTimeline`，删除旧双轨数组 |
| ✅ | `ActionEditorWindow`：手动加轨、轨头纵向拖拽排序、窗口拖拽；VFX/SFX 为单帧点事件菱形，Phase 为区间窗口；时间轴缩放 |
| ✅ | Scene 预览按 Scrub 帧显示全部激活 Hitbox / 已触发 VFX（`ActionEditorVfxPreviewExtension` 多实例） |
| ✅ | `ActionSfxPlayer` 运行时点触发；招式结束/打断时 0.1s 淡出；`CharacterAttachPointResolver` 供 VFX/Hitbox 共用 |
| ⬜ | 伤害结算、Hit 状态、GM 热重载 |

### 已知限制

- 现有资产需要在 Unity Editor 的 Phase 轨重建原 `phases[]`，并为 Recovery 配置移动取消 / Entry 重开开关；Agent 未直接修改 `.asset`
- Runtime 只接受 `ActionDefinition.sampleRate=60`；可用 `ACT/Tools/Validate Action 60Hz Readiness` 复核。仓库内已无 30Hz Action；Migrate 菜单保留作幂等兜底
- 硬打断与 Recovery 软重开走 Graph Entry，不要求 Cancel 边；独特连招进位仍依赖 Combo Window + 显式边
- 旧 `perfectFrame`、Cancel 槽 Id 与同类型多窗口不再受支持；资产需整理为一个 Normal 与可选一个 Perfect
- Scene 玩家入口已改为 Empty + `PlayerController` + `CharacterConfig`

### Editor 操作（Prefab）

创建 `CharacterConfig` 后，在 Scene 空物体的 `PlayerController` 上指定该资产；Play Mode 验证：起手、连段、移动取消、索敌旋转、`ActionTimeline` 中的 Hitbox/VFX 与预期一致。

### 相关文件

- `Assets/Scripts/Domain/Combat/Actions/{Definitions,Resolution,Execution,Frames}/*`
- `Assets/Scripts/App/Controllers/Gameplay/PlayerController.cs`
- `docs/ACTION_EDITOR.md`、`docs/ACTION_SYSTEM_LOCKSTEP_REFACTOR_PLAN.md`

---

## 8. 敌人 AI、伤害与生成

### 功能说明

敌人复用玩家的 CharacterActor、Locomotion、ActionGraph 与 Hitbox 管线。`EnemyBrain` 为门闩宿主，决策经 `IEnemyBehaviorRunner`；Hit/Death 不进树。  
**现状：** EnemyBrain 写 `ActionEntryRequestBuffer` 与 `LocomotionDesireBuffer`；角色服务图通过只读接口消费，`ProduceInput` 仅写空 `InputFrame`。契约见 `docs/ENEMY_BEHAVIOR_TREE_PLAN.md` §3.4。

### 实现方案

| 项 | 方案 |
|----|------|
| 配置 | `EnemyDefinition` 组合 CharacterConfig、BrainProfile、**BehaviorTree**、独立 teamId 与 HP；战斗半径/幅度终态迁节点（E-CFG） |
| 可替换契约 | `IEnemyBehaviorTreeAsset.CreateRunner` → `IEnemyBehaviorRunner`；Brain 不持有具体树类型；输出槽终态见 BT PLAN §3.4.2 |
| 行为树资产 | 仅 `customRoot`（SerializeReference）+ `graphLayout`；无 Kind/代码预设种树 |
| 条件节点 | UE 风格**单子装饰**（`ConditionalDecoratorNode`）；失败 Abort Self（Reset 子树） |
| Graph 数据 | `graphLayout` + `nodeGuid`；Mapper Flatten/Rebuild；Validator |
| Graph 编辑器 | 宿主牌连线；Condition/Decorator **叠徽章**；Save 展开回装饰链；A1 已落地；待优化见 `docs/2026.8.11/ENEMY_BEHAVIOR_TREE_BACKLOG_PLAN.md` |
| AI 输出 | Runner → Brain：`LocomotionDesireBuffer` + `ActionEntryRequestBuffer`；无 `AIInputWriter` |
| 分层边界 | Character / Combat 仅依赖 `IMoveIntentSource` / `IActionEntryRequestSource`；EnemyActorFactory 构造注入，Actor 无 Enemy Bind/分支 |
| 招式池 | `RandomSelector`（权重；RNG 可注入/`blackboard.Rng`）；样例 `CreateCombatPool`；**无** `PulseAttack`/`AttackPulse` |
| 冷却 | `CooldownGate` 填秒并暂存；Brain 确认后生效；失败写 `action_entry_retry`；对峙用 `CooldownNotReady` |
| 追击 / 对峙 | Attack→CD→Strafe(CdNotReady)→Chase(CdReady)；节点时间填秒；可选 DistanceBand |
| 攻击占用 | `WaitWhileInAction`：起手后 Running 至离 Action，期间清 Move；**勿**被 IsLocomotion/CdReady 包在 Wait 外层 |
| 敌人步态 | 独立 LocomotionProfile + `GaitPolicy.MaxGait=Run`；对峙 WalkLeft/Right；Run→Walk 硬切 |
| 木桩 | `enableCombatActions=false` 时不建 Runner；Hit 门闩仍消化 |
| 伤害与反应 | 同前：`CharacterReactionService` → `NotifyHit` / `NotifyDeath` → Runner.Reset |
| 生成 | `EnemySpawnController` → `SpawnEnemyCommand` → `EnemyController`；`EnemySpawnSystem` 限制存活数 |

### 关键参数

| 参数 | 默认 | 含义 |
|------|------|------|
| `HitPayload.baseDamage` | 10 | 单个 Hitbox 的基础伤害 |
| `CharacterCombatConfig.maxHealth` | 100 | 玩家默认生命值 |
| `CharacterCombatConfig.reactions` | 空规则集，默认硬直 0.35s | Resolver 按反应类型与 HitReactionId 选择表现 Action；无动作时使用规则集硬直时长 |
| `HurtboxDefinition.localOffset` | (0, 0.9, 0) | 标准人形受击框中心，角色根位于脚底 |
| `EnemyDefinition.teamId` | 1 | 敌人阵营；不继承复用 CharacterConfig 的玩家阵营 |
| `EnemyBrainProfile.enableCombatActions` | true | 木桩关行动 |
| `EnemyBrainProfile.deathDespawnDelaySeconds` | 0.5 | 死亡回收等待 |
| BT `AggroGate.enter/exit` | 10 / 14（样例） | 仇恨滞回（原 Profile 半径） |
| BT `InAttackRange.distance` / Move `stopDistance` | 2 / 1.2（样例） | 攻击与贴身停步 |
| BT Move/Strafe/BackOff `magnitude` | 1 / 0.35 / 1（样例） | 移动幅度 |
| BT `CooldownGate` basic_attack | 1.2s（样例→72 帧） | 普攻冷却；节点填秒 |
| BT `CooldownReady` / `CooldownNotReady` | basic_attack | CD 毕追击 / CD 中对峙 |
| BT `DistanceBand` min dwell | 0.1s（样例） | 可选滞回驻留；节点填秒 |
| BT `WaitSeconds` | 0.5s 默认 | 等待 Task；节点填秒 |

### 运行时流程

```
SimulationWorld.Step
  → EnemyHandle.ProduceInput
       → EnemyBrain.Step（门闩 / 填黑板 / Runner.Tick / 提交 Desire + Entry Request）
       → InputFrameBuffer 写空帧（维持统一 Actor Step 时序）
  → EnemyHandle.Step → CharacterActor.Step(InputFrame)
       → Locomotion/CharacterMotor 读 IMoveIntentSource
       → CharacterActionDriver 消费 IActionEntryRequestSource

CombatHitPipeline（全体 Actor Step 后）
  → CharacterReactionService → EnterHit / EnterDeath
  → EnemyBrain.NotifyHit / NotifyDeath → Runner.Reset
```

### 已知限制

- **真敌**须挂已配置 `customRoot` 的 `EnemyBehaviorTree` SO；空根或未挂树在 Combat Actions 开启时会失败。
- Desire + Request 命令轨已落地（E-MOVE2）；闪避/技能须用 `RequestCombatAction` 指 Entry。
- **E-CFG1：** 战斗距离/幅度在节点；旧树资产须人工补 `AggroGate` + 节点参数，否则仇恨/幅度可能用默认或不进仇。
- 追击直线趋近（`StraightPathQuery`）；寻路见演进计划 E4。
- 旧 Kind 预设资产若 `customRoot` 为空，须在 Graph 编辑器中重新搭树并 Save（无 Fill/默认种树）。
- 旧「Sequence 下挂叶子 Condition」树须改为 Condition→子树装饰链后 Save，否则 Validate 报 child 为空 / 行为不符。
- Graph Undo 以 Save 的 `RegisterCompleteObjectUndo` 为主；画布即时删节点未做完整 Undo 栈。
- 死亡回收当前使用 Destroy；对象池可后续替换。
- EditMode 样例树仅存于 `EnemyBehaviorTreeDefFactory`（测试用），不进运行时资产默认路径。

### 相关文件

- `Assets/Scripts/Domain/Enemy/*`（含 `BehaviorTree/Serialization/`）
- `Assets/Scripts/Editor/Enemy/BehaviorTree/*`
- `Assets/Scripts/Domain/Character/Commands/*`
- `Assets/Scripts/Domain/Combat/Actions/Execution/ActionEntryRequest*.cs`
- `Assets/Tests/Editor/Enemy/EnemyBehaviorTreeTests.cs`
- `docs/ENEMY_BEHAVIOR_TREE_PLAN.md`
- `docs/2026.8.11/ENEMY_BEHAVIOR_TREE_BACKLOG_PLAN.md`

---

## 9. 预留 / 未完成功能

### CharacterStateType 预留枚举

- `Hit = 80`、`Death = 100` — 已有通用 State；玩家受击/死亡动画资产尚未配置

### 空模块目录

`UI/` — 仅 `.gitkeep`；Enemy 与 Combat Damage 已实现

---

## 变更日志

| 日期 | 变更 |
|------|------|
| 2026-08-31 | Party P-SW0 / P-SW1 骨架：CharacterId/Definition/Loadout、单键 SwitchCharacter、顺序槽位协调器；PlayerController 切到 Loadout，三 Actor 与联网尚未接 |
| 2026-09-01 | Party P-SW1：每槽稳定 Actor/网络实体、Active/Exiting 输入与显隐、SwitchIn 外部意图、Owner 阵容载荷、累计切人 ACK 和 Debug HUD；Editor Graph/Test/Play 待验 |
| 2026-09-01 | Party 普通退场改为双规则：空闲播完整 SwitchOut；有招进入首次 Recovery 后停止并隐藏，不再等待整个 Action |
| 2026-09-01 | Party 退场最终规则：所有普通退场均进入 SwitchOut；原招 Recovery 仅负责切入，只有 SwitchOut 自身 Recovery 可隐藏 |
| 2026-09-01 | Party 普通登场落点改为旧角色局部右侧 600mm；客户端预测与 Dedicated 权威共用确定性算法，并经静态碰撞解析 |
| 2026-09-01 | 相机检测 Active 角色表现根切换后短暂启用 0.04s 快速平滑与完整横向跟随，0.2s 后恢复日常参数 |
| 2026-09-02 | 修复切人 VFX 泄漏：已处于 Recovery 的退场原招不再多推进一帧；RemoteProxy 新动作/重启不再从 frame -1 补播整段历史 Notify |
| 2026-09-02 | 修复 Observer 二次登场残留：远端角色隐藏前回收所属 VFX；重新显形时清空退场前插值历史并直接落到当前权威位置 |
| 2026-09-02 | 本机阵容生命周期接入同一可见性清理接口：`CharacterActor` 转入 Inactive/Dead/Empty 前回收所属 VFX，避免本机再次 SwitchIn 时复活旧特效 |
| 2026-09-02 | 修复 P-SW1 后敌人感知根停在出生点：`RemotePlayerSeat` 使用稳定锚点并在普通切人时重挂到当前权威槽位根 |
| 2026-09-03 | 受击档改为冲击力对韧性；删除 `desiredReaction`；不足 Flinch，持平起 LightStun |
| 2026-09-03 | P-HR3：`baseInterruptResist` + Phase `interruptResistBonus` 进 Service；OnValidate/菜单只补空字段；轻击 Play 已验 |
| 2026-09-03 | P-HR2：Flinch 不停招、不锁 Locomotion；`HitFlinchPlaybackController` 只 `PlayAdditive`；Stun+ 仍 EnterHit |
| 2026-06-17 | 初版：移动、输入、状态机、动画、相机、Prefab 文档化 |
| 2026-06-17 | 动作系统 §7：ComboSequence、CombatMode、ACTION_EDITOR 对齐摘要 |
| 2026-06-21 | ActionEditor 准备重构：CharacterActionDriver、UpdateFrame、Phase/Event 骨架、命中回流 |
| 2026-06-23 | QFramework 风格架构改造：CharacterActor、ActionExecutor、ACTGameArchitecture、ApplyHitCommand、AttackHitEvent、TargetSystem |
| 2026-06-29 | QFramework 式强类型契约：System/Controller/Command/Query/Event 基类与 Editor 边界校验，命中与索敌 Domain 入口移除架构单例依赖 |
| 2026-07-05 | 动作系统 Resolver 重构：`ActionResolver`(Single/Combo/Directional) + `ActionResolverService` 承接起手/连段/Dodge 方向/Cancel 选招；`ActionExecutor` 收敛为纯播放器；`Combat/Actions` 分 Definitions/Resolution/Execution/Frames；`IActionComboInput`→`IActionInputBuffer` |
| 2026-07-09 | ActionNotify 时间轴重构：新增 `ActionTimeline` / `ActionNotify` / `ActionNotifyState` / `ActionTimelineRunner`；Hitbox/VFX/Cancel/Movement/Rotation 改为统一 Timeline 数据真源并删除旧字段路径 |
| 2026-07-10 | VFX/SFX 改为区间窗口（`naturalDurationSeconds` / `playbackSpeed`）；新增 `ActionEditorWindow` 手动加轨与拖拽编辑；`ActionVfxPlayer` 改窗口 Enter/Exit 消费 |
| 2026-07-13 | VFX/SFX 改回点事件：显式 `playbackSpeed`、`PlayVfxNotify.attachPointId`、`CharacterAttachPointResolver`、`ActionSfxPlayer`；删除窗口派生倍率路径 |
| 2026-07-12 | 动画改为 Clip + 薄层 Playable：`IAnimationPlayback` / `PlayableAnimationPlayback`；Profile 映射 Clip；HitStop 走 `SetSpeed`；废弃 Animator Controller 业务依赖 |
| 2026-07-12 | `ActionDefinition` 多段 `ActionAnimationSegment[]`：同招顺序播多 Clip；`ActionExecutor` 段边界自动切；旧 `animationClip` OnValidate 迁入 segments |
| 2026-07-13 | 相机方案 B：`CameraOrbitPivot` 对 `CameraRoot` SmoothDamp；LookAt 改为 `orbitPivot`；新增 `followSmoothTime` / `SnapFollowToTarget` |
| 2026-07-16 | VFX：连招切招不再强制回收；`VfxPooledInstance` 按自然生命周期（含 playbackSpeed）自行回池 |
| 2026-07-18 | Locomotion Phase/FootCycle：`LocomotionService`（Start/Gait/PivotTurn/Stop）、落脚脚步、`ApplyLocomotion` |
| 2026-07-18 | 拆分 Run/Sprint：满输入先进 Run，持续 `sprintAfterRunSeconds` 后 Sprint；Pivot 仅 Sprint |
| 2026-07-18 | Locomotion 方案 B：Stop/Pivot 烘焙根位移轨（`LocomotionRootMotionBaker`）+ 运行时采样驱动 |
| 2026-07-19 | Stop 全程可取消进 Start；移除 `stopCancelNormalized`；Pivot→Stop 用转身目标朝向 |
| 2026-07-19 | Start 急停播 `StartEnd`（Run_Start_End）；Gait/Pivot 仍用 StopL/R；烘焙轨含 StartEnd |
| 2026-07-19 | 输入语义化：GameplayIntentProfile/Producer/Buffer；Action Trigger 改为枚举；SprintAttack、PressedThenLong、Dodge 后直入 Sprint |
| 2026-07-19 | 动作优先级打断：`interruptPriority` + `TryInterrupt`；Action 态高优走 Graph Entry 硬切；CancelWindow 连招路径不变；`IsInterruptibleAtFrame` 无 Phase 默认可打断 |
| 2026-07-19 | `DirectionalActionResolver` 改为统一六向闪避解析；删除纯左/纯右字段及 Locomotion 起手强制前闪/转向旧路径 |
| 2026-07-19 | Action Graph 可视化节点新增 `Variant Resolver` 编辑与保存；修复 Graph Editor Save 未写回 Resolver 引用的问题 |
| 2026-07-22 | TurnBack 固定锁根 0.08 秒后，将实时输入相对初始折返输入的方向差叠加到角色根；避免绝对输入朝向与 Clip 自带约 180° 转身重复累加，烘焙位移同步重定向 |
| 2026-07-22 | Locomotion 内层改为纯状态机：删除 `LocomotionService`；新增 `LocomotionStateMachine` / `LocomotionContext` / 五相位 State；`CharacterContext.LocomotionStateMachine` |
| 2026-07-23 | `ActionSfxPlayer`：专用 `ActionSfx` AudioSource；`OnActionEnded`（打断/切招/自然结束）`Stop` 未播完动作音效 |
| 2026-07-22 | 新增 `GameplayIntentType.AttackRelease`（攻击键松开语义）；供蓄力释放等 Action.Trigger 使用；Profile 需映射 Released→AttackRelease |
| 2026-07-23 | Cancel 同槽多缓冲意图按 `GameplayIntentCancelPriority` 降序解析（LongPressedAttack &gt; Attack），避免连段边抢赢蓄力 |
| 2026-07-23 | 蓄力修复：自动 Transition 回写 Graph 游标；连段 Cancel 保留 LongPressedAttack；Locomotion 起手清残留 AttackRelease 防秒放 |
| 2026-07-25 | ActionGraph 稀疏路由：显式边仅保留独特拓扑；新增 SharedRoute、Recovery Phase→Entry、Directional 逻辑节点；删除 Recovery Cancel 与 ComboResolver；输入缓冲增加 0.15s 过期 |
| 2026-07-25 | Phase 收敛到 `ActionTimeline.phaseStates`；Action Editor 开放 Phase 轨；Recovery 窗口集成移动取消与 Entry 重开；删除独立 `ActionPhase` 数据路径 |
| 2026-07-25 | Action Editor 手动轨道支持拖拽换序：轨头手柄、插入线、松开写回 `timeline.tracks`，完整支持 Undo |
| 2026-07-26 | Perfect 独立窗口：CancelWindowType=Normal/Perfect；允许重叠，同一 Trigger 优先 Perfect；删除 perfectFrame 分割路径 |
| 2026-07-25 | 新增 DodgeAttack 语义：GameplayIntentProfile 通过 IsDodging 条件将闪避 Action 中的 Attack Pressed 映射为闪避攻击 |
| 2026-07-29 | 敌人系统接入：EnemyDefinition/BrainProfile、五态 AI、AIInputSource、共享 CharacterActor、伤害/Hit/Death 闭环、Spawn/Despawn 与玩家对称 Hurtbox |
| 2026-07-29 | 敌人联调修正：EnemyDefinition 独立持有 teamId；默认 Hurtbox 中心抬高；CharacterConfig 增加玩家受击/死亡 Action |
| 2026-07-29 | 动作配置收敛：删除 EnemyDefinition 的 hitStunAction/deathAction；玩家与敌人统一读取 CharacterConfig.Combat |
| 2026-07-29 | 命中反馈修正：Hit 与实际扣血解耦；自击过滤覆盖完整角色层级；玩家镜头只响应玩家主动命中 |
| 2026-07-29 | ActionDefinition 职责重构：输入/索敌/起手/自动衔接迁到 ActionGraphNode；伤害与反馈迁到 HitPayload；Controller 通过 CharacterReactionResolver 选择受击/死亡 Action |
| 2026-07-30 | 角色反应链路收敛：CharacterReactionService 统一玩家/敌人 Health 事件；Resolver 直接产出状态请求；默认硬直时长归 CharacterReactionSet，删除 CharacterConfig/EnemyBrainProfile 双真源 |
| 2026-07-30 | Graph Editor 增加节点内联策略编辑；命中去重改为每个 Hitbox 窗口×目标一次；HitState 支持每次有效命中强制重入并保留启动失败硬直回退 |
| 2026-07-31 | Lockstep L0A：新增 60Hz SimulationHost/World、稳定 SimActorId 与纯 C# asmdef；玩家/敌人删除分散 Controller Tick，渲染输入边沿先汇聚再由固定帧消费 |
| 2026-07-31 | 修复 L0A 移动抖动：新增 CharacterPresentationBridge 前后 Pose 插值与表现锚点，相机改跟随插值根；SmoothDampAngle 显式使用固定逻辑步长 |
| 2026-07-31 | 修复固定帧后攻击转向变慢：ActionRotationDriver 不再隐式读取 Time.deltaTime，并在退出 Action 时清空旧旋转速度 |
| 2026-08-01 | Lockstep L0B：删除 PlayerInputFrame/ICharacterInputSource/AIInputSource；新增量化 InputFrame、输入历史与 World Input Produce 阶段；Hold/Buffer/AI 冷却改整数帧 |
| 2026-08-01 | Lockstep L0C：Hitbox 改为 Collect→稳定排序→帧末 Resolve；新增 SimHitKey/PostCombat，删除 ApplyHitCommand、InstanceId 去重与 Event→ActionExecutor 卡肉回写 |
| 2026-08-01 | Lockstep L1A：ActionSession 整数帧权威、ActionFrameClock 30→60 整数换帧、单次 Action Step、下一 World 帧切招，以及 Hit/Death 整数帧收尾 |
| 2026-08-01 | Lockstep L1B：纯 `ActionSim` + Snapshot/Event 表现边界、共享 `ActionFrameQuery`、60Hz 迁移工具；删除 ActionExecutor/Session 与 30Hz Runtime 路径 |
| 2026-08-02 | L1B 收口：确认全部 ActionDefinition 为 60Hz；新增 Validate Readiness；Editor/VFX 默认采样率改为 `ActionSim.LogicHz` |
| 2026-08-02 | L2/M0：`ActionBakedMotion` + 双文件夹命名匹配烘焙（`ACTGame/Motion/Bake From Folders...`）；不生成 InPlace |
| 2026-08-02 | L2/M1：表现桥查表位移；表就绪禁用 OnAnimatorMove；`ActionMotionRuntimePolicy` |
| 2026-08-02 | 运动表取消烘焙/施加 yaw；朝向仅 ActionRotation（索敌/输入）；位移烘焙不再用 RootQ 投影 |
| 2026-08-02 | L2 HitStop：`ActionSim.freezeFrames` + Pipeline `RequestHitStop`；删除 HitStop 秒制倒计时 |
| 2026-08-02 | L2 Locomotion：`LocomotionRootMotionTrack.TryGetFrameDelta` + Player 整数帧；删除 NormalizedTime 位移权威 |
| 2026-08-02 | L2 MotorSim：`CharacterMotorSim` 水平权威；Locomotion/动作表/RM 经 Motor；CC 仅临时重力与 XZ 跟随 |
| 2026-08-02 | 锁步方案定案：角色互撞软弹开；联网完整预测回滚（撤销「仅齐帧」非目标） |
| 2026-08-02 | L2 软弹开落地：`SoftBodySeparation` + World 帧末；CharacterActor/EnemyHandle 参与 |
| 2026-08-02 | 软弹开质量比 + `softBodyImmovable`（大体型怪像墙） |
| 2026-08-02 | L2 逻辑 Hitbox：`SimCombatPose` + MotorSim 根；删除 Transform 世界盒权威与层级自伤判断 |
| 2026-08-02 | Action Editor UX：点事件菱形、时间轴 Zoom（含 Ctrl+滚轮）、VFX Scene 预览按 Scrub 帧多实例驱动（无需选中窗口） |
| 2026-08-04 | Action Editor UX：playhead 自动跟视口、Create 选文件夹+默认命名、左侧列表按文件夹分组 |
| 2026-08-04 | L2 静态碰撞：`SimStaticCollisionWorld` + `StaticCollisionBake` Editor 烘焙；Host 共享 CollisionWorld |
| 2026-08-04 | L2 重力迁出 CC：`CharacterMotorSim` 竖直权威；`CharacterMotor` 只 Sync 根位姿 |
| 2026-08-04 | L2/M2：Bake Dirty Only、Dirty 指纹黄条、Validate Motion Dirty 菜单 |
| 2026-08-06 | Wave 0：`ActionMotionSourceClassifier` 全库审计；烘焙轨迹 Scene 预览；Motor/相机锚点 Gizmo；`CombatDebugHudController` + `CharacterDebugSnapshot`（N0） |
| 2026-08-06 | Wave 1：`ForwardSigned`；`ActionBaseMotionMode`+迁移；相机 `lateralFollowFactor`；Motor 读 Orbit `PlanarForward` |
| 2026-08-06 | Wave 2 核心：`CharacterVisualMotionRoot` + 残差派生；Gameplay→Motor，Residual→模型；未删 RM 回退 |
| 2026-08-06 | Wave 3：`CharacterResourceSim`/Gate/Spec；Pipeline GrantOnHit；`Special`/`Ultimate` Intent；同键 EX 选形；HUD Next Special；卡肉跳过资源 Step |
| 2026-08-07 | GAS G1：`Domain/Combat/Numeric`（AttributeSet/Aggregator/Flags/NumericSystem）；EditMode `NumericSystemTests`；未接 Actor/Pipeline |
| 2026-08-07 | GAS G2：`EffectDefinition`/`EffectContainer`（Instant/Duration/Periodic + 叠层）；`NumericDebugSnapshot`；`EffectContainerTests` |
| 2026-08-07 | GAS G3：`NumericCostGate`+Spec 编译器；Factory/Host/Pipeline/Vitality；删 ResourceSim/旧 Health；完美窗/无敌早退 |
| 2026-08-07 | GAS G4：`DamageNumericCalculator`；Outgoing/IncomingDamageMult；DOT Health handler 无 Reaction |
| 2026-08-08 | GAS G5：旧权威删除确认；Snapshot/HUD（Effects/Flags/ATK）；文档完成态；Resources 仅作者壳 |
| 2026-08-08 | Wave 3.4：`PerfectDodgeAttack` Intent；Producer 缓冲内劫持攻击键；Begin 清 Flags；Cancel 优先级 93 |
| 2026-08-08 | Wave 2.5：删 Action `useRootMotion`/`LegacyResolve`/`ForwardOnly` 与 Animator RM→Motor |
| 2026-08-08 | A2：`HitFeedbackSettings` 受击 VFX/SFX；`HitImpactController` 订 `AttackHitEvent`；PD 吞伤跳过 Cue |
| 2026-08-08 | Action Editor：同类型多选窗口支持右侧属性批量应用 |
| 2026-08-08 | Action Editor：轨道路面拖拽框选多窗口 |
| 2026-08-08 | `ActionSfxPlayer`：打断/结束改为 0.1s 音量淡出（`ActionSfxFadeDriver`） |
| 2026-08-08 | 受击 Cue：接触点=攻击盒中心→Hurtbox 最近点；随机旋转；F4 Hurtbox 线框 |
| 2026-08-08 | 动作 SFX 多声道淡出（连招不掐断） |
| 2026-08-08 | Action Editor：Scrub 展示烘焙根运动轨迹/位移；右侧 Inspector 纵向滚动 |
| 2026-08-09 | Hitbox `parentToAttachPoint`：世界空间冻结盒（对齐 VFX）；编辑器预览同步 |
| 2026-08-09 | Wave 4 P0～P2：TargetAdhesion（连线动态+剩余帧均摊）+ SoftBodySuppress 接线；Editor MotionModifier 轨；Relocate 未接 |
| 2026-08-09 | Action Editor：选中 MotionModifier 时 Scene 假敌球 + Adhesion 修正轨迹/预览根 |
| 2026-08-09 | TargetAdhesion 方案 A：只补朝向前方缺口，过冲不倒拖 |
| 2026-08-09 | Wave 4 位移切片 + 打击感优化：Branch_02 Editor 验收收口 |
| 2026-08-09 | Wave 4 P3：MotionCommand → ActionMotionResolver 接线（Relocate/SnapFacing） |
| 2026-08-09 | Wave 4 出口收窄为位移；Wave 5 撤出大招演出；LockOn/SkillShot/Finisher 全归 Camera 篇 |
| 2026-08-09 | A2 打击感（命中 VFX/SFX）验收；日计划下一项改为 A5 BT |
| 2026-08-09 | BT-1：`IEnemyBehaviorRunner` + 自研近战树；删 EnemyBrain Idle/Chase/Attack switch |
| 2026-08-09 | BT-2 调试：NamedNode 路径、EnemyController Gizmo/日志、Create/Validate 菜单 |
| 2026-08-09 | BT-1 Play 验收；BT-2 Custom SerializeReference 节点定义 + Inspector Fill |
| 2026-08-09 | 修复 Action→Idle 模型抖动：`BlendToZero` 期间禁止 `ApplyLogicLocalPose` 回写；回锚结束前后快照一并清零；新动作起手取消未完成 Blend |
| 2026-08-09 | VFX 池化支持 Animator：Spawn Rebind/从头播、寿命取粒子与 clip 较长者、playbackSpeed/卡肉同步 `Animator.speed`；导入 C1 包至 `Assets/Resources/Effect-C1/` |
| 2026-08-09 | Action Editor VFX 预览：`SampleAt` 同步粒子 Simulate 与 Animator Clip 采样；`Restart` 对齐 playOnAwake + Animator 从头播 |
| 2026-08-09 | BT-E1：`AIInputWriter` 多按钮脉冲；`CooldownTable`/`CooldownGate`；BackOff/Strafe/PulseDodge；Kite 预设；删单字段攻击 CD 与 `BasicAttackCooldownReady*` |
| 2026-08-09 | BT-E2：`EnemyBehaviorGraphLayout` + `GraphMapper` Flatten/Rebuild + `Validator`；Custom `Wrap` 始终带 Debug 名 |
| 2026-08-09 | BT-E3：`EnemyBehaviorTreeEditorWindow` GraphView MVP（调色板/连线/Inspector/Save/模板/Play 高亮） |
| 2026-08-10 | 文档：敌人 AI 输出槽终态改为 Desire+Request；OPT B1/B3 并入 8.10；代码仍为 AIInputWriter 过渡 |
| 2026-08-10 | E-CFG1：BT 节点自带距离/幅度；AggroGate；薄 BrainProfile；删黑板 Profile 读参 |
| 2026-08-10 | E-ST1：DistanceBand 滞回条件 + CreateMeleeStanceLoop 样例 |
| 2026-08-10 | E-REQ1：EnemyCombatRequest + RequestCombatAction + Driver 按 Entry 起手 |
| 2026-08-10 | E-REQ2：RandomSelector + CreateCombatPool；删除敌人 PulseAttack/AttackPulse 路径 |
| 2026-08-10 | BT Editor：RequestCombatAction Entry 下拉；Graph 只读反查 EnemyDefinition→CombatProfile（删 EditorActionGraph / Action 反查） |
| 2026-08-10 | E-MOVE1：EnemyLocomotionDesire + Buffer；Brain 停 SetMove；Actor 覆盖 MoveIntent |
| 2026-08-10 | E-MOVE2：删除 AIInputWriter 与 PulseDodge/Heavy/Skill；ProduceInput=Empty |
| 2026-08-11 | 命令源分层：LocomotionDesire / ActionEntryRequest 上提为通用契约；CharacterActor 删除 Enemy Buffer 与 InputManager 覆盖路径 |
| 2026-08-11 | 对峙循环：CdReady Chase / CdNotReady Strafe；CooldownGate/Dwell/Wait 节点改秒制 |
| 2026-08-11 | L-DIR4：`SprintLeanModel`→`VisualMotionRoot` Roll；L-DIR5：`CameraManager` yaw 跟朝向绕圈 |
| 2026-08-11 | FollowInput：水平位移沿当前朝向；与 `RotationSmoothTime` 单参决定 W→WD 转向时长 |
| 2026-08-12 | PivotTurn 两段式：AnimAuth（bake pos+yaw）→ InputAuth（FollowInput）；删 PivotTarget/偏移转向 |
| 2026-08-12 | L-DIR1：`FacingMode` + `LocomotionAnimSet` + `DirectionModel`；循环选片不再硬编码 WalkLeft/Right |
| 2026-08-12 | L-DIR2：AnimSet Start 表；`ActiveStartGait`；Gait `cardinalMinDwellFrames` 滞回 |
| 2026-08-12 | L-DIR3：FaceTarget 旋转+软锁；本地 cardinal；锁定禁 Pivot；相机跟朝向关闭 |
| 2026-08-12 | Locomotion Play 验收关闭（L-DIR1～5 + Pivot）；旧案 Phase D 减速曲线不做 |
| 2026-08-13 | C-AT0～3 代码重构：MoveReferenceYaw 入 InputFrame；唯一 SelectedTarget + Action 中切敌；删除 PlanarBasis、CombatTargetLock、ActionTargetId 与表现 late-bind |
| 2026-08-13 | FollowMove 不再因 SelectedTarget 自动升格 FaceTarget；玩家锁面仅攻击窗口/显式 FaceTarget Profile |
| 2026-08-14 | 联网主路径更正为状态同步（删 TECHNICAL 内过期 FramePacket 句）；补服务器代码规范索引 |
| 2026-08-14 | NS0：`ILocalPlayer` / `LocalPlayerService`；敌人感知最近玩家根；玩法删除 Find 唯一 PlayerController |
| 2026-08-14 | NS1：复制 Snapshot/Tick/Command + Codec + Loopback；EditMode 往返与 60 帧单调 |
| 2026-08-14 | NS2：RemoteProxy + Loopback 同机幽灵；Host AfterLogicStep 打包；不 Collect |
| 2026-08-14 | 朝向调试箭头解耦为 `ICharacterFacingDebugTarget`；幽灵同步黄/品红箭（wish 走 moveV*） |
| 2026-08-15 | 幽灵 Locomotion：复制归一化时间并硬切 Seek；工厂关掉 Animator RM |
| 2026-08-15 | NS3：PredictedLocomotionDriver + 纠偏单测 + 同机预测预览；Host 不预测 |
| 2026-08-15 | NS3 表现：FollowInput 转向、Sprint 倾身拷贝、出招/转身贴齐权威以免 10Hz 吸附 |
| 2026-08-15 | NS3 Play 验收关闭；NS4：PredictedActionDriver + 命中/生命边沿复制 + 敌人幽灵 |
| 2026-08-15 | NS4 Play 验收关闭；NS5：UDP 房间 + Listen Host/Client + 稳定 actionId + 10s 空闲剔除 |
| 2026-08-15 | NS5：ParrelSync 克隆自动当 Client（反射探测，不硬引用包） |
| 2026-08-15 | NS5 客机：FrameHint 不再当权威帧丢包；相机改跟预测体；本机走跑/起手 Clip |
| 2026-08-15 | NS5 客机手感：渲染帧合并输入、命令批冗余、CarryForward 不下发旧 Hint、转身/出招贴齐、走跑 Tick |
| 2026-08-15 | NS5 客机表现：单步 Apply、Proxy 过点 VFX/SFX、命中下行落点、克隆端受击 Cue |
| 2026-08-15 | NS5 客机 Locomotion：本地 GaitPolicy 升 Sprint；松手等权威 Stop；Idle↔走跑淡入 |
| 2026-08-15 | UE1：客机本机 `AutonomousLocomotionRunner`；删除房间/预览 `ResolveSelfKey`；纠偏相位重放仍待 UE2 |
| 2026-08-15 | UE1 复验：走跑禁止 `SyncRootPoseFromSim`；Prefill 含 Directional 变体；走跑硬吸 2m |
| 2026-08-15 | UE2：`LocomotionSavedState` + `IPredictedLocomotionReplay` Restore/Replay；闪避后 SprintAfterDodge |
| 2026-08-15 | UE3：删除 `PredictedLocomotionVisual` 猜片；相位判断并入 `ReplicationPresentationAlign` |
| 2026-08-15 | 客机 L-DIR5：`HasMoveIntent` 走设备采样；纠偏后 `SnapPresentationToSimulation`；吸附宽限 150mm/8 包 |
| 2026-08-15 | 走跑+Runner 纠偏默认 2m：禁止 50mm 每包 Restore+Replay（客机卡顿） |
| 2026-08-15 | 出招/闪避禁止每包 SnapPresentation；`IsPresentingAction` 时相机暂停跟朝向 |
| 2026-08-15 | UE4：`AutonomousActionRunner` 只读 ActionSim；删除 `PredictedActionDriver` |
| 2026-08-15 | 客机出招表现：自然结束不重播延迟招；连招超前不误 Cancel；权威卡肉暂停本机推帧 |
| 2026-08-15 | 按已落地代码整理网络同步实现说明（后续阅读入口迁到 `NETSYNC_FROM_JOIN_TO_HIT`） |
| 2026-08-15 | CA1：客机同一 `CharacterActor` + `ReplicationSeat.Autonomous`；删除 Runner / CreateAutonomous |
| 2026-08-15 | CA2：`RemoteCharacterProxy` 只读 ITargetable 进 TargetSystem；OnHit 空操作 |
| 2026-08-15 | 客机注入 WorldQuery：TargetAdhesion / Relocate / SoftBodySuppress 与 Host 同一套桥 |
| 2026-08-15 | 客机穿敌吸附/关碰撞窗与权威卡肉：纠偏只 Ack，禁止 2m 硬吸拉回 |
| 2026-08-15 | 客机预测卡肉：`PredictedHitStopConsumer`；删除权威 Freeze 拖时钟 / FollowAuthorityAction |
| 2026-08-15 | 删除 Host 同机预览：`RemoteGhostViewController` / `PredictedClientPreviewController` / `SetAutonomousPredictMode` |
| 2026-08-17 | NetSync W0：新增 Codec Golden Bytes / Room 执行顺序测试；F3 HUD 增加 Tick/Command 字节、Proxy 与预测 pending 基线观测 |
| 2026-08-17 | NetSync W1：新增零依赖 `ACTNet.Core` 身份/版本/结果/Metrics/有界小端 Buffer；Room/Replication Codec 切换 Core 并删除重复私有 Reader/Writer |
| 2026-08-17 | NetSync W2 Transport：新增 `ACTNet.Transport`、多连接 Loopback 与 ConnectionId UDP；Host/Client 单轨切换 `INetTransport`，删除方向固化旧接口与实现 |
| 2026-08-17 | NetSync W2 Session：新增 `ACTNet.Session`、连接/玩家注册表、Join/Heartbeat/Kick 状态机与 FakeGame 三连接测试；Composition Root 注入 Session，删除 Room 控制 DTO、握手 switch 与 IdleTracker |
| 2026-08-17 | NetSync W3 Runtime 基础：新增纯 C# `ACTNet.Replication`，提供 Version 1 Frame Codec、Schema Registry、Server full-set 生命周期差分、Client 原子应用与 Sequence 丢旧；尚未切换 Character 生产路径 |
| 2026-08-18 | NetSync W3 Character Adapter：Snapshot 字段布局收敛为 `ActorReplicationSnapshotCodec`；新增纯 C# `ACTGame.Networking`、`CharacterSnapshotSchemaV1` 与 stableKey Archetype Catalog；尚未切换 Host/Client |
| 2026-08-18 | NetSync W3 生产切换：Host/Client 单轨使用 `ReplicationFrame` 显式生命周期与 Sequence；hint/hits 迁入 V1 ApplicationPayload；删除 `AuthorityTick`、缺 Tick 即销毁和首敌配置回退 |
| 2026-08-18 | NetSync W3 出口测试：Replication Runtime 用真实 V1 Frame Codec 覆盖中间 Update 整帧丢失、乱序旧帧和双 Archetype；生产 Play 已验收 |
| 2026-08-18 | NetSync W4 Authority Adapter 首切片：远端 InputFrame 灌入、权威角色 Capture 与 FrameHits ActionId 补齐迁出 RoomHost；删除 Room 内对应 Gameplay 实现，仅保留单轨调用与 Frame/Session 编排 |
| 2026-08-18 | NetSync W4 Session Handler 切片：Guest Authority Actor 创建、App/Simulation 注册及断线逆序清理迁出 RoomHost；Room 通过最小服务委托注入 Architecture 能力，仍独占 Session Accept/Reject |
| 2026-08-18 | NetSync W4 Owner Adapter 切片：Owner ActorId 门禁、HP 覆盖、Action Ack、Locomotion Reconcile、Hit/Death 硬吸和预测历史迁出 RoomClient；Adapter 边界使用 SimActorId，避免网络身份类型泄漏到 ACT 预测接口 |
| 2026-08-18 | NetSync W4 Observer/Proxy 切片：Schema/Archetype 校验、Proxy 显式生命周期、TargetSystem 与 View 清理迁出 RoomClient；删除 Domain 旧 Factory 类型/文件，App 层 `ActRemoteProxyFactory` 成为唯一装配入口 |
| 2026-08-18 | NetSync W4 ActContentRegistry 切片：动作 Catalog、角色 Archetype 与 Unity 配置映射合并为唯一内容真源；删除 `CharacterReplicationContentRegistry`，Host/Client 与 Adapter 不再独立持有 Action Catalog |
| 2026-08-18 | NetSync W4 Character Schema Capture 切片：`ActCharacterSnapshotSchema` 统一 CharacterActor Capture 与 V1 编解码并注册到生产 Schema Registry；删除独立 `CharacterReplicationCapture` |
| 2026-08-18 | NetSync W4 Room Facade：新增 Host/Client Gameplay 与内容预填 Service；Room 删除 Character/Config/Proxy/Hit Cue/HitStop 具体实现，仅保留 Session 收发、固定帧调度与 HUD；新增 W4 架构边界守卫 |
| 2026-08-18 | NetSync W4/M1 验收关闭：Authority/Owner/Observer、Room 架构守卫、Golden Bytes 与双进程移动/战斗/CameraLock/断线回归通过；网络层分离完成，下一阶段为尚未开始的 W5 Dedicated |
| 2026-08-18 | M1 网络层分离验收关闭；实现阅读入口后迁到 `NETSYNC_FROM_JOIN_TO_HIT` |
| 2026-08-19 | NetSync W5：`ACTGame.Server` Dedicated Bootstrap / Match / 每连接 ACK；Listen Host 改 N Guest；JoinAccept 允许无房主实体；权威 World 仍属 W6 |
| 2026-08-19 | NetSync W6：`ServerSimulationRunner` + Headless `CharacterPresentationMode`；Capture 改读模拟 Locomotion 时钟；`ServerContentManifest` 指纹加入 Join；Dedicated 创建权威 Actor 并步进 |
| 2026-08-19 | NetSync W7：Dedicated Match 状态机、每连接 `ReplicationFrame`、`MatchEnd`、JoinAccept 改写 SimulationId；Owner 预测复用 W4 Adapter |
| 2026-08-19 | 修复 Dedicated 客机无法操作：Join 先于 Drain；入房立刻发首帧 Spawn，避免 Owner 被建成 Proxy 导致 `CanPredict` 不开 |
| 2026-08-19 | Dedicated Play：命令按 Hint 逐步灌入；Headless `Play` 仍记 CurrentKey；Dodge 期间推迟 2m 硬吸 |
| 2026-08-19 | Dedicated Play 复验：取消逐步灌入（观察者延迟）；Merge 进下一帧 + 首 Hint 和解；吸附/闪避整段推迟硬吸且不掐本机招 |
| 2026-08-19 | W7 Editor Play 用户验收；W8：`ServerLaunchConfigResolver` CLI/Env/File、READY、空房超时与对局结束退出（Editor 不 Quit） |
| 2026-08-19 | W8 Dedicated 出包 + H-DS-D 用户验收；M2 / LAN DS-Demo 关闭 |
| 2026-08-20 | NetSync W9：Listen = `DedicatedServerRuntime` + `LocalClientRuntime`；删除 `ReplicationRoomHost` / `ActHostRoomGameplay` 与 Host 本机 Capture |
| 2026-08-20 | W9 Listen 组合用户验收；本机预测按 `PeekAdvanceSteps` 对齐 60Hz |
| 2026-08-20 | NetSync W10 代码切面：`ACTNet.Prediction`、ChannelMux、可靠命中事件、SnapshotTimeline；出口待 Play |
| 2026-08-22 | NetSync W11 代码切面：Delta/兴趣/预算、`GraphNodeKey`、Recover、FakeActionGame；R2 出口未关 |
| 2026-08-22 | 远端隔步快照：时间线改为向后括号取样；Proxy 按跳过 Tick 补动画时间 |
| 2026-08-22 | 方案 B：`RemotePlaybackClock` + `TickAnimation`；不再用本机 InterpolationAlpha 取样远端 |
| 2026-08-22 | 远端战斗立即提交：判定/受击/Notify 不等播放头；Urgent 破节拍 |
| 2026-08-23 | 新增现行联网阅读入口 `docs/2026.8.23/NETSYNC_FROM_JOIN_TO_HIT.md`（Join→命中调用链） |
| 2026-08-23 | 文档整理：删除已关闭波次备忘 / 被替代方案；TECHNICAL 交叉引用改指现行入口 |
| 2026-08-26 | 相机排期交叉引用改挂 `docs/2026.8.26/CAMERA_SYSTEM_PLAN.md`（Director / SkillShot / UI 展示舱）；实现未改 |
| 2026-08-26 | L-DIR5：相机相对后退（`MoveInput.y`）暂停跟朝向，避免按住 S 与镜头互追转圈 |
| 2026-08-29 | Camera CS0～CS3 代码：Timeline Camera 轨、Rig FollowHold、Director/SkillShot 池、FOV/Dolly/Impulse、Action Editor Scene 预览；待 Editor 验收 |
| 2026-08-30 | Camera C-SP0～C-SP3：保留 Cinemachine 2，接入 Unity Splines 2.8.4；Spline/Binding、A/B VCam、Knot/Tangent/Helix 编辑替换旧 offset/Dolly/VcamKey/Anchor |
| 2026-08-30 | Camera Editor 收敛：删除 Helix 生成器与旧定位 Gizmo；Knot 独立点击热区、W/E 位移/旋转、CameraWindow Debug Scene Camera |
| 2026-08-30 | Camera Editor 可读性：隐藏官方 Spline 未使用扩展数据；按当前预览帧 Position/LookAt/FOV 绘制视锥，不改变朝向真源 |
| 2026-08-30 | Camera Spline 端点规则：新增 Linear/上下左右 Arc 预设；非 Custom 只拖首尾端点，放大选点并隐藏无效首尾切线 |
| 2026-08-30 | Action Camera View：新增独立可停靠 RenderTexture 实际构图窗口；删除会量化闪跳且妨碍编辑的 SceneView Debug 接管路径 |
| 2026-08-30 | Camera Spline 恒速修复：改为逐 Bezier 段累计弧长并查表定位，避免高曲率路径在窗口结束帧前提前到达终点 |
| 2026-08-14 | SprintLean 从静止向右倾改走 engage；GaitPolicy Run 计时加 0.1ms 容差；量化单测不再用非精确 2.5mm |
| 2026-08-09 | BT：删除 `EnemyBehaviorTreeKind` / Presets / Fill / Create Default；运行时仅 `customRoot.Build()` |
| 2026-08-09 | BT：Condition 改为 UE 风格单子装饰 + Abort Self；不再作为 Sequence 叶子条件 |
| 2026-08-09 | BT Graph：装饰/条件改为宿主顶部徽章（UE 表现）；运行真源仍为装饰链 |
| 2026-08-09 | L-GP：`LocomotionGaitPolicy` + `DefaultLocomotionAnimResolver`（WalkLeft/Right）；`strafeMoveMagnitude`；升档/选片无身份 if |

---

## Wave 4 — TargetAdhesion / SoftBodySuppress

### 功能说明

攻击吸附窗口：每帧沿玩家→敌人连线算 `desired`（`horizontalOffsetMm` 控制敌前/心/后），按剩余帧均摊**朝向前方未到达缺口**（过冲落到身后则不倒拖），并夹每帧上限；SoftBody 抑制窗内不参与角色互撞、仍碰静物墙。

### 实现方案

| 项 | 方案 |
|----|------|
| 顺序 | BaseDelta → TargetAdhesion → MotionCommand（Relocate）→ MotorSim → SoftBodySeparation |
| 纯计算 | `ActionMotionAdhesion`；Command 经 `ActionMotionResolver` |
| 目标 | `CharacterTargetingState.SelectedTargetId`；动作中切敌后下一逻辑帧改读新目标 |
| Pose | `ActionMotionWorldQuery` → `IHurtboxTarget.GetLogicalCombatPose`（含朝向） |
| SoftBody | Modifier 窗 / Relocate 落地 `SetSoftBodySuppressFrames` |
| 数据 | `motionModifierStates` + `motionCommandNotifies` |
| Editor | MotionModifier / MotionCommand 轨；Adhesion Scene 假敌预览 |

### 运行时流程

```
CharacterTargetingState.Step → SelectedTargetId
ApplyStep：SoftBodySuppress 刷新（含卡肉帧）
  → Base → Adhesion → MotionCommand（Resolver.Teleport + Facing）
  → SyncRootPoseFromSim
SimulationWorld 帧末 SoftBodySeparation（抑制者不参与）
客机：同一 Bridge；WorldQuery 读 Proxy.GetLogicalCombatPose；帧末 AutonomousSoftBodySolver（抑制者不参与）
客机纠偏：窗内 / 权威卡肉 / Dodge 进行中走 ActionMotionReconcileGate，禁止 2m 硬吸
```

### 已知限制

- Lock-On / SkillShot / UI 展示舱不在 Wave 4/5；排期见 `docs/2026.8.26/CAMERA_SYSTEM_PLAN.md`
- Relocate 挡墙精细候选（FindNearestValid 首版≈ ResolveMove）可后续加强
- 共线退化（玩家与敌人水平重合）本帧不吸
- **打击感吸附已验收；Relocate 需在招上配 MotionCommand 点事件后 Play 验**
- 客机 Adhesion desired 读 Proxy MotorSim（有 Tick 延迟），落点相对 Host 可能有 RTT 级偏差
- 客机不 Collect；卡肉由本机几何预测，伤害只信权威下行；穿敌窗 / Dodge 进行中禁止 2m 硬吸（`ActionMotionReconcileGate`）

### 相关文件

- `Assets/Scripts/Domain/Simulation/Motion/ActionMotionAdhesion.cs`
- `Assets/Scripts/Domain/Character/Presentation/CharacterActionPresentationBridge.cs`
- `Assets/Scripts/Domain/Combat/Actions/Definitions/Timeline/MotionModifierNotifyState.cs`
- `Assets/Tests/EditMode/Simulation/ActionMotionAdhesionTests.cs`

---

## GAS G0～G5 — Numeric 完成态

### 功能说明

Attribute + Effect + Flags 为唯一数值权威；Gate/Pipeline/Hurtbox/Reaction/伤害公式已切换；旧 ResourceSim/Health 已删除；F3 HUD 展示 ATK/DEF/倍率/Effects/反击缓冲。

### 实现方案

| 项 | 方案 |
|----|------|
| 中枢 | `NumericSystem`（Factory 装配；Host 注册；Actor.Step） |
| 扣费/回填 | `NumericCostGate` + `ActionResourceSpecEffectCompiler` |
| 生命边沿 | `CharacterVitality` → Reaction Hit/Death |
| 命中 | Pipeline：完美窗/无敌早退 → OnHit → Grant Effect |
| 伤害 | `DamageNumericCalculator`（Attack/Defense + Out/In 倍率） |
| 配置 | `CharacterNumericConfig.FromResourceConfig`（作者壳 Config） |
| Snapshot | `NumericDebugSnapshot` / `CharacterDebugSnapshot` → `CombatDebugHudController` |

### 已知限制

- 完美闪避慢动作表现事件未做
- HitStop / EffectNotifyState 未进 Effect
- Effect 尚无 ScriptableObject 资产壳（程序 `Create*`）
- Graph Counter Entry / Dodge 完美窗资产需 Editor 人工

### 相关文件

- `Assets/Scripts/Domain/Combat/Numeric/*`
- `Assets/Scripts/Domain/Combat/Actions/Execution/NumericCostGate.cs`
- `Assets/Scripts/Domain/Character/Reactions/CharacterVitality.cs`
- `Assets/Tests/EditMode/Domain/*Numeric*` / `EffectContainerTests` / `DamageNumericCalculatorTests` / `ActionSimResourceGateTests` / `PerfectDodgeAttackTests`

---

## Wave 0 — 观测与保护网

### 功能说明

不改手感：全库归类动作位移源（Baked/Scripted/None/Conflict），Scene 对照烘焙轨迹与 Motor/相机锚点，Play Mode 左上角可读 Intent/Buffer/HP/Lock/横向峰峰值。

### 实现方案

| 项 | 方案 |
|----|------|
| 位移源归类 | `ActionMotionSourceClassifier`（Simulation）+ Editor `ActionDefinitionAuditUtility` |
| 轨迹预览 | Action Inspector「Show Baked Trajectory」→ `ActionMotionTrajectorySceneDrawing` |
| 锚点 Gizmo | Editor `CharacterAnchorGizmoDrawer`（DrawGizmo → PlayerController） |
| Debug HUD | `CharacterActor.BuildDebugSnapshot` + `CombatDebugHudController`（F3） |

### 运行时流程

```
菜单 Validate Motion Sources → 报告窗口（不改资产）
Play：Actor.Step 更新 ActionLateralPeakMm
LateUpdate：HUD 采样 Snapshot → OnGUI 绘制
```

### 已知限制

- 0.4 人工基线手记可选，不阻塞
- Wave 2.5 已删除 Animator RM 回退

### 相关文件

- `Assets/Scripts/Domain/Simulation/Motion/ActionMotionSourceClassifier.cs`
- `Assets/Scripts/Editor/Combat/Motion/ActionDefinitionAuditUtility.cs`
- `Assets/Scripts/App/Controllers/Debug/CombatDebugHudController.cs`
- `docs/2026.8.6/MASTER_IMPLEMENTATION_PLAN.md`

---

## Wave 3 — 技能资源循环

### 功能说明

绝区零式单角色资源：Energy / Decibel / DodgeCharges；起手 Gate 扣费；ConfirmHit 回填；Special 同键按能量选 EX。

### 实现方案

| 项 | 方案 |
|----|------|
| 数值权威 | `NumericSystem` + `CharacterVitality` |
| 价签 | `ActionDefinition.ResourceSpec`（`ActionResourceSpec`） |
| 扣费 | `NumericCostGate` → Spec→Instant Cost Effect |
| 回能 | Pipeline ConfirmHit → `ActionResourceSpecEffectCompiler.ApplyGrant` |
| 同键 EX | `GameplayIntentType.Special` + `ActionEnergyFormSelector`；Graph 多 Entry |
| 观测 | HUD：EX/Decibel/Dodge + `Next Special`（读 Numeric） |

### 运行时流程

```
Intent Special → Graph 收集 Entry → EnergyFormSelector(Ex if CanAfford else Special)
  → ActionSim.TryStart → NumericCostGate.CommitCost
ConfirmHit → ApplyGrant（挥空不回）
Actor.Step：非卡肉时 NumericSystem.Step
```

### 已知限制

- Graph Counter Entry（`Intent=PerfectDodgeAttack`）与 Dodge 完美窗轨需 Editor 人工
- 正式招费用 / Graph Special 双 Entry / Ultimate 资产需 Editor 人工
- 完美闪避慢动作表现未做
- Wave 2.5：Action RM 回退已删（Locomotion Stop/Pivot 仍可选用 RM）

### 相关文件

- `Assets/Scripts/Domain/Combat/Numeric/*`
- `Assets/Scripts/Domain/Combat/Resources/*`（价签）
- `Assets/Scripts/Domain/Input/GameplayIntentProducer.cs`
- `Assets/Scripts/Domain/Combat/Actions/Execution/NumericCostGate.cs`
- `Assets/Tests/EditMode/Domain/ActionSimResourceGateTests.cs` / `ActionEnergyFormSelectionTests.cs` / `PerfectDodgeAttackTests.cs`
- `docs/2026.8.7/GAS_STYLE_COMBAT_REFACTOR_PLAN.md`
