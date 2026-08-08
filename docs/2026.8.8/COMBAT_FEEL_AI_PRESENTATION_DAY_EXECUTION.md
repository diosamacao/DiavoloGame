# 战斗表现 / AI / 木桩 — 今日执行方案（2026-08-08）

> 依据：[COMBAT_FEEL_AI_PRESENTATION_DAY_OUTLINE.md](./COMBAT_FEEL_AI_PRESENTATION_DAY_OUTLINE.md)  
> 角色：**可开工任务单**（谁做、做到哪、怎么验收）；大纲仍管范围与非目标  
> 前置：A0 ✅；GAS G5 ✅；Wave 3.4 ✅；Wave 2.5 删 RM ✅

---

## 0. 今日一句话

以**木桩验收台**为中心：先能稳定验伤/停/震，再接通命中音画与完美闪避子弹，相机只调参；午后做 **BT Phase-1 等价替换**（只写 `InputFrame`）。

---

## 1. 分工与就绪

| 轨 | 就绪 | Agent（代码） | 你（Editor） |
|----|------|---------------|--------------|
| **A1 木桩** | ✅ 验收 2026-08-08 | `enableCombatActions` + Hit_Shake | Monster_EDF 高 HP + 场景 |
| **A2 命中 Cue** | 代码通道 ✅；资产待绑 | 已做：`HitImpactController` | 普攻 Feedback 绑 VFX/SFX；刀光 Timeline；SS9 色系新 Prefab |
| **A3 子弹** | 权威齐、表现零 | **必做**：完美吸收只读事件 + 短减速 | 调时长；用现敌测窗 |
| **A4 相机** | Shake 已接 | 可选：lateral 运行时微调 API | **必做**：Shake / `lateralFollowFactor` 木桩调参 |
| **A5 BT** | 代码 0 | **必做**：骨架 + 删五态决策双轨 | 挂树资产 / 只追不打变体 |

**硬约束（全天）：** 不碰 Wave 4 Lock-On；不新增血量/资源口袋；Domain 不直调 Audio/VFX；BT 不 `TryStart` / 不改 Numeric。

---

## 2. 推荐时间盒

| 时段 | 轨 | 交付 |
|------|-----|------|
| **T0 · 0.5h** | A1 Editor | 木桩可挨打；F3 见 HP；受击/HitStop/震屏能看见 |
| **T1 · 1.5～2h** | A2 代码 + Editor | ≥1 段普攻：命中火花+音；挥空无 Cue |
| **T2 · 1～1.5h** | A3 代码 + 手感 | 完美窗可感知减速；权威结果不变 |
| **T3 · 0.5h** | A4 Editor | 连打镜头稳、命中有反馈 |
| **T4 · 2～3h** | A5 代码 + Editor | 近战敌 BT 追+打；受击/死亡不回归 |
| **T5 · 0.5h** | 收工 | 大纲 §7 勾选；债记入本文 §6 |

A5 不阻塞 A1～A4；若下午时间紧，A5 可降级为「骨架 + EditMode + 单敌挂树」，完整手感明日补。

---

## 3. 分轨执行单

### 3.1 A1 — 木桩（先做）

**目标：** 固定站桩验收 Numeric + Reaction + HitStop + 震屏。  
**形态定案：** 复用敌人装配链（`EnemyDefinition` → `EnemyActorFactory` → `CharacterActor` + Vitality + Hurtbox）；**无追打**，不新建第二套血量。

#### 3.1.0 你已有资产 vs 缺口（2026-08-08 扫描）

| 资产 | 路径 | 现状 |
|------|------|------|
| `Monster_EDF` | `Assets/Data/Enemy/Monster/` | ❌ `characterConfig` / `brainProfile` 空；`maxHp=100` 偏低 |
| `MonsterConfig` | `Assets/Data/Combat/Actions/Monster/` | ❌ `modelPrefab`、Locomotion Profile、Intent、CombatProfile 均空；Reactions 无规则 |
| `MonsterAnimationSO` | 同上 | 🟡 仅绑了 1 条 Clip；校验要求 **Idle+Walk+Run** |
| `MonsterLocomotion` | 同上 | 🟡 参数在；脚步/RM 可后补 |
| `Monster_Goblin_Ani_Hit_Stay` | `.../ActionDefinition/` | ✅ 可作默认受击 Action |
| 旧 `EnemyDefinition` | `Assets/Data/Enemy/` | ✅ 已挂 `UnagiConfig` + `maxHp=10000` + Brain `aggroRadius=0` → **可当临时木桩** |

```text
木桩最小依赖图

Monster_EDF (EnemyDefinition)
├─ BrainProfile_Dummy          aggroRadius=0
├─ maxHp ≥ 99999（或 10000）
├─ teamId = 1
└─ MonsterConfig (CharacterConfig)
   ├─ modelPrefab              Goblin 模型 Prefab
   ├─ MonsterAnimationSO       Idle/Walk/Run（站桩可三键同 Idle Clip）
   ├─ MonsterLocomotion        CharacterLocomotionProfile
   ├─ GameplayIntentProfile    可复用敌人/玩家 Intent（工厂会警告缺 Attack，木桩可忽略）
   ├─ CombatModeProfile        最小 ActionGraph（可无攻击 Entry，但引用不能空）
   └─ Combat.reactions
      └─ Hit 默认规则 → Monster_Goblin_Ani_Hit_Stay
         （Death 可选：超高血不测；或另绑 Die Action）
```

#### 3.1.1 两条路径

| 路径 | 何时用 | 做法 |
|------|--------|------|
| **P0 临时验收** | 今天先验打击感 | 场景 `EnemySpawnController` 直接刷现有 `EnemyDefinition`（Unagi 皮 + 高 HP + aggro0） |
| **P1 正式怪物木桩** | 用你新建的 Monster 线 | 按下方 Editor 清单补齐引用后，刷 `Monster_EDF` |

建议：**P0 先通一遍验收清单**，同时按 P1 填 Monster，避免堵在模型/Graph 上。

#### 3.1.2 Editor 清单（P1 · Monster 木桩）

按顺序做；Agent **不改** `.asset`。

1. **模型**  
   - 指定 Goblin（或占位）`modelPrefab` → `MonsterConfig.Model Prefab`。  
   - 调 `controllerHeight/Radius/Center` 与 Hurtbox `size/offset` 贴合体型。

2. **Locomotion 动画**  
   - `MonsterAnimationSO`：补齐 **Idle / Walk / Run**（木桩可三键拖同一 Idle Clip）。  
   - `MonsterConfig.Default Locomotion Profile` → `MonsterAnimationSO`；`Locomotion Profile` → `MonsterLocomotion`。

3. **Intent + 出招表（校验必填，木桩可不攻击）**  
   - `GameplayIntentProfile`：可复用现有敌人/玩家 Profile。  
   - 新建最小 `ActionGraph`（可无 Attack Entry）+ `PlayerActionSet` + `CombatModeProfile`（Default 条目指向该 Set）。  
   - `MonsterConfig.Combat Profile` 挂上。

4. **受击**  
   - `MonsterConfig.Combat.Reactions`：一条 **Hit + IsDefault** → `Monster_Goblin_Ani_Hit_Stay`。  
   - 确认该 Action 60Hz、有 Clip；Hurtbox 窗口非必须（受击方）。  
   - Death：首版可不配（靠超高 HP）；若要测死亡再补 Die Action + 默认 Death 规则。

5. **Brain（站桩）**  
   - 新建或复制 `EnemyBrainProfile` → `BrainProfile_MonsterDummy`：`aggroRadius = 0`（现网 Idle 永不进 Chase）。  
   - 可选稍后加代码开关（见 3.1.3），首版不依赖。

6. **Definition**  
   - `Monster_EDF`：`Character Config` → `MonsterConfig`；`Brain Profile` → Dummy；`Max Hp` → `99999`；`Display Name` → `Dummy` / `GoblinDummy`；`Team Id = 1`。

7. **场景**  
   - `EnemySpawnController` 增加 Entry：`definition = Monster_EDF`，固定出生点。  
   - Play：普攻连段 → F3 HP/Grant；看受击、HitStop、震屏。

#### 3.1.3 可选代码（P1 填完后仍不稳再做）

| 改动 | 用途 |
|------|------|
| `EnemyDefinition.aiMode = Combat / Dummy` 或 `disableCombatAi` | Dummy：Brain.Step 直接 return / 不写移动与攻击（比 `aggroRadius=0` 语义更硬） |
| 调试「重置 HP」按钮或重刷 | 倒地后快速复测；仍走 Vitality，禁止旁路扣血 |

**禁止：** 第二套 Health；木桩专用 HitDetector；BT 挂在 Dummy 上。

#### 3.1.4 验收

大纲 §3.2；另加：

- [x] Console 无 `EnemyDefinition` / `CharacterConfig` 校验 Error  
- [x] 木桩不追人、不出招（`enableCombatActions=false`）  
- [x] 受击播 `Hit_Shake`（Default Reaction）  
- [x] F3 `MaxHp` 为 Definition 覆盖值（非 Config 默认 100）

---

### 3.2 A2 — 命中 VFX / SFX

**目标：** 命中看得见、听得见；挥空不播。

**定案（本日锁定）：**

| Cue | 通道 |
|-----|------|
| 起手刀光 / 脚步 | Timeline `PlayVfx` / `PlaySfx` |
| 命中火花 / 命中音 / 震屏 | `HitPayload.Feedback` → Confirm 后 `AttackHitEvent`（或等价只读 Cue）→ **App** 播放 |
| HitStop | 仅 `freezeFrames` 逻辑权威；表现跟帧 |

**Agent 任务：**

1. ~~扩展 `HitFeedbackSettings`：命中 VFX Prefab、SFX Clip（可空）。~~ ✅  
2. ~~App 层订阅命中事件播 Cue~~ ✅（`HitImpactController`；PD 吞伤跳过）  
3. ~~Collect 不播；卡肉跟攻击者 Owner 暂停粒子~~ ✅  

**Editor（当前卡点）：**

1. 1～2 段普攻 Hitbox → Feedback：`Hit Impact Vfx Prefab` + `Hit Impact Sfx`  
2. 刀光仍走 Timeline `PlayVfx`；受击火花走 Feedback（勿塞进 Collect）  
3. **刀光色系 SS9 新资产**（见下方 §3.2.1）做完后，把 Timeline 引用切到 `_SS9` Prefab  

**验收：** 大纲 §3.3；Domain HitDetector 无 Audio/VFX 引用。

#### 3.2.1 Editor：刀光对齐 Sword Slash 9（人工，Agent 不改 Prefab/Mat）

**色板真源**（`Sword Slash 9` / 已有 `M_slashD_wind_SS9`）：

| 用途 | RGB（约） |
|------|-----------|
| 主色 `_Color` / gradient key0 | `(0.335, 0.537, 1.0)` |
| 深蓝 key1 | `(0.222, 0.308, 1.0)` |
| 浅蓝辅色 | `(0.467, 0.523, 1.0)` |

**源 → 目标（复制后改色，勿改原包）：**

| 源 Prefab | 已输出 ✅ |
|-----------|----------|
| `Slash_D_Quintuple` | `Assets/Art/VFX/Prefabs/SwordSlash9/Slash_D_Quintuple_SS9.prefab` |
| `Slash_Aoe_A_Dstyle` | `.../SwordSlash9/Slash_Aoe_A_Dstyle_SS9.prefab` |
| `Slash_B_BlueSlash` | `.../SwordSlash9/Slash_B_BlueSlash_SS9.prefab` |
| `Slash_Aoe_A_Ball_Blue` | `.../SwordSlash9/Slash_Aoe_A_Ball_Blue_SS9.prefab` |

材质在 `Assets/Art/VFX/Materials/Slash_SwordSlash9/`（`_Color` = SS9 蓝）；粒子青蓝已 remap。  
Unagi `Attack_05/06/Branch_01/02`（含 Perfect）Timeline 已改指 `*_SS9`。  
**仍需人工：** 受击火花挂 Feedback；Scene 确认 `FeedbackController` + `VFXManager`；Play 目视色差。

---

### 3.3 A3 — 完美闪避子弹时间

**目标：** 完美吸收有减速感；不改吞伤 / Grant / Counter 缓冲权威。

**Agent 任务：**

1. Pipeline 完美吸收成功时发**只读**事件（新建 `PerfectDodgeAbsorbEvent` 或扩展现有 hit 事件旗标）。  
2. App `BulletTimeController`（名可再定）：短时表现减速。  
   - **优先：** 独立 Presentation clock / 动画与 VFX 速率，避免长期依赖全局 `Time.timeScale` 拖慢 `SimulationHost`。  
   - 若首版必须用 `timeScale`：必须确认 Host 仍按真实时间累加逻辑步，或改为 Host 不受影响的时钟源；并在 TECHNICAL 写清。  
3. 恢复条件：超时 / Counter 起手 / 玩家受击抢占。  
4. 与逻辑 HitStop：**可叠表现，不双写 `freezeFrames`**。

**Editor / Play：** 用会出伤的敌人打玩家 PerfectDodge 窗；对照 F3 `PDCounter`。

**验收：** 大纲 §3.4.2。

---

### 3.4 A4 — 相机（轻量）

**做：** 木桩上调 `CameraShakeProfile`、`CameraManager.lateralFollowFactor`；可选完美吸收短 Impulse（订 A3 事件）。  
**不做：** CameraDirector / Lock-On / SkillShot。

**验收：** 连打稳、命中有反馈、不穿模到不可玩。

---

### 3.5 A5 — BT Phase-1

**真源：** [`ENEMY_BEHAVIOR_TREE_PLAN.md`](../ENEMY_BEHAVIOR_TREE_PLAN.md)

**Agent 任务（零兼容：决策只留 BT）：**

1. `Domain/Enemy/BehaviorTree/`：`BehaviorTree` + Blackboard + Selector/Sequence + 少量 Condition/Action（进战、距离、冷却、追击、PulseAttack）。  
2. 输出**只**经 `AIInputWriter` → `InputFrame`。  
3. Hit / Death：**外层门闩**，BT 不 Tick 或空跑（对齐现 Brain）。  
4. `EnemyBehaviorTreeAsset` + `EnemyDefinition` 引用字段；删除 Idle/Chase/Attack 五态决策双轨（Hit/Dead 门闩可保留为宿主逻辑）。  
5. EditMode：节点 Tick / 冷却帧。

**Editor：**

1. `Assets/Data/Enemy/BehaviorTrees/` 建近战树资产，挂到真敌 Definition。  
2. 变体「只追不打」证明换资产改行为。  
3. 木桩 Definition **不挂树** / Dummy 模式。

**验收：** 大纲 §3.5；无第三方 BT；无 Domain 越权。

**预留（总清单 §6.4，本日不做编辑器）：** 运行时接口形状保持可替换（资产 + Runner），勿把未来插件 API 写进 Actor。

---

## 4. 并行规则（本日）

| 组合 | 判定 |
|------|------|
| A1 + A2 | ✅ 共用 Feedback |
| A3 + A4 Impulse | ✅ 共用完美吸收事件 |
| A5 + A1 | ✅ 木桩关 AI |
| A5 + 完美闪避联调 | ⚠ 需攻击者；用真敌非木桩 |
| Lock-On + 任何轨 | ❌ |

---

## 5. 收工 Definition of Done

对照大纲 §7，本日最低完成线：

1. **Must：** A1 木桩可复现伤 / 停 / 震 → ✅ 2026-08-08  
2. **Must：** A2 至少一条普攻命中 Cue（VFX+SFX）完整 ← 代码 ✅，Editor 绑资产中  

3. **Should：** A3 子弹可感知 + 恢复正确  
4. **Should：** A4 木桩镜头手感可接受  
5. **Should：** A5 一棵近战树可跑；EditMode 有测  
6. **Must：** 文档勾选本执行单 + 大纲；A3/A5 有定案则回写真源 2～5 行  

未完成项记入 §6，不塞进 Wave 4。

---

## 6. 明日债（收工填写）

| 项 | 状态 | 备注 |
|----|------|------|
| | | |
| | | |

---

## 7. 开工命令（建议顺序）

```text
1. 你：A1 Editor 木桩摆好并 Play 通一次
2. Agent：A2 命中 Cue 通道
3. 你：普攻 Feedback 资产绑定 → 木桩验收音画
4. Agent：A3 子弹事件 + Controller
5. 你：A4 Shake / 滤左右调参；完美窗手感
6. Agent：A5 BT Phase-1（可与 4～5 交错若你验 A3）
7. 双方：大纲 §7 + 本文 §5 勾选
```

需要 Agent 立刻从某一轨开写时，直接指定：**先 A2** 或 **先 A5**（A1 你可同时做 Editor）。
