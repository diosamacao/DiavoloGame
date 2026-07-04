# Ability 重构 Part 3：AbilitySystem 表现层接入

> 主计划：`ability_驱动动作系统重构_c3ce1ae4.plan.md`  
> 本文是三段式重构的第三部分，目标是让 AbilitySystem 通过可扩展表现层驱动 `ActionExecutor`，并为未来 Graph、相机、音效、UI 等表现形式预留边界。

## 目标

AbilitySystem 不直接依赖 `ActionExecutor`，而是依赖表现层接口。

初版表现层只实现：

```text
AbilityPresentationRequest(ActionDefinition)
  -> ActionAbilityPresentationDriver
  -> ActionExecutor.TryStart(action)
```

未来可以扩展：

- ActionGraph 表现
- CameraShake 表现
- Audio 表现
- UI 表现
- StatusEffect 表现
- 多表现请求组合

核心原则：

```text
AbilityService 只决定要表现什么
PresentationDriver 负责如何表现
ActionExecutor 只播放 ActionDefinition
```

## 目标目录

```text
Assets/Scripts/Domain/Combat/Abilities/
  Presentation/
    AbilityPresentationKind.cs
    AbilityPresentationRequest.cs
    AbilityPresentationResult.cs
    IAbilityPresentationDriver.cs
    AbilityPresentationDispatcher.cs
    ActionAbilityPresentationDriver.cs
```

如果未来表现层变复杂，可继续拆：

```text
Presentation/Action/
Presentation/Camera/
Presentation/Audio/
```

初版不需要提前拆太细。

## 核心接口

### `IAbilityPresentationDriver`

表现驱动接口：

```csharp
public interface IAbilityPresentationDriver
{
    bool CanPlay(
        in AbilityPresentationRequest request,
        in AbilityActivationContext context);

    bool TryPlay(
        in AbilityPresentationRequest request,
        in AbilityActivationContext context,
        out AbilityPresentationResult result);
}
```

职责：

- 判断自己是否能处理请求
- 执行表现
- 返回表现结果

禁止：

- 修改 Ability 冷却
- 扣除 Ability 消耗
- 读取 InputReader
- 自行决定 Ability 是否能激活

### `AbilityPresentationRequest`

初版请求：

```text
AbilityPresentationRequest
  kind
  action
  requestActionState
```

字段建议：

- `kind`：表现类型，初版只有 `Action`。
- `action`：要播放的 `ActionDefinition`。
- `requestActionState`：播放成功后是否请求进入 `CharacterStateType.Action`。

后续可以添加：

```text
cameraShakeProfile
audioEvent
uiCue
statusEffect
graph
```

但不要在初版一次性做完。

### `AbilityPresentationResult`

表现执行结果：

```text
AbilityPresentationResult
  success
  playedAction
  shouldEnterActionState
  failReason
```

`AbilityService` 或上层 Actor 根据结果决定是否切状态。

### `AbilityPresentationDispatcher`

如果未来有多个表现驱动，可以引入 Dispatcher：

```text
AbilityPresentationDispatcher
  drivers[]
  TryPlay(request, context)
    -> 找到 CanPlay == true 的 driver
    -> driver.TryPlay
```

初版只有 `ActionAbilityPresentationDriver` 时，可以直接注入该 Driver；但保留 Dispatcher 设计有助于后续扩展。

## Action 表现驱动

### `ActionAbilityPresentationDriver`

初版核心实现：

```text
ActionAbilityPresentationDriver
  IActionExecutor actionExecutor

  CanPlay:
    request.kind == Action
    request.action != null
    actionExecutor.IsPlaying == false 或当前允许被新 Action 替换

  TryPlay:
    actionExecutor.TryStart(request.action)
```

注意：

- Driver 不判断 Ability 条件。
- Driver 可以判断表现层是否忙碌，例如 `ActionExecutor.IsPlaying`。
- 如果是 Cancel 派生，AbilityService 已经判断当前 CancelWindow 合法，Driver 只负责播放。

## 与 AbilityService 的关系

激活流程：

```text
AbilityService.ProcessInput
  -> TrySelectAbility
  -> EvaluateConditions
  -> SelectPresentationRequest
  -> presentationDriver.TryPlay
  -> OnPresentationPlayed
```

AbilityService 决定：

- 哪个 Ability
- 是否允许激活
- 使用哪个 Branch
- 生成什么 `AbilityPresentationRequest`
- 是否消耗资源 / 启动冷却

PresentationDriver 决定：

- 能否播放这个表现请求
- 实际调用哪个表现系统
- 表现是否成功

## 与状态机协作

推荐由 AbilityService 返回结果，CharacterActor 或 AbilityService 持有状态机引用后切状态。为了避免 AbilityService 过重，建议初版由 `CharacterActionDriver` 或新的 Ability 输入桥接类负责状态切换。

流程：

```text
InputManager.IngestFrame
  -> AbilityService.ProcessInput
  -> result.success && result.shouldEnterActionState
  -> CharacterStateMachine.TryChangeState(Action)
```

`ActionState` 继续负责：

```text
ActionExecutor.Tick(deltaTime)
ActionRotationDriver.Tick(...)
```

动作播放结束后，状态机回到 Locomotion。

## 与 CancelWindow 协作

旧流程：

```text
ActionExecutor
  -> 扫描 CancelWindow
  -> 消费输入
  -> PlayerActionSet.TryResolveNext
  -> TransitionTo(nextAction)
```

新流程：

```text
Input pressed while Action playing
  -> InputManager.Buffer(inputId)
  -> AbilityService.ProcessBufferedInput
  -> CancelWindowCondition 检查当前帧是否允许该 inputId
  -> AbilityDefinition Branch 选择表现
  -> PresentationDriver.TryPlay
  -> ActionExecutor.TryStart(nextAction)
```

重点：

- `ActionExecutor` 不再解析下一招。
- `CancelWindow` 只是 Ability 条件之一。
- 跨模组派生在 Ability Branch 中表达。

## 跨模组派生示例

### 轻击 Ability

```text
LightAttackAbility
  input: Light
  branches:
    CurrentAction == Light1 -> Light2
    CurrentAction == Light2 -> Light3
  defaultPresentation -> Light1
```

### 重击 Ability

```text
HeavyAttackAbility
  input: Heavy
  branches:
    CurrentAction == Light1 -> Heavy2
    CurrentAction == Light2 -> Heavy3
    CurrentAction == Heavy1 -> Heavy2
  defaultPresentation -> Heavy1
```

这样可以支持：

```text
Light1 + Light -> Light2
Light1 + Heavy -> Heavy2
Light2 + Heavy -> Heavy3
Heavy1 + Heavy -> Heavy2
```

不需要修改 `ActionExecutor`。

## 多表现扩展

如果某个 Ability 需要播放 Action，同时触发其他表现，未来可以让 `AbilityDefinition` 支持多个请求：

```text
presentationRequests[]
  Action(Light1)
  CameraShake(Small)
  Audio(Slash)
```

初版可以不做多请求，但接口设计不要阻止后续扩展。

Dispatcher 后续流程：

```text
foreach request in presentationRequests:
  dispatcher.TryPlay(request, context)
```

## 装配方式

`CharacterActorFactory` 后续装配：

```text
new ActionExecutor(...)
new ActionAbilityPresentationDriver(actionExecutor)
new AbilityService(combatMode, abilitySetProvider, presentationDriver)
new CharacterActor(... abilityService ...)
```

如果引入 Dispatcher：

```text
var actionDriver = new ActionAbilityPresentationDriver(actionExecutor);
var dispatcher = new AbilityPresentationDispatcher(actionDriver);
var abilityService = new AbilityService(..., dispatcher);
```

## 与现有 CharacterActionDriver 的关系

重构后可以有两种选择。

### 方案 A：保留并降级

`CharacterActionDriver` 继续存在，但只做：

- 输入注册
- 输入缓冲
- 调用 AbilityService
- 状态机切换
- Movement Cancel 桥接

### 方案 B：重命名为 Ability 输入桥接

如果旧命名误导，可以改为：

```text
CharacterAbilityDriver
```

职责更清楚，但改动更大。建议在本次重构中评估后再决定，避免无意义命名 churn。

## 实施步骤

1. 定义 `AbilityPresentationKind`、`AbilityPresentationRequest`、`AbilityPresentationResult`。
2. 定义 `IAbilityPresentationDriver`。
3. 实现 `ActionAbilityPresentationDriver`。
4. 可选实现 `AbilityPresentationDispatcher`。
5. 修改 `AbilityService`：激活成功后生成表现请求并调用 Driver。
6. 修改 `CharacterActorFactory`：装配 AbilityService 与表现 Driver。
7. 修改输入桥接：输入进入 AbilityService 后，根据表现结果切状态。
8. 将 CancelWindow 派生从 `ActionExecutor` 迁移到 `AbilityService + CancelWindowCondition`。
9. 验证轻击、重击、闪避、Cancel 派生。

## 验收清单

- [ ] AbilityService 不直接依赖 Animator。
- [ ] AbilityService 不直接依赖 Hitbox / VFX。
- [ ] AbilityService 不直接调用 `ActionExecutor.TryStart`，而是通过表现 Driver。
- [ ] ActionAbilityPresentationDriver 是唯一调用 `ActionExecutor.TryStart` 的 Ability 表现实现。
- [ ] `ActionExecutor` 不知道 Ability。
- [ ] Cancel 派生通过 Ability 条件和 Branch 表达。
- [ ] 轻击 1 接重击 2、轻击 2 接重击 3 可通过 Ability 配置表达。
- [ ] 新增表现类型不需要修改 `ActionExecutor`。

## Unity Editor 资产操作

Agent 不修改 `.asset`。脚本完成后需在 Editor 中：

1. 创建 `AbilitySet`。
2. 创建 `LightAttackAbility`、`HeavyAttackAbility`、`DodgeAbility` 等 `AbilityDefinition`。
3. 在 `AbilitySet` 中绑定输入与 Ability。
4. 在 Ability 的 Branch / Default Presentation 中绑定 `ActionDefinition`。
5. 在角色配置中绑定 `AbilitySet`。
6. Play Mode 验证起手、连段、跨模组派生、Cancel、闪避。

## 风险

- 如果 `AbilityPresentationRequest` 初版塞入太多字段，会变成新的大杂烩。只实现当前需要的 Action 表现。
- 如果 Driver 参与 Ability 条件判断，会破坏边界。条件只在 AbilityService 中做。
- 如果状态机切换分散在多个 Driver 中，后续会难维护。初版建议统一由 AbilityService 结果或输入桥接层请求状态切换。
