# ACTGame ActionDefinition 优化方案

> 制定：2026-08-06  
> 修订：2026-08-06 — 对齐总案 Wave、统一烘焙契约、修复链接  
> 基准：`develop` 当前 `ActionDefinition` / `ActionTimeline` / `ActionBakedMotion` / `ActionSim`  
> 目标：收束重复配置与双权威，保持 Action 只描述「招式内容与执行策略」，为技能资源系统接入留出单一扩展点  
> **排期真源：** [MASTER_IMPLEMENTATION_PLAN.md](./MASTER_IMPLEMENTATION_PLAN.md)（本文 A* 为细节索引）  
> 关联：[ACTION_SYSTEM_LOCKSTEP_REFACTOR_PLAN.md](../ACTION_SYSTEM_LOCKSTEP_REFACTOR_PLAN.md)、[INPLACE_ROOTMOTION_MOTION_TABLE_PLAN.md](../INPLACE_ROOTMOTION_MOTION_TABLE_PLAN.md)、[CHARACTER_MOVEMENT_ANCHOR_OPTIMIZATION_PLAN.md](./CHARACTER_MOVEMENT_ANCHOR_OPTIMIZATION_PLAN.md)、[SKILL_AND_RESOURCE_SYSTEM_PLAN.md](./SKILL_AND_RESOURCE_SYSTEM_PLAN.md)、[COMBAT_NUMERICS_PLAN.md](../COMBAT_NUMERICS_PLAN.md)

---

## 1. 结论摘要

1. `ActionDefinition` 当前整体职责仍合理，问题不是字段数量本身，而是部分字段存在**人工配置、派生缓存、过渡回退并存**。
2. 最大风险是动作位移的三条基础路径：`UseRootMotion`、`bakedMotion`、Timeline `MovementNotifyState`。
3. `sampleRate`、`totalFrames`、`bakedMotion.logicHz/frameCount` 存在重复表达，应明确唯一真源并自动校验。
4. 伤害与 HitStop 已正确下沉到 `HitPayload`，禁止重新提升到 Action 顶层。
5. CancelWindow 与 Phase Recovery 不是重复配置，但必须在编辑器中明确区分语义。
6. 技能资源系统只向 Action 增加一份 `ActionResourceSpec`「价签」；字段以 `COMBAT_NUMERICS_PLAN` 为准；鉴权、扣费和回填由 Gate、Pipeline 负责（过渡 ResourceSim → 终态 GAS NumericSystem）。
7. 位移必须预留**组合规则层**：基础位移之外允许目标吸附、距离修正与离散重定位，但这些规则不得成为新的基础位移权威。
8. `bakedMotion` **演进为** `ActionBakedTrajectory`（GameplayDelta + VisualResidual）；轨迹拆分细节见 Movement Anchor 篇。
9. 优化按总案 Wave 推进：先校验与可视化，再迁资产，**Wave 2 出口硬删除**旧路径；不直接批量手改 `.asset`。

---

## 2. 当前结构与问题

### 2.1 当前顶层字段

```text
ActionDefinition
├─ Animation
│  ├─ animationSegments[]
│  ├─ sampleRate
│  ├─ totalFrames
│  ├─ actionType
│  └─ crossFadeDuration
├─ Execution
│  └─ executionPolicy
│     ├─ interruptPriority
│     └─ useRootMotion
├─ Timeline
│  ├─ Phase / Cancel / Hitbox
│  ├─ Movement / Rotation
│  ├─ VFX / SFX / ActionEvent
│  └─ tracks（编辑器元数据）
└─ bakedMotion（机器写回）
```

### 2.2 风险分级

| 级别 | 问题 | 后果 |
|------|------|------|
| P0 | `UseRootMotion` / `bakedMotion` / Movement 窗口三源 | 同一招可能配置多种位移，运行时按隐式优先级忽略其中一条 |
| P0 | 未烘焙动作仍可回退 Animator Root Motion | 逻辑依赖表现，破坏无皮回放与未来网络确定性 |
| P1 | `sampleRate` 与全局 60Hz、运动表 `logicHz` 重复 | 资产迁移或烘焙后出现频率漂移 |
| P1 | `totalFrames` 与动画段帧数、运动表 `frameCount` 重复 | 动作结束、位移表、Timeline 窗口可能错位 |
| P1 | Timeline Hurtbox 与常驻 HurtboxDefinition 并存 | 启用动态 Hurtbox 后可能形成双权威 |
| P2 | Action 与 Segment 都有 CrossFade | 主从规则不直观，容易误配 |
| P2 | CancelWindow 与 Phase 软取消界面接近 | 设计师误把两者当作同一种取消 |
| P2 | `tracks` 与类型化数组同时保存轨道关系 | 编辑器字符串元数据可能漂移 |
| P3 | 烘焙偏航字段存在但运行时不消费 | 资产体积与维护成本，无实际语义 |

---

## 3. 职责边界

### 3.1 ActionDefinition 应负责

- 动画段及其表现过渡；
- 动作类型和稳定 ID；
- 中断优先级；
- 整数帧 Timeline；
- 唯一动作位移策略及机器烘焙表；
- 技能资源的静态声明 `ActionResourceSpec`；
- 编辑器预览所需的只读派生查询。

### 3.2 ActionDefinition 不应负责

- 读取或修改角色当前 Energy / Decibel；
- 判断玩家是否负担得起技能；
- 在 `ActionSim.Step` 中扣费；
- 命中后直接回填资源；
- 直接执行伤害公式；
- 用 Animator、Transform 或 Physics 决定权威结果。

```text
ActionDefinition：声明「这是什么招、怎么跑、多少钱」
Resolver：选择候选招式
Gate：鉴权并收款
ActionSim：推进招式
Pipeline：命中后结算伤害与奖励
```

---

## 4. 目标数据结构

### 4.1 建议目标

```text
ActionDefinition
├─ Identity
│  ├─ stableId
│  └─ actionType
├─ Animation
│  ├─ animationSegments[]
│  └─ defaultCrossFadeDuration
├─ Execution
│  ├─ interruptPriority
│  └─ baseMotionMode
├─ Timeline
│  ├─ MotionModifier 窗口
│  └─ MotionCommand 点事件
├─ ResourceSpec                 // 技能资源阶段新增，唯一价签
├─ Derived（只读）
│  ├─ TotalFrames
│  ├─ DurationSeconds
│  └─ IsSimulationReady
└─ Generated（隐藏、机器写回）
   └─ bakedTrajectory   // 由 ActionBakedMotion 演进；含 gameplayDelta + visualResidual
```

> 与 Movement Anchor 的契约：`baseMotionMode` 回答「基础 Δ 从哪来」；`gameplayTrajectoryMode` 回答「如何从动画根提取 Gameplay 路径」。二者共存于同一烘焙产物，禁止再维护第二套运动表类型。

### 4.2 基础位移策略收束

用单一枚举替代 `useRootMotion` 的含糊布尔：

```text
ActionBaseMotionMode
├─ None
├─ BakedMotion
└─ ScriptedTimeline
```

| 模式 | 唯一权威 | 校验 |
|------|----------|------|
| `None` | 无动作位移 | 禁止存在 Movement 窗口和有效 bakedMotion |
| `BakedMotion` | `bakedMotion` | 必须 Ready，帧数与 Hz 一致；禁止 Movement 窗口 |
| `ScriptedTimeline` | Movement 窗口 | 禁止使用 bakedMotion；位移值采用整数/毫米 |

**删除 `AnimatorRootMotion` 运行时模式。** Root Motion Clip 只作为 Editor 烘焙源，不再作为 Runtime 权威。

`ActionBaseMotionMode` 只回答「该帧未经修正的基础 Δ 从哪里来」，不负责攻击吸附、追踪、瞬移或击退。

### 4.2.1 位移组合管线（为吸附/瞬移预留）

未来动作位移按固定顺序组合：

```text
BaseMotionSource
  → MotionModifiers（连续修正）
  → MotionCommands（离散指令）
  → CharacterMotorSim / CollisionWorld
  → FinalMotionResult
```

| 层 | 典型需求 | 性质 |
|----|----------|------|
| BaseMotion | 动画烘焙位移、脚本逐帧位移 | 每帧基础 Δ，三选一 |
| Modifier | 攻击吸附、朝目标轻微修正、限制最大脱靶距离 | 对基础 Δ 做确定性修正 |
| Command | 瞬移到目标身后、跳到指定战斗点、强制转向 | 指定帧执行一次的离散操作 |
| Motor | 静态碰撞、软体、地面规则 | 最终提交与约束 |

**关键原则：**

- 吸附不是第四种 `MotionMode`，而是 Modifier；
- 瞬移不是把 `Transform.position` 直接改掉，而是 MotionCommand；
- Modifier 与 Command 都从 Action Timeline 读取，并按固定顺序执行；
- 所有目标只使用 `SimActorId` + 逻辑 Pose，禁止读取敌人表现 Transform；
- 所有距离、角度、偏移使用毫米/量化角度；
- 规则必须可重放、可 Snapshot 恢复、可 Hash。

### 4.2.2 连续位移修正器

首版预留以下类型：

```text
MotionModifierNotifyState
├─ mode
│  ├─ TargetAdhesion          // 攻击吸附
│  ├─ FaceTarget              // 仅修正朝向
│  └─ ClampTargetDistance     // 限制与目标距离
├─ startFrame / endFrame
├─ targetSource               // CurrentLock / ActionTarget / HitConfirmedTarget
├─ maxCorrectionMmPerFrame
├─ desiredDistanceMm
├─ maxAcquireDistanceMm
├─ maxAngleMilliDeg
└─ stopOnTargetLost
```

攻击吸附的建议语义：

```text
baseDelta = 基础位移
targetError = targetPose - actorPose
correction = Clamp(ProjectToPlanar(targetError), maxCorrectionMmPerFrame)
finalDelta = baseDelta + correction
```

必须同时限制：

- 最大捕获距离；
- 最大夹角；
- 每帧最大修正量；
- 总修正量（可选）；
- 目标丢失后的行为。

不得把角色整帧强拉到敌人中心，否则会穿墙、抖动并破坏 PVP 可读性。

### 4.2.3 离散位移指令

```text
MotionCommandNotify
├─ atFrame
├─ commandType
│  ├─ RelocateBehindTarget
│  ├─ RelocateToTargetOffset
│  └─ SnapFacingToTarget
├─ targetSource
├─ localOffsetMm
├─ behindDistanceMm
├─ facingPolicy
├─ collisionPolicy
├─ fallbackPolicy
└─ preserveVertical
```

朝向策略必须显式配置：

```text
MotionFacingPolicy
├─ PreserveCurrent        // 保持执行命令前的角色朝向
├─ FaceTarget             // 执行后立即面向目标
├─ MatchTarget            // 与目标保持相同朝向
└─ FaceDestination        // 面向本次位移方向
```

| 策略 | 推荐场景 |
|------|----------|
| `PreserveCurrent` | 纯位置修正、演出不希望角色突然转身 |
| `FaceTarget` | 绕背攻击、贴身追击；瞬移后面对敌人 |
| `MatchTarget` | 与目标并排、背靠背或复制朝向的演出 |
| `FaceDestination` | 冲刺、扑击；朝实际移动方向 |

`SnapFacingToTarget` 固定等价于 `FaceTarget`，不读取其它 `facingPolicy`；该命令只改朝向、不改位置。其余 Relocate 命令必须显式填写 `facingPolicy`，禁止依赖隐式默认值。

`RelocateBehindTarget` 的权威计算：

```text
targetPose = World.GetCommittedPose(targetId)
behind = -targetPose.forward * behindDistanceMm
desired = targetPose.position + behind + targetPose.right * sideOffsetMm
resolved = CollisionWorld.ResolveRelocation(actorShape, desired, collisionPolicy)
resolvedFacing = ResolveFacing(
  facingPolicy,
  actorPose,
  targetPose,
  resolved)
MotorSim.CommitRelocation(resolved, resolvedFacing)
```

`FaceDestination` 使用**碰撞解析后的实际落点**计算朝向，而不是原始期望点；若实际位移为零，则回退 `PreserveCurrent`，避免零向量产生非法旋转。

碰撞策略必须显式配置：

| 策略 | 行为 |
|------|------|
| `RequireFreeSpace` | 目标点被占用则失败 |
| `FindNearestValid` | 在固定、确定性的候选顺序中找最近合法点 |
| `IgnoreCharacters` | 可忽略软体，但仍不可穿静态墙 |
| `IgnoreAll` | 仅演出/调试允许，不建议用于战斗权威 |

目标失效或落点失败时：

| Fallback | 行为 |
|----------|------|
| `CancelCommand` | 不瞬移，动作继续 |
| `CancelAction` | 动作中断（慎用） |
| `UseForwardOffset` | 沿自身前方向固定距离（确定性） |

默认建议：`FindNearestValid + CancelCommand`。

### 4.2.4 目标锁定与时序

- 动作起手时可把目标固化为 `ActionTargetId`，避免吸附期间锁定目标变化导致瞬间转向另一敌人；
- `CurrentLock` 仅适合持续瞄准类动作；
- `HitConfirmedTarget` 仅在命中结算后可用，不能让本帧 Collect 反向改变已执行的位移；
- 同一帧多个 Command 按 Timeline 顺序 + 稳定 commandId 排序；
- 离散重定位完成后刷新本帧逻辑 Pose，并让 PresentationBridge 按「演出策略」选择 Snap 或短时软校正；
- PVP 中敌我同时瞬移时，先按 `SimActorId` 稳定顺序求落点，再做帧末软体处理。

### 4.3 帧率与总帧数

定案：

```text
LogicHz = ActionSim.LogicHz = 60       // 全局常量
TotalFrames = Sum(animationSegments)   // 唯一派生
DurationSeconds = TotalFrames / 60f    // 仅表现/编辑器
```

- `sampleRate` 不再作为设计师字段；
- `totalFrames` 不再允许人工填写；
- 若 Unity 序列化或性能需要保留缓存，应改成隐藏机器字段，并由 Editor 重建；
- `bakedMotion.logicHz` 只用于内容校验，不参与运行时选择频率；
- `bakedMotion.frameCount` 必须等于 `TotalFrames`。

### 4.4 CrossFade

保留「动作默认 + 段覆盖」，但明确主从：

```text
resolvedFade =
  segment.hasCrossFadeOverride
    ? segment.crossFadeDuration
    : action.defaultCrossFadeDuration
```

**禁止长期保留「首段 ≤ 0 回退默认」。** Wave 2 内用迁移工具写成显式 `hasCrossFadeOverride`；`0` 仅表示硬切。迁移完成前校验 Warning，Wave 2 出口升为 Error。

### 4.5 Cancel 与 Recovery

| 配置 | 唯一语义 |
|------|----------|
| `CancelWindowNotifyState` | 在 Graph 中路由到另一个动作 |
| `Phase.AllowMovementCancel` | Recovery 中退出到 Locomotion |
| `Phase.AllowEntryRestart` | Recovery 中按 Graph Entry 重新起手 |

三者保留，但编辑器分成不同颜色、轨道和说明；禁止新增第四种泛化 `canCancel`。

### 4.6 Hurtbox

首版定案：

- 常驻受击框只认 `HurtboxDefinition`；
- Timeline `HurtboxNotifyState` 在运行时保持禁用/隐藏；
- 若未来启用动态 Hurtbox，规则必须是「Timeline 激活时替换常驻源」，禁止叠加双源；
- 动态 Hurtbox 正式落地前，不允许内容团队同时维护两套配置。

### 4.7 ResourceSpec

资源阶段只增加一份嵌套数据；**字段真源为 `COMBAT_NUMERICS_PLAN` §6.2**，此处不另立加肥字段：

```text
ActionResourceSpec
├─ tags / resourceTag
├─ energyCost
├─ energyGrantOnHit          // EX/Ult 不回能 → 填 0，不另增 bool
├─ decibelGrantOnHit
├─ requiresDecibelFull
├─ clearsDecibelOnStart
└─ consumeDodgeCharge
```

禁止：Action 顶层散落 cost；在 Spec 内堆无敌/Poise/HeavyHit（走 Timeline / Tag 路由）；第二套 SkillExecutor。

---

## 5. Runtime 目标逻辑

### 5.1 起手

```text
Intent
  → Resolver 选择候选 ActionDefinition
  → Gate.CanAfford(action.ResourceSpec)
  → Gate.CommitCost(action.ResourceSpec)
  → ActionSim.Begin(action)
```

ActionSim 不读取 ResourceSim。

### 5.2 每帧动作位移

```text
switch action.BaseMotionMode:
  None:
    baseDeltaMm = 0
  BakedMotion:
    baseDeltaMm = bakedMotion[currentFrame]
  ScriptedTimeline:
    baseDeltaMm = activeMovementState[currentFrame]

targetSnapshot = ResolveStableActionTarget()
modifiedDeltaMm = ApplyActiveMotionModifiers(
  baseDeltaMm,
  targetSnapshot,
  currentFrame)

CharacterMotorSim.TryMove(modifiedDeltaMm)

for command in GetMotionCommandsAtFrame(currentFrame):
  ExecuteDeterministicMotionCommand(command, targetSnapshot)
```

不再出现「表失败 → Animator RM → CC/Transform」回退。

这里仍只有一个基础位移权威；Modifier/Command 是受 Timeline 驱动的组合规则，不允许各自绕开 MotorSim 修改 Transform。

### 5.2.1 推荐运行时职责

```text
ActionSim
  → 只输出：
      baseMotionDelta
      activeMotionModifierSpecs
      motionCommands

ActionMotionResolver
  → 读取逻辑目标 Pose
  → 应用 Modifier / Command
  → 生成 MotionRequest

CharacterMotorSim
  → 碰撞解析与最终提交
```

不要把目标查询、碰撞找点和瞬移算法全部塞进 `ActionDefinition` 或 `ActionSim`；Action 只保存规则数据。

### 5.3 命中

```text
Hitbox Collect
  → Pipeline Resolve
      → Damage
      → ResourceSim.GrantOnHit(action.ResourceSpec)
      → HitConfirm / Reaction
```

伤害仍来自 `HitPayload`，资源仍来自 `ResourceSpec`；两者不提升到 Action 顶层散字段。

---

## 6. Editor 与校验工具

### 6.1 Inspector 分组

```text
Identity
Animation
Execution
  Motion Mode
Timeline
Resource（资源阶段启用）
Generated（默认折叠，只读）
Validation
```

### 6.2 必须新增的校验

| 条件 | 严重度 |
|------|--------|
| BakedMotion 模式但表未 Ready | Error |
| BakedMotion `logicHz != 60` | Error |
| BakedMotion `frameCount != TotalFrames` | Error |
| BakedMotion 与 Movement 窗口并存 | Error |
| None 模式存在 Movement 窗口 | Warning/Error |
| Scripted 模式不存在 Movement 窗口 | Warning |
| MotionModifier 缺少合法 targetSource | Error |
| 吸附最大距离/角度/每帧修正量非法 | Error |
| MotionCommand 同帧重复 commandId | Error |
| Relocate 指令未配置 Collision/Fallback | Error |
| Relocate 指令未显式配置 facingPolicy | Error |
| SnapFacingToTarget 配置了无意义的 facingPolicy | Warning |
| MotionCommand 帧越界 | Error |
| Timeline 窗口越界 | Error |
| 重复同类型唯一 CancelWindow | Error |
| 动态 Hurtbox 与常驻 Hurtbox 双开 | Error |
| Resource Tag 与费用明显矛盾 | Warning |

### 6.3 资产迁移工具

```text
ACT/Tools/Migrate Action Motion Mode
  if bakedMotion.IsReady:
    motionMode = BakedMotion
  else if timeline.HasScriptedMovement:
    motionMode = ScriptedTimeline
  else:
    motionMode = None

  输出：
    成功列表
    冲突列表（表 + Movement 并存）
    未烘焙但旧 UseRootMotion=true 列表
```

迁移工具只生成报告并写入明确字段；冲突资产必须人工决策，禁止静默选择一条路径。

---

## 7. 分阶段实施（映射总案 Wave）

> 勾选与开工顺序以 [MASTER_IMPLEMENTATION_PLAN.md](./MASTER_IMPLEMENTATION_PLAN.md) 为准。

### Phase A0 — 审计与保护网 → **Wave 0**

- [ ] 增加 Action 全库校验报告；
- [ ] 列出位移三源冲突资产；
- [ ] 校验 Hz / frameCount / Timeline 越界；
- [ ] 为现有行为补 EditMode 测试；
- [ ] 不改变 Runtime 行为。

**验收：** 所有 Action 能被归类为 Baked / Scripted / None / Conflict。

### Phase A1 — 引入单一 BaseMotionMode → **Wave 1**

- [ ] 新增 `ActionBaseMotionMode`；
- [ ] Editor 自动推导并迁移无冲突资产；
- [ ] Runtime 改为显式 switch；
- [ ] 冲突资产由人工确认；
- [ ] `useRootMotion` **仅只读**供迁移工具读取，禁止新逻辑依赖；窗口在 Wave 2 出口关闭。

**验收：** 新路径与旧路径对全部已迁资产输出相同位移。

### Phase A2 — 删除 Root Motion 权威回退 → **Wave 2**

- [x] 所有需要位移的 Action 完成烘焙或脚本化（含轨迹拆分，见 Anchor）；
- [x] 删除 Animator RM → Motor 的逻辑入口；
- [x] **删除**旧 `useRootMotion` / `LegacyResolve` / `ForwardOnly` 运行时路径（2026-08-08）；
- [x] Action 表现动画不再驱动权威根（Locomotion Stop/Pivot 的 RM 另议）。

**验收：** 关闭 Animator 仍可完成动作位移、命中和结束（Play 回归）。

### Phase A3 — 收束派生字段 → **Wave 1 校验 + Wave 2 删手改**

- [ ] `sampleRate` 改常量 60；
- [ ] `totalFrames` 改只读派生或隐藏缓存；
- [ ] Bake 强制帧数与 Hz 校验；
- [ ] 清理未消费的烘焙偏航字段；
- [ ] CrossFade 显式 Override；取消 `≤0` 回退默认。

**验收：** Action Inspector 无可手改 Hz / 总帧数；重复 Bake 结果幂等。

### Phase A4 — Timeline 语义整理 → **Wave 2 末 / 可与 A3 并行**

- [ ] Cancel / Recovery / EntryRestart 分轨、分色；
- [ ] `tracks` 明确 Editor-only，不进入 Runtime 查询；
- [ ] 动态 Hurtbox 未启用前隐藏入口；
- [ ] 更新 Action Editor 校验信息。

**验收：** 设计师能从 Inspector 明确知道各取消配置的作用。

### Phase A5 — 位移规则扩展层 → **Wave 4**（依赖 Wave 2 稳定锚点）

> **进度（2026-08-09）：** TargetAdhesion + SoftBodySuppress + Relocate Command 均已接线；Branch_02 吸附已验收。

- [x] 新增 `ActionMotionResolver`；Bridge 在帧末执行 MotionCommand（ActionSim 仍不堆位移逻辑）
- [x] 新增类型化 `MotionModifierNotifyState`；
- [x] 新增类型化 `MotionCommandNotify`；运行时已执行
- [x] 首批实现 `TargetAdhesion` 与 `RelocateBehindTarget`（及 Offset / SnapFacing）
- [x] `MotionFacingPolicy` 由 Resolver → MotorSim 提交朝向，Bridge Sync 表现根
- [x] 目标固化为稳定 `ActionTargetId`；
- [x] 吸附与重定位均经 `CharacterMotorSim` / CollisionWorld；
- [x] Editor 增加 Motion Modifier / Command 轨道和 Adhesion Scene 预览；
- [x] Adhesion EditMode 门禁；Relocate 依赖 OpenField/资产手感验收

**验收（吸附主路径）：** ✅ Branch_02 Editor 验收 2026-08-09。  
**验收（Relocate）：** ✅ 运行时已接线；具体招式点事件需人工配置后 Play 验。

### Phase A6 — 接入技能资源 Spec → **Wave 3**

- [ ] 新增单一 `ActionResourceSpec`（字段对齐 NUMERICS）；
- [ ] Inspector 独立 Resource 分组；
- [ ] Gate / ResourceSim / Pipeline 接入；
- [ ] 禁止 ActionSim 内扣费；
- [ ] 校验 Tag 与费用关系。

**验收：** 删除 Spec 后动作仍能执行但不产生资源语义；资源逻辑无散落 Action 字段。

---

## 8. 删除清单

迁移全部完成后删除：

- `ActionExecutionPolicy.useRootMotion`；
- Runtime Animator Root Motion 权威回退；
- 人工可编辑 `sampleRate`；
- 人工可编辑 `totalFrames`；
- 未消费的 `yawDeltaMilliDeg` 存储与烘焙；
- Timeline 动态 Hurtbox 的无效运行时入口（若仍未实施）；
- 所有临时兼容分支与旧 Inspector。

保留：

- `interruptPriority`；
- `animationSegments`；
- `timeline`；
- `bakedMotion` 机器数据；
- `actionType`；
- 默认 CrossFade + 段覆盖；
- `HitPayload`；
- 后续单一 `ActionResourceSpec`。

---

## 9. 测试计划

| 用例 | 类型 |
|------|------|
| BaseMotionMode 三种模式互斥 | EditMode |
| Baked 表帧数/Hz 不符拒绝 Ready | EditMode |
| 同输入重复回放位移一致 | EditMode |
| Baked 基础位移 + TargetAdhesion 组合一致 | EditMode |
| 吸附超距离/超角度不生效 | EditMode |
| RelocateBehindTarget 落点合法 | EditMode |
| RelocateBehindTarget + FaceTarget 瞬移后面向目标 | EditMode |
| PreserveCurrent / MatchTarget / FaceDestination 朝向语义正确 | EditMode |
| FaceDestination 零位移时保持原朝向 | EditMode |
| 瞬移点被墙阻挡按 Fallback 处理 | EditMode |
| 目标死亡/丢失时 Command 不产生非法 Pose | EditMode |
| 同帧双方重定位结果按稳定顺序一致 | EditMode |
| Animator 关闭后位移/命中/结束正确 | Play Mode |
| Cancel 与 Recovery 语义不互相污染 | EditMode / Play |
| CrossFade 0 硬切与未配置可区分 | Editor |
| Gate 扣费仅发生一次 | EditMode |
| Hitbox Payload 伤害与 ResourceSpec 回填各自单轨 | EditMode |
| 资产迁移重复执行幂等 | Editor |

---

## 10. 风险与对策

| 风险 | 对策 |
|------|------|
| 删除 RM 回退后部分旧招无位移 | A0 先列清单；未完成烘焙不得进入 A2 |
| 自动迁移误选位移源 | 冲突只报告，不自动决策 |
| `totalFrames` 去序列化影响 Editor | 可保留隐藏缓存，但禁止人工编辑 |
| CrossFade 改模型造成表现变化 | 独立迁移，录制前后对比 |
| ResourceSpec 再次让 Action 膨胀 | 只存静态价签；运行逻辑全部外置 |
| 攻击吸附变成隐形自动追踪 | 严格限制捕获距离、角度、每帧/总修正量 |
| 瞬移穿墙或卡进角色 | 所有落点经过确定性 CollisionWorld，配置 Fallback |
| Modifier/Command 让 ActionSim 膨胀 | 新建 ActionMotionResolver；ActionSim 只输出规则数据 |
| 锁定目标变化导致吸附跳人 | 起手固化 ActionTargetId |
| 文档与 develop 不同步 | 落地后更新总案勾选、锁步/运动表/NUMERICS；排期冲突以总案为准 |

---

## 11. 成功标准

- [ ] 每个 Action 只有一个明确位移权威；
- [ ] 基础位移、连续修正和离散指令职责分离；
- [ ] 攻击吸附与目标后瞬移均走确定性 MotionResolver + MotorSim；
- [ ] 目标丢失、落点阻挡和同帧冲突有明确规则；
- [ ] Runtime 无 Animator Root Motion 权威回退；
- [ ] 全局动作只使用 60Hz；
- [ ] 总帧数只由动画段派生；
- [ ] 烘焙表与动作帧数、Hz 强校验；
- [ ] Cancel 与 Recovery 配置语义清晰；
- [ ] 动态/常驻 Hurtbox 不形成双源；
- [ ] 资源费用只存在于 `ActionResourceSpec`；
- [ ] ActionSim 不直接访问 ResourceSim；
- [ ] 迁移后全库 Action 校验无 Error。

---

## 12. 一句话

此次优化不是把 `ActionDefinition` 拆得更碎，而是删除隐式回退与重复权威：**动作只有一个基础位移来源；吸附等连续修正与瞬移等离散指令通过类型化 Timeline 组合，并统一交给 MotionResolver + MotorSim；Action 仍只保存规则数据。**
