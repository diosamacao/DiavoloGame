# Ability 重构 Part 2：AbilitySystem 核心

> 主计划：`ability_驱动动作系统重构_c3ce1ae4.plan.md`  
> 本文是三段式重构的第二部分，目标是新增 AbilitySystem，替代旧 `PlayerActionSet` / `ActionComboSequence` / Resolver 作为动作选择与激活层。

## 目标

AbilitySystem 负责回答一个问题：

```text
这次输入在当前上下文下，是否可以激活某个 Ability；如果可以，应该发出什么表现请求？
```

它承担：

- 输入到 Ability 的映射
- Ability 优先级
- Ability 条件判断
- 当前动作派生规则
- CancelWindow 激活限制
- 冷却接口
- 消耗接口
- Ability 运行时状态
- 表现请求生成

它不承担：

- 直接播放 Animator
- Hitbox 判定
- VFX 生成
- HitStop 控制
- Camera / Audio / UI 的具体执行

这些都通过表现层接口接入。

## 命名约定

项目当前约定 `System` 后缀保留给 `App/Systems` 架构系统。代码落地建议：

```text
AbilitySystem   = 模块/概念名
AbilityService  = 运行时纯 C# 服务类
```

除非同步更新 `CONVENTIONS.md` 允许例外，否则不要新增 `AbilitySystem.cs` 作为 Domain 类名。

## 目标目录

```text
Assets/Scripts/Domain/Combat/Abilities/
  Definitions/
    AbilityDefinition.cs
    AbilitySet.cs
    AbilityEntry.cs
    AbilityInputTrigger.cs
    AbilityActivationPolicy.cs
    AbilityBranch.cs

  Runtime/
    AbilityService.cs
    AbilityActivationRequest.cs
    AbilityActivationContext.cs
    AbilityActivationResult.cs
    AbilityRuntimeState.cs
    AbilityStateStore.cs

  Conditions/
    AbilityCondition.cs
    AbilityConditionResult.cs
    CurrentActionCondition.cs
    CurrentStateCondition.cs
    CombatModeCondition.cs
    CancelWindowCondition.cs

  Costs/
    AbilityCost.cs
    AbilityCostResult.cs

  Cooldowns/
    AbilityCooldown.cs
    AbilityCooldownState.cs
```

表现层文件放在 Part 3 的 `Abilities/Presentation/`。

## 核心数据模型

### `AbilitySet`

替代旧 `PlayerActionSet`，表示当前战斗模式下可用的能力入口。

```text
AbilitySet
  entries[]
```

每个 `CombatModeEntry` 后续应从：

```text
mode -> PlayerActionSet
```

迁移为：

```text
mode -> AbilitySet
```

不保留旧 `PlayerActionSet` 兼容入口。

### `AbilityEntry`

输入到 Ability 的路由项：

```text
AbilityEntry
  input: InputActionReference
  trigger: AbilityInputTrigger
  priority: int
  ability: AbilityDefinition
```

`InputId` 仍使用 Input System Action 名，延续当前输入约定。

初版触发类型：

```text
Pressed
```

可预留但不立即实现：

```text
Held
Released
```

### `AbilityDefinition`

能力配置资产，描述能否激活和激活后请求什么表现。

```text
AbilityDefinition
  id
  displayName
  activationPolicy
  conditions[]
  costs[]
  cooldown
  branches[]
  defaultPresentation
```

建议字段语义：

- `id`：稳定逻辑 id。
- `displayName`：编辑器显示名。
- `activationPolicy`：从 Locomotion 起手、Action 中派生、两者都允许等。
- `conditions`：通用激活条件。
- `costs`：消耗接口，初版可空。
- `cooldown`：冷却接口，初版可空。
- `branches`：根据当前 Action / 状态选择不同表现。
- `defaultPresentation`：没有命中分支时的默认表现。

### `AbilityBranch`

用于轻重派生和上下文表现选择。

```text
AbilityBranch
  conditions[]
  presentation
  priority
```

示例：

```text
HeavyAttackAbility
  branches:
    priority 300:
      CurrentAction == Light1
      presentation -> Heavy2

    priority 200:
      CurrentAction == Light2
      presentation -> Heavy3

  defaultPresentation -> Heavy1
```

这样可以支持：

```text
轻击1 + 重击输入 -> 重击2
轻击2 + 重击输入 -> 重击3
无上下文 + 重击输入 -> 重击1
```

### `AbilityActivationRequest`

一次激活请求，来自玩家输入、AI、回放或网络。

```text
AbilityActivationRequest
  inputId
  trigger
```

初版不把移动输入放在 Request 中。移动、朝向、当前动作等运行时状态放在 Context。

### `AbilityActivationContext`

一次激活判定的上下文快照：

```text
AbilityActivationContext
  currentMode
  currentState
  currentAction
  currentFrame
  currentElapsedSeconds
  hasMoveIntent
  moveIntent
  hasBufferedMoveIntent
  bufferedMoveIntent
  actorRoot
  actionExecutor
```

注意：Context 可以读 `IActionExecutor` 的只读状态，但不要让 Condition 直接控制 `ActionExecutor`。

## AbilityService 职责

`AbilityService` 是运行时中心。

### 输入处理

```text
ProcessInput(request)
  -> 找到当前 AbilitySet
  -> 过滤 inputId / trigger 匹配的 AbilityEntry
  -> 按 priority 降序尝试
  -> TryActivate(ability, context)
```

### 激活流程

```text
TryActivate(ability, context)
  -> 检查 ActivationPolicy
  -> 检查 CancelWindowCondition
  -> 检查 ability.conditions
  -> 检查 cooldown
  -> 检查 costs
  -> 选择 branch 或 defaultPresentation
  -> 扣除 costs
  -> 启动 cooldown
  -> 返回 AbilityActivationResult
```

### 表现请求生成

AbilityService 不直接播放表现，只返回或派发：

```text
AbilityPresentationRequest
```

由 Part 3 的表现层驱动执行。

## 条件系统

### `AbilityCondition`

抽象条件：

```text
AbilityCondition
  Evaluate(context) -> AbilityConditionResult
```

结果建议包含：

```text
success
reason
```

`reason` 用于 Debug 或 Editor 校验，不参与玩法逻辑。

### 初版条件

#### `CurrentActionCondition`

用于跨模组派生：

```text
CurrentAction == Light1
CurrentAction == Light2
CurrentAction in [Light1, Light2]
```

#### `CurrentStateCondition`

用于限制 Locomotion / Action：

```text
CurrentState == Locomotion
CurrentState == Action
```

#### `CombatModeCondition`

用于不同战斗模式能力：

```text
CurrentMode == Katana
CurrentMode == Beast
```

#### `CancelWindowCondition`

用于 Action 中输入派生：

```text
CurrentAction has active CancelWindow
CancelWindow allows inputId
CancelWindow cancelType == Action
```

这会替代旧 `ActionExecutor.TryResolveCancelWindows` 的“解析下一招”职责。

## 消耗与冷却

### `AbilityCost`

初版仅定义接口，不强制接入资源系统：

```text
CanPay(context)
Pay(context)
```

可以先实现空成本：

```text
NoCost
```

### `AbilityCooldown`

初版支持最小冷却状态：

```text
durationSeconds
```

运行时由 `AbilityStateStore` 记录：

```text
abilityId -> cooldownRemaining
```

如果当前阶段不需要冷却，可保留字段为空。

## AbilityStateStore

运行时状态存储：

```text
AbilityStateStore
  cooldowns
  activeAbility
  lastActivatedAbility
  lastActivationTime
```

不要把运行时状态写回 `AbilityDefinition`。

## 与 CombatMode 的关系

`CombatModeProfile` 后续应绑定：

```text
CombatModeEntry
  mode
  abilitySet
  locomotionProfile
```

`CombatModeService` 继续负责当前模式与 Locomotion Profile 切换，但不再提供 `PlayerActionSet`。

## 与输入系统的关系

`InputReader` 仍采集离散输入。

`InputManager` 仍摄入：

```text
PressedInputIds
MoveIntent
LookIntent
BufferedMoveIntent
```

`AbilityService` 消费输入：

```text
foreach pressed input:
  AbilityService.ProcessInput(inputId, Pressed)
```

`Held / Released` 后续再扩展，不在初版扩大输入生命周期。

## 实施步骤

1. 新建 `Combat/Abilities` 目录结构。
2. 定义 `AbilityDefinition`、`AbilitySet`、`AbilityEntry`。
3. 定义 `AbilityActivationRequest`、`AbilityActivationContext`、`AbilityActivationResult`。
4. 实现 `AbilityService` 的 Entry 查找、优先级排序、条件判断流程。
5. 实现 `CurrentActionCondition`、`CurrentStateCondition`、`CombatModeCondition`、`CancelWindowCondition`。
6. 定义 `AbilityCost` / `AbilityCooldown` 接口与空实现或最小实现。
7. 将 `CombatModeProfile` 从 `PlayerActionSet` 概念迁移到 `AbilitySet` 概念。
8. 将 `CharacterActorFactory` 构造链中接入 `AbilityService`。

## 验收清单

- [ ] 输入到动作选择不再依赖 `PlayerActionSet`。
- [ ] `AbilitySet` 能枚举当前模式所有可用输入。
- [ ] `AbilityService` 能按 inputId、trigger、priority 选择 Ability。
- [ ] Ability 能根据当前 Action 做分支表现选择。
- [ ] CancelWindow 只作为 Ability 条件参与判断。
- [ ] 冷却和消耗接口存在，且不污染 `ActionExecutor`。
- [ ] Runtime 状态不写入 ScriptableObject。

## 风险

- Ability 分支如果继续膨胀，会接近 Graph，需要后续 `AbilityGraph` 或 `ActionGraph`。
- 初版不要做表达式系统，优先使用明确的条件类。
- Agent 不修改 `Assets/Data/**` 资产，Ability 资产需要用户在 Unity Editor 中创建和绑定。
