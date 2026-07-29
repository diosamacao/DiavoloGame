# ACTGame 技术文档

> Last updated: 2026-07-29
> 说明：记录**已实现功能**及其**实现方案**。架构分层见 [ARCHITECTURE.md](ARCHITECTURE.md)；编码约定见 [CONVENTIONS.md](CONVENTIONS.md)。

## 功能索引

| 功能 | 状态 | 入口 / 核心类 | 关键资源 |
|------|------|---------------|----------|
| 第三人称移动 | ✅ 已实现 | `PlayerController` + `CharacterActor` + `CharacterConfig` | Scene Empty + CharacterConfig |
| 输入（原始帧 + 语义意图） | ✅ 已实现 | `InputReader`、`InputManager`、`GameplayIntentProducer` | `GameInputActions.inputactions` + `GameplayIntentProfile` |
| 状态机框架 | ✅ 已实现 | `StateMachine<,>`、`CharacterStateMachine` | — |
| 架构通信框架 | ✅ 已实现 | `ACTGameArchitecture`、`ArchitectureSystemBase`、`AppControllerBase`、Command / Query / Event | — |
| Locomotion 动画驱动 | ✅ 已实现 | `LocomotionStateMachine` + `LocomotionState` | AnimationProfile + `CharacterLocomotionProfile` |
| Locomotion 起步/急停/转身 | 🟡 代码已接、资产待绑 | 内层 `LocomotionPhase` 纯状态机 | Start/Stop/Pivot Clip + 落脚标记 |
| 第三人称相机 | ✅ 已实现 | `CameraManager` | 场景内 CameraManager 对象 |
| 动作系统（选招 / 播放 / 取消 / 连段 / 高优打断 / 战斗模式） | ✅ 已实现 | 纯 C# `ActionResolverService` + `ActionExecutor` + `CombatModeService` | `CombatModeProfile`、`PlayerActionSet`、`ActionGraph`、`ActionDefinition.interruptPriority` |
| Action Editor（时间轴编辑） | 🟡 骨架/部分 | `ActionEditorWindow` + `ActionTimeline` 手动加轨/窗口 | Menu：`ACT/Action Editor` |
| 攻击 / 战斗判定 | ✅ 已实现 | `HitboxFrameConsumer` + `CombatDamageCalculator` + `CharacterHealth` | Action 基础伤害 × Hitbox 权重；Hit/Death 状态 |
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

- 仍处于单一 `Assembly-CSharp`，目录边界由 Editor 校验辅助，尚未通过 asmdef 强制。
- Model / Utility 容器已具备 API，但当前暂无业务 Model / Utility 注册。

### 相关文件

- `Assets/Scripts/App/Architecture/*`
- `Assets/Scripts/Editor/Architecture/ArchitectureBoundaryValidator.cs`

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
| 旋转 | `SmoothDampAngle` 绕 Y 轴对齐移动方向 |
| 重力 | 独立 `velocity.y`；着地时设为 `groundedGravity`，否则累加 `gravity` |

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
Update
  → InputReader.CaptureFrame
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

使用 Unity **Input System** 采集原始帧，再由 `GameplayIntentProducer` 将离散输入转换为设备无关意图；ActionGraph 不再依赖 InputAction 名。

### 实现方案

| 项 | 方案 |
|----|------|
| 资产 | `GameInputActions.inputactions` |
| 形态 | `InputReader` 为玩家纯 C# 输入源，实现 `ICharacterInputSource` |
| 绑定 | Move/Look 从 Player Map 读取；离散引用来自 `GameplayIntentProfile` |
| 生命周期 | OnEnable/OnDisable 启用/禁用整个 Asset |
| 原始中枢 | `InputManager` 保存 Move/Look 与 Pressed/IsPressed/Released |
| 语义生产 | `GameplayIntentProducer`：SprintAttack、DodgeAttack 上下文映射，PressedThenLong、Dodge |
| 语义缓冲 | `GameplayIntentBuffer`：当帧事件 + Action Cancel 跨帧消费 |
| 消费方 | `CharacterActionDriver` 消费动作意图；Locomotion 继续消费连续 Move 快照 |

### 绑定摘要

| Action | 类型 | 主要绑定 |
|--------|------|----------|
| Move | Vector2 | WASD 复合键；Gamepad 左 Stick |
| Look | Vector2 | 鼠标 Delta；Gamepad 右 Stick |
| Attack | Button | Pressed→Attack（Sprint 时 SprintAttack；Dodge Action 中为 DodgeAttack）；HoldReached→LongPressedAttack；Released→AttackRelease |
| Dodge | Button | Pressed→Dodge |

### 错误处理

未分配 `inputActions` 或 `GameplayIntentProfile` 时 CharacterConfig 校验失败，不创建角色运行时。

### 相关文件

- `Assets/Scripts/Infrastructure/Input/InputReader.cs`
- `Assets/Scripts/Domain/Input/GameplayIntent*.cs`
- `Assets/Scripts/Input/GameInputActions.inputactions`

---

## 3. 角色状态机

### 功能说明

状态机驱动角色逻辑；角色侧通过 `CharacterActor` 每帧摄入输入并 Tick 当前 State。

### 实现方案

**Core 层（无 Unity 依赖）**

```
StateMachine<TStateId, TContext>
  RegisterState → Initialize(context, initial) → Tick / TryChangeState
```

- `StateBase` 默认 `CanTransitionTo`：仅允许转到**枚举值更大**的状态（Locomotion=10 → Action=60 → Hit=80 → Death=100）
- 同 ID 或转换被拒时 `TryChangeState` 返回 false

**Character 层**

- `CharacterStateMachine`（MonoBehaviour）：Awake 组装 `CharacterContext`，注册 State，初始 `Locomotion`
- `Update`：`UpdateContext()` → `_machine.Tick`

**Player 层**

- `CharacterActor`：采集输入、处理动作路由、推进重力，再 Tick `CharacterStateMachine`

### 已注册状态

| State | Id | Enter | Tick | Exit |
|-------|-----|-------|------|------|
| `LocomotionState` | 10 | `LocomotionStateMachine.Enter` | `LocomotionStateMachine.Tick` | `LocomotionStateMachine.Exit` |
| `ActionState` | 60 | `Animation.SetLocked(true)` | `ActionExecutor.Tick` + `ActionRotationDriver.Tick` | Unlock + ResetPlaybackState |

### 运行时流程（玩家）

```
CharacterActor.Tick
  → InputReader.CaptureFrame / InputManager.IngestFrame
  → GameplayIntentProducer.Tick
  → CharacterActionDriver.ProcessGameplayInput
  → CharacterMotor.TickGravity
  → CharacterStateMachine.Tick
      → LocomotionState.Tick → LocomotionStateMachine（转换→ExecuteFrame）→ Motor + Animation
      → ActionState.Tick → ActionExecutor.Tick → ActionRotationDriver.Tick
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

- `CameraManager` 引用玩家 `PlayerController`，通过 `PlayerController.Input.LookIntent` 获取视角输入
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
- 构造纯 C# `InputReader`、`CharacterAnimationService`、`CombatModeService`、`ActionResolverService`、`ActionExecutor`、`CharacterActionDriver`、`CharacterStateMachine`
- 注册纯 C# `HitboxFrameConsumer` 为 Logic Tick 消费者；注册 `ActionVfxPlayer` 为 `IActionNotifyConsumer`
- `CharacterActor.Tick` 统一输入采集、动作路由、重力和状态机；状态自身调度 Locomotion 移动或 Action 旋转

### Editor 操作

在 Unity Editor 中创建 `CharacterConfig` 资产，填写模型 Prefab、输入资产、Locomotion Profile 与 CombatModeProfile；Scene 内只需要 Empty + `PlayerController` + 该配置引用。

---

## 7. 动作系统

> 完整说明见 [docs/ACTION_SYSTEM.md](../../docs/ACTION_SYSTEM.md)。

### 功能说明

多战斗模式下，玩家通过 `ActionGraph` Entry×Trigger 起手攻击/闪避；起手、连段进位、Dodge 方向分派、招内 Cancel 下一招统一由 `ActionResolverService` 解析，`ActionExecutor` 只负责播放已解析好的招；Action 态下更高 `interruptPriority` 可经 Graph Entry **硬打断**当前招；`ActionTransition` 收招（含 **OnHitConfirm / OnWhiff**）；**Logic Tick 由 `UpdateFrame` 统一驱动** `ActionTimeline`、Hitbox 与 VFX。

### 实现方案

| 项 | 方案 |
|----|------|
| 起手 / 缓冲 | `GameplayIntentBuffer` → `CharacterActionDriver` → `ActionResolverService.TryResolveStart` → `ActionExecutor.TryStart` |
| Trigger | `ActionDefinition.Trigger = GameplayIntentType`；不保存 InputActionReference |
| 选招策略 | `ActionGraph` Entry / Normal 与 Perfect CancelWindow 边 / `ActionGraphSharedRoute`；顺序组按类型聚合子节点 |
| 六向闪避 | `DirectionalActionResolver` 统一解析前、后、左前、左后、右前、右后；前后扇区半角默认 `30°`，纯左/右输入偏向前侧变体 |
| Cancel 下一招 | 每招一个 Normal、可选一个 Perfect；窗口重叠且同 Trigger 时 Perfect 优先；不同 Trigger 仍按意图优先级竞争 |
| 高优硬打断 | Action 态：`TryResolveStart(PriorityInterrupt)` → `ActionExecutor.TryInterrupt`（候选 `interruptPriority` 严格大于当前，且 `IsInterruptibleAtFrame`） |
| 时间轴数据 | `ActionDefinition.Timeline`：`ActionNotify` 点事件（Event/VFX/SFX）+ `ActionNotifyState` 区间窗口 |
| 移动取消 | `CharacterActionDriver` + `CancelWindowNotifyState(Movement)` |
| 招式旋转 | `ActionRotationDriver` + `RotationNotifyState` + `CombatTargetLock` |
| Logic Tick | `ActionExecutor.UpdateFrame` → `ICombatFrameConsumer` + `ActionTimelineRunner` + `IActionNotifyConsumer` |
| 命中回流 | `HitboxFrameConsumer` → `IActionHitReceiver.NotifyHit` |
| Motor | `CharacterMotor`（Locomotion 位移）+ `CharacterActor`（重力调度） |

### 关键参数（打断）

| 参数 | 默认 | 说明 |
|------|------|------|
| `ActionDefinition.interruptPriority` | `0` | 越大越优先；同级不互硬打断 |
| `ActionPhaseNotifyState.interruptible` | `true` | Startup/Active/Recovery 覆盖时参与硬打断；Invincible/SuperArmor 标签不参与 |
| Recovery `allowMovementCancel` | `true` | 有移动输入时退出 Action 返回 Locomotion |
| Recovery `allowEntryRestart` | `true` | 有效动作缓冲按当前 Graph Entry 重开 |
| 无 Phase 覆盖帧 | — | `IsInterruptibleAtFrame` 返回 `true`（默认可硬打断） |
| `GameplayIntentProfile.actionBufferDurationSeconds` | `0.15` | Action 内预输入有效期；过期后不再于 Recovery/收招误触发 |

### 运行时流程（高优打断 + Logic Tick）

```
CharacterActionDriver.ProcessGameplayInput（Action 态）
  → TryPriorityInterrupt(intent)
      → ActionResolverService.TryResolveStart(Origin=PriorityInterrupt)  // Graph Entry
      → ActionExecutor.TryInterrupt → TransitionTo
  → 失败则 Buffer(intent)  // 留给 CancelWindow

ActionState.Tick
  → ActionExecutor.Tick(deltaTime)
      → SyncLogicFrameFromElapsed → DispatchCombatFrame
          → HitboxFrameConsumer.OnCombatFrameAdvanced（按 hitbox.attachPointId 解析挂点）
          → ActionTimelineRunner.Dispatch
              → PlayVfxNotify 点触发 → ActionVfxPlayer.OnActionNotify（Resolve attachPointId + 显式 playbackSpeed）
              → PlaySfxNotify 点触发 → ActionSfxPlayer.OnActionNotify（pitch = playbackSpeed）
              → 其他 ActionNotifyState Enter/Tick/Exit
      → CancelWindow / Transition（含 OnHitConfirm）
          → CancelWindow：汇总当前帧 Normal / Perfect；同一意图先 Perfect 后 Normal，再按显式边 / SharedRoute 解析
          → Recovery Phase：按窗口开关处理移动取消 / Graph Entry 软重开
          → NotifyActionEnded → ActionSfxPlayer.OnActionEnded → 专用 AudioSource.Stop
```

VFX 生命周期：`ActionVfxPlayer` 在招式结束 / 连招切招时**不**强制 Despawn；池化实例由 `VfxPooledInstance` 按粒子自然时长（含 `playbackSpeed` / 卡肉冻结）自行回池。无 `VFXManager` 时回退 `Destroy(lifetime)`。

SFX 生命周期：`ActionSfxPlayer` 使用角色根下专用子物体 `ActionSfx` 的 `AudioSource`（与脚步声隔离）；`Stop` / `TransitionTo`（含硬打断与 Cancel 切招）经 `OnActionEnded` 调用 `AudioSource.Stop`，打断未播完的动作音效。

编辑器 Scrub：`UpdateFrame(frameIndex)` 与上列帧派发共用路径；`ACT/Action Editor` 窗口用 `ActionEditorPreviewSession` 做 Pose/VFX 预览（触发帧后 `Simulate(t * playbackSpeed)`）。

### ActionEditor 对齐状态（2026-07-25）

| 对齐度 | 项 |
|--------|-----|
| ✅ | `UpdateFrame` API、`ICombatFrameConsumer`、`ActionTimelineRunner`、`ActionNotify` / `ActionNotifyState` Schema |
| ✅ | 命中回流、`OnHitConfirm` / `OnWhiff` Transition 条件 |
| ✅ | `CharacterActionDriver` 角色无关输入路由 |
| ✅ | Hitbox/VFX/Cancel/Movement/Rotation 已收敛到 `ActionTimeline`，删除旧双轨数组 |
| ✅ | `ActionEditorWindow`：手动加轨、轨头纵向拖拽排序、窗口拖拽；VFX/SFX 为单帧点事件，Phase 为区间窗口 |
| ✅ | `ActionSfxPlayer` 运行时点触发；招式结束/打断时 `Stop`；`CharacterAttachPointResolver` 供 VFX/Hitbox 共用 |
| ⬜ | 伤害结算、Hit 状态、GM 热重载 |

### 已知限制

- 现有资产需要在 Unity Editor 的 Phase 轨重建原 `phases[]`，并为 Recovery 配置移动取消 / Entry 重开开关；Agent 未直接修改 `.asset`
- 硬打断与 Recovery 软重开走 Graph Entry，不要求 Cancel 边；独特连招进位仍依赖 Combo Window + 显式边
- 旧 `perfectFrame`、Cancel 槽 Id 与同类型多窗口不再受支持；资产需整理为一个 Normal 与可选一个 Perfect
- Scene 玩家入口已改为 Empty + `PlayerController` + `CharacterConfig`

### Editor 操作（Prefab）

创建 `CharacterConfig` 后，在 Scene 空物体的 `PlayerController` 上指定该资产；Play Mode 验证：起手、连段、移动取消、索敌旋转、`ActionTimeline` 中的 Hitbox/VFX 与预期一致。

### 相关文件

- `Assets/Scripts/Domain/Combat/Actions/{Definitions,Resolution,Execution,Frames}/*`
- `Assets/Scripts/App/Controllers/Gameplay/PlayerController.cs`
- `docs/ACTION_SYSTEM.md`、`docs/ACTION_EDITOR.md`

---

## 8. 敌人 AI、伤害与生成

### 功能说明

敌人复用玩家的 CharacterActor、Locomotion、ActionGraph 与 Hitbox 管线；AI 仅生成输入帧，并通过 Idle / Chase / Attack / Hit / Dead 五态完成追击、攻击、受击和死亡回收。

### 实现方案

| 项 | 方案 |
|----|------|
| 配置 | `EnemyDefinition` 只组合 CharacterConfig、BrainProfile、独立 teamId 与 HP；全部 Action 由 CharacterConfig 持有 |
| AI 输入 | `AIInputSource` 从 GameplayIntentProfile 解析 Always + Pressed → Attack 的物理 id |
| 追击 | `EnemyBrain` 更新 facing proxy，持续写 `Move=(0, chaseMoveMagnitude)` |
| 攻击 | Brain 只发一帧 Attack 脉冲；选招仍由 GameplayIntentProducer → CharacterActionDriver → ActionGraph |
| 伤害与反应 | 扣血为 `ActionDefinition.BaseDamage × HitboxNotifyState.DamageWeight`；非致命命中始终触发 Hit，独立于伤害是否为 0 |
| 受击目标 | `CharacterHurtboxTarget` 统一玩家/敌人的 Hurtbox、阵营和生命值；自身整棵 Transform 层级均被排除 |
| 状态 | `CharacterStateMachine` 正式注册 `HitState` / `DeathState` |
| 生成 | `EnemySpawnController` → `SpawnEnemyCommand` → `EnemyController`；`EnemySpawnSystem` 限制存活数 |
| 回收 | 死亡立即注销 Target/CombatActor，死亡 Action 完成并等待配置延迟后 Destroy |

### 关键参数

| 参数 | 默认 | 含义 |
|------|------|------|
| `ActionDefinition.baseDamage` | 10 | 招式基础伤害 |
| `CharacterCombatConfig.maxHealth` | 100 | 玩家默认生命值 |
| `CharacterCombatConfig.hitStunAction / deathAction` | null | 玩家与敌人共用的可选受击、死亡表现 |
| `HurtboxDefinition.localOffset` | (0, 0.9, 0) | 标准人形受击框中心，角色根位于脚底 |
| `EnemyDefinition.teamId` | 1 | 敌人阵营；不继承复用 CharacterConfig 的玩家阵营 |
| `EnemyBrainProfile.aggroRadius / loseAggroRadius` | 10 / 14 | 进战/脱战距离 |
| `attackRange / stopDistance` | 2 / 1.2 | 攻击与贴身停步距离 |
| `attackCooldownSeconds` | 1.2 | 成功起手后的攻击冷却 |
| `hitStunSeconds` | 0.35 | 无受击 Action 时硬直 |

### 运行时流程

```
EnemyController.Update
  → EnemyBrain.Tick → AIInputSource
  → CharacterActor.Tick → Locomotion / Action

ApplyHitCommand
  → CharacterHurtboxTarget.OnHit
  → CharacterHealth.ApplyDamage
      → EnterHit / EnterDeath
```

玩家镜头震动只响应 `Attacker` 根节点带 `PlayerController` 的命中；敌人命中玩家仍触发受击和攻击者卡肉，但不触发玩家进攻震屏。

### 已知限制

- 代码已编译，仍需在 Unity Editor 人工创建 EnemyDefinition、EnemyBrainProfile、敌人 CharacterConfig 与 ActionGraph 资产。
- 首版追击是直线趋近，不含 NavMesh、绕障、Strafe 与群体避让。
- 死亡回收当前使用 Destroy；对象池可在后续替换 DespawnEnemyCommand 内实现。

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
