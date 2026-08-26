# 相机移动参考与战斗目标权威收口 — 优化方案

> 制定：2026-08-13  
> 角色：**Camera C1 前置的移动输入、唯一自动选敌与纯表现镜头锁定结构真源**  
> 实施状态：**C-AT0～C-AT3 代码已完成（2026-08-13）；Input Actions、Unity Test Runner 与 Play 回归待 Editor 人工确认**  
> 相关：  
> - [相机系统方案（现行排期）](../2026.8.26/CAMERA_SYSTEM_PLAN.md)  
> - [相机系统方案（8.6 历史细节）](../2026.8.6/CAMERA_SYSTEM_PLAN.md)  
> - [锁步模拟重构](../ACTION_SYSTEM_LOCKSTEP_REFACTOR_PLAN.md)  
> - [架构 ROADMAP](../../.cursor/skills/actgame-architecture/ROADMAP.md)  
> - 装配链：`本地 Orbit Yaw → InputFrame → MoveIntent`；`TargetingState → ActionSim / Locomotion / CameraDirector`

---

## 0. 一句话

把相机相对移动所需的 **MoveReferenceYaw** 固化进每个 `InputFrame`，用 **CharacterTargetingState.SelectedTargetId** 作为自动索敌、玩家切敌、攻击旋转、吸附/绕背与相机构图共用的唯一目标；动作期间也可切换且后续攻击逻辑立即改读新目标。相机锁定键只切换本地 `CameraLockEnabled`，不选择目标、不新增目标 Id，并删除 `CameraManager → Motor` PlanarBasis 权威旁路、招式临时 `CombatTargetLock`、`ActionSim.ActionTargetId` 与 `CharacterActionPresentationBridge → ActionSim` 回写，禁止长期双轨和 Transform 索敌回退。

---

## 1. 问题与动机

### 1.1 现状基线

#### A. Orbit Yaw 通过渲染态进入逻辑移动

```text
CameraManager.Update / LateUpdate
  → yaw（Look + L-DIR5 SmoothDamp）
  → PushPlanarBasisToPlayer
  → PlayerController
  → CharacterActor
  → CharacterMotor.SetCameraPlanarBasis
  → ResolveWorldMoveDirection(moveIntent)
  → MotorSim
```

| 点 | 现状 |
|----|------|
| 输入帧 | `InputFrame` 有未消费的 `AimYawQuantized`，`InputReader.Sample` 未写该字段 |
| 世界方向 | `CharacterMotor` 优先读取 CameraManager 在 `LateUpdate` 推入的浮点 PlanarBasis |
| 追帧 | 同一渲染帧内多个逻辑 Step 共用上一次 LateUpdate 的 Basis |
| 回放 / 网络 | 只有 `MoveX/MoveY` 无法重建当帧世界 wish；服务端没有 CameraManager |
| L-DIR5 | `Sim facing → Render → Camera SmoothDamp(Time.deltaTime) → 下一帧 Sim move` 形成渲染帧率相关反馈环 |

#### B. `CombatTargetLock` 把“选中目标”错误绑定在单次 Action 生命周期

```text
ActionGraphNode.TargetLockSettings
  → CombatTargetLock.AcquireForActionNode
  → ITargetable / AimTransform / float 距离与角度
  → ActionRotationDriver / LocomotionFacingTargetSource

目标产品语义
  → 范围内自动维护唯一 SelectedTargetId
  → 攻击统一使用 SelectedTargetId
  → CameraLock 按键只切相机模式
  → CameraDirector 只读同一个 SelectedTargetId 做构图
```

| 语义 | 生命周期 | 是否权威 | 终态 |
|------|----------|----------|------|
| Selected Target | 角色在范围内自动选择；玩家可随时切换 | 是；攻击/旋转/吸附共用 | `CharacterTargetingState.SelectedTargetId` |
| Camera Lock | 玩家按键切换；始终跟随当前 SelectedTarget | 否；只控制相机构图 | `CameraDirector.CameraLockEnabled` |

锁定按键**不参与选敌**、不改变 Character/Locomotion/Action 状态，也不保存第二个目标。范围内无 `SelectedTargetId` 时锁定请求失败。`TargetSwitchLeft/Right` 是独立 gameplay 输入，动作期间也可更新 `SelectedTargetId`；相机处于 LockOn 时随当前 SelectedTarget 平滑切换构图。

#### C. Presentation 在动作 Started 后写回 ActionSim

```text
ActionSim.Begin
  → ActionSimEvent.Started
  → CharacterActionPresentationBridge.HandleStarted
  → CombatTargetLock.AcquireForActionNode（读 Transform）
  → ActionSim.BindActionTarget(targetId)
```

| 问题 | 后果 |
|------|------|
| 目标在 Started 事件之后才绑定 | 同一 Started 的其他消费者可能读到 Invalid |
| 选择发生在 PresentationBridge | 无头服务端、回放与回滚重演无法保证执行 |
| 候选读 `AimTransform.position` | 表现插值、帧率与对象层级可改变选择结果 |
| `ActionSim` 重复保存 `ActionTargetId` | 与角色自动选中的唯一目标形成第二份目标状态 |
| `BindActionTarget` 是公开后写 API | 任意调用者可在 Begin 后改变动作目标 |

### 1.2 痛点

1. 相同 `InputFrame.MoveX/MoveY` 在不同帧率、回放端或服务器上可能产生不同世界位移。  
2. Camera C1 若直接复用 `CombatTargetLock`，会把纯相机开关错误升级成第二套 gameplay 目标状态。
3. Wave 4 的吸附与绕背读取 `ActionTargetId`，但角色本应已有唯一 `SelectedTargetId`，当前双存储不满足单一真源。
4. 目标选择没有稳定候选顺序与 `SimActorId` tie-break，候选集合顺序变化即可改变结果。  
5. 若先实现 TargetGroup / CameraDirector，再修权威边界，会同时返工 Input、Locomotion、Action、Camera 与 UI。

### 1.3 目标

| 目标 | 说明 |
|------|------|
| 移动可重演 | 单个 `InputFrame` 足以重建玩家世界移动方向，不需要 CameraManager 或 Main Camera |
| 目标单一真源 | 全角色只保存一个 `SelectedTargetId`；自动索敌、攻击与相机共用 |
| 确定性索敌 | 只读稳定逻辑候选快照；距离量化；最终用 `SimActorId` 打破平局 |
| 动态切敌 | TargetSwitch 输入在 Locomotion/Action 中均有效；动作消费者逐逻辑帧读取最新 SelectedTarget |
| 回滚就绪 | `SelectedTargetId` 与 TargetSwitch 输入进入未来 Simulation Snapshot / FramePacket；表现从 Snapshot 导出 |
| Camera C1 就绪 | CameraDirector 只读 `SelectedTargetId`；锁定键只切本地 CameraLockEnabled |
| 不做 | 本方案不实现 VCam/TargetGroup/锁定 UI/SkillShot；不实现完整 L3/L5 网络宿主；不以 Unity Physics 遮挡作为首版权威解锁条件 |

---

## 2. 设计原则

1. **InputFrame 闭包**：凡影响玩家 Motor 位移/朝向的本地输入量，必须可从当帧 `InputFrame` 重建。  
2. **原始 Look 与移动参考分离**：鼠标/摇杆 Look delta 仍为本地表现；由它产生且被移动消费的水平参考 yaw 必须量化进输入帧。  
3. **目标 Id 权威、Transform 表现**：Sim 只认 `SimActorId + 整数量化 Pose`；相机/UI 可由 Id 映射到 `LockPoint/AimTransform`。  
4. **目标只存一份**：`SelectedTargetId` 属于角色 Targeting；ActionSim、Camera 与 Presentation 都不得复制持有第二个目标 Id。  
5. **动作期间允许换敌**：TargetSwitch 与目标失效后的自动补选在 Action 中照常生效；Rotation/Motion 每帧读取最新 SelectedTargetId。  
6. **候选快照稳定**：索敌只读当前逻辑帧已提交的候选快照；遍历前按 `SimActorId` 稳定排序。  
7. **首版不做权威遮挡**：场景 Physics Raycast 仅可控制 UI 可见性/构图，不得令 SelectedTarget 在各端产生不同失效结果。  
8. **表现可丢、逻辑可重演**：CameraLockEnabled、TargetGroup、Blend、FOV、Impulse 不进 Sim Snapshot；SelectedTargetId 与动作帧必须进。  
9. **零长期兼容**：迁移完成即删除 PlanarBasis Motor 路径、Transform TargetSelector、`CombatTargetLock`、`ActionTargetId` 与晚绑定 API。  
10. **资产只列人工步骤**：Agent 不直接修改 `.inputactions`、`Assets/Data/**`、Prefab 或 `.asset`。

---

## 3. 目标架构

### 3.1 总数据流

```text
【本地设备 / Render Sample】
Look Input → CameraManager.yaw
                 │
                 └─ StageMoveReferenceYaw（最新渲染样本）
                            ↓
InputReader.Sample(targetFrame)
  → InputFrame {
       MoveX,
       MoveY,
       MoveReferenceYawQuantized,
       Buttons...（含 TargetSwitchLeft/Right Pressed）
     }
                            ↓
【SimulationWorld 60Hz】
InputManager.IngestFrame
  → IMoveIntentSource.MoveReferenceYaw
  → LocomotionContext.BuildSnapshot
  → ResolveWorldMoveDirection(localMove, moveReferenceYaw)
  → CharacterMotorSim

CharacterTargetingState.Step
  → 读取 TargetSwitchLeft / TargetSwitchRight
  → DeterministicTargetResolver(SimTargetCandidateSnapshot[])
  → SelectedTargetId（唯一）
       ├─ ActionRotation / Adhesion / MotionCommand
       ├─ CharacterTargetingSnapshot
       └─ CameraDirector 只读

Action 进行中
  → TargetSwitch 仍可改变 SelectedTargetId
  → ActionRotation / Adhesion / MotionCommand 每帧读取最新目标
  → 不在 ActionSim 复制目标 Id

【Presentation】
CameraDirector
  → CameraLock 按键：只切本地 CameraLockEnabled
  → 只读 CharacterTargetingSnapshot.SelectedTargetId
  → 无有效 SelectedTargetId：不能进入 LockOn
  → 目标切换：保持 LockOn 并平滑跟随；无任何目标：自动回 Free
  → SimActorId → Presentation LockPoint → LockOn VCam

CharacterActionPresentationBridge
  → 只读 CharacterTargetingSnapshot.SelectedTargetId
  → 动画 / Timeline / 位移表现
```

### 3.2 MoveReferenceYaw 契约

#### 字段定案

`InputFrame.AimYawQuantized` 当前无业务消费者，直接重命名为：

```text
MoveReferenceYawQuantized : ushort
单位：0.1°
范围：[0, 3599]
```

不保留 `AimYawQuantized` 别名。未来若出现准星/瞄准方向需求，另建语义明确的字段，不复用移动参考。

#### 本地采样时序

```text
CameraManager.LateUpdate
  → 完成 Look / L-DIR5 yaw
  → PlayerController.StageMoveReferenceYaw(yaw)

下一次 SimulationWorld.SampleRenderFrame
  → InputReader.Sample
  → 取最近 staged yaw 写入目标 InputFrame
```

接受“最近渲染样本供下一逻辑输入帧使用”的一帧 staging 语义；该语义与现有 PlanarBasis 下一逻辑帧消费一致，但结果被固化进 InputFrame，追帧、回放与网络端不再重跑相机。

#### 消费契约

```text
IMoveIntentSource
  MoveIntent
  MoveMagnitude
  HasMoveIntent
  BufferedMoveIntent
  MoveReferenceYawQuantized

Player InputManager
  → 从 InputFrame 返回 MoveReferenceYawQuantized

AI LocomotionDesireBuffer
  → Desire 显式携带确定性 referenceYaw（由目标/路径逻辑生成）
```

`CharacterMotor.ResolveWorldMoveDirection` 改为纯参数计算：

```text
worldMove = RotateYaw(Dequantize(moveReferenceYaw), localMove)
```

Motor 不再持有 `_cameraPlanarForward/_cameraPlanarRight/_hasCameraPlanarBasis`，也不再用 `Camera.main.forward` 作为 gameplay 回退。无输入参考时使用显式 `0°` 世界前向，禁止静默读取场景相机。

### 3.3 唯一 SelectedTarget 与纯表现 Camera Lock

| 状态 | 所有者 | 写入时机 | 消费者 | Snapshot |
|------|--------|----------|----------|----------|
| `SelectedTargetId` | `CharacterTargetingState` | 每个逻辑 Step 自动 Acquire/Validate 或响应 TargetSwitch | ActionRotation、Adhesion、MotionCommand、CameraDirector、UI | 必须 |
| `CameraLockEnabled` | `CameraDirector` | 本地锁定按键 / 范围内无目标 | Cinemachine | 不进 |
| UI LockPoint | Presentation 映射 | LateUpdate：Id→Transform | 锁定 UI / TargetGroup | 不进 |

#### SelectedTarget 自动维护

```text
CharacterTargetingState.Step(candidates, characterState)
  当前 SelectedTarget 有效且仍在 retainRange:
    无 TargetSwitch → 保持，禁止因“出现更近敌人”每帧抖动换敌
    TargetSwitchLeft/Right → 按输入方向切到范围内另一目标

  当前目标无效:
    → 在 acquireRange 内自动选择最近敌人
    → 距离相同取较小 SimActorId

  Action 中规则完全相同:
    → 玩家可切换目标
    → 目标失效可立即自动补选
    → 攻击、旋转、吸附、绕背从下一逻辑帧使用新 SelectedTargetId
```

`acquireRange` / `retainRange` 由单一 `CharacterTargetingProfile`（或 CharacterConfig Combat 子配置）提供，且 `retainRange >= acquireRange`，形成滞回避免边界抖动。自动选择固定为**范围内最近敌人**；显式切换按 `MoveReferenceYaw` 参考的左右方位选择。两者都不读取 Transform 可见性或 Camera Lock 状态。

#### Camera Lock

```text
本地 CameraLock 按键:
  CameraLockEnabled == false:
    SelectedTargetId 有效 → true
    SelectedTargetId 无效 → 保持 false（锁定失败）

  CameraLockEnabled == true:
    → false（主动解锁）

每帧 Validate:
  CameraLockEnabled && SelectedTargetId 有效
    → 持续 LockOn；SelectedTarget 改变时平滑重组 TargetGroup
  CameraLockEnabled && 范围内无任何 SelectedTarget
    → false，CameraDirector 回 Free
```

Camera Lock 不进入 `InputButton`、`GameplayIntentType`、`InputFrame` 或 Simulation Snapshot。它不改变 `SelectedTargetId`、角色朝向、Locomotion FacingMode 或 Action 行为；其唯一效果是 CameraDirector 用当前 `SelectedTargetId` 的 Presentation LockPoint 构图。

`TargetSwitchLeft/Right` 与 Camera Lock 键不同：TargetSwitch 是 gameplay 输入，必须进入 `InputFrame`；Camera Lock 只是本地表现开关。镜头锁定期间发生显式切敌或目标失效后的自动补选时，Camera Lock 保持开启并以短 Blend 跟随新的 SelectedTarget；只有范围内已无任何有效目标时才自动回 Free。

### 3.4 确定性 TargetResolver

#### 输入

```text
SimTargetResolveRequest
  requesterId
  requesterTeamId
  originPositionMm
  moveReferenceYawQuantized
  acquireRangeMm
  retainRangeMm
  currentSelectedTargetId
  switchDirection（None / Left / Right）

SimTargetCandidate
  actorId
  teamId
  positionMm
  isAlive
```

#### 规则

1. 候选按 `SimActorId` 升序稳定遍历。  
2. 自身、同阵营、死亡、超距先过滤。  
3. 距离使用毫米整数平方；禁止 `Transform.position`。  
4. 当前 SelectedTarget 在 `retainRange` 内且无 Switch 输入时保持，不因候选评分变化自动换敌。  
5. 需要 Acquire 时按距离平方升序，平局取较小 `SimActorId`。  
6. SwitchLeft/Right 只在 `acquireRange` 内候选中，按 MoveReferenceYaw 的有符号方位选同侧最近角目标；同侧无候选时环绕到另一侧。  
7. 切换结果按有符号角绝对值 → 距离平方 → `SimActorId` 稳定排序。  
8. Switch 使用 Pressed 边沿：左右同帧同时按下视为 `None`；无其他候选时保持当前目标。  
9. 当前目标无效的帧优先按最近规则 Acquire，不对同帧 Switch 再做第二次跳转。  
10. Resolver 在 Locomotion/Action 中使用同一规则，禁止因 CharacterState 分叉。  
11. Resolver 为无 Unity Transform 依赖的纯函数，候选顺序打乱不得改变结果。

### 3.5 Action / Presentation 消费契约

```text
CharacterActor.Step
  → CharacterTargetingState.Step（先于动作解析）
  → CharacterActionDriver / ActionSim
  → ActionRotation / Motion 每帧只读最新 SelectedTargetId

CharacterActionPresentationBridge
  → 只读 CharacterTargetingSnapshot.SelectedTargetId
  → 禁止 Acquire / Select / Bind / 写 ActionSim

Action 期间 SelectedTarget 改变
  → 下一逻辑帧的朝向、位移修正和目标查询使用新目标
  → 不复制、不缓存 ActionTargetId
```

`ActionSim` 不再持有 `_actionTargetId`，也不提供 `BindActionTarget`。动作是否消费目标由其 Rotation / Motion 配置决定，但消费时统一逐帧读取角色的 `SelectedTargetId`；ActionGraph 节点不再拥有独立选敌范围、Policy 或第二套目标生命周期。已经生成的命中事件不可追溯改写，切敌只影响生效帧及之后尚未解析的攻击逻辑。

### 3.6 回滚与 FramePacket 边界

| 数据 | InputFrame | Simulation Snapshot | FramePacket | Presentation |
|------|------------|---------------------|-------------|--------------|
| MoveReferenceYaw | ✅ | 由输入历史恢复 | ✅ | Camera 可本地产生 |
| TargetSwitchLeft/Right | ✅（Pressed） | 由输入历史恢复 | ✅ | 不消费 |
| SelectedTargetId | — | ✅ | 可由状态重演；关键帧 Snapshot 必含 | Action/Camera/UI 只读 |
| CameraLockEnabled | ❌ | ❌ | ❌ | ✅ 本地按键 |
| Camera yaw/pitch | ❌ | ❌ | ❌ | ✅ |
| VCam/Blend/FOV/Impulse | ❌ | ❌ | ❌ | ✅ |

本方案只把字段和所有权准备到可无损恢复；完整 `SimulationSnapshot.Restore`、预测窗口与 FramePacket 仍归 L3/L5。

### 3.7 层边界

| 层 | 负责 | 不负责 |
|----|------|--------|
| Infrastructure Input | 采样 Move/Button + staged MoveReferenceYaw，生成 InputFrame | 运行 Motor、选择目标 |
| Simulation Input | 固定布局、量化、Carry/Merge/Hash | Camera SmoothDamp |
| Character Targeting | 自动维护唯一 SelectedTarget、响应确定性切敌输入 | Camera Lock、VCam、UI |
| Deterministic TargetResolver | 纯候选过滤、评分、稳定 Id | Transform/Physics 遮挡 |
| ActionSim | 动作实例、帧、Graph 与命中状态 | 保存/选择第二个目标 |
| Action Rotation/Motion | 只读 SelectedTargetId + 逻辑 Pose | 重新索敌、写 TargetingState |
| Locomotion/Motor | 消费 MoveIntent + MoveReferenceYaw | 因 CameraLock 改 gameplay 朝向 |
| CameraDirector | 本地 CameraLockEnabled + 只读 SelectedTargetId 做构图 | 选择目标、写 Character/ActionSim |
| PresentationBridge | 消费 Snapshot/Event/SelectedTarget | 修改 ActionSim、重新索敌 |

---

## 4. 范围声明

| 阶段 | 包含 | 不包含 |
|------|------|--------|
| C-AT0 | MoveReferenceYaw 入 InputFrame；删除 PlanarBasis Motor 路径 | Camera C1 VCam |
| C-AT1 | 自动 SelectedTarget + 确定性 Resolver + 全状态切敌 | Camera Lock、锁定 UI、遮挡解锁 |
| C-AT2 | Action/Motion 改读 SelectedTarget；删除 Presentation 回写与 ActionTargetId | SkillShot |
| C-AT3 | 本地 CameraLock 契约、Snapshot、文档与回归 | 完整网络 L3/L5、LockOn VCam |

---

## 5. 分阶段交付（任务 / 验收 / 出口）

> 勾选：未开始 `[ ]`；完成后 `[x]` 并在出口注明日期。

### C-AT0 — MoveReferenceYaw 输入闭包

**任务**

- [x] `InputFrame.AimYawQuantized` 直接重命名为 `MoveReferenceYawQuantized`（0.1°、`[0,3599]`），更新构造、Merge、Carry、Equals、Hash  
- [x] 本地输入边界增加 staged yaw：`CameraManager` 只把最新 Orbit yaw 提交给 `PlayerController/InputReader` 的输入采样槽  
- [x] `InputReader.Sample` 将 staged yaw 固化进目标 `InputFrame`；回放/远端输入不依赖 CameraManager  
- [x] `IMoveIntentSource` 暴露确定性 MoveReferenceYaw；`InputManager` 读 InputFrame，AI Desire 显式提交参考 yaw  
- [x] `CharacterMotor.ResolveWorldMoveDirection` 改为 `localMove + quantizedYaw` 的纯参数计算  
- [x] **删除** `CameraManager.PushPlanarBasisToPlayer`、`PlayerController/CharacterActor.SetCameraPlanarBasis` 与 Motor PlanarBasis 缓存  
- [x] **删除** gameplay 路径的 `Camera.main.forward` / `cameraTransform.forward` 回退；Camera Transform 仅可保留给表现或调试  
- [x] EditMode：更新 `InputFrameTests` / `InputFrameBufferTests`，覆盖 MoveReferenceYaw 量化、Merge 与 Carry

**验收**

- [ ] 同一组完整 InputFrame 在无 CameraManager 的测试 World 中得到相同 WorldMoveDirection / MotorSim 位置  
- [ ] 30/60/144 FPS 采样记录回放时，按已记录 InputFrame 重演的最终位置与朝向一致  
- [ ] 单渲染帧追赶多个逻辑 Step 时，CarryForward 明确延续同一 MoveReferenceYaw，不读取中途渲染状态  
- [x] `rg "SetCameraPlanarBasis|_cameraPlanarForward|_cameraPlanarRight" Assets/Scripts` 无业务引用  
- [x] `rg "AimYawQuantized" Assets` 无残留  
- [ ] Unity 编译 / EditMode 在 Editor 确认通过

**出口：** 单帧移动输入对世界方向闭包，Simulation 不再读取相机渲染态。→ **未达成**

### C-AT1 — 自动 SelectedTarget 与确定性 TargetResolver

**任务**

- [x] 新增 `CharacterTargetingState`：唯一持有 `SelectedTargetId`
- [x] 新增纯 `DeterministicTargetResolver`、`SimTargetResolveRequest`、`SimTargetCandidate`；候选来自逻辑 Actor/Pose 快照  
- [x] 新增单一 Targeting 配置：`CharacterCombatConfig.TargetAcquireRangeMeters / TargetRetainRangeMeters`；自动选择按最近距离 + SimActorId
- [x] `InputButton` 增加 `TargetSwitchLeft/Right`，以 Pressed 进入 InputFrame；`InputReader` 支持同名可选 InputAction
- [ ] 在 Input Actions 资产创建并绑定 `TargetSwitchLeft/Right`（Editor 人工步骤）
- [x] 有效当前目标默认保持；无效时自动 Acquire，禁止每帧抢更近目标造成抖动
- [x] Locomotion/Action 中均响应 TargetSwitch；按 MoveReferenceYaw 方位与稳定 tie-break 切换范围内目标
- [x] 目标死亡/超距时在同一套规则下清空并立即自动补选，不因 Action 状态禁止 Acquire
- [x] Action、Locomotion 与 Camera Lock 状态均不得改变选敌 Policy；Camera Lock 不写 TargetingState
- [x] 暴露只读 `CharacterTargetingSnapshot.SelectedTargetId`，供 Action/Camera/UI 读取，不暴露外部 SetTarget
- [x] 首版目标失效仅含死亡、阵营变化、确定性距离；遮挡只影响表现
- [x] EditMode：新增 `DeterministicTargetResolverTests`，覆盖最近、保持、稳定 tie-break、左右切换与失效补选

**验收**

- [ ] 候选列表任意洗牌 100 次，自动选择结果均为同一 SimActorId
- [ ] 等距离候选始终选最小 SimActorId
- [ ] 当前目标仍在 retainRange 时，即使出现更近敌人也不换目标
- [ ] 同一 InputFrame 序列在 Locomotion 与 Action 中产生完全一致的左右切敌结果
- [ ] 动作期间按 TargetSwitch 后，下一逻辑帧 SelectedTargetId 更新为指定方向的敌人
- [ ] 动作期间目标死亡/超距后，范围内仍有候选时立即确定性补选；无候选时清空
- [ ] 无 Camera/Transform/Physics 引用进入 Resolver  
- [ ] 仅有 SelectedTarget 不改变 Free Locomotion FacingMode；攻击按动作配置消费该目标
- [ ] Unity 编译 / EditMode 在 Editor 确认通过

**出口：** 范围内自动索敌拥有唯一、稳定、可重演的 SelectedTargetId。→ **未达成**

### C-AT2 — Action/Motion 统一消费 SelectedTarget

**任务**

- [x] `CharacterActor.Step` 在动作输入/ActionSim 前推进 `CharacterTargetingState`
- [x] ActionRotation、TargetAdhesion、MotionCommand 改为每逻辑帧只读最新 `CharacterTargetingSnapshot.SelectedTargetId` + 逻辑 Pose Query
- [x] 删除 Action 进入/退出时的 Freeze/Unfreeze 概念；Cancel、Recovery、AutomaticTransition 不阻断 TargetSwitch
- [x] ActionGraph 节点只保留“是否消费目标/旋转平滑”等动作策略，删除独立选敌范围与 Policy
- [x] **删除** `CharacterActionPresentationBridge.BindActionTargetAtStart` 与所有 Presentation→ActionSim 目标写入  
- [x] **删除** `ActionSim.BindActionTarget` 公开晚绑定 API  
- [x] **删除** `ActionSim.ActionTargetId`、内部 `_actionTargetId` 及相关 Snapshot/调试字段，不保留第二份目标
- [x] **删除** `CombatTargetLock`；自动选择职责归 C-AT1 的 CharacterTargetingState
- [x] 删除无引用的 Transform 版 `TargetSelector/TargetingResolver`，不保留 fallback  
- [ ] EditMode：新增 `SelectedTargetActionIntegrationTests`，覆盖起手、动作中切敌、切招与目标失效补选

**验收**

- [ ] 直接起手、硬打断、Cancel、Recovery、OnHit/OnWhiff、AnimationEnd 自动衔接期间 TargetSwitch 均可生效
- [ ] 切敌后的下一逻辑帧，Rotation/Adhesion/MotionCommand 使用新目标；无目标时安全 no-op
- [ ] 切敌前已确认的命中事件不追溯改写，尚未解析的攻击窗口使用新目标
- [ ] 无 PresentationBridge 时 CharacterActor + ActionSim 测试仍使用相同 SelectedTargetId
- [x] `rg "ActionTargetId|BindActionTarget|BindActionTargetAtStart|CombatTargetLock" Assets/Scripts` 无业务引用
- [x] `rg "AimTransform.position" Assets/Scripts/Domain/Combat/Targeting` 无权威索敌引用  
- [ ] Wave 4 TargetAdhesion / Relocate EditMode 回归通过  
- [ ] Unity 编译 / EditMode 在 Editor 确认通过

**出口：** Action/Motion 全部消费角色唯一 SelectedTarget，ActionSim 与表现层不再保存或补写第二目标。→ **未达成**

### C-AT3 — 纯表现 Camera Lock 与回滚契约收口

**任务**

- [x] 为 CameraManager/未来 CameraDirector 增加本地 `CameraLockEnabled` 与 CameraLock 按键读取；不写 `InputFrame`
- [x] 锁定键按下时仅在 `SelectedTargetId` 有效时进入 LockOn；无目标时保持 Free
- [x] Camera Lock 开启时 SelectedTarget 改变则保持开启；无任何目标才回 Free（VCam 短 Blend 归 Camera C1）
- [x] 定义只读 `ILocalCameraTargetSource`：只返回 SelectedTargetId 与 Id→Presentation 目标映射
- [x] CameraManager L-DIR5 仅在 `CameraLockEnabled` 时停止自动跟朝向；SelectedTarget 存在本身不改变 L-DIR5
- [x] 明确 Camera Lock 不改变 Locomotion FacingMode、角色朝向、Action 或 TargetingState
- [x] 未来 Simulation Snapshot 登记 SelectedTargetId，InputFrame 登记 TargetSwitch；CameraLockEnabled/mode/Blend 不登记
- [x] 更新 CAMERA_SYSTEM_PLAN、ACTION_SYSTEM_LOCKSTEP_REFACTOR_PLAN、ARCHITECTURE、TECHNICAL、CONVENTIONS 与 ROADMAP  
- [ ] Play 回归：自由移动 L-DIR5、FaceTarget strafing、动作自动索敌、吸附/绕背均无回归

**验收**

- [ ] 无 SelectedTarget 时按锁定键无效；有 SelectedTarget 时只切 Camera 模式
- [ ] Camera Lock 开启后 L-DIR5 停止自动跟朝向；解锁后恢复
- [ ] 锁定期间显式切敌或自动补选时镜头平滑跟随；范围内无目标时 Camera 自动解锁
- [ ] Camera Lock 前后 CharacterTargetingSnapshot、Locomotion FacingMode、ActionSim 状态完全一致
- [x] HUD 分栏显示 SelectedTarget 与 `CameraLockEnabled`，不再显示 Manual/Action 两套目标
- [x] CameraManager/CameraDirector 无 Target 选择、Motor 写入、ActionSim 写入 API  
- [ ] Unity Play：锁定、解锁、目标死亡、动作起手、Cancel/打断、Wave4 位移全部通过

**出口：** SelectedTarget 单一权威与纯表现 Camera Lock 完成收口，Camera C1 可在不新增逻辑旁路的前提下开工。→ **未达成**

---

## 6. 迁移与删除

### 6.1 保留 / 迁入

| 现有能力 | 终态 |
|----------|------|
| CameraManager Orbit yaw / L-DIR5 | 保留为本地相机状态；仅在输入采样边界固化 MoveReferenceYaw |
| `InputFrame.MoveX/MoveY` | 保留本地二维轴语义 |
| 现有 TargetLockSettings 范围配置 | 收敛到角色单一 Targeting 配置的 acquire/retain range |
| `SimActorId` | `SelectedTargetId` 的唯一身份 |
| `ActionMotionWorldQuery` 思路 | 扩展/替换为可枚举稳定逻辑候选的只读查询 |
| Presentation LockPoint/AimTransform | 仅供 Camera/UI/VFX 映射，不参与选择 |

### 6.2 明确删除

| 删除 | 替代 |
|------|------|
| `InputFrame.AimYawQuantized` 旧名 | `MoveReferenceYawQuantized` |
| `CameraManager.PushPlanarBasisToPlayer` | staged yaw → InputFrame |
| `SetCameraPlanarBasis` Actor/Controller/Motor 链 | `IMoveIntentSource.MoveReferenceYawQuantized` |
| Motor `_cameraPlanarForward/_cameraPlanarRight` | 量化 yaw 纯计算 |
| Motor `Camera.main/cameraTransform.forward` gameplay 回退 | InputFrame / AI Desire 显式参考 yaw |
| `CombatTargetLock` | `CharacterTargetingState.SelectedTargetId` |
| Transform 版 `TargetSelector/TargetingResolver` 权威路径 | `DeterministicTargetResolver` |
| `CharacterActionPresentationBridge.BindActionTargetAtStart` | 只读 SelectedTarget，无绑定步骤 |
| `ActionSim.ActionTargetId` / `BindActionTarget` | 删除；Action/Motion 直接读 SelectedTarget |
| Camera 自己维护 current target | 只读 `CharacterTargetingSnapshot` |

迁移阶段不允许合并带旧 fallback 的稳定态；每个阶段出口必须同时删除被替代路径。

---

## 7. 目录与文件预期（增量）

```text
Assets/Scripts/Domain/Simulation/Input/
  InputFrame.cs                         // MoveReferenceYawQuantized
  InputQuantizer.cs                     // yaw normalize/quantize

Assets/Scripts/Domain/Simulation/Targeting/
  SimTargetCandidate.cs
  SimTargetResolveRequest.cs
  DeterministicTargetResolver.cs

Assets/Scripts/Domain/Combat/Targeting/
  CharacterTargetingState.cs
  CharacterTargetingSnapshot.cs

Assets/Scripts/Domain/Character/Commands/
  IMoveIntentSource.cs                  // reference yaw
  LocomotionDesire.cs                   // AI 显式 reference yaw

Assets/Scripts/Domain/Simulation/Action/
  ActionSim.cs
  ActionSimSnapshot.cs
  ActionSimEvent.cs

Assets/Scripts/App/Controllers/Camera/
  CameraManager.cs                      // 仅 staging，不写 Motor

Assets/Tests/EditMode/Simulation/
  MoveReferenceYawTests.cs
  DeterministicTargetResolverTests.cs
  SelectedTargetActionIntegrationTests.cs

Assets/Tests/EditMode/Domain/
  CharacterTargetingStateTests.cs

docs/2026.8.13/
  CAMERA_AUTHORITY_AND_TARGETING_REFACTOR_PLAN.md
```

具体目录可在实现时按 asmdef 依赖微调，但纯 Resolver 必须留在无 Transform / 无 App 依赖层。

---

## 8. 风险与对策

| 风险 | 对策 |
|------|------|
| MoveReferenceYaw staging 带一渲染样本延迟 | 与现网下一逻辑帧消费一致；InputFrame 固化后可重演，手感单独 Play 对比 |
| `ushort` yaw 在 0/360 边界跳变 | Quantizer 统一 Normalize `[0,360)`；角差使用最短弧 |
| AI 本地轴语义变化 | `LocomotionDesire` 显式携带 reference yaw；不读取玩家 Camera，也不恢复假设备输入 |
| 候选同距结果漂移 | 整数距离 + 稳定排序 + SimActorId 最终 tie-break |
| 当前目标附近出现更近敌人导致抖动 | 有效目标保持到失效；acquire/retain range 滞回 |
| 动作中切敌导致朝向/吸附突变 | 切换只在逻辑帧边界生效；Rotation 服从动作转向速率，Motion 清除上一目标的未消费修正后按新目标重算 |
| 遮挡在不同客户端不一致 | 首版不做权威遮挡解锁；只影响 UI/构图 |
| 目标死亡后动作吸附 | 先解析确定性自动补选；有新目标则后续帧改读新目标，无候选则 Query 失败并安全 no-op |
| Snapshot 还未完整实现 | 本方案先锁字段/所有权和纯重演入口；L3 再实现无损 Restore |
| Camera 锁定中切敌导致镜头硬跳 | CameraLock 保持开启，TargetGroup/VCam 对目标变化使用专用短 Blend；无候选才解锁 |
| 大范围重构难定位回归 | 按 C-AT0→1→2→3 小步提交；每阶段独立编译、测试、Play |

---

## 9. Editor 人工步骤

1. 在 Input Actions 中新增纯表现 `CameraLock`，绑定键鼠/手柄；CameraManager/Director 渲染帧读取，Agent 不直接改 `.inputactions`。  
2. 在 Input Actions 中新增 gameplay `TargetSwitchLeft/Right`（或一个可稳定量化为左右的切敌轴），绑定键鼠/手柄；它们必须映射到 InputButton/InputFrame。  
3. `CameraLock` 不进入 GameplayIntentProfile、InputButton 或 InputFrame；不得形成 gameplay 锁定意图。  
4. 配置单一 Targeting acquire/retain range；现有 ActionGraph TargetLockSettings 的选敌范围/Policy 迁出并删除。  
5. C-AT3 后 Play 检查玩家自由移动、软锁 strafing、动作中切敌、自动补选、吸附/绕背。  
6. 本方案不要求创建 LockOn VCam、TargetGroup 或锁定 UI；这些仍在 Camera C1 执行。  
7. 未来 C1 配 LockPoint 时，LockPoint 只用于构图/UI，不参与权威候选位置。

---

## 10. 推荐开工顺序

```text
C-AT0 MoveReferenceYaw 输入闭包
  → C-AT1 自动 SelectedTarget + 确定性 Resolver
  → C-AT2 Action/Motion 统一消费 SelectedTarget
  → C-AT3 纯表现 CameraLock / Snapshot 契约收口
  → 2026.8.26 CAMERA_SYSTEM_PLAN C1（Director + LockOn VCam + TargetGroup）
```

**最小正确切片：** C-AT0。它不依赖 Camera C1，却先消除现有回放/帧同步中最直接的相机渲染态权威旁路。

完整 L3/L5 不是本方案开工前置；但 C-AT0～C-AT3 必须在 Camera C1 合入前完成，避免把非确定性目标、重复目标 Id 与移动参考封装进 CameraDirector。

---

## 11. 方案完成定义

同时满足：

1. 完整 InputFrame 可在无 Camera 的测试环境重建玩家世界移动方向。  
2. 角色只保存一个 SelectedTargetId；ActionSim、Camera 与 Presentation 无第二目标字段。  
3. TargetSwitch 在 Action 期间仍更新 SelectedTarget；切换生效帧之后未解析的攻击、旋转与位移目标均改为新敌人。  
4. 权威索敌不读取 Transform、Camera、Physics Raycast 或候选集合偶然顺序。  
5. Camera/Presentation 无 Motor、TargetingState、ActionSim 写入口。  
6. 被替代的 PlanarBasis、CombatTargetLock、ActionTargetId、Presentation late-bind 路径全部删除。  
7. EditMode + Play 清单通过，架构与相机文档完成同步。

未完成 C-AT2 不得开始 Camera C1 的 TargetGroup 联动；未完成 C-AT3 不得宣告本方案完成。

---

## 12. 变更日志

| 日期 | 说明 |
|------|------|
| 2026-08-13 | 初版：定案 MoveReferenceYaw 输入闭包、Manual/Action/Camera 三种目标语义与 ActionSim Begin 原子绑定 |
| 2026-08-13 | 产品语义修订：目标收敛为唯一 SelectedTargetId；CameraLock 仅为本地相机开关；删除 ManualLockTargetId / ActionTargetId 双目标方案 |
| 2026-08-13 | 动态切敌修订：删除动作期冻结；TargetSwitch 进入 InputFrame，Action/Motion 每帧改读最新 SelectedTarget，Camera Lock 平滑跟随切换 |
