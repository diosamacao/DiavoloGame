# Wave 4 — 玩法位移扩展（Branch_02 穿敌绕背）实施细则

> 制定：2026-08-09  
> 修订：2026-08-09 — 主路径改为攻击吸附；吸附点按 **玩家↔敌人连线动态计算** + 水平距离偏移；**窗口时长 = 吸附完成时长**  
> 修订：2026-08-09 — **Editor / Play 验收通过**；位移切片（P0～P2 + P4）收口；P3 Relocate / P5 Lock-On 仍可选未做  
> 角色：**Wave 4 位移切片可执行真源**（类型 / 管线 / 验收 / Editor）  
> 排期与依赖仍以 [MASTER_IMPLEMENTATION_PLAN.md](../2026.8.6/MASTER_IMPLEMENTATION_PLAN.md) 为准  
> 设计细节对齐：[ACTION_DEFINITION_OPTIMIZATION_PLAN.md](../2026.8.6/ACTION_DEFINITION_OPTIMIZATION_PLAN.md) §4.2 / Phase A5  
> 样板招：`Unagi_Attack_Branch_02`（及 Perfect 变体复用同管线）

---

## 0. 一句话

落地 Wave 4 **TargetAdhesion 窗口**：每帧沿 **玩家→敌人水平连线** 动态算吸附点，并用可配的 **水平距离偏移** 决定落在敌前/敌心/敌后（穿身）；**窗口起止帧决定吸附时长**（剩余误差摊到剩余帧）。配合 SoftBody 抑制穿身；Relocate 仅可选补钉。

---

## 0.1 定案说明

| 问题 | 曾考虑 | **现定案** |
|------|--------|------------|
| 核心玩法 | Bake 加长 / 瞬移背后 | **攻击吸附 Modifier 窗口** |
| 吸附点 | 敌人 `forward` 背后点（固定朝向） | **动态**：`enemy + normalize(enemy−player) * horizontalOffsetMm` |
| 偏移语义 | `behindDistance` 沿敌人朝向 | **水平距离偏移**沿连线：`>0` 穿到敌后侧，`=0` 吸敌心，`<0` 停在敌前 |
| 吸附时长 | 仅靠每帧 `maxCorrection` 慢慢吸 | **窗口时长 = 计划完成时长**（按剩余帧均摊，可再夹每帧上限） |
| SoftBody | — | **仍必做** |
| RelocateBehind | 曾作主终点 | **可选** |

---

## 1. 入口条件（已满足）

| 条件 | 状态 | 备注 |
|------|------|------|
| Wave 2：Baked/Scripted 唯一基础位移；RM→Motor 已删 | ✅ | `CharacterActionPresentationBridge` |
| Wave 3 + GAS G5：Numeric 唯一权威 | ✅ | 本切片不扩资源口袋 |
| 总案 Wave 4 入口 | ✅ | 可开工 4.1～4.4；Lock-On（4.5）可并行不阻塞本切片 |

**现状（2026-08-09 Editor 验收后）：**

- ✅ TargetAdhesion + SoftBodySuppress 已接线；Editor MotionModifier 轨 + Scene 假敌预览
- ✅ Branch_02 人工配窗并完成 Editor 验收；打击感位移切片收口
- ⬜ MotionCommand / RelocateBehind **未接线**（P3 可选，不阻塞）
- ⬜ Lock-On（P5 / 总案 4.5～4.6）未开工

---

## 2. 产品目标（Branch_02）

```text
起手固化 ActionTargetId
  → SoftBodySuppress 窗（可叠人，仍碰墙）
  → TargetAdhesion 窗（与冲刺重叠）：
       每帧动态算 desired（玩家↔敌人连线 + 水平偏移）
       在窗口剩余帧内摊完误差（窗口时长=吸附时长）
  → （可选）Relocate 补钉
  → Hitbox / Cancel
```

**动态吸附点（平面，毫米）：**

```text
player = actor.positionMm.xz
enemy  = target.positionMm.xz
axis   = Normalize(enemy - player)     // 玩家→敌人连线；共线退化时用 actor.forward
desired = enemy + axis * horizontalOffsetMm
          + Perp(axis) * lateralOffsetMm   // 可选侧向，默认 0

// horizontalOffsetMm：
//   > 0  → 沿连线穿过敌人，落在「敌后侧」（Branch_02 穿身）
//   = 0  → 吸向敌人中心
//   < 0  → 停在敌人身前（贴脸距离）
```

敌人移动时 **每帧重算** `axis` / `desired`（跟锁目标走），不是起手冻死一个世界点。

| 期望手感 | 非目标 |
|----------|--------|
| 按敌位加长行程；偏移>0 时穿到连线另一侧 | 按敌人朝向「背后」死板点（敌转身吸附点乱跳可另议） |
| 吸附过程长度 = 窗口帧数 | 窗口外继续吸 / 单帧甩到 desired |
| 可叠人、不穿静物墙 | `IgnoreAll` 战斗权威 |
| 可 Hash 重放 | 读表现骨骼 |

---

## 3. 架构定案

### 3.1 位移组合顺序（全项目唯一）

```text
BaseDelta（BakedMotion / ScriptedTimeline / None）
  → MotionModifiers（连续：Adhesion / SoftBodySuppress 等）
  → MotionCommands（离散：RelocateBehindTarget 等）
  → CharacterMotorSim（静态碰撞 ResolveMove）
  → SoftBodySeparation（可被窗抑制）
```

视觉残差（`CharacterVisualMotionRoot`）**永不**进入上式。

### 3.2 推荐组合（连线动态吸附为主）

| 层 | 职责 | Branch_02 |
|----|------|-----------|
| SoftBody 抑制窗 | 穿身不被弹开 | **必配**（建议 ≥ Adhesion 窗） |
| **TargetAdhesion 窗** | 连线动态 desired + 水平偏移；按时长摊误差 | **主路径**；`horizontalOffsetMm > 0` |
| 既有 BakedMotion | 基础前冲曲线 | 与修正叠加 |
| RelocateBehind | 可选补钉 | 非必须 |

### 3.2.1 窗口时长 = 吸附时长（强制语义）

`TargetAdhesion` **必须是区间窗口**（`startFrame`～`endFrame`），不是点事件。

```text
remainingFrames = endFrame - currentFrame + 1   // 含本帧
error = desired - actor.position                 // 平面
forwardGap = Dot(error, actor.forward)          // >0 desired 仍在朝向前方
// 方案 A：只补朝向前方缺口；过冲/身后不倒拖
if forwardGap <= 0: correction = 0
else:
  planned = (forward * forwardGap + right * Dot(error, right)) / remainingFrames
  correction = ClampMagnitude(planned, maxCorrectionMmPerFrame)
finalDelta = baseDelta + correction
```

| 规则 | 说明 |
|------|------|
| 窗口越长 | 同样距离吸得越「肉」、越跟得上动画 |
| 窗口越短 | 同样距离吸得越猛；触顶 `maxCorrection` 时可能窗末吸不完 |
| 窗末 | 不保证数学上误差=0（有墙/夹逼/目标移动）；不在窗外继续吸 |
| Enter | 可缓存 `ActionTargetId`（已在起手固化则复用） |
| Exit / 目标丢失 | 停止修正；不补瞬移（除非另配 Relocate） |

`maxCorrectionMmPerFrame`：防单帧爆炸的安全阀；**主节奏由窗口长度决定**，不是只靠这个常量慢慢磨。

### 3.3 碰撞策略语义

| `MotionCollisionPolicy` | 行为 | Branch_02 |
|-------------------------|------|-----------|
| `RequireFreeSpace` | 占用则失败 | 不用 |
| `FindNearestValid` | 确定性候选最近点 | Relocate Fallback |
| `IgnoreCharacters` | 忽略角色软体，**仍碰静态墙** | Relocate / 冲刺默认 |
| `IgnoreAll` | 仅调试/演出 | **禁止**作战斗权威 |

软体抑制与 `IgnoreCharacters` 互补：

- **连续位移帧**：Motor 仍 `ResolveMove`（墙）；`ParticipatesInSoftBodySeparation=false` 跳过互撞
- **Relocate**：`ActionMotionRelocation` 按 policy 解析落点；成功后可再写 `SoftBodySuppressFrames`

### 3.4 目标与朝向

| 项 | 定案 |
|----|------|
| 目标固化 | 动作起手写 `ActionTargetId`（优先当前 Lock；无效则 Command/Adhesion 不生效） |
| `MotionTargetSource` | Branch_02 用 `ActionTarget`；禁止中途跳人 |
| Pose 来源 | 仅 `SimActorId` + `SimCombatPose`（World Query） |
| Relocate 朝向 | `FaceTarget`（绕背后面对敌人） |
| Fallback | 默认 `FindNearestValid` → 仍失败则 `CancelCommand`（动作继续） |

---

## 4. 数据契约（Timeline）

### 4.1 MotionModifier 窗口

```text
MotionModifierNotifyState : ActionNotifyState   // 区间：startFrame / endFrame
├─ mode
│  ├─ SoftBodySuppress          // P1
│  ├─ TargetAdhesion            // P2 主路径
│  ├─ FaceTarget                // 可选
│  └─ ClampTargetDistance       // 可选
├─ targetSource                 // Adhesion 用；Suppress 可忽略
│
│  // —— TargetAdhesion 专用 ——
├─ horizontalOffsetMm           // 沿 玩家→敌人 连线，相对敌人中心的水平偏移（可负）
├─ lateralOffsetMm              // 沿连线法线的侧向偏移，默认 0
├─ maxCorrectionMmPerFrame      // 安全夹逼；主节奏看窗口长度
├─ maxAcquireDistanceMm         // 玩家到敌人距离超限则本帧不吸
├─ maxAngleMilliDeg             // 可选：连线与角色朝向夹角超限不吸
└─ stopOnTargetLost
```

**SoftBodySuppress：** 窗内攻击者不参与软体分离；仍碰静态墙。

**TargetAdhesion（动态连线 + 窗口均摊）：**

```text
// 1) 动态 desired（每逻辑帧）
axis = Normalize((enemy - player).xz)
desired = enemy + axis * horizontalOffsetMm
        + Perp(axis) * lateralOffsetMm

// 2) 只补朝向前方缺口，按剩余帧均摊（方案 A：过冲不倒拖）
remainingFrames = max(1, endFrame - frame + 1)
error = desired - player
forwardGap = Dot(error, actor.forward)
if forwardGap <= 0: correction = 0        // desired 已在身后 / Bake 过冲
else:
  planned = (fwd*forwardGap + right*Dot(error,right)) / remainingFrames
  correction = ClampMagnitude(planned, maxCorrectionMmPerFrame)

// 3) 与基础位移合成
finalDelta = baseDelta + correction
→ MotorSim.TryMove（静物碰撞仍生效）
```

约束：

- 仅 `IsActiveAtFrame` 为真时修正；**窗外不吸**  
- 超 acquire / 角度 / 目标丢失 → 本帧 correction=0  
- **过冲后 desired 落到朝向后方时不反向拉回**  
- **禁止** `correction = error` 整段瞬移（除非另配 MotionCommand）  
- `horizontalOffsetMm` 与敌人朝向无关，只跟玩家–敌人相对位置有关（敌转身不改「穿过去」方向）

### 4.2 MotionCommand 点事件

```text
MotionCommandNotify : ActionNotify
├─ commandType
│  ├─ RelocateBehindTarget      // 可选补钉，非 Branch_02 主路径
│  ├─ RelocateToTargetOffset
│  └─ SnapFacingToTarget
├─ targetSource
├─ behindDistanceMm             // RelocateBehind
├─ localOffsetMm                // x=目标右，z=沿背后/局部
├─ facingPolicy                 // PreserveCurrent / FaceTarget / MatchTarget / FaceDestination
├─ collisionPolicy
├─ fallbackPolicy               // CancelCommand / CancelAction / UseForwardOffset
├─ forwardFallbackMm
├─ softBodySuppressFrames       // 落地后继续抑制 N 逻辑帧
└─ preserveVertical             // 默认 true
```

**RelocateBehindTarget 权威计算：**

```text
targetPose = World.GetCommittedPose(actionTargetId)
behind = -targetPose.forward * behindDistanceMm
desired = targetPose.position + behind + targetPose.right * sideOffsetMm
resolved = CollisionWorld.ResolveRelocation(shape, desired, IgnoreCharacters)
facing = FaceTarget(actor, target)   // 用解析后落点无关；FaceDestination 才用 resolved
MotorSim.TeleportMm(resolved) + SetFacing(facing)
softBodySuppress = max(softBodySuppress, command.softBodySuppressFrames)
```

### 4.3 Action / Actor 运行时状态

| 字段 | 位置 | 说明 |
|------|------|------|
| `ActionTargetId` | ActionSim 或 Action 会话 | 起手固化，Cancel/结束清空 |
| `SoftBodySuppressFrames` | CharacterActor / Motor 会话 | 每逻辑帧递减；>0 时不参与软体 |

---

## 5. 代码落点（建议路径）

| 模块 | 路径 / 类 | 职责 |
|------|-----------|------|
| 契约枚举与 Notify | `Domain/Combat/Actions/Definitions/Timeline/` | Modifier / Command 数据 |
| Timeline | `ActionTimeline` + `ActionEventKind` / TrackKind | 数组与枚举轨 |
| Resolver | `Domain/Character/Motion/ActionMotionResolver` | 已有草稿，补齐依赖后接线 |
| Relocation / Facing | 同目录新建 | 确定性找点与朝向 |
| WorldQuery | `IActionMotionWorldQuery` 由 SimulationWorld / Host 实现 | Committed Pose |
| 帧消费 | `CharacterActionPresentationBridge` 或专用 `ActionMotionFrameConsumer` | Base→Modifier→Command |
| SoftBody | `CharacterActor.ParticipatesInSoftBodySeparation` | 尊重 suppress |
| Editor | Action Editor 加轨 + 选中 Inspector；Gizmo 画 behind 点 | 人工配 Branch_02 |
| 测试 | `Assets/Tests/EditMode/...` | 见 §8 |

**删除 / 禁止：**

- 直接 `transform.position =`
- 运行时读 Animator RootT 作权威位移
- 在旧 ResourceSim API 上挂 Wave 4
- 为「保险」保留第二套穿敌旁路

---

## 6. 分阶段任务（可勾选）

### P0 — 契约与 Modifier 管线骨架

- [x] 补齐 `MotionModifierNotifyState`（`horizontalOffsetMm` / `lateralOffsetMm` / 窗口帧）及枚举
- [x] Timeline 增加 **Modifier 区间轨**；Command 可预留
- [x] `IActionMotionWorldQuery` + 起手固化 `ActionTargetId`
- [x] 帧管线：`BaseDelta → ApplyActiveModifiers → MotorSim`
- [x] Action Editor：可拖拽 Adhesion **窗口**起止帧，编辑偏移毫米

**出口：** EditMode 一帧修正 Δ ≠ 纯 Baked；窗外无修正。✅（纯函数用例）

### P1 — SoftBody 抑制窗

- [x] `SoftBodySuppress` 区间窗 + Actor 门闩
- [x] Play / Editor：叠人不弹、撞墙仍挡（2026-08-09 验收）

**出口：** ✅ 抑制可用。

### P2 — TargetAdhesion（连线动态 + 窗口均摊）← **主交付**

- [x] 每帧：`desired = enemy + normalize(enemy−player) * horizontalOffsetMm`
- [x] `correction = 朝向前方缺口 / remainingFrames`（过冲不倒拖），再夹 `maxCorrectionMmPerFrame`
- [x] 偏移 `>0 / =0 / <0` 三种 EditMode 用例（穿后 / 敌心 / 敌前）
- [x] 敌人位移时 desired 跟随；超距不吸；同输入 Hash 一致
- [x] Play / Editor：Branch_02 吸附手感验收（2026-08-09）

**出口：** ✅ **不依赖 Relocate** 即可按敌位加长；打击感位移主路径收口。

### P3 — RelocateBehindTarget（可选补钉）

- [ ] 补齐 Command 类型与 `ActionMotionResolver` 接线（草稿已有思路）
- [ ] 仅当 P2 终点仍飘 / 需要瞬切朝向时启用
- [ ] EditMode：落点、FaceTarget、Fallback、挡墙

**出口：** 可选；Branch_02 首版验收**不强制**配 Relocate（当前未做）。

### P4 — Branch_02 资产装配（人工 Editor，Agent 不改 `.asset`）

- [x] Editor 配窗 + 手感验收（2026-08-09）

见 §7。

### P5 — Lock-On 相机（并行，不阻塞）

总案 4.5～4.6；位移切片验收不依赖 Lock-On。

---

## 7. Branch_02 Editor 装配清单（人工）

样板：`Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Attack_Branch_02.asset`  
Perfect：`Unagi_Attack_Branch_02_Perfect.asset`（同结构参数可略放大）

### 7.1 建议时间轴（帧号按手感微调；以 60Hz 为准）

| 轨 | 建议 | 配置要点 |
|----|------|----------|
| SoftBodySuppress | ≥ Adhesion 窗 | **必配** |
| TargetAdhesion | **冲刺过程整段窗口** | 窗长 = 想要的吸附时长；`horizontalOffsetMm > 0` |
| Relocate | 可选 | 窗末仍差一截再加 |
| Rotation / Hitbox | 保持现有 | 按手感微调 |

**TargetAdhesion 初值建议：**

| 字段 | 初值建议 | 说明 |
|------|----------|------|
| `startFrame`～`endFrame` | 对齐冲刺段（例：十余～数十帧） | **拉长窗口 = 吸得更顺** |
| `horizontalOffsetMm` | +900～+1200 | 穿到敌后；贴脸改负值 |
| `lateralOffsetMm` | 0 | 侧向 |
| `maxCorrectionMmPerFrame` | 150～400 | 安全阀；正常应由窗长主导 |
| `maxAcquireDistanceMm` | 3500～5000 | 太远不吸 |
| `stopOnTargetLost` | true | |

### 7.2 调参顺序

1. SoftBodySuppress 能叠人。  
2. 设 `horizontalOffsetMm > 0`，先调 **窗口长度** 匹配动画冲刺时长。  
3. 再微调偏移毫米（落点离敌人多远）。  
4. 仅当窗末误差仍大或单帧触顶，再加大 `maxCorrection` 或略缩窗。  
5. 仍不够再考虑可选 Relocate。  

### 7.3 验收清单（Play / Editor）

> 2026-08-09：用户已在 Editor 完成打击感位移验收，下列项按验收结论勾选。

- [x] 吸附只在窗口内生效；窗外不再追  
- [x] 同样距离，窗口加倍 → 过程明显更慢/更顺  
- [x] `horizontalOffsetMm > 0` → 穿过敌人到连线另一侧  
- [x] 敌人更远（acquire 内）→ 行程自适应加长  
- [x] 敌人平移 → 吸附点跟随连线变化  
- [x] 可重叠、墙仍挡；无目标仅 Baked  
- [x] 不配 Relocate 也能验收穿身落点  
- [x] 普攻无 Modifier 无回归

---

## 8. 测试门禁

| 用例 | 类型 | 阶段 | 状态 |
|------|------|------|------|
| SoftBodySuppress 期间不参与分离 | EditMode | P1 | ✅ |
| offset>0 的 desired 在敌人「连线远侧」 | EditMode | P2 | ✅ |
| offset=0 / <0 分别为敌心 / 敌前 | EditMode | P2 | ✅ |
| 敌人移动后 desired 重算 | EditMode | P2 | ✅ |
| 剩余帧均摊：窗末附近误差收敛趋势正确 | EditMode | P2 | ✅ |
| 窗外 correction=0 | EditMode | P2 | ✅ |
| 超距不吸；触顶不超过 maxCorrection | EditMode | P2 | ✅ |
| 同输入 Hash 一致 | EditMode | P2 | ✅ |
| Relocate（可选） | EditMode | P3 | ⬜ 未做 |
| Branch_02 Play / Editor：吸附手感 | Play | P4 | ✅ 2026-08-09 |

---

## 9. 与总案任务映射

| 总案 Wave 4 | 本文件 |
|-------------|--------|
| 4.1 ActionMotionResolver；Sim 只出意图 | P0（Modifier 管线）；Command Resolver 可随 P3 |
| 4.2 TargetAdhesion + 参数校验 | **P2 主交付**（连线动态点 + 窗口均摊） |
| 4.3 RelocateBehind + Facing/Collision/Fallback | P3 **可选** |
| 4.4 起手固化 ActionTargetId | P0 |
| 4.5～4.6 Lock-On / Predict | P5 并行，不阻塞 |

完成后回写：

1. 本文对应 checkbox  
2. [MASTER_IMPLEMENTATION_PLAN.md](../2026.8.6/MASTER_IMPLEMENTATION_PLAN.md) Wave 4 落地状态  
3. [ACTION_DEFINITION_OPTIMIZATION_PLAN.md](../2026.8.6/ACTION_DEFINITION_OPTIMIZATION_PLAN.md) Phase A5 checkbox  

---

## 10. 风险与裁定

| 风险 | 裁定 |
|------|------|
| 只加长 Bake、不做吸附 | 无法按敌位自适应 → **P2 主交付** |
| 按敌人 forward 算「背后」 | 敌转身点位乱 → **改为玩家↔敌人连线** |
| 只用 maxCorrection 磨、不认窗口长度 | 与「窗长=吸附时长」不符 → **必须剩余帧均摊** |
| 有吸附、不抑制软体 | 穿身被弹开 → **P1 不可省** |
| 单帧 correction=error | 变瞬移 → 禁止；安全阀夹逼 |
| Unity CharacterController 开关当权威 | **禁止**；权威在 MotorSim + SoftBody 门闩 |
| Agent 改 Branch_02 数值资产 | **禁止**；只改脚本 + 本文 Editor 清单 |

---

## 11. 建议开工顺序

```text
P0 Modifier 区间轨 + 偏移字段 + ActionTargetId
  → P1 SoftBodySuppress
  → P2 TargetAdhesion（连线动态 + 剩余帧均摊）← 主交付
  → P4 配 Branch_02（窗长=冲刺时长，offset>0）
  → （可选）P3 Relocate
  → （并行）P5 Lock-On
```

**最小可玩切片：** P0+P1+P2 → 不配 Relocate 即可演示「窗内按连线吸穿到敌后侧」。

---

## 12. 成功标准（本切片）

- [x] 吸附点每帧按玩家↔敌人连线 + `horizontalOffsetMm` 动态计算  
- [x] Adhesion 为区间窗；窗外不吸；主节奏由剩余帧均摊决定（方案 A：过冲不倒拖）  
- [x] SoftBody 可抑制；静物墙有效  
- [x] Branch_02 Editor / Play：吸附手感验收（2026-08-09）  
- [x] 总案 4.2 / 4.4 可打勾；4.3 Relocate 不阻塞（仍可选未做）  

**切片结论：** Wave 4 **位移主路径（吸附 + SoftBody）已收口**；后续仅可选 Relocate 与 Lock-On。

---

## 13. 相关文件（预期）

```text
Assets/Scripts/Domain/Character/Motion/ActionMotionResolver.cs          // 已有草稿
Assets/Scripts/Domain/Character/Motion/ActionMotionRelocation.cs        // 新建
Assets/Scripts/Domain/Character/Motion/ActionMotionFacing.cs            // 新建
Assets/Scripts/Domain/Combat/Actions/Definitions/Timeline/
  MotionModifierNotifyState.cs / MotionCommandNotify.cs / 枚举
Assets/Scripts/Domain/Character/Presentation/CharacterActionPresentationBridge.cs
Assets/Scripts/Domain/Character/CharacterActor.cs                       // SoftBody 门闩
Assets/Scripts/Editor/Combat/ActionEditor/...                          // 轨与 Inspector
Assets/Data/Combat/Actions/Unagi/.../Unagi_Attack_Branch_02*.asset     // 人工装配
docs/2026.8.6/MASTER_IMPLEMENTATION_PLAN.md
docs/2026.8.6/ACTION_DEFINITION_OPTIMIZATION_PLAN.md
```
