# ACTGame — 动作系统技术实现文档

> 本文档描述**当前已落地**的动作系统：架构、实现细节、使用方式，以及与 [ACTION_EDITOR.md](./ACTION_EDITOR.md) 长期目标的对齐分析。  
> Last updated: 2026-06-17

---

## 1. 文档定位

| 文档 | 内容 |
|------|------|
| **本文档（ACTION_SYSTEM.md）** | 已实现功能、运行时行为、编辑器适配评估 |
| [ACTION_EDITOR.md](./ACTION_EDITOR.md) | 动作编辑器愿景、完整数据模型、分期规划 |
| [TECHNICAL.md](../.cursor/skills/actgame-architecture/TECHNICAL.md) | 全项目功能索引 |

**当前阶段：** Phase A 中后期 — 取消窗 / 收招 / 出招表 / 战斗模式 / 线性连招队列已落地；**不含** Hitbox、Phase、ActionEvent、编辑器 UI。

---

## 2. 功能状态总览

| 能力 | 状态 | 说明 |
|------|------|------|
| `ActionDefinition` SO | ✅ 已实现 | 动画、帧数、CancelWindow、Transition、位移、起手行为 |
| `CancelWindow`（Action / Movement） | ✅ 已实现 | 帧窗口 + priority；Action 取消不直接绑目标招 |
| `ActionComboSequence` 线性连招 | ✅ 已实现 | 出招表 Entry 绑定；Cancel 按队列进位 |
| `ActionTransition` | 🟡 部分 | `AnimationEnd`、`AtFrame`；无 OnHit / OnWhiff |
| `PlayerActionSet` + `CombatModeProfile` | ✅ 已实现 | 多战斗模式出招表；模式切换 Locomotion Profile |
| `CombatModeController` | ✅ 已实现 | Immediate / OnNextLocomotion / StopCurrentAction |
| `ActionRuntimeController` | ✅ 已实现 | 播放、取消、Transition、Root Motion / 脚本位移 |
| `InputManager` + `PlayerInputFrame` | ✅ 已实现 | 帧快照、多 id 缓冲、移动意图 |
| `ActionStartBehavior` | 🟡 部分 | `FaceBufferedMoveIntent`、`SwitchCombatMode` |
| Root Motion 桥接 | ✅ 已实现 | `CharacterRootMotionDriver` + Receiver |
| `ActionState` + 动画锁定 | ✅ 已实现 | 薄层状态机 |
| `ActionPhase` / Hitbox / Hurtbox / `ActionEvent` | ⬜ 未实现 | 编辑器 Must Have，运行时待建 |
| `ActionGraph` 节点图 | ⬜ 未实现 | 由 `ActionComboSequence` 线性折中 |
| `UpdateFrame(frameIndex)` 统一 Logic Tick | ⬜ 未实现 | 仍用 `elapsed` + `sampleRate` 换算 |
| Combat 命中 / 伤害 / `Hit` 状态 | ⬜ 未实现 | — |
| `ActionEditorWindow` | ⬜ 未实现 | M5 目标 |

---

## 3. 架构总览

### 3.1 模块关系

```
InputReader.CaptureFrame()
       │
       ▼
PlayerController ── InputManager（唯一持有者）
       │    ├─ RegisterPressed(inputId) → 起手 / Buffer
       │    ├─ MoveIntent / BufferedMoveIntent
       │    └─ Movement 取消 → Locomotion
       │
       ├── CombatModeController ── CombatModeProfile
       │         └─ mode → PlayerActionSet → ActionComboSequence
       │
       ├── ActionRuntimeController
       │         ├─ TryStartByInput / CancelWindow / Transition
       │         └─ ActionDefinition（单招数据）
       │
       └── PlayerStateMachine
                 ├─ LocomotionState（Idle/Walk/Run）
                 └─ ActionState（Tick Runtime → 结束回 Locomotion）
```

### 3.2 职责分层

| 层 | 职责 |
|----|------|
| `InputReader` | 设备 → `PlayerInputFrame` |
| `InputManager` | 摄入帧、离散缓冲、移动意图、回调注册 |
| `PlayerController` | 输入路由、移动执行、移动取消、起手切状态 |
| `CombatModeController` | 战斗模式、出招表切换、Locomotion Profile |
| `ActionRuntimeController` | 招式播放、Cancel 解析、Transition、位移 |
| `ActionState` / `LocomotionState` | 动画锁与 Locomotion 动画 |

### 3.3 设计原则（已贯彻）

1. **数据驱动** — 单招数据在 `ActionDefinition`；连招队列在 `ActionComboSequence`；起手映射在 `PlayerActionSet`。
2. **输入与玩法解耦** — 状态机不读输入；`InputManager` 仅 `PlayerController` 持有。
3. **状态机薄层** — `Action` 状态只 Tick `IActionRuntime`。
4. **Animator 双轨** — Locomotion 走 Profile；招式 `PlayClip`。
5. **角色无关执行器** — `ActionRuntimeController` 可复用于敌人（输入源可替换）。

---

## 4. 数据模型

### 4.1 ActionDefinition（单招）

| 区块 | 字段 | 说明 |
|------|------|------|
| 基础 | `id`, `displayName`, `animationClip`, `sampleRate`, `totalFrames`, `actionType`, `crossFadeDuration` | 动画与标识 |
| Cancel Windows | `cancelWindows[]` | 帧区间、`cancelType`、`allowedInputs`、`priority` |
| Transitions | `transitions[]` | `condition`, `startFrame`, `targetAction`, `priority` |
| Start Behaviors | `startBehaviors[]` | 起手副作用 |
| Combat Mode | `switchCombatModeTarget`, `switchCombatModePolicy` | 配合 `SwitchCombatMode` 行为 |
| Movement | `useRootMotion`, `displacementDistance`, 帧窗口 | Root Motion 或脚本位移 |

**帧换算：** `frame = FloorToInt(elapsed * sampleRate)`

### 4.2 CancelWindow

| 字段 | 说明 |
|------|------|
| `startFrame` / `endFrame` | 生效帧区间 |
| `cancelType` | `Action`：消费缓冲并衔接下一招；`Movement`：由 `PlayerController` 检测移动意图 |
| `allowedInputs` | `InputActionReference[]`；运行时 id = Action 名 |
| `priority` | 降序扫描，首个匹配生效 |

**与 ACTION_EDITOR 的差异：** 当前 **无 `targetAction` 字段**。Action 取消的下一招由 `PlayerActionSet` → `ActionComboSequence.TryResolveNext` 解析，而非取消窗直接指向目标 SO。

### 4.3 ActionTransition

| `condition` | 运行时行为 |
|-------------|------------|
| `AnimationEnd` | `elapsed >= DurationSeconds` 时触发 |
| `AtFrame` | `frame >= startFrame` 时每帧检查（可提前自动衔接） |

按 `priority` 降序；`targetAction == null` 则 `Stop`。

### 4.4 ActionComboSequence（线性连招队列）

```
steps[]: [Attack1, Attack2, Attack3]
leafPolicy: LoopToRoot | StopCombo
```

| 方法 | 行为 |
|------|------|
| `GetStartAction()` | `steps[0]`，Locomotion 起手 |
| `TryResolveNext(inputId, current, out next)` | 当前招在队列中则 `index+1`；不在队列则回 `steps[0]`；末段按 `leafPolicy` |

绑定在 `PlayerActionSet.ActionEntry.comboSequence`。

### 4.5 PlayerActionSet / CombatModeProfile

```
CombatModeProfile
  └─ CombatModeEntry[] (mode, actionSet, locomotionProfile)
       └─ PlayerActionSet
            └─ ActionEntry[] (input → ActionComboSequence)
```

| 组件 | 职责 |
|------|------|
| `PlayerActionSet.TryGetStartAction` | Locomotion 起手 |
| `PlayerActionSet.TryResolveNext` | 招内 Cancel 进位 |
| `CombatModeController` | 运行时当前 mode、挂起切换、Locomotion Profile |

### 4.6 输入 id

- 离散输入 id = Input System **Action 名**（`Attack`、`Dodge` 等）。
- 移动取消不走路由表，由 `InputManager.HasMoveIntent` + `CancelType.Movement` 窗口判定。

---

## 5. 运行时流程

### 5.1 每帧顺序

```
PlayerController.Update（ExecutionOrder -50）
  1. IngestInput
  2. ProcessGameplayInput（离 Action 清缓冲 / 应用挂起 mode / 移动取消）
  3. ExecuteMovement
  4. ApplyGravity

PlayerStateMachine.Update
  → ActionState.Tick → ActionRuntime.Tick
```

### 5.2 起手（Locomotion → Action）

```
离散输入 → InputManager.NotifyPressed
  → TryStartByInput → ActionComboSequence.RootAction
  → ExecuteStartBehaviors → BeginAction(PlayClip)
  → TryChangeState(Action)
```

### 5.3 招内 Cancel（连段）

```
输入 → Buffer(inputId)

ActionRuntime.Tick:
  → 按 priority 扫描 CancelType.Action 窗口
  → HasBuffer(allowedInput) → Consume
  → actionSet.TryResolveNext → TransitionTo(next)
```

### 5.4 移动取消

```
招式中 HasMoveIntent && CanCancelByMovement
  → PlayerController 切 Locomotion
  → ActionState.Exit → Stop()
```

### 5.5 收招（Transition / 自然结束）

```
每帧 TryResolveTransitions（AtFrame 可提前触发）
  → 无匹配且 elapsed >= Duration → Stop
  → ActionState 下一帧回 Locomotion
```

### 5.6 战斗模式切换

- 起手行为 `SwitchCombatMode` 或外部 `CombatModeController.TrySetMode`。
- `OnNextLocomotion`：招式中挂起，回 Locomotion 后 `ApplyPendingModeIfReady`。
- 切换 mode 可换 `PlayerActionSet` 与 `CharacterAnimationProfile`（Locomotion）。

---

## 6. 使用方式（Editor）

### 6.1 配置三连招

1. 创建 `ActionComboSequence`，`steps` = [attack_1, attack_2, attack_3]。
2. `PlayerActionSet` Entry：`Attack` → 上述 Sequence。
3. 各 `ActionDefinition` 的 **Cancel Windows** 添加 `CancelType.Action` 窗 + `allowedInputs: [Attack]`（无需填目标招）。
4. 可选 **Movement** 取消窗 + `ActionTransition(AnimationEnd)` 收招。

### 6.2 多战斗模式

1. 创建 `CombatModeProfile`，配置 `Katana` / `Beast` 等 mode 的 `PlayerActionSet` 与 `LocomotionProfile`。
2. `CombatModeController.profile` 绑定该资产。
3. 招式需切模式时：`Start Behaviors` 勾选 `SwitchCombatMode` 并填目标 mode / policy。

### 6.3 Prefab 检查

| 组件 | 配置 |
|------|------|
| `CombatModeController` | `CombatModeProfile` |
| `ActionRuntimeController` | 依赖 `CombatModeController` 解析出招表 |
| `InputReader` | `GameInputActions`；离散输入由 Profile 并集自动注入 |
| `PlayerController` | 自动注册全部 mode 的 Entry |

---

## 7. 与 ACTION_EDITOR 的对齐分析

> **阅读方式：** ✅ 已对齐 · 🟡 部分对齐 / 有偏差 · ⬜ 未实现 · 🔀 项目扩展（编辑器文档未覆盖）

### 7.1 总体结论

| 维度 | 评估 | 说明 |
|------|------|------|
| **技术路线** | ✅ 一致 | SO 帧表 + `ActionRuntimeController` + 自研 Editor（路线 A） |
| **核心单招 Schema** | 🟡 约 40% | 基础字段 + Cancel/Transition 已有；Phase/Hitbox/Event 缺失 |
| **连招编排** | 🟡 有偏差 | 线性 `ActionComboSequence` 代替 `ActionGraph` / Cancel 内 `targetActionId` |
| **运行时 Tick** | 🟡 有偏差 | 无统一 `UpdateFrame`；编辑器预览需补入口 |
| **输入与取消语义** | ✅ 基本一致 | Action/Movement 取消、priority、缓冲消费 |
| **编辑器 UI 适配** | ⬜ 未开始 | 数据结构可部分复用；需补轨道类型与校验 |

**结论：** 当前架构**方向正确**，已为实现动作编辑器打好「单招 + 取消窗 + 过渡 + 执行器」主干；**连招与战斗模式**做了 Demo 期折中，编辑器落地时需明确是**延续折中**还是**回迁到 ACTION_EDITOR 完整模型**。

### 7.2 模块对照表

| ACTION_EDITOR 概念 | 当前实现 | 对齐度 | 编辑器适配备注 |
|--------------------|----------|--------|----------------|
| `ActionDefinition` | `ActionDefinition.cs` | 🟡 | 缺 `tags`, `ActionPhase[]`, `ActionEvent[]`, Hitbox/Hurtbox, `damageWeight` |
| `CancelWindow` | `CancelWindow.cs` | 🟡 | 有帧区间/type/priority/inputs；**无 `targetActionId`**，改由 ComboSequence 解析 |
| `ActionTransition` | `ActionTransition.cs` | 🟡 | 有 `AnimationEnd`；新增 `AtFrame`（编辑器文档未列）；缺 OnHit/OnWhiff/OnBlocked |
| `ActionGraph` | `ActionComboSequence` | 🔀 偏差 | 线性队列 vs 节点图；编辑器 M7 图编辑器需评估迁移或并存 |
| `CharacterCombatProfile` | `CombatModeProfile` + `PlayerActionSet` | 🔀 扩展 | 多模式武器切换；编辑器需否纳入「角色战斗根配置」待定义 |
| `ActionRuntimeController` | 已实现 | ✅ | 编辑器预览应共用同一套 Cancel/Transition 解析 |
| `UpdateFrame(frameIndex)` | 未实现 | ⬜ | **编辑器 Phase C 阻塞项**：预览与 Play Mode 须统一 |
| `ActionPhase` | 未实现 | ⬜ | 时间轴 Phases 轨道无数据源 |
| `HitboxKeyframe` / `HurtboxKeyframe` | 未实现 | ⬜ | 时间轴 Hitbox 轨道无数据源 |
| `ActionEvent` | 未实现 | ⬜ | VFX/SFX/顿帧轨道无数据源 |
| `ActionEditorWindow` | 未实现 | ⬜ | M5 目标 |
| GM 热重载 | 未实现 | ⬜ | Phase B 建议提前落地 |
| Logic Tick = 编辑器帧 | 部分 | 🟡 | 帧换算公式已有，缺集中 `UpdateFrame` API |

### 7.3 已对齐的设计决策

1. **数据驱动** — 运行时只读 SO，不在 `ActionState` 硬编码招式。
2. **CancelWindow.cancelType** — `Action` / `Movement` 分工与 ACTION_EDITOR §3.6 一致。
3. **Cancel vs Transition** — Cancel 需输入；Transition 自动衔接（含 AnimationEnd）。
4. **priority 解析** — 多窗口/多 Transition 按 priority 降序，与文档一致。
5. **Animator 仅 Locomotion** — 招式 `PlayClip`，与编辑器约定一致。
6. **输入缓冲** — 全程 Buffer、窗口内 Consume，与 §5.1 输入缓冲设计一致。
7. **数值与逻辑分离** — 伤害公式未进 `ActionDefinition`（符合 §2.5）。

### 7.4 有意的偏差与风险

| 偏差 | 原因 | 编辑器影响 | 建议 |
|------|------|------------|------|
| Cancel 无 `targetAction` | 线性连招队列简化配置 | 编辑器 Cancels 轨道不能只编辑「边到目标招」；需联动 `ActionComboSequence` 或 Graph | M5 Inspector 显示「下一招 = Sequence 进位」；M7 评估恢复 `targetActionId` 或 Graph 边 |
| `ActionComboSequence` 代替 `ActionGraph` | Demo 三连招够用 | 无法表达分支连招（挥空、多输入树） | 保留 Sequence 作「线性模板」；Graph 作高级层 |
| `AtFrame` Transition | 项目新增，支持中段自动切招 | ACTION_EDITOR 需补充枚举 | 更新 ACTION_EDITOR 变更日志 |
| `CombatModeProfile` | 多武器 ACT 需求 | 编辑器角色配置需增加 mode 维度 | 纳入 `CharacterCombatProfile` 设计或单列「模式」面板 |
| 无 `UpdateFrame` | 实现成本低 | **预览与运行时易不一致** | 编辑器开发前优先重构 `ActionRuntimeController.Tick` |

### 7.5 动作编辑器插件适配度评估

按 ACTION_EDITOR 分期评估当前代码对插件的承载能力：

| 插件阶段 | 目标 | 当前适配度 | 缺口 |
|----------|------|------------|------|
| **Phase A（数据层）** | Schema + Runtime 读 SO | **75%** | Phase/Hitbox/Event 类型未定义；`UpdateFrame` 缺失 |
| **Phase B（基础 Editor）** | 列表 + Inspector + 热重载 | **55%** | 无 `ActionEditorWindow`；ComboSequence 需独立编辑流；无校验器 |
| **Phase C（时间轴）** | Frameline 多轨道 + Scrub | **25%** | 无 Phase/Hitbox/Event 数据；无预览 Rig 共用 Tick |
| **Phase D（连招图）** | ActionGraph GraphView | **15%** | 仅线性 Sequence；与 Graph 模型不兼容 |
| **Phase E（运行时调试）** | Play Mode Overlay | **40%** | 有 `CurrentAction`/帧换算基础；无 Overlay / diff |

**优先补全项（编辑器开发前）：**

1. **`UpdateFrame(int frameIndex)`** — `ActionRuntimeController` 统一入口；编辑器 Scrub 与 Play Mode 共用。
2. **`ActionPhase` / `HitboxKeyframe` / `ActionEvent` 类型** — 先空跑通 Tick，再接入 Combat。
3. **CancelWindow `targetAction` 可选字段** — 与 ComboSequence **二选一**解析，便于编辑器直接填目标招。
4. **GM 热重载** — 编辑 SO 后刷新 Runtime 缓存，缩短策划迭代。
5. **数据校验 API** — 未闭合 Hitbox、Cancel 窗重叠、Sequence 断链等（Editor 与 CI 共用）。

### 7.6 推荐演进路径

```
当前 (Phase A 后期)
  │
  ├─[P0] UpdateFrame + ActionPhase/Hitbox/Event 类型骨架
  │
  ├─[P1] ActionEditorWindow 基础版（单招 Inspector + Cancel/Transition 列表）
  │       └─ 复用现有 ActionDefinition 字段
  │
  ├─[P2] Combo 编辑：Sequence 可视化 或 恢复 Cancel.targetAction
  │
  ├─[P3] Frameline 时间轴（Phase C）
  │
  └─[P4] ActionGraph 与 CombatMode 纳入角色战斗配置
```

**原则：** 编辑器只增 **序列化字段与校验**，不改 `ActionRuntimeController` 对外职责；新条件/事件用**子类或枚举扩展**（与 ACTION_EDITOR §2.3 一致）。

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

### ICombatModeController

```csharp
CombatModeType CurrentMode { get; }
bool TrySetMode(CombatModeType mode, CombatModeSwitchPolicy policy);
void ApplyPendingModeIfReady();
event Action<CombatModeType, CombatModeType> ModeChanged;
```

---

## 9. 已知限制

| 限制 | 说明 |
|------|------|
| 无战斗判定 | Hitbox / 伤害 / 受击未接入 |
| 连招仅线性 | 无分支、挥空、多输入树 |
| Transition 条件少 | 无 OnHitConfirm / OnWhiff |
| 无统一 Logic Tick | 编辑器预览parity 风险 |
| 敌人未接入 | 执行器可复用，输入源需替换 |

---

## 10. 相关文件

### 脚本

```
Assets/Scripts/Combat/
  Actions/ActionDefinition.cs, ActionRuntimeController.cs
  Actions/CancelWindow.cs, CancelType.cs
  Actions/ActionTransition.cs, ActionTransitionCondition.cs
  Actions/ActionComboSequence.cs, PlayerActionSet.cs
  Actions/ActionStartBehaviorType.cs, IActionStartContext.cs
  CombatModeController.cs

Assets/Scripts/Input/
  InputManager.cs, InputReader.cs, PlayerInputFrame.cs
  IActionComboInput.cs, InputIds.cs, IPlayerInputSource.cs

Assets/Scripts/Player/
  PlayerController.cs, PlayerStateMachine.cs

Assets/Scripts/Character/
  StateMachine/IActionRuntime.cs, States/ActionState.cs, States/LocomotionState.cs
  Animation/CharacterRootMotionDriver.cs, CharacterAnimationController.cs
```

### 资产（Editor 维护）

```
Assets/Data/Combat/Actions/Player/
  player_attack_*.asset, player_dodge_*.asset
  PlayerActionSet.asset, ActionComboSequence/*.asset
  CombatModeProfile.asset（若已建）
```

---

## 11. 变更日志

| 日期 | 变更 |
|------|------|
| 2026-06-17 | 初版与多轮迭代（InputManager、Root Motion、CancelWindow） |
| 2026-06-17 | **全面重写**：`ActionComboSequence`、`CombatModeProfile`、Transition `AtFrame`、对齐 ACTION_EDITOR 分析、编辑器适配度评估 |
