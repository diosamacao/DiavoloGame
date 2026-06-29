# ACTGame 技术文档

> Last updated: 2026-06-29
> 说明：记录**已实现功能**及其**实现方案**。架构分层见 [ARCHITECTURE.md](ARCHITECTURE.md)；编码约定见 [CONVENTIONS.md](CONVENTIONS.md)。

## 功能索引

| 功能 | 状态 | 入口 / 核心类 | 关键资源 |
|------|------|---------------|----------|
| 第三人称移动 | ✅ 已实现 | `PlayerController` + `CharacterActor` + `CharacterConfig` | Scene Empty + CharacterConfig |
| 输入（移动 + 视角 + 离散按键） | ✅ 已实现 | `ICharacterInputSource`、纯 C# `InputReader`、`InputManager` | `GameInputActions.inputactions` |
| 状态机框架 | ✅ 已实现 | `StateMachine<,>`、`CharacterStateMachine` | — |
| 架构通信框架 | ✅ 已实现 | `ACTGameArchitecture`、`ArchitectureSystemBase`、`AppControllerBase`、Command / Query / Event | — |
| Locomotion 动画驱动 | ✅ 已实现 | `LocomotionState` | `Player_KatanaGirl_AnimationProfile.asset` |
| 第三人称相机 | ✅ 已实现 | `CameraManager` | 场景内 CameraManager 对象 |
| 动作系统（播放 / 取消 / 连段 / 战斗模式） | ✅ 已实现 | 纯 C# `ActionExecutor`、`CombatModeService` | `CombatModeProfile`、`ActionComboSequence` |
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
| 位移执行 | `LocomotionState.Tick` 调用 `CharacterMotor.TickLocomotion` |
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

- Locomotion 水平移动由 `LocomotionState` 拥有；重力仍由 `CharacterActor` 每帧统一推进
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
| `LocomotionState` | 10 | — | `CharacterMotor.TickLocomotion` + 选 AnimationKey 并 Play | — |
| `ActionState` | 60 | `Animation.SetLocked(true)` | `ActionExecutor.Tick` + `ActionRotationDriver.Tick` | Unlock + ResetPlaybackState |

### 运行时流程（玩家）

```
CharacterActor.Tick
  → InputReader.CaptureFrame / InputManager.IngestFrame
  → CharacterActionDriver.ProcessGameplayInput
  → CharacterMotor.TickGravity
  → CharacterStateMachine.Tick
      → LocomotionState.Tick → CharacterMotor.TickLocomotion → CharacterAnimationService.Play(key)
      → ActionState.Tick → ActionExecutor.Tick → ActionRotationDriver.Tick
```

### 相关文件

- `Assets/Scripts/Core/StateMachine/*`
- `Assets/Scripts/Domain/Character/StateMachine/*`
- `Assets/Scripts/Domain/Character/CharacterActor.cs`

---

## 4. Locomotion 动画

### 功能说明

根据移动输入幅度在 Idle / Walk / Run 间切换；CrossFade 过渡；与 Animator Controller 状态名通过 Profile 映射。

### 实现方案

| 项 | 方案 |
|----|------|
| 逻辑键 | `AnimationKey` 枚举（Idle, Walk, Run） |
| 映射 | `CharacterAnimationProfile` ScriptableObject |
| 播放 | `CharacterAnimationService.Play(key)` |
| 去重 | 相同 key 不重复 CrossFade |
| Root Motion | 关闭（`applyRootMotion = false`） |

### 动画选择规则（LocomotionState）

```
MoveInputMagnitude < 0.01        → Idle
MoveInputMagnitude ≤ RunThreshold → Walk
否则                              → Run
```

`RunThreshold` 来自 Context（与 PlayerController 一致，默认 0.6）。

### Profile 配置（KatanaGirl）

| AnimationKey | Animator 状态名 | CrossFade 默认 |
|--------------|-----------------|----------------|
| Idle | Idle | 0.15s |
| Walk | Walk | 0.15s |
| Run | Run | 0.15s |

资产路径：`Assets/Data/Characters/Player_KatanaGirl_AnimationProfile.asset`  
Animator Controller：`Assets/Art/Characters/.../ACT_Runtime.controller`（Prefab 内嵌引用）

### Action 状态下的动画锁

进入 `ActionState` 时 `SetLocked(true)`，`Play` 调用被忽略；Exit 时解锁并重置 `_currentKey`。

### 相关文件

- `Assets/Scripts/Domain/Character/Animation/CharacterAnimationService.cs`
- `Assets/Scripts/Domain/Character/Animation/CharacterAnimationProfile.cs`
- `Assets/Scripts/Domain/Character/StateMachine/States/LocomotionState.cs`

---

## 5. 第三人称相机

### 功能说明

Cinemachine 第三人称跟随；鼠标控制 yaw/pitch；碰撞遮挡；启动时锁定光标。

### 实现方案

**层级结构（运行时创建或复用）**

```
Player
  └── CameraRoot (y = 1.4)     ← 跟随锚点，LookAt 目标

CameraManager (场景对象)
  └── CameraOrbitPivot         ← 位置同步 CameraRoot，yaw 旋转
        └── CameraPitchPivot   ← pitch 旋转
              └── CM ThirdPerson (CinemachineVirtualCamera)
                    Follow = pitchPivot, LookAt = cameraRoot
```

**Virtual Camera 组件**

- `CinemachineTransposer`：后方 `-followDistance`，LockToTarget，无 damping
- `CinemachineHardLookAt`：注视 cameraRoot
- `CinemachineCollider`：Default 层遮挡，PreserveCameraHeight

**输入**

- `CameraManager` 引用玩家 `PlayerController`，通过 `PlayerController.Input.LookIntent` 获取视角输入
- Update 累加 yaw/pitch；LateUpdate 同步 Pivot 变换

**初始化**

- 确保 Main Camera 有 `CinemachineBrain`
- 按 Tag `Player` 查找 followTarget（若未指定）
- 销毁 legacy `CinemachineFreeLook`（若存在）

### 关键参数（Inspector 默认）

| 字段 | 典型值 | 含义 |
|------|--------|------|
| `cameraRootHeight` | 1.4 | 锚点高度 |
| `followDistance` | 4 | 相机距离 |
| `initialPitch` | 15 | 初始俯角 |
| `horizontalSensitivity` | 0.15 | 水平灵敏度 |
| `verticalSensitivity` | 0.15 | 垂直灵敏度 |
| `topClamp` / `bottomClamp` | 70 / -60 | 俯角限制 |
| `invertY` | true | Y 轴反转 |
| `lockCursorOnStart` | true | 启动锁定鼠标 |

### 与移动的协作

`PlayerController` 用 `Camera.main`（或指定 `cameraTransform`）计算移动方向，与相机 yaw 一致。

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
- 构造纯 C# `InputReader`、`CharacterAnimationService`、`CombatModeService`、`ActionExecutor`、`CharacterStateMachine`
- 注册纯 C# `HitboxFrameConsumer` / `ActionVfxPlayer` 为 Logic Tick 消费者
- `CharacterActor.Tick` 统一输入采集、动作路由、重力和状态机；状态自身调度 Locomotion 移动或 Action 旋转

### Editor 操作

在 Unity Editor 中创建 `CharacterConfig` 资产，填写模型 Prefab、输入资产、Locomotion Profile 与 CombatModeProfile；Scene 内只需要 Empty + `PlayerController` + 该配置引用。

---

## 7. 动作系统

> 完整说明见 [docs/ACTION_SYSTEM.md](../../docs/ACTION_SYSTEM.md)。

### 功能说明

多战斗模式下，玩家通过出招表 + `ActionComboSequence` 起手攻击/闪避；招内 Cancel 消费缓冲；`ActionTransition` 收招（含 **OnHitConfirm / OnWhiff**）；**Logic Tick 由 `UpdateFrame` 统一驱动** Hitbox/VFX。

### 实现方案

| 项 | 方案 |
|----|------|
| 起手 / 缓冲 | `CharacterActionDriver` → `ActionExecutor.TryStartByInput` |
| 移动取消 | `CharacterActionDriver` + `CancelWindow(Movement)` |
| 招式旋转 | `ActionRotationDriver` + `CombatTargetLock` |
| Logic Tick | `ActionExecutor.UpdateFrame` → `ICombatFrameConsumer` + `IActionEventConsumer` |
| 命中回流 | `HitboxFrameConsumer` → `IActionHitReceiver.NotifyHit` |
| Motor | `CharacterMotor`（Locomotion 位移）+ `CharacterActor`（重力调度） |

### 运行时流程（Logic Tick）

```
ActionState.Tick
  → ActionExecutor.Tick(deltaTime)
      → SyncLogicFrameFromElapsed → DispatchCombatFrame
          → HitboxFrameConsumer.OnCombatFrameAdvanced
          → ActionVfxPlayer.OnCombatFrameAdvanced
      → CancelWindow / Transition（含 OnHitConfirm）
```

编辑器 Scrub：`UpdateFrame(frameIndex)` 与上列帧派发共用路径。

### ActionEditor 对齐状态（2026-06-21）

| 对齐度 | 项 |
|--------|-----|
| ✅ | `UpdateFrame` API、`ICombatFrameConsumer`、`ActionEventContext` 派发、Phase/Event Schema |
| ✅ | 命中回流、`OnHitConfirm` / `OnWhiff` Transition 条件 |
| ✅ | `CharacterActionDriver` 角色无关输入路由 |
| 🟡 | ActionEvent 已派发但 Hitbox/VFX 仍兼容旧数组；无 `ActionEditorWindow` |
| ⬜ | 伤害结算、Hit 状态、GM 热重载 |

### 已知限制

- ActionEvent 已有运行时派发入口，但 Hitbox/VFX 仍处于旧字段兼容期
- 连招仍线性 `ActionComboSequence`
- Scene 玩家入口已改为 Empty + `PlayerController` + `CharacterConfig`

### Editor 操作（Prefab）

创建 `CharacterConfig` 后，在 Scene 空物体的 `PlayerController` 上指定该资产；Play Mode 验证：起手、连段、移动取消、索敌旋转、Hitbox/VFX 与重构前一致。

### 相关文件

- `Assets/Scripts/Domain/Combat/Actions/*`
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
