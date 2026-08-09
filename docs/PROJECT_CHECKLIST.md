# ACTGame 项目总清单

> 更新：2026-08-09 — A2 打击感（命中 VFX/SFX）验收；下一项 A5 行为树；Wave 4 位移已关；镜头归 Camera 篇  

> 角色：**一页总览**（进度 / 下一步 / 明确不做）  
> 细节真源勿与本文抢权威：
>
> | 主题 | 真源 |
> |------|------|
> | 设计方向与重构项 | [`.cursor/skills/actgame-architecture/ROADMAP.md`](../.cursor/skills/actgame-architecture/ROADMAP.md) |
> | 已实现功能与方案 | [`.cursor/skills/actgame-architecture/TECHNICAL.md`](../.cursor/skills/actgame-architecture/TECHNICAL.md) |
> | Wave / GAS 排期与验收 | [`2026.8.6/MASTER_IMPLEMENTATION_PLAN.md`](./2026.8.6/MASTER_IMPLEMENTATION_PLAN.md) |
> | 下一会话短清单 | [`2026.8.8/COMBAT_FEEL_AI_PRESENTATION_DAY_OUTLINE.md`](./2026.8.8/COMBAT_FEEL_AI_PRESENTATION_DAY_OUTLINE.md) |
> | 文档索引 | [`README.md`](./README.md) |

图例：✅ 完成 · 🟡 代码就绪 / 资产或体验未齐 · ⬜ 未开始 · ⏸ 后置

---

## 1. Demo 目标（现行）

构建可重复游玩的第三人称动作 Demo：

- 1 名可操控近战角色 + 至少 1 种近战敌人 / 木桩靶
- 普攻连段、闪避、Special/EX、大招门槛、完美闪避反击
- 60Hz 锁步 Sim + ActionGraph 数据驱动招式
- 第三人称相机；F3 Debug HUD（正式 UI 后置）
- 单场景内可验收战斗手感与 AI 基础行为

**明确不做（Demo）：** 装备、任务对话、大地图、完整存档、正式联机上线、独立 SkillExecutor、第二套血量/资源口袋。

**长期：** Action Editor 持续增强；联网走完整客户端预测 + 回滚（见锁步方案）。

---

## 2. 一页进度总览

| 域 | 状态 | 说明 |
|----|------|------|
| 核心框架 / 架构 IOC | ✅ | `ACTGameArchitecture`、Command/Query/Event |
| 60Hz SimulationHost | ✅ | L0A～L1B 代码；Play 回归仍待 |
| 输入 → GameplayIntent | ✅ | 量化 `InputFrame` + Intent Profile |
| Locomotion | 🟡 | 相位机已接；Phase D 减速/Pivot 位移未做 |
| ActionSim + Graph + Timeline | ✅ | 整数帧权威；Editor 基础可用 |
| 位移权威 / VisualResidual | ✅ | Wave 0～2.5（含删 RM/Legacy/ForwardOnly） |
| 战斗判定 / Reaction | ✅ | Pipeline 帧末结算；资产受击/死亡待齐 |
| Numeric / GAS-lite | ✅ | G0～G5；`NumericSystem` 唯一权威 |
| 资源循环 Special·EX·闪避·Ult | 🟡 | 代码闭环；Graph/Spec 资产持续填表 |
| 完美闪避反击 | ✅ | 窗轨 + Counter Entry（2026-08-08） |
| 敌人 AI | 🟡 | 五态 Brain 代码在；BT Phase-1 / 资产待做 |
| 相机 | 🟡 | 跟随 + 滤左右；Lock-On / SkillShot 未做 |
| 正式 UI / 血条 | ⬜ | 仅 Debug HUD；目标 MVVM |
| 吸附 / 绕背 | ✅ | Wave 4 位移出口（2026-08-09） |
| 预测回滚 / 联网 | ⬜ | L3 / L5 |
| 打击感木桩验收台 | ✅ | Monster_EDF + 关行动 + Hit_Shake；Play 验收 2026-08-08 |
| 命中 VFX/SFX（A2） | ✅ | HitFeedback + Cue；打击感验收 2026-08-09 |
| 学习/工程实践轨 | ⬜ | BT 编辑器、A*、AB/Lua、SDK、剧情等（§6.4） |

---

## 3. 当前焦点（立刻做什么）

**下一项：** 日计划 **A5 — AI 行为树 Phase-1**  
→ 方案：[`ENEMY_BEHAVIOR_TREE_PLAN.md`](./ENEMY_BEHAVIOR_TREE_PLAN.md)  
→ 日计划：[`2026.8.8/COMBAT_FEEL_AI_PRESENTATION_DAY_OUTLINE.md`](./2026.8.8/COMBAT_FEEL_AI_PRESENTATION_DAY_OUTLINE.md)  
→ A3 完美闪避 SlowMo / A4 轻量相机：可选后置（不挡 BT）

| # | 项 | 状态 |
|---|------|------|
| A0 | Editor：PerfectDodge 窗 + Counter Entry + Spec 抽查 | ✅ |
| A1 | 木桩靶（Numeric + Reaction + HitStop/震屏可观测） | ✅ 2026-08-08 |
| A2 | 打击 VFX/SFX + HitFeedback | ✅ 2026-08-09 验收 |
| A3 | 完美闪避子弹时间（表现，不改吞伤权威） | ⬜ 可选后置 |
| A4 | 相机轻优化（勿塞完整 Lock-On） | ⬜ 可选后置 |
| A5 | AI 行为树 Phase-1（只写 InputFrame） | ⬜ ← 当前 |

主排期：**Wave 4 位移 ✅ 已关闭**；Wave 5 仅可选玩法后置（失衡/命中盒烘焙）；相机 LockOn/SkillShot/Finisher 见 [`CAMERA_SYSTEM_PLAN.md`](./2026.8.6/CAMERA_SYSTEM_PLAN.md)。

---

## 4. Wave / GAS 勾选（摘要）

细节与验收以 MASTER 为准；此处只跟踪出口状态。

| 阶段 | 状态 | 备注 |
|------|------|------|
| Wave 0 观测保护网 | 🟡 | 0.1～0.3 ✅；0.4 人工基线手记可选 |
| Wave 1 位移止血 | ✅ | ForwardSigned + BaseMotionMode + 滤左右 |
| Wave 2 锚点闭环 | ✅ | Residual + VisualRoot；**2.5 删 RM 已落地** |
| Wave 3 资源循环 | 🟡 | 代码 ✅；Ult/EX 等资产持续绑 |
| GAS G0～G5 | ✅ | 2026-08-08 零兼容完成 |
| Wave 4 玩法位移 | ✅ | 吸附/SoftBody/Relocate；**不含相机**（2026-08-09） |
| Wave 5 可选后置（无镜头） | ⬜ | Daze/HeavyHit 可选；命中盒烘焙后置 |
| 相机系统（独立） | ⬜ | LockOn/Predict/SkillShot/Finisher → Camera 篇 C1～C4 |

**MASTER 整包成功标准（摘录）：**

- [x] Numeric 唯一真源（G5）
- [ ] 正式 Action 单一位移权威且全库校验无 Error（资产侧持续）
- [ ] 同键 EX + 闪避反击 + Ult 资产闭环齐
- [x] 吸附/绕背管线可重放（Wave 4；资产按需配 Relocate）
- [ ] Lock-On + 多段 SkillShot 纯表现（排期见 Camera 篇，不挂 Wave 4/5）

---

## 5. 锁步与模拟剩余

真源：[`ACTION_SYSTEM_LOCKSTEP_REFACTOR_PLAN.md`](./ACTION_SYSTEM_LOCKSTEP_REFACTOR_PLAN.md)

| 项 | 状态 |
|----|------|
| L0A～L1B 代码 + 60Hz 资产 | ✅ |
| L1B Play Mode 回归 + `ActionSim*` 测试 | ⬜ |
| L2 核心（表位移、HitStop、MotorSim、软分离、静态 AABB） | ✅ |
| L2 收口：斜坡/网格精确碰撞 | ⬜ |
| L3 Snapshot + 单机预测雏形 | ⬜ |
| L5 完整预测回滚 | ⬜ |

---

## 6. 系统模块清单

### 6.1 已可用（代码为主）

- [x] `SimulationHost` / `SimulationWorld` / 稳定 `SimActorId`
- [x] `CharacterActor` + `CharacterConfig` 装配；Controller 仅 Scene 入口
- [x] `ActionSim` + PresentationBridge + `ActionFrameQuery`
- [x] `ActionGraph`（Normal/Perfect、SharedRoute、多 Entry）
- [x] `CombatHitPipeline` 帧末结算 + `CharacterReactionService`
- [x] `NumericSystem`（Attribute / Effect / Flags）+ `NumericCostGate` + Vitality
- [x] Action Editor 基础时间轴 / Graph 编辑
- [x] F3 `CombatDebugHudController`
- [x] 敌人共享 Actor + 五态 Brain（资产待齐）

### 6.2 进行中 / 资产待绑

- [ ] 玩家/敌人受击与死亡 Action 全量配置
- [ ] Special / EX / Ultimate Graph + `ActionResourceSpec` 填表验收
- [ ] Locomotion Phase D：减速曲线、Pivot 位移、落脚编辑工具
- [ ] Action Editor：SFX 预览、校验强化、GraphView 润色
- [ ] 敌人 Definition / Graph / 动画资产齐套
- [x] A1 木桩 Prefab + 测试场景摆放（人工，✅ 2026-08-08）

### 6.3 待建设（战斗 / Demo 主线）

| 模块 | 优先级 | 说明 |
|------|--------|------|
| AI Behavior Tree 运行时 | P1 | [`ENEMY_BEHAVIOR_TREE_PLAN.md`](./ENEMY_BEHAVIOR_TREE_PLAN.md)；决策只写 `InputFrame` |
| Lock-On / Director | P1 | Camera 篇 C1（不挂 Wave 4） |
| TargetAdhesion / RelocateBehind | ✅ | Wave 4 已齐；Relocate 资产按需 |
| 正式 HUD（血条/资源条） | P2 | 替代 F3；实现走 §6.4 MVVM |
| SkillShot 多段镜头 | P2 | Camera 篇 C3（不挂 Wave 5） |
| 对象池 / 伤害数字 | P2 | 表现优化 |
| 场景胜负流 / Boot 流程 | P2 | Demo 包装 |
| 斜坡精确碰撞 | P2 | L2 收口 |
| 预测回滚联网 | P3 | L3→L5 |

### 6.4 学习与工程实践轨（可与主线交错，不阻塞 Wave）

> 目标：可演示的最小闭环 + 可替换接口；**不得**绕过 60Hz Sim / Numeric 权威，也不得把热更脚本写成第二套战斗数值口袋。

| 模块 | 优先级 | 范围与约束 |
|------|--------|------------|
| **MVVM UI 框架** | P2 | View / ViewModel / Model（或 Binder）分层；首版接血条、资源条、简易菜单；跨系统用现有 Command/Query/Event，UI 不直写 Domain 权威状态 |
| **性能优化学习测试实践** | P2 | 建立可复现基准：Profiler / Frame Debugger / 内存快照；固定测试场景（木桩连打、多敌人、相机）；记录 CPU/GC/DrawCall 基线与优化前后对比；优先验证 Hitbox、动画、VFX、UI 重建热点 |
| **简单行为树编辑器** | P2 | 自研轻量 Graph/节点编辑（Selector/Sequence/Decorator/Action 够用即可）；运行时经 **抽象接口**驱动（如 `IBehaviorTreeAsset` + `IBehaviorTreeRunner`），**预留可整体替换**为 Unity 行为树插件或第三方包，不把插件 API 泄漏进 `EnemyBrain` / Actor |
| **A\* 寻路** | P2 | 网格或导航点 A\* 学习实现；输出路径供 AI 移动意图；与锁步对齐时路径查询应确定性（或明确「仅表现/非 Hash」边界）；可先单机 Demo，再决定是否进 Sim |
| **AssetBundle + Lua 热更新** | P3 | AB 打包/加载/依赖与版本清单最小流程；Lua（或等价脚本）热更学习环境：热更 UI/配置/活动逻辑优先，**禁止**热更改写 ActionSim / Numeric 权威；与正式 C# 主循环边界写清 |
| **SDK 打包流程实践** | P3 | 渠道/平台 SDK 接入演练：登录、支付占位、隐私合规钩子、多渠道打包脚本（CI 或 Editor 菜单）；与热更包产出流水线可衔接 |
| **剧情编辑器** | P3 | 对话/镜头/标记位时间轴或节点图；播放器与 Gameplay 状态机解耦（暂停输入、切镜头、触发 Action 用事件/Command）；首版单场景线性剧情即可 |

**建议依赖顺序（实践轨内部）：**

```text
MVVM HUD 骨架
  → 性能基线场景（可与 A1 木桩共用）
  → BT 运行时接口 + 简易编辑器（可并行 A\* Demo）
  → 剧情编辑器（复用镜头/UI 事件）
  → AssetBundle / Lua 热更沙盒
  → SDK 打包流水线挂接热更产物
```

勾选进度（摘要）：

- [ ] MVVM UI 框架 + 首屏 HUD
- [ ] 性能基线场景与优化对照记录
- [ ] BT 抽象接口 + 简易编辑器（可替换插件）
- [ ] A\* Demo（寻路 → 移动意图）
- [ ] AssetBundle 加载闭环
- [ ] Lua（或脚本）热更沙盒（非战斗权威）
- [ ] SDK / 多渠道打包演练
- [ ] 剧情编辑器 + 单场景播放器

---

## 7. Tech Debt（观察）

摘自 ROADMAP，完成后回写两边：

- [ ] `CharacterActor` 与 `LocomotionState` 双处感知移动输入
- [ ] 业务程序集仍多在 Assembly-CSharp（仅 `Domain/Simulation` 已拆 asmdef）
- [ ] Action YAML 可能残留孤儿字段（如旧 `useRootMotion`），需 Editor 重存清洗
- [ ] TECHNICAL 功能索引部分条目日期需随 Wave 出口回写

---

## 8. 维护约定

1. **改进度先改真源**：Wave 勾选 → MASTER；重构项 → ROADMAP；功能方案 → TECHNICAL；本文只同步摘要表。  
2. **完成一大段后**更新本文 §2 / §3 / §4 状态，避免再次变成 2026-06 式过期清单。  
3. Agent **不改** `Assets/Data/**`、`.prefab`、`.asset`；清单中的 Editor 项由人工勾选。  
4. 旧版（2026-06-11、`Assets/_Game/` 路径）已废弃，勿恢复为实施入口。
