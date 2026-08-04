# 战斗数值总案 — 属性 / 伤害 / 玩法资源 / Debug HUD

> 状态：**方案待实施（合并版）**  
> 创建：2026-08-04  
> 合并自：  
> - [COMBAT_ATTRIBUTES_DAMAGE_PLAN.md](./COMBAT_ATTRIBUTES_DAMAGE_PLAN.md)（归档）  
> - [COMBAT_RESOURCE_SYSTEM_PLAN.md](./COMBAT_RESOURCE_SYSTEM_PLAN.md)（归档）  
> 关联锁步：[ACTION_SYSTEM_LOCKSTEP_REFACTOR_PLAN.md](./ACTION_SYSTEM_LOCKSTEP_REFACTOR_PLAN.md)

---

## 1. 结论摘要

1. **一份真源**：属性/伤害与玩法资源（EX、喧响、闪避）共用 `CombatHitPipeline` 帧末结算；禁止再引入 `ApplyHitCommand` 旁路扣血。
2. **现状已有**：`CharacterHealth` + `HitPayload.BaseDamage` + `CombatDamageCalculator` 已打通「命中 → 扣血 → Hit/Death」；本方案在其上演进，而非从零重做。
3. **待补**：ATK/DEF/`AttributeSheet`（可选增强）、绝区零式玩法资源 + Gate、左上角 Debug HUD。
4. **落地顺序**：先可观测（HUD）→ 再资源闸门 → 再属性公式增强（避免无观测联调）。

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
| 木桩无扣血 | `CharacterHurtboxTarget` → `CharacterHealth.ApplyDamage` |
| 招式 `damageMultiplier` + Attack | 首版伤害唯一来自 `HitPayload.BaseDamage` |
| Domain 经 Command 编排扣血 | **禁止** App Command 改 HP；只发布已结算结果 |

---

## 3. 现状（2026-08 代码真源）

```text
SimulationWorld.Step
  → Actors Collect（HitDetector → Pipeline.Collect）
  → Pipeline.ResolveBeforePostCombat
       → target.OnHit(context)
            → CombatDamageCalculator.Calculate → HitPayload.BaseDamage
            → CharacterHealth.ApplyDamage
       → hitReceiver.NotifyHit
  → PostCombatActors
  → Pipeline.CompleteFrame → PublishResolvedHit（App 只读反馈）
```

| 模块 | 状态 |
|------|------|
| `CharacterHealth` / `EnemyHealth` | ✅ 扣血与死亡边沿 |
| `CombatDamageCalculator` | ✅ 扁平 `BaseDamage` |
| `HitPayload` | ✅ damage / reactionId / feedback |
| `AttributeSheet` / ATK·DEF 公式 | ⬜ 未做 |
| `CharacterResourceSim` / Gate | ⬜ 未做 |
| Debug HUD / `CharacterDebugSnapshot` | ⬜ 未做 |
| IntentBuffer / TargetLock 对外暴露 | ⬜ 工厂局部，HUD 读不到 |

---

## 4. 统一架构

### 4.1 目录

```text
Assets/Scripts/Domain/Combat/
  Damage/                    # 已有 CharacterHealth、CombatDamageCalculator
    （演进）AttributeId.cs / AttributeSheet.cs / AttributeProfile.cs
    （演进）DamageRequest.cs / DamageResult.cs  — 若公式复杂化再拆
  Resources/                 # 新增
    ResourceId.cs
    CharacterResourceSim.cs
    CharacterResourceConfig.cs
    ActionResourceSpec.cs
    IActionResourceGate.cs / ActionResourceGate.cs
  Hitbox/CombatHitPipeline.cs  # 结算顺序扩展点

Assets/Scripts/Domain/Character/
  CharacterDebugSnapshot.cs

Assets/Scripts/App/Controllers/Debug/
  CombatDebugHudController.cs
```

### 4.2 分层铁律

- Domain：`AttributeSheet` / `CharacterResourceSim` / Gate / Calculator — **零** Architecture 引用。
- Pipeline 帧末：扣血 →（有效命中则）资源 Grant → NotifyHit；Collect 阶段禁止副作用。
- App：`PublishResolvedHit` / Debug HUD **只读**；禁止 Spend/Grant/ApplyDamage。

### 4.3 帧末结算顺序（定案）

```text
对每条 pending hit（SimHitKey 排序后）:
  1. 若目标已死 → skip 权威副作用（仍可按现有策略处理）
  2. damage = Calculator(...) → target.Health.ApplyDamage
  3. 若本次为有效几何确认（NotifyHit 路径）:
       attacker.ResourceSim.GrantOnHit(action.ResourceSpec)
  4. hitReceiver.NotifyHit
CompleteFrame → 发布 ResolvedCombatHit（表现）
```

攻击者资源查找：Pipeline 注入 `Func<SimActorId, CharacterResourceSim>`（或等价注册表），禁止经 App Command 回写。

---

## 5. 块 A — 属性与伤害（演进）

### 5.1 第一版保持（已落地）

```text
finalDamage = max(0, HitPayload.BaseDamage)
CharacterHealth.ApplyDamage(finalDamage, context)
```

0 伤仍可 `HitReceived`（现有行为保留）。

### 5.2 增强目标（N2，按需）

当需要角色成长 / 防御减伤时，引入：

| Id | 说明 |
|----|------|
| `MaxHp` / `Hp` | 生命；Hp 不参与修饰公式 |
| `Attack` / `Defense` | 攻防 |

```text
raw = Attack * hitPayloadScale *（可选 weight）
mitigation = Defense / (Defense + K)   // K=100
final = raw<=0 ? 0 : max(1, raw * (1 - mitigation))
```

- `AttributeProfile` SO + `AttributeSheet`（base + Flat/PercentAdd 修饰器）。
- `CharacterHealth` 可改为 Sheet 的 Hp 门面，或 Sheet 内聚 Hp — **禁止** float 血条与 Sheet 双轨。
- `HitPayload.BaseDamage` 语义可收束为「框系数/固定段伤」之一；迁移时二选一写死，删另一语义。
- **不**恢复 `ApplyHitCommand`；公式仍在 Domain，由 Pipeline/`OnHit` 调用。

### 5.3 木桩

`HurtboxTarget` 若仍存在：与角色同源 `CharacterHealth`（或 Sheet），删除孤立业务语义血量双轨。

---

## 6. 块 B — 玩法资源（绝区零骨架）

### 6.1 资源表

| 资源 | 归属 | 用途 | 首版规则 |
|------|------|------|----------|
| **Energy（EX）** | 角色 | 强化特殊技 | max=120；接战 milli/帧回能；命中 Grant；起手 Spend |
| **Decibel（喧响）** | 角色（单机个人条） | 终结技 | max=3000；命中增加；大招清空 |
| **DodgeCharges** | 角色 | 闪避 | max=2；耗 1；`rechargeFrames=60` |

权威时钟：仅 `ResourceSim.Step`（1 逻辑帧）；禁止 `Time.deltaTime` / OnGUI 改资源。

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
| `CharacterActor.Step` | `ResourceSim.Step`（被动回能 + 闪避充能） |
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
需暴露：`IntentBuffer`、`TargetLock`、`ResourceSim`、Health；`GameplayIntentBuffer.CopyBufferedForDebug(...)`。

刷新：`LateUpdate` 采样缓存，`OnGUI` 只绘制。禁止 HUD 写 Sim。

---

## 8. 分阶段实施（统一编号）

### Phase N0 — 调试可观测（无新资源逻辑）

- [ ] Buffer 调试枚举；Actor 暴露 IntentBuffer / TargetLock / Health  
- [ ] `CharacterDebugSnapshot` + `CombatDebugHudController`  
- [ ] 面板显示：State、Action 帧、HP、FrameIntents、Buffers、Lock、Motor  

**验收：** Play Mode 攻击可见缓冲/锁定/掉血数字变化。

### Phase N1 — Energy + Gate + 命中回能

- [ ] `CharacterResourceSim` / Config / `ActionResourceSpec`  
- [ ] Gate → Driver + ActionSim  
- [ ] Pipeline GrantOnHit；接战被动回能  
- [ ] HUD 显示 EX  

**验收：** `energyCost` 不够不起手；普攻命中回能；HUD 同步。

### Phase N2 — 属性公式增强（按需，可与 N1 并行后置）

- [ ] `AttributeSheet` / Profile；Attack/Defense  
- [ ] Calculator 升级；Health 与 Sheet 单轨  
- [ ] HUD 显示 Attack/Defense（可选）  

**验收：** 改攻防影响伤害；无双轨血量。

### Phase N3 — 闪避充能

- [ ] DodgeCharges + recharge；Dodge Action 消耗  
- [ ] HUD 次数与充能帧  

### Phase N4 — 喧响

- [ ] Decibel 累加 / 满值门槛 / 大招清空  
- [ ] HUD 喧响  

### Phase N5 — 同键双形态（可选）

- [ ] Special 意图能量分支；HUD 标注下一发 EX/普通  

---

## 9. 装配改动清单

| 位置 | 改动 |
|------|------|
| `CharacterConfig` | 嵌 `CharacterResourceConfig`；N2 时加 `AttributeProfile` |
| `CharacterActorFactory` | 创建 ResourceSim/Gate；Buffer/Lock/Health 注入 Actor |
| `CharacterActor` | 只读暴露 + `BuildDebugSnapshot` + `ResourceSim.Step` |
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

在现有 **Pipeline + CharacterHealth + HitPayload** 上，用 **ResourceSim/Gate** 补绝区零式循环，用 **AttributeSheet（按需）** 升级攻防，并用 **OnGUI Debug Snapshot** 把数值与输入状态打到左上角；先 N0 看见，再 N1～N5 逐项接真逻辑。
