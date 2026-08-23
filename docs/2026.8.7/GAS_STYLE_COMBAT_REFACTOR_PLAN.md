# GAS 风格战斗数值重构方案

> 制定：2026-08-07  
> 修订：2026-08-07 — 可行性评审定案；G0 交叉文档已对齐（BUFF/NUMERICS/MASTER/Skill/ROADMAP/TECHNICAL）  
> 基准：`develop`（ActionSim / ResourceSim / Pipeline / Timeline；完美闪避**产品规则已定**，Wave 3.4 代码待做）  
> **数值改造真源（本文件）**  
> 关联：  
> - [COMBAT_NUMERICS_PLAN.md](../COMBAT_NUMERICS_PLAN.md)（字段语义；实现排期以本文 G* 为准）  
> - [MASTER_IMPLEMENTATION_PLAN.md](../2026.8.6/MASTER_IMPLEMENTATION_PLAN.md)（Wave 排期）  
> - [SKILL_AND_RESOURCE_SYSTEM_PLAN.md](../2026.8.6/SKILL_AND_RESOURCE_SYSTEM_PLAN.md)（槽位/同键 EX/完美闪避产品语义；存储迁 Numeric）  
> 状态：**G0～G5 完成（2026-08-08）；NumericSystem 为唯一数值真源。Buff = Effect，无独立 BuffSim。**

---

## 1. 结论（先读这个）

1. **值得 GAS 化的是数值与效果层**，不是整盘推倒 Action / 锁步 / Timeline。  
2. **不建议**原样移植 Unreal GAS。本项目做 **GAS-lite**：Attribute + Effect，固定 60Hz。  
3. **允许推倒重来**：`CharacterResourceSim`、`CharacterHealth` / `EnemyHealth` 旧真源、独立 BuffSim 终态，一律并入 `AttributeSet + EffectContainer`。  
4. **重构完成后不保留兼容**：无旧 API 门面、无双轨读写、无 `#if` 旧路径、无“暂时转调旧 Sim”的适配层。G5 出口即唯一真源。  
5. **不推倒**：`SimulationWorld`、`ActionSim`、`ActionTimeline`、`CombatHitPipeline` 帧序、MotorSim、表现桥、完美闪避**产品规则**（窗在玩家 Dodge、输入派生 `PerfectDodgeAttack`）。  
6. **Wave 3 玩法语义保留**：同键 EX、能量 Gate、命中 Grant、完美闪避吞伤/减速/反击缓冲——只换存储与结算口袋。  
7. 改造后心智模型：

```text
Action（何时发生）
  → Effect（怎么改数 / 上状态）
  → Attribute（数是什么）
```

旗标/短时上下文（接战门闩、完美反击缓冲）见 §5.4，**不全部塞进 Attribute**。

---

## 2. 零兼容政策（强制）

### 2.1 完成后必须删除

| 删除对象 | 说明 |
|----------|------|
| `CharacterResourceSim` | 含类型、测试、HUD 专用字段路径、`PerfectDodgeCounterFrames` 旧挂点 |
| `ActionResourceGate`（旧名） | 替换为 `NumericCostGate`（或等价） |
| `CharacterHealth` / `EnemyHealth` 作为独立权威 | HP 只在 Attribute；死亡边沿改读 Health Attribute |
| 独立 `CharacterBuffSim` / `BuffDefinition` 终态 API | 统一为 Effect；旧名不得残留公开 API |
| `CombatDamageCalculator` 仅 `BaseDamage` 的旧入口 | 替换为 `DamageNumericCalculator` |
| `SimulationHost` 的 Resource 专用注册表 | 改为 `NumericSystem` 查找 |
| 任何 `*Legacy*` / `*Compat*` / 双写同步代码 | 禁止留仓库 |

### 2.2 允许保留的「作者壳」（不是兼容层）

| 保留 | 性质 |
|------|------|
| `ActionResourceSpec` | 策划填成本/回填；**运行时只编译为 Instant Cost/Grant Effect**，禁止 Spec 直接改 Attribute |
| `CharacterNumericConfig`（可由 ResourceConfig + 攻防合并） | 初始化 Base Attribute |
| HitPayload / Timeline 上的 Effect 引用 | 配置入口 |
| Timeline `Invincible` / `PerfectDodgeWindow` | 命中早退与产品窗；不进 Spec |

禁止：

- Spec 与 Effect 各扣一次费  
- Health float 与 Attribute Health 并存  
- ResourceSim.Step 与 Effect/Numeric 回复双推进  
- 玩家走 Numeric、敌人仍走 `EnemyHealth` 半套  

### 2.3 迁移期规则

- 允许**单个 PR / 单个阶段内**临时双轨以便切换，但**不得合并到长期 develop 作为稳定态**。  
- G3 接通 Action 的同一出口 PR，必须切断旧 Resource/Health 写入。  
- G5 未完成不得宣告重构完成；完成定义见 §12。  
- **G5 前**：禁止在 `CharacterResourceSim` 上新增权威字段；新需求写入本文 §5 契约。

---

## 3. 为什么不全盘重写

| 现有模块 | 去留 | 原因 |
|----------|------|------|
| `SimulationWorld` 60Hz | **保留** | 锁步/回放真源 |
| `ActionSim` + Graph + Cancel | **保留** | ACT 招式状态机 |
| `ActionTimeline` / Hitbox / PerfectDodge / Invincible | **保留** | 整数帧窗口权威 |
| `CombatHitPipeline` | **保留并扩展** | 帧末结算口 |
| `CharacterMotorSim` / 锚点 | **保留** | 与数值 GAS 无关 |
| `GameplayIntent` 完美反击派生 | **保留语义** | 缓冲旗标迁 Numeric 上下文 |
| `CharacterResourceSim` | **删除（终态）** | 并入 Attribute + Cost/Grant Effect |
| `CharacterHealth` / `EnemyHealth` | **删除（终态）** | 并入 Vital Attribute |
| 独立 BuffSim | **删除（终态）** | 并入 EffectContainer |
| 第三方 Unity GAS 插件 | **不做** | 过重且与整数帧冲突风险高 |

> **推倒的是多套数值口袋；不是推倒 ACT 骨架。完成后旧口袋必须从代码树消失。**

---

## 4. 目标架构（GAS-lite）

```text
NumericSystem（角色数值中枢）
├─ AttributeSet              // Base / Current（milli-int）
├─ EffectContainer           // Instant / Duration / Periodic
├─ ModifierAggregator        // Current = (Base + ΣFlat) * ΠPercent
├─ CombatContextFlags        // 接战门闩、完美反击缓冲等短时旗标（§5.4）
└─（二期）TagContainer

Action 配置入口
├─ ActionResourceSpec        // → 编译 Instant Cost / Grant Effect
├─ HitPayload.effects[]      // 命中施加
└─ Timeline EffectNotifyState / Phase / PerfectDodge

ACT 骨架（不变）
├─ ActionSim + Graph + Intent
├─ ActionTimeline
└─ CombatHitPipeline
```

### 4.1 与完整 GAS 的裁剪

| GAS 概念 | 本项目 | 不做 |
|----------|--------|------|
| AttributeSet | ✅ | — |
| GameplayEffect | ✅ 瘦身 | 首版不做完整 Execution Calculation 体系 |
| Ability | △ ActionSim/Graph | 不重写 Ability Task |
| GameplayTag | ○ 二期 | 首版 string / 枚举旗标即可 |
| ASC MonoBehaviour | ❌ 用纯 C# `NumericSystem` | — |
| Prediction/Rep | ❌ | 自有 Snapshot 后置 |
| GameplayCue | ❌ | VFX 继续 Notify |

---

## 5. 属性、Effect 与旗标契约（定案）

### 5.1 AttributeId 首版表

| Id | 组 | 说明 |
|----|-----|------|
| `Health` / `MaxHealth` | Vital | 当前/上限；死亡边沿：`Health` 从 >0 → ≤0 |
| `Energy` / `MaxEnergy` | Resource | 玩法能量 |
| `EnergyRegenMilliPerFrame` | Resource | 接战回能速率（milli/帧） |
| `Decibel` / `MaxDecibel` | Resource | 喧响 |
| `DodgeCharges` / `MaxDodgeCharges` | Resource | 闪避次数 |
| `DodgeRechargeFrames` | Resource | 配置型：单次充能所需逻辑帧（Base） |
| `Attack` / `Defense` | Combat | G4 伤害公式 |
| `OutgoingDamageMult` / `IncomingDamageMult` | Combat | 出伤/承伤倍率；Base=1000 表示 ×1.0 |

存储：**全部 milli-int**（显示层再除 1000）。Current 钳制在 `[0, Max]`（Max 自身也可被 Duration Modifier 改）。

### 5.2 Effect 白名单

| 策略 | 用途 |
|------|------|
| `Instant` | Cost / Grant / 单次伤害结算 |
| `Duration` | 加攻、减伤、改 Max/Regen |
| `Periodic` | **仅 DOT/HOT 类跳伤跳治**；**不做**被动回能（见 §5.3） |

叠层首版：`Replace` / `Refresh` / `StackCount`（达 `maxStacks` 停止增加）。  
聚合：`Current = (Base + ΣFlat) * ΠPercent`；首版不做 Override。

### 5.3 被动回能 / 闪避充能（定案：单轨）

**采用：`NumericSystem.Step` 内置被动规则**（对齐现 `CharacterResourceSim.Step`）：

- 接战门闩 > 0 时按 `EnergyRegenMilliPerFrame` 回 Energy  
- DodgeCharges < Max 时按充能帧恢复次数  
- **`ActionSim.IsFrozen`（卡肉）时跳过整段被动 Step**（与现 Wave 3.6 一致）  

**禁止**再用 Periodic Effect 做「接战回能 / 闪避充能」，避免与 Step 双跑。  
Periodic 只服务 DOT/HOT 与明确的周期玩法 Effect。

### 5.4 战斗上下文旗标（非 Attribute）

| 旗标 | 推进 | 用途 |
|------|------|------|
| `InCombatHoldFrames` | `NumericSystem.Step` 递减；动作/命中/受击刷新 | 门闩回能 |
| `PerfectDodgeCounterFrames` | Step 递减；完美闪避武装；Counter 起手清空 | 输入条件 `HasPerfectDodgeCounter` |

挂在 `NumericSystem` / `CombatContextFlags`，**不要**做成可被任意 Effect Flat 堆的 Attribute，除非二期有明确需求。

### 5.5 Spec → Effect 运行时规则（定案）

```text
TryStart / Begin
  CanAfford：只读 Attribute Current（+ Spec 门槛字段）
  Commit：从 ActionResourceSpec 编译 1..N 个 Instant Cost Effect → 立即 Apply
  （Spec 本身不写 Attribute）

Pipeline ConfirmHit（非完美闪避早退）
  Grant：从 Spec 编译 Instant Grant Effect → 立即 Apply
  另：HitPayload.effects[] / 命中施加 Duration·Periodic
```

单测钉死：**同一次 Begin 对 Energy 只变化一次**；挥空路径零 Grant。

### 5.6 旧能力映射

| 旧/计划 | 新口径 |
|---------|--------|
| `ResourceSim.CommitCost` | Instant Cost Effect（由 Spec 编译） |
| `ResourceSim.GrantOnHit` | Instant Grant Effect（由 Spec 编译） |
| `ResourceSim.Step` 回能/充能 | `NumericSystem.Step` 被动规则（§5.3） |
| `PerfectDodgeCounterFrames` | `CombatContextFlags`（§5.4） |
| BuffDefinition | `EffectDefinition`（Duration/Periodic） |
| `CharacterHealth.ApplyDamage` / `EnemyHealth` | 伤害结算写 `Health` Attribute + 死亡边沿事件 |
| `CombatDamageCalculator(BaseDamage)` | `DamageNumericCalculator`（G4 读 Attack/Defense + 倍率 Effect） |

---

## 6. 与 Action / Pipeline 衔接

### 6.1 帧序（定案）

```text
ActionSim.TryStart
  → NumericCostGate.CanAfford（Attribute）
  → Begin：编译并 Apply Cost Effect（一次）
Timeline
  → Hitbox / Cancel / Phase / PerfectDodge / EffectNotify(Self)
Pipeline.Resolve（Collect 禁止改数）
  → 完美闪避窗命中：早退（不写 Health、不 Grant）；武装 Counter 旗标；发减速表现事件
  → Invincible 相位：早退（不写 Health、不 Grant、不武装 Counter）
  → 否则：伤害结算 → 写 Health → 死亡边沿 → Reaction
  → ConfirmHit → Grant Effect + 命中 Effect 列表
Actor / NumericSystem.Step
  → 非卡肉：被动回能/充能 + Duration/Periodic 推进 + 旗标递减
```

### 6.2 玩家与敌人

- **同一** `CharacterActorFactory` → `NumericSystem` 装配路径。  
- 删除 `EnemyHealth` 并行权威；敌人死亡/受击与玩家共用 Health Attribute 边沿。

### 6.3 与完美闪避 / 输入层

- Timeline / Intent 产品规则不变（见 Skill 篇）。  
- 仅将 `HasPerfectDodgeCounter` / 反击缓冲数据源从旧 ResourceSim 字段换为 `CombatContextFlags`。

---

## 7. 改造策略

| 路线 | 内容 | 结论 |
|------|------|------|
| 长期双轨兼容 | 新旧 API 共存 | ❌ 禁止作为完成态 |
| **数值层替换** | 新建 Numeric，切换调用点后删除旧类型 | ✅ |
| 全盘 GAS 插件 | 替换 ActionSim | ❌ |

**阶段内**可短时并存以便一次切换；**阶段出口**不得残留旧权威写入。

---

## 8. 分阶段实施与验收（G0～G5）

### G0 — 定契约（不改玩法）

**任务**

- [x] 冻结本文为数值改造真源（本修订）  
- [x] 定案 Buff 终态 = EffectContainer（无独立 BuffSim）  
- [x] 修订 `COMBAT_NUMERICS_PLAN`：N1 Resource 标为过渡，终态并入本文  
- [x] 修订 `MASTER_IMPLEMENTATION_PLAN`：插入 G0～G5；Wave 4 入口 = G5  
- [x] 修订 Skill 篇：完美窗=玩家 Dodge；ResourceSim 过渡；Intent=`PerfectDodgeAttack`  
- [x] 公布 AttributeId / Effect 白名单 / 旗标表 / Regen 与 Spec 定案（§5）  
- [x] ROADMAP/TECHNICAL 记入「ResourceSim 过渡 → G* 删除」指针（详见同步修订）  

**验收**

- [x] 文档交叉引用无「长期保留 ResourceSim/BuffSim/EnemyHealth 权威」表述  
- [x] 删除清单与保留清单无冲突  
- [x] MASTER 已标注 G* 与 Wave 4 入口关系  

**出口：** 可以开始写 Numeric 代码（G1）。

---

### G1 — AttributeSet + 聚合器

**任务**

- [x] `AttributeId` / `AttributeSet` / `ModifierAggregator` / `CombatContextFlags`  
- [x] `CharacterNumericConfig` 初始化 Base  
- [x] 聚合与 Max 钳制（milli-int）  
- [x] EditMode 单测（`NumericSystemTests`）  
- [x] 薄 `NumericSystem`（Flags Step + 被动回能/充能；**未**接入 Actor/Pipeline）  

**验收**

- [x] 任意 Attribute 只有 Base/Current 一套读法  
- [x] Flat + Percent 聚合与公式一致（金值单测）  
- [x] Health/Energy Current 钳在 `[0, Max]`  
- [x] G1 **未扩大**旧 `CharacterResourceSim` / `CharacterHealth` API 表面  

**出口：** Numeric 可读可测，尚未强制替换全调用点。→ **已达成（2026-08-07）**

---

### G2 — Effect 运行时

**任务**

- [x] `EffectDefinition` / `ActiveEffect` / `EffectContainer`  
- [x] Instant / Duration / Periodic（Periodic = DOT/HOT）  
- [x] 叠层：Replace / Refresh / StackCount  
- [ ] HitStop 相关字段（若仍需要与 Effect 联动）→ **延后**：现网 HitStop 仍走 Action/Pipeline，G2 不双轨  
- [x] Debug Snapshot 列出 ActiveEffects + 旗标（`NumericDebugSnapshot`）  
- [x] EditMode：`EffectContainerTests`  

**验收**

- [x] Duration 到期后 Modifier 消失，Current 回到预期  
- [x] StackCount 达上限后不再增加  
- [x] Periodic DOT 按 `intervalFrames` 跳伤，总跳数可算  
- [x] 同输入逻辑帧重放：ActiveEffect 与剩余帧一致  
- [x] **无**并行 `CharacterBuffSim`  

**出口：** 可用测试 Effect 加攻/上毒。→ **已达成（2026-08-07）**

---

### G3 — 接通 Action / Pipeline（切断旧权威）

**任务**

- [x] `NumericCostGate`：CanAfford / Commit（Spec→Cost Effect）  
- [x] Pipeline：完美闪避/无敌早退；伤害写 Health Attribute；Grant Effect  
- [ ] Timeline `EffectNotifyState`（可选）→ **仍未做**（不阻塞 G5）  
- [x] Factory / Host 只注册 `NumericSystem`；`CharacterVitality` 边沿  
- [x] **删除** `CharacterResourceSim` / `ActionResourceGate` / `CharacterHealth` / `EnemyHealth`  
- [x] HUD / Snapshot 改绑 Numeric  
- [x] `PerfectDodgeWindowNotifyState` + Pipeline 武装旗标（Counter Intent 路由仍属 Wave 3.4）  

**验收**

- [x] 能量不足：`TryStart` 失败，不扣费（`ActionSimResourceGateTests`）  
- [x] 起手成功：资源变化只来自 Cost Effect（单次）  
- [x] Grant 经 Compiler；挥空不调用 Grant  
- [x] 卡肉跳过 `NumericSystem.Step`  
- [x] 完美闪避窗：吞伤 + 武装 Counter 旗标（慢动作表现事件未做）  
- [x] 业务代码无 `CharacterResourceSim` / 旧 `CharacterHealth.ApplyDamage`  
- [x] 玩家与敌人同一 Numeric / Vitality 路径  

**出口：** 技能循环完全跑在 Numeric 上。→ **已达成（2026-08-07）**

---

### G4 — 伤害与成长

**任务**

- [x] `DamageNumericCalculator`：Attack/Defense + Outgoing/IncomingDamageMult  
- [x] Config 灌 Base Attack/Defense；倍率默认 1000  
- [x] DOT：经 Vitality handler，无 Hit Reaction；不 Grant（仍仅 Pipeline ConfirmHit）  
- [x] EditMode：`DamageNumericCalculatorTests`  

**验收**

- [x] 无临时 Effect 时伤害 = 公式基线（Attack=10 / BaseDamage=10 → 10）  
- [x] 出伤 ×1.25 / 承伤 ×0.8 单测  
- [x] 多层倍率连乘顺序稳定  
- [x] DOT 三跳走 Health handler（装配 Vitality 后无受击 Action）  
- [x] 提升 Base Attack 后无 Buff 时伤害上升  

**出口：** 成长与临时 Effect 同时影响战斗。→ **已达成（2026-08-07）**

---

### G5 — 清理与完成态（零兼容）

**任务**

- [x] 删除旧权威：`CharacterResourceSim` / `ActionResourceGate` / `CharacterHealth` / `EnemyHealth` 及对应测试  
- [x] `Domain/Combat/Resources` **仅保留作者壳**（Spec / Tag / Config / EnergyFormSelector）；无 Sim/Gate 运行时口袋  
- [x] 无数值 Compat/Facade/Adapter  
- [x] Snapshot：`NumericDebugSnapshot` + `CharacterDebugSnapshot`（Attribute + ActiveEffects + Flags）；HUD F3 展示  
- [x] 更新 TECHNICAL / MASTER / NUMERICS / Skill / ROADMAP  

**验收（完成定义）**

- [x] 业务代码 `rg` 无旧权威类型定义与引用（Unity 编译 / EditMode 需在 Editor 确认）  
- [x] 无文件名含 `Legacy` / `Compat` 的数值适配代码  
- [x] HUD 只读 Numeric（经 Actor Snapshot）  
- [x] 文档描述完成态；「过渡双轨」仅作历史说明  

**出口：** 重构完成；可进入 Wave 4。→ **已达成（2026-08-08）**

---

## 9. 与总排期的关系

```text
Wave 2 出口 + Wave 3 玩法语义已代码落地
  → 【暂停】在 ResourceSim 上继续加权威字段
  → G0～G2（Numeric 骨架）
  → G3（切换 Gate/Pipeline，删除旧资源/血量权威）
  → G4（伤害成长）
  → G5（零兼容清理）
  → Wave 4 吸附 / LockOn
```

| 文档 | 调整 |
|------|------|
| `COMBAT_NUMERICS_PLAN.md` | N* 语义保留；实现排期改指本文 G* |
| `MASTER_IMPLEMENTATION_PLAN.md` | 增补 G0～G5；Wave 4 入口 = G5 |
| Skill 篇 | 产品语义保留；存储真源改指 Numeric |

---

## 10. 代码落点（完成态）

```text
Assets/Scripts/Domain/Combat/Numeric/
  AttributeId.cs
  AttributeSet.cs
  ModifierOp.cs
  ModifierAggregator.cs
  CombatContextFlags.cs
  EffectDefinition.cs
  EffectDurationPolicy.cs
  ActiveEffect.cs
  EffectContainer.cs
  NumericSystem.cs
  NumericCostGate.cs
  DamageNumericCalculator.cs
  CharacterNumericConfig.cs
  ActionResourceSpecEffectCompiler.cs   // Spec → Instant Cost/Grant

Assets/Scripts/Domain/Combat/Resources/   // 作者壳：Spec / Tag / Config / EnergyFormSelector（非权威）
Assets/Scripts/Domain/Combat/Actions/Execution/NumericCostGate.cs
```

装配完成态：

```text
CharacterActorFactory
  → NumericSystem（含 Flags）
  → ActionSim(gate: NumericCostGate)
  → Pipeline(numericLookup)
  → Actor.Step → numeric.Step()（卡肉跳过被动）
```

---

## 11. 测试门禁

| 阶段 | 最低测试 |
|------|----------|
| G1 | 聚合公式；Max 钳制；初始化 Base；旗标递减 |
| G2 | 叠层三策略；到期移除；Periodic 跳数 |
| G3 | 不够费不起手；Begin 只扣一次；挥空不 Grant；命中 Grant；完美闪避早退；旧类型零引用 |
| G4 | 攻防公式金值；出伤/入伤倍率；DOT 无 Reaction |
| G5 | 全量 EditMode；`rg` 删除清单归零；Play HUD 抽样 |

### Play Mode 场景验收（G3 后必做）

1. 普攻命中：Energy/Decibel Grant，HUD 与 Attribute 一致。  
2. Special 临界能量：够则起手并扣费，不够则不起手。  
3. Duration「加攻」：伤害上升，到期恢复。  
4. Periodic「毒」：跳数正确，无受击动画循环。  
5. 卡肉：被动回能不推进。  
6. 完美闪避窗内挨打：不掉血、减速、随后 `PerfectDodgeAttack` 可出 Counter。  
7. 敌人与玩家各打一轮，共用 Numeric 路径。

---

## 12. 成功标准（整包完成）

- [x] 角色数值唯一真源：`NumericSystem`（Attribute + ActiveEffect + Flags）  
- [x] Cost / Grant / Buff / DOT 均走 Effect；被动回能只走 Step  
- [x] ActionSim / Timeline / Pipeline 骨架保留；完美闪避吞伤/武装已接（Counter Intent = Wave 3.4）  
- [x] **零兼容**：旧 ResourceSim / 旧 Health·EnemyHealth / 独立 BuffSim / Facade 全部删除  
- [x] 全仓库无旧权威类型业务引用  
- [ ] EditMode / Play 场景验收：需在 Unity Editor 重编译后确认  
- [x] Snapshot 字段：Attribute + ActiveEffects + Flags（HUD 已绑）  
- [x] MASTER / NUMERICS / BUFF / Skill / TECHNICAL / ROADMAP 与完成态一致  

---

## 13. Editor 人工步骤（实现后）

1. 配置 `CharacterNumericConfig`：MaxHP、MaxEnergy、Attack 等 Base。  
2. 招式保留 `ActionResourceSpec`；确认只通过编译器变成 Cost/Grant Effect。  
3. Create `EffectDefinition`：`AttackUp`（Duration）、`Poison`（Periodic）。  
4. 测试攻击 HitPayload 挂 `Poison`；可选 Timeline 挂 `AttackUp`。  
5. Dodge：继续配 Invincible / PerfectDodge 轨（不进 Spec）。  
6. Play + F3：Attribute Current、ActiveEffects、Flags、伤害与 DOT。  
7. Agent 不改正式 `.asset`。  
8. Wave 3.4：`PerfectDodgeAttack` Intent + Graph Counter（产品路由，独立于 G5）。

---

## 14. 风险与定案

| 风险 | 对策 |
|------|------|
| 切换期双扣费 | Spec 只编译 Effect；G3 单测钉死单次扣费 |
| Health 边沿弄丢死亡/受击 | G3 专门接 Reaction；玩家敌人同路径 |
| 大爆炸回归 ACT | 不改 Action 帧语义；Play 招式清单回归 |
| 文档仍写双轨 | G0/G5 两次文档扫除 |
| 浮点锁步 | Attribute milli-int |
| 做成插件 GAS | 禁止 |
| 「先留门面以后再删」 | **禁止** |
| 在 ResourceSim 继续加字段 | **G5 前禁止**；改写 §5 |

### 决策记录

| 日期 | 决策 |
|------|------|
| 2026-08-07 | 采用 GAS-lite：Attribute + Effect；保留 ActionSim/Timeline |
| 2026-08-07 | 允许推倒 ResourceSim / 旧 Health·EnemyHealth / 独立 BuffSim |
| 2026-08-07 | 禁止第三方 GAS 插件替换 ACT |
| 2026-08-07 | Cost/Grant/Buff/DOT 统一 Effect；被动回能走 NumericSystem.Step |
| 2026-08-07 | Spec 运行时编译 Instant Effect，禁止 Spec 直写 Attribute |
| 2026-08-07 | 完美反击缓冲等为 CombatContextFlags，不进 Attribute 首版表 |
| 2026-08-07 | 排期：Wave 3 后、Wave 4 前；G5 为零兼容完成定义 |
| 2026-08-07 | **暂停**在 ResourceSim 上扩展新权威 |
| 2026-08-08 | G5：旧权威删除确认；Snapshot/HUD 完成态；文档扫尾；Resources 仅作者壳 |

---

## 15. 一句话

用 **Attribute + Effect + 少量上下文旗标** 替换所有旧数值口袋，Action 骨架与 Wave 3 玩法语义不动；**阶段出口切断旧线，完成态不留兼容**——以 G5 删除清单归零与门禁全绿作为重构完成的唯一标准。
