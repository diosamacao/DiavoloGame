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

**当前阶段：** Phase A 早期（M2'）— 数据驱动招式播放 + 简化三连招，**不含** Hitbox、受击、取消窗口编辑器。

---

## 2. 功能状态总览

| 能力 | 状态 | 说明 |
|------|------|------|
| `ActionDefinition` ScriptableObject | ✅ 已实现 | 简化字段；`OnValidate` 自动算帧数 |
| `ActionRuntimeController` 招式播放 | ✅ 已实现 | 播 Clip、计时结束、连招衔接 |
| `ActionState` + 动画锁定 | ✅ 已实现 | 招式期间 Locomotion 动画不覆盖 |
| `PlayClip` 直播 AnimationClip | ✅ 已实现 | Animator Controller 仅管 Locomotion |
| 攻击输入 → 进入 Action | ✅ 已实现 | `LocomotionState` 起手 |
| 招式中攻击输入缓冲 + 连段 | ✅ 已实现 | `nextAction` + `comboLink` 帧窗口 |
| `CombatActionType` 枚举 | ✅ 已实现 | Attack/Dodge/Skill/Hit 等类型预留 |
| `ActionPhase` / Hitbox / Hurtbox | ⬜ 未实现 | 见 ACTION_EDITOR §3.2–3.5 |
| `CancelWindow` / `ActionTransition` | ⬜ 未实现 | 当前用 `nextAction` 折代替换 |
| `ActionGraph` 连招图 | ⬜ 未实现 | — |
| Combat 命中 / 伤害 | ⬜ 未实现 | `Combat/` 层待建 |
| `Hit` 受击状态 | ⬜ 未实现 | 枚举已预留 |
| 移动取消（Movement Cancel） | ⬜ 未实现 | 招式结束才回 Locomotion |
| `ActionEditorWindow` | ⬜ 未实现 | M5 目标 |

---

## 3. 设计原理

### 3.1 核心原则（已贯彻）

1. **数据驱动** — 招式动画、时长、连段关系写在 `ActionDefinition` SO，不在状态机里硬编码 `if (attackCount == 2)`。
2. **Animator 分工** — `CharacterAnimationProfile` + Animator Controller **只驱动 Locomotion**（Idle/Walk/Run）；招式由 `ActionDefinition.animationClip` 引用，经 `PlayClip()` 直播。
3. **状态机薄层** — `CharacterStateMachine` 只管「是否在放招」；逐帧招式逻辑集中在 `ActionRuntimeController`。
4. **角色无关接口** — `IActionRuntime` 挂在 `CharacterContext`，玩家与后续敌人可共用同一执行器。
5. **渐进扩展** — 当前用 `nextAction` + 帧窗口实现连段，Schema 与 [ACTION_EDITOR.md](./ACTION_EDITOR.md) 对齐后，可平滑迁移到 `CancelWindow` / `ActionGraph`。

### 3.2 与目标架构的差异（Demo 折中）

| 目标设计（ACTION_EDITOR） | 当前实现 |
|---------------------------|----------|
| `CancelWindow` + `allowedInputs` 消费输入缓冲 | `comboLinkStartFrame` / `comboLinkEndFrame` + 攻击键缓冲 |
| `ActionTransition(AnimationEnd)` 控制收招 | 播放时长到达 `DurationSeconds` 后 `Stop()` |
| `UpdateFrame(frameIndex)` 逐帧 Logic Tick | 仅用 `elapsed` 秒 + `sampleRate` 换算帧索引 |
| `defaultAttack` 仅起手；后续由 Graph / Cancel 解析 | `defaultAttack` 起手；连段靠 SO 内 `nextAction` 链 |

---

## 4. 架构设计

### 4.1 模块关系

```
InputReader (Attack)
       │
       ▼
PlayerStateMachine ── CharacterContext ──┬── ICharacterInput
       │                                  ├── IActionRuntime (ActionRuntimeController)
       │                                  └── CharacterAnimationController
       │
       ├── LocomotionState ── TryStartDefaultAction() → Action
       └── ActionState ── BufferAttackInput() + Tick() → Locomotion（结束）

ActionRuntimeController
       └── ActionDefinition SO → PlayClip(animationClip)
```

### 4.2 状态机职责

| 状态 | 职责 | 与动作系统交互 |
|------|------|----------------|
| `Locomotion` | 移动动画、检测攻击输入 | `TryStartDefaultAction()` 成功 → 切 `Action` |
| `Action` | 锁定 Locomotion 动画 | `ActionRuntime.Tick()`；结束 → 切 `Locomotion` |
| `Hit` | （未实现）受击硬直 | 预留；将播放 `actionType: Hit` 的 `ActionDefinition` |
| `Death` | （未实现）死亡 | 预留 |

`ActionState` 进入时 `Animation.SetLocked(true)`，阻止 `LocomotionState` 的 `Play(Idle/Walk/Run)` 覆盖招式；退出时 `Stop()` + `SetLocked(false)` + `ResetPlaybackState()`。

### 4.3 动画双轨

```
┌─────────────────────────────────────────────────────────┐
│ CharacterAnimationController                            │
├────────────────────────┬────────────────────────────────┤
│ Locomotion 轨          │ Action 轨（招式）               │
│ Play(AnimationKey)     │ PlayClip(AnimationClip)        │
│ 依赖 AnimationProfile  │ 依赖 ActionDefinition          │
│ locked 时跳过          │ ActionState 期间由 Runtime 调用  │
└────────────────────────┴────────────────────────────────┘
```

**约定：** 招式 `AnimationClip` 须与 Animator Controller 中**同名 State** 存在（`CrossFadeInFixedTime(clip.name)`），但状态机参数不由 AC 驱动。

### 4.4 类与接口

| 类型 | 路径 | 职责 |
|------|------|------|
| `ActionDefinition` | `Combat/Actions/ActionDefinition.cs` | 招式 SO 数据 |
| `CombatActionType` | `Combat/Actions/CombatActionType.cs` | 招式分类枚举 |
| `ActionRuntimeController` | `Combat/Actions/ActionRuntimeController.cs` | 运行时播放与连段 |
| `IActionRuntime` | `Character/StateMachine/IActionRuntime.cs` | 执行器抽象 |
| `ActionState` | `Character/StateMachine/States/ActionState.cs` | Action 状态行为 |
| `LocomotionState` | `Character/StateMachine/States/LocomotionState.cs` | 起手攻击 |
| `ICharacterInput` | `Character/StateMachine/ICharacterInput.cs` | 输入抽象（当前仅攻击） |
| `CharacterAnimationController` | `Character/Animation/CharacterAnimationController.cs` | `Play` / `PlayClip` |

---

## 5. 数据模型（当前）

### 5.1 ActionDefinition 字段

| 字段 | 类型 | 默认 / 行为 | 说明 |
|------|------|-------------|------|
| `id` | `string` | `"player_attack_1"` | 唯一标识；空时 `OnValidate` 用资产名 |
| `displayName` | `string` | `"Attack 1"` | 显示名 |
| `animationClip` | `AnimationClip` | — | **必填**；招式动画 |
| `sampleRate` | `float` | `30` | 逻辑帧率；与 `totalFrames` 推算时长 |
| `totalFrames` | `int` | 自动计算 | `Round(clip.length × sampleRate)` |
| `actionType` | `CombatActionType` | `Attack` | 分类预留 |
| `crossFadeDuration` | `float` | `0.1` | 切入招式的 CrossFade 时间 |
| `nextAction` | `ActionDefinition` | `null` | 连段下一段；空则无连段 |
| `comboLinkStartFrame` | `int` | 自动：`totalFrames × 0.5` | 可接招帧区间起点 |
| `comboLinkEndFrame` | `int` | 自动：`totalFrames - 1` | 可接招帧区间终点 |

**计算属性：**

- `DurationSeconds` = `totalFrames / sampleRate`（无帧数时回退 `clip.length`）
- `HasComboLink` = `nextAction != null`
- `IsInComboLinkWindow(elapsedSeconds)` — 当前帧 ∈ `[comboLinkStartFrame, comboLinkEndFrame]`

**OnValidate 自动行为：** 绑定 Clip 后刷新 `totalFrames`；若配置了 `nextAction` 且连段帧为 0，则写入默认窗口（后半段至末帧）。

### 5.2 CombatActionType

```csharp
Attack = 0, Dodge = 1, Skill = 2, Hit = 3, Death = 4, Locomotion = 5
```

当前运行时**未**按类型分支；仅作资产标注与未来扩展。

### 5.3 资产目录

```
Assets/Data/Combat/Actions/
└── Player/
    ├── player_attack_1.asset
    ├── player_attack_2.asset
    └── player_attack_3.asset
```

创建菜单：`Create → ACT → Combat → Action Definition`

---

## 6. 实现细节

### 6.1 运行时主流程

```
[Locomotion]
  AttackPressedThisFrame?
    → ActionRuntime.TryStartDefaultAction()  // 播放 defaultAttack
    → StateMachine → Action

[Action] Enter
  → Animation.SetLocked(true)

[Action] Tick (每帧)
  → AttackPressedThisFrame? → BufferAttackInput()
  → ActionRuntime.Tick(deltaTime)
      → elapsed += dt
      → TryConsumeBufferedCombo()
          → 有缓冲 && 在 comboLink 窗口?
          → BeginAction(nextAction)   // 切下一招，重置计时
      → elapsed >= DurationSeconds?
          → Stop()

  → !IsPlaying → StateMachine → Locomotion

[Action] Exit
  → ActionRuntime.Stop()
  → Animation.SetLocked(false)
  → Animation.ResetPlaybackState()

[Locomotion] 恢复 Idle/Walk/Run
```

### 6.2 ActionRuntimeController 关键逻辑

**起手 `TryPlay(action)`：**

- 拒绝条件：已在播放、action 为空、无 Clip、无 `animationController`
- `BeginAction`：记录 `_current`、清零 `_elapsed`、`PlayClip(clip, crossFadeDuration)`

**连段 `TryConsumeBufferedCombo()`：**

- 仅在 `_attackBuffered == true` 且 `IsInComboLinkWindow(_elapsed)` 时触发
- 成功则 `BeginAction(nextAction)`，并清除缓冲
- **同帧优先连段**：连段成功后本帧不再检查结束计时

**结束 `Stop()`：**

- 清空 `_current`、`_isPlaying`、`_elapsed`、`_attackBuffered`
- 不主动切动画；由 `ActionState` 切回 `Locomotion` 后 `LocomotionState` 驱动 Idle/Walk/Run

### 6.3 输入与缓冲

| 环节 | 实现 |
|------|------|
| 输入源 | `InputReader.AttackPressedThisFrame`（Input System `Player/Attack`） |
| 起手 | `LocomotionState` 检测本帧攻击 → `TryStartDefaultAction()` |
| 缓冲 | `ActionState` 在招式播放中检测攻击 → `BufferAttackInput()`（布尔标记，非队列） |
| 消费 | 仅在 `comboLink` 帧窗口内消费，避免过早连段 |

> 与目标方案对比：完整版将在全程缓冲多种输入，由 `CancelWindow.allowedInputs` 决定消费；当前仅支持**攻击键单缓冲**。

### 6.4 移动与 Action 互斥

`PlayerController` 在 `CurrentStateType == Action` 时：

- 不处理水平移动输入（`moveInputMagnitude` 置 0）
- 重力仍生效

尚未实现「后摇移动取消」；玩家须等招式播完回到 `Locomotion` 才能移动。

### 6.5 帧索引换算

```csharp
int frame = FloorToInt(elapsedSeconds * sampleRate);
bool inWindow = frame >= comboLinkStartFrame && frame <= comboLinkEndFrame;
```

**注意：** 尚未实现统一的 `UpdateFrame(frameIndex)` 入口；后续 Hitbox / Phase / Event 应与此换算共用，以保证编辑器 scrub 与运行时一致（见 ACTION_EDITOR §2.3）。

---

## 7. 使用方式

### 7.1 新建一条招式资产

1. Project 窗口右键 → **Create → ACT → Combat → Action Definition**
2. 保存到 `Assets/Data/Combat/Actions/Player/`（建议命名 `player_attack_N`）
3. Inspector 填写：
   - **Animation Clip**：拖入招式动画（须与 Animator 中 State 名一致）
   - **Id / Display Name**：如 `player_attack_1` / `Attack 1`
   - **Sample Rate**：与动画导入帧率一致（常用 30）
   - **Total Frames**：指定 Clip 后由 `OnValidate` 自动填充，可手动微调
   - **Action Type**：`Attack`
   - **Cross Fade Duration**：默认 `0.1`

### 7.2 配置三连招

以 Attack1 → Attack2 → Attack3 为例：

| 资产 | nextAction | comboLinkStartFrame | comboLinkEndFrame |
|------|------------|---------------------|-------------------|
| `player_attack_1` | → attack_2 | 约 50% 总帧 | 末帧 |
| `player_attack_2` | → attack_3 | 按动画后摇调整 | 末帧 |
| `player_attack_3` | （空） | — | — |

1. 在 `player_attack_1` 的 **Next Action** 拖入 `player_attack_2`
2. 调整 **Combo Link Start/End Frame**：玩家须在该帧区间内再次按下攻击，才会接下一段
3. 第三段不配置 `nextAction`，播完自动回 Locomotion

**调参建议：** 窗口偏前 = 连段手感紧；偏后 = 更偏「后摇接招」。在 Scene 播放时观察动画后摇，对齐 Start Frame。

### 7.3 玩家 Prefab 挂载

在 **Player_KatanaGirl**（或同类玩家 Prefab）上确认：

| 组件 | 必要配置 |
|------|----------|
| `ActionRuntimeController` | `Animation Controller` → 同物体 `CharacterAnimationController` |
| | `Default Attack` → `player_attack_1`（起手招式） |
| `PlayerStateMachine` | 依赖自动满足（`RequireComponent` 链） |
| `InputReader` | `Input Actions` → `GameInputActions.inputactions` |
| `CharacterAnimationController` | `Profile` → 角色 AnimationProfile；`Animator` → 子物体 Animator |

`PlayerStateMachine.ConfigureContext` 已将 `ICharacterInput` 与 `IActionRuntime` 注入 `CharacterContext`，无需额外代码。

### 7.4 Animator 要求

1. Animator Controller（如 `ACT_Runtime`）包含 Locomotion 状态（Idle/Walk/Run）及**与招式 Clip 同名的 State**
2. 招式 State 不需要 Transition 连线；运行时通过 `CrossFadeInFixedTime(clip.name)` 切入
3. Locomotion 与招式建议同一 Layer（`CharacterAnimationController.layerIndex`，默认 0）

### 7.5 运行时验证清单

- [ ] 站立按攻击 → 播放第一段，移动停止
- [ ] 第一段后摇内再按攻击 → 进入第二段
- [ ] 窗口外按攻击 → 忽略，第一段播完回 Idle
- [ ] 第三段播完 → 自动回 Locomotion，可移动
- [ ] 招式中 `LocomotionState` 不会把动画切回 Walk/Run

### 7.6 代码扩展入口

**播放指定招式（非 defaultAttack）：**

```csharp
var runtime = GetComponent<IActionRuntime>() as ActionRuntimeController;
if (runtime.TryPlay(someActionDefinition))
    stateMachine.TryChangeState(CharacterStateType.Action);
```

**查询状态：**

```csharp
runtime.IsPlaying;
runtime.CurrentAction;  // ActionRuntimeController 公开属性
```

---

## 8. 与状态机 / 输入接口

### 8.1 IActionRuntime

```csharp
bool IsPlaying { get; }
bool TryStartDefaultAction();
void BufferAttackInput();
void Tick(float deltaTime);
void Stop();
```

状态机只依赖此接口，不直接引用 `ActionDefinition` 类型（除 `ActionRuntimeController` 内部）。

### 8.2 ICharacterInput（当前）

```csharp
bool AttackPressedThisFrame { get; }
```

后续扩展 Dodge / Skill 时，应同步扩展此接口，并由 `CancelWindow` 或等价逻辑消费。

### 8.3 CharacterContext 相关字段

| 字段 | 写入方 | 用途 |
|------|--------|------|
| `Input` | `PlayerStateMachine.ConfigureContext` | 攻击检测 |
| `ActionRuntime` | 同上 | 招式 Tick |
| `MoveInputMagnitude` | `UpdateContext` | Locomotion 动画选择 |

---

## 9. 已知限制与后续路线

### 9.1 当前限制

| 限制 | 影响 | 计划 |
|------|------|------|
| 无 Hitbox / 伤害 | 攻击无战斗判定 | Combat 模块 + `HitboxKeyframe` |
| 无 `CancelWindow` | 无法闪避取消、移动取消 | 迁移到 ACTION_EDITOR 数据模型 |
| 仅攻击键缓冲 | 无法多输入优先级 | 扩展 `ICharacterInput` + CancelWindow |
| 无 `Hit` 状态 | 受击无表现 | `HitState` + `actionType: Hit` 资产 |
| 无逐帧 `UpdateFrame` | 编辑器预览与运行时难统一 | `ActionRuntimeController` 重构 |
| `TryPlay` 同时只能播一条 | 无叠加层招式 | 按需求评估 |
| 敌人未接入 | 仅玩家可攻击 | 敌人挂同一 `ActionRuntimeController` |

### 9.2 建议迁移顺序

1. **连段** — `nextAction` → `CancelWindow(cancelType: Action)` + `ActionTransition`
2. **战斗** — 增加 `UpdateFrame` + `HitboxKeyframe` + Combat 查询
3. **收招** — `ActionTransition(AnimationEnd / OnHitConfirm / OnWhiff)`
4. **编辑器** — `ActionEditorWindow`（M5）替换纯 Inspector 调参

详见 [ACTION_EDITOR.md §4.2](./ACTION_EDITOR.md) 分期计划。

---

## 10. 相关文件索引

### 脚本

| 路径 |
|------|
| `Assets/Scripts/Combat/Actions/ActionDefinition.cs` |
| `Assets/Scripts/Combat/Actions/ActionRuntimeController.cs` |
| `Assets/Scripts/Combat/Actions/CombatActionType.cs` |
| `Assets/Scripts/Character/StateMachine/IActionRuntime.cs` |
| `Assets/Scripts/Character/StateMachine/States/ActionState.cs` |
| `Assets/Scripts/Character/StateMachine/States/LocomotionState.cs` |
| `Assets/Scripts/Character/StateMachine/CharacterContext.cs` |
| `Assets/Scripts/Character/StateMachine/CharacterStateMachine.cs` |
| `Assets/Scripts/Character/Animation/CharacterAnimationController.cs` |
| `Assets/Scripts/Player/PlayerStateMachine.cs` |
| `Assets/Scripts/Player/PlayerController.cs` |
| `Assets/Scripts/Input/InputReader.cs` |

### 资产与 Prefab（Editor 维护）

| 路径 |
|------|
| `Assets/Data/Combat/Actions/Player/player_attack_*.asset` |
| `Assets/Prefabs/Player/Player_KatanaGirl.prefab` |
| `Assets/Scripts/Input/GameInputActions.inputactions` |

---

## 11. 变更日志

| 日期 | 变更 |
|------|------|
| 2026-06-17 | 初版：基于当前代码归纳实现架构、数据模型、流程与使用说明 |
