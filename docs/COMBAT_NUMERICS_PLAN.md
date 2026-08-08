# 战斗数值总案 — 属性 / 伤害 / 玩法资源 / Debug HUD

> 状态：**字段与产品语义真源；运行时口袋 = GAS-lite `NumericSystem`（G5 完成）**  
> 创建：2026-08-04  
> 修订：2026-08-06 — N5 同键 EX 升格必做  
> 修订：2026-08-07 — N1 ResourceSim / 旧 Health **标为过渡**；终态见 GAS G0～G5  
> 修订：2026-08-08 — G5：旧口袋已删；本文 §3 对齐完成态  
> 修订：2026-08-08 — 文档清理：已删除归档 stub（Attributes/Resource 旧稿）  
> 关联锁步：[ACTION_SYSTEM_LOCKSTEP_REFACTOR_PLAN.md](./ACTION_SYSTEM_LOCKSTEP_REFACTOR_PLAN.md)  
> 技能槽语义：[2026.8.6/SKILL_AND_RESOURCE_SYSTEM_PLAN.md](./2026.8.6/SKILL_AND_RESOURCE_SYSTEM_PLAN.md)  
> **跨系统排期：** [2026.8.6/MASTER_IMPLEMENTATION_PLAN.md](./2026.8.6/MASTER_IMPLEMENTATION_PLAN.md)  
> **数值改造真源：** [2026.8.7/GAS_STYLE_COMBAT_REFACTOR_PLAN.md](./2026.8.7/GAS_STYLE_COMBAT_REFACTOR_PLAN.md)

---

## 1. 结论摘要

1. **一份结算口**：属性/伤害与玩法资源共用 `CombatHitPipeline` 帧末结算；禁止 `ApplyHitCommand` 旁路扣血。  
2. **字段语义**（Energy/Decibel/Dodge/`ActionResourceSpec` 等）以本文为准。  
3. **运行时真源**：GAS-lite `NumericSystem`（Attribute + Effect + Flags）；旧 `CharacterResourceSim` / `CharacterHealth` / `EnemyHealth` 已于 G3/G5 删除。  
4. 新权威字段写入 GAS §5 / `AttributeId`；`ActionResourceSpec` 仅为作者壳，运行时编译为 Instant Effect。  
5. ATK/DEF / Buff·DOT：已按 GAS G2/G4 落地；无独立 AttributeSheet/BuffSim。

---

## 2. 范围与边界

### 2.1 纳入

| 块 | 内容 |
|----|------|
| **A. 属性与伤害** | 统一数值真源演进；伤害公式；与 Pipeline 结算顺序对齐 |
| **B. 玩法资源** | Energy / Decibel / DodgeCharges；起手与 Cancel Gate；命中回填 |
| **C. Debug HUD** | OnGUI 左上角：HP、EX、喧响、闪避、意图缓冲、锁定敌人、Action 帧 |

### 2.2 不纳入

- 正式战斗 UI 美术血条
- 完整 Buff UI、元素异常积蓄运行时（可复用事件总线后续加）
- 受击/死亡 Action 资产配置（代码已通，资产 Editor 人工）
- 预测回滚 Snapshot 字段布局（锁步 L4+ 另跟）

### 2.3 与旧稿差异（必读）

| 旧属性稿写法 | 当前真源 |
|--------------|----------|
| `HitboxFrameConsumer → ApplyHitCommand` | `Collect → CombatHitPipeline.Resolve → target.OnHit` |
| 木桩无扣血 | 现：`CharacterHurtboxTarget` → Vitality / Health Attribute |
| 招式 `damageMultiplier` + Attack | 伤害经 `DamageNumericCalculator`（BaseDamage × ATK/DEF × 倍率） |
| Domain 经 Command 编排扣血 | **禁止** App Command 改 HP；只发布已结算结果 |

---

## 3. 现状（2026-08-08 代码真源 · G5）

```text
SimulationWorld.Step
  → Actors Collect（HitDetector → Pipeline.Collect）
  → Pipeline.ResolveBeforePostCombat
       → 完美窗/无敌早退
       → target.OnHit(context)
            → CombatDamageCalculator → DamageNumericCalculator
            → CharacterVitality / Health Attribute
       → ConfirmHit → Grant Instant Effect
       → hitReceiver.NotifyHit
  → PostCombatActors
  → Pipeline.CompleteFrame → PublishResolvedHit（App 只读反馈）
```

| 模块 | 状态 |
|------|------|
| `NumericSystem` + `CharacterVitality` | ✅ 唯一数值/生命权威 |
| `DamageNumericCalculator` | ✅ ATK/DEF + Out/In 倍率 |
| `HitPayload` | ✅ BaseDamage / reactionId / feedback |
| `NumericCostGate` + Spec 编译器 | ✅ 起手扣费 / 命中 Grant |
| Debug HUD / Snapshot | ✅ Attribute + Effects + Flags（F3） |
| IntentBuffer / TargetLock 对外暴露 | ✅ |

---

## 4. 统一架构

### 4.1 目录

```text
Assets/Scripts/Domain/Combat/
  Damage/                    # CombatDamageCalculator → DamageNumericCalculator
  Resources/                 # 作者壳：ActionResourceSpec / Tag / Config / EnergyFormSelector
  Numeric/                   # 权威：AttributeSet / Effect / NumericSystem / Flags
  Hitbox/CombatHitPipeline.cs  # 结算顺序扩展点

Assets/Scripts/Domain/Character/
  CharacterDebugSnapshot.cs

Assets/Scripts/App/Controllers/Debug/
  CombatDebugHudController.cs
```

### 4.2 分层铁律

- Domain：数值口袋 / Gate / Calculator — **零** Architecture 引用。  
- Pipeline 帧末：扣血 →（有效命中则）资源 Grant → NotifyHit；Collect 阶段禁止副作用。  
- App：`PublishResolvedHit` / Debug HUD **只读**；禁止 Spend/Grant/ApplyDamage。  
- **权威只在 `NumericSystem`**；禁止再引入第二套资源/血量口袋。

### 4.3 帧末结算顺序（定案）

```text
对每条 pending hit（SimHitKey 排序后）:
  0. 完美闪避窗 / Invincible → 早退（不写血、不 Grant；完美另武装反击旗标）
  1. 若目标已死 → skip 权威副作用（仍可按现有策略处理）
  2. damage = DamageNumericCalculator(...) → Health Attribute（经 Vitality）
  3. 若本次为有效几何确认（NotifyHit 路径）:
       Instant Grant Effect（Spec 编译）
  4. hitReceiver.NotifyHit
CompleteFrame → 发布 ResolvedCombatHit（表现）
```

攻击者资源查找：Pipeline 注入 `NumericSystem` 查找表，禁止经 App Command 回写。

---

## 5. 块 A — 属性与伤害（演进）

### 5.1 第一版保持（已落地）

```text
finalDamage = max(0, HitPayload.BaseDamage)
CharacterHealth.ApplyDamage(finalDamage, context)
```

0 伤仍可 `HitReceived`（现有行为保留）。

### 5.2 增强目标 → 改挂 GAS G4

成长 / 防御减伤**不再**单开长期 `AttributeSheet`。终态：

| Id | 说明 |
|----|------|
| `MaxHealth` / `Health` | Vital Attribute |
| `Attack` / `Defense` | Combat Attribute；公式见 GAS G4 |

```text
raw = Attack * hitPayloadScale *（可选 weight）
mitigation = Defense / (Defense + K)   // K=100
final = raw<=0 ? 0 : max(1, raw * (1 - mitigation))
```

- 临时加减益走 Duration Effect Modifier；DOT 走 Periodic。  
- `HitPayload.BaseDamage` 语义可收束为「框系数/固定段伤」之一；迁移时二选一写死。  
- **不**恢复 `ApplyHitCommand`；公式仍在 Domain，由 Pipeline 调用。

### 5.3 木桩

`HurtboxTarget` 若仍存在：与角色同源 Health Attribute（经 Vitality），禁止孤立业务语义血量口袋。

---

## 6. 块 B — 玩法资源（绝区零骨架）

> **存储说明：** 下列字段语义长期有效；运行时映射见 GAS §5 AttributeId / Flags。

### 6.1 资源表

| 资源 | 归属 | 用途 | 首版规则 |
|------|------|------|----------|
| **Energy（EX）** | 角色 | 强化特殊技 | max=120；接战 milli/帧回能；命中 Grant；起手 Spend |
| **Decibel（喧响）** | 角色（单机个人条） | 终结技 | max=3000；命中增加；大招清空 |
| **DodgeCharges** | 角色 | 闪避 | max=2；耗 1；`rechargeFrames=60` |

权威时钟：仅逻辑帧 `NumericSystem.Step`；禁止 `Time.deltaTime` / OnGUI 改资源。

接战：Action/Hit 态，或命中后 `combatHoldFrames`（如 180）倒计时。

### 6.2 配置

**`CharacterResourceConfig`**（推荐嵌 `CharacterConfig`，禁止 Profile 双轨）：

```text
maxEnergy, energyRegenMilliPerFrame,
maxDecibel, maxDodgeCharges, dodgeRechargeFrames, combatHoldFrames
```

**`ActionResourceSpec`**（挂 `ActionDefinition`）：

| 字段 | 含义 |
|------|------|
| `energyCost` | 起手/切招扣除 |
| `energyGrantOnHit` / `decibelGrantOnHit` | 有效命中回填 |
| `consumeDodgeCharge` | 耗闪避次数 |
| `clearsDecibelOnStart` / `requiresDecibelFull` | 终结技 |
| `resourceTag` | Basic/Special/EX/Ult/Dodge |

**产品定案：**

1. Cancel 切招按**新招**扣费；不够 → **不 Begin**，缓冲保留至过期。  
2. 回能仅 ConfirmHit（挥空不回）。  
3. 闪避：单 timer 时间充能。

### 6.3 Gate 接入点

| 位置 | 行为 |
|------|------|
| `CharacterActionDriver` 起手/高优打断 | `CanAfford` → `TryStart` → `CommitCost` |
| `ActionSim.CommitPendingDecision` | Begin 前 `CanAfford`；失败丢弃本次 pending |
| `CharacterActor.Step` | `NumericSystem.Step`（被动回能 + 闪避充能 + Effect） |
| Pipeline 有效命中后 | `GrantOnHit` |

同键双形态（N5）：能量够走 EX 节点，否则普通 Special — 挂 Resolver/Graph，不改 Cancel 窗口语义。

---

## 7. 块 C — Debug HUD

### 7.1 选型

IMGUI `OnGUI`（`CombatDebugHudController`）；不做 UGUI Prefab。`#if UNITY_EDITOR || DEVELOPMENT_BUILD`；Inspector/`F3` 开关。

### 7.2 示例面板

```text
[Combat Debug]
State: Action | Frame: 12/40 | Action: Attack_02 | Freeze: 0
HP: 72/100
EX: 86/120  (+regen 20m/f)   Decibel: 1240/3000
Dodge: 1/2  (recharge 37f)
InCombat: YES (hold 142f)
FrameIntents: Attack
Buffers: Attack(8f) Dodge(3f)
Lock: YES | Enemy_Foo | Dist=2.14
Motor: (...) SoftBody: mass=100 immovable=false
```

### 7.3 `CharacterDebugSnapshot`

由 `CharacterActor.BuildDebugSnapshot()` 只读填充；含 HP、资源、意图、缓冲剩余帧、锁定、Motor/软体。  
需暴露：`IntentBuffer`、`TargetLock`、`NumericSystem`（经 Snapshot）、Vitality；`GameplayIntentBuffer.CopyBufferedForDebug(...)`。

刷新：`LateUpdate` 采样缓存，`OnGUI` 只绘制。禁止 HUD 写 Sim。

---

## 8. 分阶段实施（统一编号）

### Phase N0 — 调试可观测（无新资源逻辑）

- [x] Buffer 调试枚举；Actor 暴露 IntentBuffer / TargetLock / Health  
- [x] `CharacterDebugSnapshot` + `CombatDebugHudController`  
- [x] 面板显示：State、Action 帧、HP、FrameIntents、Buffers、Lock、Motor  

**验收：** Play Mode 攻击可见缓冲/锁定/掉血数字变化。  
**Editor：** 在场景中挂 `CombatDebugHudController` 并指定 Player；F3 开关。

### Phase N1 — Energy + Gate + 命中回能（历史：Wave 3；现 Numeric）

- [x] Config / `ActionResourceSpec`（权威已迁 `NumericSystem` / `NumericCostGate`）  
- [x] Gate → Driver + ActionSim  
- [x] Pipeline GrantOnHit；接战被动回能  
- [x] HUD 显示 EX  

**验收：** `energyCost` 不够不起手；普攻命中回能；HUD 同步。  
**状态（2026-08-06）：** 代码已落地；正式招费用需 Editor 填表。  
**终态（2026-08-07）：** 由 GAS **G3/G5** 替换为 NumericSystem；本文 N1 不再作为长期架构。

### Phase N2 — 属性公式增强 → 改挂 GAS G4

- [ ] ~~独立 `AttributeSheet` 长期双轨~~ → **取消**  
- [ ] Attack/Defense + Calculator：见 GAS G4 `DamageNumericCalculator`  
- [ ] Health 并入 Attribute：见 GAS G3  

**验收：** 见 GAS G4 门禁。

### Phase N3 — 闪避充能

- [x] DodgeCharges + recharge；`consumeDodgeCharge` Gate  
- [x] HUD 次数与充能帧  
- [x] 玩家 Dodge `PerfectDodgeWindow` + `PerfectDodgeAttack`→Counter（Wave 3.4 代码；Graph Entry Editor）

### Phase N4 — 喧响

- [x] Decibel 累加 / 满值门槛 / 大招清空（Sim + Spec）  
- [x] HUD 喧响  
- [ ] Graph `Ultimate` Entry + 资产绑定（人工）

### Phase N5 — 同键双形态（**必做**，2026-08-06 升格）

> 与 `docs/2026.8.6/SKILL_AND_RESOURCE_SYSTEM_PLAN.md` / `MASTER_IMPLEMENTATION_PLAN` Wave 3 对齐；不再标为可选。

- [x] Special 意图能量分支（`ActionEnergyFormSelector`）；HUD 标注下一发 EX/普通  
- [ ] 正式 Graph 双 Entry + EX 费用表（Editor 人工）

---

## 9. 装配改动清单

| 位置 | 改动 |
|------|------|
| `CharacterConfig` | 嵌 `CharacterResourceConfig`；N2 时加 `AttributeProfile` |
| `CharacterActorFactory` | 创建 NumericSystem / NumericCostGate；Buffer/Lock/Vitality 注入 Actor |
| `CharacterActor` | 只读暴露 + `BuildDebugSnapshot` + `NumericSystem.Step` |
| `ActionDefinition` | `ActionResourceSpec`；N2 时明确 Payload/系数语义 |
| `CombatHitPipeline` | 注入资源查找；Resolve 内 Grant 顺序 |
| `GameplayIntentBuffer` | `CopyBufferedForDebug` |
| 场景 | 挂 `CombatDebugHudController`（或 Player 运行时 Add） |

**Agent 不改**：`Assets/Data/**`、`.asset`、Prefab；实现后列 Editor 手工步骤。

---

## 10. 测试计划

| 用例 | 类型 |
|------|------|
| 命中扣血 / 0 伤仍 HitReceived | 已有行为回归 |
| ResourceSim Spend/Grant/充能帧 | EditMode |
| Gate 不够拒绝 / 够则扣除 | EditMode |
| Grant 不在 Collect 触发 | EditMode / 手册 |
| N2 攻防公式 | EditMode |
| HUD 目视 | Play Mode |

---

## 11. 风险与定案

| 风险 | 对策 |
|------|------|
| 旧属性稿误导接入点 | 本文 §2.3；只认 Pipeline |
| Cancel 扣费卡手 | 不够不切招，缓冲保留 |
| HP 双轨（float vs Sheet） | N2 迁移时删旧字段 |
| cost 双轨 | 只保留 `ActionResourceSpec` |
| Debug GC | Snapshot 复用；`StringBuilder` |
| 锁步回滚 | 资源/属性字段进未来 Snapshot，顺序固定 |

### 决策记录

| 日期 | 决策 |
|------|------|
| 2026-07-22 | 属性稿：Sheet + 防御减伤方向（部分被扁平伤害替代） |
| 2026-08-02 | 资源权威逻辑帧；HUD 用 OnGUI；回能仅 ConfirmHit |
| 2026-08-04 | **合并**属性/资源为本文；结算口以 Pipeline 为准；N0 先 HUD |

---

## 12. Editor 人工步骤（实现后）

1. 场景挂载 `CombatDebugHudController`，确认能看到玩家 Snapshot。  
2. `CharacterConfig` 填资源默认值；为技能 Action 配 `energyCost` / Grant。  
3. N2：Create Attribute Profile，拖到 Config；核对 HitPayload 与公式语义。  
4. Play Mode：打怪看 HP/EX/缓冲/锁定；确认卡肉/震屏不回归。

---

## 13. 成功标准

- [ ] 扣血与资源回填仅在 Pipeline 结算阶段发生  
- [ ] 检测层无公式、无 Spend  
- [ ] EX/喧响/闪避可配且 Gate 生效  
- [ ] 左上角 HUD 显示 HP + 资源 + 缓冲 + 锁定  
- [ ] 无 ApplyHitCommand / 旧双轨血量  
- [ ] 落地后 TECHNICAL / ROADMAP 由 architecture skill 同步  

---

## 14. 一句话

字段与产品语义以本文为准；运行时数值口袋为 **GAS-lite `NumericSystem`**（Attribute + Effect + Flags），经 Pipeline / Gate 跑通绝区零式循环与 Debug HUD；无 AttributeSheet/ResourceSim 双轨。
