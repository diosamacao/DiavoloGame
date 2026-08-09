# 敌人行为树优化方案（对照 JL / Behavior Designer）

> 制定：2026-08-09  
> 角色：**E3 MVP 之后的优化真源**（编辑器体验 / 决策拓扑 / Abort 子集）  
> 参考项目（只读对照，**不引入为运行时依赖**）：  
> - `D:\Projects\jlbehavior-tree`（JLBehaviourTree：自制 GraphView）  
> - `D:\Projects\BehaviorTreeDemoReaper`（Behavior Designer 1.6.6 + Reaper Boss）  
> 契约真源：[ENEMY_BEHAVIOR_TREE_PLAN.md](../ENEMY_BEHAVIOR_TREE_PLAN.md) §3.4  
> 演进前置：[ENEMY_BEHAVIOR_TREE_EVOLUTION_PLAN.md](./ENEMY_BEHAVIOR_TREE_EVOLUTION_PLAN.md)（E1～E3 已关闭）

---

## 0. 一句话

在**不破坏锁步输入轨、不引入第三方 BT 运行时**的前提下：学 **JL 的编辑器壳与调试观感**，学 **Reaper/BD 的 Boss 树分层与招式池套路**，落到自研 `NodeDef` + GraphView 上分阶段补齐。

---

## 1. 参考项目摘要

### 1.1 JLBehaviourTree

| 维 | 要点 |
|----|------|
| 运行时 | `Root.Tick` + UniTask 每帧；树嵌场景 MonoBehaviour；**无黑板 / Abort / Service** |
| 节点 | Composite / Precondition / Action；有 Parallel；Sequence 语义**非经典** |
| Task | 直绑 Transform/Animator，强场景耦合 |
| 编辑器 | GraphView + UITK；**自上而下**；左 Odin 检视 / 右画布；类别配色；边上流光；自动排版；SearchWindow |

**可吸收：** 竖向图、配色分区、Running 边动画、自动排版、左检视布局。  
**不吸收：** 嵌场景存树、非经典 Sequence、无黑板、Odin 硬依赖、Task 直控动画。

### 1.2 BehaviorTreeDemoReaper（BD + Reaper）

| 维 | 要点 |
|----|------|
| 运行时 | Behavior Designer；`SharedVariable` 黑板；Abort/Parallel 能力齐全（Demo 少用） |
| 树拓扑 | `死亡 > Idle > RandomSelector(招式)`；距离门 + 血量阶段 + 招后 Wait |
| Task | **直接** NavMesh + Animator Trigger（与 ACT 红线相反） |
| 编辑器 | BD 闭源：深色竖向图、任务库、Variables、运行高亮 |

**可吸收：** Boss 分层、随机招式池、距离/血量条件、招后冷却、Variables 监视习惯。  
**不吸收：** BD 运行时依赖、Task 起招、Update 权威时钟、HybridCLR 热更边界（另议题）。

### 1.3 ACTGame 现状（基线）

```text
SimulationWorld.ProduceInput
  → EnemyBrain.Step（门闩 / 填黑板 / Runner.Tick / AIInputWriter）
  → CharacterActor.Step
```

| 层 | 状态 |
|----|------|
| 契约 / Brain / 输入轨 | ✅ |
| Task / 冷却 / Kite | ✅ E1 |
| Graph 数据 + Validate | ✅ E2 |
| GraphView MVP（竖向） | ✅ E3 |
| Abort / RandomSelector / 黑板监视 | ⬜ |
| 寻路 | ⬜ E4（可与本方案并行，不阻塞 A/B） |

---

## 2. 整合原则（硬约束）

1. **运行时不安装** Behavior Designer / JL / NodeCanvas 等包。  
2. **Task 只写黑板输出槽** → Brain 提交 `InputFrame`；禁止 `TryStart` / 改 Numeric / 驱动 Animator 权威。  
3. **`EnemyBrain` 只认** `IEnemyBehaviorRunner`；节点类型不得泄漏进宿主。  
4. **Parallel 默认不做**（单通道输入易打架）；感知由 Brain 每帧填黑板（弱化 Service）。  
5. **运行真源** = `customRoot` NodeDef 树；`graphLayout` 可丢。  
6. 参考项目仅作**体验与拓扑**蓝本，节点语义以 ACT 现有经典 Selector/Sequence 为准。

---

## 3. 差距矩阵（要补什么）

| 能力 | JL | BD/Reaper | ACT 现状 | 本方案 |
|------|----|-----------|----------|--------|
| 竖向 Graph | ✅ | ✅ | ✅ E3 | 打磨 |
| 类别配色 / USS | ✅ | ✅ | 🟡 标题色块 | **A1** |
| Running 边/节点表现 | 流光 | 窗口高亮 | 标题高亮 | **A1** |
| 左检视布局 | ✅ | ✅ | 右侧窄栏 | **A2** |
| 黑板监视面板 | ❌ | Variables | ❌ | **A3** |
| 自动排版 | Ctrl+E | 有 | Auto Layout | 小打磨 |
| RandomSelector | ❌ | ✅ Demo 核心 | ❌ | **B1** |
| 血量/阶段条件 | ❌ | ✅ | ❌ | **B2** |
| Boss 模板树 | 弱 | ✅ | Melee/Kite | **B3** |
| Abort Self/Lower | ❌ | ✅ | ❌ | **B4** |
| Shared 全量黑板 | ❌ | ✅ | 固定字段 | **C1** 可选 |
| Subtree | ❌ | ✅ | ❌ | **C2** 可选 |
| 寻路 MoveTo | 弱 | NavMesh Task | Straight 占位 | **E4** 另轨 |
| Parallel | ✅ | ✅ | 不做 | 保持不做 |

---

## 4. 分阶段计划

### Phase BT-A — 编辑器体验（优先，低风险）

**目标：** 观感接近 JL/BD，不改运行语义。

| # | 交付 | 验收 |
|---|------|------|
| A1 | **BD 风格**：深色网格 + 类别色标题条 + 左 Tasks 图例 + Properties；Running 绿框（`LastDebugPath`） | ✅ 2026-08-09 自研近似，不复制 BD 资产 |
| A2 | 窗口布局：左（或更宽右）Inspector 常驻；工具栏分区（文件 / 排版 / 模板） | 与 Action Graph 窗口操作习惯接近 |
| A3 | Play 模式只读「黑板快照」面板：距离、仇恨、CD 表、MoveDesire、脉冲旗 | 无需开 Console |
| A4 | Undo：创建/删节点/连线尽量 `RegisterCompleteObjectUndo`；文档标明边界 | Ctrl+Z 覆盖常见编辑 |
| A5 | 根节点视觉标记 + Validate 文案；便签 StickyNote 可在图画（可选） | 单根规则仍强制 Save |

**出口：** 策划/自己不靠猜色也能读树；调试不必只看 Gizmo 字串。

---

### Phase BT-B — 决策拓扑（中优先，可玩性）

**目标：** 学 Reaper 的 Boss 结构，叶节点仍只写输入。

| # | 交付 | 验收 |
|---|------|------|
| B1 | `RandomSelector`（等权或权重列表）+ Def + 调色板 | EditMode：多次 Tick 分布合理 |
| B2 | 条件：`HealthPercentLessEqual`（Brain 填 `HealthNormalized`）；可选 `DistanceBand` | 低血分支可配 |
| B3 | Fill 模板 **CombatPool**（示意）：Aggro 下 RandomSelector →（近战 Pulse / BackOff / Strafe+Wait）+ CooldownGate | 真敌可挂模板验收 |
| B4 | **Abort Self**（装饰条件失败 → Reset 子树并 Failure）；文档后再评估 LowerPriority | 追击 Running 时进攻击距能较快改支 |
| B5 | 预设/文档写清：Death/Hit **仍在 Brain 门闩外**（对齐 Reaper 外层门控，但不进树） | 与 Reaction 单源一致 |

**示例拓扑（CombatPool，非必须一字不差）：**

```text
Root (Selector)
  ├─ Combat (Sequence): Aggro → RandomSelector
  │     ├─ Melee: InAttackRange → CdReady → Stop → PulseAttack
  │     ├─ Kite: Dist≤X → BackOff
  │     └─ Strafe: CooldownGate(dodge) → Strafe → Wait
  └─ Idle: StopMove
```

**禁止：** Task 内 `SetTrigger` / `SetDestination` / `TryStart`。

**出口：** 不靠 Graph 手搓也能一键 Fill 出「有招式池」的近战敌。

---

### Phase BT-C — 运行时扩展（按需）

| # | 交付 | 说明 |
|---|------|------|
| C1 | 可配置只读黑板键（float/bool 表） | 输出槽仍收敛；避免 SharedVariable 全家桶 |
| C2 | Subtree：引用另一 `EnemyBehaviorTreeAsset` 根 | Save 时展开或运行时挂载二选一（定案时写清） |
| C3 | Abort LowerPriority（Selector 内） | 依赖 B4；单测覆盖打断序 |
| C4 | 与 E4 寻路汇合 | `MoveAlongPath` 只写方向欲望 |

---

### Phase BT-D —（明确不做 / 后置）

| 项 | 原因 |
|----|------|
| 引入 BD / JL 运行时 | 锁步边界 + §3.4 |
| Parallel 多 Task 同帧写移动 | 输入冲突 |
| 完整 EQS | 范围过大 |
| Task 热更 DLL（Reaper 路） | 另开工程议题；与当前 Demo 无关 |

---

## 5. 推荐开工顺序

```text
A1 配色 + Running 表现
  → A3 黑板监视（调试效率）
  → B1 RandomSelector + B3 CombatPool 模板   ← 可玩性跃迁
  → B4 Abort Self
  → A2/A4 布局与 Undo 打磨
  → C / E4 按需求插入
```

**最小可感切片：** 只做 **A1 + B1 + B3**，编辑器更好读，真敌立刻有招式池。

---

## 6. 目录预期（增量）

```text
Assets/Scripts/Domain/Enemy/BehaviorTree/
  Nodes/CompositeNodes.cs          // + RandomSelector
  Nodes/ConditionNodes.cs          // + HealthPercent…
  Nodes/DecoratorNodes.cs          // + AbortSelf（或独立文件）
  EnemyBlackboard.cs               // + HealthNormalized 等只读

Assets/Scripts/Editor/Enemy/BehaviorTree/
  Styles/EnemyBehaviorTree.uss     // 可选
  EnemyBehaviorGraphEdgeView.cs    // Running 流光（可选）
  EnemyBehaviorBlackboardPanel.cs  // Play 监视

docs/2026.8.9/ENEMY_BEHAVIOR_TREE_OPTIMIZATION_PLAN.md  // 本文件
```

---

## 7. 风险与对策

| 风险 | 对策 |
|------|------|
| Abort 与每帧根 Tick 语义纠缠 | 先 Self；单测「Running 子 + 条件翻面」 |
| RandomSelector 锁步非确定 | 使用可注入 `IRandom` / 帧种子；测试固定种子 |
| 学 Reaper 却把起招写进 Task | CR 红线；节点基类不暴露 Executor |
| 编辑器动画过度 | A1 流光可关；默认仅加粗/变色 |
| 黑板键膨胀 | C1 白名单；输出槽白名单不变 |

---

## 8. 成功标准

- [ ] Graph 类别一眼可辨；Play 能看见活跃支（节点或边）  
- [ ] Play 能看见关键黑板字段，无需扒日志  
- [ ] 至少 1 份 CombatPool 模板可 Fill，真敌行为有随机分支  
- [ ] Abort Self 有 EditMode 覆盖  
- [ ] 仍无第三方 BT 运行时；Brain 无具体节点类型  
- [ ] 全逻辑帧；无 BT `Update` 权威  

---

## 9. 决策记录

| 日期 | 决策 | 理由 |
|------|------|------|
| 2026-08-09 | 对照 JL/BD 只吸编辑器与拓扑，不吸运行时 | §3.4 + 锁步 |
| 2026-08-09 | 优化分 A 编辑器 → B 决策 → C 扩展 | 风险递增 |
| 2026-08-09 | Parallel 继续不做；Abort 做 Self 子集 | 输入单通道；先补打断刚需 |
| 2026-08-09 | Boss 模板用 RandomSelector + 输入型 Task | 对齐 Reaper 结构、不对齐 Task 副作用 |
| 2026-08-09 | 编辑器视觉对齐 BD（深色/类别色/三栏），不引入 BD 包 | 学样式不学运行时 |

---

## 10. 下一步（待开工）

**A1（BD 样式）已落地。** 建议下一刀：**A3 黑板监视** 或 **B1+B3（RandomSelector + CombatPool）**。
