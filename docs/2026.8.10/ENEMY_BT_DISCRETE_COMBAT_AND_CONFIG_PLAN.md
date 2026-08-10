# 敌人 BT：离散出招 · 移动脱离输入 · 对峙滞回 · 配置归属 — 优化方案

> 制定：2026-08-10  
> 修订：2026-08-10 — **终态定案**：敌人移动与出招均脱离玩家输入轨（`LocomotionDesire` + `CombatRequest`）  
> 角色：**敌人 AI 下一阶段结构真源**（命令轨 / 滞回 / 配置归属；先文档，后实现）  
> 相关：  
> - 契约锁：[`../ENEMY_BEHAVIOR_TREE_PLAN.md`](../ENEMY_BEHAVIOR_TREE_PLAN.md) §3.4（`IEnemyBehaviorRunner` / 输出槽；**目标** = Desire + Request）  
> - 演进已关：[../2026.8.9/ENEMY_BEHAVIOR_TREE_EVOLUTION_PLAN.md](../2026.8.9/ENEMY_BEHAVIOR_TREE_EVOLUTION_PLAN.md)（E1～E3）  
> - 编辑器续篇：[../2026.8.9/ENEMY_BEHAVIOR_TREE_OPTIMIZATION_PLAN.md](../2026.8.9/ENEMY_BEHAVIOR_TREE_OPTIMIZATION_PLAN.md)（**仅 Phase A**；B1/B3 并入本文 E-REQ，禁止再落地 Pulse CombatPool）  
> - 对峙循环：[../2026.8.9/LOCOMOTION_GAIT_POLICY_PLAN.md](../2026.8.9/LOCOMOTION_GAIT_POLICY_PLAN.md)  
> - 格式 skill：`.cursor/skills/actgame-design-plan`  
> 装配链：`EnemyDefinition → BehaviorTree → Runner → Blackboard → Brain 提交 Desire/Request → CharacterActor`

---

## 0. 一句话

敌人 AI 终态走 **双命令轨**：`CombatRequest`（显式 Graph Entry）+ `LocomotionDesire`（移动/朝向意图），**不再**经 `AIInputWriter` / `InputFrame` / Intent 伪装玩家；用 **滞回距离带 + 最短驻留帧** 稳住对峙/追击；战斗数值迁到 **BT 节点**，以一棵行为树为主配置面。禁止长期「AI 假手柄」双轨；禁止 BT 直调 `ActionSim` / `MotorSim` / Numeric。

---

## 1. 问题与动机

### 1.1 现状基线

```text
EnemyBrain.Step
  → FillBlackboard（距离/状态；半径读 Profile）
  → Runner.Tick → bb.MoveDesire / AttackPulse / DodgePulse…
  → CommitOutputs → AIInputWriter（Move 量化 sbyte + Pulse 按钮）
  → InputFrame → CharacterActor
       → GameplayIntentProducer（按钮→Intent→Graph）
       → Locomotion 把 Move 当玩家摇杆
```

| 点 | 现状 |
|----|------|
| 出招 | BT `PulseAttack` → `InputButton.Attack` → Intent `Attack` → Graph 按 Intent 选 Entry |
| 移动 | BT `MoveDesire` → `SetMove` → 量化 InputFrame → 与玩家同一套相对移动消费 |
| 玩家连招 | 同一 Intent 重复按下，靠 Graph Cancel/自动衔接推进 Combo |
| 怪物招式 | 多为离散招 / 多 Entry；若用 Intent 区分需大量 `Attack2/3/Skill…` |
| 对峙/追击 | `DistanceLessEqual` / `DistanceGreater` **单阈值**严格切换 |
| AI 数值 | `EnemyBrainProfile`：Aggro/AttackRange/Chase·Strafe 幅度/CD/Stop…；条件节点多数读 Profile |

### 1.2 痛点

1. **假输入扩招式**：离散招式池若继续走 Intent，输入枚举与 Intent 映射爆炸；且与玩家「连点同一 Attack」语义错位。  
2. **假摇杆移动**：出招脱离输入后，仅移动仍走 `InputFrame` 不对称；sbyte 量化损失；还被玩家相机相对移动/Intent 管线约定绑死。  
3. **距离抖动**：玩家在阈值附近反复进出 → Chase↔Strafe 高频切换 → 起步/左右走动画难看。  
4. **双配置面**：改怪手感要同时拧 Profile + 树拓扑；条件无法在图上自带参数。

### 1.3 目标

| 目标 | 说明 |
|------|------|
| 离散出招 | BT 按权重选 **具体 Graph Entry** 起手；连段走 Graph 边，不新增 Intent |
| 移动脱离输入 | BT 写 `LocomotionDesire` → Actor/Locomotion **直接消费**；敌人不再 `AIInputWriter.SetMove` |
| 对峙稳定 | 进/出不同阈值 + 最短驻留；Play 下阈值附近横跳不再每帧切态 |
| 配置归属 | 战斗距离/幅度/CD/滞回等在 **节点 Def 或树资产**；Brain 不承担战斗调参 |
| 输入壳收敛 | 敌人侧 Pulse/Move 迁完后 **删除或掏空** `AIInputWriter` 战斗/移动用途 |
| 不做 | 一招一 Intent；BT 内 `TryStart`/`CharacterController.Move`；Parallel 多写移动；长期 Profile+节点双真源；Agent 改 `.asset` |

---

## 2. 设计原则

1. **玩家设备轨 vs 敌人命令轨**：玩家保持 `InputFrame` + `GameplayIntent`；敌人走 `LocomotionDesire` + `CombatRequest`。  
2. **锁步边界不变**：两命令均在逻辑帧由 Brain 提交；禁止 Runner/`Update` 旁路改 Motor/起招。  
3. **Task 只写黑板**：不持有 Executor/Motor；Driver/Locomotion 消费命令。  
4. **结构优先于 if**：差异在树资产与节点参数，不在 `if (isEnemy)` 散落业务。  
5. **零长期兼容**：迁完删除「条件读 Profile」「敌人 SetMove→InputFrame」旧路径；不保留假手柄回退。  
6. **移动与战斗正交**：两条命令轨独立提交；Action 中 freeze 移动由 Brain/Wait 规则保留。  
7. **随机可测**：`RandomSelector` 可注入 RNG（帧种子），EditMode 固定种子。  
8. **Facing 归命令**：`FaceTarget` / 刷新 facing proxy 挂在 `LocomotionDesire`（或并列 `FacingDesire`），不依赖「有 Move 才转向」的输入副作用。

---

## 3. 目标架构

### 3.1 总览

```text
EnemyBrain.Step（门闩不变）
  ├─ Perception → bb（客观量：距离/朝向/状态/HP%）
  ├─ Runner.Tick
  │    ├─ Stance / DistanceBand（参数+滞回在节点上）
  │    ├─ RandomSelector → RequestCombatAction(graphNodeId)
  │    ├─ MoveToward / Strafe / Stop / Wait*
  │    └─ 输出：LocomotionDesire + CombatRequest（+ Face 旗）
  └─ Commit（不再写 AIInputWriter）
       ├─ LocomotionDesireBuffer.Set(desire)
       └─ ActionEntryRequestBuffer.Set(request)

CharacterActor.Step（敌人）
  → LocomotionStateMachine / Motor 通过 IMoveIntentSource 读 Desire
  → CharacterActionDriver 通过 IActionEntryRequestSource 消费 Entry
  → 无 Request 时不跑玩家 Intent 选招
  → ActionSim 同前

CharacterActor.Step（玩家）
  → InputFrame → Intent → Graph（不变）
```

### 3.2 需求① — 离散出招（CombatRequest）

**定案：只留一种起手通道给 AI 战斗招式。**

| 项 | 定案 |
|----|------|
| 请求载荷 | `ActionEntryRequest`：`HasRequest` + `EntryNodeId`（须为当前 `ActiveGraph` 的 Entry） |
| BT Task | `RequestCombatAction`；常与 `Stop` + `WaitWhileInAction` 组 Sequence；**编辑器** Entry 下拉的 Graph 只读反查：`EnemyDefinition → CombatProfile → Default ActionGraph`（BT 资产不另挂 Graph） |
| 选招 | `RandomSelector`（权重）或单支固定 Entry |
| 连段 | Entry 起手后走 Graph Cancel / AutomaticTransition |
| 废弃 | `AttackPulse` → Intent `Attack`；Dodge/Skill 默认一并迁 Request（或同阶段清 Pulse） |

```text
示例 CombatPool
Selector
├─ AttackPool（Aggro ∧ Locomotion ∧ 攻击带）
│    RandomSelector
│    ├─ w=3: Request(Entry_Swipe) → WaitWhileInAction
│    ├─ w=2: Request(Entry_ComboA) → WaitWhileInAction
│    └─ w=1: Request(Entry_Leap) → WaitWhileInAction
├─ Chase（滞回外沿）→ MoveToward(magnitude)
├─ Strafe（对峙带）→ Strafe(side, magnitude) → WaitFrames
└─ Idle → StopMove
```

| 否决 | 原因 |
|------|------|
| 每招一个 Intent/Button | 枚举膨胀 |
| 仍 Pulse Attack + 多 Entry | 同 Intent 仲裁糊、权重/分招 CD 难表达 |

### 3.3 需求② — 移动脱离输入（LocomotionDesire）

**定案：敌人移动权威 = `LocomotionDesire`，不经 `InputFrame`。**

| 项 | 定案 |
|----|------|
| 载荷 | `LocomotionDesire`：本地轴 `(x侧移, y前进)`（已含幅度）+ `FaceTarget` |
| BT | 现有 Move/Strafe/Stop Task 仍写黑板；Brain Commit 时组装 Desire，**禁止** `AIInputWriter.SetMove` |
| 消费 | `EnemyActorFactory` 构造注入 `IMoveIntentSource`；Locomotion / Motor 直接读取，`CharacterActor` 无 Enemy 分支；**不**走 `GameplayIntentProducer` |
| 量化 | **不做** sbyte 量化；用 float 欲望，由 Motor/步态阈值消化 |
| Action 中 | 保持现逻辑：Action / Confirm / Request 当帧可强制 Desire=0（对齐现 `freezeMove`） |
| Facing | `FaceTargetRequested` 并入 Desire 或同帧提交；假相机 proxy 刷新仍由 Brain 间隔执行 |
| 锁步 | Desire 与 Request 同在 `ProduceInput` 阶段之后的提交槽写入，Actor.Step 只读本帧槽 |
| 废弃 | 敌人 `InputFrame.moveX/Y` 作为移动权威；敌人 `AIInputWriter` 移动路径 |

```text
LocomotionDesire（示意）
  Vector2 localMove        // 已含幅度；Stop = 0
  bool faceTarget
  // 可选：显式世界平面方向（若日后寻路输出世界向，再定转换点；本阶段以本地轴为准）
```

**与「继续假摇杆」对比：**

| 方案 | 结论 |
|------|------|
| 出招 Request、移动仍 InputFrame | ❌ 半吊子终态，量化与管线杂质仍在 |
| LocomotionDesire + CombatRequest | ✅ 本方案终态 |
| BT 直调 MotorSim | ❌ 破坏门闩/分层 |

### 3.4 需求③ — 对峙/追击滞回

**定案：姿态带条件（进/出双阈值）+ 最短驻留帧；状态在条件装饰实例上。**

| 参数 | 含义 |
|------|------|
| `enterDistance` | 进入本支 |
| `exitDistance` | 离开本支（Chase：`exit < enter`） |
| `minDwellFrames` | 最短驻留后才允许因距离翻面失败 |

```text
未在 Chase：distance > chaseEnter → 进入
已在 Chase：distance > chaseExit 保持；≤ exit 且 dwell 满 → 离开
Attack 高优先仍可抢（Selector 更前）
```

禁止：仅调糊单阈值；禁止在 `GaitLocomotionState` 写仇恨距离 if。

### 3.5 需求④ — 配置归属到 BT

**定案：战斗调参真源 = 节点 Def（及树资产默认）；`EnemyBrainProfile` 瘦身。**

| 原 Profile 字段 | 迁入 |
|-----------------|------|
| `aggroRadius` / `loseAggroRadius` | `AggroGate` / 树根设置 |
| `attackRange` | 距离条件节点参数 |
| `chaseMoveMagnitude` / `strafeMoveMagnitude` / `stopDistance` | Move Task 字段 |
| `attackCooldownFrames` | `CooldownGate` / 招式支路 |
| `failedAttackRetryFrames` | 树默认或薄 Brain 辅助 |
| `faceTargetWhileChase` / `repathIntervalFrames` | Desire/Brain 表现默认 |
| `enableCombatActions` | **保留**（木桩） |
| `deathDespawnDelaySeconds` | **保留**（生命周期） |

```text
EnemyDefinition
  ├─ CharacterConfig / CombatMode / Numeric…（本体）
  ├─ behaviorTree          ← AI 策略主配置
  └─ brainFlags            ← 仅开关类；无距离表
```

### 3.6 层边界

| 层 | 职责 | 不负责 |
|----|------|--------|
| Perception / Brain | 客观快照、门闩、提交 Desire/Request、起手确认 CD、facing 间隔 | 攻击半径策划表；写 InputFrame 移动 |
| BT Task/条件 | 策略参数、滞回、权重、写黑板输出 | `TryStart` / `MotorSim` |
| Locomotion 消费 | 读 Desire → 相位/Motor | 读 BT 节点类型 |
| ActionDriver | 读 Request 或（玩家）Intent | 假 Pulse |
| ActionGraph | Entry/连段 | AI 权重 |

### 3.7 关键契约

```text
输入（黑板只读，Brain 填）
  HasTarget, PlanarDistance, PlanarDirection, PathDirection
  CharacterState, HealthNormalized, IsDead
  AttackConfirmPending, Cooldowns

输出（帧初清空）
  LocomotionDesire { localMove, faceTarget }   // 终态移动轨
  CombatRequest { HasRequest, GraphNodeId }      // 终态战斗轨
  （迁移动期可暂存 MoveDesire 字段，E-MOVE2 与 Desire 合并后删重复）

敌人 Actor.Step
  Apply(LocomotionDesire)
  if CombatRequest → TryStart(GraphNodeId)
  // 不消费移动用 InputFrame；不跑攻击 Intent 选招

玩家 Actor.Step
  InputFrame → Intent → Graph（不变）
```

---

## 4. 范围声明

| 阶段族 | 包含 | 不包含 |
|--------|------|--------|
| E-CFG | 参数迁节点；薄 Profile | 命令轨接线 |
| E-ST | 滞回带 + dwell | NavMesh |
| E-REQ | CombatRequest、RandomSelector、删攻击 Pulse | 玩家 Intent 重构 |
| E-MOVE | LocomotionDesire、删敌人 SetMove/移动 InputFrame 权威、收敛 AIInputWriter | 玩家移动改版；完整寻路 |
| 全文不做 | 第三方 BT；Parallel 多移动；BT 改 Numeric | |

---

## 5. 分阶段交付（任务 / 验收 / 出口）

> 勾选：未开始 `[ ]`；完成后 `[x]` 并在出口注明日期。

---

### E-CFG1 — 配置归属到节点（薄 Brain）

**任务**

- [x] 条件 Def 自带距离参数：`InAttackRange` / `Distance*` 不再读 `Profile.AttackRange`  
- [x] `MoveToward` / `Strafe` / `BackOff` Def 自带 `magnitude`、`stopDistance`；**删除**读 Profile 幅度  
- [x] `CooldownReady` / `CooldownGate` 用节点 `cooldownId` + frames（Melee 样例改 `CooldownGate`；成功 CD 不再由 Brain 写 Profile）  
- [x] `AggroGate`（enter/exit）维护 `bb.IsAggroed`；**删除** Brain 读 Profile 半径双轨（**只留一种**：AggroGate/树根）  
- [x] `EnemyBrainProfile` 删除已迁字段；保留 `enableCombatActions` / `deathDespawnDelaySeconds`  
- [x] Factory 样例改节点内参数；EditMode 覆盖断言  
- [x] **删除**「条件默认读 Profile 同名半径」与黑板 `Profile` 字段  

**验收**

- [x] `rg`：条件/Move Task 无 `Profile.AttackRange` / `ChaseMoveMagnitude` / `StrafeMoveMagnitude`  
- [x] 单测：两节点不同 `range` 同距离真值相反  
- [x] 木桩 `enableCombatActions=false` 仍早退不跑 Runner（代码路径保留）  
- [x] Unity 编译 / EditMode 通过（人工确认 2026-08-11）

**出口：** 战斗距离与幅度真源仅在 BT。→ **已达成（2026-08-11：资产补参 + Play 验收）**

---

### E-ST1 — 对峙/追击滞回（依赖 E-CFG1）

**任务**

- [x] `DistanceBandCondition`：`enterDistance` / `exitDistance` / `minDwellFrames` / 模式（InsideBand / OutsideFar / OutsideNear）  
- [x] 滞回状态在装饰实例；`Reset()` 清 latch/dwell  
- [x] 文档推荐 Chase/Strafe 带；Attack 高优先不套 dwell  
- [x] EditMode：阈值振荡不每帧翻面；越 exit 且 dwell 满后离开  
- [x] 样例拓扑说明（`CreateMeleeStanceLoop`；Agent 不改 `.asset`）  

**验收**

- [x] 单测：`OscillateBetweenEnterExit_DoesNotFlipEachFrame`  
- [x] 单测：`BeyondExit_AfterDwell_AllowsLeave`  
- [x] Play：对峙外沿横跳不抖 Chase/Strafe 动画（人工确认 2026-08-11）
- [x] 无 Locomotion 身份 if

**出口：** **已达成（2026-08-11）**

---

### E-REQ1 — CombatRequest 通道（骨架）

**任务**

- [x] `ActionEntryRequest` + 黑板字段；帧初清空
- [x] Task `RequestCombatAction`（Entry Id）
- [x] Brain 提交到 `ActionEntryRequestBuffer`
- [x] `CharacterActionDriver`：有 Request 则 `TryStart` 指定 Entry（CostGate 同玩家）
- [x] `WaitWhileInAction` 闩住 Request 当帧 / ConfirmPending
- [x] EditMode：Request Entry_A ≠ Entry_B；`DebugCombatRequestEntryId` 可显示 Pending

**验收**

- [x] 单测：黑板/缓冲 Entry_A≠B；Wait 闩 Request；空 Entry 失败（不卡死）
- [x] 玩家 Intent 路径未改消费序（Entry Request 仅构造注入后生效）
- [x] Driver 无第三方 BT API
- [x] Play：树上 `RequestCombatAction` + 正确 Entry NodeId 可起招（人工确认 2026-08-11）

**出口：** **已达成（2026-08-11）**

---

### E-MOVE1 — LocomotionDesire 通道（骨架；可与 E-REQ1 并行）

**任务**

- [x] 新增通用 `LocomotionDesire` + 提交缓冲（与 Entry Request 并列）
- [x] Brain `CommitOutputs`：由黑板 `MoveDesire`/`FaceTargetRequested` 组装 Desire；**停止**对敌人调用 `AIInputWriter.SetMove`（Writer 仍服务按钮脉冲）
- [x] Locomotion / Motor 构造注入 `IMoveIntentSource`；删除 CharacterActor 敌人分支与 InputManager Override
- [x] Action / Confirm / Request 当帧 freeze：Desire 清零（对齐现 `freezeMove`）
- [x] Facing proxy 刷新改读 Desire.FaceTarget
- [x] EditMode：空 Frame + Desire 前进 → HasMoveIntent；零 Desire 停止；玩家无 Override
- [x] 玩家路径默认由 `InputManager` 实现同一 `IMoveIntentSource`

**验收**

- [x] `rg`：`EnemyBrain.CommitOutputs` 无 `SetMove`
- [x] 单测：InputFrame.move=0 + Desire 前进仍有 MoveIntent
- [x] 单测：零 Desire（freeze）HasMoveIntent=false
- [x] Play：追击/对峙/停步手感不差于迁前（人工确认 2026-08-11）
- [x] 玩家移动无回归（人工确认 2026-08-11）

**出口：** **已达成（2026-08-11）**

---

### E-REQ2 — 权重池 + 删除攻击 Pulse（依赖 E-REQ1）

**任务**

- [x] `RandomSelector` + Def + 调色板；可注入 RNG（构造 / 黑板 `Rng`）  
- [x] `CreateCombatPool()` 样例（权重 3:2:1 → Request 叶）  
- [x] **删除**敌人 `PulseAttack` / `AttackPulse` → Intent Attack 路径  
- [x] Factory Melee 树改为 Request + Wait  
- [x] 文档 / OPT B1/B3 croplink 本阶段  

**验收**

- [x] `rg`：敌人战斗提交无 `AttackPulse` / `PulseAttack`（`AIInputWriter.PulseAttack` 仅测试/API 残留，Brain 不用）  
- [x] 固定序列 / 播种权重分布单测  
- [x] Play：同敌至少 2 种起手招（人工确认 2026-08-11）
- [x] Graph 连段边仍可用（人工确认 2026-08-11）

**出口：** **已达成（2026-08-11）**

---

### E-MOVE2 — 删除敌人输入壳（依赖 E-MOVE1；建议在 E-REQ2 之后）

**任务**

- [x] 敌人 `ProduceInput`：写 `InputFrame.Empty`（无 move/按钮）
- [x] **删除** `AIInputWriter` 类及敌人装配依赖；删 `PulseDodge` / `PulseHeavy` / `PulseSkill`
- [x] 空 Frame → IntentProducer 无攻击选招；出招仅 `CombatRequest`
- [x] Dodge/Skill：**只留** `RequestCombatAction`（无 Pulse 遗留）
- [x] `rg`：运行时敌人路径无 `AIInputWriter`
- [x] 更新 TECHNICAL / 清单：敌人输出 = Desire + Request

**验收**

- [x] `rg`：`EnemyHandle` / `EnemyBrain` 无 `SetMove` / `PulseAttack` / `PulseDodge`
- [x] 真敌 Play：移动+多招+对峙无 InputFrame 依赖（人工确认 2026-08-11）
- [x] 木桩仍可受击；无 AI 移动（人工确认 2026-08-11）
- [x] 玩家输入管线完整（人工确认 2026-08-11）

**出口：** **已达成（2026-08-11）**

---

### E-REQ3 — 起手确认 / Validator / 文档收口（依赖 E-REQ2；建议 E-MOVE2 后）

**任务**

- [x] Brain 起手确认观测 Request → Action
- [x] 招式 CD：`CooldownGate` 暂存成功 CD，Brain 只确认/丢弃；失败写独立 `action_entry_retry`
- [x] Validator：`WaitWhileInAction` 不得位于 `IsLocomotion` 子树内
- [x] 更新契约表、TECHNICAL、本文件勾选

**验收**

- [x] 新增 Request 失败/成功 CD 单测（人工确认 2026-08-11）
- [x] 新增 Validator 错误/正确拓扑单测（人工确认 2026-08-11）
- [x] `rg`：Brain 不直接写 `basic_attack`；文档与实现一致
- [x] Unity 编译 / EditMode 通过（人工确认 2026-08-11）

**出口：** **已达成（2026-08-11）**

---

## 6. 迁移与兼容

### 6.1 保留 / 迁入

- 保留：`IEnemyBehaviorRunner`、Hit/Death 门闩、ActionGraph 连段、`WaitWhileInAction`、玩家 `InputFrame` 全路径  
- 迁入：Profile 数值 → 节点；攻击 → CombatRequest；移动 → LocomotionDesire  

### 6.2 明确删除

| 删除 | 阶段 | 原因 |
|------|------|------|
| 条件/Task 读 Profile 战斗半径/幅度 | E-CFG1 | 双真源 |
| Brain 读 Profile 的 Aggro 半径双轨 | E-CFG1 | 归属树 |
| 敌人 `AttackPulse` 战斗路径 | E-REQ2 | 离散出招 |
| 敌人 `AIInputWriter.SetMove` | E-MOVE1 | 移动命令轨 |
| 敌人移动/攻击用 InputFrame 权威 | E-MOVE2 | 假手柄 |
| 敌人运行时依赖 `AIInputWriter`（若无他用） | E-MOVE2 | 壳删除 |
| 「再加 Intent 扩招」「移动永久留 InputFrame」 | 全文 | 否决 |

### 6.3 资产（人工）

- 旧树填节点参数；攻击支改 Request+RandomSelector；无需为移动改 Input 资产  
- Agent **不直接改** `Assets/Data/**`、Prefab  

---

## 7. 目录与文件预期（增量）

```text
Assets/Scripts/Domain/Enemy/
  EnemyBrain.cs
  EnemyBrainProfile.cs
  BehaviorTree/
    EnemyBlackboard.cs
    Nodes/CompositeNodes.cs             // + RandomSelector
    Nodes/ConditionNodes.cs             // + DistanceBand / 节点参数
    Nodes/ActionNodes.cs                // + RequestCombatAction
    Serialization/…

Assets/Scripts/Domain/Character/
  Commands/
    IMoveIntentSource.cs
    LocomotionDesire.cs
    LocomotionDesireBuffer.cs

Assets/Scripts/Domain/Combat/Actions/Execution/
  IActionEntryRequestSource.cs
  ActionEntryRequest.cs
  ActionEntryRequestBuffer.cs
  CharacterActionDriver.cs              // 通过接口消费 Request

Assets/Tests/Editor/Enemy/
  EnemyCombatRequestTests.cs
  EnemyLocomotionDesireTests.cs
  EnemyStanceHysteresisTests.cs

docs/2026.8.10/ENEMY_BT_DISCRETE_COMBAT_AND_CONFIG_PLAN.md
```

---

## 8. 风险与对策

| 风险 | 对策 |
|------|------|
| Desire 与旧 InputFrame 双读 | E-MOVE1 验收强制 InputFrame.move=0 仍能走；E-MOVE2 删 Writer |
| Request 非法 Entry | 仅允许 ActiveGraph 已注册 Entry；失败 + retry CD |
| 滞回 Abort 后粘住 | `Reset` 清 latch；Hit 单测 |
| Random 非确定 | 可注入种子 |
| Wait 被 IsLocomotion 误包 | Validator |
| freezeMove 遗漏导致攻击中滑步 | 单测 Action 中 Desire=0；与 WaitWhileInAction 双保险 |
| 删 AIInputWriter 波及测试 | EditMode 用 Desire/Request 直接填缓冲 |

---

## 9. Editor 人工步骤

1. **E-CFG1（资产）**：真敌树根加 `AggroGate`（原 aggro/lose 半径）；`InAttackRange` / Move / Strafe / BackOff / `CooldownGate(basic_attack)` 填原 Profile 数值；瘦后的 Profile 仅留木桩开关。  
2. **E-ST1（资产）**：Chase 用 `DistanceBand(OutsideFar)`（exit&lt;enter）；Strafe 用 `InsideBand`；**Attack 不要套 Band**；Play 外沿横跳不抖。  
3. **E-REQ1 / E-REQ2**：CombatRequest + RandomSelector；删 PulseAttack。  
4. **E-MOVE1/2**：无资产必改项；Play 确认移动不依赖输入调试 HUD。  
5. 木桩：`enableCombatActions=false`。  

---

## 10. 推荐开工顺序

```text
E-CFG1（参数上树）
  → E-ST1（滞回）
  → E-REQ1（CombatRequest）∥ E-MOVE1（LocomotionDesire）
  → E-REQ2（权重池 + 删攻击 Pulse）
  → E-MOVE2（删敌人输入壳）
  → E-REQ3（确认 / Validator / 文档）
```

**最小可感切片：** **E-CFG1 + E-ST1**（先稳对峙）。  
**命令轨切片：** **E-REQ1 + E-MOVE1**（出招与移动同时脱离假输入）。  
**终态切片：** **E-REQ2 + E-MOVE2**（删 Pulse + 删 Writer）。

---

## 11. 与既有方案关系

| 既有文档 | 关系 |
|----------|------|
| BT OPT B1/B3 | 并入 E-REQ2 |
| BT OPT B2 Health | Brain 填客观量；阈值在节点 |
| L-GP 对峙循环 | 拓扑仍成立；幅度在 Task；移动改 Desire |
| BT PLAN §3.4 | 输出槽改为 Desire + Request；删除 AI 对 InputFrame 移动/攻击依赖 |

---

## 12. 成功标准（总出口）

同时满足（**已达成 2026-08-11**）：

1. [x] E-CFG1 / E-ST1 / E-REQ1 / E-MOVE1 / E-REQ2 / E-MOVE2 / E-REQ3 均已达成
2. [x] 敌人运行时：**无** `SetMove` / 攻击 Pulse / 移动 InputFrame 权威
3. [x] 新怪 AI：主调 BT + Graph Entry；BrainProfile 无战斗距离表
4. [x] Play：离散多招 + 对峙不抖 + 追击/侧移正常
5. [x] 玩家输入管线无回归


---

## 13. 变更日志

| 日期 | 说明 |
|------|------|
| 2026-08-10 | 初版：离散出招 / 滞回 / 配置归属；阶段 E-CFG / E-ST / E-REQ |
| 2026-08-10 | **终态修订**：移动亦脱离输入；新增 `LocomotionDesire` 与阶段 E-MOVE1/2；删除「移动仍走输入轨」过渡表述 |
| 2026-08-10 | **E-CFG1 落地**：节点自带距离/幅度；`AggroGate`；薄 Profile；Factory/单测同步 |
| 2026-08-10 | **E-ST1 落地**：`DistanceBandCondition` + `CreateMeleeStanceLoop`；滞回单测 |
| 2026-08-10 | **E-REQ1 落地**：CombatRequest 缓冲 + RequestCombatAction + Driver.TryStartRequestedEntry |
| 2026-08-11 | **总出口关闭**：用户完成真敌树资产配置与 Play / EditMode 验收；E-CFG1～E-REQ3 全部达成 |
