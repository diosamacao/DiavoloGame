# 敌人行为树优化方案（对照 JL / Behavior Designer）

> 制定：2026-08-09  
> 修订：2026-08-10 — **降级角色**：本文仅保留 **Phase A 编辑器体验**；招式池 / 命令轨 / 滞回 / 配置归属以 [ENEMY_BT_DISCRETE_COMBAT_AND_CONFIG_PLAN.md](../2026.8.10/ENEMY_BT_DISCRETE_COMBAT_AND_CONFIG_PLAN.md) 为准  
> 修订：2026-08-11 — **再降级为历史记录**：未完成的 A2～A5 / Phase C 已迁至 [../2026.8.11/ENEMY_BEHAVIOR_TREE_BACKLOG_PLAN.md](../2026.8.11/ENEMY_BEHAVIOR_TREE_BACKLOG_PLAN.md)；本文仅保留 A1 完成记录与 JL/BD 对照  
> 角色：**历史对照**（编辑器待办真源见 2026.8.11 Backlog）  

> 参考项目（只读对照，**不引入为运行时依赖**）：  
> - `D:\Projects\jlbehavior-tree`（JLBehaviourTree：自制 GraphView）  
> - `D:\Projects\BehaviorTreeDemoReaper`（Behavior Designer 1.6.6 + Reaper Boss）  
> 契约真源：[ENEMY_BEHAVIOR_TREE_PLAN.md](../ENEMY_BEHAVIOR_TREE_PLAN.md) §3.4（输出槽终态 = Desire + Request）  
> 演进前置：[ENEMY_BEHAVIOR_TREE_EVOLUTION_PLAN.md](./ENEMY_BEHAVIOR_TREE_EVOLUTION_PLAN.md)（E1～E3 已关闭）  
> **结构真源：** [../2026.8.10/ENEMY_BT_DISCRETE_COMBAT_AND_CONFIG_PLAN.md](../2026.8.10/ENEMY_BT_DISCRETE_COMBAT_AND_CONFIG_PLAN.md)

---

## 0. 一句话

在**不引入第三方 BT 运行时**的前提下：学 **JL 的编辑器壳与调试观感**，把自研 GraphView 打磨到可读可调；**Boss 招式池 / 离散出招 / 移动命令轨**不再由本文 Phase B 推进，已并入 8.10 方案。

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

**可吸收（编辑器）：** Variables 监视习惯、类别色、运行高亮。  
**招式池拓扑：** 结构灵感并入 **8.10 E-REQ**（叶节点为 `RequestCombatAction`，**不是** `PulseAttack`）。  
**不吸收：** BD 运行时、Task 起招、Update 权威时钟。

### 1.3 ACTGame 现状（基线）

```text
SimulationWorld.ProduceInput
  → EnemyBrain.Step（门闩 / 填黑板 / Runner.Tick / 提交 — 现状仍为 AIInputWriter）
  → CharacterActor.Step
```

| 层 | 状态 |
|----|------|
| Runner 契约 / Brain / GraphView MVP | ✅ |
| Task / 冷却 / Kite | ✅ E1 |
| Abort Self（条件装饰） | ✅ |
| 编辑器 A1 BD 风格 | ✅ 2026-08-09 |
| A2～A5 编辑器打磨 | 部分待做 |
| 命令轨 / 离散招 / 滞回 / 配置上树 | → **8.10**（未开始） |
| 寻路 | ⬜ E4 |

---

## 2. 整合原则（硬约束）

1. **运行时不安装** Behavior Designer / JL / NodeCanvas 等包。  
2. **Task 只写黑板输出槽**；禁止 `TryStart` / 改 Numeric / 驱动 Animator 权威。  
   - 输出槽**终态** = `LocomotionDesire` + `CombatRequest`（见 8.10 / BT PLAN §3.4.2）。  
   - **禁止**再以「叶节点只写 InputFrame Pulse」作为新功能验收标准。  
3. **`EnemyBrain` 只认** `IEnemyBehaviorRunner`；节点类型不得泄漏进宿主。  
4. **Parallel 默认不做**（多写移动易打架）。  
5. **运行真源** = `customRoot` NodeDef 树；`graphLayout` 可丢。  
6. 参考项目仅作**体验与拓扑**蓝本；节点语义以 ACT 经典 Selector/Sequence 为准。

---

## 3. 差距矩阵（要补什么）

| 能力 | JL | BD/Reaper | ACT 现状 | 归属 |
|------|----|-----------|----------|------|
| 竖向 Graph | ✅ | ✅ | ✅ E3 | 打磨 |
| 类别配色 / USS | ✅ | ✅ | ✅ A1 | 本文 A |
| Running 边/节点表现 | 流光 | 窗口高亮 | ✅ A1 | 本文 A |
| 左检视布局 | ✅ | ✅ | 右侧窄栏 | **A2** |
| 黑板监视面板 | ❌ | Variables | ❌ | **A3** |
| 自动排版 | Ctrl+E | 有 | Auto Layout | 小打磨 |
| RandomSelector / CombatPool | ❌ | ✅ | ❌ | **→ 8.10 E-REQ2**（非本文） |
| 血量条件 / DistanceBand | ❌ | ✅ | ❌ | **→ 8.10**（B2 阈值在节点；滞回 E-ST） |
| Abort Self | ❌ | ✅ | ✅ | 已完成 |
| Abort LowerPriority | ❌ | ✅ | ❌ | **C3** 可选 |
| Subtree / 可配黑板键 | ❌ | ✅ | ❌ | **C** 可选 |
| 寻路 MoveTo | 弱 | NavMesh | Straight | **E4** 另轨 |
| Parallel | ✅ | ✅ | 不做 | 保持不做 |

---

## 4. 分阶段计划

### Phase BT-A — 编辑器体验（本文主责）

**目标：** 观感接近 JL/BD，不改运行语义。

| # | 交付 | 验收 |
|---|------|------|
| A1 | **BD 风格**：深色网格 + 类别色标题条 + 左 Tasks 图例 + Properties；Running 绿框（`LastDebugPath`） | ✅ 2026-08-09 自研近似，不复制 BD 资产 |
| A2～A5 | **已迁出** → [`../2026.8.11/ENEMY_BEHAVIOR_TREE_BACKLOG_PLAN.md`](../2026.8.11/ENEMY_BEHAVIOR_TREE_BACKLOG_PLAN.md) BT-A2～A5 | 见新文档 |

**出口（本文）：** A1 已达成。后续编辑器出口以 8.11 Backlog 为准。

---

### Phase BT-B — 决策拓扑（**已并入 8.10，本文不再推进**）

| # | 原交付 | 新归属 |
|---|--------|--------|
| B1 | `RandomSelector` | **8.10 E-REQ2** ✅ 代码 2026-08-10 |
| B2 | `HealthPercent` / DistanceBand | **8.10**（客观量 Brain 填；阈值/滞回在节点） |
| B3 | CombatPool 模板 | **8.10 E-REQ2** ✅ `CreateCombatPool`（叶 = `RequestCombatAction`） |
| B4 | Abort Self | ✅ 已完成（条件装饰） |
| B5 | Death/Hit 门外 | 文档约定仍有效 |

~~旧示例（作废，勿实现）：~~ `Stop → PulseAttack` 的 CombatPool。

---

### Phase BT-C — 运行时扩展（**已迁出**）

全部迁至 [`../2026.8.11/ENEMY_BEHAVIOR_TREE_BACKLOG_PLAN.md`](../2026.8.11/ENEMY_BEHAVIOR_TREE_BACKLOG_PLAN.md) **BT-C1～C4**。本文不再更新勾选。

---

### Phase BT-D —（明确不做 / 后置）

| 项 | 原因 |
|----|------|
| 引入 BD / JL 运行时 | 锁步边界 + §3.4 |
| Parallel 多 Task 同帧写移动 | 命令冲突 |
| 完整 EQS | 范围过大 |
| Task 热更 DLL（Reaper 路） | 另开工程议题 |
| Pulse 版 CombatPool | 与 8.10 终态冲突 |

---

## 5. 推荐开工顺序

结构主线与对峙表现已关闭。后续见：

```text
→ docs/2026.8.11/ENEMY_BEHAVIOR_TREE_BACKLOG_PLAN.md
   BT-A3 → A2/A4 → A5 →（按需）C1～C4
```

---

## 6. 目录预期（增量，仅编辑器）

```text
Assets/Scripts/Editor/Enemy/BehaviorTree/
  Styles/EnemyBehaviorTree.uss     // 可选
  EnemyBehaviorGraphEdgeView.cs    // Running 流光（可选）
  EnemyBehaviorBlackboardPanel.cs  // Play 监视（A3）

docs/2026.8.9/ENEMY_BEHAVIOR_TREE_OPTIMIZATION_PLAN.md  // 本文件
```

RandomSelector / Request 节点目录增量见 **8.10 §7**。  
**编辑体验（2026-08-10）：** `RequestCombatAction` Entry 下拉；ActionGraph 唯一真源 = `EnemyDefinition → CombatProfile`（只读反查，BT 不重复绑 Graph）。

---

## 7. 风险与对策

| 风险 | 对策 |
|------|------|
| 编辑器动画过度 | A1 流光可关；默认仅加粗/变色 |
| 黑板键膨胀 | C1 白名单；输出槽以 §3.4.2 为准 |
| 误按旧 B3 做 Pulse 池 | CR / 文档：以 8.10 E-REQ2 为准 |

---

## 8. 成功标准（本文范围）

- [x] Graph 类别一眼可辨；Play 能看见活跃支（A1）  
- [x] Abort Self 已有运行语义（条件装饰）  
- [x] 仍无第三方 BT 运行时；Brain 无具体节点类型  
- [x] A2～A5 / C\* 待办已迁至 8.11 Backlog（2026-08-11）  
- [x] 招式池 / 命令轨 — 由 8.10 总出口关闭  

---

## 9. 决策记录

| 日期 | 决策 | 理由 |
|------|------|------|
| 2026-08-09 | 对照 JL/BD 只吸编辑器与拓扑，不吸运行时 | §3.4 + 锁步 |
| 2026-08-09 | 优化分 A 编辑器 → B 决策 → C 扩展 | 风险递增（B 后被 8.10 吸收） |
| 2026-08-09 | Parallel 继续不做；Abort 做 Self 子集 | 先补打断刚需 |
| 2026-08-09 | 编辑器视觉对齐 BD，不引入 BD 包 | 学样式不学运行时 |
| 2026-08-10 | 本文降级为编辑器真源；B1/B3 → 8.10 E-REQ | 离散出招 + 废除假手柄 |
| 2026-08-11 | A2～A5 / C 迁至 8.11 Backlog；本文仅 A1 历史 | 对峙表现已验收，待办另册 |

---

## 10. 下一步（待开工）

**待优化真源：** [../2026.8.11/ENEMY_BEHAVIOR_TREE_BACKLOG_PLAN.md](../2026.8.11/ENEMY_BEHAVIOR_TREE_BACKLOG_PLAN.md)（建议 **BT-A3 黑板监视**）。
