# 敌人行为树完善方案（通向可视化编辑器）

> 制定：2026-08-09  
> 角色：**BT-1 之后的演进真源**（节点库 / 调试 / GraphView / 与插件边界）  
> 前置已关闭：BT-1 运行时 + Play 验收；BT-2 调试 + Custom SerializeReference Inspector  
> 基础契约仍以 [ENEMY_BEHAVIOR_TREE_PLAN.md](../ENEMY_BEHAVIOR_TREE_PLAN.md) §3.4 为准  
> 总清单交叉：[PROJECT_CHECKLIST.md](../PROJECT_CHECKLIST.md) §6.4

---

## 0. 一句话

在**不破坏锁步输入轨**的前提下，把自研 BT 从「近战追打够用」扩成「可配 Task 目录 + 可调试 + **GraphView 可视化编辑**」；插件只作可选 Adapter，永不泄漏进 `EnemyBrain`。

---

## 1. 现状（2026-08-09）

| 层 | 状态 |
|----|------|
| 契约 `IEnemyBehaviorTreeAsset` / `IEnemyBehaviorRunner` | ✅ |
| 宿主 `EnemyBrain`（门闩 + 黑板 + 提交 `AIInputWriter`） | ✅ |
| 预设 Melee / ChaseOnly + Custom SerializeReference | ✅ |
| 调试 NamedNode / Gizmo / 变化日志 | ✅ |
| Task（叶行动）目录 | ✅ E1：+BackOff / Strafe / PulseDodge·Heavy·Skill |
| 条件 / 装饰 | ✅ E1：CooldownReady / DistanceGreater / CooldownGate（仍无 Abort） |
| 冷却 | ✅ `EnemyCooldownTable`（basic_attack 由 Brain 确认写入） |
| 寻路 | ⬜ `StraightPathQuery` 占位 |
| Graph 布局 / Flatten / Validate | ✅ BT-E2 |
| GraphView 可视化编辑器 | ✅ BT-E3 MVP |

**数据流（不变）：**

```text
SimulationWorld.ProduceInput
  → EnemyBrain.Step
       → Runner.Tick(EnemyBlackboard)
       → AIInputWriter → InputFrame
  → CharacterActor.Step（Intent → Graph）
```

---

## 2. 目标与非目标

### 2.1 终态目标

1. **可视化行为树编辑器**（Unity GraphView）：拖拽复合/条件/Task，保存回 `EnemyBehaviorTreeAsset`（Custom 根或等价图数据）。  
2. **Task 目录可扩展**：闪避 / 特殊等输入型行动，仍只写黑板 → 输入帧。  
3. **运行时调试**：图上高亮 Running 路径；与现有 DebugPath/Gizmo 同源。  
4. **契约稳定**：`EnemyBrain` 只认 `IEnemyBehaviorRunner`；日后插件 Adapter 可替换，不改宿主。

### 2.2 非目标

- 本演进**不安装** Behavior Designer / NodeCanvas / Unity Behavior 作为运行时依赖。  
- Task **禁止**直接 `TryStart`、改 Numeric、驱动 Animator 权威、自挂 `Update`。  
- 不做 GOAP / Utility 主决策（可日后作另一 Runner）。  
- 完整 NavMesh 群体 AI（BT-E 仅接 `IEnemyPathQuery`，细节可另开寻路篇）。

### 2.3 与「插件架构」的对齐边界

| 对齐 | 不对齐 |
|------|--------|
| Composite / Decorator / Condition / Task 语义 | 插件 Task 全能副作用 |
| 黑板 + 三态 | 插件自管时钟 |
| Graph 编辑体验 | 插件类型进 Brain/Actor |
| SimulationWorld 逻辑帧 Tick | `MonoBehaviour.Update` 权威 |

---

## 3. 架构定案（编辑器与数据）

### 3.1 双表示：编辑图 ↔ 运行树

```text
EnemyBehaviorTreeAsset
├─ kind: Melee | ChaseOnly | Custom
├─ customRoot: SerializeReference 节点定义树   // 已有，继续为真源之一
└─ graphLayout (新增，仅 Editor)
     ├─ nodeGuid / position
     ├─ edges (parent→child 或 output port)
     └─ 可选 sticky notes

CreateRunner:
  Custom → customRoot.Build() → NativeBehaviorTreeRunner
  预设 → Presets（可继续保留作一键模板）
```

**定案：**

- **运行真源** = `EnemyBehaviorNodeDef` 树（或由其生成的 `IBehaviorNode`）。  
- **GraphView** = 编辑 `NodeDef` + 布局元数据；保存时写回 SO，不另立第二套运行格式。  
- 打开编辑器时：Def 树 → 图；保存时：图 → Def 树 + layout。

### 3.2 Task 命名（与插件用语对齐，不换执行模型）

```text
IBehaviorNode
  └─ IEnemyBehaviorTask : IBehaviorNode   // 标记接口，叶行动
  └─ IEnemyBehaviorCondition : IBehaviorNode
```

仅作分类与编辑器调色板分组；Tick 语义不变。

### 3.3 黑板输出槽扩展（帧末仍由 Brain 提交）

| 槽 | 现状 | 扩展 |
|----|------|------|
| `MoveDesire` | ✅ | 保留 |
| `AttackPulse` | ✅ | 泛化为 `ButtonPulse` 或并列 `DodgePulse` / `SpecialPulse`… |
| `FaceTargetRequested` | ✅ | 保留 |
| CD / 旗位 | Brain 维护基础攻击 CD | 通用 `CooldownTable`（id→剩余帧）供条件读 |

**提交规则不变：** 帧初清输出 → Tick → Brain 映射到 `AIInputWriter`（需扩展 Writer 支持多按钮脉冲）。

---

## 4. 分阶段计划

### Phase BT-E1 — Task / 条件目录扩容（运行时先够用）✅ 2026-08-09

**目标：** 编辑器做出来之前，树就能表达更丰富近战策略。

| # | 交付 | 验收 |
|---|------|------|
| E1.1 | `AIInputWriter` 多按钮 `Pulse` / `PulseDodge` | ✅ EditMode 测 bit |
| E1.2 | Task：Dodge / Strafe / BackOff；WaitFrames 可配 | ✅ Kite 预设 |
| E1.3 | `CooldownReady` / `DistanceGreater` / `CooldownGate` | ✅ |
| E1.4 | `EnemyCooldownTable`；删 `_attackCooldownFramesRemaining` 单字段 | ✅ |
| E1.5 | 预设 Kind=`Kite` + Fill 按钮 + 下方结构说明 | ✅ |

**禁止：** Task 内起招；新增第二套冷却权威。

**出口：** 不靠 Graph 也能用 Inspector Custom 配出「追打 / 只追 / 风筝」三类。

#### E1.5 示例树结构（Custom Fill 或 Kind 预设）

```text
MeleeRoot (Selector)
  Attack: HasTarget → InAttackRange → Locomotion → CooldownReady(basic_attack) → Stop → PulseAttack
  Chase:  HasTarget → Aggro → MoveToward
  Idle:   StopMove

ChaseOnlyRoot (Selector)
  Chase → Idle

KiteRoot (Selector)          // 默认阈值 ≤2.5 后退，>4 追击
  BackOff: HasTarget → Aggro → Dist≤2.5 → BackOff
  Chase:   HasTarget → Aggro → Dist>4 → MoveToward
  Hold:    HasTarget → Aggro → Face → Stop
  Idle:    StopMove
```

Editor：`ACT/Enemy/Create Default Behavior Tree Assets` 可创建含 `BT_Kite.asset`；Custom 用 Inspector **Fill ← Kite**。

---

### Phase BT-E2 — 编辑器数据与校验加固 ✅ 2026-08-09

**目标：** 为 GraphView 铺稳序列化与校验，避免图画布绑死临时格式。

| # | 交付 | 验收 |
|---|------|------|
| E2.1 | `EnemyBehaviorGraphLayout` + `NodeDef.nodeGuid` 挂资产 | ✅ 丢布局不丢逻辑树 |
| E2.2 | `EnemyBehaviorTreeGraphMapper` Flatten / Rebuild / SyncLayout | ✅ EditMode 往返 |
| E2.3 | `EnemyBehaviorTreeValidator`；菜单 + `OnValidate` Error | ✅ 环 / 空 child / 空根 |
| E2.4 | `Wrap` 始终 NamedNode（NodeName 或短类型名） | ✅ DebugPath 可读 |

**出口：** 数据层声明「可被 Graph 编辑」；仍可用纯 Inspector。

---

### Phase BT-E3 — GraphView 可视化编辑器（主交付）✅ 2026-08-09

**目标：** 自研轻量节点图画布，体验接近插件「能拖、能连、能存」，范围克制。

| # | 交付 | 验收 |
|---|------|------|
| E3.1 | `EnemyBehaviorTreeEditorWindow` | ✅ `ACT/Enemy/Behavior Tree Editor` |
| E3.2 | 调色板 SearchWindow（空格/右键） | ✅ 分组创建 NodeDef |
| E3.3 | Out→In 连线；复合多子/装饰单子；删节点 | ✅ Save 写回 |
| E3.4 | 右侧 Node Inspector | ✅ 距离/帧/冷却/状态等 |
| E3.5 | Save / Revert；layout 按 guid | ✅ |
| E3.6 | Fill Melee / ChaseOnly / Kite | ✅ |
| E3.7 | Play 选中敌人高亮 DebugPath | ✅ 可选 |

**出口：** 不用手写 SerializeReference 嵌套，也能编出可运行 Custom 树。

---

### Phase BT-E4 — 运行时打磨与寻路汇合

| # | 交付 | 说明 |
|---|------|------|
| E4.1 | `WaitAttackConfirm` 节点化（可选） | 或保持 Brain 观测，图上用文档节点说明 |
| E4.2 | `MoveAlongPath` + `NavMeshPathQuery` / A\* | 对齐原 BT-3；路径方向写 `PathDirection` |
| E4.3 | 多敌 repath 错峰 | Profile 帧偏移 |
| E4.4 | 性能：节点池 / 少 GC（StringBuilder Debug 已部分做） | Profiler 木桩+多敌 |

---

### Phase BT-E5 —（可选）插件 Adapter

仅当自研 Graph 不够用时：

```text
PluginBehaviorTreeAdapterAsset : IEnemyBehaviorTreeAsset
  → PluginBehaviorRunner : IEnemyBehaviorRunner
       每逻辑帧由 Brain 调用；只映射到黑板输出槽
```

不阻塞 E1～E3。

---

## 5. 推荐开工顺序

```text
E1 Task/条件扩容 + Writer 多按钮     ← 立刻可玩性
  → E2 布局/校验数据                 ← Graph 前置
  → E3 GraphView 编辑器              ← 主目标
  → E4 寻路 + 打磨
  → E5 插件 Adapter（可选）
```

**最小可视化切片（E3 MVP）：**  
只支持 Selector / Sequence / 现有 Condition+Task 子集 + 保存加载；Abort/Parallel/Service **不做进 MVP**。

---

## 6. 目录预期（增量）

```text
Assets/Scripts/Domain/Enemy/BehaviorTree/
  Tasks/                 // 可选：从 ActionNodes 拆出，实现 IEnemyBehaviorTask
  CooldownTable.cs
  Serialization/
    EnemyBehaviorGraphLayout.cs

Assets/Scripts/Editor/Enemy/BehaviorTree/
  EnemyBehaviorTreeEditorWindow.cs
  EnemyBehaviorGraphView.cs
  Nodes/*GraphNode.cs
  EnemyBehaviorTreeGraphSerializer.cs
```

---

## 7. 风险与对策

| 风险 | 对策 |
|------|------|
| Graph 与 Def 双真源 | 保存以 Def 为准；Layout 可丢 |
| SerializeReference 多态丢类型 | 稳定类名；Validate；菜单 Rebuild |
| 编辑器做太大 | E3 MVP 不加 Abort/Parallel |
| Writer 多按钮破坏玩家输入 | 仅 AI Writer 扩展；玩家路径不动 |
| 可视化后仍想「Task 起招」 | CR 红线 + 节点基类不暴露 Executor |

---

## 8. 成功标准（整包）

- [ ] Inspector Custom 与 GraphView **编辑同一资产**，Play 行为一致  
- [ ] 至少 3 类策略树可配：追打 / 只追 / 含闪避或风筝  
- [ ] 逻辑帧驱动；无 BT `Update` 权威  
- [ ] `EnemyBrain` 无具体节点/插件类型字段  
- [ ] Graph MVP 可 Undo、可保存、可从模板填充  

---

## 9. 决策记录

| 日期 | 决策 | 理由 |
|------|------|------|
| 2026-08-09 | 可视化用自研 GraphView，不先上商城 BT 包 | 锁步输入轨 + §3.4 可替换；避免 API 泄漏 |
| 2026-08-09 | 运行真源保持 NodeDef 树；Graph 只加 Layout | 防双权威 |
| 2026-08-09 | Task = 输入型叶行动；先扩目录再做画布 | 没有节点可拖则编辑器空洞 |
| 2026-08-09 | E3 MVP 不做 Abort/Parallel/Service | 控制首版可视化范围 |

---

## 10. 下一步

**BT-E1 / E2 / E3 MVP 已关闭。** 可选下一刀：**E4 寻路**，或打磨 Graph（Abort 不做、便签编辑、更稳 Undo）。
