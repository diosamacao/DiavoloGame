# ACTGame 技术文档

> Last updated: 2026-07-19
> 说明：记录**已实现功能**及其**实现方案**。架构分层见 [ARCHITECTURE.md](ARCHITECTURE.md)；编码约定见 [CONVENTIONS.md](CONVENTIONS.md)。

## 功能索引

| 功能 | 状态 | 入口 / 核心类 | 关键资源 |
|------|------|---------------|----------|
| 第三人称移动 | ✅ 已实现 | `PlayerController` + `CharacterActor` + `CharacterConfig` | Scene Empty + CharacterConfig |
| 输入（移动 + 视角 + 离散按键） | ✅ 已实现 | `ICharacterInputSource`、纯 C# `InputReader`、`InputManager` | `GameInputActions.inputactions` |
| 状态机框架 | ✅ 已实现 | `StateMachine<,>`、`CharacterStateMachine` | — |
| 架构通信框架 | ✅ 已实现 | `ACTGameArchitecture`、`ArchitectureSystemBase`、`AppControllerBase`、Command / Query / Event | — |
| Locomotion 动画驱动 | ✅ 已实现 | `LocomotionService` + `LocomotionState` | AnimationProfile + `CharacterLocomotionProfile` |
| Locomotion 起步/急停/转身 | 🟡 代码已接、资产待绑 | `LocomotionService` Phase FSM | Start/Stop/Pivot Clip + 落脚标记 |
| 第三人称相机 | ✅ 已实现 | `CameraManager` | 场景内 CameraManager 对象 |
| 动作系统（选招 / 播放 / 取消 / 连段 / 战斗模式） | ✅ 已实现 | 纯 C# `ActionResolverService` + `ActionExecutor` + `CombatModeService` | `CombatModeProfile`、`PlayerActionSet`、`ActionResolver`(Single/Combo/Directional) |
| Action Editor（时间轴编辑） | 🟡 骨架/部分 | `ActionEditorWindow` + `ActionTimeline` 手动加轨/窗口 | Menu：`ACT/Action Editor` |
| 攻击 / 战斗判定 | 🟡 部分实现 | 纯 C# `HitboxFrameConsumer` + `HitDetector` OBB + 命中回流 | 无伤害、Hit 状态 |
| 敌人 AI | ⬜ 未实现 | — | `Enemy/` 占位 |
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
| 位移执行 | `LocomotionService` → `CharacterMotor.ApplyLocomotion` |
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

- Locomotion 水平移动由 `LocomotionService` → `ApplyLocomotion` 拥有；重力仍由 `CharacterActor` 每帧统一推进
- `cameraTransform` 未绑定时回退为世界 XZ 平面移动

### 相关文件

- `Assets/Scripts/App/Controllers/Gameplay/PlayerController.cs`
- `Assets/Prefabs/Player/Player_KatanaGirl.prefab`

---

## 2. 输入系统

### 功能说明

使用 Unity **Input System**；Player Map 提供 Move（WASD / 左摇杆）与 Look（鼠标 / 右摇杆）。

### 实现方案

| 项 | 方案 |
|----|------|
| 资产 | `GameInputActions.inputactions` |
| 形态 | `InputReader` 为玩家纯 C# 输入源，实现 `ICharacterInputSource` |
| 绑定 | Awake 时 `FindActionMap("Player")`，缓存 Move/Look Action |
| 生命周期 | OnEnable/OnDisable 启用/禁用整个 Asset |
| 消费方 | `CharacterActor` 读 Move；`CameraManager` 通过 `PlayerController.Input` 读 Look |

### 绑定摘要

| Action | 类型 | 主要绑定 |
|--------|------|----------|
| Move | Vector2 | WASD 复合键；Gamepad 左 Stick |
| Look | Vector2 | 鼠标 Delta；Gamepad 右 Stick |

### 错误处理

未分配 `inputActions` 时 `LogError` 并 `enabled = false`。

### 相关文件

- `Assets/Scripts/Infrastructure/Input/InputReader.cs`
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
| `LocomotionState` | 10 | `Locomotion.Enter` | `LocomotionService.Tick` | `Locomotion.Exit` |
| `ActionState` | 60 | `Animation.SetLocked(true)` | `ActionExecutor.Tick` + `ActionRotationDriver.Tick` | Unlock + ResetPlaybackState |

### 运行时流程（玩家）

```
CharacterActor.Tick
  → InputReader.CaptureFrame / InputManager.IngestFrame
  → CharacterActionDriver.ProcessGameplayInput
  → CharacterMotor.TickGravity
  → CharacterStateMachine.Tick
      → LocomotionState.Tick → LocomotionService → Motor.ApplyLocomotion + Animation.Play
      → ActionState.Tick → ActionExecutor.Tick → ActionRotationDriver.Tick
```

### 相关文件

- `Assets/Scripts/Core/StateMachine/*`
- `Assets/Scripts/Domain/Character/StateMachine/*`
- `Assets/Scripts/Domain/Character/CharacterActor.cs`

---

## 4. Locomotion 动画与相位

### 功能说明

顶层仍为 `Locomotion` 状态；内部由 `LocomotionService` 驱动 Idle / Start / Gait(Walk|Run|Sprint) / PivotTurn / Stop。满跑输入先进 Run，连续约 3s 后进 Sprint；仅 Sprint 可大角度转身。落脚标记驱动脚步声与急停选脚。

### 实现方案

| 项 | 方案 |
|----|------|
| 相位 | `LocomotionPhase`：Idle→Start→Gait；Sprint 大角度→PivotTurn；松输入→Stop |
| 逻辑键 | `AnimationKey`：Idle/Walk/Run/Sprint/Start/PivotTurn/StopL/StopR |
| 映射 | `CharacterAnimationProfile` → `AnimationClip` |
| 相位参数 | `CharacterLocomotionProfile`（阈值、落脚、脚步音） |
| 脚步 | `LocomotionFootCycle` 按 `NormalizedTime` 采样标记 |
| 门面 | `CharacterAnimationService.Play` + `NormalizedTime` / `HasFinishedCurrent` |
| 位移 | `CharacterMotor.ApplyLocomotion`（首版无急停减速/转身专用位移） |
| Root Motion（Locomotion） | 不做 |

### 相位规则（摘要）

```
Idle + 有输入                         → Start（必经）
Start 播完                            → Gait(Walk|Run)，不直接 Sprint
Gait：跑输入持续 sprintAfterRunSeconds → Run→Sprint
Start / Pivot 松输入                  → Stop（立刻）
Gait 松输入 + 速度够或 Run/Sprint     → Stop；否则 Idle
Gait(Sprint) + |yaw| ≥ pivotAngle    → PivotTurn（Walk/Run 只平滑转）
Stop 任意时刻再输入                  → Start
Pivot→Stop 时急停朝向用转身目标方向
```

无落脚记录时急停默认右脚。缺少 Start/Pivot/Stop Clip 时 LogError 并跳过对应表现（不保留旧 Idle/Walk/Run 内联分支）。

### 关键参数（LocomotionProfile 默认）

| 字段 | 默认 | 含义 |
|------|------|------|
| `idleInputThreshold` | 0.01 | 静止判定 |
| `stopMinSpeedFactor` | 0.5 | Gait→Stop 相对 runSpeed |
| `pivotAngleDegrees` | 135 | 仅 Sprint；对齐 zzzdemo turnBackAngle |
| `pivotRootFollowsInput` | false | false=Clip 含 Y 转向时锁根；true=ReturnRun 式代码转根 |
| `pivotLockNormalizedTime` | 0.08 | 仅 rootFollows 时：前段不转根 |
| `pivotRotationSmoothTime` | 0.5 | 仅 rootFollows 时：其后 SmoothDamp |
| `stopUseRootMotion` / `pivotUseRootMotion` | true | 方案 B：用烘焙根位移轨驱动 Stop/Pivot |
| `rootMotionPositionScale` | 1 | 烘焙位移缩放 |
| `sprintAfterRunSeconds` | 3 | Run 连续满输入后进 Sprint |
| `gaitInputGapGraceSeconds` | 0.15 | Gait 松手宽限；超时才 Stop，便于键盘换向 Pivot |
| `interruptFadeDuration` | 0.08 | 切入 Stop 短淡入 |
| Motor `sprintSpeed` | 9 | 冲刺水平速度（旧资产为 0 时回退 runSpeed） |

### Profile 配置（Katana）

| AnimationKey | 说明 |
|--------------|------|
| Idle / Walk / Run | 原有循环 |
| Start / PivotTurn / StopL / StopR | 需在 Editor 绑定后方可完整体验 |

资产：`Assets/Data/CharacterLocomotion/`（AnimationProfile）；LocomotionProfile 在 CharacterConfig 上引用（可空，运行时默认阈值）。

### Action 状态下的动画锁

进入 `ActionState` 时 `SetLocked(true)`；`LocomotionService.Exit` 冻结落脚采样。Exit Action 后回 Locomotion 从 Idle 再起。

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

多战斗模式下，玩家通过出招表（`ActionEntry` → `ActionResolver`）起手攻击/闪避；起手、连段进位、Dodge 方向分派、招内 Cancel 下一招统一由 `ActionResolverService` 解析，`ActionExecutor` 只负责播放已解析好的招；`ActionTransition` 收招（含 **OnHitConfirm / OnWhiff**）；**Logic Tick 由 `UpdateFrame` 统一驱动** `ActionTimeline`、Hitbox 与 VFX。

### 实现方案

| 项 | 方案 |
|----|------|
| 起手 / 缓冲 | `CharacterActionDriver` → `ActionResolverService.TryResolveStart` → `ActionExecutor.TryStart` |
| 选招策略 | `ActionResolver`：`Single` / `Combo`（线性连段）/ `Directional`（方向闪避） |
| Cancel 下一招 | `ActionExecutor` 扫描窗口消费输入后 → `ActionResolverService.TryResolveNext` |
| 时间轴数据 | `ActionDefinition.Timeline`：`ActionNotify` 点事件（Event/VFX/SFX）+ `ActionNotifyState` 区间窗口 |
| 移动取消 | `CharacterActionDriver` + `CancelWindowNotifyState(Movement)` |
| 招式旋转 | `ActionRotationDriver` + `RotationNotifyState` + `CombatTargetLock` |
| Logic Tick | `ActionExecutor.UpdateFrame` → `ICombatFrameConsumer` + `ActionTimelineRunner` + `IActionNotifyConsumer` |
| 命中回流 | `HitboxFrameConsumer` → `IActionHitReceiver.NotifyHit` |
| Motor | `CharacterMotor`（Locomotion 位移）+ `CharacterActor`（重力调度） |

### 运行时流程（Logic Tick）

```
ActionState.Tick
  → ActionExecutor.Tick(deltaTime)
      → SyncLogicFrameFromElapsed → DispatchCombatFrame
          → HitboxFrameConsumer.OnCombatFrameAdvanced（按 hitbox.attachPointId 解析挂点）
          → ActionTimelineRunner.Dispatch
              → PlayVfxNotify 点触发 → ActionVfxPlayer.OnActionNotify（Resolve attachPointId + 显式 playbackSpeed）
              → PlaySfxNotify 点触发 → ActionSfxPlayer.OnActionNotify（pitch = playbackSpeed）
              → 其他 ActionNotifyState Enter/Tick/Exit
      → CancelWindowNotifyState / Transition（含 OnHitConfirm）
```

VFX 生命周期：`ActionVfxPlayer` 在招式结束 / 连招切招时**不**强制 Despawn；池化实例由 `VfxPooledInstance` 按粒子自然时长（含 `playbackSpeed` / 卡肉冻结）自行回池。无 `VFXManager` 时回退 `Destroy(lifetime)`。

编辑器 Scrub：`UpdateFrame(frameIndex)` 与上列帧派发共用路径；`ACT/Action Editor` 窗口用 `ActionEditorPreviewSession` 做 Pose/VFX 预览（触发帧后 `Simulate(t * playbackSpeed)`）。

### ActionEditor 对齐状态（2026-07-13）

| 对齐度 | 项 |
|--------|-----|
| ✅ | `UpdateFrame` API、`ICombatFrameConsumer`、`ActionTimelineRunner`、`ActionNotify` / `ActionNotifyState` Schema |
| ✅ | 命中回流、`OnHitConfirm` / `OnWhiff` Transition 条件 |
| ✅ | `CharacterActionDriver` 角色无关输入路由 |
| ✅ | Hitbox/VFX/Cancel/Movement/Rotation 已收敛到 `ActionTimeline`，删除旧双轨数组 |
| ✅ | `ActionEditorWindow`：手动加轨、拖拽；VFX/SFX 为单帧点事件（不可拉时长），显式 `playbackSpeed` + `attachPointId` |
| ✅ | `ActionSfxPlayer` 运行时点触发；`CharacterAttachPointResolver` 供 VFX/Hitbox 共用 |
| ⬜ | 伤害结算、Hit 状态、GM 热重载 |

### 已知限制

- 现有资产需要在 Unity Editor 中把旧字段配置迁移到 `ActionTimeline` 对应列表；Agent 未直接修改 `.asset`
- 连招仍线性 `ComboActionResolver`（分支连招需新增 Resolver 子类）
- Scene 玩家入口已改为 Empty + `PlayerController` + `CharacterConfig`

### Editor 操作（Prefab）

创建 `CharacterConfig` 后，在 Scene 空物体的 `PlayerController` 上指定该资产；Play Mode 验证：起手、连段、移动取消、索敌旋转、`ActionTimeline` 中的 Hitbox/VFX 与预期一致。

### 相关文件

- `Assets/Scripts/Domain/Combat/Actions/{Definitions,Resolution,Execution,Frames}/*`
- `Assets/Scripts/App/Controllers/Gameplay/PlayerController.cs`
- `docs/ACTION_SYSTEM.md`、`docs/ACTION_EDITOR.md`

---

## 8. 预留 / 未完成功能

### CharacterStateType 预留枚举

- `Hit = 80`、`Death = 100` — 无对应 State 类

### 空模块目录

`Enemy/`、`UI/` — 仅 `.gitkeep`；`Combat/Actions/` 动作运行时 + `Combat/Hitbox/` OBB 判定骨架已建

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
