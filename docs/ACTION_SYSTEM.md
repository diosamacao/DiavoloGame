# ACTGame — 动作系统技术实现文档

> 本文档描述**当前已落地**的动作系统：架构、实现细节与使用方式。  
> 长期目标与编辑器规划见 [ACTION_EDITOR.md](./ACTION_EDITOR.md)。  
> Last updated: 2026-06-17

---

## 1. 文档定位

| 文档 | 内容 |
|------|------|
| **本文档（ACTION_SYSTEM.md）** | 已实现功能、运行时行为、接入步骤 |
| [ACTION_EDITOR.md](./ACTION_EDITOR.md) | 完整数据模型、编辑器分期、帧级战斗语义 |
| [.cursor/skills/actgame-architecture/TECHNICAL.md](../.cursor/skills/actgame-architecture/TECHNICAL.md) | 全项目功能索引 |

**当前阶段：** Phase A 中期 — `CancelWindow` / `ActionTransition` / `PlayerActionSet` 已接入运行时；**不含** Hitbox、受击、动作编辑器 UI。

---

## 2. 功能状态总览

| 能力 | 状态 | 说明 |
|------|------|------|
| `ActionDefinition` SO | ✅ 已实现 | 动画、帧数、位移、取消窗、收招 Transition |
| `CancelWindow`（Action 取消） | ✅ 已实现 | 帧窗口 + `InputActionReference` + priority |
| `CancelWindow`（Movement 取消） | ✅ 已实现 | `PlayerController` 检测移动意图并切 Locomotion |
| `ActionTransition`（AnimationEnd） | ✅ 已实现 | 播完按 priority 衔接或 `Stop` |
| `PlayerActionSet` 出招表 | ✅ 已实现 | 离散输入 → 起手 `ActionDefinition` |
| `ActionRuntimeController` | ✅ 已实现 | 播放、取消解析、收招、Root Motion / 脚本位移 |
| `InputManager` + `PlayerInputFrame` | ✅ 已实现 | 意图摄入、多 id 缓冲、注册回调 |
| `ActionStartBehavior` | 🟡 部分 | 仅 `FaceBufferedMoveIntent` |
| Root Motion 桥接 | ✅ 已实现 | `CharacterRootMotionDriver` + Receiver 防漂移 |
| `ActionState` + 动画锁定 | ✅ 已实现 | 招式期间 Locomotion 动画不覆盖 |
| `ActionPhase` / Hitbox / Hurtbox | ⬜ 未实现 | 见 ACTION_EDITOR §3.2–3.5 |
| `ActionTransition`（OnHit / OnWhiff 等） | ⬜ 未实现 | 枚举已预留，运行时未解析 |
| `ActionGraph` | ⬜ 未实现 | — |
| Combat 命中 / 伤害 | ⬜ 未实现 | — |
| `Hit` 受击状态 | ⬜ 未实现 | — |
| `ActionEditorWindow` | ⬜ 未实现 | M5 目标 |

---

## 3. 设计原理

### 3.1 核心原则

1. **数据驱动** — 连招、取消窗、收招写在 `ActionDefinition` / `PlayerActionSet` SO，不在状态机里硬编码分支。
2. **输入与玩法解耦** — `InputManager` 只管意图与缓冲；`PlayerController` 唯一持有并注册回调；状态机不读输入。
3. **状态机薄层** — `Locomotion` / `Action` 只管动画锁定与状态切换；招式逻辑集中在 `ActionRuntimeController`。
4. **Animator 双轨** — Animator Controller 仅 Locomotion；招式 `PlayClip(animationClip)` 直播。
5. **角色无关执行器** — `IActionRuntime` 挂 `CharacterContext`，玩家与后续敌人可共用。
6. **与 ACTION_EDITOR 对齐** — 已采用 `CancelWindow.cancelType`（Action / Movement）、`ActionTransition`；尚未实现 Phase / Hitbox / Event。

### 3.2 职责分层

| 层 | 职责 | 不负责 |
|----|------|--------|
| `InputReader` | 采集设备原始输入 → `PlayerInputFrame` | 缓冲、玩法判断 |
| `InputManager` | 摄入帧、移动意图、离散缓冲、`RegisterPressed` | 位移执行、切状态 |
| `PlayerController` | 注册输入、起手/缓冲路由、移动取消、水平位移 | 招式帧推进、Cancel 解析 |
| `ActionRuntimeController` | 播招、`CancelWindow` 消费、`ActionTransition`、位移 | 读取原始设备 |
| `ActionState` / `LocomotionState` | 动画锁 / Locomotion 动画 | 输入与连招 |

---

## 4. 架构设计

### 4.1 模块关系

```
InputReader.CaptureFrame()
       │
       ▼
PlayerController.IngestInput() ── InputManager
       │                              │
       │                              ├─ RegisterPressed(inputId) → HandleDiscreteInput
       │                              ├─ Buffer(inputId)           （招式中）
       │                              └─ MoveIntent / BufferedMoveIntent
       │
       ├─ Locomotion：TryStartByInput(inputId) → ActionState
       ├─ Action 中：Buffer；Movement 取消窗内 + HasMoveIntent → Locomotion
       │
       ├── ActionRuntimeController ← IActionComboInput（消费缓冲）
       │         ↑ PlayerActionSet（起手映射）
       │         └── ActionDefinition（CancelWindow / Transition）
       │
       └── PlayerStateMachine
                ├── LocomotionState（Idle/Walk/Run）
                └── ActionState（Tick ActionRuntime → 结束回 Locomotion）
```

### 4.2 状态机职责

| 状态 | 职责 |
|------|------|
| `Locomotion` | 根据 `MoveInputMagnitude` 播放 Idle/Walk/Run |
| `Action` | `Animation.SetLocked(true)`；`ActionRuntime.Tick`；`!IsPlaying` 时回 Locomotion |
| `Hit` / `Death` | 预留 |

### 4.3 类与接口一览

| 类型 | 路径 | 职责 |
|------|------|------|
| `ActionDefinition` | `Combat/Actions/ActionDefinition.cs` | 单招数据 |
| `CancelWindow` | `Combat/Actions/CancelWindow.cs` | 取消窗序列化 + `ResolvedCancelWindow` |
| `ActionTransition` | `Combat/Actions/ActionTransition.cs` | 收招衔接 |
| `PlayerActionSet` | `Combat/Actions/PlayerActionSet.cs` | 出招表 `ActionEntry[]` |
| `ActionRuntimeController` | `Combat/Actions/ActionRuntimeController.cs` | 招式运行时 |
| `IActionRuntime` | `Character/StateMachine/IActionRuntime.cs` | 执行器抽象 |
| `IActionComboInput` | `Input/IActionComboInput.cs` | 多 id 缓冲消费 |
| `IActionStartContext` | `Combat/Actions/IActionStartContext.cs` | 起手副作用（朝向等） |
| `InputManager` | `Input/InputManager.cs` | 输入中枢 |
| `InputReader` | `Input/InputReader.cs` | 设备采集 |
| `InputBindingUtils` / `InputIds` | `Input/InputIds.cs` | Action 名解析、特殊 id |
| `PlayerInputFrame` | `Input/PlayerInputFrame.cs` | 单帧输入快照 |
| `CharacterRootMotionDriver` | `Character/Animation/CharacterRootMotionDriver.cs` | Root Motion → CC |

---

## 5. 数据模型

### 5.1 ActionDefinition

| 字段 | 说明 |
|------|------|
| `id` / `displayName` | 标识与显示名 |
| `animationClip` | 招式动画（须与 Animator State 同名） |
| `sampleRate` / `totalFrames` | 逻辑帧率与总帧（`OnValidate` 自动算） |
| `actionType` | `CombatActionType` 分类 |
| `crossFadeDuration` | 切入 CrossFade 时间 |
| `cancelWindows[]` | 取消窗（见 §5.2） |
| `transitions[]` | 收招衔接（见 §5.3） |
| `startBehaviors[]` | 起手副作用（见 §5.4） |
| `useRootMotion` | 动画 Root Motion；为 true 时忽略脚本位移 |
| `displacementDistance` + 帧窗口 | 脚本推进（可正可负，沿朝前 XZ） |

**帧换算：** `frame = FloorToInt(elapsedSeconds * sampleRate)`

### 5.2 CancelWindow

| 字段 | 说明 |
|------|------|
| `startFrame` / `endFrame` | 生效帧区间（`endFrame > startFrame`） |
| `cancelType` | `Action`：切到 `targetAction`；`Movement`：由 `PlayerController` 处理 |
| `allowedInputs` | `InputActionReference[]`；运行时 id = **Action 名**（如 `Attack`） |
| `targetAction` | Action 取消的目标招式 |
| `priority` | 数值越大越优先；同帧多窗按降序扫描 |

**Action 取消流程：** `ActionRuntimeController` 每帧按 priority 扫描 → 窗口内且有匹配缓冲 → 消费缓冲并 `TransitionTo(targetAction)`，同时清除其它离散缓冲。

**Movement 取消流程：** `ActionDefinition.IsInMovementCancelWindow(elapsed)` 为 true，且 `InputManager.HasMoveIntent` → `PlayerController` 调用 `TryChangeState(Locomotion)`；`ActionState.Exit` 会 `Stop()` 招式。

### 5.3 ActionTransition

| 字段 | 说明 |
|------|------|
| `condition` | 当前仅运行时处理 `AnimationEnd` |
| `targetAction` | 衔接目标；`null` 表示 `Stop` 回 Locomotion |
| `priority` | 降序取首个匹配项 |

### 5.4 ActionStartBehaviorType

| 值 | 行为 |
|----|------|
| `FaceBufferedMoveIntent` | 起手时朝 `InputManager` 的移动意图（或缓冲意图）转向 |

由 `PlayerController` 实现 `IActionStartContext` 并注入 `ActionRuntimeController`。

### 5.5 PlayerActionSet

```csharp
struct ActionEntry {
    InputActionReference input;   // 运行时 inputId = action.name
    ActionDefinition startAction;
}
```

| 方法 | 说明 |
|------|------|
| `TryGetStartAction(inputId, out action)` | Locomotion 起手查找 |
| `CollectEntryInputReferences()` | 供 `InputReader.ConfigureDiscreteInputs` 轮询按下 |

创建菜单：`Create → ACT → Combat → Player Action Set`

### 5.6 输入 id 约定

- 离散输入 id = Input System **Action 名**（如 `Attack`、`Dodge`），由 `InputBindingUtils.GetInputId` 解析。
- `InputIds.Move` 为移动取消语义占位，实际由 `HasMoveIntent` 判定，非离散 `NotifyPressed`。

---

## 6. 运行时流程

### 6.1 每帧总览

```
PlayerController.Update（DefaultExecutionOrder -50，先于状态机）
  1. IngestInput()        → InputManager.IngestFrame(CaptureFrame())
  2. ProcessGameplayInput → 离开 Action 清缓冲；招式中尝试移动取消
  3. ExecuteMovement()    → 非 Action 时执行水平位移
  4. ApplyGravity()

PlayerStateMachine.Update
  → ActionState / LocomotionState.Tick
      → ActionRuntime.Tick(dt)   // 仅 Action 状态
```

### 6.2 起手（Locomotion → Action）

```
离散输入按下 → InputManager.NotifyPressed(inputId)
  → HandleDiscreteInput
      → TryStartByInput(inputId)   // PlayerActionSet 查起手招
      → TryChangeState(Action)
      → ExecuteStartBehaviors      // 如 FaceBufferedMoveIntent
      → BeginAction：PlayClip + RootMotion 开关
```

### 6.3 招式中（Action 取消 / 连段）

```
离散输入按下 → InputManager.Buffer(inputId)

ActionRuntime.Tick:
  → TryResolveCancelWindows()
      → 按 priority 遍历 CancelType.Action 窗口
      → HasBuffer(allowedInput) → Consume → TransitionTo(targetAction)

Movement 取消（并行，PlayerController）:
  → HasMoveIntent && CanCancelByMovement → Locomotion
```

### 6.4 收招（AnimationEnd）

```
elapsed >= DurationSeconds
  → ResolveEndTransitions()
      → 首个 AnimationEnd Transition 有 target → TransitionTo
      → 无 target → Stop() → ActionState 下一帧切 Locomotion
```

### 6.5 位移

| 模式 | 执行位置 |
|------|----------|
| Root Motion | `CharacterRootMotionReceiver.OnAnimatorMove` → 父节点 `CC.Move`；重置子模型 localPose |
| 脚本位移 | `ActionRuntimeController.ApplyScriptedDisplacement`；`displacementDistance` 可为负 |

---

## 7. 使用方式

### 7.1 配置出招表（PlayerActionSet）

1. `Create → ACT → Combat → Player Action Set`
2. **Entries** 添加行：`Input` 拖 `GameInputActions` 中的 Action（如 `Player/Attack`）；`Start Action` 拖起手 `ActionDefinition`
3. 在 `ActionRuntimeController` 的 **Action Set** 字段绑定该资产
4. `InputReader` 无需重复配置离散输入（`PlayerController.Awake` 自动 `ConfigureDiscreteInputs`）

### 7.2 配置单条招式与三连招

**Attack1 → Attack2 → Attack3 示例：**

1. 在 `player_attack_1` 的 **Cancel Windows** 添加：
   - `cancelType: Action`，`allowedInputs: [Attack]`，`targetAction: player_attack_2`
   - `startFrame` / `endFrame` 对齐后摇可接招区间，`priority` 按需设置
2. `player_attack_2` 同理指向 `player_attack_3`
3. `player_attack_3` 可不配 Action 取消窗，靠 `transitions` 或自然 `Stop` 收招
4. **Movement 取消**（可选）：添加 `cancelType: Movement` 窗，后摇末段允许走路取消

**End Transitions（可选）：**

- `condition: AnimationEnd`，`targetAction: null` → 播完回 Locomotion

### 7.3 配置 Root Motion

1. 攻击 FBX：**Root Transform Position (XZ)** 不 Bake Into Pose
2. 招式资产 `useRootMotion = true`，`displacementDistance = 0`
3. 详见前文 §6.5；子模型漂移由 `CharacterRootMotionReceiver` 重置 localPose

### 7.4 闪避起手朝向（Start Behavior）

1. 闪避 `ActionDefinition` → **Start Behaviors** 勾选 `FaceBufferedMoveIntent`
2. 玩家按住移动再按闪避键时，起手瞬间朝向缓冲移动方向

### 7.5 Prefab 检查清单

| 组件 | 配置 |
|------|------|
| `ActionRuntimeController` | `actionSet` → `PlayerActionSet`；`animationController` 自动解析 |
| `InputReader` | `inputActions` → `GameInputActions.inputactions` |
| `PlayerController` | 相机引用；自动绑定 `InputManager` / `IActionStartContext` |
| `CharacterRootMotionDriver` | 随 `RequireComponent` 自动添加 |

### 7.6 验证清单

- [ ] 出招表 Entries 非空，攻击键能从 Locomotion 起手
- [ ] 后摇内再按攻击，CancelWindow 衔接下一段
- [ ] 后摇内推摇杆，Movement 取消窗内可回 Locomotion
- [ ] 第三段播完回 Idle/Walk/Run
- [ ] Root Motion 攻击时整体前移、模型不相对父节点漂移
- [ ] 闪避（若配置）起手朝向移动方向

---

## 8. 接口摘要

### IActionRuntime

```csharp
bool IsPlaying { get; }
ActionDefinition CurrentAction { get; }
bool CanCancelByMovement { get; }
bool TryStartByInput(string inputId);
bool TryStart(ActionDefinition action);
void BindComboInput(IActionComboInput comboInput);
void BindActionStartContext(IActionStartContext startContext);
void Tick(float deltaTime);
void Stop();
```

### IActionComboInput

```csharp
bool HasBuffer(string inputId);
bool TryConsumeBuffer(string inputId);
```

### InputManager（仅 PlayerController 持有）

```csharp
void RegisterPressed(string inputId, Action handler);
void IngestFrame(PlayerInputFrame frame);
void Buffer(string inputId);
bool HasBuffer(string inputId);
bool TryConsumeBuffer(string inputId);
Vector2 MoveIntent / BufferedMoveIntent { get; }
bool HasMoveIntent { get; }
```

---

## 9. 已知限制

| 限制 | 说明 |
|------|------|
| 无 Hitbox / 伤害 | 攻击无判定 |
| `ActionTransition` 仅 `AnimationEnd` | `OnHitConfirm` / `OnWhiff` 未实现 |
| 无 `ActionPhase` | 无敌帧、霸体、受击打断未接入 |
| 同时仅一条招式 | 无叠加层 |
| 敌人未接入 | 结构可复用 `ActionRuntimeController` |
| **资产迁移** | 部分 `ActionDefinition` / `PlayerActionSet` 资产可能仍含旧字段（`nextAction`、`attackChain` 等），需在 Editor 中改为 `cancelWindows` + `entries` |

### 后续路线

1. 资产迁移与 `CancelWindow` 调参稳定
2. `ActionPhase` + Hitbox + Combat
3. 扩展 `ActionTransition` 条件
4. `ActionEditorWindow`（M5）

---

## 10. 相关文件

### 脚本

```
Assets/Scripts/Combat/Actions/
  ActionDefinition.cs, ActionRuntimeController.cs
  CancelWindow.cs, CancelType.cs
  ActionTransition.cs, ActionTransitionCondition.cs
  ActionStartBehaviorType.cs, IActionStartContext.cs
  PlayerActionSet.cs, CombatActionType.cs

Assets/Scripts/Input/
  InputManager.cs, InputReader.cs, PlayerInputFrame.cs
  IActionComboInput.cs, InputManagerComboInput.cs
  InputIds.cs, IPlayerInputSource.cs

Assets/Scripts/Player/
  PlayerController.cs, PlayerStateMachine.cs

Assets/Scripts/Character/
  StateMachine/IActionRuntime.cs, States/ActionState.cs, States/LocomotionState.cs
  Animation/CharacterRootMotionDriver.cs, CharacterAnimationController.cs
```

### 资产（Editor 维护）

```
Assets/Data/Combat/Actions/Player/
  player_attack_*.asset, player_dodge_*.asset, PlayerActionSet.asset
Assets/Prefabs/Player/Player_KatanaGirl.prefab
Assets/Scripts/Input/GameInputActions.inputactions
```

---

## 11. 变更日志

| 日期 | 变更 |
|------|------|
| 2026-06-17 | 初版：M2' 简化连招 + 位移 |
| 2026-06-17 | Root Motion、`InputManager` 输入路由 |
| 2026-06-17 | **全面重写**：`CancelWindow` / `ActionTransition` / `PlayerActionSet`；移除 `nextAction`/`comboLink`/`defaultAttack`；`InputManager` 帧摄入与多 id 缓冲；Movement 取消与 `FaceBufferedMoveIntent` |
