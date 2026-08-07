# 战斗表现 / AI / 木桩 — 日计划大纲（2026-08-09）

> 制定：2026-08-08  
> 用途：明日开工顺序、范围边界与验收清单（**大纲，非完整方案**）  
> 前置完成：GAS G0～G5；Wave 3.4 代码路由（`PerfectDodgeAttack` / Pipeline 武装 / Editor 可加 PerfectDodge 轨）  
> 关联真源：  
> - AI：[ENEMY_BEHAVIOR_TREE_PLAN.md](../ENEMY_BEHAVIOR_TREE_PLAN.md)、[ENEMY_SYSTEM_INTEGRATION_PLAN.md](../ENEMY_SYSTEM_INTEGRATION_PLAN.md)  
> - 相机：[CAMERA_SYSTEM_PLAN.md](../2026.8.6/CAMERA_SYSTEM_PLAN.md)  
> - 完美闪避产品：[SKILL_AND_RESOURCE_SYSTEM_PLAN.md](../2026.8.6/SKILL_AND_RESOURCE_SYSTEM_PLAN.md) §5.6  
> - 数值 / 资源：[GAS_STYLE_COMBAT_REFACTOR_PLAN.md](../2026.8.7/GAS_STYLE_COMBAT_REFACTOR_PLAN.md)、[COMBAT_NUMERICS_PLAN.md](../COMBAT_NUMERICS_PLAN.md)  
> - 排期总案：[MASTER_IMPLEMENTATION_PLAN.md](../2026.8.6/MASTER_IMPLEMENTATION_PLAN.md)

---

## 1. 今日目标（一句话）

在**不破坏 60Hz Sim / Numeric 权威**的前提下，并行推进四条线：

1. **AI 行为树**起步（替换或旁路现网五态 `EnemyBrain` 决策）  
2. **战斗表现优化**：资源可读性、完美闪避子弹时间、相机手感  
3. **打击 VFX / SFX** 可配可播、跟命中反馈一致  
4. **打击感木桩**场景靶，稳定验收 HitStop / 震屏 / 音画

---

## 2. 建议开工顺序（依赖）

```text
A0  Editor 扫尾（阻塞手感验收）
    Dodge 配 PerfectDodge 窗 + Graph Counter Entry
      ↓
A1  木桩靶（最早可玩验收台）
      ↓ 可并行
A2  打击 VFX/SFX + HitFeedback 调参
A3  完美闪避子弹时间（表现事件，不改吞伤权威）
A4  相机优化（跟拍 / 震屏 / 可选轻量 FOV；Lock-On 大改勿塞一天）
      ↓ 可并行或午后
A5  AI 行为树 Phase-1 骨架（只写 InputFrame，复现进战追击+普攻）
```

**原则：** 先有木桩与反馈通道，再谈「打击感」；AI BT 不阻塞木桩验收，但必须遵守锁步时钟（逻辑帧 Tick）。

---

## 3. 分轨大纲

### 3.1 A0 — Editor 扫尾（人工，优先 30～60 分钟）

| 项 | 操作 | 验收 |
|----|------|------|
| PerfectDodge 窗 | Dodge Action 加 **PerfectDodge** 轨 + 窗口帧 | 窗内挨打：不掉血、F3 `PDCounter>0`、`Next=Counter` |
| Counter Graph | Entry：`Intent=PerfectDodgeAttack`，挂反击招 | 缓冲内按攻击出 Counter，起手缓冲清零 |
| 资源价签（可选当日） | 普攻 Grant / Special·EX cost 抽查 | HUD EX/喧响与起手扣费正确 |

Agent **不改** `.asset` / Prefab；对齐仓库规则。

---

### 3.2 A1 — 打击感木桩

**目标：** 固定站桩、可被反复殴打、走完整 Numeric + Reaction + 反馈链路。

| 项 | 建议 |
|----|------|
| 形态 | 复用敌人装配（`CharacterActor` + Hurtbox + Vitality），或极简「木桩」Definition：无 AI / 或 AI 恒 Idle |
| 血量 | Health Attribute（经 Vitality）；可配高 MaxHP / 可选无敌开关（调试） |
| 位置 | 测试场景固定点；不进正式关卡流程 |
| 禁止 | 第二套血量口袋；App Command 旁路扣血 |

**验收：**

- [ ] 普攻连段可稳定命中木桩  
- [ ] 掉血 / 受击 Reaction / HitStop / 震屏可观测  
- [ ] F3 或日志可对上 HP 与资源 Grant  
- [ ] 死亡可选：倒地后重置按钮或超高血量不测死亡

**Editor：** Prefab / Definition / 场景摆放（人工）。

---

### 3.3 A2 — 打击特效 / 打击音效

**目标：** 命中瞬间「看得见、听得见」，配置入口清晰。

| 层 | 现状锚点 | 明日动作 |
|----|----------|----------|
| 招式轨 VFX/SFX | Timeline `PlayVfx` / `PlaySfx`（起手演出） | 关键招式补点事件；确认挂点与 PlaybackSpeed |
| 命中反馈 | `HitPayload.Feedback` → `AttackHitEvent` → Shake / HitStop | 统一：命中火花 / 命中音是否走 Feedback 或独立 Cue |
| HitStop | `ActionSim.freezeFrames`（逻辑）+ 表现跟帧 | 木桩上调 `hitStopFrames` 手感表，禁止 unscaled 秒权威 |

**定案倾向（可明日修订）：**

- **起手刀光/脚步** → Timeline 点事件  
- **命中火花/命中音/震屏** → Payload Feedback（或 ConfirmHit 后 App 只读 Cue），避免 Collect 阶段播特效  

**验收：**

- [ ] 至少 1～2 段普攻：命中有 VFX + SFX + 可选震屏  
- [ ] 挥空不播命中 Cue  
- [ ] 卡肉期间音画不「偷跑」逻辑帧  

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
| 表现 | **新增**子弹时间通道（建议：Confirm 完美吸收后发只读事件 → App 短时 `timeScale` 或独立 Presentation clock） |
| 参数 | 时长（逻辑帧或表现毫秒二选一写死）、曲线、是否冻结敌人动画 |
| 清理 | 超时 / Counter 起手 / 受击抢占时恢复 |

**验收：**

- [ ] 完美窗命中有可感知减速，结束后恢复  
- [ ] 不改变扣血/Grant/缓冲帧权威结果  
- [ ] 与逻辑 HitStop 叠加规则写明（建议：子弹表现可叠，但不双改 `freezeFrames` 语义）

#### 3.4.3 相机优化（当日轻量）

对齐 Camera 篇，**当日不做完整 Director / Lock-On Wave 4**。

| 做 | 不做（留给 Wave 4） |
|----|---------------------|
| 命中 Impulse / Shake 参数表（木桩调） | `CameraDirector` 模式栈大改 |
| 跟拍抖动、侧移滤左右微调（若仍刺眼） | Target Group Lock-On 全量 |
| 完美闪避/Counter 短 Impulse（可选） | SkillShot 多段大招镜头 |

**验收：** 连打木桩时镜头稳、命中有反馈、不穿模到不可用程度。

---

### 3.5 A5 — AI 行为树编写

**真源：** [ENEMY_BEHAVIOR_TREE_PLAN.md](../ENEMY_BEHAVIOR_TREE_PLAN.md)（自研轻量 BT；逻辑帧 Tick）。

**Phase-1（明日建议交付）：**

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
| 大改 Lock-On + BT | ❌ 当日勿并行 | Lock-On 留给 Wave 4 |

---

## 5. 非目标（明确不做）

- Wave 4 吸附 / 完整 Lock-On Director  
- Effect ScriptableObject 完整作者壳  
- 正式战斗 UI 美术血条  
- 联网 / 预测回滚  
- 删除 Wave 2 RM 回退（另排期）  
- 第三方 GAS / BT 插件  

---

## 6. 建议日课表（可按精力调整）

| 时段 | 内容 |
|------|------|
| 上午前 | A0 Editor 扫尾 + A1 木桩场景 |
| 上午后 | A2 命中 VFX/SFX + HitStop/震屏调参 |
| 下午前 | A3 完美闪避子弹 + 相机轻量优化 |
| 下午后 | A5 BT Phase-1 骨架与等价替换 |
| 收工前 | 木桩连打录像/清单勾选；记明日债 |

---

## 7. 收工检查清单

- [ ] 木桩场景可复现打击感（伤 / 停 / 震 / 音画）  
- [ ] 完美闪避：窗 +（可选）子弹 + Counter 路由 Play 通过  
- [ ] 至少一条普攻命中 Cue 完整  
- [ ] BT：近战敌行为等价或可配置分支；无 Domain 越权  
- [ ] 无新增双轨血量/资源权威；无表现回写 Sim  
- [ ] 文档：本大纲勾选 + 若 BT/子弹有定案则回写对应真源篇 2～5 行  

---

## 8. 一句话

明日以**木桩验收台**为中心，补齐**命中音画与完美闪避子弹**，相机只做轻量反馈；**AI 行为树按既有方案做 Phase-1 等价替换**，严格只输出输入帧、不碰数值权威。
