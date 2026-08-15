# ACTGame 设计方向与重构路线图

> 优先级：P0 阻塞体验 → P1 架构健康 → P2 扩展预备  
> **一页总清单：** [`docs/PROJECT_CHECKLIST.md`](../../docs/PROJECT_CHECKLIST.md)（进度摘要；细节仍以本文 + MASTER + TECHNICAL 为准）

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
- 急停减速曲线等 Phase D **明确不做**（2026-08-12；见 `docs/LOCOMOTION_OPTIMIZATION_PLAN.md`）
- [x] 2026-07-22：删除手写 `LocomotionService` Phase 袋，改为 Core `StateMachine` 嵌套机
- [x] 2026-08-12：L-DIR1～5 + Pivot 两段式 Play 验收关闭

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

### [P1] 战斗数值（属性 / 伤害 / EX·喧响·闪避 / Debug HUD）

**字段/产品语义**：`docs/COMBAT_NUMERICS_PLAN.md`  
**数值口袋改造真源**：`docs/2026.8.7/GAS_STYLE_COMBAT_REFACTOR_PLAN.md`（G0～G5；Wave 4 前）  
（旧 Attributes/Resource 独立稿已删除；字段以本文 + NUMERICS 为准）

**方向**：`CombatHitPipeline` 唯一结算口；数值权威为 Attribute + Effect + Flags（`NumericSystem`）。旧 ResourceSim/Health 已删。

**状态**：GAS G0～G5 + Wave 3.4 完整闭环（代码 + Editor 窗/Counter Entry，2026-08-08）  

### [P1] Wave 4 玩法位移（吸附）

**真源**：`docs/2026.8.9/WAVE4_GAMEPLAY_MOTION_BRANCH02_PLAN.md`

**状态：✅ Wave 4 位移出口关闭（2026-08-09）**

- [x] TargetAdhesion + SoftBodySuppress；Branch_02 验收（目标已于 2026-08-13 迁为逐帧 SelectedTarget）
- [x] RelocateBehind / MotionCommand → `ActionMotionResolver` 接线（P3）
- ~~Lock-On（原 4.5～4.6）~~ → 已撤出 Wave 4；排期见 `docs/2026.8.6/CAMERA_SYSTEM_PLAN.md`

**打击感优化（木桩 / Cue / 吸附行程）至此告一段落；Relocate 按需配资产。**

### [P1] Camera C1 前置 — 移动输入与目标权威收口

**方案**：`docs/2026.8.13/CAMERA_AUTHORITY_AND_TARGETING_REFACTOR_PLAN.md`

**原风险（已删除）**：Orbit Yaw 经 PlanarBasis 渲染态进入 Motor；动作私有目标绑定与 Presentation late-bind 破坏回滚边界。

**目标**：MoveReferenceYaw 固化进 `InputFrame`；角色只保存一个自动维护且 Action 中可切换的 `SelectedTargetId`，Action/Motion/Camera 共用；`CameraLockEnabled` 仅为本地表现；删除 PlanarBasis Motor 旁路、`ActionTargetId`、Transform 索敌与 Presentation late-bind。

**状态：🟡 C-AT0～C-AT3 代码重构完成（2026-08-13）**；已删除旧权威路径并补确定性 Resolver 测试。待 Editor 绑定 TargetSwitch/CameraLock、Unity 编译/Test Runner/Play 回归后关闭出口，再进入 Camera C1 Director / LockOn VCam。

### [P1] 组队 PVE · 永劫式状态同步

**方案**：`docs/2026.8.13/TEAM_PVE_NARAKA_STYLE_STATE_SYNC_PLAN.md`

**目标**：Listen Host / 日后 Dedicated 独跑现有 `SimulationWorld`；客户端上行 `InputFrame`、下行角色快照；本地预测移动与出招表现；命中只在权威逻辑盒结算。取代锁步 L5「全员输入广播 + 完整回滚」作为产品联网。

**状态：✅ NS0～NS5 已验收（2026-08-15）**。单机即 Listen Host。房间 / 权威 World 以该方案为准。命中 **P0 仍 Host Collect**；终态以 PVP 为真源（攻击方申报几何、权威入账），PVE 同一条链，见方案 §3.3。`NS-PVP` 未开，禁止现在分叉两套盒。

**下一档（客机预测对齐 UE）：** [`docs/2026.8.15/UE_ALIGNED_CLIENT_PREDICTION_PLAN.md`](../../docs/2026.8.15/UE_ALIGNED_CLIENT_PREDICTION_PLAN.md) — **UE4 代码已落地（2026-08-15）**，只读 ActionSim；已修自然结束重播延迟招 / 连招误 Cancel / 卡肉推帧。Play 复验连招 Cancel、出招结束无二段刀光、受击取消。顺序 UE1 → UE2 → UE3 → UE4。

### [P1] Lockstep 模拟核迁移

**方案**：`docs/ACTION_SYSTEM_LOCKSTEP_REFACTOR_PLAN.md`

**已完成：**

- [x] 2026-07-31 L0A：`SimulationHost` + 60Hz accumulator + `SimulationWorld`
- [x] 2026-07-31 L0A：玩家/敌人删除 Controller 分散 Tick，统一实现 `ISimulationActor.Step`
- [x] 2026-07-31 L0A：单调 `SimActorId`、稳定 Actor 顺序、OnEnable/OnDisable 对称注册
- [x] 2026-07-31 L0A：渲染帧输入边沿汇聚，避免高 FPS 无逻辑 Step 时漏输入
- [x] 2026-07-31 L0A：模型/相机前后 Pose 插值；旋转阻尼改用显式 fixed delta
- [x] 2026-07-31 L0A：新增无 Unity 引用的 `ACTGame.Simulation` asmdef 与 EditMode 测试
- [x] 2026-08-01 L0B：量化 `InputFrame` + `InputFrameBuffer`；玩家/AI/回放统一帧格式与边沿展开
- [x] 2026-08-01 L0B：World Input Produce 阶段；删除 AI 设备伪装与 string 输入路径
- [x] 2026-08-01 L0B：Hold、Action Buffer、AI 攻击/重试/刷新冷却改为整数逻辑帧
- [x] 2026-08-01 L0C：`CombatHitPipeline` Collect→`SimHitKey` 稳定排序→帧末伤害/Reaction/命中确认
- [x] 2026-08-01 L0C：`ISimulationPostCombatActor` 保持 OnHitConfirm 同帧自动衔接；生命周期在其后 Commit
- [x] 2026-08-01 L0C：删除 `ApplyHitCommand` 同步权威链、Unity InstanceId 命中身份与 Event→ActionExecutor 回写
- [x] 2026-08-01 L1A：`ActionSession.CurrentFrame` + `ActionFrameClock` 整数帧权威；窗口、Graph、段与结束判定删除秒制 Runtime 路径
- [x] 2026-08-01 L1A：`CharacterActor` 每 World 帧唯一 Action Step；Cancel/Recovery/自动衔接延迟到下一 World 帧提交
- [x] 2026-08-01 L1A：Hit/Death 改用 `DurationFrames` 与稳定动作会话结束标记，不再读取 `IsPlaying`
- [x] 2026-08-01 L1B：提取无 Unity 依赖 `ActionSim` 与 Snapshot/Event 边界，删除 ActionExecutor/Session 和 30Hz Runtime fallback
- [x] 2026-08-01 L1B：`CharacterActionPresentationBridge` 按整数帧 Seek，Runtime/Editor 共用 `ActionFrameQuery`
- [x] 2026-08-01 L1B：提供 Action 30→60Hz Editor 迁移工具；代码只接受 60Hz
- [x] 2026-08-02 L1B：全部 ActionDefinition 已为 60Hz；Validate Readiness 菜单；清除 Editor 30Hz fallback

**下一步：**

- [ ] L1B Play Mode 回归（连招 / Perfect / Recovery / Hitbox / VFX·SFX / 位移）+ Test Runner `ActionSim*`
- [x] 2026-08-02 L2/M0：双文件夹 InPlace↔RM 匹配 + `ActionBakedMotion` 烘焙写回
- [x] 2026-08-02 L2/M1：表就绪时查表位移并禁用 Animator RM（仍经 CharacterController）
- [x] 2026-08-02 L2：`ActionSim.freezeFrames` 逻辑 HitStop（Pipeline 写入；表现跟帧）
- [x] 2026-08-02 L2：Locomotion Stop/Pivot 烘焙位移整数帧索引
- [x] 2026-08-02 L2：`CharacterMotorSim` 水平权威（空场地碰撞）；CC 仅临时重力/跟随
- [x] 2026-08-02 L2：角色圆盘软弹开（World 帧末 `SoftBodySeparation`）
- [x] 2026-08-02 L2：Hitbox/Hurtbox 逻辑坐标（MotorSim 根 + 相对根挂点局部）
- [x] 2026-08-04 L2：静态碰撞 AABB 烘焙（`StaticCollisionBake` + `SimStaticCollisionWorld`）；空场地回退保留
- [x] 2026-08-04 L2：重力/着地迁入 `CharacterMotorSim`；逻辑路径删除 `CharacterController.Move`
- [x] 2026-08-04 L2/M2：`Bake All` / `Bake Dirty Only` + Dirty 指纹黄条 + Validate 菜单
- [ ] L2 收口：斜坡/网格精确碰撞（当前 AABB 保守）
- [x] 2026-08-08 Wave 2.5：删除 Action `useRootMotion` / LegacyResolve / ForwardOnly 与 Animator RM→Motor 回退
- [ ] L3：降级为可导出 `ActorReplicationSnapshot`（组队 PVE 纠偏/重连）；不再为 GGPO 整世界回滚铺路
- [ ] ~~L5：权威 FramePacket + 客户端完整预测回滚~~ → **2026-08-13 取消为产品主路径**；改 [`TEAM_PVE_NARAKA_STYLE_STATE_SYNC_PLAN.md`](../../docs/2026.8.13/TEAM_PVE_NARAKA_STYLE_STATE_SYNC_PLAN.md)

## 待建设模块

| 模块 | 优先级 | 说明 |
|------|--------|------|
| ActionEditorWindow | P1 | ✅ 基础版 + 菱形/Zoom/Scrub 预览 + 2026-08-04 playhead 跟视口、Create 选文件夹、左侧文件夹分组；后续增强 SFX 预览 |
| Enemy/ + AI | P1 | ✅ 8.10 Desire/Entry Request 总出口关闭；对峙表现已验收；待优化见 8.11 Backlog / A* |
| UI/（MVVM） | P2 | HUD、血条；View/ViewModel 分层，不直写 Domain 权威 |
| 事件总线 | P2 | 轻量 C# event；定稿前不引入第三方 |
| 行为树编辑器 | P2 | ✅ MVP（A1）；待打磨见 `docs/2026.8.11/ENEMY_BEHAVIOR_TREE_BACKLOG_PLAN.md`（建议 A3） |
| 敌人对峙循环 + GaitPolicy | P2 | ✅ 拓扑/秒制落地；对峙表现已验收；见 `docs/2026.8.9/LOCOMOTION_GAIT_POLICY_PLAN.md` |
| Locomotion AnimSet / 倾身 / 绕圈 | P1 | ✅ Play 验收 2026-08-12 · `docs/2026.8.10/LOCOMOTION_DIRECTIONAL_ANIMSET_PLAN.md` |
| PivotTurn 两段式朝向 | P1 | ✅ Play 验收 2026-08-12 · `docs/2026.8.12/PIVOT_TURN_TWO_PHASE_FACING_PLAN.md` |
| A\* 寻路 | P2 | 学习实现；路径 → AI 移动意图；锁步确定性边界待定 |
| 性能优化实践 | P2 | 木桩/多敌人基线 + Profiler 对照；见 `docs/PROJECT_CHECKLIST.md` §6.4 |
| 剧情编辑器 | P3 | 对话/镜头节点或时间轴；与 Gameplay 用事件解耦 |
| AssetBundle + Lua 热更 | P3 | 学习沙盒；热更不得改 ActionSim/Numeric 权威 |
| SDK 打包流程 | P3 | 渠道 SDK / 多包体 / 与热更产物衔接演练 |

总清单一页表：[`docs/PROJECT_CHECKLIST.md`](../../docs/PROJECT_CHECKLIST.md) §6.3～§6.4。

## Tech Debt 观察清单

- [x] 2026-08-11：移动读取收敛为 `IMoveIntentSource`；CharacterActor 删除 Enemy Desire 应用分支，Locomotion / Motor 直接消费注入源
- [x] 2026-08-11：Combat Entry 请求收敛为 `IActionEntryRequestSource`；删除 CharacterActor/Driver 的 Enemy Buffer Bind 与旧具体类型
- [x] 2026-06-21：Prefab/运行时堆业务脚本改为 `CharacterConfig` + `PlayerController` + 纯 C# 角色实例（2026-06-23 命名为 `CharacterActor`）
- [x] 2026-06-23：命名迁移为 `CharacterActor` / `ActionExecutor`，新增 `ACTGameArchitecture`、`TargetSystem`、`CombatActorSystem`
- [x] 2026-06-23：`TargetSystem` 替代静态目标注册表
- [x] 2026-06-29：QFramework 风格强类型契约落地（`ArchitectureSystemBase`、`AppControllerBase`、`ArchitectureCommandBase`、`ArchitectureQueryBase`、`IArchitectureEvent`）
- [x] 2026-06-29：Domain 命中/索敌入口移除直接 `ACTGameArchitecture.Interface` 依赖，改为目标集合注入、`GetActiveTargetsQuery` 与 App 层 Command 编排
- [x] 2026-06-29：新增 Editor 架构边界校验，检查 `System` / `Controller` / `Event` 契约和 Domain 单例访问
- [ ] 仅 `Domain/Simulation` 已拆 asmdef；其余业务仍在单一 Assembly-CSharp
- [x] 2026-08-13：MoveReferenceYaw 输入闭包；唯一 SelectedTarget + 动作中切敌；删除 PlanarBasis/CombatTargetLock/ActionTargetId/Presentation late-bind

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
- [x] 2026-07-14：ActionGraph 多入口——删除 `GraphActionResolver` 与 `ActionEntry` 输入表；Entry×Trigger 同时支持攻击/闪避起手
- [x] 2026-08-08：删除 `PlayerActionSet`；`CombatModeProfile` 直挂 `ActionGraph`
- [x] 2026-07-19：语义化玩法意图层——物理输入经 `GameplayIntentProducer` 转为枚举 Trigger；实现 SprintAttack、PressedThenLong 与 Dodge 后 Sprint 恢复
- [x] 2026-07-19：方向闪避统一为前/后/左前/左后/右前/右后六向解析，移除 Locomotion 起手固定前闪旧路径
- [x] 2026-07-22：TurnBack 输入接管——锁根 0.08 秒后实时输入控制朝向，烘焙位移随新朝向重定向
- [x] 2026-07-22：Locomotion 内层纯状态机——`LocomotionStateMachine` + 五相位 State，删除 `LocomotionService`
- [x] 2026-07-29：敌人系统——共享 CharacterActor、五态 Brain、AI 输入、伤害/Hit/Death、Spawn/Despawn 与阵营过滤
- [x] 2026-07-29：动作职责重构——GraphNode 成为输入/流程/索敌真源，HitPayload 成为伤害/反馈真源，CharacterReactionResolver 承接受击/死亡选招
- [x] 2026-07-30：角色反应闭环去重——Resolver 生成完整请求，ReactionService 统一 Health 事件与 Actor 入口，删除 CharacterConfig/EnemyBrainProfile 硬直双真源
- [x] 2026-07-31：Lockstep L0A——场景唯一 60Hz SimulationHost、稳定 SimActorId/World、Controller Tick 单轨切换与纯 C# 测试
- [x] 2026-08-01：Lockstep L0B——InputFrame 量化/历史、玩家与 AI 单轨输入、整数帧 Intent/AI 冷却及回放基础测试
- [x] 2026-08-01：Lockstep L0C——命中延迟收集与稳定帧末结算、PostCombat 自动衔接、生命周期 Commit 与只读表现事件
- [x] 2026-08-01：Lockstep L1A——Action 整数帧权威、单次 Step、下一帧切招与 Hit/Death 帧化收尾
- [x] 2026-08-01：Lockstep L1B 代码——纯 ActionSim、Snapshot/Event 表现边界、共享帧查询与 60Hz 迁移工具
- [x] 2026-08-02：Lockstep L1B 资产 Hz——全部 ActionDefinition 已 60Hz；Validate Readiness；清除 Editor 30Hz fallback
- [x] 2026-08-04：L2 静态碰撞 AABB 烘焙 + MotorSim 重力/着地；M2 Bake Dirty / Validate

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
