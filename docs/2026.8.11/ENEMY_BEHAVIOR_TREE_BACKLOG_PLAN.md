# 敌人行为树待优化 — 编辑器体验与可选运行时扩展

> 制定：2026-08-11  
> 角色：**行为树后续优化真源**（结构主线已关闭后的待办排期；先文档，后实现）  
> 契约：[`../ENEMY_BEHAVIOR_TREE_PLAN.md`](../ENEMY_BEHAVIOR_TREE_PLAN.md) §3.4  
> 对峙循环：[`../2026.8.9/LOCOMOTION_GAIT_POLICY_PLAN.md`](../2026.8.9/LOCOMOTION_GAIT_POLICY_PLAN.md)  
> 装配链：`EnemyDefinition → BehaviorTree → Runner → Blackboard → Brain 提交 Desire/Request`

---

## 0. 一句话

在**不改 AI 命令轨终态**（Desire + ActionEntryRequest）的前提下，把 GraphView 编修/调试体验补齐，并按需做 Subtree / 黑板只读键 / Abort LowerPriority / 寻路汇合；**禁止**引入第三方 BT 运行时、Parallel 多写移动、或恢复敌人假手柄。

---

## 1. 问题与动机

### 1.1 现状基线（2026-08-11）

```text
SimulationWorld.ProduceInput
  → EnemyBrain.Step（门闩 / 填黑板 / Runner.Tick / 提交 Desire + Entry Request）
  → CharacterActor.Step（IMoveIntentSource / IActionEntryRequestSource）

Editor：EnemyBehaviorTreeEditorWindow GraphView MVP
  → A1 深色网格 / 类别色 / Running 高亮 ✅
  → A2～A5 布局 / 黑板监视 / Undo / 根标记 未齐
运行时：Abort Self ✅；RandomSelector / CooldownGate(秒) / CdReady|NotReady ✅
可选：Subtree / 可配黑板键 / Abort LowerPriority / MoveAlongPath 未做
```

| 点 | 现状 |
|----|------|
| 结构主线 8.10 | ✅ 总出口关闭 |
| 对峙 CD 循环 + 节点秒制 | ✅ 代码落地；用户已验收表现 |
| Graph 编辑 | 可用；右栏窄、无 Play 黑板面板、Undo 边界弱 |
| 调试 | 多靠 Gizmo / `LastDebugPath` / Console |
| 大型 Boss 树 | 缺 Subtree 复用；缺 Selector 下级打断 |

### 1.2 痛点

1. Play 调试仍要扒日志才能看距离 / CD / Desire / Request。  
2. 窗口布局与 Action Graph 习惯不完全一致，长时编树效率低。  
3. Undo 与根标记不足时，误操作成本高。  
4. 多怪复用子树、高优先级打断、寻路欲望尚未有正式阶段。

### 1.3 目标

| 目标 | 说明 |
|------|------|
| 编辑器 | A2～A5 补齐后，编树/调试不必依赖 Console |
| 扩展 | C1～C4 按需开工，每阶段可独立合并 |
| 不做 | 第三方 BT 运行时；Parallel 多移动；改 8.10 命令槽语义；Agent 改 `.asset`/Prefab |

---

## 2. 设计原则

1. **结构主线已冻结**：本文不重开 Desire/Request / 假手柄议题。  
2. **编辑器不改运行语义**：A 阶段只动 `Editor/Enemy/BehaviorTree`。  
3. **Task 只写黑板输出槽**；禁止节点 `TryStart` / 改 Numeric / 直驱 Animator 权威。  
4. **Brain 只认** `IEnemyBehaviorRunner`；扩展不得泄漏节点类型进宿主。  
5. **锁步边界不变**：决策仍在 ProduceInput；移动/出招经 Desire/Request。  
6. **零长期兼容**：新能力直接替换旧调试路径，不保留双面板双 Undo。  
7. **Parallel 继续不做**；Abort 先 Self（已有），再按需 LowerPriority。

---

## 3. 目标架构

```text
【编辑器】
EnemyBehaviorTreeEditorWindow
  ├─ 工具栏（文件 / 排版 / 模板）
  ├─ Tasks 调色板
  ├─ GraphView（宿主牌 + 条件徽章 + Running）
  ├─ Inspector（常驻、可调宽）
  └─ Play：BlackboardSnapshotPanel（只读）

【运行时扩展（可选）】
EnemyBlackboard
  ├─ 固定输出槽：MoveDesire / CombatRequest / Face…
  └─ 可选只读键表（C1，白名单）
SubtreeAsset → CreateRunner 挂载或 Save 展开（C2 定一种）
Selector + Abort LowerPriority（C3）
PathQuery → MoveAlongPath → Desire（C4，与 A* Demo 汇合）
```

### 3.1 关键契约

```text
编辑器输入：EnemyBehaviorTreeAsset.customRoot + graphLayout
编辑器输出：Save → SerializeReference 树；Validate 错误阻断脏拓扑
Play 监视：只读黑板快照（不写权威）

运行时扩展输入：同现有 Perception / Cooldown / PathQuery
运行时扩展输出：仍只进 LocomotionDesireBuffer / ActionEntryRequestBuffer
```

### 3.2 边界

| 层 | 职责 | 不负责 |
|----|------|--------|
| Editor A* | 布局、监视、Undo、根标记 | 改 Brain / Driver |
| C1 黑板键 | 条件可读的策划参数表 | SharedVariable 全家桶 / 输出槽分叉 |
| C2 Subtree | 复用子树 | 跨资产改命令轨 |
| C3 Abort LP | Selector 内高优打断 | Parallel |
| C4 寻路 | 路径方向进 Desire | NavMesh 直驱 Transform |

---

## 4. 分阶段交付（任务 / 验收 / 出口）

> 勾选：未开始 `[ ]`；完成后 `[x]` 并在出口注明日期。  
> 编号延续历史：`BT-A*` 编辑器、`BT-C*` 运行时扩展。

---

### BT-A2 — 窗口布局与工具栏

**任务**

- [ ] Inspector 常驻且可调宽（左栏或加宽右栏，与 Action Graph 操作习惯接近）
- [ ] 工具栏分区：文件（Open/Save/Validate）/ 排版（Auto Layout）/ 模板
- [ ] 文档截图级说明（本文件 Editor 步骤）

**验收**

- [ ] 编树时无需反复点选才能改节点参数
- [ ] 工具栏分区可一眼区分文件操作与排版

**出口：** 长时编树布局可用。→ **未达成**

---

### BT-A3 — Play 黑板快照面板（建议先做）

**任务**

- [ ] `EnemyBehaviorBlackboardPanel`（Editor）：Play 只读显示距离、仇恨、`AttackConfirmPending`、CD 剩余（含 `basic_attack` / `action_entry_retry`）、`LocomotionDesire`、Pending Entry Request、`LastDebugPath`
- [ ] 绑定当前选中/调试中的 `EnemyController` / `EnemyBrain`（定一种选取规则）
- [ ] 非 Play 显示占位提示

**验收**

- [ ] Play 中可不看 Console 判断 CD 中对峙 / CD 毕追击 / Request 是否提交
- [ ] 面板只读，无写入权威路径

**出口：** 调试主路径 = 面板 + Graph Running 高亮。→ **未达成**

---

### BT-A4 — Undo 边界

**任务**

- [ ] 创建/删节点、改连线、改 Inspector 字段走 `RegisterCompleteObjectUndo`（或项目既有 Undo 惯例）
- [ ] 文档标明：哪些操作不可 Undo（若有）

**验收**

- [ ] Ctrl+Z 能撤销常见「加节点 / 删节点 / 改参数」
- [ ] 无「Undo 后资产损坏 / Guid 丢失」

**出口：** 误操作可恢复。→ **未达成**

---

### BT-A5 — 根标记 / Validate 文案 / 便签（可选）

**任务**

- [ ] 根节点视觉标记（单根强制）
- [ ] Validate 错误文案可定位到节点路径（含 Wait/Locomotion 拓扑）
- [ ] （可选）StickyNote 画布便签

**验收**

- [ ] 无根 / 多根意图在 Save/Validate 时可读
- [ ] StickyNote 不进入运行时 Build（若做）

**出口：** 编树规则自解释。→ **未达成**

---

### BT-C1 — 可配置只读黑板键（按需）

**任务**

- [ ] 白名单 float/bool 键表（资产或 Profile 侧）；Brain/Perception 填客观量
- [ ] 条件节点可读键；**禁止** Task 把输出槽改成任意 SharedVariable
- [ ] EditMode：未知键 / 类型错误失败可测

**验收**

- [ ] 新条件可不改 C# 枚举即可读策划参数（在白名单内）
- [ ] `rg`：无第三套输出槽旁路 Desire/Request

**出口：** 参数扩展不破坏命令轨。→ **未达成**

---

### BT-C2 — Subtree（按需）

**任务**

- [ ] 定案**只留一种**：Save 时展开嵌入 **或** 运行时挂载另一 `EnemyBehaviorTreeAsset` 根
- [ ] Graph 可选中「Subtree」节点引用资产
- [ ] Validate：环引用 / 空引用报错

**验收**

- [ ] 两棵树复用同一攻击子树，改一处两边生效（按定案语义）
- [ ] 环引用被 Validator 拒绝

**出口：** Boss/杂兵可复用子树。→ **未达成**

---

### BT-C3 — Abort LowerPriority（按需）

**任务**

- [ ] Selector 内高优先级支成功/运行时，打断低优先级 Running（依赖已有 Abort Self）
- [ ] EditMode：打断序单测
- [ ] 文档约定：哪些 Composite 支持

**验收**

- [ ] 单测：高优 Attack 可打断低优 Strafe Running
- [ ] 不引入 Parallel

**出口：** 高优支可抢占低优 Running。→ **未达成**

---

### BT-C4 — 寻路汇合（依赖 A* Demo / E4）

**任务**

- [ ] `MoveAlongPath`（或等价 Task）只写路径方向欲望 → Brain 组装 `LocomotionDesire`
- [ ] `IEnemyPathQuery` 可替换实现；Straight 仍为默认
- [ ] 明确锁步：路径查询确定性或「仅非 Hash」边界写进 TECHNICAL

**验收**

- [ ] Demo：绕障方向进 Desire，敌人仍无 InputFrame 移动权威
- [ ] 木桩 / 无 PathQuery 时回退直线

**出口：** 寻路与命令轨正交汇合。→ **未达成**

---

## 5. 迁移与兼容

### 5.1 从旧文档迁入

| 旧出处 | 迁入 |
|--------|------|
| OPT Phase A2～A5 | BT-A2～A5 |
| OPT Phase C1～C4 | BT-C1～C4 |
| OPT「下一步 A3」 | 本文推荐开工顺序 |

### 5.2 明确不在本文

| 项 | 归属 |
|----|------|
| Desire / Entry Request / 删 AIInputWriter | 8.10（已关闭） |
| RandomSelector / CombatPool / 秒制 CD / CdNotReady 对峙循环 | 已落地 |
| 引入 BD/JL 运行时、Parallel、完整 EQS、Pulse 招式池 | 永久不做 / 另题 |

### 5.3 文档角色

- 未完成编辑器项以**本文为准**。  
- Agent **不直接改** `Assets/Data/**`、Prefab。

---

## 6. 风险与对策

| 风险 | 对策 |
|------|------|
| 黑板面板误写权威 | 只读 API；无 Set 按钮 |
| Undo 半套导致资产坏 | 整资产 Undo；单测/手测 Save 往返 |
| Subtree 双语义 | C2 开工前书面定案只留一种 |
| Abort LP 抖动 | 单测 + 与 WaitWhileInAction 拓扑回归 |
| 寻路非确定进 Hash | C4 验收强制写清边界 |

---

## 7. Editor 人工步骤（实现阶段）

1. **A3**：Play 选中敌人，确认面板字段随 CD/移动变化。  
2. **A2/A4**：长时编一棵真敌树，验证布局与 Ctrl+Z。  
3. **C\***：仅在有明确 Boss/寻路需求时开工；完成后补 TECHNICAL 一行。  
4. 不要求为本文改真敌战斗数值资产。

---

## 8. 推荐开工顺序

```text
BT-A3（黑板监视）          ← 最小可感
  → BT-A2 / BT-A4（布局 + Undo）
  → BT-A5（根标记 / 文案）
  →（按需）BT-C1 黑板键 → BT-C2 Subtree → BT-C3 Abort LP
  →（依赖 A*）BT-C4 寻路汇合
```

**最小可感切片：** **BT-A3**。

---

## 9. 成功标准（本文总出口）

同时满足可关闭本文：

1. [ ] BT-A2～A4 已达成（A5 可选）  
2. [ ] Play 调试不以 Console 为唯一手段  
3. [ ] 若做了任一 C\*：对应验收勾满，且 Desire/Request 仍为唯一 AI 输出  
4. [ ] 仍无第三方 BT 运行时；无 Parallel 多移动  

---

## 10. 变更日志

| 日期 | 说明 |
|------|------|
| 2026-08-11 | 初版：从 OPT A2～A5 / C1～C4 迁出；结构主线与对峙表现验收后另立待优化真源 |
| 2026-08-11 | 索引收口：README / PROJECT_CHECKLIST / ROADMAP / 契约文「下一步」改指向本文；8.9 OPT 降为 A1 历史 |
