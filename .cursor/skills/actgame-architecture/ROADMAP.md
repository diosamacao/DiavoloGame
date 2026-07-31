# ACTGame 设计方向与重构路线图

> 优先级：P0 阻塞体验 → P1 架构健康 → P2 扩展预备

## 设计原则（长期）

1. **状态机驱动角色表现**：动画、动作阶段、可取消窗口由 State 负责
2. **Controller 只做 Scene 入口**：PlayerController 通过 CharacterConfig 创建纯 C# `CharacterActor`；业务不再挂载到 Player 根对象
3. **Combat 与 Character 解耦**：Hitbox 拉取 `IActionExecutor`；Character State 只 Tick Executor
4. **QFramework 风格跨系统通信**：跨系统使用 `ACTGameArchitecture`、System、Command、Query、Event；进入 IOC 的对象必须实现对应契约或基类，动作帧内部保留强时序直连
4. **单一固定逻辑时钟**：`SimulationHost` 统一驱动 Runtime；ActionEditor 与 Runtime 的纯帧查询在 L1B 收敛
5. **数据驱动**：数值、动画映射、技能表进 ScriptableObject（Assets/Data/）
6. **小步可验证**：每步可在 Play Mode 单独验证移动/动画/战斗

## 进行中的结构迁移

### [P1] 移动职责迁移

**现状**：`LocomotionState.Tick` 调用 `CharacterMotor.TickLocomotion` 执行水平位移并选择动画 key；`CharacterActor` 只保留输入、动作路由、重力和状态机调度。

**目标**：LocomotionState（或 Motor 服务类）成为移动决策点；Controller 降为 Motor 执行层。

**状态**：已完成（2026-06-21）

### [P1] Locomotion Phase / FootCycle ✅ 2026-07-18

**已完成**：

- `LocomotionStateMachine` 内层纯状态机：Idle → Start → Gait(Walk/Run/Sprint) →（Sprint 大角度）PivotTurn / Stop
- Run 持续 `sprintAfterRunSeconds`（默认 3s）后进 Sprint；仅 Sprint 可 Pivot
- `LocomotionFootCycle` + SO 落脚标记；急停默认右脚；Stop 全程可取消回 Start；Pivot→Stop 用转身目标朝向；Start 急停播 StartEnd
- 首版不做急停减速 / 转身专用位移（见 `docs/LOCOMOTION_OPTIMIZATION_PLAN.md` Phase D）
- [x] 2026-07-22：删除手写 `LocomotionService` Phase 袋，改为 Core `StateMachine` 嵌套机

**待做（Phase D）**：减速曲线、Pivot 位移手感、敲定 Profile 挂载与落脚编辑器工具

### [P1] ActionEditor 准备 — 动作系统职责收敛 ✅ 2026-06-21

**已完成**：

- `CharacterActionDriver`：离散输入起手/缓冲、移动取消（纯 C#，角色无关）
- `ActionRotationDriver`：`RotationNotifyState` + 索敌（纯 C#）
- `ActionExecutor.UpdateFrame` + `ICombatFrameConsumer` + `ActionTimelineRunner`（纯 C# Hitbox/VFX 统一 Logic Tick）
- `ActionTimeline` / `ActionNotify` / `ActionNotifyState` 写入 `ActionDefinition`
- `IActionHitReceiver` 命中回流 + `OnHitConfirm` / `OnWhiff` Transition
- `IActionExecutor` 位于 `Combat/Actions/`

**下一步（ActionEditor M5 前）**：

- [x] 2026-07-10：`ActionEditorWindow` 基础版（列表 + Scrub + 手动加轨/窗口拖拽）
- [x] 2026-07-09：ActionNotify 时间轴入口：Hitbox/VFX/Cancel/Movement/Rotation 收敛到 `ActionTimeline`
- [x] 2026-07-10：VFX/SFX 区间窗口 + 播放倍率语义（已于 2026-07-13 改回点事件 + 显式 `playbackSpeed`）
- [x] 2026-07-13：VFX/SFX 点事件 + `attachPointId` + 显式 `playbackSpeed`；`CharacterAttachPointResolver`；`ActionSfxPlayer`
- [x] 2026-07-25：ActionGraph 稀疏路由——SharedRoute、Recovery Phase→Entry、Directional 共用逻辑节点；删除 Recovery Cancel 与 ComboResolver 旧路径
- [x] 2026-07-26：ActionGraph 独立双窗口——Normal 必需、Perfect 可选；重叠同 Trigger 时 Perfect 优先
- [x] 2026-07-25：Phase 帧数据迁入 ActionTimeline；Action Editor 开放 Phase 轨；Recovery 窗口集成移动取消与 Entry 重开
- [x] 2026-07-25：Action Editor 手动轨道支持轨头拖拽排序与 Undo
- [x] 2026-07-25：新增 DodgeAttack 上下文意图——闪避 Action 中 Attack Pressed 映射为闪避攻击
- [x] 2026-07-29：`ActionDefinition` 职责收敛——只保留播放内容、Timeline 与 ExecutionPolicy；输入/流程/索敌迁到 Graph，伤害/反馈迁到 HitPayload
- [x] 2026-07-30：玩家/敌人反应事件统一由 `CharacterReactionService` 桥接；默认硬直时长收敛到 `CharacterReactionSet`
- [x] 2026-07-30：Graph Editor 支持完整节点内联策略编辑；命中去重收敛为每个 Hitbox 窗口×目标一次；连续受击强制重入 HitState

### [P1] 战斗闭环

**现状**：Hitbox OBB + Payload 命中反馈；Graph 节点自动衔接、生命值、Controller 反应解析与通用 Hit/Death 状态已接通。

**待做**：在 Editor 为玩家/敌人配置受击与死亡 Action；后续扩展抗性、护盾与 UI。

**状态**：代码闭环完成（2026-07-29），资产待绑

### [P1] Lockstep 模拟核迁移

**方案**：`docs/ACTION_SYSTEM_LOCKSTEP_REFACTOR_PLAN.md`

**已完成：**

- [x] 2026-07-31 L0A：`SimulationHost` + 60Hz accumulator + `SimulationWorld`
- [x] 2026-07-31 L0A：玩家/敌人删除 Controller 分散 Tick，统一实现 `ISimulationActor.Step`
- [x] 2026-07-31 L0A：单调 `SimActorId`、稳定 Actor 顺序、OnEnable/OnDisable 对称注册
- [x] 2026-07-31 L0A：渲染帧输入边沿汇聚，避免高 FPS 无逻辑 Step 时漏输入
- [x] 2026-07-31 L0A：模型/相机前后 Pose 插值；旋转阻尼改用显式 fixed delta
- [x] 2026-07-31 L0A：新增无 Unity 引用的 `ACTGame.Simulation` asmdef 与 EditMode 测试

**下一步：**

- [ ] L0B：量化 `InputFrame`、玩家/AI/回放统一输入边界、Intent/Buffer 帧计时
- [ ] L0C：HitEvent 延迟收集、稳定排序与帧末结算
- [ ] L1：Action 整数帧权威与逻辑/表现拆分

## 待建设模块

| 模块 | 优先级 | 说明 |
|------|--------|------|
| ActionEditorWindow | P1 | ✅ 2026-07-10 基础版已落地；后续增强 FramePlayer / SFX 预览 |
| Enemy/ + AI | P2 | 🟡 2026-07-29 代码已实现；EnemyDefinition、Graph、动画资产待配置 |
| UI/ | P2 | HUD、血条 |
| 事件总线 | P2 | 轻量 C# event；定稿前不引入第三方 |

## Tech Debt 观察清单

- [ ] `CharacterActor` 与 `LocomotionState` 双处感知移动输入
- [x] 2026-06-21：Prefab/运行时堆业务脚本改为 `CharacterConfig` + `PlayerController` + 纯 C# 角色实例（2026-06-23 命名为 `CharacterActor`）
- [x] 2026-06-23：命名迁移为 `CharacterActor` / `ActionExecutor`，新增 `ACTGameArchitecture`、`TargetSystem`、`CombatActorSystem`
- [x] 2026-06-23：`TargetSystem` 替代静态目标注册表
- [x] 2026-06-29：QFramework 风格强类型契约落地（`ArchitectureSystemBase`、`AppControllerBase`、`ArchitectureCommandBase`、`ArchitectureQueryBase`、`IArchitectureEvent`）
- [x] 2026-06-29：Domain 命中/索敌入口移除直接 `ACTGameArchitecture.Interface` 依赖，改为目标集合注入、`GetActiveTargetsQuery` 与 App 层 Command 编排
- [x] 2026-06-29：新增 Editor 架构边界校验，检查 `System` / `Controller` / `Event` 契约和 Domain 单例访问
- [ ] 仅 `Domain/Simulation` 已拆 asmdef；其余业务仍在单一 Assembly-CSharp

## 已完成

- [x] 2026-06-17：建立 Core 泛型状态机 + Character/Player 分层
- [x] 2026-06-17：CharacterAnimationService + Profile 映射模式
- [x] 2026-06-17：InputReader + CameraManager 组件化
- [x] 2026-06-17：动作系统 Phase A（ActionRuntime、Combo、CombatMode、Hitbox 骨架）
- [x] 2026-06-21：ActionEditor 准备重构（CharacterActionDriver、UpdateFrame、Phase/Event 骨架、命中回流）
- [x] 2026-06-21：CharacterConfig 装配入口、ActionSession、目标注册 / HitDetectionSystem / TargetingSystem 骨架
- [x] 2026-06-23：命中后跨系统通信迁移为 `ApplyHitCommand` + `AttackHitEvent`
- [x] 2026-06-21：移除 Player 根对象运行时业务脚本挂载，动作/输入/状态/判定改为纯 C# runtime
- [x] 2026-06-29：`HitBoxSystem` / `HitDetectionSystem` / `TargetingSystem` 命名收敛为 `HitboxFrameConsumer` / `HitDetector` / `TargetingResolver`，`System` 后缀仅保留给架构 IOC
- [x] 2026-07-05：动作系统 Resolver 重构——新增 `ActionResolver`（Single/Combo/Directional）+ `ActionResolverService`；起手/连段/Dodge 方向/Cancel 解析全部走 Resolver；删除 `ActionExecutor.TryStartByInput` 与 Dodge 特判、`ActionComboSequence`、`DodgeDirectionVariants`；`IActionComboInput`→`IActionInputBuffer`；`Combat/Actions` 按 Definitions/Resolution/Execution/Frames 分层
- [x] 2026-07-12：动画薄层 Playable（`IAnimationPlayback` + `PlayableAnimationPlayback`）；Action/Locomotion 同切 Clip；HitStop 走门面 Speed；Animancer 可替换预留
- [x] 2026-07-14：ActionGraph P0——`ActionDefinition.Trigger`、`ActionGraph`、图游标与编辑器（现已收敛为 Normal/Perfect 独立窗口）
- [x] 2026-07-14：ActionGraph 多入口——删除 `GraphActionResolver` 与 `ActionEntry` 输入表；`PlayerActionSet` 直接绑 Graph；Entry×Trigger 同时支持攻击/闪避起手
- [x] 2026-07-19：语义化玩法意图层——物理输入经 `GameplayIntentProducer` 转为枚举 Trigger；实现 SprintAttack、PressedThenLong 与 Dodge 后 Sprint 恢复
- [x] 2026-07-19：方向闪避统一为前/后/左前/左后/右前/右后六向解析，移除 Locomotion 起手固定前闪旧路径
- [x] 2026-07-22：TurnBack 输入接管——锁根 0.08 秒后实时输入控制朝向，烘焙位移随新朝向重定向
- [x] 2026-07-22：Locomotion 内层纯状态机——`LocomotionStateMachine` + 五相位 State，删除 `LocomotionService`
- [x] 2026-07-29：敌人系统——共享 CharacterActor、五态 Brain、AI 输入、伤害/Hit/Death、Spawn/Despawn 与阵营过滤
- [x] 2026-07-29：动作职责重构——GraphNode 成为输入/流程/索敌真源，HitPayload 成为伤害/反馈真源，CharacterReactionResolver 承接受击/死亡选招
- [x] 2026-07-30：角色反应闭环去重——Resolver 生成完整请求，ReactionService 统一 Health 事件与 Actor 入口，删除 CharacterConfig/EnemyBrainProfile 硬直双真源
- [x] 2026-07-31：Lockstep L0A——场景唯一 60Hz SimulationHost、稳定 SimActorId/World、Controller Tick 单轨切换与纯 C# 测试

## 剩余项

- [x] 2026-07-19：输入生命周期扩展：Pressed / IsPressed / Released 原始帧与 HoldReached 一次触发
- [ ] ActionGraph P1–P3：校验强化、Directional 与图边再解析体验、conditions、GraphView 润色
- [ ] `ActionEditorWindow`（多轨道时间轴，基于 `ActionTimeline`）与 ActionMap 可视化编辑 — 实现方案见 `docs/ACTION_EDITOR_IMPLEMENTATION.md`

## 决策记录

| 日期 | 决策 | 理由 |
|------|------|------|
| 2026-06-17 | 不用 namespace，用文件夹分层 | 当前规模小，减少样板 |
| 2026-06-17 | CharacterController 非 Rigidbody | ACT 地面移动更可控 |
| 2026-06-17 | 状态机 Core 不引用 UnityEngine | 可测试性与分层清晰 |
| 2026-06-21 | 连招保持线性 | 近期无分支图需求 |
| 2026-07-12 | 自研薄 Playable + `IAnimationPlayback`；不同时引入 Animancer | Action 时序已自研；门面可替换后端 |
| 2026-06-21 | 输入路由命名 `CharacterActionDriver` | 敌人复用同一组件 |
| 2026-06-29 | 架构层采用能力接口 + 基类约束 | 让 Controller/System/Command/Query/Event 职责在类型系统中表达 |
