# 重构记录 — 动作系统 ActionEditor 准备（2026-06-21）

> **目标**：为 ActionEditor 开发铺路，统一 Logic Tick、收敛玩家侧职责、补齐 Phase/Event Schema 与命中回流。  
> **范围**：`Assets/Scripts` 动作/战斗运行时；不含 Prefab/Asset 自动修改。  
> **关联文档**：[ACTION_SYSTEM.md](./ACTION_SYSTEM.md) · [ACTION_EDITOR.md](./ACTION_EDITOR.md) · [ROADMAP.md](../.cursor/skills/actgame-architecture/ROADMAP.md)

---

## 1. 背景与动机

### 1.1 重构前主要问题

| 问题 | 影响 |
|------|------|
| `PlayerController` 承担输入路由、起手、移动取消、索敌旋转等 ~360 行职责 | 扩展招式逻辑需改 God Component；敌人复用困难 |
| Hitbox/VFX 各自 `LateUpdate` 拉取帧 | 隐式帧序约定；编辑器 Scrub 与 Play Mode 无法共用路径 |
| 无 `UpdateFrame` API | ActionEditor 时间轴预览阻塞项 |
| 命中不回流 `ActionRuntime` | 无法实现 `OnHitConfirm` / `OnWhiff` Transition |
| `ActionDefinition` 缺 Phase/Event Schema | 编辑器 Phases/Events 轨道无数据源 |
| `IActionRuntime` 位于 `Character/` | Combat 类型渗入 Character 层边界模糊 |
| 架构文档仍写 Combat 为占位 | 与代码脱节 |

### 1.2 确认的重构约束（用户决策）

1. **首要目标**：ActionEditor 准备（非分支连招图）
2. **接受** Prefab 增组件（Editor 人工操作）
3. **保留** 线性 `ActionComboSequence`（近期不建 ActionGraph）
4. **命名** 偏向 `CharacterActionDriver`（角色无关，敌人可复用）
5. **同步** 更新架构/技术文档

---

## 2. 目标架构（重构后）

### 2.1 职责分层

```
InputReader
    ↓ CaptureFrame
PlayerController          ← Motor + InputManager + IMoveIntentResolver
    ↓ ProcessGameplayInput
CharacterActionDriver     ← 离散输入起手/缓冲、移动取消、离开 Action 预输入
ActionRotationDriver      ← RotationWindow + CombatTargetLock
PlayerStateMachine
    └─ ActionState.Tick
           └─ ActionRuntimeController
                  ├─ Tick / UpdateFrame（Logic Tick 统一入口）
                  ├─ CancelWindow / Transition（含 OnHitConfirm / OnWhiff）
                  └─ DispatchCombatFrame → ICombatFrameConsumer
                         ├─ HitBoxSystem（OBB + IActionHitReceiver 回流）
                         └─ ActionVfxPlayer
```

### 2.2 设计原则（本次贯彻）

1. **Logic Tick = 编辑器帧** — Play Mode 与 ActionEditor Scrub 共用 `UpdateFrame(frameIndex)`
2. **执行 / 判定 / 受击 三分** — `ActionRuntimeController` 不引用 `HitBoxSystem`
3. **PlayerController 只管 Motor** — 招式输入与旋转迁出
4. **数据驱动扩展** — Phase/Event 先建 Schema，运行时派发留 M5

---

## 3. 变更清单

### 3.1 新增文件

| 路径 | 职责 |
|------|------|
| `Combat/Actions/CharacterActionDriver.cs` | 角色招式输入路由（起手、缓冲、移动取消） |
| `Combat/Actions/ActionRotationDriver.cs` | 招式 RotationWindow + 索敌转向 |
| `Combat/Actions/CombatFrameContext.cs` | Logic Tick 帧上下文（readonly struct） |
| `Combat/Actions/ICombatFrameConsumer.cs` | Hitbox/VFX 等帧订阅接口 |
| `Combat/Actions/IActionHitReceiver.cs` | 命中回流接口 |
| `Combat/Actions/IActionRuntime.cs` | 从 `Character/StateMachine/` 迁入 |
| `Combat/Actions/IMoveIntentResolver.cs` | 移动意图 → 世界方向 |
| `Combat/Actions/ActionPhaseKind.cs` | 阶段枚举（Startup/Active/Recovery + 覆盖标记） |
| `Combat/Actions/ActionPhase.cs` | 阶段帧区间 Serializable |
| `Combat/Actions/ActionEventKind.cs` | 事件类型枚举骨架 |
| `Combat/Actions/ActionEvent.cs` | 事件帧数据骨架 |

### 3.2 删除 / 迁移

| 变更 | 说明 |
|------|------|
| 删除 `Character/StateMachine/IActionRuntime.cs` | 迁至 `Combat/Actions/IActionRuntime.cs` |
| `PlayerController` 内 `PlayerComboInput` 嵌套类 | 迁至 `CharacterActionDriver.ComboInputBridge` |

### 3.3 主要修改文件

| 文件 | 变更摘要 |
|------|----------|
| `PlayerController.cs` | 358 行 → ~156 行；仅 Motor、重力、InputManager、`IActionStartContext`、`IMoveIntentResolver` |
| `PlayerStateMachine.cs` | 增加 `RequireComponent`：`CharacterActionDriver`、`ActionRotationDriver` |
| `ActionRuntimeController.cs` | `UpdateFrame`、`IActionHitReceiver`、`FrameAdvanced` 事件、`ICombatFrameConsumer` 自动发现；逐帧补发 |
| `ActionDefinition.cs` | 新增 `phases[]`、`actionEvents[]`；`IsTransitionEligible` 支持命中；SerializeField 显式默认值 |
| `ActionTransitionCondition.cs` | 新增 `OnHitConfirm = 2`、`OnWhiff = 3` |
| `ActionTransition.cs` | SerializeField 显式默认值（消除 CS0649） |
| `HitBoxSystem.cs` | 实现 `ICombatFrameConsumer`；命中后调用 `IActionHitReceiver.NotifyHit`；移除 `LateUpdate` |
| `ActionVfxPlayer.cs` | 实现 `ICombatFrameConsumer`；移除 `LateUpdate` |
| `CombatTargetLock.cs` | 注释更新：由 `ActionRotationDriver` 驱动 |

### 3.4 文档同步

| 文件 | 更新内容 |
|------|----------|
| `.cursor/skills/actgame-architecture/ARCHITECTURE.md` | Combat 模块、Logic Tick 数据流、扩展点 |
| `.cursor/skills/actgame-architecture/ROADMAP.md` | 标记 ActionEditor 准备项完成；下一步 |
| `.cursor/skills/actgame-architecture/TECHNICAL.md` | §7 动作系统重写；Editor 操作步骤 |
| `docs/ACTION_SYSTEM.md` | UpdateFrame / Phase/Event / 命中回流状态 |

---

## 4. 核心 API（ActionEditor 对接）

### 4.1 Logic Tick

```csharp
// Play Mode：由 ActionState.Tick → ActionRuntimeController.Tick 内部推进
actionRuntime.Tick(deltaTime);

// Editor Scrub：与 Play Mode 共用帧派发路径（要求招式已在播放）
actionRuntime.UpdateFrame(frameIndex);

// 自定义预览轨道订阅
actionRuntime.FrameAdvanced += ctx =>
{
    int frame = ctx.FrameIndex;
    ActionDefinition action = ctx.Action;
    // Gizmo / 采样 / 调试
};
```

### 4.2 帧消费者

```csharp
public interface ICombatFrameConsumer
{
    void OnActionBegan(ActionDefinition action);
    void OnCombatFrameAdvanced(in CombatFrameContext context);
    void OnActionEnded();
}
```

同 GameObject 上实现该接口的组件会在 `ActionRuntimeController.Awake` 时自动注册。

### 4.3 命中回流

```csharp
public interface IActionHitReceiver
{
    bool HasConfirmedHitThisAction { get; }
    void NotifyHit(in ActionHitContext context);
}
```

`ActionRuntimeController` 已实现。`HitBoxSystem` 命中后调用 `NotifyHit`，支撑：

- `ActionTransitionCondition.OnHitConfirm` — 命中后立即收招/衔接
- `ActionTransitionCondition.OnWhiff` — 动画结束且未命中时衔接

### 4.4 ActionDefinition 新增 Schema（Editor 数据源）

| 字段 | 类型 | 运行时 |
|------|------|--------|
| `phases[]` | `ActionPhase[]` | 只读查询 `GetActivePhasesAtFrame` / `IsInterruptibleAtFrame`；**未派发** |
| `actionEvents[]` | `ActionEvent[]` | **未派发**；VFX 仍走 `vfxEvents[]` |

---

## 5. Prefab / Editor 人工操作

> Agent 不得修改 Prefab；以下需在 Unity Editor 完成。

### 5.1 `Player_KatanaGirl.prefab`

在根节点 **Add Component**（若尚未挂载）：

1. `CharacterActionDriver`
2. `ActionRotationDriver`

`InputReader`、`CombatTargetLock` 可留空，脚本 Awake 会 `GetComponent` 回退。

### 5.2 验证清单（Play Mode）

- [ ] Locomotion 移动、跑走切换正常
- [ ] 攻击起手 → 进入 Action 状态
- [ ] Cancel 连段（同输入队列进位）
- [ ] 移动取消（Recovery 窗口 + 移动意图）
- [ ] RotationWindow 内索敌 / 输入转向
- [ ] Hitbox 命中、震屏、卡肉
- [ ] VFX 帧事件触发
- [ ] 离开 Action 后预输入缓冲消费
- [ ] （可选）配置 `OnHitConfirm` Transition 验证命中收招

---

## 6. 行为变更说明

| 项 | 重构前 | 重构后 |
|----|--------|--------|
| 帧采样时机 | `HitBoxSystem` / `ActionVfxPlayer` 在 `LateUpdate` | `ActionState.Tick` 内 `DispatchCombatFrame` 同步派发 |
| 大 delta 跳帧 | 可能漏中间帧 Hitbox | `AdvanceLogicFramesThrough` 逐帧补发 |
| 起手/缓冲逻辑 | `PlayerController` | `CharacterActionDriver` |
| 招式旋转 | `PlayerController.ExecuteMovement` 分支 | `ActionRotationDriver.Update` |
| 命中 → Runtime | 无 | `NotifyHit` → `HasConfirmedHitThisAction` |
| Transition 条件 | `AnimationEnd`、`AtFrame` | + `OnHitConfirm`、`OnWhiff` |

**预期**：玩法行为与重构前一致；架构与 Editor 扩展性提升。

---

## 7. 已知限制与后续工作

### 7.1 本次未做

- `ActionEditorWindow` UI
- Phase / ActionEvent **运行时派发**（仅 Schema）
- `ActionDefinition` 子 SO 拆分
- 伤害结算、`Hit` 状态、受击 `ActionDefinition` 衔接
- `HurtboxTargetRegistry` 场景级化
- Locomotion 移动职责迁移（ROADMAP 独立项）

### 7.2 ActionEditor 推荐下一步（ROADMAP P1）

1. `ActionEditorWindow` 基础版：列表 + 帧 Scrub 调 `UpdateFrame`
2. Phase/Event 运行时派发接入 `ICombatFrameConsumer` 或独立 Dispatcher
3. Editor 预览 Rig 与 Play Mode 帧 parity 自动化测试

### 7.3 IDE / 编译备注

- 若 Cursor IDE 报 `CharacterActionDriver` 找不到，但 Unity/`dotnet build Assembly-CSharp.csproj` 通过 → 为 IDE 索引滞后；**以 Unity 编译为准**
- 修改脚本后若 csproj 引用缺失文件，在 Unity 中 **Regenerate project files** 或重新聚焦项目

---

## 8. 文件索引（快速定位）

```
Assets/Scripts/
├── Player/
│   ├── PlayerController.cs          # Motor + Input
│   └── PlayerStateMachine.cs
├── Combat/
│   ├── Actions/
│   │   ├── ActionRuntimeController.cs
│   │   ├── CharacterActionDriver.cs
│   │   ├── ActionRotationDriver.cs
│   │   ├── ActionDefinition.cs
│   │   ├── IActionRuntime.cs
│   │   ├── CombatFrameContext.cs
│   │   ├── ICombatFrameConsumer.cs
│   │   ├── IActionHitReceiver.cs
│   │   ├── ActionPhase.cs / ActionEvent.cs
│   │   └── ActionTransitionCondition.cs
│   ├── Hitbox/HitBoxSystem.cs
│   └── VFX/ActionVfxPlayer.cs
└── Character/StateMachine/States/ActionState.cs
```

---

## 9. 变更日志

| 日期 | 作者 | 说明 |
|------|------|------|
| 2026-06-21 | Agent + 用户确认 | ActionEditor 准备重构：Logic Tick、职责拆分、Phase/Event 骨架、命中回流、文档同步 |
| 2026-06-21 | 单向依赖收敛 | PlayerController→PlayerStateMachine（PushMotorSnapshot）；ActionRuntimeController→ICombatModeController（CombatModeSwitchResult） |

---

## 10. 单向依赖收敛（2026-06-21 补充）

### 10.1 PlayerController → PlayerStateMachine

- 移除 `PlayerStateMachine` 对 `PlayerController` 的 `[RequireComponent]` 与字段引用。
- 新增 `PlayerStateMachine.PushMotorSnapshot(...)`；`PlayerController.Update` 末尾 Push（`DefaultExecutionOrder(-50)` 保证先于 StateMachine Tick）。
- `UpdateContext()` 改为空实现。

### 10.2 ActionRuntimeController → ICombatModeController

- 移除 `CombatModeController` 对 `ActionRuntimeController` 的引用与 `Update` 轮询。
- 新增 `CombatModeSwitchResult`；`TrySetMode(mode, policy, isActionPlaying)` 由调用方传入是否在招式中。
- `StopCurrentAction`：返回 `RequiresStopCurrentAction`，由 `ActionRuntimeController.TrySwitchCombatMode` 先 `Stop()` 再重试。
- `OnNextLocomotion` 挂起模式由 `CharacterActionDriver` 在回到 Locomotion 后调用 `ApplyPendingModeIfReady()`（不再检查 `IsPlaying`）。
