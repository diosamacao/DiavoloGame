# 动作系统重构方案

## 1. 背景

当前动作系统的核心链路为：

```text
PlayerController
  -> CharacterActor
  -> CharacterActionDriver
  -> ActionExecutor
  -> CombatModeService
  -> CombatModeProfile
  -> PlayerActionSet
  -> ActionComboSequence
  -> ActionDefinition
```

现有结构已经具备较好的运行时分层：

- `PlayerController` 负责 Unity 场景入口与角色装配。
- `CharacterActor` 负责每帧输入采集、动作路由、重力和状态机推进。
- `CharacterActionDriver` 负责离散输入起手、缓冲和移动取消。
- `ActionExecutor` 负责动作播放、逻辑帧推进、CancelWindow、Transition、Hitbox、VFX 和 HitStop。
- `CombatModeService` 负责当前战斗模式和出招表切换。
- `ActionDefinition` 负责描述单个动作的动画、帧事件、碰撞、特效、顿帧、位移和旋转窗口。

主要问题集中在动作选择层：`PlayerActionSet` 当前强制把所有输入映射到 `ActionComboSequence`。攻击连段适合这种结构，但防御、闪避、切模式、蓄力、交互等动作并不天然是线性连招。

因此，重构目标不是推翻现有动作执行器，而是把“动作选择”和“动作执行”拆开。

## 2. 重构目标

### 2.1 设计目标

- 降低配置冗余，防御等单动作不再需要创建只有一个元素的 `ActionComboSequence`。
- 支持方向动作，例如四向、八向闪避，或锁定目标相对方向技能。
- 支持更丰富的输入类型，例如 Pressed、Held、Released。
- 保持 `ActionDefinition` 作为单个动作时间轴数据的核心资产。
- 保持 `ActionExecutor` 专注执行，不继续塞入 Dodge、Guard、Charge 等特判。
- 为后续动作编辑器预留清晰的数据边界。

### 2.2 核心边界

```text
ActionDefinition = 一个具体动作如何播放
ActionResolver   = 一个输入在当前上下文下选择哪个动作
ActionExecutor   = 执行已经选好的动作
```

## 3. 推荐最终结构

```text
CharacterActor
  -> 采集输入，推进角色状态机

CharacterActionDriver
  -> 将输入转换为 ActionRequest
  -> 管理输入缓冲
  -> 请求 ActionResolverService 解析动作
  -> 成功后驱动状态机进入 Action

ActionResolverService
  -> 查询当前 CombatMode
  -> 查询当前 PlayerActionSet / ActionMap
  -> 根据 ActionEntry 调用对应 Resolver

CombatModeProfile
  -> CombatModeEntry[]
      -> CombatModeType
      -> PlayerActionSet / ActionMap
      -> LocomotionProfile

PlayerActionSet / ActionMap
  -> ActionEntry[]

ActionEntry
  -> InputActionReference
  -> TriggerType
  -> Priority
  -> Conditions
  -> ActionResolver

ActionResolver
  -> SingleActionResolver
  -> ComboActionResolver
  -> DirectionalActionResolver
  -> HoldActionResolver
  -> ConditionalActionResolver
  -> BranchActionResolver

ActionExecutor
  -> 播放最终解析出的 ActionDefinition
```

## 4. 运行时驱动流程

### 4.1 Locomotion 起手流程

```text
InputReader.CaptureFrame()
  -> PlayerInputFrame
  -> InputManager.IngestFrame()
  -> CharacterActionDriver.ProcessGameplayInput()
  -> 构造 ActionRequest
  -> ActionResolverService.TryResolve(request, context, out action)
  -> ActionExecutor.TryStart(action)
  -> CharacterStateMachine.TryChangeState(Action)
```

示例：

```text
玩家按下 Attack
  -> ActionRequest(InputId = "Attack", Trigger = Pressed)
  -> PlayerActionSet 找到 Attack Entry
  -> ComboActionResolver 返回 Attack_01
  -> ActionExecutor 播放 Attack_01
```

```text
玩家按下 Dodge + 左方向
  -> ActionRequest(InputId = "Dodge", Trigger = Pressed, MoveIntent = Left)
  -> PlayerActionSet 找到 Dodge Entry
  -> DirectionalActionResolver 返回 Dodge_Left
  -> ActionExecutor 播放 Dodge_Left
```

```text
玩家按下 Guard
  -> ActionRequest(InputId = "Guard", Trigger = Pressed)
  -> PlayerActionSet 找到 Guard Entry
  -> HoldActionResolver 返回 Guard_Start
  -> ActionExecutor 播放 Guard_Start
```

### 4.2 招式中 Cancel 流程

当前系统由 `ActionExecutor` 扫描 `CancelWindow`，再通过 `PlayerActionSet.TryResolveNext()` 找下一段连招。重构后建议调整为：

```text
ActionExecutor.Tick()
  -> 检查当前 ActionDefinition.CancelWindow
  -> 若窗口允许某个 inputId
  -> 请求 CharacterActionDriver / ActionResolverService 解析缓冲输入
  -> 得到 next ActionDefinition
  -> ActionExecutor.TransitionTo(nextAction)
```

示例：

```text
Attack_01 的 CancelWindow 允许 Attack
  -> ComboActionResolver 根据 CurrentAction = Attack_01
  -> 返回 Attack_02
  -> ActionExecutor 切到 Attack_02
```

```text
Attack_01 的 CancelWindow 允许 Dodge
  -> DirectionalActionResolver 根据当前或缓冲移动方向
  -> 返回 Dodge_Back / Dodge_Left / Dodge_Right
  -> ActionExecutor 切到对应闪避
```

```text
Attack_01 的 CancelWindow 允许 Guard
  -> HoldActionResolver 根据 Trigger = Pressed
  -> 返回 Guard_Start
  -> ActionExecutor 切到 Guard_Start
```

### 4.3 动作执行流程

`ActionExecutor` 继续保留现有职责：

```text
ActionExecutor.TryStart(action)
  -> BeginAction
  -> 播放 AnimationClip
  -> 开启 Root Motion 或脚本位移
  -> 派发 ActionBegan
  -> DispatchCombatFrame(0)

ActionExecutor.Tick(deltaTime)
  -> Advance elapsed time
  -> ApplyScriptedDisplacement
  -> SyncLogicFrameFromElapsed
  -> DispatchCombatFrame
  -> DispatchActionEvents
  -> Resolve CancelWindow
  -> Resolve Transition
  -> 动作结束则 Stop
```

重点是：`ActionExecutor` 不再判断“Dodge 应该选左闪还是后闪”，也不再关心“防御是不是 ComboSequence 的第一段”。它只执行已经解析出的 `ActionDefinition`。

## 5. 核心数据结构建议

### 5.1 ActionRequest

`ActionRequest` 表示一次动作请求，可以由玩家输入、AI、回放或网络同步产生。

```csharp
public readonly struct ActionRequest
{
    public readonly string InputId;
    public readonly ActionInputTrigger Trigger;
    public readonly Vector2 MoveIntent;
    public readonly Vector2 BufferedMoveIntent;
    public readonly bool HasMoveIntent;
    public readonly bool HasBufferedMoveIntent;
}
```

建议的触发类型：

```csharp
public enum ActionInputTrigger
{
    Pressed,
    Held,
    Released,
}
```

### 5.2 ActionResolveContext

`ActionResolveContext` 表示解析动作时需要的运行时上下文。

```csharp
public readonly struct ActionResolveContext
{
    public readonly CombatModeType CurrentMode;
    public readonly CharacterStateType CurrentState;
    public readonly ActionDefinition CurrentAction;
    public readonly float CurrentActionElapsedSeconds;
    public readonly Transform ActorRoot;
    public readonly IMoveIntentResolver MoveIntentResolver;
    public readonly CombatTargetLock TargetLock;
}
```

该结构应避免直接绑定过多 Unity 组件。对于可替代的能力，优先通过接口传入。

### 5.3 ActionEntry

`ActionEntry` 不再直接绑定 `ActionComboSequence`，而是绑定 `ActionResolver`。

```csharp
[Serializable]
public struct ActionEntry
{
    [SerializeField] InputActionReference input;
    [SerializeField] ActionInputTrigger trigger;
    [SerializeField] int priority;
    [SerializeField] ActionResolver resolver;

    public string InputId => InputBindingUtils.GetInputId(input);
    public ActionInputTrigger Trigger => trigger;
    public int Priority => priority;
    public ActionResolver Resolver => resolver;
}
```

### 5.4 ActionResolver

所有动作选择规则都通过 Resolver 表达。

```csharp
public abstract class ActionResolver : ScriptableObject
{
    public abstract bool TryResolve(
        in ActionRequest request,
        in ActionResolveContext context,
        out ActionDefinition action);
}
```

## 6. Resolver 类型设计

### 6.1 SingleActionResolver

适用于单个动作：

- 防御开始
- 切模式
- 交互
- 普通技能
- 受击反应

```text
Input: Guard Pressed
Resolver: SingleActionResolver
Action: Guard_Start
```

### 6.2 ComboActionResolver

适用于线性连段：

```text
Input: Attack Pressed
Resolver: ComboActionResolver
Steps:
  Attack_01
  Attack_02
  Attack_03
LeafPolicy:
  StopCombo / LoopToRoot
```

解析规则：

- 当前没有动作时，返回第一段。
- 当前动作在 Steps 中时，返回下一段。
- 当前动作不在 Steps 中时，返回第一段。
- 末段根据 `LeafPolicy` 决定循环或停止。

### 6.3 DirectionalActionResolver

适用于方向动作：

- 四向闪避
- 八向闪避
- 方向攻击
- 锁定目标相对方向技能

```text
Input: Dodge Pressed
DirectionSource:
  CurrentMoveInput / BufferedMoveInput / ActorForward / TargetRelative
Default:
  Dodge_Back
Variants:
  Forward      -> Dodge_Forward
  Backward     -> Dodge_Back
  Left         -> Dodge_Left
  Right        -> Dodge_Right
  ForwardLeft  -> Dodge_ForwardLeft
  ForwardRight -> Dodge_ForwardRight
  BackLeft     -> Dodge_BackLeft
  BackRight    -> Dodge_BackRight
```

方向解析逻辑建议放在 Resolver 中，而不是放在 `ActionExecutor` 中。

### 6.4 HoldActionResolver

适用于按住类动作：

- 防御
- 蓄力
- 举枪瞄准
- 引导技能

```text
Guard Pressed  -> Guard_Start
Guard Held     -> Guard_Loop
Guard Released -> Guard_End
```

这要求输入层支持 Held 和 Released。

### 6.5 ConditionalActionResolver

适用于根据上下文选择动作：

```text
同一个 Skill 输入：
  Airborne    -> AirSkill
  Grounded    -> GroundSkill
  LockOn      -> LockOnSkill
  NoTarget    -> FreeSkill
```

条件建议可组合，但不要过早做复杂表达式系统。初期可以先支持常用条件枚举。

### 6.6 BranchActionResolver

适用于复杂派生：

```text
Attack_01
  Attack Input -> Attack_02
  Heavy Input  -> Heavy_02
  Dodge Input  -> DodgeResolver
  Guard Input  -> Guard_Start
```

该 Resolver 更适合中后期配合节点式动作编辑器。

## 7. 对输入系统的调整

当前 `InputManager` 只记录：

- `MoveIntent`
- `LookIntent`
- `PressedInputIds`
- `HashSet<string>` 输入缓冲

建议扩展为：

```text
PlayerInputFrame
  Move
  Look
  PressedInputIds
  HeldInputIds
  ReleasedInputIds
```

输入缓冲建议从 `HashSet<string>` 升级为带时间的列表或队列：

```csharp
public readonly struct BufferedActionInput
{
    public readonly string InputId;
    public readonly ActionInputTrigger Trigger;
    public readonly float Time;
}
```

这样后续可以支持：

- 输入过期时间
- 同一个输入重复缓冲
- 最后输入优先
- 最早输入优先
- 不同输入不同缓冲窗口

## 8. 对动作编辑器的影响

该重构方向会提高短期实现量，但会显著降低中长期编辑器复杂度。

### 8.1 ActionDefinition 编辑器

继续负责单个动作的时间轴数据：

- 动画片段
- 总帧数 / SampleRate
- Startup / Active / Recovery
- Hitbox
- VFX
- ActionEvent
- CancelWindow
- Transition
- HitStop
- 位移窗口
- 旋转窗口
- 索敌窗口

这部分基本不需要因为 Resolver 重构而推翻。

### 8.2 ActionMap 编辑器

新增出招表编辑器，负责编辑输入到动作规则的映射：

```text
[Input] [Trigger] [Priority] [Resolver Type] [Conditions]
Attack  Pressed   100        Combo
Dodge   Pressed   200        Directional
Guard   Pressed   150        Hold
Guard   Released  150        Hold
```

该编辑器面向“玩家当前战斗模式下能做什么动作”。

### 8.3 Resolver 编辑器

不同 Resolver 使用不同 Inspector：

```text
ComboActionResolver
  Steps
  LeafPolicy
  ResetTime
```

```text
DirectionalActionResolver
  DirectionSource
  AngleMode: FourWay / EightWay
  DefaultAction
  ForwardAction
  BackwardAction
  LeftAction
  RightAction
  ForwardLeftAction
  ForwardRightAction
  BackLeftAction
  BackRightAction
```

```text
HoldActionResolver
  PressedAction
  HeldAction
  ReleasedAction
```

### 8.4 后续节点式编辑器

如果未来做节点式动作编辑器，可以把 Resolver 映射为图节点：

```text
Input Node
  -> Condition Node
  -> Direction Node
  -> ActionDefinition Node
```

或：

```text
Attack Input
  -> Combo Node
      -> Attack_01
      -> Attack_02
      -> Attack_03
```

由于 `ActionDefinition`、`ActionResolver`、`ActionMap` 的职责已经分离，节点编辑器可以只操作动作选择层，而不需要侵入 `ActionExecutor`。

## 9. 迁移方案

### 阶段一：兼容现有结构

目标：不破坏当前攻击连段。

- 新增 `ActionResolver` 抽象。
- 新增 `ComboActionResolver`，内部逻辑复用当前 `ActionComboSequence`。
- 修改 `ActionEntry`，允许配置 `resolver`。
- 保留旧 `comboSequence` 字段作为迁移期兼容。
- `PlayerActionSet.TryGetStartAction()` 内部逐步改为 `TryResolve()`。

### 阶段二：迁移闪避

目标：移除 `ActionExecutor.ResolveStartAction()` 中的 Dodge 特判。

- 新增 `DirectionalActionResolver`。
- 将 `DodgeDirectionVariants` 的配置迁移到 `DirectionalActionResolver`。
- `ActionExecutor` 不再关心 `CombatActionType.Dodge` 的方向选择。
- `PlayerActionSet` 中 Dodge Entry 改为绑定 `DirectionalActionResolver`。

### 阶段三：迁移防御

目标：防御不再依赖单元素 ComboSequence。

- 新增 `SingleActionResolver` 或 `HoldActionResolver`。
- 扩展输入系统支持 `Held` 和 `Released`。
- 防御 Entry 改为：

```text
Guard Pressed  -> Guard_Start
Guard Held     -> Guard_Loop
Guard Released -> Guard_End
```

### 阶段四：整理 ActionExecutor

目标：让执行器只执行动作。

- 移除 `ActionExecutor.TryStartByInput()` 或将其降级为兼容 API。
- 移除执行器内的方向闪避解析。
- CancelWindow 中的下一动作解析改为请求 `ActionResolverService`。
- 保留 `TryStart(ActionDefinition action)` 作为唯一核心播放入口。

### 阶段五：编辑器支持

目标：让配置体验优于当前结构。

- 为 `ActionDefinition` 保留时间轴编辑器。
- 为 `PlayerActionSet / ActionMap` 增加列表式编辑器。
- 为常用 Resolver 增加 Inspector。
- 后续再考虑节点式动作图。

## 10. 风险与注意事项

### 10.1 不要过早做复杂条件系统

建议先实现：

- Single
- Combo
- Directional
- Hold

这四类已经能解决当前防御和闪避问题。`Conditional` 与 `Branch` 可以在技能复杂度上来后再做。

### 10.2 保持 ActionDefinition 纯粹

不要把方向选择、输入规则、连招顺序继续塞进 `ActionDefinition`。它应该只描述一个动作如何播放。

### 10.3 保持 ActionExecutor 稳定

`ActionExecutor` 是运行时核心，不适合频繁添加动作类型特判。新的动作类型应优先通过 Resolver 扩展。

### 10.4 输入缓冲需要尽早升级

如果要做防御松开、蓄力、复杂派生，`HashSet<string>` 缓冲会很快不够用。建议在 Resolver 重构早期同步升级输入帧结构。

## 11. 推荐落地优先级

```text
P0:
  - 新增 ActionResolver 抽象
  - 新增 ComboActionResolver
  - ActionEntry 支持 resolver

P1:
  - 新增 DirectionalActionResolver
  - 迁移 DodgeDirectionVariants
  - 移除 ActionExecutor 中 Dodge 特判

P2:
  - 输入系统支持 Pressed / Held / Released
  - 新增 HoldActionResolver
  - 迁移 Guard

P3:
  - 改造 CancelWindow 的下一动作解析路径
  - ActionExecutor 只接收 ActionDefinition

P4:
  - ActionMap 编辑器
  - Resolver Inspector
  - 后续节点式动作编辑器
```

## 12. 最终收益

重构完成后，动作系统会从：

```text
所有输入都必须映射到 ComboSequence
```

变为：

```text
每个输入根据自己的动作类型选择合适的 Resolver
```

配置体验会更符合动作游戏实际需求：

- 攻击是 Combo。
- 闪避是 Directional。
- 防御是 Hold。
- 切模式是 Single。
- 技能可以是 Conditional 或 Branch。

同时，`ActionDefinition` 的时间轴编辑器可以继续专注于单个动作，`ActionMap` 和 `Resolver` 编辑器负责动作选择规则，后续扩展为完整动作编辑器的成本会更可控。
