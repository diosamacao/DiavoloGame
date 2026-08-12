# PivotTurn：两段式朝向 — 动画根运动 → 输入接管（FollowInput）

> 制定：2026-08-12  
> 角色：**PivotTurn 朝向/位移权威切换的结构真源**  
> 相关：  
> - 定向 AnimSet / FollowInput 位移：[`../2026.8.10/LOCOMOTION_DIRECTIONAL_ANIMSET_PLAN.md`](../2026.8.10/LOCOMOTION_DIRECTIONAL_ANIMSET_PLAN.md)  
> - 既有相位：[`../LOCOMOTION_OPTIMIZATION_PLAN.md`](../LOCOMOTION_OPTIMIZATION_PLAN.md)  
> - 格式 skill：`.cursor/skills/actgame-design-plan`  
> 装配链：`Gait(Sprint) → PivotTurn → [AnimAuth | InputAuth] → Sprint/Stop`  
> 入口：`PivotTurnLocomotionState` + `LocomotionContext.ApplyBakedRootMotion` + `LocomotionRootMotionPlayer`

---

## 0. 一句话

PivotTurn **前半段**由烘焙根位移 + **烘焙根偏航**驱动权威朝向与位移；**后半段**切换为与稳态 Gait 相同的 **FollowInput**（朝向追 wish、位移沿朝向、共用 `RotationSmoothTime`）；禁止长期保留「锁根 + PivotTarget 偏移叠加」与两段式双轨；禁止第二段再发明第三种转向语义。

---

## 1. 问题与动机

### 现状基线（改前）

- `PivotTurnLocomotionState`：约 `0.08s` 锁 `PivotEnterFacing`，其后 `PivotTarget` + `ResolvePivotSteeringRootDirection` 偏移转向。
- 默认 `pivotApplyRootYaw = false`：权威根几乎不跟烘焙偏航转。
- 解锁过早 → 动画权威窗口几乎没有。

### 目标

| 做 | 不做 |
|----|------|
| AnimAuth：bake pos + yaw | 第二段专用第三种转向 |
| InputAuth = Gait FollowInput | 长期 Compat / 双轨 |
| handoff = `pivotAnimAuthNormalized` | 继续用 `pivotInputUnlockSeconds` 业务语义 |

---

## 2. 设计原则

1. 新逻辑唯一真源；删除偏移转向与 `PivotTarget` 旋转模式。
2. InputAuth 复用 `CharacterMotor.ApplyLocomotion` + `ResolveGaitRotationMode()`。
3. 交接禁止硬切 wish；靠 `RotationSmoothTime`（可 `ResetRotationDamping` 清残留角速度）。
4. 位移 AnimAuth 仍由 `RootMotionPlayer` 逻辑帧消费；handoff 时刻用 `Animation.NormalizedTime`。

---

## 3. 目标架构

```
Enter → Begin(bake) @ PivotEnterFacing
  │
  ├─ NormalizedTime < pivotAnimAuthNormalized  → AnimAuth
  │     ApplyBakedRootMotion(applyYaw=true, no input move)
  │
  └─ else → InputAuth（一次性 End bake）
        ApplyLocomotion(FollowInput/FaceCamera, Sprint, move=true)
  │
  └─ clip end / 松手 → GoGait(Sprint) / GoStop
```

---

## 4. 分阶段交付

### P-PIV1 — 两段式主逻辑

**任务**

- [x] `PivotTurnLocomotionState`：AnimAuth → InputAuth；handoff = `PivotAnimAuthNormalized`
- [x] AnimAuth：`ApplyBakedRootMotion` 强制吃 bake yaw + pos；无输入推移
- [x] InputAuth：`RootMotionPlayer.End()` + `ApplyLocomotion(ResolveGaitRotationMode, Sprint)`
- [x] Profile 新增 `pivotAnimAuthNormalized`（默认 0.5）
- [x] 删除 `ResolvePivotSteeringRootDirection` / `ReorientPivotDeltaToCurrentFacing` 业务路径

**验收**

- [x] `rg ResolvePivotSteeringRootDirection` 无业务引用
- [x] Play：Sprint 大角度转身前半跟 bake，后半黄箭（wish）可拉转向与日常跑一致
- [ ] Unity Editor 编译与手测（待本地确认）

**出口：** 两段式为唯一运行路径。→ **已达成（2026-08-12）**（手测待确认）

### P-PIV2 — 清旧字段与模式

**任务**

- [x] 删除 `pivotInputUnlockSeconds` / `pivotRotationSmoothTime` / `pivotApplyRootYaw`
- [x] 删除 `LocomotionRotationMode.PivotTarget` 与 Motor 分支
- [x] 删除 `PivotElapsedSeconds` / `PivotInitialTargetDirection`
- [x] `LocomotionMotorCommand` 去掉 `PivotTargetDirection` 参数

**验收**

- [x] 上述符号无残留（除文档说明）
- [x] `FaceCamera = 3` 保持，避免资产枚举错位

**出口：** 无旧解锁/偏移语义。→ **已达成（2026-08-12）**

---

## 5. 迁移与删除

| 删除 | 替代 |
|------|------|
| `pivotInputUnlockSeconds` | `pivotAnimAuthNormalized` |
| `pivotRotationSmoothTime` | `CharacterMotorConfig.RotationSmoothTime` |
| `pivotApplyRootYaw` | AnimAuth 恒 true |
| `PivotTarget` / 偏移转向 | InputAuth FollowInput |

资产 YAML 中旧字段可残留，Unity 忽略未知序列化；Inspector 以新字段为准。

---

## 6. 风险与对策

| 风险 | 对策 |
|------|------|
| bake 轨缺 yaw | AnimAuth 位移仍有、朝向不动 → 检查烘焙；可调 handoff |
| handoff 与 clip 观感不齐 | 调 `pivotAnimAuthNormalized` |
| 无 bake 轨 | Enter 时无 `IsActive` 则整段 InputAuth |

---

## 7. Editor 人工步骤

1. 选中玩家 `CharacterLocomotionProfile`：确认 **Pivot Anim Auth Normalized ≈ 0.5**（新字段默认 0.5）。
2. 确认 PivotTurn 烘焙轨含合理偏航；必要时重烤。
3. Play：Sprint 急转身 → 前半跟动画，后半可用摇杆微调朝向；松手仍进 Stop。

---

## 8. 开工顺序

P-PIV1 状态机 + ApplyBakedRootMotion → P-PIV2 删旧符号 → 文档勾选。

---

## 变更日志

| 日期 | 说明 |
|------|------|
| 2026-08-12 | 定案两段式；文档落地 |
| 2026-08-12 | **P-PIV1 / P-PIV2 代码落地**：AnimAuth→InputAuth；删除 PivotTarget 偏移双轨 |
| 2026-08-12 | Pivot→Sprint **不再** `faceDirection=wish` 硬切；接轨靠 InputAuth 当前朝向 |
