# Ability 重构 Part 1：ActionExecutor 表现层收敛

> 主计划：`ability_驱动动作系统重构_c3ce1ae4.plan.md`  
> 本文是三段式重构的第一部分，目标是先把 `ActionExecutor` 收敛为 AbilitySystem 的动作表现层。

## 目标

`ActionExecutor` 只负责播放已经确定的 `ActionDefinition`，并继续维护当前已落地的表现能力：

- 播放动画 Clip
- 推进 Action 逻辑时间与逻辑帧
- 派发 `CombatFrameContext`
- 驱动 Hitbox / VFX / ActionEvent
- 处理 Root Motion / 脚本位移
- 处理 HitStop 暂停
- 处理 `ActionTransition`

它不再负责：

- 输入到动作的解析
- 连段进位
- Dodge 方向分派
- Ability 条件判断
- 冷却、消耗、资源
- 跨模组派生，例如轻击接重击

## 目标目录

将 `Assets/Scripts/Domain/Combat/Actions` 按表现职责整理为：

```text
Assets/Scripts/Domain/Combat/Actions/
  Definitions/
    ActionDefinition.cs
    ActionPhase.cs
    ActionPhaseKind.cs
    ActionEvent.cs
    ActionEventKind.cs
    ActionEventContext.cs
    ActionTransition.cs
    ActionTransitionCondition.cs
    CancelWindow.cs
    CancelType.cs
    RotationWindow.cs
    CombatActionType.cs

  Execution/
    ActionExecutor.cs
    IActionExecutor.cs
    ActionSession.cs
    ActionRotationDriver.cs
    IActionStartContext.cs
    IActionHitReceiver.cs

  Frames/
    CombatFrameContext.cs
    ICombatFrameConsumer.cs
```

`CombatModeService.cs`、`ICombatModeService.cs`、`CombatModeSwitchResult.cs` 暂时保留在 `Combat/` 根层，因为它们描述角色当前战斗模式，不属于 Action 表现内部。

## 职责边界

### `ActionDefinition`

仍然是单个动作的时间轴资产定义：

- 动画 Clip
- 总帧数 / SampleRate
- Phase / Event
- CancelWindow
- Transition
- Hitbox / VFX
- HitStop / CameraShake
- RotationWindow / TargetLock
- RootMotion / ScriptedDisplacement

不要把 Ability 条件、输入规则、冷却、消耗写入 `ActionDefinition`。

### `ActionExecutor`

表现层执行器，只接收最终动作：

```text
ActionExecutor.TryStart(ActionDefinition action)
```

`ActionExecutor` 不应该再出现：

```text
TryStartByInput
TryGetStartAction
ActionComboSequence
DodgeDirectionVariants
ResolveStartAction
InputId
```

### `ActionSession`

保持当前动作播放状态：

- 当前 Action
- 已播放秒数
- 已处理逻辑帧
- 是否已命中
- HitStop 暂停状态
- 每招一次性触发标记

AbilitySystem 可以读取 `ActionExecutor.CurrentAction`、`CurrentFrame`、`ElapsedSeconds`，但不直接修改 `ActionSession`。

### `CancelWindow`

`CancelWindow` 继续是 `ActionDefinition` 的时间窗数据，但语义调整为：

```text
当前 Action 在某些帧允许哪些输入触发新的 Ability
```

它不再决定下一招是哪一个。下一招由 AbilitySystem 根据 Ability 配置、当前 Action、输入和条件决定。

## 需要删除的旧逻辑

### 删除输入起手入口

删除：

```text
ActionExecutor.TryStartByInput(string inputId)
IActionExecutor.TryStartByInput(string inputId)
```

上层如果要起手，应先由 AbilitySystem 激活能力，再通过表现层桥接调用：

```text
ActionAbilityPresentationDriver
  -> ActionExecutor.TryStart(action)
```

### 删除连段进位

`ActionExecutor` 不再调用：

```text
PlayerActionSet.TryResolveNext(...)
ActionComboSequence.TryResolveNext(...)
```

连段和派生归 AbilitySystem：

```text
LightAttackAbility
  currentAction == Light1 -> Light2
  currentAction == Light2 -> Light3
```

### 删除 Dodge 方向分派

`ActionExecutor` 不再判断：

```text
if action.ActionType == Dodge
```

Dodge 是一个 Ability，方向选择在 Ability 激活或表现请求生成阶段完成。

## 保留逻辑

以下逻辑不迁出：

- `BeginAction`
- `TransitionTo`
- `Stop`
- `Tick`
- `UpdateFrame`
- `AdvanceLogicFramesThrough`
- `DispatchCombatFrame`
- `DispatchActionEvents`
- `ApplyScriptedDisplacement`
- `NotifyHit`
- `SetHitStopPaused`
- `TryConsumeHitStopTrigger`

`ActionTransition` 是否继续由 `ActionExecutor` 执行可以保留。它描述单个动作内部的自动表现衔接，例如命中确认自动转入另一个 Action。复杂玩家输入派生不要放在这里。

## 与状态机协作

Ability 成功播放 Action 后，状态机进入 `CharacterStateType.Action`：

```text
AbilityService.Activate
  -> PresentationDriver.TryPlay
  -> ActionExecutor.TryStart(action)
  -> CharacterStateMachine.TryChangeState(Action)
```

`ActionState` 仍然 Tick `ActionExecutor`：

```text
ActionState.Tick
  -> ActionExecutor.Tick(deltaTime)
  -> ActionRotationDriver.Tick(...)
```

动作结束后回到 Locomotion 的逻辑保持现状。

## 输入桥接调整

`CharacterActionDriver` 不再是动作选择入口。第一阶段可将它收敛为：

- 注册离散输入
- 将输入写入 `InputManager`
- 管理输入缓冲
- 向 AbilitySystem 提供输入事件
- 处理 Movement Cancel 的桥接，或交给 AbilitySystem 统一处理

最终目标：

```text
InputManager
  -> AbilityService.ProcessInput
  -> AbilityService.TryActivateAbility
  -> PresentationDriver.TryPlay
  -> ActionExecutor.TryStart
```

## 实施步骤

1. 移动 `Actions` 文件到 `Definitions`、`Execution`、`Frames`。
2. 删除 `ActionExecutor.TryStartByInput` 与 `IActionExecutor.TryStartByInput`。
3. 删除 `ActionExecutor` 中 Dodge 方向分派私有方法。
4. 删除 `ActionExecutor` 对 `PlayerActionSet` / `ActionComboSequence` 的依赖。
5. 保留 `TryStart(ActionDefinition)` 作为唯一动作播放入口。
6. 调整 `CharacterActorFactory` 构造链，先让表现层可被外部桥接驱动。
7. 全局搜索旧符号，确认无残留引用。

## 验收清单

- [ ] `ActionExecutor` 不再读输入 id。
- [ ] `ActionExecutor` 不再知道 `PlayerActionSet`。
- [ ] `ActionExecutor` 不再知道 `ActionComboSequence`。
- [ ] `ActionExecutor` 不再内置 Dodge 分派。
- [ ] `IActionExecutor` 只暴露表现播放、状态读取、Tick、Frame 事件。
- [ ] Hitbox、VFX、HitStop、ActionEvent 表现链路不退化。
- [ ] Unity 编译无缺失符号。

## 风险

- 这一步会临时打断旧输入起手链路，必须与 Part 2 / Part 3 连续实施。
- 文件移动会产生较大 diff，需同步 Editor 工具和文档路径。
- 不保留旧兼容层，旧 `PlayerActionSet` / `ActionComboSequence` 资产不可继续作为动作入口。
