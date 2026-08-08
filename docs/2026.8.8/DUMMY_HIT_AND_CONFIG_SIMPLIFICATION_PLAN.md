# 木桩受击 / 校验 / 配置链简化 — 修改方案（归档指向）

> 制定：2026-08-08  
> **已被取代：** 木桩 AI 开关 + 玩家/敌人整链简化见  
> → [`CONFIG_CHAIN_AND_DUMMY_AI_PLAN.md`](./CONFIG_CHAIN_AND_DUMMY_AI_PLAN.md)  
> 本篇仅保留 Hit_Shake 资产根因备忘；实施以新篇为准。

---

## 1. 问题结论（先对齐事实）

### 1.1 Hit_Shake 不播 — 配置匹配问题（非管道坏了）

调用链本身正常：

```text
Pipeline 命中 → Vitality 扣血 → CharacterReactionService
  → ReactionSet.Resolve(Hit, payload.hitReactionId)
  → EnterHit → HitState：有 Action 则 TryStart，否则只倒数硬直帧
```

当前 `MonsterConfig.combat.reactions`：

| 字段 | 现值 | 问题 |
|------|------|------|
| `reactionType` | Hit | OK |
| `action` | `Monster_Goblin_Hit_Shake` | 资产本身可用（60Hz / 有 Clip） |
| `defaultRule` | **false** | 空 Id 攻击对不上 |
| `reactionId` | **`"1"`** | 玩家普攻 Payload `hitReactionId` 多为**空** |

`Resolve` 规则：先精确匹配 `HitReactionId`，否则用该类型的 **Default** 规则。  
→ 攻击方空 Id + 防守方非默认 `"1"` → **解析出 null** → HitState 只跑 `defaultHitStunFrames`（看起来像挨打定住、无 Hit_Shake）。

对照：`UnagiConfig` 受击规则是 `defaultRule=true`、空 `reactionId`，所以能播。

### 1.2 Locomotion「有动画不配就报错」

`CharacterAnimationProfile.ValidateClips` **强制** Idle / Walk / Run 三键都有 Clip。  
`ValidateForEnemy` ≡ `ValidateShared`，失败则 `EnemyDefinition.Validate` 失败 → **不 Spawn**。

站桩木桩运行时几乎只用 Idle，但与玩家共用同一校验，故「少绑 Walk/Run 就报错」。

### 1.3 配置链过长

现状（木桩也要凑齐）：

```text
EnemyDefinition
└─ CharacterConfig
   ├─ ModelPrefab
   ├─ AnimationProfile（Idle/Walk/Run…）
   ├─ LocomotionProfile（可选运行时，常仍要建）
   ├─ GameplayIntentProfile
   ├─ CombatModeProfile → ActionSet → ActionGraph（木桩可空图，但链不能断）
   └─ Combat.Reactions → ActionDefinition(s)
+ BrainProfile
```

站桩真正需要的是：**模型 + Idle + 默认受击 Action + Hurtbox + MaxHp + 不追打**；Intent/Graph/Walk/Run 多为校验或复用玩家管线的代价。

---

## 2. 目标与非目标

**目标**

1. 木桩被普攻稳定播 `Hit_Shake`  
2. 站桩敌人校验与作者成本下降（不必为过检填假 Walk/Run / 空 Graph 仪式）  
3. 配置入口变短，**单一装配真源**，不长期双轨  

**非目标（本方案不做）**

- 第二套血量 / 旁路 HitDetector  
- 完整 BT 编辑器、Wave 4 Lock-On  
- Agent 手改 `.asset`（你在 Editor 改反应规则）  
- 永久保留「严格校验 + 宽松校验」两套互相兜底  

---

## 3. 分期方案

### Phase 0 — 立刻修好 Hit_Shake（仅 Editor，今天）

**改动（人工）：** `MonsterConfig` → Combat → Reactions：

1. 该条规则勾选 **Default Rule**  
2. **清空** `Reaction Id`（不要填 `"1"`）  
3. Action 仍指向 `Monster_Goblin_Hit_Shake`  
4. （建议）`Monster_Goblin_Hit_Shake` 的 `actionType` 保持/改为 Hit；与解析无关但利于编辑器语义  

**Play 验收：**

- [ ] 普攻命中播 Hit_Shake，而非仅定住  
- [ ] F3 HP 下降；连打可反复进 Hit  
- [ ] 木桩仍不追打（Brain `aggroRadius=0`）

**可选对照：** 同场景刷旧 `EnemyDefinition`（Unagi 默认受击）确认管道；若 Unagi 能播而 Monster 不能，则仍是规则匹配问题。

**后续（P2+）：** 若要对轻/重受击做差异化，再在**攻击方** Hitbox Payload 填 `hitReactionId`，防守方加精确规则；**始终保留一条 Default Hit** 兜底。

---

### Phase 1 — 放松「站桩敌人」校验（小代码，解痛点 2）

**方向：** 敌人/木桩校验与玩家 Locomotion 完整校验拆开，**直接改规则，不做永久 fallback**。

| 项 | 现状 | 改为 |
|----|------|------|
| `ValidateClips` | 强制 Idle+Walk+Run | 增加 `ValidateClipsForEnemy` / 参数：站桩只强制 **Idle**；Chase 敌仍要 Walk/Run（或 Idle+Walk） |
| `ValidateForEnemy` | = `ValidateShared` | 站桩路径：Intent / CombatProfile 允许「空壳」策略见下 |
| 空 ActionGraph | Profile 非 null 即可 | 明确文档：木桩 Graph 可为 0 Entry；校验不要求 Attack Entry |

**定案选项（二选一，推荐 A）：**

- **A. Definition 标记 `stationaryDummy`（或 Brain `aiMode=Dummy`）**  
  - Validate：只强制 Model + Idle + Reactions 结构合法 + Brain  
  - Intent/CombatProfile：仍建议挂共享「EmptyCombat」壳（一个项目级空 Graph），避免工厂 null 分支扩散  
- **B. 一律放宽所有敌人 Validate**  
  - 实现更简单，但真追击敌可能带着缺 Walk 进 Play 才炸  

**Locomotion 动画作者体验（配合 A）：**

- 校验文案改为：「站桩敌人只需 Idle；需要 Chase 时再绑 Walk/Run」  
- Editor 可选：一键「用 Idle 填满缺失 gait」（工具菜单，写资产仍由你确认）

**删除：** 「必须三键齐才给刷怪」对 Dummy 的旧语义；不保留「报 Warning 仍按旧三键强制」的兼容。

---

### Phase 2 — 缩短配置链（中代码，解痛点 3）

原则：**作者入口变短，运行时仍进现有 CharacterActor 工厂**；禁止另起一套 DummyActor。

#### 推荐形态：`EnemyDefinition` 变「装配面板」，内部仍展开为现有类型

```text
【作者只配】EnemyDefinition（或 EnemyBodyPreset）
  - Model Prefab
  - Idle Clip（可选 Walk/Run）
  - Default Hit Action（+ 可选 Death）
  - MaxHp / Team / Hurtbox 覆盖
  - Brain：Dummy | ChaseAttack
  - （可选）CombatModeProfile；Dummy 自动用项目级 EmptyCombat

【运行时生成或项目级共享壳】
  - CharacterConfig 可由 Factory 组装 / 或 SO 子资产自动生成
  - AnimationProfile：由 Clip 字段生成 entries
  - Intent + Empty ActionGraph：项目级单例资产，所有木桩共用
```

| 子方案 | 侵入性 | 说明 |
|--------|--------|------|
| **2a 共享空壳资产** | 低 | 做 1 份 `Intent_EnemyShared` + `CombatMode_EmptyGraph`；MonsterConfig 只填 Model/Anim/Reactions；文档规定木桩必挂这两份共享壳 | 
| **2b Definition 直填 Hit + Idle** | 中 | `EnemyDefinition` 增加 `defaultHitAction` / `idleClip`；工厂写入运行时 Config 或旁路 Validate；**迁移后删除「只配在 Config 里才生效」的双写** |
| **2c 生成器** | 中高 | Editor 菜单「从 Monster 模板生成 CharacterConfig」；日常仍编辑生成结果，链不短但少手误 |

**建议落地顺序：** Phase 0 → **2a（当天可文档+共享壳）** → Phase 1 校验 → 若仍嫌长再 **2b**（一次切真源，删双写）。

**明确不采用：** Config.Reactions 与 Definition.defaultHitAction 长期同时生效；AI「Dummy + aggro0」双语义并存超过一个迭代。

---

### Phase 3 — Dummy AI 语义硬化（可选，跟 Phase 1/2）

| 项 | 做法 |
|----|------|
| `aiMode = Dummy` | `EnemyBrain.Step` 直接清输入并 return（或宿主不 Tick 决策） |
| 删除对木桩依赖 `aggroRadius=0` 的隐式约定 | 迁移后 Chase 敌必须显式 `Combat` 模式 |

受击/死亡门闩逻辑保留在宿主，与 BT Phase-1 不冲突：Dummy 无树；真敌上 BT。

---

## 4. 推荐执行顺序（给你勾选）

```text
今天
  P0  Editor：Hit 规则改为 Default → Hit_Shake     ← 先做
  2a  文档 + 共享 EmptyCombat / Intent（可选同日）

下一会话（若 P0 已通、仍痛校验）
  P1  ValidateForEnemy / Idle-only + Dummy 标记
  P3  aiMode=Dummy（可与 P1 同 PR）

再后（产品要「一键木桩」）
  P2b Definition 直填 Hit/Idle，删双写
```

---

## 5. 验收矩阵

| 项 | 标准 |
|----|------|
| Hit_Shake | 普攻命中必播；挥空不进 Hit |
| 硬直回退 | 无默认规则时仍可进 Hit（仅帧硬直）——调试用；正式木桩必须有默认 Action |
| 校验 | Dummy：缺 Walk/Run **不再**阻止 Spawn；Chase 敌缺 gait 仍 Error |
| 配置 | 新建一只站桩怪：共享壳 + Model + Idle + Hit Action + Definition ≤ 上述字段 |
| 权威 | 受击只走 ReactionService；HP 只走 Vitality/Numeric |

---

## 6. 一句话

**Hit_Shake 先改成 Default 规则即可通；校验与配链用「Dummy 标记 + 共享空壳 + Idle-only Validate」缩短，最后才把 Hit/Idle 抬到 Definition 并删掉双写。**
