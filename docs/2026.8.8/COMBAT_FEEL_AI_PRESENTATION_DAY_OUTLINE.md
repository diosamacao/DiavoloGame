# 战斗表现 / AI / 木桩 — 日计划大纲（2026-08-09）

> 制定：2026-08-08  
> 修订：2026-08-09 — A2 打击感验收；下一项 A5 BT；A3/A4 可选后置  
> **历史日计划：** A5 Phase-1（InputFrame）已验收。敌人 AI **下一阶段**见 [../2026.8.10/ENEMY_BT_DISCRETE_COMBAT_AND_CONFIG_PLAN.md](../2026.8.10/ENEMY_BT_DISCRETE_COMBAT_AND_CONFIG_PLAN.md)。  
> 用途：开工顺序、范围边界与验收清单（**大纲，非完整方案**）  
> **今日执行方案：** [COMBAT_FEEL_AI_PRESENTATION_DAY_EXECUTION.md](./COMBAT_FEEL_AI_PRESENTATION_DAY_EXECUTION.md)  
> 前置完成：GAS G0～G5；Wave 3.4 代码路由（`PerfectDodgeAttack` / Pipeline 武装 / Editor 可加 PerfectDodge 轨）  
> 关联真源：  
> - AI（现行结构）：[../2026.8.10/ENEMY_BT_DISCRETE_COMBAT_AND_CONFIG_PLAN.md](../2026.8.10/ENEMY_BT_DISCRETE_COMBAT_AND_CONFIG_PLAN.md)  
> - AI（契约/历史）：[ENEMY_BEHAVIOR_TREE_PLAN.md](../ENEMY_BEHAVIOR_TREE_PLAN.md)、[ENEMY_SYSTEM_INTEGRATION_PLAN.md](../ENEMY_SYSTEM_INTEGRATION_PLAN.md)  
> - 相机：[CAMERA_SYSTEM_PLAN.md](../2026.8.6/CAMERA_SYSTEM_PLAN.md)  
> - 完美闪避产品：[SKILL_AND_RESOURCE_SYSTEM_PLAN.md](../2026.8.6/SKILL_AND_RESOURCE_SYSTEM_PLAN.md) §5.6  
> - 数值 / 资源：[GAS_STYLE_COMBAT_REFACTOR_PLAN.md](../2026.8.7/GAS_STYLE_COMBAT_REFACTOR_PLAN.md)、[COMBAT_NUMERICS_PLAN.md](../COMBAT_NUMERICS_PLAN.md)  
> - 排期总案：[MASTER_IMPLEMENTATION_PLAN.md](../2026.8.6/MASTER_IMPLEMENTATION_PLAN.md)

---

## 1. 今日目标（一句话）

在**不破坏 60Hz Sim / Numeric 权威**的前提下，并行推进四条线：

1. **AI 行为树**起步（替换或旁路现网五态 `EnemyBrain` 决策）← **当前**  
2. **战斗表现优化**：资源可读性、完美闪避子弹时间、相机手感（A3/A4 可选后置）  
3. **打击 VFX / SFX** ✅（A2 已验收）  
4. **打击感木桩** ✅（A1 已验收）

---

## 2. 建议开工顺序（依赖）

```text
A0  Editor 扫尾 ✅（2026-08-08）
      ↓
A1  木桩靶 ✅（2026-08-08 验收）
      ↓ 可并行
A2  打击 VFX/SFX + HitFeedback ✅（2026-08-09 验收）
A3  完美闪避子弹时间（可选后置）
A4  相机轻优化（可选后置；完整 Lock-On/SkillShot 归 Camera 篇）
      ↓
A5  AI 行为树 Phase-1 ← **当前**（只写 InputFrame，复现进战追击+普攻）
```

**原则：** 木桩与命中音画已齐；下一主交付为 BT Phase-1；A3/A4 不阻塞。

---

## 3. 分轨大纲

### 3.1 A0 — Editor 扫尾（人工，优先 30～60 分钟）

**状态：✅ 已完成（2026-08-08，人工）**

| 项 | 操作 | 验收 |
|----|------|------|
| PerfectDodge 窗 | Dodge Action 加 **PerfectDodge** 轨 + 窗口帧 | ✅ 窗内挨打：不掉血、F3 `PDCounter>0`、`Next=Counter` |
| Counter Graph | Entry：`Intent=PerfectDodgeAttack`，挂反击招 | ✅ 缓冲内按攻击出 Counter，起手缓冲清零 |
| 资源价签（可选当日） | 普攻 Grant / Special·EX cost 抽查 | ✅ HUD EX/喧响与起手扣费正确 |

Agent **不改** `.asset` / Prefab；对齐仓库规则。

---

### 3.2 A1 — 打击感木桩

**状态：✅ 已验收（2026-08-08）**  
`Monster_EDF` + `enableCombatActions=false` + Default Hit → `Hit_Shake`；超高 MaxHp。

| 项 | 建议 |
|----|------|
| 形态 | 复用敌人装配（`CharacterActor` + Hurtbox + Vitality），或极简「木桩」Definition：无 AI / 或 AI 恒 Idle |
| 血量 | Health Attribute（经 Vitality）；可配高 MaxHP / 可选无敌开关（调试） |
| 位置 | 测试场景固定点；不进正式关卡流程 |
| 禁止 | 第二套血量口袋；App Command 旁路扣血 |

**验收：**

- [x] 普攻连段可稳定命中木桩  
- [x] 掉血 / 受击 Reaction / HitStop / 震屏可观测  
- [x] F3 或日志可对上 HP 与资源 Grant  
- [x] 死亡可选：倒地后重置按钮或超高血量不测死亡

**Editor：** Prefab / Definition / 场景摆放（人工）✅。

---

### 3.3 A2 — 打击特效 / 打击音效

**状态：✅ 已验收（2026-08-09）**

| 层 | 现状锚点 | 当日动作 |
|----|----------|----------|
| 招式轨 VFX/SFX | Timeline `PlayVfx` / `PlaySfx` | 刀光等 Timeline 点事件 |
| 命中反馈 | `HitPayload.Feedback` → `AttackHitEvent` → `HitImpactController` ✅ | Feedback 绑受击 Prefab/Clip |
| HitStop | `ActionSim.freezeFrames` + Owner 暂停粒子 | 木桩可观测 |

**定案：**

- **起手刀光/脚步** → Timeline 点事件  
- **受击火花/受击音/震屏** → Payload Feedback → Confirm 后 App Cue  

**验收：**

- [x] 至少 1～2 段普攻：命中有 VFX + SFX + 可选震屏  
- [x] 挥空不播命中 Cue（验收结论）  
- [x] 卡肉期间音画不「偷跑」逻辑帧（验收结论）  

**禁止：** Domain HitDetector 直调 Audio/VFX；表现回写 Sim。

---

### 3.4 A3 — 战斗表现：资源 / 完美闪避子弹 / 相机

#### 3.4.1 资源表现

| 项 | 说明 |
|----|------|
| Debug | F3 已有 EX / 喧响 / 闪避 / Effects；保持只读 |
| 可选增强 | 临界 Special→EX 时 HUD 提示强化（非正式 UI 美术） |
| 不做 | 正式战斗 HUD 美术、编队共享喧响 |

#### 3.4.2 完美闪避子弹时间

| 项 | 说明 |
|----|------|
| 权威 | 仍只在 Pipeline 完美窗：吞伤 + `ArmPerfectDodgeCounter` |
| 表现 | **新增**减速时间通道（建议：Confirm 完美吸收后发只读事件 → App 短时 `timeScale` 或独立 Presentation clock） |
| 参数 | 时长（逻辑帧或表现毫秒二选一写死）、曲线、是否冻结敌人动画 |
| 清理 | 超时 / Counter 起手 / 受击抢占时恢复 |

**验收：**

- [ ] 完美窗命中有可感知减速，结束后恢复  
- [ ] 不改变扣血/Grant/缓冲帧权威结果  
- [ ] 与逻辑 HitStop 叠加规则写明（建议：减速表现可叠，但不双改 `freezeFrames` 语义）

#### 3.4.3 相机优化（轻量，可选后置）

对齐 Camera 篇；**完整 LockOn / SkillShot 归 Camera 篇自管，不挂 Wave 4/5**。

| 做 | 不做 |
|----|------|
| 命中 Impulse / Shake 参数表（木桩调） | `CameraDirector` 模式栈大改 |
| 跟拍抖动、侧移滤左右微调（若仍刺眼） | Target Group Lock-On 全量 |
| 完美闪避/Counter 短 Impulse（可选） | SkillShot 多段大招镜头 |

**验收：** 连打木桩时镜头稳、命中有反馈、不穿模到不可用程度。

---

### 3.5 A5 — AI 行为树编写

**状态：✅ BT-1 Play 验收（2026-08-09）；BT-2 Custom SerializeReference 已接**  
**真源：** [ENEMY_BEHAVIOR_TREE_PLAN.md](../ENEMY_BEHAVIOR_TREE_PLAN.md)（自研轻量 BT；逻辑帧 Tick）。

**Phase-1 交付：**

| 项 | 内容 |
|----|------|
| 骨架 | `BehaviorTree` + Blackboard + 少量 Condition/Action 节点 |
| 输出 | **只写** `AIInputWriter` / `InputFrame`（与现网一致） |
| 门闩 | Hit / Death 外层抢占，BT 不 Tick 或空跑 |
| 等价行为 | 进战追击 + 冷却普攻（复现现 `EnemyBrain` Idle/Chase/Attack） |
| 资产 | `EnemyBehaviorTreeAsset` 挂到 `EnemyDefinition`（Editor） |

**禁止：**

- 第三方 BT 插件  
- BT 内 `TryStart` Action / 直改 Health  
- 读 Scene Physics 作权威感知（沿用 Perception + Snapshot 纪律）  
- 当日做 NavMesh / 完整 GraphView 编辑器  

**验收：**

- [ ] 至少 1 种近战敌：换树资产可改「只追不打」vs「追+打」  
- [ ] 受击/死亡行为不回归  
- [ ] EditMode：节点 Tick / 冷却帧可测  

**与木桩关系：** 木桩默认无 BT；真敌用 BT。互不阻塞。

---

## 4. 并行与冲突表

| 组合 | 可否并行 | 注意 |
|------|----------|------|
| 木桩 + VFX/SFX | ✅ | 共用 Feedback 字段 |
| 子弹时间 + 相机 Impulse | ✅ | 都走 App 表现；约定事件名 |
| BT + 木桩 | ✅ | 木桩关闭 AI |
| BT + 完美闪避 | ⚠ | 需能攻击玩家以测窗；可用现有敌或临时攻击者 |
| 大改 Lock-On + BT | ❌ 勿并行 | LockOn/SkillShot 归 Camera 篇 |

---

## 5. 非目标（明确不做）

- 完整 Lock-On / SkillShot / Director（归 Camera 篇）  
- Effect ScriptableObject 完整作者壳  
- 正式战斗 UI 美术血条  
- 联网 / 预测回滚  
- ~~删除 Wave 2 RM 回退~~ → **已完成（2026-08-08 Wave 2.5）**；Locomotion Stop/Pivot RM 另议  
- 第三方 GAS / BT 插件  

---

## 6. 建议日课表（可按精力调整）

| 时段 | 内容 |
|------|------|
| — | A0～A2 ✅（含打击感验收 2026-08-09） |
| 当前 | **A5 BT Phase-1** 骨架与等价替换 |
| 可选后置 | A3 完美闪避子弹；A4 相机轻量调参 |

---

## 7. 收工检查清单

- [x] 木桩场景可复现打击感（伤 / 停 / 震）  
- [x] 完美闪避：窗 + Counter 路由 Play 通过（A0 ✅）；子弹仍属 A3  
- [x] 至少一条普攻命中 Cue 完整（A2 ✅ 2026-08-09）  
- [x] BT Phase-1：近战敌可配置分支；无 Domain 越权（A5 ✅ 2026-08-09）  
- [x] 无新增双轨血量/资源权威；无表现回写 Sim（木桩路径）  
- [x] 文档：A1 / A2 勾选已回写  

---

## 8. 一句话

打击感主线与 BT Phase-1 已验收；**敌人 AI 下一阶段**见 [../2026.8.10/ENEMY_BT_DISCRETE_COMBAT_AND_CONFIG_PLAN.md](../2026.8.10/ENEMY_BT_DISCRETE_COMBAT_AND_CONFIG_PLAN.md)（E-CFG1 起）。

---

## 9. 后续进度备忘

**打击感优化（含 Wave 4 Branch_02 吸附行程 + A2 命中 Cue）已验收收口**；**A5 BT Phase-1** 已关闭。  
**当前结构主线：** 8.10 Desire + Request / 滞回 / 配置归属。  
可选后置：完美闪避表现（A3）、相机轻量调参（A4）；LockOn/SkillShot 归 Camera 篇。
