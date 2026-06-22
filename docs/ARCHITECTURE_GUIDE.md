# ACTGame 架构规范

> 初版目标：参考 QFramework 的 Architecture / System / Model / Command / Query / Event 分层，保留 ACT 动作帧内部的强时序直连。

## 总原则

ACTGame 的跨系统通信统一通过架构层完成：一次行为使用 Command，状态变化使用 Event，领域能力放入 System，状态数据放入 Model，无副作用查询使用 Query。

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

## 分层职责

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
          -> HitBoxSystem
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
HitBoxSystem
  -> HitDetectionSystem
    -> ApplyHitCommand
      -> IActionHitReceiver.NotifyHit
      -> IHurtboxTarget.OnHit
      -> DamageSystem.ApplyDamage
      -> AttackHitEvent
        -> CombatFeedbackSystem / VfxSystem / CameraFeedbackSystem / AudioSystem / UI
```

禁止 `HitDetectionSystem` 直接调用 VFX、Camera、Audio、UI 或卡肉实现。

## 注册与查询规范

- 目标注册统一进入 `TargetSystem`。
- 角色战斗执行器注册统一进入 `CombatActorSystem`。
- 不新增 static registry 作为业务入口。
- 索敌通过 `TargetSystem` 或 Query 完成。

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
