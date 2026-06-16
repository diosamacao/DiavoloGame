# ACTGame 技术文档

> Last updated: 2026-06-17  
> 说明：记录**已实现功能**及其**实现方案**。架构分层见 [ARCHITECTURE.md](ARCHITECTURE.md)；编码约定见 [CONVENTIONS.md](CONVENTIONS.md)。

## 功能索引

| 功能 | 状态 | 入口 / 核心类 | 关键资源 |
|------|------|---------------|----------|
| 第三人称移动 | ✅ 已实现 | `PlayerController` | `Player_KatanaGirl.prefab` |
| 输入（移动 + 视角） | ✅ 已实现 | `InputReader` | `GameInputActions.inputactions` |
| 状态机框架 | ✅ 已实现 | `StateMachine<,>`、`CharacterStateMachine` | — |
| Locomotion 动画驱动 | ✅ 已实现 | `LocomotionState` | `Player_KatanaGirl_AnimationProfile.asset` |
| 第三人称相机 | ✅ 已实现 | `CameraManager` | 场景内 CameraManager 对象 |
| 动作状态（Action） | 🟡 骨架 | `ActionState` | — |
| 攻击 / 战斗 | ⬜ 未实现 | — | `Combat/` 占位 |
| 敌人 AI | ⬜ 未实现 | — | `Enemy/` 占位 |
| UI | ⬜ 未实现 | — | `UI/` 占位 |

状态图例：✅ 可玩可用 · 🟡 有类/占位但未接完 · ⬜ 未开始

---

## 1. 第三人称移动

### 功能说明

玩家通过 WASD 相对**相机朝向**移动；摇杆/键盘输入幅度影响移动速度；角色平滑转向移动方向；含简易重力与贴地。

### 实现方案

| 项 | 方案 |
|----|------|
| 碰撞体 | `CharacterController`（非 Rigidbody） |
| 位移执行 | `PlayerController.Update` 中 `controller.Move` |
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
  → InputReader.MoveInput
  → GetCameraRelativeMoveDirection
  → 有方向：SmoothDamp 旋转 + Move(水平)
  → ApplyGravity：Move(垂直)
```

### 对外暴露（供状态机）

- `MoveInputMagnitude`、`RunThreshold`、`IsGrounded` — 由 `PlayerStateMachine.UpdateContext` 写入 `CharacterContext`

### 已知限制

- 移动逻辑在 `PlayerController`，不在 State 内；动画与位移决策分离（见 ROADMAP「移动职责迁移」）
- `cameraTransform` 未绑定时回退为世界 XZ 平面移动

### 相关文件

- `Assets/Scripts/Player/PlayerController.cs`
- `Assets/Prefabs/Player/Player_KatanaGirl.prefab`

---

## 2. 输入系统

### 功能说明

使用 Unity **Input System**；Player Map 提供 Move（WASD / 左摇杆）与 Look（鼠标 / 右摇杆）。

### 实现方案

| 项 | 方案 |
|----|------|
| 资产 | `GameInputActions.inputactions` |
| 组件 | `InputReader` 挂在玩家根节点 |
| 绑定 | Awake 时 `FindActionMap("Player")`，缓存 Move/Look Action |
| 生命周期 | OnEnable/OnDisable 启用/禁用整个 Asset |
| 消费方 | `PlayerController` 读 Move；`CameraManager` 读 Look |

### 绑定摘要

| Action | 类型 | 主要绑定 |
|--------|------|----------|
| Move | Vector2 | WASD 复合键；Gamepad 左 Stick |
| Look | Vector2 | 鼠标 Delta；Gamepad 右 Stick |

### 错误处理

未分配 `inputActions` 时 `LogError` 并 `enabled = false`。

### 相关文件

- `Assets/Scripts/Input/InputReader.cs`
- `Assets/Scripts/Input/GameInputActions.inputactions`

---

## 3. 角色状态机

### 功能说明

泛型状态机驱动角色逻辑；玩家侧通过 `PlayerStateMachine` 每帧同步 Context 并 Tick 当前 State。

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

- `PlayerStateMachine`：override `UpdateContext`，从 `PlayerController` 快照输入与接地状态

### 已注册状态

| State | Id | Enter | Tick | Exit |
|-------|-----|-------|------|------|
| `LocomotionState` | 10 | — | 按输入选 AnimationKey 并 Play | — |
| `ActionState` | 60 | `Animation.SetLocked(true)` | 空（预留） | Unlock + ResetPlaybackState |

### 运行时流程（玩家）

```
PlayerStateMachine.Update
  → UpdateContext（MoveInputMagnitude, RunThreshold, IsGrounded）
  → StateMachine.Tick
      → LocomotionState.Tick → CharacterAnimationController.Play(key)
```

### 相关文件

- `Assets/Scripts/Core/StateMachine/*`
- `Assets/Scripts/Character/StateMachine/*`
- `Assets/Scripts/Player/PlayerStateMachine.cs`

---

## 4. Locomotion 动画

### 功能说明

根据移动输入幅度在 Idle / Walk / Run 间切换；CrossFade 过渡；与 Animator Controller 状态名通过 Profile 映射。

### 实现方案

| 项 | 方案 |
|----|------|
| 逻辑键 | `AnimationKey` 枚举（Idle, Walk, Run） |
| 映射 | `CharacterAnimationProfile` ScriptableObject |
| 播放 | `CharacterAnimationController.Play(key)` |
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

- `Assets/Scripts/Character/Animation/CharacterAnimationController.cs`
- `Assets/Scripts/Character/Animation/CharacterAnimationProfile.cs`
- `Assets/Scripts/Character/StateMachine/States/LocomotionState.cs`

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

- `CameraManager` 引用玩家 `InputReader`（或 SerializeField 指定）
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

- `Assets/Scripts/Camera/CameraManager.cs`

---

## 6. 玩家 Prefab 组装

### 功能说明

`Player_KatanaGirl` 为可 Play 的玩家实体：物理、输入、移动、动画、状态机一体。

### 根节点组件

| 组件 | 作用 |
|------|------|
| `CharacterController` | 胶囊碰撞，高 1.7，半径 0.28 |
| `InputReader` | 绑定 GameInputActions |
| `PlayerController` | 移动与重力 |
| `CharacterAnimationController` | 动画播放 + Profile |
| `PlayerStateMachine` | 状态机宿主 |

### 子对象

- `CameraRoot`：本地 y=1.4（与 CameraManager 逻辑一致；Manager 也可运行时创建）
- 嵌套 `School_Katana_FullBody-Magica cloth2` 美术 Prefab，挂载 `ACT_Runtime` Animator

### Tag

`Player` — 供 CameraManager 自动查找。

### 相关文件

- `Assets/Prefabs/Player/Player_KatanaGirl.prefab`

---

## 7. 预留 / 未完成功能

### ActionState（骨架）

- 已实现 Enter/Exit 动画锁
- `Tick` 为空，注释预留 `ActionRuntimeController`
- 尚无输入切换到 Action、无攻击动画播放

### CharacterStateType 预留枚举

- `Hit = 80`、`Death = 100` — 无对应 State 类

### 空模块目录

`Enemy/`、`Combat/`、`UI/`、`Editor/` — 仅 `.gitkeep`

---

## 变更日志

| 日期 | 变更 |
|------|------|
| 2026-06-17 | 初版：移动、输入、状态机、动画、相机、Prefab 文档化 |
