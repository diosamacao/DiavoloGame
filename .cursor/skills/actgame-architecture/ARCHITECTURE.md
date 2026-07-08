# ACTGame 架构文档

> Last audited: 2026-07-09

## 项目概述

Unity ACT（动作）游戏。当前重点：第三人称移动、状态机驱动动画、Cinemachine 相机、**数据驱动动作系统（ActionEditor 准备中）**。

> 各功能的实现细节、参数与运行时流程见 [TECHNICAL.md](TECHNICAL.md)。

## 目录结构

```
Assets/
├── Scripts/
│   ├── Core/StateMachine/     # 泛型状态机（与角色无关）
│   ├── Character/
│   │   ├── Animation/         # 动画播放与 Profile
│   │   └── StateMachine/      # 角色状态机基类与共享 State
│   ├── Player/                # PlayerController（玩家输入源适配）
│   ├── Enemy/                 # （占位）
│   ├── Combat/
│   │   ├── Actions/           # Definitions(数据) / Resolution(选招) / Execution(播放) / Frames(帧契约)（纯 C#）
│   │   ├── Hitbox/            # OBB 判定
│   │   ├── VFX/               # 招式 VFX 帧事件
│   │   ├── Targeting/         # 索敌
│   │   └── Feedback/          # 命中反馈、卡肉
│   ├── App/
│   │   ├── Architecture/      # QFramework 风格强类型 Architecture / 能力接口 / 基类
│   │   ├── Controllers/       # Unity 表现入口，继承 AppControllerBase
│   │   ├── Systems/           # 注册到 Architecture IOC 的业务系统
│   │   ├── Commands/          # 跨系统业务行为
│   │   ├── Queries/           # 无副作用读取请求
│   │   └── Events/            # IArchitectureEvent 事件
│   ├── Input/                 # Input System 封装
│   ├── Camera/                # Cinemachine 第三人称相机
│   ├── UI/                    # （占位）
│   └── Editor/Combat/         # ActionDefinition 预览 Editor
├── Data/                      # ScriptableObject 配置
├── Prefabs/Player/            # 玩家 Prefab
└── Art/                       # 美术资源（不参与代码依赖）
```

## 分层依赖

```mermaid
flowchart TB
    subgraph gameplay [Gameplay Layer]
        Player
        Enemy
        Combat
        Camera
        UI
    end
    subgraph character [Character Layer]
        CharAnim[Character/Animation]
        CharSM[Character/StateMachine]
    end
    subgraph core [Core Layer]
        CoreSM[Core/StateMachine]
    end
    Input --> Player
    Player --> CharSM
    Player --> Combat
    CharSM --> CoreSM
    CharSM --> CharAnim
    Combat --> CharAnim
    Camera --> Input
    Enemy -.-> CharSM
    Enemy -.-> Combat
```

## 核心子系统

### 1. 泛型状态机（Core）

| 类 | 职责 |
|----|------|
| `IState<TStateId, TContext>` | 状态生命周期与转换守卫 |
| `StateBase<,>` | 状态基类，持有 Context |
| `StateMachine<TStateId, TContext>` | 注册、Initialize、Tick、TryChangeState |

### 2. 角色状态机（Character）

| 类 | 职责 |
|----|------|
| `CharacterStateType` | 状态枚举（Locomotion, Action, …） |
| `CharacterContext` | 运行时共享数据（Transform、Animation、Motor、ActionRuntime） |
| `CharacterStateMachine` | 纯 C# 状态机宿主：RegisterStates、Tick |
| `LocomotionState` | Tick `CharacterMotor`，再根据 MoveInputMagnitude 选择 Idle/Walk/Run 动画 |
| `ActionState` | Tick `IActionExecutor` + `ActionRotationDriver`，结束回 Locomotion |
| `CharacterConfig` | 角色装配根配置：模型、输入、动画、移动、战斗模式 |
| `CharacterMotor` | 纯 C# 移动服务：Locomotion 位移、重力、移动意图解析 |
| `CharacterActor` | 单角色纯 C# Actor：输入、Motor、状态机、动作、旋转 |
| `CharacterActorFactory` | 通过 `CharacterConfig` + `ICharacterInputSource` 创建角色实例 |

**数据流（玩家）**：

```
CharacterConfig → PlayerController（Empty 根创建玩家输入源）
                    ↓
InputReader（ICharacterInputSource）→ CharacterActor（InputManager + 重力 + 状态机）
                    ↓
              CharacterActionDriver（起手 / 缓冲 / 移动取消）
                    ↓
              CharacterStateMachine
                    ├─ LocomotionState.Tick → CharacterMotor.TickLocomotion
                    └─ ActionState.Tick → ActionExecutor + ActionRotationDriver
                    ↓ UpdateFrame（Logic Tick）
              HitboxFrameConsumer（ICombatFrameConsumer）/ ActionVfxPlayer（IActionNotifyConsumer）
```

### 3. 动作系统（Combat/Actions）

| 类 | 职责 |
|----|------|
| `ActionDefinition` | 单招 SO：动画、`ActionTimeline`、Transition、Phase、反馈默认值 |
| `ActionTimeline` / `ActionNotify` / `ActionNotifyState` | 动作帧数据唯一真源：点事件（VFX/自定义事件）与区间窗口（Hitbox/Hurtbox/Cancel/Movement/Rotation） |
| `ActionExecutor` | 纯播放器：播放、Cancel（委托 Resolver 选下一招）、Transition、**UpdateFrame Logic Tick**、统一 Timeline 派发、命中回流；不做输入查表 / 动作类型特判 |
| `ActionSession` | 当前招式唯一会话状态：CurrentAction、Elapsed、命中确认、卡肉暂停 |
| `ActionResolverService` / `ActionResolver` | 选招策略层：输入请求 + 上下文 → ActionDefinition（Single / Combo / Directional） |
| `CharacterActionDriver` | 角色无关：离散输入路由、起手（经 Resolver）切状态、移动取消 |
| `ActionRotationDriver` | `RotationNotifyState` + 索敌转向 |
| `CombatModeService` | 战斗模式、出招表、Locomotion Profile 切换 |
| `CombatWorldController` | 场景级战斗系统生命周期锚点 |
| `ACTGameArchitecture` | QFramework 风格架构入口：System/Model/Utility 注册、Command 执行、Query 查询、Event 分发 |
| `ArchitectureSystemBase` / `AppControllerBase` / `ArchitectureCommandBase` / `ArchitectureQueryBase` | 架构对象基类；通过能力接口限制谁能访问 System、发送 Command、订阅 Event |
| `CombatActorSystem` / `TargetSystem` / `CombatFeedbackSystem` | 战斗角色注册、目标注册、反馈状态 |
| `ApplyHitCommand` / `GetActiveTargetsQuery` / `AttackHitEvent` | 命中后的跨系统通信入口与无副作用目标查询 |
| `HitboxFrameConsumer` / `HitDetector` / `TargetingResolver` | 动作帧命中检测与索敌纯计算入口，不直接访问 Architecture |
| `PlayerActionSet` | 出招表：离散输入 → `ActionResolver` 映射 |

**Logic Tick 原则**：编辑器 Scrub 与 Play Mode 共用 `ActionExecutor.UpdateFrame(frameIndex)`；帧消费者实现 `ICombatFrameConsumer`，点事件/区间事件消费者实现 `IActionNotifyConsumer`。

### 4. 玩家（Player）

| 类 | 职责 |
|----|------|
| `PlayerController` | Scene Empty 上唯一玩家脚本；创建 `InputReader` 并启停 `CharacterActor` |
| `CharacterActor` | 输入采集、动作路由、重力、状态机 Tick |
| `CharacterMotor` | Locomotion 位移、相机相对方向、起手面向、移动快照 |

**注意**：`PlayerController` 现在是 Scene 空物体上的装配入口；通过 `CharacterConfig` 生成模型与纯 C# runtime。Player 根对象运行时只保留 `PlayerController` + `CharacterController`，不再挂载业务脚本。

### 5. 动画（Character/Animation）

| 类 | 职责 |
|----|------|
| `AnimationKey` | 逻辑动画键枚举 |
| `CharacterAnimationProfile` | AnimationKey → Animator 状态名映射 |
| `CharacterAnimationService` | 纯 C# Animator 封装：CrossFade 播放 Locomotion；招式 `PlayClip` |

### 6. 输入（Input）

| 类 | 职责 |
|----|------|
| `InputManager` | 帧快照、离散缓冲、移动意图 |
| `ICharacterInputSource` | 角色输入源抽象：玩家、AI、回放、网络 |
| `InputReader` | 玩家纯 C# 输入源：绑定 GameInputActions |

### 7. 相机（Camera）

| 类 | 职责 |
|----|------|
| `CameraManager` | Cinemachine 第三人称 |

## 技术栈

- Unity + **Input System**
- **CharacterController** 移动
- **Cinemachine** 虚拟相机
- **Animator** + CrossFade（Locomotion）；招式 Clip 由 `ActionDefinition` 驱动
- 无命名空间（全局类名，靠目录分层）

## 扩展点

| 需求 | 推荐接入位置 |
|------|--------------|
| 新玩家状态 | `CharacterStateType` + 新 State 类 + RegisterStates |
| 新招式帧事件 | `ActionNotify` / `ActionNotifyState` + `IActionNotifyConsumer` 或专用查询服务 |
| 编辑器 Scrub | `ActionExecutor.UpdateFrame` |
| OnHit 收招 | `ActionTransitionCondition.OnHitConfirm` + `IActionHitReceiver` |
| 敌人 AI 出招 | `CharacterActionDriver` + AI 输入源替换 `InputManager` |
| 配置数据 | `Assets/Data/` ScriptableObject |
