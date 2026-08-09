# 敌人对峙循环 + Locomotion 步态策略 — 优化方案

> 制定：2026-08-09  
> 修订：2026-08-09 — **终态定案**：对峙→追击→攻击→对峙循环 + 左右走动画为必达出口  
> 角色：**敌人近战循环玩法**与 **Locomotion 步态/横步表现** 的结构真源（先文档，后实现）  
> 相关：  
> - 既有 Locomotion 相位：[`docs/LOCOMOTION_OPTIMIZATION_PLAN.md`](../LOCOMOTION_OPTIMIZATION_PLAN.md)  
> - 敌人 AI / BT：[`ENEMY_BEHAVIOR_TREE_OPTIMIZATION_PLAN.md`](./ENEMY_BEHAVIOR_TREE_OPTIMIZATION_PLAN.md)  
> - 装配链：`CombatMode → CharacterLocomotionProfile → LocomotionStateMachine`；AI：`EnemyBrain → BT → InputFrame`

---

## 0. 一句话

用 **GaitPolicy（Profile 资产）** 拉开敌我步态上限、用 **BT 拓扑** 跑通「对峙 ↔ 追击 ↔ 攻击」循环，并用 **定向步态 Resolver + WalkLeft/WalkRight** 做出对峙左右移动的真实动画；禁止在 State 里 `if (敌人)`，禁止长期「横移却播正向 Walk」的半成品终态。

---

## 1. 问题与动机

### 1.1 现状基线

```text
CombatModeProfile
  → CharacterLocomotionProfile
       → GaitLocomotionState：Walk → Run →（约 3s）Sprint（硬编码）

EnemyBrain → BT → MoveDesire / PulseAttack → InputFrame
  StrafeAroundTarget 已写横向 MoveDesire + FaceTarget
  AnimationKey 仅 Idle/Walk/Run/Sprint → 横移仍播 Walk/Run
```

| 点 | 现状 |
|----|------|
| 升档 | 写死在 `GaitLocomotionState`；敌人易进 Sprint 态 |
| 循环 | 常见只有 Chase/Attack；对峙支路未成为稳定循环 |
| 横移输入 | BT Task 已有；幅度与追击共用 `chaseMoveMagnitude` |
| 横移动画 | **无** WalkL/R Key；表现不等于 walk_Left / walk_Right |

### 1.2 痛点

1. 敌人长时间移动进 Sprint（逻辑态），对峙手感错误。  
2. 缺少清晰的 **对峙→追击→攻击→回到对峙** 决策拓扑与幅度分工。  
3. 左右移动只有位移、没有左右走 Clip，终态不可接受。  
4. 用 `if (敌人)` / 复制 EnemyLocomotionSM / 超大 Sprint 秒数都不是结构解。

### 1.3 目标（方案完成定义）

| 目标 | 说明 |
|------|------|
| 循环 | Play：进战后可重复观察 **对峙（左右走）→（过远）追击 →（进距+CD）攻击 →（CD/中距）对峙** |
| 步态结构 | 升档 / Pivot 由 **GaitPolicy**；敌人 Profile `MaxGait=Run`（或不冲刺） |
| 幅度 | `strafeMoveMagnitude` 与 `chaseMoveMagnitude` 分离 |
| 表现 | 对峙时播 **WalkLeft / WalkRight**（名可微调，须为独立 Key+Clip），非正向 Walk 冒充 |
| 不做 | 身份 if、整机复制、长期双轨、方案完成时仍无左右 Clip 的「可接受降级」 |

---

## 2. 设计原则

1. **身份不进 State**：禁止 `isEnemy` / `teamId` 出现在 Locomotion 相位。  
2. **差异在资产**：敌人独立 LocomotionProfile（Policy + AnimationProfile 含左右走）。  
3. **单一升档入口**：`Policy.Evaluate`；迁完删除 State 硬编码 Sprint 分支。  
4. **AI / 步态 / 表现正交**：BT 写输入 → Policy 定档 → Resolver 选片。  
5. **锁步边界不变**：只经 `InputFrame`；不直驱 Animator 旁路。  
6. **零长期兼容**：无 Legacy 升档路径；横步 Resolver 为唯一选片入口（默认实现可等价旧逻辑，但调用点不双轨）。  
7. **终态含表现**：L-GP3 为方案必达阶段，不是「有美术再另开」的可选附录。

---

## 3. 目标架构

### 3.1 玩法循环（BT）

```text
Root (Selector)  —— 每帧重选，高优先在上
  ├─ Attack     条件：有目标 ∧ 仇恨 ∧ InAttackRange ∧ CdReady ∧ Locomotion
  │               → StopMove → PulseAttack
  ├─ Chase      条件：有目标 ∧ 仇恨 ∧ 距离 > 对峙外沿（TooFar）
  │               → MoveTowardTarget（chaseMoveMagnitude）
  ├─ Strafe     条件：有目标 ∧ 仇恨 ∧ 距离带内（对峙带）∧（可选 CD 未好）
  │               → StrafeAroundTarget(±side)（strafeMoveMagnitude，Walk 档）
  │               → WaitFrames（持续对峙）/ 或左右支路轮换
  └─ Idle       → StopMove
```

循环语义：

| 局面 | 选中支路 |
|------|----------|
| 进攻击距且 CD 好 | Attack → 成功后 CD → 下一帧落到 Strafe/Chase |
| 过远 | Chase |
| 中距/贴脸对峙带 | Strafe（左右走动画） |
| 无目标 | Idle |

左右方向：同一 Strafe 宿主上配置 `SideSign`，或 Selector 下 Left/Right 两支（可用距离/冷却/简单交替——实现阶段定一种，**禁止双套并行语义**）。

### 3.2 Locomotion

```text
CharacterLocomotionProfile
  ├─ GaitPolicy（MaxGait / AllowPivot / SprintAfterRunSeconds）
  ├─ AnimationProfile（含 WalkLeft / WalkRight Clip）
  └─ （经 Resolver 选片）

GaitLocomotionState
  → Policy.Evaluate → SetGait
  → AnimResolver.Resolve(gait, localMove) → Play(Key)
```

### 3.3 Policy 契约

```text
GaitPolicyInput  : CurrentGait, MoveMagnitude, RunThreshold, DeltaTime, RunHoldSeconds
GaitPolicyResult : NextGait, RunHoldSeconds
AllowsPivot(gait) → bool
```

形态 **A（定案）**：`[Serializable] LocomotionGaitPolicy` 嵌在 Profile 内。

| 预设 | MaxGait | AllowPivot | 用途 |
|------|---------|------------|------|
| FullPlayer | Sprint | Sprint 可 Pivot | 玩家 |
| EnemyCombat | Run | false | 近战敌（本方案默认） |

### 3.4 横步选片（定案）

**只留一种：离散 `AnimationKey`。**

| Key | 用途 |
|-----|------|
| `WalkLeft` | 本地 move.x &lt; -ε 且步态为 Walk（对峙） |
| `WalkRight` | 本地 move.x &gt; +ε 且步态为 Walk |
| 现有 Walk | 主要前后走；|x|≤ε 时的 Walk 回退 |

规则（写入 Resolver，避免散落 if）：

1. 先由 Policy 得到 Gait（敌人对峙幅度 → Walk）。  
2. 若 Gait==Walk 且 |localMove.x| 为主导横向 → WalkLeft/Right。  
3. 缺 Clip → 回退 Walk（开发期允许；**方案完成验收要求敌人 Profile 已绑左右 Clip**）。  
4. Run/Sprint 不强制左右 Key（追击用 Run 正向即可）；若以后要跑 strafing 另开阶段。

禁止：为横步引入第二套 Animator Controller 旁路；禁止 BT 里写动画名。

### 3.5 层边界

| 层 | 职责 | 不负责 |
|----|------|--------|
| BT | 选 Attack/Chase/Strafe、写 MoveDesire/脉冲 | AnimationKey、Sprint |
| BrainProfile | chase / strafe 幅度、距离参数 | 升档 |
| GaitPolicy | MaxGait、Sprint 计时、Pivot 许可 | 读仇恨 |
| AnimResolver | gait+局部输入 → AnimationKey | 改 InputFrame |

---

## 4. 阶段总览

| 阶段 | 必达 | 交付焦点 |
|------|------|----------|
| L-GP1 | ✅ | GaitPolicy 外置；敌人 MaxGait=Run |
| L-GP2 | ✅ | 幅度分离 + BT 对峙循环拓扑可玩 |
| L-GP3 | ✅ | WalkLeft/WalkRight Resolver + 敌人绑 Clip + Play 见左右走 |
| 方案完成 | ✅ | GP1+GP2+GP3 出口均达成（见各阶段出口） |

---

## 5. 分阶段交付（任务 / 验收 / 出口）

> 格式对齐 [`GAS_STYLE_COMBAT_REFACTOR_PLAN.md`](../2026.8.7/GAS_STYLE_COMBAT_REFACTOR_PLAN.md)。  
> 勾选：`[ ]` / `[x]`；出口注明日期。

---

### L-GP1 — GaitPolicy 外置

**任务**

- [x] `LocomotionGaitPolicy`：`MaxGait` / `AllowPivot` / `SprintAfterRunSeconds`；`Evaluate` + `AllowsPivot`  
- [x] 嵌挂 `CharacterLocomotionProfile`；默认 = 现网玩家（MaxGait=Sprint）  
- [x] `GaitLocomotionState` 升档 / Pivot 只调 Policy  
- [x] **删除** State 内硬编码 Sprint 累计；**删除** Profile 顶层重复 `sprintAfterRunSeconds`  
- [x] EditMode：`LocomotionGaitPolicyTests`  
- [x] 更新 TECHNICAL / 本文件勾选；Editor 步骤：敌人独立 Profile `MaxGait=Run`  
- [ ] （人工）敌人 CombatMode 挂独立 LocomotionProfile——Agent 不改 `.asset`

**验收**

- [x] `rg`：State 无内联 Sprint 累计、无 `isEnemy` / `enableSprint`  
- [x] 单测：Full→Sprint；MaxGait=Run 永不 Sprint；MaxGait=Walk 保持 Walk  
- [ ] 玩家 Play：走跑冲刺 + Sprint Pivot 与迁前一致  
- [ ] 敌人 MaxGait=Run：连续移动 >3s **逻辑 Gait≠Sprint**  
- [x] Sprint 秒数单一真源（`GaitPolicy`）  
- [ ] Unity 编译 / EditMode Editor 确认  

**出口：** 升档唯一真源 = GaitPolicy。→ **代码已落地；Play/人工挂资产待确认**

---

### L-GP2 — 对峙 / 追击 / 攻击循环（AI）

**任务**

- [x] `EnemyBrainProfile.strafeMoveMagnitude`（与 `chaseMoveMagnitude` 分离）  
- [x] `StrafeAroundTargetAction` 读 strafe 幅度；Chase/BackOff 读 chase 幅度  
- [x] 对峙距离带：复用现有 Distance 条件装饰（手搭树）  
- [ ] 真敌 BT **手搭**（Editor）：§3.1 Selector；推荐 **Attack > Chase > Strafe > Idle**；Strafe 用 `SideSign` ±1 两支或单支  
- [x] Strafe 左右：定案 **SideSign 字段**（双支树由策划挂两个 Strafe 节点）  
- [x] EditMode：幅度写入 `MoveDesire` 可区分 chase/strafe  
- [x] TECHNICAL：敌人近战循环 / 幅度一行  

**验收**

- [x] 单测：strafe/chase 幅度配置互不影响  
- [ ] Play（真敌）：  
  - [ ] 过远会 Chase  
  - [ ] 进入攻击距且 CD 就绪会 Attack（有 Pulse / 起手）  
  - [ ] 攻击后或中距会进入 Strafe（横向 MoveDesire + FaceTarget）  
  - [ ] 上述三类可在一局里重复出现（形成循环，而非卡死单一支路）  
- [ ] 对峙幅度默认 0.35，配合 GP1 落在 Walk  
- [x] 无 Locomotion 身份分支  

**出口：** 输入与决策层循环可玩。→ **代码已落地；BT 手搭 + Play 待确认**

---

### L-GP3 — 左右走动画表现（必达）

**任务**

- [x] 新增 `AnimationKey.WalkLeft` / `WalkRight`  
- [x] `CharacterAnimationProfile` 条目可绑（既有 Entry 数组）  
- [x] `ILocomotionAnimResolver` + `DefaultLocomotionAnimResolver`  
- [x] `GaitLocomotionState` 经 `ResolveLocomotionAnimationKey` 播片  
- [x] **删除** `ResolveGaitAnimationKey` 业务双轨  
- [x] |x| 主导 → WalkLeft/Right（ε=0.2）  
- [x] EditMode：输入矩阵 → 期望 Key  
- [ ] （人工）敌人 AnimationProfile 拖入 walk_Left / walk_Right  
- [ ] Play 验收左右走肉眼可辨  

**验收**

- [ ] 敌人对峙 Strafe：播放 **WalkLeft 或 WalkRight**  
- [ ] 换向时左右 Clip 切换正确  
- [x] 单测：Run 忽略横向 Key；无侧向 Clip 时回退 Walk  
- [x] 玩家未绑左右 Key 时回退 Walk  
- [x] Resolver / State 无 `isEnemy`  
- [ ] 方案完成 Play：完整循环中对峙阶段可见左右走动画  

**出口：** 横步表现代码已落地；绑 Clip + Play 待确认。→ **未完全达成**

---

## 6. 方案完成定义（总出口）

同时满足：

1. L-GP1 / L-GP2 / L-GP3 出口均为 **已达成**  
2. Play 清单：对峙（左右走）↔ 追击 ↔ 攻击 可循环  
3. 敌人逻辑不进 Sprint；无身份 if；无升档/选片双轨  

未完成 GP3 不得宣告本方案完成。

---

## 7. 迁移与删除

### 7.1 玩家

- Policy 默认 Full；Sprint 秒数迁入 Policy 后删顶层重复字段。  

### 7.2 敌人

- 独立 LocomotionProfile：`MaxGait=Run` + AnimationProfile 含左右走。  
- BT 按 §3.1 手搭；Agent 不改 `.asset` / Prefab。  

### 7.3 必须删除

| 删除 | 阶段 |
|------|------|
| State 硬编码 Sprint 累计 | GP1 |
| Profile 双份 Sprint 秒数 | GP1 |
| 播片绕过 Resolver 的旧调用 | GP3 |
| 「超大秒数当禁 Sprint」长期用法 | GP1 |
| 身份 if / 复制 EnemyLocomotionSM | 全程禁止 |

---

## 8. 目录预期

```text
Assets/Scripts/Domain/Character/Locomotion/
  LocomotionGaitPolicy.cs
  ILocomotionAnimResolver.cs
  DefaultLocomotionAnimResolver.cs
  CharacterLocomotionProfile.cs
  States/GaitLocomotionState.cs

Assets/Scripts/Domain/Character/Animation/
  AnimationKey.cs                    // + WalkLeft / WalkRight
  CharacterAnimationProfile.cs

Assets/Scripts/Domain/Enemy/
  EnemyBrainProfile.cs               // + strafeMoveMagnitude
  BehaviorTree/Nodes/ActionNodes.cs  // Strafe 读 strafe 幅度

Assets/Tests/.../LocomotionGaitPolicyTests.cs
Assets/Tests/.../LocomotionAnimResolverTests.cs

docs/2026.8.9/LOCOMOTION_GAIT_POLICY_PLAN.md
```

---

## 9. 风险与对策

| 风险 | 对策 |
|------|------|
| 无左右 Clip 资产 | Editor 清单阻塞 GP3 出口；可用临时占位 Clip，但必须是独立 Key |
| BT 优先级导致卡死单支路 | GP2 Play 清单强制验证三态切换；Distance/CD 条件写清 |
| 横移与朝向导致选片抖动 | ε 死区 + FaceTarget 稳定后再采 local x |
| 玩家缺左右 Key | 回退 Walk；不强制玩家绑 |
| 旧文「冲刺≡Run」 | 以本文件 + Sprint 枚举为准 |

---

## 10. Editor 人工步骤

### 10.1 Locomotion（GP1）

1. 复制/新建敌人 `CharacterLocomotionProfile`。  
2. GaitPolicy：`MaxGait=Run`，关闭 Pivot（或按设计）。  
3. 挂到敌人 CombatMode，勿与玩家共用 SO。  

### 10.2 BT（GP2）

1. Behavior Tree Editor 按 §3.1 搭 Attack / Chase / Strafe / Idle。  
2. Strafe 用徽章条件限制对峙带；配置 SideSign 或左右支。  
3. BrainProfile：strafe 小、chase 大；Save 树并挂 Definition。  

### 10.3 动画（GP3）

1. 敌人 AnimationProfile 绑定 WalkLeft / WalkRight Clip（`walk_Left` / `walk_Right` 或项目命名）。  
2. Play：对峙阶段确认左右动画，而非滑步假走。  

---

## 11. 推荐开工顺序

```text
L-GP1（Policy）
  → 人工：敌人 LocomotionProfile
  → L-GP2（幅度 + BT 循环）
  → 人工：搭树 / 调距离与 CD
  → L-GP3（WalkLeft/Right + Resolver）
  → 人工：绑 Clip
  → 总出口 Play 清单
```

**最小可感切片（开发中）：** GP1 单独可合并；**产品演示切片**必须 GP1+GP2+GP3。

---

## 12. 变更日志

| 日期 | 说明 |
|------|------|
| 2026-08-09 | 初版：GaitPolicy 外置 |
| 2026-08-09 | 阶段节改为任务/验收/出口 |
| 2026-08-09 | **终态定案**：对峙-追击-攻击循环必达；WalkLeft/WalkRight 表现升格为 L-GP3 必达；方案完成=GP1+2+3 |
| 2026-08-09 | **代码落地**：GaitPolicy / strafe 幅度 / AnimResolver；待 Editor 挂敌人 Profile+BT+左右 Clip 与 Play |
