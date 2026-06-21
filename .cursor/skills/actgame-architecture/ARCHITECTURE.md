# ACTGame 架构文档

> Last audited: 2026-06-21

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
│   ├── Player/                # PlayerController + 纯 C# PlayerCharacterRuntime
│   ├── Enemy/                 # （占位）
│   ├── Combat/
│   │   ├── Actions/           # ActionDefinition、ActionRuntimeController、CharacterActionDriver（纯 C#）
│   │   ├── Hitbox/            # OBB 判定
│   │   ├── VFX/               # 招式 VFX 帧事件
│   │   ├── Targeting/         # 索敌
│   │   └── Feedback/          # 命中反馈、卡肉
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
| `LocomotionState` | 根据 MoveInputMagnitude 选择 Idle/Walk/Run 动画 |
| `ActionState` | 薄层：Tick `IActionRuntime`，结束回 Locomotion |
| `CharacterConfig` | 角色装配根配置：模型、输入、动画、移动、战斗模式 |
| `PlayerCharacterRuntime` | 单角色纯 C# runtime：输入、Motor、状态机、动作、旋转 |

**数据流（玩家）**：

```
CharacterConfig → PlayerController（Empty 根创建 PlayerCharacterRuntime）
                    ↓
InputReader（纯 C#）→ PlayerCharacterRuntime（InputManager + Motor + 状态机）
                    ↓
              CharacterActionDriver（起手 / 缓冲 / 移动取消）
                    ↓
              CharacterStateMachine → ActionState.Tick → ActionRuntimeController
                    ↓ UpdateFrame（Logic Tick）
              HitBoxSystem / ActionVfxPlayer（ICombatFrameConsumer）
```

### 3. 动作系统（Combat/Actions）

| 类 | 职责 |
|----|------|
| `ActionDefinition` | 单招 SO：动画、Cancel、Transition、Hitbox、Phase/Event 骨架 |
| `ActionRuntimeController` | 播放、Cancel、Transition、**UpdateFrame Logic Tick**、命中回流 |
| `ActionSession` | 当前招式唯一会话状态：CurrentAction、Elapsed、命中确认、卡肉暂停 |
| `CharacterActionDriver` | 角色无关：离散输入路由、起手切状态、移动取消 |
| `ActionRotationDriver` | RotationWindow + 索敌转向 |
| `CombatModeController` | 战斗模式、出招表、Locomotion Profile 切换 |
| `CombatWorldSystem` | 场景级战斗系统生命周期锚点 |
| `TargetRegistry` / `HitDetectionSystem` / `TargetingSystem` | 目标注册、命中检测、索敌查询集中入口 |
| `PlayerActionSet` / `ActionComboSequence` | 起手映射与线性连招 |

**Logic Tick 原则**：编辑器 Scrub 与 Play Mode 共用 `ActionRuntimeController.UpdateFrame(frameIndex)`；帧消费者实现 `ICombatFrameConsumer`。

### 4. 玩家（Player）

| 类 | 职责 |
|----|------|
| `PlayerController` | Scene Empty 上唯一玩家脚本；创建/启停 `PlayerCharacterRuntime` |
| `PlayerCharacterRuntime` | 位移、重力、输入采集、状态机 Tick、动作旋转 |

**注意**：`PlayerController` 现在是 Scene 空物体上的装配入口；通过 `CharacterConfig` 生成模型与纯 C# runtime。Player 根对象运行时只保留 `PlayerController` + `CharacterController`，不再挂载业务脚本。

### 5. 动画（Character/Animation）

| 类 | 职责 |
|----|------|
| `AnimationKey` | 逻辑动画键枚举 |
| `CharacterAnimationProfile` | AnimationKey → Animator 状态名映射 |
| `CharacterAnimationController` | 纯 C# Animator 封装：CrossFade 播放 Locomotion；招式 `PlayClip` |

### 6. 输入（Input）

| 类 | 职责 |
|----|------|
| `InputManager` | 帧快照、离散缓冲、移动意图 |
| `InputReader` | 纯 C# 输入源：绑定 GameInputActions |

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
| 新招式帧事件 | `ActionEvent` + `ICombatFrameConsumer` 或扩展 `ActionRuntimeController` |
| 编辑器 Scrub | `ActionRuntimeController.UpdateFrame` |
| OnHit 收招 | `ActionTransitionCondition.OnHitConfirm` + `IActionHitReceiver` |
| 敌人 AI 出招 | `CharacterActionDriver` + AI 输入源替换 `InputManager` |
| 配置数据 | `Assets/Data/` ScriptableObject |
