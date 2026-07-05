# ACTGame — 动作系统技术实现文档

> 本文档描述**当前已落地**的动作系统：架构、实现细节、使用方式，以及与 [ACTION_EDITOR.md](./ACTION_EDITOR.md) 长期目标的对齐分析。  
> Last updated: 2026-07-05（Resolver 重构：出招表 Entry 绑定 ActionResolver；ActionExecutor 不再做输入/Dodge 特判；Actions 目录按 Definitions / Resolution / Execution / Frames 分层）

---

## 0. 当前脚本层级与调用链（重构后）

### 0.1 运行时挂载边界

玩家根对象运行时只允许存在：

- `PlayerController`：Scene 入口，负责读取 `CharacterConfig` 并创建纯 C# 运行时。
- `CharacterController`：Unity 碰撞与位移执行组件，由 `CharacterActorFactory` 补齐或复用。
- 模型子物体：由 `CharacterConfig.ModelPrefab` 实例化，保留模型、Renderer、Animator、美术相关组件。

不再允许把以下业务脚本挂到 Player 根对象：`InputReader`、`CharacterAnimationService`、`CombatModeService`、`ActionExecutor`、`CharacterActionDriver`、`ActionRotationDriver`、`CombatTargetLock`、`HitBoxSystem`、`ActionVfxPlayer`、`CharacterStateMachine`。这些现在都是纯 C# Actor / Executor / Service，由工厂创建并持有。

### 0.2 文件层级

| 层级 | 文件 | 形态 | 职责 |
|------|------|------|------|
| Scene 入口 | `Assets/Scripts/App/Controllers/Gameplay/PlayerController.cs` | `MonoBehaviour` | Scene Empty 上唯一玩家脚本；创建玩家输入源并 Tick `CharacterActor` |
| Actor 工厂 | `Assets/Scripts/Domain/Character/CharacterActorFactory.cs` | static 纯 C# | 读取 `CharacterConfig` + `ICharacterInputSource`，实例化模型，补齐 `CharacterController`，组装 Actor/Executor/Service |
| 角色 Actor | `Assets/Scripts/Domain/Character/CharacterActor.cs` | 纯 C# | 输入采集、动作路由、重力、状态机 Tick |
| 配置根 | `Assets/Scripts/Domain/Character/CharacterConfig.cs` | `ScriptableObject` 定义类 | 模型、输入、移动、动画、战斗模式、挂点名等角色配置 |
| 输入源抽象 | `Assets/Scripts/Domain/Input/ICharacterInputSource.cs` | interface | 玩家、AI、回放、网络输入统一入口 |
| 玩家输入源 | `Assets/Scripts/Infrastructure/Input/InputReader.cs` | 纯 C# | Input System → `PlayerInputFrame` |
| AI 输入源 | `Assets/Scripts/Infrastructure/Input/AIInputSource.cs` | 纯 C# | AI 决策 → `PlayerInputFrame`，复用 `CharacterActorFactory` |
| 输入中枢 | `Assets/Scripts/Domain/Input/InputManager.cs` | 纯 C# | 摄入输入帧、离散输入回调、输入缓冲、移动意图 |
| 移动服务 | `Assets/Scripts/Domain/Character/CharacterMotor.cs` | 纯 C# | Locomotion 位移、重力、移动意图解析、起手面向 |
| 动画服务 | `Assets/Scripts/Domain/Character/Animation/CharacterAnimationService.cs` | 纯 C# | Locomotion CrossFade、Action Clip 播放、动画锁 |
| Root Motion | `Assets/Scripts/Domain/Character/Animation/CharacterRootMotionDriver.cs` | 纯 C# + 内部 Receiver | 控制 Animator Root Motion；内部 `CharacterRootMotionReceiver` 仅作为 `OnAnimatorMove` 桥接挂在 Animator 子物体 |
| 状态机 | `Assets/Scripts/Domain/Character/StateMachine/CharacterStateMachine.cs` | 纯 C# | 注册并 Tick `LocomotionState` / `ActionState` |
| 状态上下文 | `Assets/Scripts/Domain/Character/StateMachine/CharacterContext.cs` | 纯 C# | 状态机共享 Transform、Animation、Motor、ActionExecutor、Motor 快照 |
| Locomotion 状态 | `Assets/Scripts/Domain/Character/StateMachine/States/LocomotionState.cs` | 纯 C# State | 根据移动幅度选择 Idle/Walk/Run |
| Action 状态 | `Assets/Scripts/Domain/Character/StateMachine/States/ActionState.cs` | 纯 C# State | Tick `IActionExecutor`；招式结束回 Locomotion |
| 战斗模式 | `Assets/Scripts/Domain/Combat/CombatModeService.cs` | 纯 C# | 当前模式、出招表、Locomotion Profile 切换 |
| 战斗模式配置 | `Assets/Scripts/Domain/Combat/CombatModeProfile.cs` | `ScriptableObject` 定义类 | mode → PlayerActionSet / Locomotion Profile 绑定 |
| 动作执行 | `Assets/Scripts/Domain/Combat/Actions/Execution/ActionExecutor.cs` | 纯 C# | Action 播放、Cancel、Transition、Logic Tick、事件派发、命中回流（不做输入/动作类型特判） |
| 动作会话 | `Assets/Scripts/Domain/Combat/Actions/Execution/ActionSession.cs` | 纯 C# | 当前招式、时间、逻辑帧、命中确认、卡肉暂停 |
| 输入路由 | `Assets/Scripts/Domain/Combat/Actions/Execution/CharacterActionDriver.cs` | 纯 C# | 起手（经 Resolver 解析）、输入缓冲、移动取消、离开 Action 后预输入消费 |
| 动作旋转 | `Assets/Scripts/Domain/Combat/Actions/Execution/ActionRotationDriver.cs` | 纯 C# | RotationWindow 内按输入/锁定目标修正朝向 |
| 动作解析服务 | `Assets/Scripts/Domain/Combat/Actions/Resolution/ActionResolverService.cs` | 纯 C# | 按当前出招表把输入请求路由到对应 ActionResolver（起手 + Cancel 共用） |
| 动作解析策略 | `Assets/Scripts/Domain/Combat/Actions/Resolution/ActionResolver.cs`（+ Single / Combo / Directional 子类） | `ScriptableObject` | 把 ActionRequest + 上下文解析为最终 ActionDefinition |
| 出招表 | `Assets/Scripts/Domain/Combat/Actions/Resolution/PlayerActionSet.cs` | `ScriptableObject` 定义类 | 离散输入 → ActionResolver 映射 |
| 索敌锁定 | `Assets/Scripts/Domain/Combat/Targeting/CombatTargetLock.cs` | 纯 C# | 单角色当前锁定目标状态 |
| 架构入口 | `Assets/Scripts/App/Architecture/ACTGameArchitecture.cs` | 纯 C# | System 注册、Command 执行、Query 查询、Event 分发 |
| 战斗角色注册 | `Assets/Scripts/App/Systems/Combat/CombatActorSystem.cs` | 纯 C# System | Transform → `CharacterActor` / `ActionExecutor` / Animator 查询 |
| 目标注册 | `Assets/Scripts/App/Systems/Combat/TargetSystem.cs` | 纯 C# System | 当前场景可命中/可索敌目标列表 |
| 索敌查询 | `Assets/Scripts/Domain/Combat/Targeting/TargetingSystem.cs` / `TargetSelector.cs` | static 纯 C# | 从 `TargetSystem` 中选择目标、计算目标方向 |
| 受击目标 | `Assets/Scripts/App/Controllers/Combat/HurtboxTarget.cs` | `MonoBehaviour` | 场景目标组件；OnEnable/OnDisable 注册到 `TargetSystem` |
| 命中帧消费者 | `Assets/Scripts/Domain/Combat/Hitbox/HitBoxSystem.cs` | 纯 C# | 作为 `ICombatFrameConsumer` 接收逻辑帧，提交 OBB 检测 |
| 命中批处理 | `Assets/Scripts/Domain/Combat/Hitbox/HitDetectionSystem.cs` | static 纯 C# | 扫描 `TargetSystem`，执行 OBB 相交，发送 `ApplyHitCommand` |
| VFX 帧消费者 | `Assets/Scripts/Domain/Combat/VFX/ActionVfxPlayer.cs` | 纯 C# | 作为 `ICombatFrameConsumer` 按帧触发 `ActionVfxSpawner` |
| VFX 池 | `Assets/Scripts/Domain/Combat/VFX/VFXManager.cs` | `MonoBehaviour` Manager | 场景级对象池与 VFX 实例生命周期 |
| 战斗世界 | `Assets/Scripts/App/Controllers/Combat/CombatWorldController.cs` | `MonoBehaviour` Manager | 场景级战斗系统生命周期锚点，确保反馈系统存在 |
| 命中命令 / 事件 | `ApplyHitCommand` / `AttackHitEvent` | 纯 C# | 命中后统一回调目标、攻击者，并向反馈系统广播 |
| 反馈系统 | `Assets/Scripts/App/Controllers/Combat/FeedbackController.cs` | `MonoBehaviour` Manager | 场景级反馈入口，托管 `HitStopController` |
| 卡肉控制 | `Assets/Scripts/App/Controllers/Combat/HitStopController.cs` | `MonoBehaviour` Manager | 订阅命中反馈，暂停纯 C# ActionExecutor 并冻结 Animator |
| 相机 | `Assets/Scripts/App/Controllers/Camera/CameraManager.cs` | `MonoBehaviour` Manager | 第三人称相机；通过 `PlayerController.Input.LookIntent` 读取视角输入 |

### 0.3 启动装配链

```
Scene Empty
  └─ PlayerController.Awake
      → CharacterConfig.ValidateForPlayer
      → new InputReader(CharacterConfig.InputActions)
      → CharacterActorFactory.Create
          → EnsureCombatWorldController
          → Instantiate(CharacterConfig.ModelPrefab)
          → GetOrAdd CharacterController（唯一允许补到 Player 根的 Unity 组件）
          → use ICharacterInputSource（玩家为 InputReader，敌人为 AIInputSource）
          → new CharacterAnimationService(Animator, LocomotionProfile)
          → new CharacterRootMotionDriver(CharacterController, Animator)
              → Animator 子物体 AddComponent<CharacterRootMotionReceiver>（OnAnimatorMove 桥接）
          → new CombatModeService(CombatModeProfile, AnimationService)
          → new CharacterContext(root, animation, controller)
          → new CharacterStateMachine(context)
          → new ActionResolverService(combatMode)
          → new ActionExecutor(root, controller, animation, rootMotion, combatMode, resolverService)
          → new CombatTargetLock(root, teamId, aimOrigin)
          → new HitBoxSystem(root, actionRuntime, attachPoint)
          → new ActionVfxPlayer(root, attachPoint)
          → actionRuntime.RegisterFrameConsumer(HitBoxSystem / ActionVfxPlayer)
          → new CharacterActionDriver(inputReader, inputManager, stateMachine, actionRuntime, combatMode, targetLock, resolverService, root, motor)
          → actionRuntime.BindInputBuffer(actionDriver.CreateInputBufferBridge())
          → new CharacterActor(...)
          → new ActionRotationDriver(root, stateMachine, inputManager, runtime, actionRuntime, targetLock)
          → CombatActorSystem.Register(root, actor, actionExecutor, animator)
```

### 0.4 每帧调用链

```
PlayerController.Update
  → CharacterActor.Tick(deltaTime)
      → InputReader.CaptureFrame
      → InputManager.IngestFrame
          → RegisterPressed(inputId) callbacks
              → CharacterActionDriver.HandleDiscreteInput
      → CharacterActionDriver.ProcessGameplayInput
          → Locomotion: TryStartFromLocomotion(inputId)
              → ActionResolverService.TryResolveStart(request, context)
              → ActionExecutor.TryStart(resolvedAction)
              → CharacterStateMachine.TryChangeState(Action)
          → Action: Buffer(inputId) / Movement Cancel
      → CharacterMotor.TickGravity
      → CharacterStateMachine.Tick
          → LocomotionState.Tick
              → CharacterMotor.TickLocomotion
              → sync Motor snapshot to CharacterContext
              → CharacterAnimationService.Play(Idle/Walk/Run)
          → ActionState.Tick
              → CharacterMotor.ClearMoveSnapshot
              → ActionExecutor.Tick(deltaTime)
              → ActionRotationDriver.Tick
                  → CombatTargetLock.Tick(ActionSession)
                  → TargetingSystem.Select / TryGetDirectionToTarget
                  → rotate actor root during RotationWindow
```

### 0.5 ActionExecutor Logic Tick 链

```
ActionExecutor.Tick / UpdateFrame
  → ActionSession.Advance / SetFrame
  → ApplyScriptedDisplacement 或 Root Motion
  → SyncLogicFrameFromElapsed
      → DispatchCombatFrame(frame)
          → ICombatFrameConsumer.OnCombatFrameAdvanced
              → HitBoxSystem
                  → HitDetectionSystem.ProcessHitboxesAtFrame
                      → TargetSystem.ActiveTargets
                      → HitboxMath.Intersects
                      → IHurtboxTarget.OnHit
                      → ApplyHitCommand
                          → IHurtboxTarget.OnHit
                          → IActionHitReceiver.NotifyHit(ActionExecutor)
                          → AttackHitEvent
              → ActionVfxPlayer
                  → ActionVfxSpawner.Spawn
                  → VFXManager / ObjectPool
          → DispatchActionEvents
              → IActionEventConsumer.OnActionEvent（后续扩展）
  → TryResolveCancelWindows
  → TryResolveTransitions
  → duration end: Stop
```

### 0.6 命中反馈 / 卡肉链

```
HitDetectionSystem
  → ApplyHitCommand
      → AttackHitEvent
      → HitStopController.HandleAttackHit
          → CombatActorSystem.TryGet(attackerRoot)
          → ActionExecutor.TryConsumeHitStopTrigger
          → ActionExecutor.SetHitStopPaused(true)
          → Animator.speed = 0
          → Update unscaled timer
          → ActionExecutor.SetHitStopPaused(false)
          → Animator.speed restore
          → CombatFeedbackSystem.BeginHitStop / EndHitStop
```

### 0.7 关键边界

- Player 根对象不挂业务脚本；运行时业务必须放在 `CharacterActor` 及其纯 C# service。
- `ActionExecutor` 不查找 GameObject 组件，依赖全部由 `CharacterActorFactory` 构造注入。
- `HitBoxSystem` / `ActionVfxPlayer` 不是组件，只是 `ICombatFrameConsumer`。
- `CombatWorldController`、`FeedbackController`、`VFXManager`、`CameraManager` 是场景级 Manager；它们可以是 `MonoBehaviour`，但不挂在 Player 根对象上。
- `CharacterRootMotionReceiver` 是唯一保留的运行时桥接组件，因为 Unity 的 `OnAnimatorMove` 必须由 `MonoBehaviour` 接收；它挂在 Animator 子物体，不是玩家业务组件。

---

## 1. 文档定位

| 文档 | 内容 |
|------|------|
| **本文档（ACTION_SYSTEM.md）** | 已实现功能、运行时行为、编辑器适配评估 |
| [ACTION_EDITOR.md](./ACTION_EDITOR.md) | 动作编辑器愿景、完整数据模型、分期规划 |
| [TECHNICAL.md](../.cursor/skills/actgame-architecture/TECHNICAL.md) | 全项目功能索引 |

**当前阶段：** Phase A 后期 — 取消窗 / 收招 / 出招表 / 战斗模式 / 线性连招队列已落地；**Hitbox 判定骨架**（OBB 重叠 + 受击回调）已接入；`ActionEvent` 已有运行时派发入口但 Hitbox/VFX 仍兼容旧数组；**不含**伤害结算、Hit 状态、ActionEditorWindow。

---

## 2. 功能状态总览

| 能力 | 状态 | 说明 |
|------|------|------|
| `ActionDefinition` SO | ✅ 已实现 | 动画、帧数、CancelWindow、Transition、位移、起手行为 |
| `CancelWindow`（Action / Movement） | ✅ 已实现 | 帧窗口 + priority；Action 取消不直接绑目标招 |
| `ActionResolver` 解析层 | ✅ 已实现 | 出招表 Entry 绑定 Resolver；`Single` / `Combo`（线性连段）/ `Directional`（方向闪避）三类策略 |
| `ActionResolverService` | ✅ 已实现 | 起手与 Cancel 共用路由；`ActionExecutor` 不再认知出招表结构 |
| `ActionTransition` | 🟡 部分 | `AnimationEnd`、`AtFrame`；无 OnHit / OnWhiff |
| `PlayerActionSet` + `CombatModeProfile` | ✅ 已实现 | 多战斗模式出招表；模式切换 Locomotion Profile |
| `CombatModeService` | ✅ 已实现 | Immediate / OnNextLocomotion / StopCurrentAction |
| `ActionExecutor` | ✅ 已实现 | 播放、取消、Transition、Root Motion / 脚本位移 |
| `InputManager` + `PlayerInputFrame` | ✅ 已实现 | 帧快照、多 id 缓冲、移动意图 |
| `ActionStartBehavior` | 🟡 部分 | `FaceBufferedMoveIntent`、`SwitchCombatMode` |
| Root Motion 桥接 | ✅ 已实现 | `CharacterRootMotionDriver` + Receiver |
| `ActionState` + 动画锁定 | ✅ 已实现 | 薄层状态机 |
| `HitboxKeyframe` + `HitBoxSystem` | 🟡 部分 | `ActionDefinition` 帧表 + OBB 检测；无 Physics |
| `HurtboxTarget` / `IHurtboxTarget` | 🟡 部分 | `TargetSystem` 注册 + `OnHit` 回调；现阶段仅测试日志 |
| `ActionPhase` / `ActionEvent` | 🟡 骨架 | SO 字段与类型已建；`ActionEvent` 已有运行时派发入口，消费者待扩展 |
| `ActionGraph` 节点图 | ⬜ 未实现 | 由 `ComboActionResolver` 线性折中 |
| `UpdateFrame(frameIndex)` 统一 Logic Tick | ✅ 已实现 | `ActionExecutor` + `ICombatFrameConsumer` |
| Combat 伤害 / `Hit` 状态 / OnHit 回流 | 🟡 部分 | `IActionHitReceiver` + OnHitConfirm Transition；无伤害/Hit 状态 |
| `ActionEditorWindow` | ⬜ 未实现 | M5 目标 |

---

## 3. 架构总览

### 3.1 模块关系

```
InputReader.CaptureFrame()（纯 C#）
       │
       ▼
CharacterActor ── InputManager（唯一持有者）
       │    ├─ RegisterPressed(inputId) → 起手 / Buffer
       │    ├─ MoveIntent / BufferedMoveIntent
       │    └─ Movement 取消 → Locomotion
       │
       ├── CombatModeService ── CombatModeProfile
       │         └─ mode → PlayerActionSet → ActionEntry(input → ActionResolver)
       │
       ├── ActionResolverService ── 按出招表把 ActionRequest 路由到 Resolver
       │         └─ Single / Combo / Directional Resolver → ActionDefinition
       ├── CharacterActionDriver（纯 C#）── 起手(经 Resolver) / Buffer / 移动取消
       ├── ActionRotationDriver（纯 C#）── RotationWindow + TargetLock
       ├── ActionExecutor（IActionExecutor + IActionHitReceiver）
       │         ├─ UpdateFrame / Tick → ICombatFrameConsumer
       │         ├─ Cancel 下一招 → ActionResolverService.TryResolveNext
       │         └─ ActionDefinition（单招 + Phase/Event 骨架）
       ├── HitBoxSystem（纯 C# ICombatFrameConsumer → OBB + NotifyHit）
       │
       └── CharacterStateMachine（纯 C#）
                 ├─ LocomotionState（Idle/Walk/Run）
                 └─ ActionState（Tick Executor → 结束回 Locomotion）
```

### 3.2 职责分层

| 层 | 职责 |
|----|------|
| `ICharacterInputSource` | 角色输入源抽象：玩家、AI、回放、网络 |
| `InputReader` | 玩家设备 → `PlayerInputFrame`（纯 C#） |
| `InputManager` | 摄入帧、离散缓冲、移动意图、回调注册 |
| `PlayerController` | Scene 入口，只创建玩家输入源并 Tick `CharacterActor` |
| `CharacterActor` | 输入采集、动作路由、重力、状态机 Tick |
| `CharacterActionDriver` | 输入路由、起手切状态（经 Resolver）、移动取消、缓冲消费 |
| `ActionResolverService` + `ActionResolver` | 输入请求 → ActionDefinition 的解析策略层 |
| `ActionRotationDriver` | RotationWindow + 索敌转向 |
| `CombatModeService` | 战斗模式、出招表切换、Locomotion Profile |
| `ActionExecutor` | 播放、Cancel、Transition、**UpdateFrame**、命中回流（不做输入/动作类型特判） |
| `HitBoxSystem` | `ICombatFrameConsumer`：Logic Tick 帧上 OBB 检测 |
| `ActionState` / `LocomotionState` | 动画锁与 Locomotion 动画 |

### 3.3 设计原则（已贯彻）

1. **数据驱动** — 单招数据在 `ActionDefinition`；选招策略在 `ActionResolver`（Single/Combo/Directional）；输入→Resolver 映射在 `PlayerActionSet`。
2. **选招与播放分离** — `ActionResolverService` 负责"选哪招"，`ActionExecutor` 只负责"播已解析好的招"，执行器不认识出招表也不做 Dodge 特判。
3. **输入与玩法解耦** — 状态机不读输入；`InputManager` 由 `CharacterActor` 持有。
4. **状态机薄层** — `Action` 状态只 Tick `IActionExecutor`。
5. **Animator 双轨** — Locomotion 走 Profile；招式 `PlayClip`。
6. **角色无关执行器** — `ActionExecutor` 可复用于敌人（输入源可替换）。

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
| Hitboxes | `hitboxes[]`（`HitboxKeyframe`） | 帧区间内生效的攻击 OBB；由 `HitBoxSystem` 采样 |
| Movement | `useRootMotion`, `displacementDistance`, 帧窗口 | Root Motion 或脚本位移 |

**帧换算：** `frame = FloorToInt(elapsed * sampleRate)`

### 4.7 Hitbox / Hurtbox（碰撞数据）

| 类型 | 字段 | 说明 |
|------|------|------|
| `HitboxKeyframe` | `hitboxId`, `startFrame`, `endFrame`, `localOffset`, `localEulerAngles`, `size` | 挂在攻击者 `attachPoint` 的局部 Box；`GetActiveHitboxesAtFrame` 按帧筛选 |
| `HurtboxDefinition` | `localOffset`, `localEulerAngles`, `size` | 受击方局部 Box；`HurtboxTarget` Inspector 配置 |
| `ActionHitContext` | `Action`, `Hitbox`, `Attacker` | 一次命中判定的只读上下文，传给 `IHurtboxTarget.OnHit` |

碰撞几何统一为 `HitboxOrientedBox`（OBB），由 `HitboxMath` 构建与相交检测，**不依赖** Unity Physics。

### 4.2 CancelWindow

| 字段 | 说明 |
|------|------|
| `startFrame` / `endFrame` | 生效帧区间 |
| `cancelType` | `Action`：消费缓冲并衔接下一招；`Movement`：由 `CharacterActionDriver` 读取 `InputManager.HasMoveIntent` |
| `allowedInputs` | `InputActionReference[]`；运行时 id = Action 名 |
| `priority` | 降序扫描，首个匹配生效 |

**与 ACTION_EDITOR 的差异：** 当前 **无 `targetAction` 字段**。Action 取消的下一招由 `ActionExecutor` 消费匹配输入后委托 `ActionResolverService.TryResolveNext` → 对应 `ActionResolver` 解析，而非取消窗直接指向目标 SO。

### 4.3 ActionTransition

| `condition` | 运行时行为 |
|-------------|------------|
| `AnimationEnd` | `elapsed >= DurationSeconds` 时触发 |
| `AtFrame` | `frame >= startFrame` 时每帧检查（可提前自动衔接） |

按 `priority` 降序；`targetAction == null` 则 `Stop`。

### 4.4 ActionResolver（动作解析策略）

`ActionResolver` 是 `ScriptableObject` 策略基类，统一契约：

```csharp
bool TryResolve(in ActionRequest request, in ActionResolveContext context, out ActionDefinition action);
```

- `ActionRequest`：输入侧意图（`InputId` + `ActionInputTrigger`，当前仅 `Pressed`）。
- `ActionResolveContext`：世界/状态侧信息（`Origin` = LocomotionStart / CancelWindow、`CurrentAction`、`ActorRoot`、`IActionStartContext`）。

| 子类 | 数据 | 行为 |
|------|------|------|
| `SingleActionResolver` | `action` | 始终返回固定招；用于切模式、单段技能、单段闪避 |
| `ComboActionResolver` | `steps[]` + `ComboLeafPolicy` | `CurrentAction==null` 或不在队列 → `steps[0]`；在队列则 `index+1`；末段按 `LoopToRoot / StopCombo` |
| `DirectionalActionResolver` | `defaultAction` + 前/后/左/右 + `sideThresholdDeg` + `rotateToInputOnForward` | 依 `Origin`：Locomotion 起手偏前闪并可先转向；CancelWindow 按输入与朝向夹角判左右/前后；变体缺失回退 `defaultAction`，仍无效则失败 |

Resolver 作为资产绑定在 `PlayerActionSet.ActionEntry.resolver`。

### 4.5 PlayerActionSet / CombatModeProfile

```
CombatModeProfile
  └─ CombatModeEntry[] (mode, actionSet, locomotionProfile)
       └─ PlayerActionSet
            └─ ActionEntry[] (input → ActionResolver)
```

| 组件 | 职责 |
|------|------|
| `PlayerActionSet.TryGetResolver(inputId, out resolver)` | 按输入 id 找绑定的 Resolver |
| `ActionResolverService.TryResolveStart / TryResolveNext` | 起手 / Cancel 解析（同一路由，差异由 context 表达） |
| `CombatModeService` | 运行时当前 mode、挂起切换、Locomotion Profile |

### 4.6 输入 id

- 离散输入 id = Input System **Action 名**（`Attack`、`Dodge` 等）。
- 移动取消不走路由表，由 `InputManager.HasMoveIntent` + `CancelType.Movement` 窗口判定。

---

## 5. 运行时流程

### 5.1 每帧顺序

```
PlayerController.Update（ExecutionOrder -50）
  → CharacterActor.Tick
  1. IngestInput
  2. ProcessGameplayInput（离 Action 清缓冲 / 应用挂起 mode / 移动取消）
  3. ExecuteMovement
  4. ApplyGravity

CharacterStateMachine.Tick
  → ActionState.Tick → ActionExecutor.Tick

HitBoxSystem.OnCombatFrameAdvanced（ActionExecutor 同步派发）
  → 读 CurrentAction + CurrentFrame
  → GetActiveHitboxesAtFrame → OBB 相交 → OnHit
```

### 5.2 起手（Locomotion → Action）

```
离散输入 → InputManager.NotifyPressed
  → CharacterActionDriver.TryStartFromLocomotion(inputId)
  → ActionResolverService.TryResolveStart(request, context{Origin=LocomotionStart})
      → ActionEntry.resolver.TryResolve → ActionDefinition
  → ActionExecutor.TryStart(resolvedAction)
      → ExecuteStartBehaviors → BeginAction(PlayClip)
  → TryChangeState(Action)
```

### 5.3 招内 Cancel（连段 / 方向派生）

```
输入 → Buffer(inputId)

ActionExecutor.Tick → TryResolveCancelWindows:
  → 按 priority 扫描 CancelType.Action 窗口
  → HasBuffer(allowedInput) → Consume
  → ActionResolverService.TryResolveNext(request, context{Origin=CancelWindow, CurrentAction})
      → ComboActionResolver 进位 / DirectionalActionResolver 方向派生
  → ClearOtherActionBuffers → TransitionTo(next)
```

### 5.4 移动取消

```
招式中 HasMoveIntent && CanCancelByMovement
  → CharacterStateMachine 切 Locomotion
  → ActionState.Exit → Stop()
```

### 5.5 收招（Transition / 自然结束）

```
每帧 TryResolveTransitions（AtFrame 可提前触发）
  → 无匹配且 elapsed >= Duration → Stop
  → ActionState 下一帧回 Locomotion
```

### 5.6 战斗模式切换

- 起手行为 `SwitchCombatMode` 或外部 `CombatModeService.TrySetMode`。
- `OnNextLocomotion`：招式中挂起，回 Locomotion 后 `ApplyPendingModeIfReady`。
- 切换 mode 可换 `PlayerActionSet` 与 `CharacterAnimationProfile`（Locomotion）。

### 5.7 与碰撞系统（Hitbox）的通信

动作执行器通过 `ICombatFrameConsumer` **同步派发逻辑帧**；`HitBoxSystem` 作为纯 C# 帧消费者读取招式状态与 SO 数据，完成判定后推送给受击方与动作运行时。

```
ActionExecutor                   HitBoxSystem              受击方
        │                              │                      │
        │  CombatFrameContext          │                      │
        │ ────────────────────────────►│                      │
        │                              │ GetActiveHitboxesAtFrame
        │                              │ HitboxMath.Build + Intersects
        │                              │ ─────────────────────► OnHit(context)
        │                              │                      │
        │◄──────── NotifyHit(context)  │                      │
```

| 环节 | 方向 | 载体 |
|------|------|------|
| 帧同步 | 动作 → 碰撞 | `CombatFrameContext` / `ICombatFrameConsumer` |
| 攻击形状 | 碰撞 → 数据（只读） | `ActionDefinition.GetActiveHitboxesAtFrame` |
| 受击目标发现 | 碰撞内部 | `TargetSystem.ActiveTargets` |
| 命中通知 | 碰撞 → 受击方 | `IHurtboxTarget.OnHit(in ActionHitContext)` |
| 防重复命中 | 碰撞内部 | `(hitboxId, targetInstanceId)` 缓存，换招清空 |

**同招单次命中：** 同一 `ActionDefinition` 播放周期内，每个 `(HitboxId, TargetInstanceId)` 对只触发一次 `OnHit`；`TransitionTo` / `Stop` 换招时 `ClearHitCacheIfNeeded` 清空缓存。

---

## 6. 使用方式（Editor）

### 6.1 配置三连招

1. 创建 `ComboActionResolver`（Create → ACT/Combat/Resolvers/Combo Action Resolver），`steps` = [attack_1, attack_2, attack_3]，设置 `leafPolicy`。
2. `PlayerActionSet` Entry：`Attack` → 上述 `ComboActionResolver`。
3. 各 `ActionDefinition` 的 **Cancel Windows** 添加 `CancelType.Action` 窗 + `allowedInputs: [Attack]`（无需填目标招）。
4. 可选 **Movement** 取消窗 + `ActionTransition(AnimationEnd)` 收招。

### 6.1b 配置方向闪避

1. 创建 `DirectionalActionResolver`（Create → ACT/Combat/Resolvers/Directional Action Resolver）。
2. 填入 `defaultAction`（根/后闪回退）与前/后/左/右动作，调 `sideThresholdDeg`、`rotateToInputOnForward`。
3. `PlayerActionSet` Entry：`Dodge` → 上述 `DirectionalActionResolver`。
4. 单招技能 / 切模式用 `SingleActionResolver`（Create → ACT/Combat/Resolvers/Single Action Resolver）。

### 6.2 多战斗模式

1. 创建 `CombatModeProfile`，配置 `Katana` / `Beast` 等 mode 的 `PlayerActionSet` 与 `LocomotionProfile`。
2. `CombatModeService.profile` 绑定该资产。
3. 招式需切模式时：`Start Behaviors` 勾选 `SwitchCombatMode` 并填目标 mode / policy。

### 6.3 Prefab 检查

| 组件 | 配置 |
|------|------|
| `CombatModeService` | `CombatModeProfile` |
| `ActionExecutor` | 依赖 `ActionResolverService`（仅 Cancel 解析）+ `CombatModeService`（仅 SwitchCombatMode） |
| `PlayerActionSet` Entry | 每个 `input` 必须绑定一个 `ActionResolver`（Single / Combo / Directional） |
| `InputReader` | `GameInputActions`；玩家纯 C# 输入源，离散输入由 Profile 并集自动注入 |
| `CharacterActor` | 自动注册全部 mode 的 Entry |
| `HitBoxSystem` | 纯 C# 帧消费者；`attachPoint` 来自 `CharacterConfig` 挂点名 |
| 场景受击目标 | 添加 `HurtboxTarget`，配置 `HurtboxDefinition`；`OnEnable` 自动注册到 `TargetSystem` |

### 6.4 配置 Hitbox（单招）

1. 打开 `ActionDefinition` → **Hitboxes** 列表添加 `HitboxKeyframe`（`startFrame` / `endFrame` / 局部 offset / size）。
2. 使用自定义 Inspector（`ActionDefinitionHitboxEditor`）Scrub 预览帧、在 Scene 视图拖拽 Handles 调形状。
3. Preview Character 拖入带 Animator 的角色根；编辑器默认用 Preview Character 根节点对齐预览。

---

## 7. 与 ACTION_EDITOR 的对齐分析

> **阅读方式：** ✅ 已对齐 · 🟡 部分对齐 / 有偏差 · ⬜ 未实现 · 🔀 项目扩展（编辑器文档未覆盖）

### 7.1 总体结论

| 维度 | 评估 | 说明 |
|------|------|------|
| **技术路线** | ✅ 一致 | SO 帧表 + `ActionExecutor` + 自研 Editor（路线 A） |
| **核心单招 Schema** | 🟡 约 55% | 基础字段 + Cancel/Transition + HitboxKeyframe 已有；Phase/Event 缺失 |
| **连招编排** | 🟡 有偏差 | 线性 `ComboActionResolver` 代替 `ActionGraph` / Cancel 内 `targetActionId` |
| **运行时 Tick** | 🟡 有偏差 | 无统一 `UpdateFrame`；编辑器预览需补入口 |
| **输入与取消语义** | ✅ 基本一致 | Action/Movement 取消、priority、缓冲消费 |
| **编辑器 UI 适配** | ⬜ 未开始 | 数据结构可部分复用；需补轨道类型与校验 |

**结论：** 当前架构**方向正确**，已为实现动作编辑器打好「单招 + 取消窗 + 过渡 + 执行器」主干；**连招与战斗模式**做了 Demo 期折中，编辑器落地时需明确是**延续折中**还是**回迁到 ACTION_EDITOR 完整模型**。

### 7.2 模块对照表

| ACTION_EDITOR 概念 | 当前实现 | 对齐度 | 编辑器适配备注 |
|--------------------|----------|--------|----------------|
| `ActionDefinition` | `ActionDefinition.cs` | 🟡 | 已有 `HitboxKeyframe[]`；缺 `tags`, `ActionPhase[]`, `ActionEvent[]`, `damageWeight` |
| `CancelWindow` | `CancelWindow.cs` | 🟡 | 有帧区间/type/priority/inputs；**无 `targetActionId`**，改由 ComboSequence 解析 |
| `ActionTransition` | `ActionTransition.cs` | 🟡 | 有 `AnimationEnd`；新增 `AtFrame`（编辑器文档未列）；缺 OnHit/OnWhiff/OnBlocked |
| `ActionGraph` | `ComboActionResolver` | 🔀 偏差 | 线性队列 vs 节点图；可作为新的 `ActionResolver` 子类接入，不破坏分层 |
| `CharacterCombatProfile` | `CombatModeProfile` + `PlayerActionSet` | 🔀 扩展 | 多模式武器切换；编辑器需否纳入「角色战斗根配置」待定义 |
| `ActionExecutor` | 已实现 | ✅ | 编辑器预览应共用同一套 Cancel/Transition 解析 |
| `UpdateFrame(frameIndex)` | 未实现 | ⬜ | **编辑器 Phase C 阻塞项**：预览与 Play Mode 须统一 |
| `ActionPhase` | 未实现 | ⬜ | 时间轴 Phases 轨道无数据源 |
| `HitboxKeyframe` | `HitboxKeyframe.cs` + `HitBoxSystem` | 🟡 | 运行时 OBB 已通；编辑器时间轴轨道与校验待建 |
| `HurtboxKeyframe`（动画驱动） | `HurtboxDefinition` + `HurtboxTarget` | 🟡 | 静态局部 Box；无逐帧 Hurtbox 轨道 |
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
| Cancel 无 `targetAction` | 选招交给 Resolver 简化配置 | 编辑器 Cancels 轨道不能只编辑「边到目标招」；需联动 `ActionResolver` 或 Graph | M5 Inspector 显示「下一招 = Resolver 进位」；M7 评估恢复 `targetActionId` 或 Graph 边 |
| `ComboActionResolver` 代替 `ActionGraph` | Demo 三连招够用 | 无法表达分支连招（挥空、多输入树） | 保留线性 Resolver 作「线性模板」；新增 Graph 类 Resolver 作高级层 |
| `AtFrame` Transition | 项目新增，支持中段自动切招 | ACTION_EDITOR 需补充枚举 | 更新 ACTION_EDITOR 变更日志 |
| `CombatModeProfile` | 多武器 ACT 需求 | 编辑器角色配置需增加 mode 维度 | 纳入 `CharacterCombatProfile` 设计或单列「模式」面板 |
| 无 `UpdateFrame` | 实现成本低 | **预览与运行时易不一致** | 编辑器开发前优先重构 `ActionExecutor.Tick` |

### 7.5 动作编辑器插件适配度评估

按 ACTION_EDITOR 分期评估当前代码对插件的承载能力：

| 插件阶段 | 目标 | 当前适配度 | 缺口 |
|----------|------|------------|------|
| **Phase A（数据层）** | Schema + Runtime 读 SO | **80%** | HitboxKeyframe 已定义；Phase/Event 未定义；`UpdateFrame` API 缺失 |
| **Phase B（基础 Editor）** | 列表 + Inspector + 热重载 | **55%** | 无 `ActionEditorWindow`；ComboSequence 需独立编辑流；无校验器 |
| **Phase C（时间轴）** | Frameline 多轨道 + Scrub | **35%** | Hitbox 有 Inspector 预览；无 Phase/Event；无统一 `UpdateFrame` |
| **Phase D（连招图）** | ActionGraph GraphView | **15%** | 仅线性 Sequence；与 Graph 模型不兼容 |
| **Phase E（运行时调试）** | Play Mode Overlay | **40%** | 有 `CurrentAction`/帧换算基础；无 Overlay / diff |

**优先补全项（编辑器开发前）：**

1. **`UpdateFrame(int frameIndex)`** — `ActionExecutor` 统一入口；编辑器 Scrub 与 Play Mode 共用。
2. **`ActionPhase` / `ActionEvent` 类型** — Hitbox 骨架已有；补 Phase/Event 与统一 Tick。
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

**原则：** 编辑器只增 **序列化字段与校验**，不改 `ActionExecutor` 对外职责；新条件/事件用**子类或枚举扩展**（与 ACTION_EDITOR §2.3 一致）。

---

## 8. 动作系统与碰撞系统：耦合分析

### 8.1 总体结论

**当前两系统之间不属于高耦合。** 采用「执行器发布只读状态 + 碰撞系统拉取采样 + 受击方接口回调」的单向数据流；`ActionExecutor` **零引用** `HitBoxSystem` / `IHurtboxTarget`，职责边界清晰。

| 维度 | 评估 | 说明 |
|------|------|------|
| 调用方向 | ✅ 单向 | 碰撞 → 动作（只读）；动作不回调碰撞 |
| 接口边界 | ✅ 良好 | `IActionExecutor` 暴露帧状态；`IHurtboxTarget` 消费命中 |
| 数据共享 | 🟡 可接受 | `ActionDefinition` / `HitboxKeyframe` 为共享 SO 类型（数据驱动，非运行时环依赖） |
| 装配方式 | ✅ 良好 | `HitBoxSystem` 是纯 C# 帧消费者，由 `CharacterActorFactory` 注册到 `ActionExecutor` |
| 帧同步 | ✅ 显式 | `ActionExecutor.DispatchCombatFrame` 同步派发，无 `LateUpdate` 顺序约定 |
| 战斗反馈闭环 | 🟡 部分 | 命中通过 `IActionHitReceiver.NotifyHit` 回流 `ActionExecutor`，伤害/Hit 状态未实现 |

### 8.2 解耦做得好的地方

1. **动作执行器只知道帧消费者接口** — `ActionExecutor` 只推进 `elapsed`、Cancel、Transition，并向 `ICombatFrameConsumer` 派发帧上下文。
2. **显式帧上下文** — `HitBoxSystem` 通过 `CombatFrameContext` 读取 `Action` / `FrameIndex`，不调用 `Tick` / `Stop`。
3. **受击侧可替换** — 攻击逻辑不硬编码 `HurtboxTarget`；任意 `IHurtboxTarget` 实现（敌人、可破坏物）均可注册。
4. **纯函数几何层** — `HitboxMath` / `HitboxOrientedBox` 与 MonoBehaviour 生命周期无关，可单测。
5. **命中上下文值类型** — `ActionHitContext` 为 `readonly struct`，无共享可变状态。

### 8.3 现存耦合点与风险

| 耦合点 | 严重程度 | 影响 | 缓解方向 |
|--------|----------|------|----------|
| `ActionHitContext` 携带 `ActionDefinition` + `HitboxKeyframe` | 低 | 受击逻辑与招式 SO 类型耦合 | 后续可增 `IHitSnapshot`（仅 id、伤害倍率、击退向量） |
| 旧静态目标注册表 | 已处理 | 已迁移为 `TargetSystem` | 后续可增加空间分区 |
| Hitbox/VFX 仍走旧数组 | 中 | `ActionEvent` 虽已派发，但 `hitboxes[]` / `vfxEvents[]` 尚未统一到事件轨道 | 迁移到 `ActionEventKind.SpawnHitbox` / `PlayVfx` 消费者 |

### 8.4 与理想分层的对照

```
[数据层]  ActionDefinition.hitboxes[]     ← SO，两系统共读，合理
[执行层]  ActionExecutor                   ← 不依赖碰撞
[判定层]  HitBoxSystem + HitDetectionSystem + HitboxMath
                                           ← 依赖 CombatFrameContext + SO，不依赖 PlayerController
[受击层]  IHurtboxTarget 实现              ← 依赖 ActionHitContext，不依赖 ActionExecutor
```

当前分层符合「执行 / 判定 / 受击」三分，**没有出现**「动作系统内嵌 Physics.Overlap」或「碰撞系统直接 `TransitionTo`」等双向强耦合反模式。

### 8.5 演进建议（保持低耦合前提下）

1. **P0 — 事件轨道统一** — 将 `hitboxes[]` / `vfxEvents[]` 迁移到 `ActionEvent` 消费者，减少双轨维护。
2. **P1 — 场景级目标系统** — 已由 `TargetSystem` 替代旧静态注册入口，后续可接空间分区。
3. **P2 — 受击上下文瘦身** — 伤害结算层只读 `HitSnapshot`，不直接持有完整 `ActionDefinition` 引用。

---

## 9. 接口摘要

### IActionExecutor

```csharp
bool IsPlaying { get; }
ActionDefinition CurrentAction { get; }
float ElapsedSeconds { get; }
int CurrentFrame { get; }
bool CanCancelByMovement { get; }
bool CanRotateByInput { get; }
event Action<CombatFrameContext> FrameAdvanced;
bool TryStart(ActionDefinition action);           // 只播放已解析好的招
void BindInputBuffer(IActionInputBuffer inputBuffer);
void BindActionStartContext(IActionStartContext startContext);
void Tick(float deltaTime);
void UpdateFrame(int frameIndex);
void Stop();
```

### IHurtboxTarget

```csharp
int TargetInstanceId { get; }
HitboxOrientedBox GetWorldHurtbox();
void OnHit(in ActionHitContext context);
```

### IActionInputBuffer

```csharp
bool HasBuffer(string inputId);
bool TryConsumeBuffer(string inputId);
```

### ActionResolver / ActionResolverService

```csharp
// 策略基类（ScriptableObject）
abstract bool ActionResolver.TryResolve(in ActionRequest request, in ActionResolveContext context, out ActionDefinition action);

// 服务（纯 C#）
bool ActionResolverService.TryResolveStart(in ActionRequest request, in ActionResolveContext context, out ActionDefinition action);
bool ActionResolverService.TryResolveNext(in ActionRequest request, in ActionResolveContext context, out ActionDefinition action);
IEnumerable<string> ActionResolverService.EnumerateActiveInputIds();
```

### ICombatModeService

```csharp
CombatModeType CurrentMode { get; }
bool TrySetMode(CombatModeType mode, CombatModeSwitchPolicy policy);
void ApplyPendingModeIfReady();
event Action<CombatModeType, CombatModeType> ModeChanged;
```

---

## 10. 已知限制

| 限制 | 说明 |
|------|------|
| 碰撞仅 OBB 骨架 | 有重叠检测、`OnHit` 与命中回流；无伤害、击退、无敌帧、`Hit` 状态 |
| Hitbox/VFX 双轨 | `ActionEvent` 已派发，但 Hitbox/VFX 仍通过旧数组消费 |
| 受击框静态 | `HurtboxDefinition` 无逐帧动画驱动 |
| 连招仅线性 | 无分支、挥空、多输入树 |
| Transition 条件仍少 | 已有 AnimationEnd / AtFrame / OnHitConfirm / OnWhiff，缺更丰富条件组合 |
| 敌人未接入 | 纯 C# runtime 可复用，输入源和 AI 驱动需替换 |

---

## 11. 相关文件

### 脚本

```
Assets/Scripts/App/
  Controllers/Gameplay/PlayerController.cs
  Controllers/Combat/HurtboxTarget.cs, CombatWorldController.cs, FeedbackController.cs, HitStopController.cs
  Controllers/Camera/CameraManager.cs, CameraShakeController.cs
  Architecture/ACTGameArchitecture.cs, Architecture/Contracts/IArchitecture*.cs
  Commands/Combat/ApplyHitCommand.cs
  Events/Combat/AttackHitEvent.cs, HitStopBeganEvent.cs, HitStopEndedEvent.cs
  Systems/Combat/CombatActorSystem.cs, TargetSystem.cs, CombatFeedbackSystem.cs

Assets/Scripts/Domain/
  Character/CharacterConfig.cs, CharacterActor.cs, CharacterActorFactory.cs, CharacterMotor.cs
  Character/Animation/CharacterRootMotionDriver.cs, CharacterAnimationService.cs
  Character/StateMachine/CharacterStateMachine.cs, CharacterContext.cs
  Character/StateMachine/States/ActionState.cs, States/LocomotionState.cs
  Combat/CombatModeService.cs, ICombatModeService.cs, CombatModeSwitchResult.cs, CombatModeProfile.cs
  Combat/Actions/Definitions/ActionDefinition.cs, ActionPhase.cs, ActionPhaseKind.cs
  Combat/Actions/Definitions/ActionEvent.cs, ActionEventKind.cs, ActionEventContext.cs
  Combat/Actions/Definitions/ActionTransition.cs, ActionTransitionCondition.cs
  Combat/Actions/Definitions/CancelWindow.cs, CancelType.cs, RotationWindow.cs, CombatActionType.cs
  Combat/Actions/Resolution/PlayerActionSet.cs, IMoveIntentResolver.cs
  Combat/Actions/Resolution/ActionRequest.cs, ActionInputTrigger.cs, ActionResolveContext.cs
  Combat/Actions/Resolution/ActionResolver.cs, SingleActionResolver.cs, ComboActionResolver.cs, ComboLeafPolicy.cs, DirectionalActionResolver.cs
  Combat/Actions/Resolution/ActionResolverService.cs
  Combat/Actions/Execution/ActionExecutor.cs, IActionExecutor.cs, ActionSession.cs
  Combat/Actions/Execution/CharacterActionDriver.cs, ActionRotationDriver.cs
  Combat/Actions/Execution/IActionStartContext.cs, IActionHitReceiver.cs
  Combat/Actions/Frames/CombatFrameContext.cs, ICombatFrameConsumer.cs
  Combat/Hitbox/HitBoxSystem.cs, HitboxKeyframe.cs, HitboxMath.cs
  Combat/Hitbox/HurtboxDefinition.cs, IHurtboxTarget.cs, ActionHitContext.cs, HitboxGizmoDrawing.cs
  Combat/Targeting/TargetingSystem.cs, TargetSelector.cs, CombatTargetLock.cs
  Combat/VFX/ActionVfxPlayer.cs, ActionVfxSpawner.cs, VFXManager.cs
  Input/InputManager.cs, PlayerInputFrame.cs, IActionInputBuffer.cs, InputIds.cs, ICharacterInputSource.cs

Assets/Scripts/Infrastructure/
  Input/InputReader.cs, AIInputSource.cs

Assets/Scripts/Editor/Combat/
  ActionDefinitionHitboxEditor.cs, HitboxSceneDrawing.cs
```

### 资产（Editor 维护）

```
Assets/Data/Combat/Actions/Player/
  player_attack_*.asset, player_dodge_*.asset
  PlayerActionSet.asset, Resolvers/*.asset（Single / Combo / Directional ActionResolver）
  CombatModeProfile.asset（若已建）
```

> **Resolver 重构后的 Editor 资产迁移（必须在 Unity Editor 中人工完成）：**
> 1. 旧 `ActionComboSequence.asset` 已废弃：改用 `Create → ACT/Combat/Resolvers/Combo Action Resolver`，把原 `steps` / `leafPolicy` 填入。
> 2. 旧 `PlayerActionSet` 的每个 `Entry` 现在只有 `input` + `resolver` 两个字段：为每个输入绑定对应的 Resolver（Attack→Combo、Dodge→Directional、单招→Single）。
> 3. 旧 `dodgeDirectionVariants` 字段与 `DodgeDirectionVariants` 已删除：改用 `Create → ACT/Combat/Resolvers/Directional Action Resolver`，把根/前/后/左/右动作与阈值填入，并让 Dodge Entry 的 `resolver` 指向它。
> 4. 迁移期旧资产会出现缺字段/丢引用（预期结果，不做旧资产兼容），需一次性重配并保存。

---

## 12. 变更日志

| 日期 | 变更 |
|------|------|
| 2026-06-17 | 初版与多轮迭代（InputManager、Root Motion、CancelWindow） |
| 2026-06-17 | **全面重写**：`ActionComboSequence`、`CombatModeProfile`、Transition `AtFrame`、对齐 ACTION_EDITOR 分析、编辑器适配度评估 |
| 2026-06-21 | ActionEditor 准备：CharacterActionDriver、UpdateFrame、ICombatFrameConsumer、Phase/Event 骨架、命中回流 |
| 2026-07-05 | **Resolver 重构**：新增 `ActionResolver`（Single/Combo/Directional）+ `ActionResolverService`；起手/连段/Dodge 方向/ Cancel 解析全部走 Resolver；删除 `ActionExecutor.TryStartByInput`、Dodge 特判、`ActionComboSequence`、`DodgeDirectionVariants`、`PlayerActionSet.TryGetStartAction/TryResolveNext/TryGetDodgeDirectionVariants`；`IActionComboInput` 改名 `IActionInputBuffer`；`Actions` 目录按 Definitions/Resolution/Execution/Frames 分层；`CombatModeProfile` 移至 Combat 层根目录 |
