# ACTGame 设计方向与重构路线图

> 优先级：P0 阻塞体验 → P1 架构健康 → P2 扩展预备

## 设计原则（长期）

1. **状态机驱动角色表现**：动画、动作阶段、可取消窗口由 State 负责
2. **Controller 只做 Scene 入口**：PlayerController 通过 CharacterConfig 创建纯 C# `CharacterActor`；业务不再挂载到 Player 根对象
3. **Combat 与 Character 解耦**：Hitbox 拉取 `IActionExecutor`；Character State 只 Tick Executor
4. **QFramework 风格跨系统通信**：跨系统使用 `ACTGameArchitecture`、System、Command、Query、Event；进入 IOC 的对象必须实现对应契约或基类，动作帧内部保留强时序直连
4. **Logic Tick = 编辑器帧**：`UpdateFrame` 统一 Play Mode 与 ActionEditor Scrub
5. **数据驱动**：数值、动画映射、技能表进 ScriptableObject（Assets/Data/）
6. **小步可验证**：每步可在 Play Mode 单独验证移动/动画/战斗

## 进行中的结构迁移

### [P1] 移动职责迁移

**现状**：`LocomotionState.Tick` 调用 `CharacterMotor.TickLocomotion` 执行水平位移并选择动画 key；`CharacterActor` 只保留输入、动作路由、重力和状态机调度。

**目标**：LocomotionState（或 Motor 服务类）成为移动决策点；Controller 降为 Motor 执行层。

**状态**：已完成（2026-06-21）

### [P1] Locomotion Phase / FootCycle ✅ 2026-07-18

**已完成**：

- `LocomotionService` 内嵌 Phase：Idle → Start → Gait(Walk/Run/Sprint) →（Sprint 大角度）PivotTurn / Stop
- Run 持续 `sprintAfterRunSeconds`（默认 3s）后进 Sprint；仅 Sprint 可 Pivot
- `LocomotionFootCycle` + SO 落脚标记；急停默认右脚；Stop 全程可取消回 Start；Pivot→Stop 用转身目标朝向
- 首版不做急停减速 / 转身专用位移（见 `docs/LOCOMOTION_OPTIMIZATION_PLAN.md` Phase D）

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
- [ ] `ActionDefinition` 子 SO 拆分（CombatData / PresentationData，可选）

### [P1] 战斗闭环

**现状**：Hitbox OBB + 命中反馈（震屏/卡肉）；`OnHitConfirm` Transition 已可配置。

**待做**：伤害结算、`Hit` 状态、受击 `ActionDefinition` 衔接。

**状态**：部分完成

## 待建设模块

| 模块 | 优先级 | 说明 |
|------|--------|------|
| ActionEditorWindow | P1 | ✅ 2026-07-10 基础版已落地；后续增强 FramePlayer / SFX 预览 |
| Enemy/ + AI | P2 | 复用纯 C# `CharacterActionDriver` + `ActionExecutor` |
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
- [ ] 无 asmdef，全项目单一 Assembly-CSharp

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
- [x] 2026-07-14：ActionGraph P0——`ActionDefinition.Trigger`、`ActionGraph`、图游标、Cancel 槽边路由、移除 `allowedInputs`、编辑器（见 `docs/ACTION_GRAPH_DESIGN.md`）
- [x] 2026-07-14：ActionGraph 多入口——删除 `GraphActionResolver` 与 `ActionEntry` 输入表；`PlayerActionSet` 直接绑 Graph；Entry×Trigger 同时支持攻击/闪避起手

## 剩余项

- [ ] 输入生命周期扩展：`ActionInputTrigger.Held / Released` 缓冲匹配、`HoldActionResolver`（枚举已预留；见 ActionGraph P2）
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
