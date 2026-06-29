# ACTGame 架构规范

> 初版目标：参考 QFramework 的 Architecture / System / Model / Command / Query / Event 分层，保留 ACT 动作帧内部的强时序直连。

## 总原则

ACTGame 的跨系统通信统一通过架构层完成：一次行为使用 Command，状态变化使用 Event，领域能力放入 System，状态数据放入 Model，无副作用查询使用 Query。进入架构 IOC 的对象必须实现对应契约或基类，避免只靠命名约束职责。

动作系统内部允许保留直接依赖和接口回调，因为 Hitbox、CancelWindow、动作 VFX 与逻辑帧顺序强绑定，过度事件化会降低确定性与调试效率。

## 后缀规范

| 后缀 | 含义 |
|------|------|
| `Controller` | Unity 入口层，通常是 `MonoBehaviour`，负责生命周期、Inspector 引用、Unity 回调 |
| `System` | 架构级业务系统，注册到 `ACTGameArchitecture`，负责一个领域能力 |
| `Model` | 架构级数据状态，不承载复杂业务流程 |
| `Command` | 一次业务行为，可调用多个 System 并发送 Event |
| `Event` | 已经发生的事实，用于跨系统通知 |
| `Query` | 无副作用查询 |
| `Factory` / `Builder` | 对象创建或服务图组装 |
| `Actor` | 场景中一个可运行角色实例 |
| `Executor` | 局部执行器，例如单角色动作播放与动作会话推进 |
| `Service` | 局部服务，不注册到项目架构层 |

禁止新增业务类使用泛化的 `Runtime` 后缀。已有 `Runtime` 类在架构迁移中改为 `Actor`、`Executor`、`System`、`Factory` 等明确后缀。
`Controller` 仅用于 `App/Controllers` 下的 Unity 入口，且应继承 `AppControllerBase` 或实现 `IArchitectureController`；`Domain` 纯 C# 业务类应优先使用 `Service` / `Actor` / `Executor` / `Resolver` / `Detector` / `Consumer`。

## 强类型契约

- `IArchitectureSystem` / `ArchitectureSystemBase`：只有 System 契约对象可注册进 `ACTGameArchitecture` 并被 `GetSystem<T>()` 取出。
- `IArchitectureController` / `AppControllerBase`：Unity 表现入口通过能力方法发送 Command、Query 和订阅 Event。
- `IArchitectureCommand` / `ArchitectureCommandBase`：表达一次会改变状态的业务行为；命令实例只携带本次执行上下文，不保存跨帧可变状态。
- `IArchitectureQuery<TResult>` / `ArchitectureQueryBase<TResult>`：表达无副作用读取，不写状态、不发送事件。
- `IArchitectureEvent`：只有显式标记的事件类型可被 `SendEvent` / `RegisterEvent` 使用。
- `IArchitectureModel` / `ArchitectureModelBase` 与 `IArchitectureUtility`：预留共享状态与工具对象容器。

`Assets/Scripts/Editor/Architecture/ArchitectureBoundaryValidator.cs` 会在 Editor 中校验核心目录/后缀规则：`App/Systems` 的 `*System` 必须实现 System 契约，`App/Controllers` 的 MonoBehaviour 必须实现 Controller 契约，`App/Events` 的 `*Event` 必须实现 Event 契约，`Domain` 禁止直接访问 `ACTGameArchitecture.Interface`。

## 分层职责

## 目录落地（2026-06-24）

- `Assets/Scripts/App/Architecture`：`ACTGameArchitecture` 与 `IArchitecture*` 契约
- `Assets/Scripts/App/Commands`：跨系统行为命令（如 `ApplyHitCommand`）
- `Assets/Scripts/App/Events`：跨系统事实事件（如 `AttackHitEvent`、`HitStop*Event`）
- `Assets/Scripts/App/Systems`：注册到 Architecture 的业务系统（如 `CombatActorSystem`、`TargetSystem`、`CombatFeedbackSystem`）
- `Assets/Scripts/App/Controllers`：Unity 入口层 `MonoBehaviour`（如 `PlayerController`、`CameraManager`、`CombatWorldController`）
- `Assets/Scripts/Domain`：纯业务域对象（如 `Character/*`、`Combat/*`、`Input/*`、`Camera/*`）
- `Assets/Scripts/Infrastructure`：外设与框架适配层（如 `InputReader`、`AIInputSource`）

### Controller

- 只接收 Unity 生命周期、输入、碰撞、Inspector 引用。
- 不直接串联多个业务系统完成复杂流程。
- 复杂行为通过 Command、Query 或 Event 进入架构层。

### System

- 负责单一业务领域，例如目标注册、战斗角色注册、伤害、反馈、VFX。
- 可读写 Model，可发送 Event。
- 不直接依赖无关领域的具体实现。

### Command

- 表达一次业务行为，例如应用命中、开始动作、注册角色。
- 可调用多个 System。
- 完成业务后发送 Event。

### Event

- 表达“某事已经发生”。
- 只携带上下文数据，不写业务逻辑。
- 跨系统响应通过订阅 Event 完成。

### Query

- 表达无副作用查询，例如索敌、查询角色运行实例。
- 不修改状态，不发送事件。

## 动作系统例外

以下动作帧内链路允许强依赖直连：

```text
CharacterActor.Tick
  -> CharacterActionDriver
  -> CharacterStateMachine
    -> ActionState
      -> ActionExecutor.Tick
        -> ICombatFrameConsumer
          -> HitboxFrameConsumer
          -> ActionVfxPlayer
```

跨系统反馈必须事件化：

- 命中反馈
- 卡肉
- 镜头震动
- 命中特效
- 音效
- UI 飘字
- 伤害结算完成通知

## 命中流程规范

标准命中流程：

```text
HitboxFrameConsumer
  -> HitDetector
    -> ApplyHitCommand
      -> IActionHitReceiver.NotifyHit
      -> IHurtboxTarget.OnHit
      -> DamageSystem.ApplyDamage
      -> AttackHitEvent
        -> CombatFeedbackSystem / VfxSystem / CameraFeedbackSystem / AudioSystem / UI
```

禁止 `HitDetector` 直接调用 VFX、Camera、Audio、UI 或卡肉实现；它只接收目标集合和命中回调，跨系统结算由 App 层 Command 处理。

## 注册与查询规范

- 目标注册统一进入 `TargetSystem`。
- 角色战斗执行器注册统一进入 `CombatActorSystem`。
- 不新增 static registry 作为业务入口。
- 索敌目标集合通过 `GetActiveTargetsQuery` 或调用方注入完成，Domain 内的 `TargetingResolver` 只做纯计算。

## 迁移命名

| 旧名 | 新名 |
|------|------|
| `CharacterRuntime` | `CharacterActor` |
| `CharacterRuntimeFactory` | `CharacterActorFactory` |
| `ActionRuntimeController` | `ActionExecutor` |
| `IActionRuntime` | `IActionExecutor` |
| `CombatRuntimeRegistry` | `CombatActorSystem` |
| `CombatHitFeedback` | `AttackHitEvent` + 事件订阅 |
| `CombatHitStop` | `CombatFeedbackSystem` 状态 + `HitStopBeganEvent` / `HitStopEndedEvent` |
| `TargetRegistry` | `TargetSystem` |
