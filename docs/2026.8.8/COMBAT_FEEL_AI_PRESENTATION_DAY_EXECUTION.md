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
| **A1 木桩** | 现网可配 | 可选：显式「关闭 AI」开关 | **必做**：高 HP Definition + Idle/aggro0 + 场景摆点 |
| **A2 命中 Cue** | 缺通道 | **必做**：Confirm 后 VFX/SFX Cue | 普攻 Feedback / Timeline 刀光 / Prefab·Clip |
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

**Editor 步骤（主路径，无代码也可先验）：**

1. 复制现有 `EnemyDefinition` → 如 `EnemyDefinition_Dummy`：`MaxHp` 拉高（如 `99999`）。  
2. `EnemyBrainProfile`：`aggroRadius = 0`（保持 Idle，永不 Chase/Attack）。  
3. 确认敌人 `CharacterConfig` / Hurtbox / 受击 Reaction 已绑（否则只有掉血无动作）。  
4. 测试场景 `EnemySpawnController`（或等价）固定点刷木桩。  
5. Play：普攻连段 → F3 对 HP / Grant；观察 HitStop、震屏。

**可选 Agent（仅当 aggro0 语义不够稳）：**

- `EnemyDefinition` 或 Brain：`disableCombatAi` / `aiMode = Dummy`：不 Tick 决策、不写攻击输入。  
- **禁止**第二套 HP；重置用 Vitality / 重刷。

**验收：** 大纲 §3.2 四条。

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

1. 扩展 `HitFeedbackSettings`：命中 VFX Prefab、SFX Clip（可空）。  
2. App 层订阅命中事件（已有 Shake/HitStop 旁）播 Cue；命中点/朝向用事件数据。  
3. 保证 Collect 阶段不播；`freezeFrames>0` 时不按 wall-clock 偷跑。

**Editor：** 1～2 段普攻 Hitbox 填 Feedback；火花 Prefab / 命中音；刀光仍走 Timeline。

**验收：** 大纲 §3.3；Domain HitDetector 无 Audio/VFX 引用。

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

1. **Must：** A1 木桩可复现伤 / 停 / 震  
2. **Must：** A2 至少一条普攻命中 Cue（VFX+SFX）完整  
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
