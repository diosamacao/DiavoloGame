# ActionDefinition 职责拆分与数据模型优化方案

> 制定日期：2026-07-29  
> 目标：将 `ActionDefinition` 从跨动画、选招、拓扑、战斗、反馈、索敌的万能对象，收敛为单动作聚合根  
> 原则：新数据模型是唯一真源；每阶段同步替换调用点并删除旧字段、旧转发 API 与运行时兼容分支

---

## 1. 背景与问题

当前 `ActionDefinition` 同时承担以下职责：

1. 输入路由：`trigger`
2. 动画播放：`animationSegments`、CrossFade、Clip 时间换算
3. 统一时钟：`sampleRate`、`totalFrames`
4. 动作分类与打断：`actionType`、`interruptPriority`
5. 伤害：`baseDamage`
6. 帧时间轴：`ActionTimeline`
7. 自动拓扑：`ActionTransition[]`
8. 起手副作用：`startBehaviors`、战斗模式切换参数
9. 命中反馈：CameraShake、HitStop
10. 索敌：`TargetLockSettings`
11. 位移策略：Root Motion / Timeline Movement
12. 查询与算法：动画段解析、Transition 判定、Phase/Cancel 查询、资产迁移和校验

这造成三个核心问题：

- **依赖扩散**：ActionExecutor、ActionGraph、Damage、Targeting、Camera、HitStop、Editor 都依赖完整 `ActionDefinition`
- **数据双真源**：Graph 管一部分拓扑，`ActionDefinition.transitions` 又管另一部分；Graph 路由意图还依赖目标 Action 的 Trigger
- **变更放大**：修改反馈或伤害字段也会影响 ActionDefinition、Inspector、ActionEditor 和全部资产

---

## 2. 重构目标

### 2.1 目标状态

`ActionDefinition` 只负责：

- 单动作的语义分类
- 统一 Logic Tick 采样率
- 聚合各职责数据块
- 暴露动作总帧数、时长等少量聚合只读结果

### 2.2 非目标

- 不重写 `ActionExecutor` 的播放循环
- 不改变 `ActionTimeline` 作为帧数据唯一真源的原则
- 不引入第二套 ActionDefinition V2
- 不同时保留旧字段与新字段运行
- 首阶段不把所有数据块都拆成独立 ScriptableObject 资产
- 不在本方案中直接完成 AttributeSheet / 完整属性伤害系统

---

## 3. 最终职责边界

### 3.1 ActionDefinition — 聚合根

建议最终字段：

```text
ActionDefinition : ScriptableObject
├─ actionType : CombatActionType
├─ sampleRate : float
├─ presentation : ActionPresentationData
├─ timeline : ActionTimeline
├─ execution : ActionExecutionPolicy
├─ combat : ActionCombatData
├─ feedback : ActionFeedbackData
└─ targeting : ActionTargetingData
```

保留的聚合 API：

```text
ActionType
SampleRate
TotalFrames
DurationSeconds
Presentation
Timeline
Execution
Combat
Feedback
Targeting
```

`ActionDefinition` 不再直接实现动画段搜索、Transition 判定、Phase 查询、反馈开关计算等算法。

### 3.2 ActionPresentationData — 动画与动画驱动位移

负责：

- `ActionAnimationSegment[]`
- 默认 CrossFade
- `useRootMotion`
- `HasAnimation`
- 总帧数计算
- 全局帧 → 动画段 / 段内帧解析
- Clip 局部采样时间
- 每段 CrossFade 解析

建议 API：

```text
Segments
UseRootMotion
HasAnimation
CalculateTotalFrames(sampleRate)
TryGetSegmentAtFrame(frame, sampleRate, ...)
GetLocalTimeInSegment(frame, sampleRate)
ResolveSegmentCrossFade(segmentIndex)
```

说明：

- `useRootMotion` 与动画表现绑定；关闭时才允许 Timeline Movement 生效
- `totalFrames` 不再作为独立可编辑字段，由动画段实时推导
- ActionEditor 的时间轴长度直接读取 `ActionDefinition.TotalFrames`

### 3.3 ActionExecutionPolicy — 执行与打断规则

负责：

- `interruptPriority`
- 起手行为列表
- 起手行为参数

当前 `ActionStartBehaviorType[]` 与战斗模式参数分散，建议改为：

```text
ActionStartBehaviorData
├─ type
├─ combatModeTarget
└─ combatModeSwitchPolicy
```

执行策略结构：

```text
ActionExecutionPolicy
├─ interruptPriority
└─ startBehaviors : ActionStartBehaviorData[]
```

这样每个行为携带自己的参数，不再出现“枚举在一个数组、参数在 ActionDefinition 其他位置”的隐式关联。

### 3.4 ActionCombatData — 单招战斗载荷

第一阶段负责：

- `baseDamage`

与属性伤害系统接轨后调整为：

```text
ActionCombatData
├─ damageMultiplier
├─ poiseDamage
├─ damageType
└─ hitReactionLevel
```

最终伤害建议：

```text
Attacker.Attack
  × ActionCombatData.DamageMultiplier
  × HitboxNotifyState.DamageWeight
  → DamageCalculator
```

边界：

- ActionCombatData 描述“这招携带什么战斗载荷”
- HitboxNotifyState 描述“本段判定如何修正该载荷”
- DamageCalculator 描述“攻击者与受击者属性如何形成最终结果”

### 3.5 ActionFeedbackData — 命中反馈

负责：

```text
ActionFeedbackData
├─ cameraShakeProfile
├─ enableCameraShake
├─ enableHitStop
├─ hitStopFrames
└─ hitStopOncePerAction
```

删除以下双重布尔路径：

```text
useCameraShakeOnHit + disableCameraShakeOnHit
useHitStopOnHit + disableHitStopOnHit
```

运行时建议：

```text
ApplyHitCommand
  → 从 ActionFeedbackData 创建 ActionFeedbackSnapshot
  → AttackHitEvent 携带 Snapshot
  → CameraShakeController / HitStopController 只读取 Snapshot
```

这样 App 反馈系统不再依赖完整 `ActionDefinition`。

### 3.6 ActionTargetingData — 单招索敌策略

负责：

- 是否开启起手索敌
- LockRange
- ForwardCone
- SelectionPolicy
- 锁定转向平滑覆盖值

可直接由现有 `TargetLockSettings` 重命名或包裹为 `ActionTargetingData`。

调用边界：

```text
CombatTargetLock.Acquire(ActionTargetingData)
ActionRotationDriver.Resolve(ActionTargetingData, RotationNotifyState)
```

Targeting 模块不再接收完整 `ActionDefinition`。

### 3.7 ActionTimeline — 保留为帧数据唯一真源

保留现有职责：

- Event / VFX / SFX 点事件
- Phase / Hitbox / Hurtbox / Cancel / Movement / Rotation 区间状态
- 帧触发查询
- 区间 Enter / Tick / Exit 查询
- Clamp

删除 `ActionDefinition` 中以下 Timeline 转发 API：

```text
HitboxStates
PlayVfxNotifies
PlaySfxStates
Phases
ActionEvents
GetCancelWindow
GetActivePhasesAtFrame
GetActiveHitboxesAtFrame
GetActiveMovementState
GetActiveRotationState
IsCancelWindowActiveAtFrame
AllowsRecoveryMovementCancelAtFrame
AllowsRecoveryEntryRestartAtFrame
```

消费者统一调用：

```text
action.Timeline.*
```

ActionTimeline 内可补充语义查询：

```text
IsInterruptibleAtFrame(frame)
AllowsRecoveryMovementCancelAtFrame(frame)
AllowsRecoveryEntryRestartAtFrame(frame)
```

---

## 4. 必须移出 ActionDefinition 的路由与拓扑

### 4.1 Trigger 移到 ActionGraph

问题：

- Trigger 是“如何选择动作”，不是动作本体属性
- 同一 Action 无法自然对应多个输入意图
- Hit / Death 等非输入动作仍被迫携带 Trigger
- Graph 的 Entry 与边通过目标 Action.Trigger 推导路由，拓扑语义不完整

目标模型：

```text
ActionGraphNode
├─ action
├─ isEntry
├─ entryIntent            // 仅 Entry 使用
└─ variantResolver

ActionGraphEdge
├─ fromNodeId
├─ toNodeId
├─ routeKind              // Normal / Perfect
└─ intent                 // 本边消费的 GameplayIntentType
```

运行时变化：

- `TryResolveStart` 按 `node.EntryIntent` 匹配
- `TryResolveNext` 按 `edge.Intent` 匹配
- SharedRoute 继续使用自身 `Intent`
- ActionDefinition 删除 `trigger`

Editor 变化：

- Graph 节点显示 EntryIntent
- 边显示 Intent
- Entry 重复校验改为检查 `EntryIntent`
- 边冲突校验改为 `(FromNodeId, RouteKind, Intent)`

### 4.2 自动 Transition 移到 ActionGraph

问题：

- `ActionDefinition.transitions[]` 直接引用目标 Action
- ActionGraph 同时保存 Cancel 拓扑
- 自动衔接与输入衔接形成两套动作关系真源

建议新增：

```text
ActionGraphAutoTransition
├─ fromNodeId
├─ toNodeId               // 空表示 Stop
├─ condition              // AnimationEnd / AtFrame / OnHitConfirm / OnWhiff
├─ startFrame
└─ priority
```

运行时变化：

```text
ActionExecutor.TryResolveTransitions
  → CurrentGraph.TryResolveAutoTransition(
        CurrentNodeId,
        CurrentFrame,
        Elapsed,
        HasConfirmedHit)
```

规则：

- 有 Graph 游标时只查询 Graph AutoTransition
- 无 Graph 游标的直接播放 Action（如 Hit / Death）播完后 Stop
- ActionDefinition 删除 `transitions`
- 自动 Transition 目标必须是同一 Graph 中的节点，禁止继续直接引用游离 Action

---

## 5. 算法归属调整

### 5.1 移入 ActionPresentationData

- `ComputeTotalFramesFromSegments`
- `TryGetSegmentAtFrame`
- `TryGetSegmentAtElapsed`
- `TryGetLastValidSegment`
- `GetClipAtFrame`
- `GetLocalTimeInSegment`
- `ResolveSegmentCrossFade`

### 5.2 移入 ActionTimeline

- Phase 生效查询
- Interruptibility 查询
- Recovery Movement Cancel 查询
- Recovery Entry Restart 查询
- CancelWindow 生效查询
- Movement / Rotation 最高优先级窗口查询

### 5.3 移入 ActionGraph

- Transition 排序
- Transition 条件判定
- Trigger 路由
- Cancel 边候选意图收集

### 5.4 移入 Editor

- 旧字段资产迁移
- 数据 Clamp 与跨字段校验
- Graph 路由冲突检查
- Action 数据完整性检查

`OnValidate` 最终只调用轻量数据规范化，不保留旧资产迁移代码。

---

## 6. 序列化形态决策

第一阶段所有新数据块使用内嵌 `[Serializable]`：

```text
ActionDefinition.asset
└─ presentation / timeline / execution / combat / feedback / targeting
```

原因：

- 保持一招一个主资产
- ActionEditor Undo 简单
- 避免大量子资产
- 批量迁移成本较低

只有明确需要跨 Action 复用的数据才使用独立 ScriptableObject：

- `CameraShakeProfile`
- 通用 DamageProfile（后续）
- 通用 TargetingProfile（出现实际复用需求后）

禁止为每个数据块默认创建独立 `.asset`。

---

## 7. 分阶段实施

### Phase A — 拆 Feedback / Combat / Targeting

改动：

- 新增 `ActionFeedbackData`
- 新增 `ActionCombatData`
- 将 `TargetLockSettings` 收敛为 `ActionTargetingData`
- 修改 CameraShakeController、HitStopController、DamageCalculator、CombatTargetLock
- AttackHitEvent 携带反馈快照
- 删除 ActionDefinition 旧反馈、伤害、索敌字段与旧 API

资产迁移：

- CameraShake / HitStop 字段写入 Feedback
- `baseDamage` 写入 Combat
- `targetLockSettings` 写入 Targeting
- 迁移后删除旧字段，不保留运行时 fallback

验收：

- Hitbox、伤害、卡肉、玩家镜头震动行为不变
- Target Lock 行为不变
- Camera / Feedback / Damage / Targeting 不再读取完整 ActionDefinition

### Phase B — 拆 Presentation / Execution

改动：

- 新增 `ActionPresentationData`
- 新增 `ActionExecutionPolicy`
- 新增带参数的 `ActionStartBehaviorData`
- ActionExecutor 改读 `Presentation` / `Execution`
- 动画段算法移出 ActionDefinition
- `TotalFrames` 改为 Presentation 推导
- 删除旧 `animationClip`、`animationSegments`、`crossFadeDuration`、`useRootMotion`、`interruptPriority`、StartBehavior 字段

资产迁移：

- 旧单 Clip 和多段字段统一迁入 Presentation
- StartBehavior 的 CombatMode 参数合并到对应行为项
- 迁移后删除 `MigrateLegacyAnimationClipIfNeeded`

验收：

- 多段动画边界、裁切帧和 CrossFade 与现状一致
- Root Motion / Timeline Movement 互斥规则不变
- Dodge、硬打断、模式切换行为不变
- ActionEditor Preview 与 Play Mode 使用同一 Presentation API

### Phase C — Timeline API 收敛

改动：

- 将 Phase / Cancel / Movement / Rotation 查询移入 ActionTimeline
- 所有消费者改用 `action.Timeline`
- 删除 ActionDefinition 的 Timeline 转发属性与查询方法

验收：

- Normal / Perfect CancelWindow 行为不变
- Recovery 移动取消和 Entry Restart 行为不变
- Hitbox / VFX / SFX / Rotation / Movement Logic Tick 不变

### Phase D — Trigger 迁入 ActionGraph

改动：

- ActionGraphNode 新增 `entryIntent`
- ActionGraphEdge 新增 `intent`
- Resolver、Graph Validator、Graph Editor 全部改读 Graph 路由字段
- 顺序组生成边时显式写入 Intent
- 删除 `ActionDefinition.trigger`

资产迁移：

- Entry 节点的 `entryIntent = node.Action.Trigger`
- 每条边的 `intent = targetNode.Action.Trigger`
- SharedRoute 已有 Intent，无需从 ActionDefinition 读取

验收：

- 多 Entry 起手行为不变
- Normal / Perfect / SharedRoute 解析不变
- Directional Variant 仍只改变实际播放 Action，不改变逻辑节点
- 同一 Action 可被不同 Graph 或不同 Intent 复用

### Phase E — Transition 迁入 ActionGraph

改动：

- 新增 `ActionGraphAutoTransition`
- Graph Editor 支持自动边与条件编辑
- ActionExecutor 通过当前 Graph 游标解析自动 Transition
- 删除 `ActionDefinition.transitions`
- 删除 ActionDefinition 的 Transition 排序与条件判定

资产迁移：

- 按当前 Action 所在节点，把每条 `ActionTransition` 转为 Graph AutoTransition
- TargetAction 必须能映射到同 Graph 节点
- 无法映射的 Transition 在迁移报告中列为阻塞错误，不生成 fallback

验收：

- AnimationEnd / AtFrame / OnHitConfirm / OnWhiff 行为不变
- 自动边与 Cancel 边优先级明确：Cancel → Recovery Entry → AutoTransition → Duration Stop
- Graph 成为动作拓扑唯一真源

### Phase F — 删除迁移工具与旧路径

完成资产迁移并人工抽查后：

- 删除旧字段读取逻辑
- 删除旧序列化字段
- 删除旧 ActionDefinition 转发 API
- 删除运行时 fallback
- 删除一次性迁移菜单或将其改为纯校验工具
- 更新 ACTION_SYSTEM、ACTION_EDITOR、TECHNICAL、CONVENTIONS

---

## 8. Editor 迁移工具要求

由于 Agent 不直接修改 `.asset`，实施时提供一次性 Editor Migration Tool，由用户在 Unity Editor 中执行。

建议菜单：

```text
ACT/Action Data/Migrate ActionDefinition Schema
ACT/Action Data/Validate ActionDefinition Schema
```

迁移流程：

1. 扫描全部 ActionDefinition
2. 扫描全部 ActionGraph
3. 预检查 Action → Graph 节点映射
4. 输出迁移预览报告
5. 用户确认后 `Undo.RecordObject`
6. 写入新数据结构
7. 标记 Dirty 并 SaveAssets
8. 再运行 Validator

迁移必须满足：

- 可重复运行时第二次无改动
- 遇到无法映射的 Transition 立即报告，不静默丢失
- 不创建 Legacy / V1 / Fallback 数据
- 不在运行时执行资产迁移

---

## 9. 影响文件

### Domain

- `Assets/Scripts/Domain/Combat/Actions/Definitions/ActionDefinition.cs`
- `Assets/Scripts/Domain/Combat/Actions/Definitions/ActionAnimationSegment.cs`
- `Assets/Scripts/Domain/Combat/Actions/Definitions/ActionTransition.cs`
- `Assets/Scripts/Domain/Combat/Actions/Definitions/Timeline/ActionTimeline.cs`
- `Assets/Scripts/Domain/Combat/Actions/Resolution/ActionGraph.cs`
- `Assets/Scripts/Domain/Combat/Actions/Resolution/ActionResolverService.cs`
- `Assets/Scripts/Domain/Combat/Actions/Execution/ActionExecutor.cs`
- `Assets/Scripts/Domain/Combat/Actions/Execution/ActionSession.cs`
- `Assets/Scripts/Domain/Combat/Targeting/CombatTargetLock.cs`
- `Assets/Scripts/Domain/Combat/Actions/Execution/ActionRotationDriver.cs`
- `Assets/Scripts/Domain/Combat/Damage/CombatDamageCalculator.cs`

### App

- `Assets/Scripts/App/Events/Combat/AttackHitEvent.cs`
- `Assets/Scripts/App/Commands/Combat/ApplyHitCommand.cs`
- `Assets/Scripts/App/Controllers/Combat/HitStopController.cs`
- `Assets/Scripts/App/Controllers/Camera/CameraShakeController.cs`

### Editor

- `Assets/Scripts/Editor/Combat/ActionEditor/*`
- `Assets/Scripts/Editor/Combat/ActionGraph/*`
- `Assets/Scripts/Editor/Combat/ActionDefinitionHitboxEditor.cs`
- 新增一次性 Schema Migration / Validator

---

## 10. 风险与对策

### 10.1 SerializedProperty 路径变化

风险：

- ActionEditor 当前直接查找 `animationSegments`、`sampleRate`、`totalFrames`、`timeline`

对策：

- 每阶段同步修改 Editor，不保留旧路径
- 将常用 SerializedProperty 路径集中到一个 Editor 常量类
- Phase B 完成后不再直接编辑 `totalFrames`

### 10.2 Trigger 迁移破坏 Graph 路由

风险：

- 多 Entry、SharedRoute、Directional、顺序组都依赖 Trigger

对策：

- 先完成 Graph 预检查和迁移报告
- 对每个 Entry 和 Edge 显式写入 Intent
- 迁移后运行 Graph Validator

### 10.3 Transition 无法映射到节点

风险：

- 某些 TargetAction 不在当前 Graph

对策：

- 迁移前列出全部游离 TargetAction
- 用户先补 Graph 节点或决定删除该 Transition
- 禁止自动创建隐藏节点或运行时回退

### 10.4 资产数量与复用

风险：

- 过早拆成子 SO 会造成资产碎片

对策：

- 默认内嵌 Serializable 数据块
- 只有实际存在跨招复用时才提升为 Profile SO

### 10.5 每阶段资产不可用

风险：

- Schema 调整后旧资产在迁移前无法运行

对策：

- 每个涉及 Schema 的阶段同时交付 Migration Tool
- 用户先执行迁移，再进入 Play Mode 验收
- 不通过 runtime fallback 延长双轨期

---

## 11. 验收清单

### 数据边界

- [ ] ActionDefinition 仅保留聚合根职责
- [ ] Feedback / Combat / Targeting / Presentation / Execution 均有独立数据类型
- [ ] Timeline 是帧数据唯一真源
- [ ] ActionGraph 是路由与拓扑唯一真源
- [ ] ActionDefinition 不再保存 Trigger 和 Transition

### 运行时

- [ ] 起手、连段、Perfect、SharedRoute 行为一致
- [ ] Directional Variant 行为一致
- [ ] 高优硬打断行为一致
- [ ] Recovery 移动取消和 Entry Restart 行为一致
- [ ] 多段动画、Root Motion、脚本位移行为一致
- [ ] Hitbox、伤害、HitStop、CameraShake 行为一致
- [ ] Hit / Death 直接 Action 能自然结束

### Editor

- [ ] ActionEditor 可创建、预览、Scrub 新数据结构
- [ ] Graph Editor 可编辑 EntryIntent、EdgeIntent 与 AutoTransition
- [ ] Undo / Redo 正常
- [ ] Validator 无错误
- [ ] 全部资产迁移完成
- [ ] 无旧字段、旧 API、Legacy/Fallback 分支

---

## 12. 推荐执行顺序

建议按以下顺序实施：

```text
Phase A Feedback / Combat / Targeting
  → Phase B Presentation / Execution
  → Phase C Timeline API
  → Phase D Trigger → Graph
  → Phase E Transition → Graph
  → Phase F 删除迁移路径并同步文档
```

前 3 个阶段降低 ActionDefinition 的字段与算法复杂度；后 2 个阶段消除 ActionDefinition 与 ActionGraph 的拓扑双真源。

完成后，`ActionDefinition` 仍是每招的主资产，但不再成为 Camera、Damage、Targeting、Graph 和 Editor 的万能依赖对象。
