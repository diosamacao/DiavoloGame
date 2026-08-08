# ACTGame 技术文档

> Last updated: 2026-08-08
> 说明：记录**已实现功能**及其**实现方案**。架构分层见 [ARCHITECTURE.md](ARCHITECTURE.md)；编码约定见 [CONVENTIONS.md](CONVENTIONS.md)。

## 功能索引

| 功能 | 状态 | 入口 / 核心类 | 关键资源 |
|------|------|---------------|----------|
| 命中受击 Cue（VFX/SFX） | 🟡 代码通道已接、资产待绑 | `HitImpactController` + `HitFeedbackSettings` | 接触点落点 + 随机旋转；Feedback 填 Prefab/Clip |
| 逻辑 Hurtbox 调试线框 | ✅ 已实现 | `CombatHurtboxDebugSettings` + `CombatHurtboxDebugVisualizer` | F4 开关（F3 HUD 显示状态） |
| 固定帧模拟宿主 | ✅ L0A 已实现 | `SimulationHost`、`SimulationWorld`、`SimActorId` | 60Hz，无资产 |
| Wave0 动作审计 / 锚点可视化 / Debug HUD | ✅ 已实现 | `ActionDefinitionAuditUtility`、`CharacterAnchorGizmoDrawer`、`CombatDebugHudController` | 菜单 `ACTGame/Action/Validate Motion Sources`；场景挂 HUD |
| Wave1 位移止血 / BaseMotionMode / 相机滤左右 | ✅ 已实现 | `ForwardSigned`、`ActionBaseMotionMode`、`CameraManager.lateralFollowFactor` | Attack 需以 ForwardSigned 重烘焙；菜单 Migrate Base Motion Mode |
| Wave2 视觉残差 / VisualMotionRoot | ✅ 已实现（含 2.5） | `CharacterVisualMotionBridge`、`TryGetVisualResidualMm` | ForwardSigned：Motor 无横摆，模型在 VisualRoot 摆；2026-08-08 已删 Action RM/Legacy/ForwardOnly |
| Wave3 玩法资源 / 同键 EX | 🟡 资产待绑；运行时已迁 Numeric | `NumericCostGate`、`ActionResourceSpec`、`ActionEnergyFormSelector` | Spec 填表；Graph 双 Entry |
| GAS-lite 数值重构 | ✅ G0～G5 完成 | `NumericSystem`、`DamageNumericCalculator`、`CharacterVitality` | Effect SO 壳 |
| 完美闪避反击（Wave 3.4） | ✅ 代码路由完成 | `PerfectDodgeAttack`、Pipeline 武装、Begin 清缓冲 | Graph Counter Entry（Editor） |
| 第三人称移动 | ✅ 已实现 | `PlayerController` + `CharacterActor` + `CharacterConfig` | Scene Empty + CharacterConfig |
| 输入（量化帧 + 语义意图） | ✅ L0B 代码已实现 | `InputFrameBuffer`、`InputReader`、`AIInputWriter`、`GameplayIntentProducer` | `GameInputActions.inputactions` + 全局 `GameplayIntentSettings` |
| 敌人木桩 AI 开关 | ✅ 已实现并验收 | `EnemyBrainProfile.enableCombatActions` + `Monster_EDF` | 2026-08-08 Play：Hit_Shake / 高 HP / 不追打 |
| CombatMode→Graph | ✅ Phase B | `CombatModeEntry.actionGraph` / `ActiveGraph` | 已删 PlayerActionSet；Editor 迁移菜单 |
| 全局 Input + Locomotion 收敛 | ✅ B2/B3 | `GameInputSettings`；Mode→`LocomotionProfile`（内含 Anim） | Config 不再挂 Input/Locomotion |
| 状态机框架 | ✅ 已实现 | `StateMachine<,>`、`CharacterStateMachine` | — |
| 架构通信框架 | ✅ 已实现 | `ACTGameArchitecture`、`ArchitectureSystemBase`、`AppControllerBase`、Command / Query / Event | — |
| Locomotion 动画驱动 | ✅ 已实现 | `LocomotionStateMachine` + `LocomotionState` | AnimationProfile + `CharacterLocomotionProfile` |
| Locomotion 起步/急停/转身 | 🟡 代码已接、资产待绑 | 内层 `LocomotionPhase` 纯状态机 | Start/Stop/Pivot Clip + 落脚标记 |
| 第三人称相机 | ✅ 已实现 | `CameraManager` | 场景内 CameraManager 对象 |
| 动作系统（整数帧 / 选招 / 取消 / 连段 / 高优打断 / 战斗模式） | ✅ L1B 已实现（Play Mode 待回归） | `ActionSim` + `CharacterActionPresentationBridge` + `ActionFrameQuery` | 60Hz Action + `ActionGraph` |
| Action Editor（时间轴编辑） | 🟡 骨架/部分 | `ActionEditorWindow` + `ActionTimeline` 手动加轨/窗口 | Menu：`ACT/Action Editor` |
| 攻击 / 战斗判定 | ✅ L0C 延迟结算已实现 | `CombatHitPipeline` + `CombatDamageCalculator` + `CharacterReactionService` | SimHitKey；HitPayload；Hit/Death 状态 |
| 敌人 AI | 🟡 代码已接、资产待绑 | `EnemyController` + `EnemyBrain` + 共享 `CharacterActor` | `EnemyDefinition`、`EnemyBrainProfile`、敌人 CharacterConfig/Graph |
| UI | ⬜ 未实现 | — | `UI/` 占位 |

状态图例：✅ 可玩可用 · 🟡 有类/占位但未接完 · ⬜ 未开始

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
| 输入帧 | `InputFrame` 使用 sbyte Move、稳定按钮 bitset、frame 与 SimActorId；World 持有 `InputFrameBuffer` 历史 |
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
- L2/M0–M1：运动表烘焙 + 运行时查表。`bakeStatus=Ok` 时表现桥按帧取本地 Δ 经 MotorSim 移动并关闭 Animator RM；未烘焙招式仍可走 Animator RM→Motor。
- L2 HitStop：`hitStopFrames` 经 Pipeline 写入 `ActionSim.freezeFrames`；冻结期间不推进动作帧/位移；骨骼由表现桥读 Snapshot，VFX 由 `SimulationLogicStepEvent` 递减。
- L2 Locomotion：Stop/Pivot 根位移按 `ActionSim.LogicHz` 整数帧取轨，不再用 `NormalizedTime`。
- L2 MotorSim：水平+竖直毫米权威；`TickVertical` 整数重力/着地；逻辑路径不再 `CharacterController.Move`；CC 保持禁用（禁止 Sync 后 re-enable，否则 PhysX 挤出地面呈悬空）。
- L2 静态碰撞：`StaticCollisionBake`（菜单 `ACTGame/Collision/Bake Static From Scene...`）→ `SimStaticCollisionWorld` AABB 滑墙；`CombatWorldController` 绑定资产，未绑定则 `OpenField`。地面薄板/名含 Floor·Ground·Terrain 只写 GroundY，不进水平硬挡；墙体才投影 AABB。Mesh 墙仍用包围盒（保守）；无斜坡。
- L2/M2：`Bake All` / `Bake Dirty Only` + Inspector Dirty 黄条 + `ACTGame/Motion/Validate Motion Dirty`。
- L2 软弹开：`SimulationWorld` 帧末按 Id 序对 `ISimSoftBodyParticipant` 执行 `SoftBodySeparation`（默认 factor=500‰、迭代 3）；按 `softBodyMass` 分配推力，`softBodyImmovable` 像墙；死亡不参与。
- L2 命中：`SimCombatPose` 从 MotorSim 取水平根；Hitbox 挂点只提供相对根局部 TRS；Hurtbox 用 `GetLogicalHurtbox`；自身排除用 `SimActorId`。
- 联网定案（方案层）：完整客户端预测 + 回滚；权威仍为 FramePacket（见 `docs/ACTION_SYSTEM_LOCKSTEP_REFACTOR_PLAN.md` §5.12）。

### 相关文件

- `Assets/Scripts/Domain/Simulation/*`
- `Assets/Scripts/App/Controllers/Gameplay/SimulationHost.cs`
- `Assets/Scripts/App/Controllers/Combat/CombatWorldController.cs`
- `Assets/Scripts/Domain/Character/Presentation/CharacterPresentationBridge.cs`
- `Assets/Tests/EditMode/Simulation/*`

---

## 1. 第三人称移动

### 功能说明

玩家通过 WASD 相对**相机朝向**移动；摇杆/键盘输入幅度影响移动速度；角色平滑转向移动方向；含简易重力与贴地。

### 实现方案

| 项 | 方案 |
|----|------|
| 碰撞体 | `CharacterController`（非 Rigidbody） |
| 位移执行 | `LocomotionStateMachine` 各相位 → `CharacterMotor.ApplyLocomotion` |
| 方向计算 | 输入 Vector2 → 相机 forward/right 投影到 XZ 平面 → 归一化方向 |
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
  → GetCameraRelativeMoveDirection
  → 有方向：SmoothDamp 旋转 + Move(水平)
  → ApplyGravity：Move(垂直)
```

### 对外暴露（供状态机）

- `MoveInputMagnitude`、`RunThreshold`、`IsGrounded` — 由当前 State 从 `CharacterMotor` 同步到 `CharacterContext`

### 已知限制

- Locomotion 水平移动由内层相位 State → `ApplyLocomotion` 拥有；重力仍由 `CharacterActor` 每帧统一推进
- `cameraTransform` 未绑定时回退为世界 XZ 平面移动

### 相关文件

- `Assets/Scripts/App/Controllers/Gameplay/PlayerController.cs`
- `Assets/Prefabs/Player/Player_KatanaGirl.prefab`

---

## 2. 输入系统

### 功能说明

使用 Unity **Input System** 在设备边界采样，立即量化为带逻辑帧与 SimActorId 的 `InputFrame`；玩家、AI、回放共用同一格式，再由 `GameplayIntentProducer` 转换为设备无关意图。

### 实现方案

| 项 | 方案 |
|----|------|
| 资产 | `GameInputActions.inputactions` |
| 形态 | `InputReader` 实现 `ILocalInputSampler`；AI 使用 `AIInputWriter`，不伪装设备 |
| 绑定 | Move 从 Player Map 读取并量化为 sbyte；Look 只供相机表现；离散 Action 名仅在边界映射为固定 `InputButton` |
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

### 错误处理

未分配 `inputActions`（玩家）或全局 `GameplayIntentProfile` 未就绪时校验/工厂失败。意图经 `GameplayIntentSettings`（Resources `ACT/GameplayIntentProfile`，菜单可迁移）。木桩：Brain `enableCombatActions=false`。L0B 帧阈值：Intent 缓冲常见 60；EnemyBrainProfile 建议攻击冷却 72、失败重试 12、朝向刷新 6。

### 相关文件

- `Assets/Scripts/Domain/Simulation/Input/*`
- `Assets/Scripts/Infrastructure/Input/InputReader.cs`
- `Assets/Scripts/Infrastructure/Input/AIInputWriter.cs`
- `Assets/Scripts/Domain/Input/GameplayIntent*.cs`
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

顶层仍为 `Locomotion` 状态；内部由 `LocomotionStateMachine`（Core `StateMachine<>`）驱动 Idle / Start / Gait(Walk|Run|Sprint) / PivotTurn / Stop。各相位为独立 State；转换默认全开、由各态主动 `RequestPhase`。满跑输入先进 Run，连续约 3s 后进 Sprint；仅 Sprint 可大角度转身。落脚标记驱动脚步声与急停选脚。

### 实现方案

| 项 | 方案 |
|----|------|
| 内层机 | `LocomotionStateMachine` + `LocomotionContext`；Tick = 转换后 `ExecuteFrame` |
| 相位 State | `Idle/Start/Gait/PivotTurn/StopLocomotionState`（`Locomotion/States/`） |
| 相位 Id | `LocomotionPhase`：Idle→Start→Gait；Sprint 大角度→PivotTurn；松输入→Stop |
| 逻辑键 | `AnimationKey`：Idle/Walk/Run/Sprint/Start/StartEnd/PivotTurn/StopL/StopR |
| 映射 | `CharacterAnimationProfile` → `AnimationClip` |
| 相位参数 | `CharacterLocomotionProfile`（阈值、落脚、脚步音） |
| 脚步 | `LocomotionFootCycle` 按 `NormalizedTime` 采样标记 |
| 门面 | `CharacterAnimationService.Play` + `NormalizedTime` / `HasFinishedCurrent` |
| 位移 | `CharacterMotor.ApplyLocomotion`（首版无急停减速/转身专用位移） |
| Root Motion（Locomotion） | StartEnd/Stop/Pivot 使用 Profile 内烘焙轨；TurnBack 解锁后仅把当前输入相对初始折返输入的方向差叠加到角色根，位移同步重定向 |

### 相位规则（摘要）

```
Idle + 有输入                         → Start（必经）
Start 播完                            → Gait(Walk|Run)，不直接 Sprint
Gait：跑输入持续 sprintAfterRunSeconds → Run→Sprint
Start 松输入                          → Stop（播 StartEnd / Run_Start_End）
Pivot 松输入                          → Stop（StopL/R；朝向=转身目标）
Gait 松输入 + 速度够或 Run/Sprint     → Stop（StopL/R）；否则 Idle
Gait(Sprint) + |yaw| ≥ pivotAngle    → PivotTurn（Walk/Run 只平滑转）
PivotTurn 播放 < 0.08s               → 锁定进入朝向
PivotTurn 播放 ≥ 0.08s               → Clip 保留自身转身；角色根只叠加实时输入相对初始折返输入的方向差
Stop 任意时刻再输入                  → Start
Dodge Action 退出 + 仍有移动输入      → 直接 Gait(Sprint)，跳过 Start/Run 计时
```

无落脚记录时急停默认右脚。缺少 StartEnd 时回退 StopL/R；缺少 Start/Pivot/Stop Clip 时 LogError 并跳过对应表现。

### 关键参数（LocomotionProfile 默认）

| 字段 | 默认 | 含义 |
|------|------|------|
| `idleInputThreshold` | 0.01 | 静止判定 |
| `stopMinSpeedFactor` | 0.5 | Gait→Stop 相对 runSpeed |
| `pivotAngleDegrees` | 135 | 仅 Sprint；对齐 zzzdemo turnBackAngle |
| `pivotInputUnlockSeconds` | 0.08s | TurnBack 起手锁根时长；到时后允许实时输入修正 Clip 的目标方向 |
| `pivotRotationSmoothTime` | 0.5s | 解锁后对输入方向差形成的角色根偏移做 SmoothDamp |
| `stopUseRootMotion` / `pivotUseRootMotion` | true | 方案 B：烘焙根位移驱动 StartEnd/Stop/Pivot |
| `rootMotionPositionScale` | 1 | 烘焙位移缩放 |
| `sprintAfterRunSeconds` | 3 | Run 连续满输入后进 Sprint |
| `gaitInputGapGraceSeconds` | 0.15 | Gait 松手宽限；超时才 Stop，便于键盘换向 Pivot |
| `interruptFadeDuration` | 0.08 | 切入 Stop 短淡入 |
| Motor `sprintSpeed` | 9 | 冲刺水平速度（旧资产为 0 时回退 runSpeed） |

### Profile 配置（Katana）

| AnimationKey | 说明 |
|--------------|------|
| Idle / Walk / Run | 原有循环 |
| Start / StartEnd / PivotTurn / StopL / StopR | 需在 Editor 绑定；StartEnd 对应 Run_Start_End |

资产：`Assets/Data/CharacterLocomotion/`（AnimationProfile）；LocomotionProfile 在 CharacterConfig 上引用（可空，运行时默认阈值）。

### Action 状态下的动画锁

进入 `ActionState` 时 `SetLocked(true)`；`LocomotionStateMachine.Exit` 冻结落脚采样。Exit Action 后回 Locomotion 从 Idle 再起（可消费 Resume）。

### 已知限制

- 未实现急停减速曲线与 Pivot 专用位移（计划 Phase D）
- Start/Stop/Pivot Clip 与落脚标记需人工配置

### 相关文件

- `Assets/Scripts/Domain/Character/Locomotion/*`
- `Assets/Scripts/Domain/Character/Animation/*`
- `Assets/Scripts/Domain/Character/StateMachine/States/LocomotionState.cs`
- `docs/LOCOMOTION_OPTIMIZATION_PLAN.md`

---

## 5. 第三人称相机

### 功能说明

Cinemachine 第三人称跟随；鼠标控制 yaw/pitch；碰撞遮挡；启动时锁定光标。Orbit 锚点对 `CameraRoot` 做 SmoothDamp，减轻攻击多段位移时的镜头顿挫。

### 实现方案

**层级结构（运行时创建或复用）**

```
Player
  └── CameraRoot (y = 1.4)     ← 角色跟随目标（硬绑角色）

CameraManager (场景对象)
  └── CameraOrbitPivot         ← SmoothDamp 追 CameraRoot；Follow/LookAt 共用此平滑点
        └── CameraPitchPivot   ← pitch 旋转
              └── CM ThirdPerson (CinemachineVirtualCamera)
                    Follow = pitchPivot, LookAt = orbitPivot
```

**Virtual Camera 组件**

- `CinemachineTransposer`：后方 `-followDistance`，LockToTarget，无 damping（平滑在 Orbit 层完成）
- `CinemachineHardLookAt`：注视平滑后的 `orbitPivot`
- `CinemachineCollider`：Default 层遮挡，PreserveCameraHeight

**跟随平滑**

- `LateUpdate`：`orbitPivot` 对 `cameraRoot` 做 `SmoothDamp`（`followSmoothTime`）
- 首帧、`followSmoothTime <= 0`、或距离超过 `SnapDistance`(3) 时直接吸附
- 对外提供 `SnapFollowToTarget()` 供传送等硬重置

**输入**

- `CameraManager` 引用玩家 `PlayerController`，通过 `PlayerController.LookInput` 获取非权威视角输入
- Update 累加 yaw/pitch；LateUpdate 平滑同步 Pivot 变换

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
| `initialPitch` | 15 | 初始俯角 |
| `horizontalSensitivity` | 0.15 | 水平灵敏度 |
| `verticalSensitivity` | 0.15 | 垂直灵敏度 |
| `topClamp` / `bottomClamp` | 70 / -60 | 俯角限制 |
| `invertY` | true | Y 轴反转 |
| `lockCursorOnStart` | true | 启动锁定鼠标 |

### 与移动的协作

`PlayerController` 用 `Camera.main`（或指定 `cameraTransform`）计算移动方向，与相机 yaw 一致。

### 已知限制

- 平滑仅抹平位置顿挫；未按 Action/Locomotion 切换不同 `followSmoothTime`（可后续做方案 C）
- LookAt 已切到 `orbitPivot`，角色急速冲刺时镜头会略滞后于角色身体

### 相关文件

- `Assets/Scripts/App/Controllers/Camera/CameraManager.cs`

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
| 招式旋转 | `ActionRotationDriver` + `RotationNotifyState` + 节点 `TargetLockSettings`；SmoothDamp 显式使用固定逻辑步长，离开 Action 清空阻尼速度 |
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
| App | `HitImpactController`：XZ=接触点，Y=半身高；`LookRotation * Random.Euler` |
| 调试 | F4 → `CombatHurtboxDebugSettings.ShowHurtboxes` 画逻辑 Hurtbox |
| 卡肉 | 火花 `VfxPooledInstance.SetSpawnOwner(attacker)`，与刀光同窗暂停 |

**关键参数：** `hitImpactWorldOffset` 默认 `0`（相对接触点）；`randomizeImpactRotation` 默认开，Y `0～360`；SFX Volume `0～1`。

**已知限制：** 仍需在 Action Hitbox Feedback 人工绑定 Prefab/Clip；单 Hurtbox/角色，无多部位表。

VFX 生命周期：`ActionVfxPlayer` 在招式结束 / 连招切招时**不**强制 Despawn；池化实例由 `VfxPooledInstance` 按粒子自然时长（含 `playbackSpeed` / 卡肉冻结）自行回池。无 `VFXManager` 时回退 `Destroy(lifetime)`。

SFX 生命周期：`ActionSfxPlayer` 使用 `ActionSfx` 下多声道 `AudioSource`（与脚步声隔离）；`OnActionEnded` 对仍在播的声道做 **0.1s（unscaled）** 淡出；连招新 `PlaySfx` 走空闲声道，不 Cancel 正在淡出的旧声道。

编辑器 Scrub 使用 `ActionEditorPreviewSession` 做 Pose/VFX 预览，并与 Runtime 共用无副作用 `ActionFrameQuery` 的段映射、窗口与点事件规则；不执行 `ActionSim.Step`。

**编辑器交互（2026-08-04）**：

- VFX/SFX/Event 点事件在时间轴上绘制为**菱形**（热区按轨高，不随 1 帧条宽缩小）
- Timeline 顶栏 **Zoom**（1×–16×）+ Ctrl/Cmd+滚轮；放大后横向滚动以精确拖帧
- Scrub / 播放 / 工具栏改帧时，playhead 超出 Zoom 可视区会**自动平移**时间轴视图
- Scene Hitbox 线框与 VFX Prefab/粒子按 **Preview Frame** 驱动：拖到对应帧/区间即可预览（Hitbox 仅在窗口激活时可见），无需选中时间轴窗口；选中仅用于 Handles 编辑
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

敌人复用玩家的 CharacterActor、Locomotion、ActionGraph 与 Hitbox 管线；AI 仅生成输入帧，并通过 Idle / Chase / Attack / Hit / Dead 五态完成追击、攻击、受击和死亡回收。

### 实现方案

| 项 | 方案 |
|----|------|
| 配置 | `EnemyDefinition` 只组合 CharacterConfig、BrainProfile、独立 teamId 与 HP；受击/死亡映射在 `CharacterConfig.Combat.Reactions` |
| AI 输入 | `AIInputWriter` 直接写固定 Attack bit；World 在 Actor Step 前调用 Brain 生成当前逻辑帧 |
| 追击 | `EnemyBrain` 更新 facing proxy，持续写 `Move=(0, chaseMoveMagnitude)` |
| 攻击 | Brain 只发一帧 Attack 脉冲；选招仍由 GameplayIntentProducer → CharacterActionDriver → ActionGraph |
| 伤害与反应 | `HitboxNotifyState.Payload` 持有基础伤害、HitReactionId 与反馈；`CharacterReactionService` 统一玩家/敌人事件桥接，Resolver 产出完整状态请求 |
| 受击目标 | `CharacterHurtboxTarget` 统一玩家/敌人的 Hurtbox、阵营和生命值；自身整棵 Transform 层级均被排除 |
| 状态 | `CharacterStateMachine` 正式注册 `HitState` / `DeathState` |
| 生成 | `EnemySpawnController` → `SpawnEnemyCommand` → `EnemyController`；`EnemySpawnSystem` 限制存活数 |
| 回收 | 死亡立即注销 Target/CombatActor，死亡 Action 完成并等待配置延迟后 Destroy |

### 关键参数

| 参数 | 默认 | 含义 |
|------|------|------|
| `HitPayload.baseDamage` | 10 | 单个 Hitbox 的基础伤害 |
| `CharacterCombatConfig.maxHealth` | 100 | 玩家默认生命值 |
| `CharacterCombatConfig.reactions` | 空规则集，默认硬直 0.35s | Resolver 按反应类型与 HitReactionId 选择表现 Action；无动作时使用规则集硬直时长 |
| `HurtboxDefinition.localOffset` | (0, 0.9, 0) | 标准人形受击框中心，角色根位于脚底 |
| `EnemyDefinition.teamId` | 1 | 敌人阵营；不继承复用 CharacterConfig 的玩家阵营 |
| `EnemyBrainProfile.aggroRadius / loseAggroRadius` | 10 / 14 | 进战/脱战距离 |
| `attackRange / stopDistance` | 2 / 1.2 | 攻击与贴身停步距离 |
| `attackCooldownSeconds` | 1.2 | 成功起手后的攻击冷却 |

### 运行时流程

```
SimulationWorld.Step
  → EnemyHandle.ProduceInput → EnemyBrain.Step → AIInputWriter → InputFrameBuffer
  → EnemyHandle.Step → CharacterActor.Step(InputFrame) → Locomotion / Action

CombatHitPipeline（全体 Actor Step 后）
  → 按 SimHitKey 稳定排序
  → CharacterHurtboxTarget.OnHit
  → CharacterHealth.ApplyDamage
      → CharacterReactionService
      → CharacterReactionResolver（生成 CharacterReactionRequest）
      → EnterHit / EnterDeath
  → IActionHitReceiver.NotifyHit
  → PostCombat 自动衔接
  → PublishAttackHitCommand → AttackHitEvent
```

玩家镜头震动只响应 `Attacker` 根节点带 `PlayerController` 的命中；敌人命中玩家仍触发受击和攻击者卡肉，但不触发玩家进攻震屏。

### 已知限制

- 代码已编译，仍需在 Unity Editor 人工创建 EnemyDefinition、EnemyBrainProfile、敌人 CharacterConfig 与 ActionGraph 资产。
- 本次职责重构不保留旧序列化兼容层：现有 Graph 需重填节点 Intent / 索敌 / 自动衔接，Hitbox 需重填 Payload，CharacterConfig 需重填 Reaction Rules。
- 首版追击是直线趋近，不含 NavMesh、绕障、Strafe 与群体避让。
- 死亡回收当前使用 Destroy；对象池可在后续替换 DespawnEnemyCommand 内实现。
- `HurtboxTarget` 静态木桩没有 `SimActorId`，L0C 后不进入权威命中；如需可攻击木桩，应实现并注册正式 Simulation Actor。

### 相关文件

- `Assets/Scripts/Domain/Enemy/*`
- `Assets/Scripts/App/Controllers/Gameplay/Enemy*.cs`
- `Assets/Scripts/App/Commands/Enemy/*`
- `Assets/Scripts/App/Systems/Enemy/EnemySpawnSystem.cs`
- `Assets/Scripts/Domain/Combat/Damage/*`

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
| 2026-07-18 | Locomotion Phase/FootCycle：`LocomotionService`（Start/Gait/PivotTurn/Stop）、落脚脚步、`ApplyLocomotion`；方案见 `docs/LOCOMOTION_OPTIMIZATION_PLAN.md` |
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
| 2026-08-08 | 动作 SFX 多声道淡出（连招不掐断）；受击特效 Y 固定半身高 |

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

- 0.4 基准招样例需人工填写 `docs/2026.8.6/WAVE0_BASELINE_NOTES.md`
- 尚未删除 Animator RM 回退（Wave 2.4/2.5）

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
