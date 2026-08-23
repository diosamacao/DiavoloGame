# ACTGame 2026.8.6 分步实施总案

> 制定：2026-08-06  
> 修订：2026-08-07 — 插入 GAS-lite G0～G5；完美闪避真源改为玩家 Dodge 窗；Wave 4 入口 = G5  
> 修订：2026-08-09 — Wave 4 位移切片（4.2/4.4 + SoftBody）Editor 验收收口；Relocate Command 已接线  
> 修订：2026-08-09 — **Wave 4 不再含相机**：原 4.5～4.6 Lock-On/Predict 撤出本 Wave；LockOn / SkillShot / Director 排期与实施归 [CAMERA_SYSTEM_PLAN.md](./CAMERA_SYSTEM_PLAN.md)  
> 修订：2026-08-09 — **Wave 5 也不再含大招演出**：原 5.1/5.2/5.5 SkillShot·Camera 轨·Finisher 撤出；一律归 Camera 篇  


> 基准：`develop`  
> 角色：**跨文档唯一排期与依赖真源**；单篇方案保留设计细节，阶段号与开工顺序以本文为准  
> 覆盖文档：
>
> - [SKILL_AND_RESOURCE_SYSTEM_PLAN.md](./SKILL_AND_RESOURCE_SYSTEM_PLAN.md)
> - [CAMERA_SYSTEM_PLAN.md](./CAMERA_SYSTEM_PLAN.md)
> - 关联真源：[COMBAT_NUMERICS_PLAN.md](../COMBAT_NUMERICS_PLAN.md)（字段语义 / N*）  
> - **数值改造真源：** [GAS_STYLE_COMBAT_REFACTOR_PLAN.md](../2026.8.7/GAS_STYLE_COMBAT_REFACTOR_PLAN.md)（G0～G5 已关）

---

## 1. 结论

1. 四篇方案方向正确，但不可并行按各自 A*/M*/S*/C* 独立开工。
2. 实施按 **Wave 0～5** 推进；每 Wave 有明确入口条件、交付物与验收，未验收不得进入下一 Wave 的破坏性删除。
3. **真源裁定**见 §2；冲突时以本文 + 对应「细节真源」为准，不以口头或旧归档文档为准。
4. Agent **不直接改** `Assets/Data/**`、`.asset`、Prefab；代码落地后按各篇 Editor 清单由人工绑定。

---

## 2. 真源裁定（强制）

| 主题 | 真源文档 | 其它文档职责 |
|------|----------|--------------|
| 排期 / 依赖 / 并行边界 | **本文** | 单篇只描述本域设计，阶段勾选同步本文 |
| `ActionResourceSpec` 字段语义 | **`COMBAT_NUMERICS_PLAN`** | 运行时存储/结算改造以 **GAS G*** 为准 |
| 数值口袋 / Resource·Health·Buff 终态 | **`GAS_STYLE_COMBAT_REFACTOR_PLAN`** | NUMERICS/Skill 只保留字段与产品语义 |
| Action 位移权威 / Modifier / Command | **TECHNICAL + 代码**（Wave 0～2.5 已关） | 本文只保留 Wave 裁定 |
| 烘焙轨迹 Gameplay/Residual | **TECHNICAL + 代码**（Wave 2 已关） | 逻辑表 + 可选残差，见 §3 |
| 相机模式 / LockOn / SkillShot | **Camera 篇** | 日常跟随锚点必须挂在无视觉残差的 `CameraRoot` |
| 技能槽 / Special·EX / 完美闪避产品 | **Skill 篇** | 阶段号对齐本文 Wave；存储迁 GAS Numeric |

**优先级冲突裁定（已定案）：**

| 原冲突 | 定案 |
|--------|------|
| NUMERICS N5「可选」vs Skill S2「必做」 | **同键 Special/EX 为首版必做**（Wave 3）；NUMERICS 视为同步修订 |
| 相机「滤左右即可」vs 锚点「必须拆轨迹」 | **Wave 1 可临时滤左右止血**；Wave 2 轨迹拆分落地后，滤左右降为构图缓冲，不得替代拆分 |
| `ActionBakedMotion` vs `ActionBakedTrajectory` | **一套数据**：逻辑表 + 可选残差；见 §3 |
| Spec 再堆无敌/硬直 | **禁止**；无敌/Poise 走 Timeline；Spec 只保留 NUMERICS 字段 + `tags` |
| 完美闪避窗口双来源 | **玩家 Dodge Timeline `PerfectDodgeWindow` 为唯一逻辑真源**；敌攻击窗内命中 → 完美闪避 |
| 长期保留 ResourceSim vs GAS-lite | ✅ G5 已零兼容删除；权威仅 `NumericSystem` |

---

## 3. 统一运动数据契约

运行时与烘焙只认以下结构（名称可落在 `ActionBakedTrajectory`，由现有 `ActionBakedMotion` 演进）：

```text
ActionBakedTrajectory
├─ logicHz (= 60，仅校验)
├─ frameCount (= Action.TotalFrames)
├─ baseMotionMode          // None | BakedMotion | ScriptedTimeline（来自 Action 执行策略）
├─ gameplayTrajectoryMode  // 首版仅：Exact | ForwardSigned | Stationary | Authored
├─ gameplayDeltaMmX/Z[]    // 进 MotorSim
├─ visualResidualMmX/Z[]   // 进 VisualMotionRoot（可空=全零）
├─ sourceHash / dataVersion
└─ （禁止运行时再从 Residual 反推逻辑位移）
```

**首版不做：** `DominantAxis`、`Smoothed` 作为独立运行时模式（Smoothed 仅可作 Editor 生成 Authored 初稿的离线工具）。

**位移组合顺序（全项目唯一）：**

```text
gameplayDelta / ScriptedMovement
  → MotionModifier（Wave 4）
  → MotionCommand（Wave 4）
  → CharacterMotorSim
```

视觉残差永远不进入上式。

---

## 4. 推荐层级（Wave 2 起）

```text
Player / SimulationRoot
├─ CharacterController（disabled，仅配置）
└─ CharacterPresentationRoot
   ├─ CameraRoot                 // 相机源：无视觉残差
   └─ CharacterVisualMotionRoot  // 动作视觉残差
      └─ Model / Animator
```

`CameraRig` 的 Follow/Predict/Orbit 只读 `CameraRoot`（经 Presentation 插值），禁止挂在 `VisualMotionRoot` 下。

---

## 5. Wave 总览

```text
Wave 0  观测与保护网（不改 Runtime 行为）
Wave 1  位移权威止血（ForwardSigned + BaseMotionMode + 相机临时滤左右）
Wave 2  稳定锚点闭环（双轨迹 + VisualMotionRoot + 删 RM 权威回退）
Wave 3  技能资源循环（产品语义；实现口袋即将被 GAS 替换）
G0～G5  GAS-lite 数值重构（Attribute+Effect；G5 零兼容）← Wave 4 前必做
Wave 4  玩法位移扩展（吸附 Modifier + 绕背 Command）← ✅ 位移出口
Wave 5  可选玩法后置（失衡等）+ 命中盒烘焙后置；**不含镜头**
※ LockOn / Predict / Director / SkillShot / Finisher 全部 → Camera 篇自管（不挂 Wave 4/5）
```

| Wave | 主要单篇映射 | 可并行 | 破坏性删除 |
|------|--------------|--------|------------|
| 0 | Action A0、Anchor M0、NUMERICS N0 | 全部可并行 | 无 |
| 1 | Anchor M1、Action A1、Camera C0（部分） | 三者可并行 | 无（旧字段只读迁移） |
| 2 | Anchor M2～M3、Action A2～A3、Camera 跟稳定根 | 代码可并行；资产迁移串行验收 | **删除 Animator RM 权威回退、旧 ForwardOnly 运行时语义** |
| 3 | Skill S0～S4 ≡ NUMERICS N0～N5 + Action A6 | HUD 与资源逻辑可交错 | 禁止散落 cost 字段 |
| **G*** | **GAS_STYLE_COMBAT_REFACTOR_PLAN** | G0～G2 少碰资产；G3 单出口切换 | **G5：删除 ResourceSim / 旧 Health·EnemyHealth** |
| 4 | Action A5 | 无相机任务 | 无 |
| 5 | Skill S5 可选、Anchor M4 后置 | 命中盒烘焙不阻塞；**无镜头任务** | 无 |

---

## 6. 分 Wave 实施

### Wave 0 — 观测与保护网

**目标：** 不改手感，摸清冲突资产与抖动来源。

| # | 任务 | 细节真源 | 验收 |
|---|------|----------|------|
| 0.1 | Action 全库校验报告（三源冲突 / Hz / 帧越界） | Action A0 | 每条 Action ∈ Baked / Scripted / None / Conflict |
| 0.2 | 轨迹与 Motor/相机锚点 Scene 可视化 | Anchor M0 | 能证明横跳来自运动表或 RM，而非软碰撞 |
| 0.3 | Debug HUD：HP / Intent / Buffer / Action 帧 / Lock | NUMERICS N0 | Play 可见连招与缓冲 |
| 0.4 | 选定 1～2 条高频横摆正式招作基准样例 | — | 记录 Motor 横向峰峰值基线 |

**入口：** 随时可开。  
**出口：** 0.1～0.3 完成；Conflict 列表归档。

#### Wave 0 落地状态（2026-08-06）

- [x] 0.1 `ACTGame/Action/Validate Motion Sources` + `ActionMotionSourceClassifier` + Audit Window
- [x] 0.2 Action Inspector「Show Baked Trajectory」+ Play Mode `CharacterAnchorGizmoDrawer`
- [x] 0.3 `CombatDebugHudController`（F3）+ `CharacterDebugSnapshot`（含 `ActionLateralPeakMm`）
- [ ] 0.4 人工：跑审计后选 1～2 条 Conflict/高横摆招，Play 记 HUD `ActionLateralPeakMm` 基线

**Editor 步骤：** 场景挂 `CombatDebugHudController`（可挂在现有 Debug/GameSystems 物体上，拖入 Player）；菜单跑校验并保存 Console 报告。

---

### Wave 1 — 位移权威止血

**目标：** 修正错误投影；显式运动模式；镜头临时稳定。

| # | 任务 | 细节真源 | 验收 |
|---|------|----------|------|
| 1.1 | 新增 `ForwardSigned`（累计 Z）；旧 `ForwardOnly` 标需重烘焙，**运行时不静默改语义** | Anchor M1 | 纯 X 横摆不再变前进 |
| 1.2 | 引入 `ActionBaseMotionMode`；无冲突资产自动迁移；冲突仅报告 | Action A1 | 已迁资产位移与迁前一致 |
| 1.3 | CameraRig / `lateralFollowFactor`（默认 0～0.1）；Motor 读 `PlanarForward` | Camera C0 | 侧向攻击镜头明显更稳；前向仍跟随 |
| 1.4 | `sampleRate`/`totalFrames` 开始只读校验（可先不删序列化） | Action A3 前半 | 烘焙 Hz≠60 或帧数不一致报 Error |

**迁移窗口（唯一允许的旧字段）：**

- `useRootMotion`：**只读**，供迁移工具推导；**不得**再写新逻辑分支依赖它。
- 窗口关闭点：Wave 2 出口（见 2.5）。

**禁止：** 本 Wave 不上吸附/瞬移；不删 RM 回退；不改 Prefab 层级挂模型。

#### Wave 1 落地状态（2026-08-06）

- [x] 1.1 `ForwardSigned` + `ForwardOnly` 旧语义保留；审计 Warning 提示重烘焙
- [x] 1.2 `ActionBaseMotionMode` + `ACTGame/Action/Migrate Base Motion Mode`（Conflict 跳过）
- [x] 1.3 `CameraManager.lateralFollowFactor`（默认 0.1）+ Motor `SetCameraPlanarBasis(Orbit Yaw)`
- [x] 1.4 审计 Error：`BAKED_HZ_MISMATCH` / `BAKED_FRAME_COUNT_MISMATCH` / `SAMPLE_RATE_NOT_60`

**Attack5 等横摆招 Editor 步骤：** Planar Mode 选 **ForwardSigned** → Bake Motion → Play 看 `ActionLateralPeakMm` 应接近 0；镜头左右也应明显减弱。

---

### Wave 2 — 稳定锚点闭环

**目标：** 逻辑/视觉拆分；正式动作单一位移权威。

| # | 任务 | 细节真源 | 验收 |
|---|------|----------|------|
| 2.1 | 烘焙输出 GameplayDelta + VisualResidual；模式限 Exact/ForwardSigned/Stationary/Authored | Anchor M2 | Editor 可预览三条轨迹；量化终点误差 ≤1mm |
| 2.2 | 工厂增加 `CharacterVisualMotionRoot`；表现桥写残差；取消/受击 BlendToZero | Anchor M3 | 模型有张力，Motor 无无效横摆 |
| 2.3 | `CameraRoot` 保持 Presentation 子节点、**不在** Visual 下；C0 滤左右保留为缓冲 | Camera + Anchor §8 | 原地连击基础锚点不再左右往返 |
| 2.4 | 正式动作全部 Baked 或 Scripted；关闭 Animator RM→Motor 入口 | Action A2 | **关闭 Animator 仍可位移/命中/结束** |
| 2.5 | **删除** `useRootMotion`、RM 权威回退、旧 ForwardOnly 运行时路径 | Action 删除清单 | 全库校验无 Error；无兼容分支 |
| 2.6 | CrossFade 显式 Override 迁移；取消 0 兼义 | Action A3/A4 | 硬切与未配置可区分 |

**入口：** Wave 1 出口 + Conflict 资产已人工决策。  
**出口：** 2.4～2.5 完成；基准样例 Motor 峰峰值进入阈值。

#### Wave 2 落地状态（2026-08-06，核心闭环）

- [x] 2.1 残差由原始表 − Gameplay（planarMode）运行时派生；Scene 画 Full/Gameplay/Residual
- [x] 2.2 工厂创建 `CharacterVisualMotionRoot` + `CharacterVisualMotionBridge`；结束 BlendToZero
- [x] 2.3 CameraRoot 仍挂 Presentation（与 Visual 并列）；不跟模型残差
- [x] 2.4 正式招全烘焙 / 关 Animator RM（内容已迁；2026-08-08 代码切断 RM→Motor）
- [x] 2.5 删除 `useRootMotion` / `LegacyResolve` / `ForwardOnly` 运行时与 Action RM 回退（2026-08-08）
- [x] 2.6 CrossFade 显式 Override（`hasCrossFadeOverride` / `crossFadeDuration`）

**Attack5 验收：** Planar=`ForwardSigned` 重烘焙 → Motor/`ActionLateralPeakMm`≈0，模型在 VisualMotionRoot 上仍左右摆。

**Editor 人工（代码完成后）：**

1. Attack5 等直线招用 **ForwardSigned** 重烘焙。  
2. Play：Hierarchy 应见 `CharacterPresentationRoot/CharacterVisualMotionRoot/Model`；`CameraRoot` 与 Visual 并列。  
3. 跑全库校验；2.4/2.5 待 Conflict=0 与正式招烘焙完成后再删 RM。

---

### Wave 3 — 技能资源循环

**目标：** 绝区零式单角色资源闭环；字段只认 NUMERICS。

| # | 任务 | 对齐 | 验收 |
|---|------|------|------|
| 3.1 | `CharacterResourceSim` + Config + Gate | N1 / Skill S1 | 能量不够不起手；挥空不回能 |
| 3.2 | `ActionResourceSpec` 挂 Action；Inspector Resource 分组 | Action A6 / NUMERICS | 无顶层散落 cost |
| 3.3 | Special Intent + Graph 能量分支（同键 EX） | **必做** N5/S2 | 临界上下打出两套招 |
| 3.4 | DodgeCharges + 完美闪避（**玩家 Dodge Timeline**）+ `PerfectDodgeAttack`→Counter | N3/S3 | 耗次数；窗内挨打吞伤→反击 |
| 3.5 | Decibel + Ult 清条 | N4/S4 | 满档可放；放后不能连放 |
| 3.6 | `freezeFrames>0` 时暂停被动回能与闪避充能 | Skill 定案 | 卡肉期资源不偷跑 |

#### Wave 3 落地状态（2026-08-06）

- [x] 3.1 `CharacterResourceSim` + Config + Gate；EditMode：`CharacterResourceSimTests` / `ActionSimResourceGateTests`
- [x] 3.2 `ActionDefinition.resourceSpec`；费用字段只认 Spec
- [x] 3.3 `GameplayIntentType.Special`（原 Skill=6）+ `Ultimate`；`ActionEnergyFormSelector` 同键 EX；HUD `Next Special`
- [x] 3.4 玩家 Dodge `PerfectDodgeWindow` + Pipeline 吞伤/武装 + `PerfectDodgeAttack` Intent/清缓冲 + Editor Counter Entry（✅ 2026-08-08 代码+资产）
- [x] 3.5 Sim 侧 Decibel 门槛/清条就绪；需 Graph Entry=`Ultimate` + 资产填 Spec（人工）
- [x] 3.6 卡肉期间跳过 `ResourceSim.Step`

**Editor 人工（不改 `.asset` by Agent）：** CharacterConfig.Resources；招式 `energyCost`/`energyGrantOnHit`/`resourceTag`；Graph 双 Entry（Special+ExSpecial）；Profile 绑定 Special/Ultimate。

**`ActionResourceSpec` 首版字段（与 NUMERICS 对齐，禁止加肥）：**

```text
tags / resourceTag
energyCost
energyGrantOnHit
decibelGrantOnHit
consumeDodgeCharge
requiresDecibelFull
clearsDecibelOnStart
```

EX/Ult 的「命中不回能」用 `energyGrantOnHit = 0` 表达，**不**另增 `grantsEnergyOnHit` 布尔。  
无敌 / 重击 / 完美窗：**玩家 Dodge Timeline**（Invincible / PerfectDodge），不进 Spec。

**入口：** Wave 2 出口（避免资源接在仍抖动的位移上调表）。  
**可提前：** 3.1 的纯 EditMode 单测可在 Wave 2 末并行，但不得依赖未删的 RM 回退。  
**后续：** Wave 3 玩法语义保留；**数值口袋改造走下方 G0～G5**，勿再扩展 `CharacterResourceSim` 权威字段。

---

### G0～G5 — GAS-lite 数值重构（Wave 3 后 / Wave 4 前）

**目标：** 用 `NumericSystem`（Attribute + Effect + 上下文旗标）替换 ResourceSim / 旧 Health / 独立 Buff；Action 骨架不动。  
**真源：** [GAS_STYLE_COMBAT_REFACTOR_PLAN.md](../2026.8.7/GAS_STYLE_COMBAT_REFACTOR_PLAN.md)

| # | 任务 | 验收摘要 |
|---|------|----------|
| G0 | 契约冻结、交叉文档对齐 | ✅ 2026-08-07 |
| G1 | AttributeSet + 聚合器 + Flags | ✅ 2026-08-07（未接 Actor） |
| G2 | EffectContainer | ✅ 2026-08-07（未接 Pipeline） |
| G3 | Gate/Pipeline 切换并切断旧写入 | ✅ 2026-08-07（Counter Intent 路由仍待 3.4） |
| G4 | 伤害成长 + DOT | ✅ 2026-08-07 |
| G5 | 零兼容清理 | ✅ 2026-08-08（`rg` 归零；Snapshot/HUD；文档完成态） |

**入口：** Wave 3 产品语义已代码落地（可与资产填表交错）。  
**出口（G5）：** ✅ 已达成；可开始 Wave 4 中依赖稳定数值 API 的新功能。  
**历史禁止（已兑现）：** 不得合入「ResourceSim 门面长期共存」。

---

### Wave 4 — 玩法位移扩展

**目标：** 吸附/绕背走确定性管线（**不含相机**）。

| # | 任务 | 细节真源 | 验收 |
|---|------|----------|------|
| 4.1 | `ActionMotionResolver`；ActionSim 只输出意图 | Action A5 | 职责不堆进 ActionDefinition |
| 4.2 | `TargetAdhesion` Modifier + 限制参数校验 | Action A5 | 超距/超角不生效；Hash 可重放 |
| 4.3 | `RelocateBehindTarget` + Facing/Collision/Fallback | Action A5 | 挡墙走 Fallback；关闭 Animator 结果不变 |
| 4.4 | 起手固化 `ActionTargetId` | Action A5 | 吸附不跳人 |

**已撤出（2026-08-09）：** 原 4.5 LockOn、4.6 Predict/Feedback → 改由 [CAMERA_SYSTEM_PLAN.md](./CAMERA_SYSTEM_PLAN.md)（C1/C2）独立排期，不再作为 Wave 4 出口条件。

**入口：** Wave 2 完成；**GAS G5 完成**（数值唯一真源为 NumericSystem）。  
**禁止：** Relocate 直接改 Transform；modifier 读表现骨骼；在旧 ResourceSim API 上堆 Wave 4 依赖。

**实施细则（2026-08-09）：** 以 `Unagi_Attack_Branch_02` 为样板（SoftBody 抑制 + 行程加长 + RelocateBehind）；运行时见 TECHNICAL「Wave 4」。

#### Wave 4 落地状态（2026-08-09）— ✅ 出口达成

- [x] **4.2 / 4.4 + SoftBodySuppress**：TargetAdhesion 管线 + Branch_02 Editor 验收收口（方案 A 过冲不倒拖；MotionModifier Scene 预览）
- [x] Branch_02 人工配窗与打击感位移验收（2026-08-09）
- [x] **4.1 / 4.3**：`ActionMotionResolver` + RelocateBehind / MotionCommand 已接线（Bridge；资产按需配点事件）

**打击感相关：** 木桩 / 命中 Cue / Branch_02 吸附等位移手感优化 **至此告一段落**；Wave 4 **位移出口已关闭**。相机后续见 Camera 篇。

---

### Wave 5 — 可选玩法后置项（不含相机）

**目标：** 不阻塞主循环的玩法/模拟后置项。  
**已撤出（2026-08-09）：** 原 5.1 SkillShot、5.2 Action Editor Camera 轨、5.5 Timeline Finisher/过场 → 全部改由 [CAMERA_SYSTEM_PLAN.md](./CAMERA_SYSTEM_PLAN.md)（C3/C4）独立排期，**不再作为 Wave 5 出口或产品勾选**。

| # | 任务 | 优先级 | 验收 |
|---|------|--------|------|
| 5.1 | 敌方 Daze + HeavyHit（可选） | P2 | 不阻塞主循环 |
| 5.2 | 命中盒挂点烘焙，去掉运行时 Animator 采样 | **后置** | 同输入命中 Hash 一致 |

**入口：** Wave 3 产品语义可用 + Wave 4 位移出口 ✅。镜头能力与 Wave 5 解耦，按需开 Camera 篇即可。

---

## 7. 明确不要并行的事项

| 组合 | 原因 |
|------|------|
| 未做 Wave 1.1 就批量重烘焙 | 会固化错误 ForwardOnly 语义 |
| Wave 2 未删 RM 回退就接 Wave 4 吸附 | 隐式双权威放大吸附误差 |
| Wave 3 与另立「SkillExecutor」 | 禁止；技能=Action+Spec+Gate |
| 只做相机滤左右、跳过 Wave 2 | 控制器与碰撞仍横跳 |
| Spec 与 Timeline 双写无敌 | 双权威 |
| Agent 手改正式 `.asset` 位移/费用 | 用迁移工具 + 人工确认 Conflict |

---

## 8. 测试门禁（每 Wave 出口必跑）

| Wave | 最低测试 |
|------|----------|
| 0 | 校验报告可重复生成 |
| 1 | ForwardSigned EditMode；侧向攻击镜头 Play 对比 |
| 2 | 关闭 Animator 位移/命中；残差重建 Full；Motor 哈希重放 |
| 3 | Gate 单次扣费；Cancel 不够费不切换；ConfirmHit 才 Grant；同键 EX |
| 4 | 吸附/绕背 EditMode + 双 Actor 同帧重定位 |
| 5 | （可选）Daze/HeavyHit 行为正确；命中盒烘焙后同输入 Hash 一致 |

---

## 9. 文档维护约定

1. 完成某 Wave 出口后：在本文对应任务打勾，并回写相关单篇「分阶段」复选框。  
2. 变更字段或模式枚举：先改真源文档，再改本文 §2/§3。  
3. 已删除的归档稿（旧 Resource/Attributes 独立计划等）**不要**再复活为实施入口；字段与口袋以 NUMERICS + GAS 为准。  
4. 单篇中的阶段号（A*/M*/S*/C*）仅作细节索引；**排期冲突以本文 Wave 为准**。

---

## 10. 成功标准（整包）

- [ ] 每个正式 Action 仅一个基础位移权威；无 Animator RM 逻辑回退  
- [ ] 无玩法意义的横摆不进 MotorSim / 受击体 / 相机源  
- [ ] 资源字段唯一来自 `ActionResourceSpec`（NUMERICS 表）；同键 EX + 闪避反击 + Ult 闭环  
- [x] GAS G5：数值唯一真源为 `NumericSystem`；旧 ResourceSim/Health 权威已删（2026-08-08）  
- [ ] 吸附/瞬移经 MotionResolver + MotorSim，可重放  
- [ ] （Camera 篇）Lock-On / SkillShot 等为纯表现，不写回 Sim；不阻塞 Wave 4/5 出口
- [ ] 全库 Action 校验无 Error；关键路径 EditMode/Play 门禁通过  

---

## 11. 一句话

先用校验与 `ForwardSigned` 止血，再用 Gameplay/Residual 稳住锚点并删除 RM 回退，接入唯一资源 Spec 跑通绝区零式循环，再以 **GAS-lite G0～G5** 收敛数值口袋，最后上吸附瞬移；**Lock-On / SkillShot 等相机能力由 Camera 篇独立排期**——位移与数值真源不能分叉。
