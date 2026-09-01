# ACTGame 项目总清单

> 更新：2026-09-01 — P-SW1 三槽稳定实体与普通切人代码完成，Editor 验收待办

> | 主题 | 真源 |
> |------|------|
> | 设计方向与重构项 | [`.cursor/skills/actgame-architecture/ROADMAP.md`](../.cursor/skills/actgame-architecture/ROADMAP.md) |
> | 已实现功能与方案 | [`.cursor/skills/actgame-architecture/TECHNICAL.md`](../.cursor/skills/actgame-architecture/TECHNICAL.md) |
> | 战斗 / 位移 Wave | [`2026.8.6/MASTER_IMPLEMENTATION_PLAN.md`](./2026.8.6/MASTER_IMPLEMENTATION_PLAN.md) |
> | 联网实现 | [`2026.8.23/NETSYNC_FROM_JOIN_TO_HIT.md`](./2026.8.23/NETSYNC_FROM_JOIN_TO_HIT.md) |
> | 文档索引 | [`README.md`](./README.md) |

图例：✅ 完成 · 🟡 代码就绪 / 资产或体验未齐 · ⬜ 未开始 · ⏸ 后置

---

## 1. Demo 目标（现行）

构建可重复游玩的第三人称动作 Demo：

- 最多 3 名可出战角色轮换（方案：[`2026.8.30/PARTY_SWITCH_ASSIST_PLAN.md`](./2026.8.30/PARTY_SWITCH_ASSIST_PLAN.md)）+ 至少 1 种近战敌人 / 木桩靶
- 普攻连段、闪避、Special/EX、大招门槛、完美闪避反击；切人弹刀按 P-SW 阶段落地
- 60Hz 锁步 Sim + ActionGraph 数据驱动招式
- 第三人称相机；F3 Debug HUD（正式 UI 后置）
- 单场景内可验收战斗手感与 AI 基础行为

**明确不做（Demo）：** 装备、任务对话、大地图、完整存档、正式联机上线、独立 SkillExecutor、第二套血量/资源口袋。

**长期：** Action Editor 持续增强；联网 = Dedicated 权威状态同步 + 客机 Autonomous 预测。

---

## 2. 一页进度总览

| 域 | 状态 | 说明 |
|----|------|------|
| 核心框架 / 架构 IOC | ✅ | `ACTGameArchitecture`、Command/Query/Event |
| 60Hz SimulationHost | ✅ | L0A～L1B 代码；Play 回归仍待 |
| 输入 → GameplayIntent | ✅ | 量化 `InputFrame` + Intent Profile |
| Locomotion | ✅ | 相位 + 方向 AnimSet + Pivot 两段式；急停减速曲线**不做** |
| ActionSim + Graph + Timeline | ✅ | 整数帧权威；Editor 基础可用 |
| 位移权威 / VisualResidual | ✅ | Wave 0～2.5（已删 RM/Legacy/ForwardOnly 回退） |
| 战斗判定 / Reaction | ✅ | Pipeline 帧末结算；资产受击/死亡待齐 |
| Numeric / GAS-lite | ✅ | G0～G5；`NumericSystem` 唯一权威 |
| 资源循环 Special·EX·闪避·Ult | 🟡 | 代码闭环；Graph/Spec 资产持续填表 |
| 完美闪避反击 | ✅ | 窗轨 + Counter Entry |
| 敌人 AI | ✅ | Desire + Entry Request；编辑器待优化见 8.11 Backlog |
| 相机 | 🟡 | 跟随 + 滤左右；Director / Lock-On / SkillShot / UI 展示舱见 8.26 篇 |
| 正式 UI / 血条 | ⬜ | 仅 Debug HUD；目标 MVVM |
| 吸附 / 绕背 | ✅ | Wave 4 位移出口 |
| 预测 / 联网 | 🟡 | W10/W11 代码切面；Play 未关；不得称公网可用 |
| 打击感木桩 / 命中 Cue | ✅ | 2026-08-08 / 08-09 验收 |
| 三人换人 / 极限支援 | 🟡 | P-SW1 三 Actor 与复制完成；普通退场统一为原招 Recovery→SwitchOut→其 Recovery 隐藏，Graph/Test/Play 待验 |
| 学习/工程实践轨 | ⬜ | A*、AB/Lua、SDK、剧情等（§6.4） |

---

## 3. 当前焦点

**下一项（联网）：** W11 Play（远敌裁剪 / Owner 不被饿死）或用户指定的 W12；W10 Clumsy 验收仍开放。

下行带宽三项方案已立、**未实现**：[`2026.8.24/README.md`](./2026.8.24/README.md)（`RS-M` 掩码 → `RS-S` 分频 → `RS-C` 推帧）。未点名实现前不挡 W10/W11 Play。

- 实现阅读：[`2026.8.23/NETSYNC_FROM_JOIN_TO_HIT.md`](./2026.8.23/NETSYNC_FROM_JOIN_TO_HIT.md)
- 排期：[`2026.8.17/NETSYNC_FRAMEWORK_DEDICATED_MASTER_DEVELOPMENT_PLAN.md`](./2026.8.17/NETSYNC_FRAMEWORK_DEDICATED_MASTER_DEVELOPMENT_PLAN.md)
- 启动：[`2026.8.19/DEDICATED_SERVER_LAUNCH.md`](./2026.8.19/DEDICATED_SERVER_LAUNCH.md)
- 踩坑：[`2026.8.20/NETSYNC_ARCHITECTURE_PROBLEMS.md`](./2026.8.20/NETSYNC_ARCHITECTURE_PROBLEMS.md)

战斗主线：Wave 4 位移已关；Wave 5 仅可选后置；相机见 [`2026.8.26/CAMERA_SYSTEM_PLAN.md`](./2026.8.26/CAMERA_SYSTEM_PLAN.md)。

---

## 4. Wave / GAS 勾选（摘要）

细节与验收以 MASTER 为准。

| 阶段 | 状态 | 备注 |
|------|------|------|
| Wave 0～2.5 位移 / 锚点 | ✅ | 0.4 人工基线手记可选，不阻塞 |
| Wave 3 资源循环 | 🟡 | 代码 ✅；Ult/EX 等资产持续绑 |
| GAS G0～G5 | ✅ | 零兼容完成 |
| Wave 4 玩法位移 | ✅ | 吸附/SoftBody/Relocate；**不含相机** |
| Wave 5 可选后置 | ⬜ | Daze/HeavyHit 可选；命中盒烘焙后置 |
| 相机系统（独立） | ⬜ | C1 Director/LockOn · C3 SkillShot · C5 UI 展示舱 → [`2026.8.26`](./2026.8.26/CAMERA_SYSTEM_PLAN.md) |

**整包仍开放：**

- [ ] 正式 Action 单一位移权威且全库校验无 Error（资产侧）
- [ ] 同键 EX + 闪避反击 + Ult 资产闭环齐
- [ ] Lock-On + 多段 SkillShot + UI 展示舱（纯表现）

---

## 5. 锁步与联网剩余

模拟核：[`ACTION_SYSTEM_LOCKSTEP_REFACTOR_PLAN.md`](./ACTION_SYSTEM_LOCKSTEP_REFACTOR_PLAN.md)  
联网：[`2026.8.23/NETSYNC_FROM_JOIN_TO_HIT.md`](./2026.8.23/NETSYNC_FROM_JOIN_TO_HIT.md) + [`2026.8.17` 总排期](./2026.8.17/NETSYNC_FRAMEWORK_DEDICATED_MASTER_DEVELOPMENT_PLAN.md)

| 项 | 状态 |
|----|------|
| L0A～L2 核心 + 60Hz 资产 | ✅ |
| L1B Play Mode 回归 + `ActionSim*` | ⬜ |
| L2 斜坡/网格精确碰撞 | ⬜ |
| L3 可导出复制快照（纠偏用，非 GGPO） | ⬜ |
| NS0～NS5 / W0～W9 | ✅ |
| W10 预测 / 可靠通道 / 网络时间 | 🟡 代码切面；Play 暂缓 |
| W11 Delta / Relevancy / FakeActionGame | 🟡 代码切面；R2 未关 |
| W12 公网 / 重连 / 运维 | ⬜ |
| 复制带宽 RS-M / RS-S / RS-C | ⬜ 方案 [`2026.8.24`](./2026.8.24/README.md) |
| L5 全员输入广播 + 完整回滚 | ❌ 已取消产品主路径 |

---

## 6. 系统模块清单

### 6.1 已可用（代码为主）

- [x] `SimulationHost` / `SimulationWorld` / 稳定 `SimActorId`
- [x] `CharacterActor` + `CharacterConfig` 装配；Controller 仅 Scene 入口
- [x] `ActionSim` + PresentationBridge + `ActionFrameQuery`
- [x] `ActionGraph`（Normal/Perfect、SharedRoute、多 Entry）
- [x] `CombatHitPipeline` 帧末结算 + `CharacterReactionService`
- [x] `NumericSystem` + `NumericCostGate` + Vitality
- [x] Action Editor 基础时间轴 / Graph 编辑
- [x] F3 `CombatDebugHudController`
- [x] 敌人共享 Actor + BT Runner + Desire/Request

### 6.2 进行中 / 资产待绑

- [ ] 玩家/敌人受击与死亡 Action 全量配置
- [ ] Special / EX / Ultimate Graph + `ActionResourceSpec` 填表
- [ ] Action Editor：SFX 预览、校验强化、GraphView 润色
- [ ] 敌人 Definition / Graph / 动画资产齐套

### 6.3 待建设（战斗 / Demo 主线）

| 模块 | 优先级 | 说明 |
|------|--------|------|
| Lock-On / Director | P1 | Camera C1（[`2026.8.26`](./2026.8.26/CAMERA_SYSTEM_PLAN.md)） |
| 正式 HUD（血条/资源条） | P2 | 替代 F3；实现走 §6.4 MVVM |
| SkillShot 多段镜头 | P1 | Camera C3（大招多机位） |
| UI 展示舱 | P1 | Camera C5；与战斗 Brain 隔离 |
| 对象池 / 伤害数字 | P2 | 表现优化 |
| 场景胜负流 / Boot 流程 | P2 | Demo 包装 |
| 斜坡精确碰撞 | P2 | L2 收口 |
| 联网 Play / W12 | P1 | 见 §3；不得称公网可用 |
| 三人换人 / 弹刀 | P1 | [`2026.8.30`](./2026.8.30/PARTY_SWITCH_ASSIST_PLAN.md)；先 P-SW1 普通切人 |

### 6.4 学习与工程实践轨

不得绕过 60Hz Sim / Numeric 权威，也不得把热更脚本写成第二套战斗数值口袋。

| 模块 | 优先级 | 范围 |
|------|--------|------|
| MVVM UI 框架 | P2 | 首版接血条、资源条、简易菜单 |
| 性能优化实践 | P2 | 木桩/多敌人基线 + Profiler 对照 |
| 行为树编辑器 | P2 | GraphView MVP 已有；待优化见 [`ENEMY_BEHAVIOR_TREE_BACKLOG_PLAN.md`](./2026.8.11/ENEMY_BEHAVIOR_TREE_BACKLOG_PLAN.md) |
| A\* 寻路 | P2 | 路径 → AI 移动意图；锁步确定性边界待定 |
| AssetBundle + Lua 热更 | P3 | 禁止热更改写 ActionSim / Numeric |
| SDK 打包流程 | P3 | 渠道接入演练 |
| 剧情编辑器 | P3 | 与 Gameplay 用事件解耦 |

勾选：

- [ ] MVVM UI + 首屏 HUD
- [ ] 性能基线与优化对照
- [x] BT 抽象 + GraphView + Desire/Request（2026-08-11）
- [ ] A\* Demo
- [ ] AssetBundle / Lua 热更沙盒
- [ ] SDK / 多渠道打包
- [ ] 剧情编辑器 + 单场景播放器

---

## 7. Tech Debt（观察）

完成后回写 ROADMAP：

- [ ] 业务程序集仍多在 Assembly-CSharp（仅 `Domain/Simulation` 已拆 asmdef）
- [ ] Action YAML 可能残留孤儿字段，需 Editor 重存清洗

---

## 8. 维护约定

1. **改进度先改真源**：Wave → MASTER；重构项 → ROADMAP；功能方案 → TECHNICAL；本文只同步摘要表。
2. **完成一大段后**更新本文 §2 / §3 / §4，避免再次堆过期清单。
3. Agent **不改** `Assets/Data/**`、`.prefab`、`.asset`；清单中的 Editor 项由人工勾选。
4. 已关闭方案不再保留归档副本；现行阅读入口见 [`README.md`](./README.md)。
