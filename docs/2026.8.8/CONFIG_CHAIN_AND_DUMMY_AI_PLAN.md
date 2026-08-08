# 配置链简化 + 木桩 AI 开关 — 方案

> 制定：2026-08-08  
> 取代/收束：[`DUMMY_HIT_AND_CONFIG_SIMPLIFICATION_PLAN.md`](./DUMMY_HIT_AND_CONFIG_SIMPLIFICATION_PLAN.md) 中「靠校验放松当木桩」的主路径  
> 用户定案：
>
> 1. **木桩 = 敌人 AI 开关**：关闭后不再「行动」，仍保留受击等非行动能力  
> 2. **配置链过长是玩家+敌人共同问题**：需整体分析并删除多余层（如 ActionSet），建立配置规范  
> 3. **`GameplayIntentProfile` 改为项目全局唯一真源**：角色不再逐个配置  

**原则：** 新逻辑为唯一真源；删除被替代壳层，不长期双轨。

---

## 1. 木桩：在敌人 AI 上关「行动」

### 1.1 「行动」在现网指什么

`EnemyBrain` **只写输入，不直接 `TryStart`**。主动「行动」= 决策写出的：

| 行动 | 输入 |
|------|------|
| 追击移动 | `SetMove` |
| 攻击 | `PulseAttack` → Attack 意图 → Graph Entry |

**不是行动（开关关闭后仍须保留）：**

| 能力 | 路径 |
|------|------|
| 扣血 / Numeric | Hurtbox → Vitality |
| 受击 / 死亡表现 | `CharacterReactionService` → `EnterHit` / `EnterDeath` |
| Brain 受控门闩 | `NotifyHit` / `NotifyDeath`（清输入、进 Hit/Dead，**不**走 Chase/Attack） |
| Hurtbox / 软碰撞 / 站桩 Locomotion Idle | 角色管线 |

### 1.2 开关放哪

| 位置 | 推荐 | 理由 |
|------|------|------|
| **`EnemyBrainProfile.enableCombatActions`（或 `actionsEnabled`）** | ✅ 首选 | 行为变体：同一身体可「真敌 / 木桩」两份 Profile，或一份 Profile 勾选 |
| `EnemyDefinition` 再挂一份覆盖 | 可选 | 身份级「整只是木桩」；若与 Profile 并存，**只允许一层权威**（建议只留 Profile，或 Definition 覆盖 Profile） |
| 拔 Graph / 只靠 `aggroRadius=0` | ❌ | 语义模糊；关 Graph 仍可能写 Move；且不表达「保留受击」 |

**运行时（唯一行为）：** `EnemyBrain.Step` 在 Capture 之后：

```text
若 !enableCombatActions：
  若 State 不是 Hit/Dead：
    ClearAll 输入；State = Idle
  return
NotifyHit / NotifyDeath 仍可把 State 打到 Hit/Dead（门闩保留）
```

`EnemyHandle.ProduceInput`：**不要**在开关打开时整段跳过 `Brain.Step`（否则 Hit 门闩不跑）。死亡后现网已停 Step，保持即可。

### 1.3 与配置简化的关系

木桩**不再依赖**「空 Graph / 假 Walk/Run / aggro=0」才能站桩。  
仍建议木桩 Graph 可为空（无主动招），但那是**作者可选**，不是木桩定义。

### 1.4 Hit_Shake（仍先修资产）

与 AI 开关正交：Reactions 须有 **Hit Default → Hit_Shake**（清空精确 Id）。见旧篇 Phase 0。

### 1.5 验收

- [ ] `enableCombatActions=false`：不追、不打  
- [ ] 同配置被打：播 Hit_Shake（或默认受击）、扣血、可连打  
- [ ] 死后 Despawn 行为与现网一致  
- [ ] `=true`：恢复追打（同一身体资产）

---

## 2. 现状配置链总览

### 2.1 玩家

```text
PlayerController
└─ CharacterConfig
   ├─ ModelPrefab
   ├─ CharacterAnimationProfile      // Key→Clip（含 defaultLocomotion）
   ├─ CharacterLocomotionProfile     // 阈值/落脚/烘焙轨（与上者正交）
   ├─ InputActionAsset               // 仅玩家设备
   ├─ GameplayIntentProfile          // ⚠️ 每角色必填，但全库已共用同一资产（冗余拖拽）
   ├─ CombatModeProfile              // mode → PlayerActionSet + 可选 AnimProfile
   │    └─ PlayerActionSet           // ⚠️ 几乎只包一层 ActionGraph
   │         └─ ActionGraph          // 真源：Entry/Cancel/衔接
   │              └─ ActionDefinition(+ Resolver)
   ├─ Combat（嵌套）：Team / HP / Hurtbox / Reactions→ActionDefinition
   └─ Resources（嵌套）
```

### 2.2 敌人

```text
EnemyDefinition
├─ CharacterConfig（同上，但不要求 InputActions）
├─ EnemyBrainProfile                 // 追打参数；将加「行动」开关
├─ maxHp / teamId                    // 覆盖 Config 内同名字段
```

### 2.3 冗余与易混点（裁决）

| 层 | 裁决 |
|----|------|
| **PlayerActionSet** | **冗余壳**，应删除；Mode/Config 直挂 `ActionGraph` |
| **CombatModeProfile** | 多模式（Katana/Beast）有价值；**单模式**时不应再强制独立 SO + Set |
| **ActionGraph** | **出招真源，保留** |
| **AnimationProfile vs LocomotionProfile** | **正交，勿合并**（Clip 映射 ≠ 相位/落脚/烘焙） |
| **InputActions vs IntentProfile** | **分层保留**：InputActions 仍可挂玩家 Config（或日后也全局）；**Intent 改为项目全局唯一，删掉 CharacterConfig 字段** |
| **Reactions vs Graph** | **分流，保留**（受控态 ≠ 主动作）；勿把 Hit/Death 塞回 Graph |
| **Config.teamId/maxHealth vs Definition** | 敌人以 Definition 为准；Config 同字段易误导 → 校验/Inspector 隐藏或报 Warning |

---

## 3. 目标配置形态（简化后）

### 3.0 全局意图（定案）

**`GameplayIntentProfile` = 项目级唯一真源**，不再出现在每个 `CharacterConfig` 上。

| 项 | 裁决 |
|----|------|
| 现状 | 全库已共用同一资产（如 Unagi 下那份），但每角色仍要拖一次 |
| 目标 | 固定路径 / ProjectSettings / Resources 加载**一份**；Factory / `AIInputWriter` / `GameplayIntentProducer` 只读全局 |
| 作者 | 新建角色**不配** Intent；改键位→意图、缓冲帧时只改全局资产 |
| 与 InputActions | Intent 引用全局（或玩家）`.inputactions` 的 Action；设备绑定仍属 Input 层 |
| 禁止 | 「全局默认 + CharacterConfig 可选覆盖」长期双轨；首版只保留全局 |

```text
【全局】
GameplayIntentProfile（唯一）
  └─ bindings[] + actionBufferDurationFrames
       ↑ 引用 InputActionAsset 中的 Action

【角色不再挂】
CharacterConfig.gameplayIntentProfile  → 删除字段与 Validate 必填
```

### 3.1 出招链（玩家 / 敌人共用）

**删除 `PlayerActionSet`。**

```text
【多模式玩家】
CharacterConfig
├─ InputActionAsset                  // 玩家设备（可后续再全局化，本方案不强制）
├─ CombatModeProfile
│    └─ mode → ActionGraph (+ 可选 AnimationProfile)
│              └─ ActionDefinition
└─ …模型 / 动画 / Locomotion / Reactions / Resources

【单模式（多数敌人 / 初期玩家）】
CharacterConfig
├─ defaultActionGraph → ActionGraph
└─ …（无 Intent 字段；敌人通常无 InputActions）

【项目全局，角色外】
GameplayIntentProfile（唯一）
```

`CombatModeService` / `ActionResolverService`：`ActiveGraph` 直接来自 ModeEntry 或 Config 的 Graph，**不再**经 ActionSet。

### 3.2 身体 vs 身份（敌人）

```text
CharacterConfig = 身体与表现（模型、动画、Locomotion、Graph、Hurtbox、Reactions、资源壳；玩家另挂 InputActions）
EnemyDefinition = 身份（显示名、BrainProfile含行动开关、MaxHp、Team）
GameplayIntentProfile = 项目全局（物理→意图），不属于单个角色
```

规范：敌人 Config 的 `teamId` / `maxHealth` **不作为权威**；Validate 对敌人忽略或提示「以 Definition 为准」。

### 3.3 作者最小清单（规范）

**项目级（一次）：**

- [ ] 全局 `GameplayIntentProfile`（含 Always+Pressed→Attack，供 AI）  
- [ ] 玩家用 `InputActionAsset`  

**玩家（可玩）：**

1. CharacterConfig + Model  
2. AnimationProfile（Idle/Walk/Run 至少）+ LocomotionProfile  
3. InputActions（角色侧仅此输入相关项）  
4. **一张** ActionGraph（或 ModeProfile 多图）  
5. Reactions：默认 Hit（+ 可选 Death）  
6. Resources / Hurtbox / Motor  

**敌人（可追打）：**

1. CharacterConfig（**不配 Intent**；Graph 含 Attack Entry）  
2. Animation + Locomotion（Chase 需要 Walk/Run）  
3. EnemyDefinition + BrainProfile（`enableCombatActions=true`）  
4. Reactions 默认 Hit  

**木桩：**

1. 同上身体 + 默认 Hit  
2. BrainProfile：`enableCombatActions=false`  
3. Graph 可空；Walk/Run 可与 Idle 同 Clip（校验策略见 Phase D，非木桩定义）

### 3.4 简化后整链一览

```text
【全局】
GameplayIntentProfile ──► 所有角色 Intent 生产 / AI Attack 探测

【玩家】
PlayerController → CharacterConfig
  ├─ Model / Anim / Locomotion / InputActions
  ├─ Mode→Graph 或 defaultActionGraph
  └─ Reactions / Resources / Motor / Team·HP

【敌人】
EnemyDefinition
  ├─ CharacterConfig（同上，无 InputActions，无 Intent 字段）
  ├─ BrainProfile（enableCombatActions）
  └─ maxHp / teamId
```

---

## 4. 分期落地（建议）

### Phase A — 木桩 AI 开关（小、优先）✅ 代码 2026-08-08

**代码**

- [x] `EnemyBrainProfile.enableCombatActions`（默认 true）  
- [x] `EnemyBrain.Step`：关行动时清输入停 Idle；仍 `TickHit`；硬直后不入 Chase  

**Editor（人工）**

- [ ] 木桩 BrainProfile：取消勾选 **Enable Combat Actions**  
- [ ] Reactions Default → Hit_Shake  
- [ ] 场景刷 `Monster_EDF`  

**删除：** 文档中「必须空 Graph / aggro0 才是木桩」的表述（aggro0 可留作调参，非定义）。

### Phase A2 — 全局 GameplayIntentProfile（小，可与 A 同迭代）✅ 代码 2026-08-08

**代码**

- [x] `GameplayIntentSettings`：Resources `ACT/GameplayIntentProfile`；Editor 可回退 FindAssets  
- [x] Factory 只读全局；**删除** `CharacterConfig.gameplayIntentProfile`  
- [x] 菜单 `ACTGame/Input/Migrate Intent Profile To Resources`  

**资产（人工）**

- [ ] 运行上述菜单完成迁移（打包前必须）  
- [ ] CharacterConfig 重存以清掉残留 Intent 槽（可选）  

**验收：** 玩家连招意图 / 缓冲帧与迁前一致；敌人仍能 `PulseAttack`；新建 Config 无需拖 Intent。

### Phase B — 删除 PlayerActionSet（中，玩家+敌人一起切）✅ 代码 2026-08-08

**代码**

- [x] `CombatModeEntry.actionGraph`；`ICombatModeService.ActiveGraph`  
- [x] 删除 `PlayerActionSet.cs`  
- [x] Editor：`ACTGame/Combat/Migrate ActionSet To Mode Graph`（打开工程 delayCall 自动跑一次）  
- [x] `ACTGame/Combat/Delete Orphan PlayerActionSet Assets`  

**资产（人工）**

- [ ] 打开 Unity 确认 Console 迁移日志；ModeProfile 条目显示 Graph  
- [ ] 可选：执行 Delete Orphan… 清理空壳 Set  
- [ ] Play：连招 / Cancel / 敌人攻击  

**验收：** 连招 / Cancel / 模式切换 / 敌人攻击与迁前一致。

### Phase C — 单模式直挂 Graph（中）

**代码**

- `CharacterConfig`：可选 `ActionGraph defaultActionGraph`  
- 当 `combatProfile == null` 且 `defaultActionGraph != null`：`CombatModeService` 以 Default 单图构造  
- 或：`combatProfile` 仍要，但允许「Inline 单图」嵌在 Config 而不建独立 Mode SO  

**规范：** 敌人默认走单图直挂；多模式玩家才建 ModeProfile。

**删除：** 「每个角色必须 ModeProfile + ActionSet + Graph 三件套」的作者流程。

### Phase D — 校验与 Inspector 体验（低风险跟进）

- 敌人 Validate：不强制与玩家完全同一套（Chase 敌要 Walk/Run；木桩 Idle 即可——**服务作者体验**，定义仍以 AI 开关为准）  
- 敌人 Config Inspector：隐藏/灰显无效的 `teamId`/`maxHealth`  
- 可选：CharacterConfig 自定义 Editor「配置向导」按清单勾选缺失项  

### Phase E — 不做（明确拒绝）

| 项 | 原因 |
|----|------|
| 合并 Animation + Locomotion Profile | 职责不同，合并只省文件、编辑器更臃肿 |
| Hit/Death 并进 ActionGraph | 破坏受控态与主动作分离 |
| 木桩专用 CharacterActor / 第二套血量 | 违反单一管线 |
| ActionSet 与直挂 Graph 长期双读 | 违反零兼容 |
| Intent「全局 + 角色覆盖」双读 | 违反零兼容；角色级缓冲差异以后另开需求再设计 |

---

## 5. 配置规范（落地后写入 CONVENTIONS / README）

1. **出招真源只有 ActionGraph**；禁止再引入「Set」类转发 SO。  
2. **受击/死亡真源只有 CharacterConfig.Combat.Reactions**。  
3. **意图真源是项目唯一 `GameplayIntentProfile`**；`CharacterConfig` 不挂 Intent；InputActions 只服务玩家设备。  
4. **敌人身份字段在 EnemyDefinition**；身体在 CharacterConfig。  
5. **木桩 = Brain 关闭行动**；受击走 Reactions，与玩家打真敌同一管道。  
6. **一个模式 = 一张 Graph**；多模式才用 Mode 表。  
7. **Clip 映射与 Locomotion 参数分资产**，命名避免都叫「Locomotion」造成误绑（文档写清：`AnimationProfile` vs `LocomotionProfile`）。  
8. **新建角色检查表**：按 §3.3 勾选；缺 Default Hit 的正式敌人/木桩视为未完成；**不**再检查 Intent 槽位。

---

## 6. 与旧木桩文档的关系

| 旧提案 | 新裁决 |
|--------|--------|
| 主要靠 aggro=0 / 空 Graph | 改为 **AI 行动开关** |
| 优先大改 Validate 当木桩定义 | Validate 放松降为 Phase D 体验项 |
| Definition 直填 Hit 当主简化 | 次要；主简化是 **删 ActionSet + 单模式直挂 Graph + 全局 Intent** |
| Hit_Shake Default 规则 | **仍必做（资产）**，与 AI 开关并行 |
| 每角色拖 Intent | 改为 **全局唯一**（Phase A2） |

---

## 7. 建议开工顺序

```text
1. Editor：Hit Default → Hit_Shake（当天可验受击）
2. Phase A：BrainProfile 行动开关（木桩语义落地）
3. Phase A2：GameplayIntentProfile 全局化（删 Config 字段）
4. Phase B：删除 PlayerActionSet（玩家+敌人配置链变短）
5. Phase C：单模式 Config 直挂 Graph（敌人作者路径最短）
6. Phase D：校验/Inspector 规范
```

**已落地代码：** Phase A / A2 / B。打开 Unity 后确认 ModeProfile 迁移成功；下一代码步为 **Phase C**（单模式 Config 直挂 Graph）。
