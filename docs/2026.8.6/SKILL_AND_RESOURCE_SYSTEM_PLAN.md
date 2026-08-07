# ACTGame 技能 / 资源系统方案（绝区零向）

> 制定：2026-08-06  
> 修订：2026-08-06 — 收敛字段真源到 NUMERICS；同键 EX 定为必做；完美闪避单真源  
> 修订：2026-08-07 — 完美窗改玩家 Dodge Timeline；ResourceSim 标过渡；存储终态指 GAS Numeric  
> 修订：2026-08-08 — GAS G5：数值口袋完成态为 NumericSystem；旧 ResourceSim/Gate 已删  
> 基准：`develop`（`ActionSim` / `ActionGraph` / Intent / `CombatHitPipeline` / `COMBAT_NUMERICS_PLAN`）  
> 产品参考：**绝区零**战斗技能槽 + 能量 / 喧响 / 闪避反击循环  
> **排期真源：** [MASTER_IMPLEMENTATION_PLAN.md](./MASTER_IMPLEMENTATION_PLAN.md)（Wave 3 产品；数值口袋 → [GAS G0～G5](../2026.8.7/GAS_STYLE_COMBAT_REFACTOR_PLAN.md)）  
> **字段 / N* 真源：** [COMBAT_NUMERICS_PLAN.md](../COMBAT_NUMERICS_PLAN.md)  
> **数值口袋终态：** [GAS_STYLE_COMBAT_REFACTOR_PLAN.md](../2026.8.7/GAS_STYLE_COMBAT_REFACTOR_PLAN.md)  
> 关联：[ACTION_DEFINITION_OPTIMIZATION_PLAN.md](./ACTION_DEFINITION_OPTIMIZATION_PLAN.md)、[ACTION_SYSTEM_LOCKSTEP_REFACTOR_PLAN.md](../ACTION_SYSTEM_LOCKSTEP_REFACTOR_PLAN.md)、[CAMERA_SYSTEM_PLAN.md](./CAMERA_SYSTEM_PLAN.md)  
>  
> **文档分工：** 本文只负责技能槽语义、Graph/Intent 路由、产品裁剪与完美闪避规则；**不**另立 `ActionResourceSpec` 字段表或与 NUMERICS 平行的实施阶段。归档的 `COMBAT_RESOURCE_SYSTEM_PLAN` 勿再实施。

---

## 0. 关于「收集全部角色技能描述」

绝区零代理人数量持续增长（数十名），每人技能面板含 **普通攻击 / 闪避 / 支援 / 特殊技 / 连携·终结 / 核心技** 等多条招式文案，且随等级、影画、形态切换变化。  
**逐角色全文收录既不现实，也对框架设计无增益**——真正可复用的是：**全员共用的技能槽位、资源规则、触发条件与描述句式共性**。

本文做法：

1. 以官方/百科战斗系统（萌百、Bilibili Wiki、Prydwen、米游社能量机制解析等）归纳 **全角色统一模板**；  
2. 从大量代理人技能文案中抽取 **反复出现的句式与机制标签**；  
3. 映射到本项目 **ActionDefinition + ActionGraph + ResourceGate + Pipeline**；  
4. 明确 **首版采纳 / 后置 / 不照搬**，避免把三人本与属性异常整包塞进单人 ACT。

资料锚点（非全文转载）：

- [绝区零/战斗 · 萌娘百科](https://zh.moegirl.org.cn/绝区零/战斗)  
- [战斗 · biligame Wiki](https://wiki.biligame.com/zzz/战斗)  
- [Prydwen · Combat System](https://www.prydwen.gg/zenless/guides/combat-system/)  
- 米游社《能量机制完全解析》等社区机制帖  

---

## 1. 结论摘要

1. **绝区零技能不是「每人一套完全不同的系统」**，而是 **统一技能槽 + 资源门槛 + 条件变招**；角色差异主要在数值、Tag、被动与演出。  
2. 本项目应对齐的核心循环：  
   `普攻/反击命中回能 → 能量够放强化特殊技 → 命中攒喧响 → 满喧响放大招 → 闪避有限次数 / 完美闪避接反击`。  
3. **实现落点（产品层）**：招式仍是 `ActionDefinition`；同键 Special/EX、闪避反击等用 **Intent + Graph 路由**，不新开第二套执行器。  
4. **数值口袋**：GAS-lite `NumericSystem` + `NumericCostGate`（G5 完成）；`ActionResourceSpec` 仅为价签并编译为 Instant Effect。新权威字段写入 GAS §5 / Attribute。  
5. **首版（单机单角色 ACT）**：Energy + Decibel + DodgeCharges + PerfectDodge 窗口 + **Special/EX 同键双形态（必做）** + Ult；**暂缓**切人支援、连携技、邦布、属性异常。  
6. **失衡（Daze）** 可作为敌方资源后置（Wave 5），与「重击 Tag」预留接口，不阻塞技能资源主线。  
7. 与 `COMBAT_NUMERICS_PLAN` **合并产品语义**：阶段号 **N0～N5** ≡ 总案 Wave 3；本文 S* 为索引别名。数值改造排期以 **GAS G*** 为准。

---

## 2. 绝区零：全员技能槽共性

### 2.1 统一六类技能（每名代理人都有）

| 技能类 | 子招式（共性） | 输入 | 资源 / 条件 |
|--------|----------------|------|-------------|
| **普通攻击** | 多段连招；部分长按/蓄力/形态普攻 | 攻击键 | 通常无消耗；命中回能、攒喧响/失衡 |
| **闪避** | 闪避（无敌）→ 冲刺攻击；极限闪避 → **闪避反击** | 闪避 / 闪避后攻击 | 闪避有冷却或次数感；极限闪避看攻击闪光窗口 |
| **支援技** | 快速支援、招架/回避支援、支援突击 | 切人键 + 时机 | **支援点**；金闪/红闪提示（本项目首版不做） |
| **特殊技** | **特殊技**（能量不足也可放） / **强化特殊技**（耗能量） | **同一按键** | Energy ≥ 阈值 → EX，否则普通 Special |
| **连携 / 终结** | 连携（失衡+重击+多人）；终结技（喧响满） | 连携选人 / 大招键 | 喧响等级「极」；发动常无敌 |
| **核心技** | 核心被动（自动）；额外能力（队伍条件） | 无主动键 | 被动状态机 / Buff（后置） |

### 2.2 资源与战斗资源共性

| 资源 | 归属 | 获得 | 消耗 | 备注 |
|------|------|------|------|------|
| **能量 Energy** | 角色个人 | 普攻/闪避反击/冲刺攻击等**命中**；接战中自动回复 | **强化特殊技**起手 | 不足时同键放「弱特殊技」 |
| **喧响 Decibel** | 队内共享→1.4 后可个人独立 | 命中与特定招式 | **终结技**清空（或耗满档） | 1000/2000/3000 三档「喧/特/极」 |
| **支援点 Assist** | 队伍 | 连携/终结回复 | 极限支援 | 首版不做 |
| **失衡 Daze** | **敌人** | 受击积蓄 | 失衡状态时间流逝 | 连携触发条件；首版可选 |
| **闪避** | 角色 | 时间充能 / 短 CD | 闪避发动 | 本项目用 DodgeCharges 近似 |

能量机制社区共识（落地时对齐精神即可）：

- 接战才稳自动回能；脱战一段时间停回；  
- 不同招式命中回能值为**配置表固定值**；  
- EX / 连携 / 终结等部分招式本身**不回能**（避免循环刷能）。

### 2.3 技能描述文案的共同点（跨角色）

对大量代理人技能说明归纳，描述几乎总是按同一套「字段」写，而不是自由散文：

| 共性字段 | 文案形态 | 映射到本项目 |
|----------|----------|--------------|
| **发动条件** | 「点按」「长按」「能量足够时」「极限闪避后」「喧响达到极时」 | Intent 相位 + ResourceGate + 上下文 Flag |
| **消耗** | 「消耗能量」「消耗喧响」「消耗支援点」 | `ActionResourceSpec` |
| **获得** | 「命中回复能量」「获得喧响」「积蓄失衡」 | `GrantOnHit` / 敌方 Daze |
| **无敌 / 霸体** | 「招式发动期间拥有无敌效果」 | Timeline 无敌窗口 / 抗打断等级 |
| **伤害类型** | 物理/火/电/冰/以太 + 倍率 | 首版可扁平 `BaseDamage`；属性后置 |
| **打断 / 重击** | 「较高打断」「带有重击效果」 | Action Tag：`HeavyHit` |
| **衔接** | 「后点按普攻发动 XX」「可接闪避反击」 | Graph Cancel / 上下文路由 |
| **形态 / 充能** | 「获得 N 层 XX」「消耗充能强化下一段」 | 角色 `SkillState` 计数器（后置） |
| **队伍条件** | 「队伍存在同属性/同阵营时」 | 额外能力；单机可砍 |

**结论：** 角色差异 = 同一槽位下的 **Action 资产 + Tag + 被动计数**；不要为每个角色写一套代码分支。

### 2.4 同键双形态（Special / EX）——框架级共性

```text
按「特殊技」键
  if Energy >= exCost → 强化特殊技（Spend Energy）
  else                → 普通特殊技（通常 0 能量或低消耗）
```

这是绝区零技能资源设计的核心 UX，也是本项目 **N5 / S2** 必须做成的路由，而不是两个不同按键。

### 2.5 完美闪避 → 反击（框架级共性）

```text
玩家进入 Dodge，Timeline 处于 PerfectDodgeWindow
  → 敌攻击在该窗内命中玩家 = 极限闪避（吞伤 / 可选慢动作）
  → 武装反击缓冲 → 缓冲内出 PerfectDodgeAttack Intent → Counter Action
```

与「消耗闪避次数的普通闪避」是两条线：次数 Gate + 玩家 Dodge 窗 + 反击缓冲旗标。

---

## 3. 对本项目的产品裁剪

| 绝区零机制 | 本项目首版 | 理由 |
|------------|------------|------|
| 普攻多段 + 命中回能 | ✅ | 已有 Graph/连招 |
| Special / EX 同键 | ✅ | 资源系统核心 |
| Ult + 喧响 | ✅ | 大招循环；单机喧响归角色个人 |
| 闪避次数 / CD | ✅ DodgeCharges | 近似闪避压力 |
| 极限闪避 + 闪避反击 | ✅ | ACT 手感刚需 |
| 冲刺攻击 | ✅ 可选 | 已有 Sprint/Dodge 上下文 Intent |
| 切人 / 支援点 / 支援技 | ❌ 后置 | 无多角色编队前不做 |
| 连携技 | ❌ 后置 | 依赖失衡+多人；可先做「破防一击」简化 |
| 敌人失衡 | ⚪ S3 可选 | 与 HeavyHit Tag 预留 |
| 属性异常 / 紊乱 | ❌ 后置 | 数值膨胀大 |
| 核心被动 / 额外能力 | ⚪ 极简 Buff | 先做 1～2 个通用被动槽 |
| 邦布 | ❌ | 非目标 |
| 大招定制镜头 | ✅ 跟 CAMERA C3 | SkillShot 多段 |

**单机喧响定案：** 采用「角色个人喧响条」（接近绝区零 1.4+ 个人喧响思路），避免假造全队共享。

---

## 4. 目标架构（叠在现有动作核上）

```text
InputFrame
  → GameplayIntentProducer（Attack / Special / Ult / Dodge / PerfectDodgeAttack …）
  → CharacterActionDriver
       → Gate.CanAfford / CommitCost（`NumericCostGate`）
       → ActionGraph / Resolver（含 Special↔EX、PerfectDodgeAttack→Counter）
  → ActionSim（整数帧 Timeline：含 PerfectDodge / Invincible）
  → Collect Hits
  → CombatHitPipeline.Resolve
       → 完美窗/无敌早退 或 伤害
       → GrantOnHit（仅 ConfirmHit；Instant Grant Effect）
       → NotifyHit / Reaction
  → Step 被动回能/充能（NumericSystem）
```

**成熟框架对齐点：**

| 层 | 职责 | 禁止 |
|----|------|------|
| Intent | 「玩家想干什么」 | 不直接扣资源 |
| Gate | 「付不付得起、付哪招」 | 不播动画 |
| Graph/Action | 「播哪招、窗口、Cancel」 | 不在 Collect 改资源 |
| 数值口袋 | 资源/旗标权威（`NumericSystem`） | 不读 `Time.deltaTime`；禁止第二套口袋 |
| Pipeline | 命中副作用顺序 | App Command 旁路扣费/扣血 |

---

## 5. 技能槽 → 本项目数据映射

### 5.1 `SkillSlot` / `ActionResourceTag`

```text
enum ActionResourceTag :
  Basic,          // 普攻段
  DashAttack,     // 冲刺攻击
  Special,        // 弱特殊技
  ExSpecial,      // 强化特殊技
  Ultimate,       // 终结技
  Dodge,          // 普通闪避
  PerfectDodge,   // 极限闪避（可为同一 Dodge Action 的窗口态）
  DodgeCounter,   // 闪避反击
  HeavyHit,       // 重击标记（可与上列叠）
  Utility         // 其他
```

挂在 `ActionDefinition.resourceSpec.tag`（或并列 tags）。

### 5.2 Intent 扩展（定案）

| Intent | 说明 |
|--------|------|
| `Attack` | 已有；可路由冲刺攻击等（**不**再兼完美反击选形） |
| `Special` | 已有；Producer 不区分 EX，由 Gate+Selector 分支 |
| `Ultimate` | 已有；需喧响满档 |
| `Dodge` | 已有 |
| `PerfectDodgeAttack` | **完美反击专用 Intent**；条件含 `HasPerfectDodgeCounter`；Graph Entry 指向 Counter |

`GameplayIntentProfile`：Special / Ultimate / PerfectDodgeAttack（高优先级）绑定。

### 5.3 Graph 路由规则（定案）

```text
Special Intent:
  if Gate.CanAfford(ExSpecialSpec) → 选 ExSpecial Action，Commit energyCost
  else → 选 Special Action（energyCost=0）

PerfectDodgeAttack Intent（HasPerfectDodgeCounter）:
  → DodgeCounter / Counter Action（Entry.Intent = PerfectDodgeAttack）

Attack Intent + Sprint/DodgeForward 上下文:
  → DashAttack Action（可选）

Ultimate Intent:
  if Decibel >= max → Ult Action，clearsDecibelOnStart
  else → 忽略或 UI 提示（不扣费）
```

Cancel 切招：按 **目标招** 的 Spec 再 `CanAfford`；不够则 **不 Begin**，缓冲保留至过期（与 NUMERICS 定案一致）。  
**禁止**在 Attack Intent 上再叠一套「有 Counter Tag 则换招」双轨选形。

### 5.4 `ActionResourceSpec`

**字段表以 NUMERICS §6.2 为准**，此处不重复、不加肥。约定：

- EX/Ult「命中不回能」→ `energyGrantOnHit = 0`，不另增布尔。  
- 无敌 / Poise → Timeline 窗口，不进 Spec。  
- `HeavyHit` → `ActionResourceTag` / 独立 Tag，供 Wave 5 失衡使用。  
- 完美闪避窗口 → **玩家 Dodge Timeline**（见 §5.6），不进 Spec。

### 5.5 数值口袋（完成态）

| 项 | 实现 | 说明 |
|------|------|------|
| 权威 | `NumericSystem` + Flags + `NumericCostGate` | 见 GAS §5；G5 已删除 ResourceSim/旧 Health |
| 价签 | `ActionResourceSpec` | 运行时只编译 Instant Cost/Grant Effect |

`Step`：仅逻辑帧；`freezeFrames>0` / `ActionSim.IsFrozen` 时暂停被动回能与闪避充能。

### 5.6 完美闪避窗口（单真源 · 玩家 Dodge）

**逻辑唯一真源：玩家 Dodge Action Timeline 上的 `PerfectDodgeWindowNotifyState`（相对玩家逻辑帧）。**

1. 玩家 Dodge 进入完美窗；窗内被敌攻击命中 → Pipeline **吞伤**（不写 Health、不 Grant）。  
2. 武装 `PerfectDodgeCounterFrames`（反击缓冲）；可选慢动作表现事件。  
3. 缓冲内输入派生 `PerfectDodgeAttack` → Graph 出 Counter；起手清空缓冲。  
4. 普通 i-frame 由 Timeline `Invincible` 相位消费；**完美窗优先于无敌早退语义**（完美：吞伤+武装；无敌：仅吞伤）。

**不做**：敌方攻击 Timeline 广播「可完美闪避」作为第二权威；不做「受击前 N 帧」第二套来源。  
表现层：子弹 / 闪光仅表现。**不必先做完整闪光 UI。**

---

## 6. 与 Action 系统的装配

| 现有模块 | 改动 |
|----------|------|
| `ActionDefinition` | 挂 `ActionResourceSpec`；Ult 可挂 `CameraShotSequence` |
| `ActionGraph` | Special 入口用 Route：Energy 分支；Attack 增加 DodgeCounter / Dash 条件边 |
| `GameplayIntentProducer` | Special / Ultimate Intent |
| `CharacterActionDriver` | Begin 前 Gate |
| `ActionSim.CommitPendingDecision` | 二次 CanAfford，防 Cancel 偷放 |
| `CombatHitPipeline` | 完美窗/无敌早退；ConfirmHit 后 Grant |
| `CharacterActor` | 每帧数值 Step；暴露 Debug Snapshot |
| `CharacterConfig` | 资源字段经 `CharacterNumericConfig.FromResourceConfig` 灌入 Numeric |

**不新建** `SkillExecutor`；技能 = 带资源 Spec 的 Action。

---

## 7. 分阶段实施（映射总案 Wave 3 / 5）

> **排期以总案 Wave 为准**；下列 S* 仅索引 NUMERICS N*。勾选请同步总案与 NUMERICS。

| 本文索引 | NUMERICS | 总案 | 内容 | 必做？ |
|----------|----------|------|------|--------|
| S0 | N0 | Wave 0/3 | Debug HUD | 是 |
| S1 | N1 | Wave 3 | Energy + Gate + GrantOnHit | 是 |
| S2 | N5 | Wave 3 | Special/EX 同键 | **是（升格）** |
| S3 | N3 | Wave 3 | DodgeCharges + 完美闪避反击 | 是 |
| S4 | N4 | Wave 3 | Decibel + Ult | 是 |
| S5 | — | Wave 5 | 敌方 Daze + HeavyHit | 可选 |
| S6 | — | 后置 | 被动 / 支援 / 属性异常 | 后置 |

建议 Wave 3 内顺序：`N0 → N1 → N5(同键) → N3 → N4`（先打通 EX 路由，再加压闪避与大招）。  
Ult 的 SkillShot 镜头属 Camera C3 / **Wave 5**，不阻塞 S4 逻辑清条。

---

## 8. 内容配置规范（策划表）

每条主动技能 Action 至少填：

| 列 | 示例 |
|----|------|
| slot/tag | ExSpecial |
| energyCost | 60 |
| energyGrantOnHit | 0 |
| decibelGrantOnHit | 80 |
| consumeDodge | false |
| heavyHit | true |
| invuln frames | Timeline 窗口 |
| 备注 | 对齐绝区零「强化特殊技通常高伤高打断」 |

普攻每段单独配 `energyGrantOnHit`（可不同）。  
EX/Ult 默认 `grantsEnergyOnHit=false`。

---

## 9. 测试计划

| 用例 | 类型 |
|------|------|
| 能量不足 → Special；充足 → EX | Play / EditMode |
| Cancel 到 EX 时不够费用 → 不切换 | EditMode |
| 命中 Grant；Collect 阶段无 Grant | EditMode |
| 闪避耗尽不能闪；充能后可闪 | Play |
| 完美窗口外 Attack 不进 DodgeCounter | Play |
| Ult 清喧响；未满不能 Ult | Play |
| 卡肉冻结时回能/充能是否按产品定案暂停 | 定案后测 |

**定案建议：** `freezeFrames>0` 时暂停被动回能与闪避充能，与绝区零「停帧手感」接近。

---

## 10. 风险与对策

| 风险 | 对策 |
|------|------|
| 照搬三人本把范围撑爆 | S0～S4 单角色闭环；支援/连携后置 |
| 每角色写死 if-else | 只扩 Tag + Spec + Graph 条件 |
| 回能与 EX 循环炸经济 | EX/Ult 不回能；回能表走配置 |
| Gate 与 Graph 双真源 | 费用只认 Spec；Graph 只选节点 |
| 与 NUMERICS 文档重复 | 字段与 N* 只认 NUMERICS；本文只管槽位/路由；排期认总案 Wave |
| 完美闪避依赖复杂闪光 | 先玩家 Dodge 逻辑窗 + Pipeline 早退，表现后补 |
| 文档仍写敌方窗权威 | 以 §5.6 / MASTER 裁定为准 |
| ResourceSim 被当作终态 | 已删除；权威仅 NumericSystem |

---

## 11. 明确非目标

- 不逐条移植绝区零全部代理人技能文案与倍率  
- 不实现邦布、秽息、控制技紫闪等版本特化机制  
- 不在 Collect/检测层扣费或回能  
- 不恢复 `ApplyHitCommand`  
- Agent 不直接改 `.asset` 数值；给表与 Editor 步骤  

---

## 12. 决策记录

| 日期 | 决策 | 理由 |
|------|------|------|
| 2026-08-06 | 以绝区零**技能槽+资源共性**为模板，不逐角色抄文案 | 可落地、可扩展 |
| 2026-08-06 | Special/EX 同键双形态为首版必做 | 资源 UX 核心 |
| 2026-08-06 | 喧响归单角色个人条 | 无编队时的诚实模型 |
| 2026-08-06 | 支援/连携/异常后置 | 服务单机 ACT 主线 |
| 2026-08-06 | 技能=Action+Spec+Gate，不新建 SkillExecutor | 符合现有动作核 |
| 2026-08-06 | 与 NUMERICS N* 并行，技能阶段用 S* | 避免文档打架 |
| 2026-08-06 | N5 同键 EX 升格必做；Spec 不加肥 | 与总案 Wave 3 对齐 |
| 2026-08-06 | 完美闪避单真源（初稿：敌方 Timeline） | 消灭双权威 |
| 2026-08-07 | **改定**：完美窗在玩家 Dodge Timeline；Intent=`PerfectDodgeAttack` | 与产品/Pipeline 设计对齐 |
| 2026-08-07 | ResourceSim 为 Wave 3 过渡；终态 NumericSystem | 对齐 GAS-lite |

---

## 13. Editor 人工步骤（实现后）

1. `CharacterConfig` 填 Energy/Decibel/Dodge 默认值。  
2. 普攻各段填 `energyGrantOnHit`；做一条 EX（cost）与一条 Special（0）。  
3. Graph：Special Intent → Energy 路由；**Counter Entry Intent=`PerfectDodgeAttack`**（条件 `HasPerfectDodgeCounter`）。  
4. Dodge Action：配 `Invincible` 与/或 `PerfectDodgeWindow` 轨；ActionType=Dodge。  
5. Profile：Pressed+HasPerfectDodgeCounter → PerfectDodgeAttack（高优先级）。  
6. 一条 Ult：`requiresDecibelFull` + `clearsDecibelOnStart`；可选 CameraShotSequence。  
7. 挂 Debug HUD，跑 S1～S4 验收清单。  

---

## 14. 成功标准

- [x] 玩家必须消耗资源才能放强化技 / 大招（Gate 生效；资产填 cost 后验收）  
- [x] 同键 Special/EX 行为符合绝区零心智模型（代码选形就绪；Graph 双 Entry 人工）  
- [x] 命中回能/喧响仅在 Pipeline ConfirmHit  
- [ ] 闪避有限 + 可完美反击（次数就绪；**玩家 Dodge 窗 + PerfectDodgeAttack 待 Wave 3.4**）  
- [x] 技能差异主要靠 Action 资产与 Tag，而非角色硬编码  
- [x] Debug HUD 可观测全部资源与当前路由结果（Next Special）  

---

## 15. 一句话

绝区零教给我们的不是「一百套独特技能脚本」，而是 **统一技能槽 + 能量/喧响/闪避门槛 + 同键强化 + 完美闪避反击**；本项目用 **Intent/Graph 路由 + Gate** 叠在 `ActionSim` 上，数值口袋为 GAS Numeric，先做单角色完整循环，再考虑失衡、编队与异常。
