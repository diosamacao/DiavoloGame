# ActionGraph 连招图设计方案

> 日期：2026-07-25
> 状态：**已实现（稀疏多入口 Graph）**；玩法资产需在 Editor 人工迁移
> 相关：`docs/ACTION_SYSTEM.md`、`docs/ACTION_SYSTEM_REFACTOR_PLAN.md`、`docs/ACTION_EDITOR.md`

---

## 1. 背景与问题

当前选招层已完成 Resolver 拆分：

```text
PlayerActionSet
  → ActionResolverService
    → Single / Combo / Directional ActionResolver
      → ActionDefinition
        → ActionExecutor 播放
```

`ActionDefinition` 只描述单招时间轴；下一招由 `ActionGraph` 决定。旧线性
`ComboActionResolver` 已在稀疏图落地后删除。

### 1.1 配置冗余：CancelWindow × AllowInput

每个 `CancelWindowNotifyState` 都需配置 `allowedInputs`。当一段攻击同时允许「攻击进位」与「闪避取消」时，**每一个** Cancel 窗都要重复挂 Attack + Dodge。多窗、多段连招下配置噪音大、易漏配。

根因：Cancel 窗同时承担了「何时可取消」与「允许什么输入」，而输入语义本应属于**被派生的那一招**。

### 1.2 结构能力：线性序列无法表达环与分支

`ComboActionResolver` 无法自然表达环（`A4→A2`）、同窗多输入分支、以及同一 `ActionDefinition` 在路径上多次出现且出边不同（身份被 SO 引用绑死）。

### 1.3 双窗口双路由：Normal 与 Perfect 派生

> **每个 Action 有一个 Normal CancelWindow，可另配一个独立 Perfect CancelWindow。**

边绑定到 `Normal` 或 `Perfect` 类型；两窗重叠且目标 Trigger 相同时，Perfect 优先。

### 1.4 决策：输入归属 ActionDefinition.Trigger（本方案采纳）

将「用什么输入、何种方式触发本招」从 CancelWindow / 图边挪到 **`ActionDefinition.Trigger`**：

| 招式 | Trigger（示意） |
|------|-----------------|
| `Attack_01` / `Attack_02_*` | Attack + Pressed |
| `Dodge_*` | Dodge + Pressed |
| 未来蓄力斩 | Attack + Held |
| 未来长按闪避 | Dodge + Held |

配置动作树时：

> **每个 Action 必须有一个 Normal、可选一个 Perfect CancelWindow；图上按窗口类型连接目标 Action。**
> 运行时用目标招的 `Trigger` 去匹配输入缓冲。

这与已有 `ActionRequest(InputId, ActionInputTrigger)` / `ActionInputTrigger`（当前仅 `Pressed`，预留 Held/Released）对齐，并消除 Cancel 窗与边上的输入重复配置。

---

## 2. 设计目标

1. 支持连招 **分支** 与 **环**（有向图，而非纯树）。
2. Normal / Perfect CancelWindow 独立配置帧范围，可重叠并派生不同下一招。
3. 可用输入由当前路由通道出边目标的 `Trigger` 集合推导。
4. **`ActionDefinition.Trigger`** 描述本招由何种输入、何种触发类型启动（Pressed / Held / Released…）。
5. 图边 **不携带 inputId**；编辑器连线 =「此窗可接到该 Action」。
6. **保持** 选招拓扑在 Graph；时间轴只提供 Normal / Perfect 时间门。
7. **保持** `ActionExecutor` 薄：同一意图先尝试 Perfect，再尝试 Normal，并交给 Graph Resolver。
8. **必须提供 ActionGraph 编辑器**：节点/顺序组出口固定为 NormalCancelWindow 与 PerfectCancelWindow。

### 2.1 非目标（本阶段不做）

- 把完整 Ability / GAS 式技能树塞进 ActionGraph。
- 自动修改现有 `.asset`（资产迁移由 Unity Editor 人工完成）。
- 在 Graph 编辑器内重做整套单招时间轴（Hitbox/VFX 等仍由 Action Editor 编辑）。

---

## 3. 核心原则

```text
ActionDefinition.Trigger = 本招如何被输入触发（唯一输入配置处）
ActionDefinition.Timeline = 本招如何播放（含 Normal / Perfect CancelWindow）
ActionGraph               = 一张图可含多个 Entry（攻击/闪避…）+ Cancel 边
PlayerActionSet           = 只挂 ActionGraph，不再配 input→Resolver 表
边                        = (fromNode, CancelWindowType) → toNode（Trigger 取自目标）
ActionResolverService     = 调 ActiveGraph.TryResolveStart / TryResolveCancel
节点.VariantResolver      = 可选（Directional 闪避等）
```

命名上可用「连招树」，**数据模型按有向图 + 多入口**实现。

---

## 3.1 稀疏路由（2026-07-25 定稿）

Graph 不再把所有可达关系都展开为线：

| 关系 | 表达方式 |
|------|----------|
| 独特连招、分支、环 | 显式边 `(fromNode, CancelWindowType) → toNode` |
| 多来源共用同路由、同意图、同目标 | `ActionGraphSharedRoute`（显式边未命中时回退） |
| 线性连招 | 顺序组按行自动生成普通 Cancel 边；每行保留独立 In |
| 后摇退出 | Timeline Recovery Phase：`allowMovementCancel` / `allowEntryRestart` |
| 高优硬打断 | Entry + `interruptPriority` |
| 自然收招 | Stop → Locomotion；有效预输入再次走 Entry |
| Directional 六向变体 | 一个逻辑节点 + VariantResolver；变体不改变图游标 |

`CancelType`、Cancel 槽、`ComboActionResolver`、`ComboLeafPolicy` 已删除。移动取消由
Recovery Phase 负责；共享路由不会画成重复连线，Inspector 负责配置和冗余校验。

输入缓冲改为有时效数据，默认 `0.15s`，避免动作早期输入在很久以后自然收招时误触发。

---

## 3.2 多入口起手（攻击 + 闪避同一张图）

```text
ActionGraph
  Node Attack_01   [Entry] Trigger=Attack●
  Node Attack_02           Trigger=Attack●
  Node Dodge_Back  [Entry] Trigger=Dodge●  + VariantResolver=Directional
  Node Dodge_Fwd / Left / Right   （变体落点，便于 Cancel 边）

Locomotion + Attack → 命中 Attack_01 Entry
Locomotion + Dodge  → 命中 Dodge Entry → Directional 选变体 → 游标落到对应 Dodge 节点
```

`PlayerActionSet` 仅引用该 Graph；InputReader 注册的离散输入由 Graph 内所有 Trigger 自动收集。

---

## 4. 数据模型

### 4.1 ActionDefinition.Trigger

在单招资产上配置（与时间轴并列的基础字段）：

```text
ActionDefinition
  trigger :
    input     : InputActionReference   // 解析为 inputId（Attack / Dodge / …）
    kind      : ActionInputTrigger     // Pressed | Held | Released（扩展现有枚举）
  timeline  : … NormalCancelWindow(frames), PerfectCancelWindow?(frames) …
```

约定：

| 项 | 说明 |
|----|------|
| 语义 | 「要进入/派生到本招，需要满足的输入条件」 |
| 与 `ActionRequest` | `Request.InputId` + `Request.Trigger`（kind）应对齐本字段；起手与 Cancel 共用同一套匹配 |
| 扩展 | 长按攻击 = 新 Action + `kind=Held`；不必改 Cancel 窗或边结构 |
| 命名 | 资产字段名 `Trigger`；kind 枚举继续用已有 `ActionInputTrigger`；文档中称 **Trigger.kind** 以免与 `ActionRequest.Trigger` 混淆 |

**每个招式通常一个 Trigger。** 若同一动画既要「点按」又要「长按」进不同逻辑，拆成两个 `ActionDefinition`（或两个图节点引用不同资产），而不是在一个 Action 上挂多个 Trigger。

### 4.2 独立 CancelWindow（时间轴）

```text
CancelWindowNotifyState
  startFrame / endFrame
  windowType : Normal | Perfect
```

- 每个 Action 必须且只能有一个 Normal CancelWindow，最多一个 Perfect CancelWindow。
- 两个窗口可以重叠；同一 Trigger 在重叠帧优先走 Perfect。
- 移动取消由 Recovery Phase 的 `allowMovementCancel` 承担，不再复用 CancelWindow。
- `perfectFrame`、`CancelType`、`CancelSlotId`、`ResolvedCancelWindow` 已删除。

### 4.3 ActionGraph

```text
ActionGraph
  entryNodeId : string
  nodes[] :
    nodeId  : string
    action  : ActionDefinition   // 其 Trigger 参与 Cancel 匹配
  edges[] :
    fromNodeId    : string
    routeKind     : Normal | Perfect
    conditions    : EdgeCondition[]  // 可选：OnHit / OnWhiff …
    to            : NodeRef        // 目标节点；Trigger = to.action.Trigger
```

**边匹配键：**

```text
结构键：  (fromNodeId, routeKind) → toNode
运行匹配：缓冲满足 to.action.Trigger 且 conditions 通过
```

同一 `from` + 同一 `routeKind` 可连出多条边，它们靠**目标 Trigger 不同**区分。
同一路由下两条边指向 Trigger 签名相同的两个目标为非法。

闪避取消 = 连到带 `Trigger=Dodge` 的 Dodge 节点（或见 6.4 的 Directional 再解析）。

### 4.4 GraphActionResolver

```text
GraphActionResolver : ActionResolver
  graph : ActionGraph
```

`PlayerActionSet`：Attack Entry → 本 Resolver（Locomotion 起手进 entry）。  
Cancel 路径：汇总当前帧开放的 Normal / Perfect 窗口，再用各目标 Trigger 反查缓冲。

### 4.5 关系一览

| 概念 | 关系 |
|------|------|
| Action ↔ Trigger | 一对一（本方案） |
| Graph Node ↔ Action | 多对一允许 |
| Cancel 路由 ↔ 出边 | 一对多（多目标招 = 多 Trigger） |
| 运行时位置 | `CurrentNodeId` |
| 运行时取消上下文 | `CancelWindowType` |

---

## 5. 双窗口派生 + Trigger 匹配

### 5.1 编辑器心智模型（目标体验）

```text
Attack_01
  NormalCancelWindow  ──连接──→ Attack_02
  PerfectCancelWindow ──连接──→ BranchPerfect
  Recovery Phase ──隐式 Entry──→ Attack_01（Trigger: Attack，重开；不画边）
```

策划只选「Cancel 通道 → Action」；边标签由目标 `Trigger` 自动显示。

### 5.2 运行时解析链

```text
ActionExecutor.Tick
  → 判断 Normal / Perfect CancelWindow 是否开放
  → 按输入意图优先级枚举；同一意图先尝试 Perfect，再尝试 Normal
  → 枚举 Graph 中 (CurrentNodeId, windowType) 的全部出边
  → 对每条边读取 to.action.Trigger，查询输入缓冲是否满足
       （Pressed：HasBuffer(inputId)；Held：后续接入按住状态等）
  → 若唯一命中（或 conditions 筛后唯一）→ 消费对应缓冲
  → Context { CancelWindowType, CurrentNodeId, … }
  → TransitionTo(to.action) + 游标 = to.nodeId
```

与旧方案差异：不是「先消费某个 allowedInput，再带 inputId 去 Resolver 找下一招」，而是「**先看本窗能接到哪些招，再用这些招的 Trigger 问缓冲**」。

### 5.3 同路由多 Trigger / 冲突

| 情况 | 处理 |
|------|------|
| Normal → Quick(Attack●) + Dodge(Dodge●) | 合法；按输入优先级匹配 |
| Normal → Quick(Attack●) + Finisher(Attack●) | **非法**（同路由同 Trigger） |
| Normal → Quick(Attack●) + Perfect → Finisher(Attack●)，两窗重叠 | 合法；该帧优先 Finisher |
| Attack● 与 Attack●Held 同时满足 | P2 建议 **Held 优先于 Pressed**（或可配置） |

### 5.5 为何比「边上等 inputId」更好

1. Trigger 与招式绑定一次，全图复用，改 Dodge 输入名只改 Action。  
2. 配树时零 Input 选择，降低配错。  
3. 长按等扩展只加 `ActionInputTrigger` + 新 Action，边模型不变。  
4. CancelWindow 只负责类型与时间门，职责干净。

---

## 6. 运行时行为

### 6.1 图游标

`ActionSession`：`CurrentNodeId`（+ 可选 `CurrentGraph`）。  
`ActionResolveContext`：`CurrentNodeId` + `CancelWindowType`。

### 6.2 TryResolve

| Origin | 行为 |
|--------|------|
| `LocomotionStart` | 返回 entry 的 action；游标 = entry（起手仍由 PlayerActionSet 按输入路由到本 GraphResolver） |
| `CancelWindow` | 见 §5.2：按槽出边 + 目标 Trigger 匹配缓冲 |
| 无匹配边 / 缓冲不满足 | 失败，不进位 |

### 6.3 Executor

```text
扫 Normal / Perfect 窗 → 同意图 Perfect 优先 → Trigger 匹配缓冲 → TransitionTo
```

不在 Executor 内写死 Attack/Dodge；不读窗上的 allowedInputs。

### 6.4 方向闪避（Directional）与「连到 Dodge Action」

推荐：

1. 图上只保留一个 Dodge 逻辑节点（`Trigger = Dodge`），并绑定 `DirectionalActionResolver`。
2. 匹配成功后 Resolver 选择前、后、左前、左后、右前、右后实际 Action，但图游标仍停在 Dodge 逻辑节点。
3. 所有方向共享同一套显式边 / SharedRoute；只有拓扑确实不同的方向才拆独立节点。

这样仍满足「配树只选 Action」，又保留现有方向闪避策略，无需在边上再标 Input。

---

## 7. 职责对照（最终）

| 层 | 职责 | 不负责 |
|----|------|--------|
| `ActionDefinition.Trigger` | 本招由什么输入、何种 kind 触发 | 连招下一跳 |
| `CancelWindow` | 何时、以 Normal 或 Perfect 取消 | 允许哪些输入、下一招是谁 |
| `ActionGraph` 边 | 某窗口类型可派生到哪些 Action 节点 | 再写一遍 Input |
| `GraphActionResolver` | 窗口类型 + 缓冲 ↔ 目标 Trigger | 播动画 |
| `PlayerActionSet` | Locomotion 起手：输入 → Resolver | Cancel 窗内的 input 白名单 |

**删除主路径上的 `CancelWindow.allowedInputs`**（按项目无兼容层约定，切 Graph 后不保留双轨）。

---

## 8. ActionGraph 编辑器（硬需求）

### 8.1 目标

1. 拖入 `ActionDefinition` 生成节点（节点上显示其 **Trigger** 徽章）。  
2. 节点输出端口固定为 **NormalCancelWindow / PerfectCancelWindow**。
3. 多选 Action 可合并为顺序组；普通 Cancel 按行自动进入下一 Action。
4. 组内每行 Action 保留独立 In，因此可明确连到 `Branch` 或 `BranchPerfect`。
5. 组级 Normal / Perfect 出口分别展开到所有配置对应窗口类型的子节点。
6. 可直接把 `ActionDefinition` 拖入组末尾，并用上下按钮调整顺序。

### 8.2 界面草图

```text
┌─ ActionGraph Editor ─────────────────────────────────────────┐
│ Graph: Player_Sword_Combo              [Set Entry] [Validate] │
├────────────┬──────────────────────────────────────────────────┤
│ Action 库  │  ┌─ N1 Attack_01 [Attack●] ●Entry ─────┐       │
│ (可拖入)   │  │  ◉ Early  10-20                       │──→ N2_Quick [Attack●]
│            │  │  ◉ Late   28-40                       │──→ N2_Fin [Attack●]
│            │  │  ◉ Early / Late 另线 ──→ N_Dodge [Dodge●]    │
│            │  └──────────────────────────────────────┘       │
│            │  连线时无需选择 Input；冲突 Trigger 标红         │
└────────────┴──────────────────────────────────────────────────┘
```

| 操作 | 行为 |
|------|------|
| 拖入 Action | 建节点；展示 Trigger + Cancel 端口 |
| Normal / Perfect → 节点连线 | 写入对应窗口类型 edge；标签 = 目标 Trigger |
| 多选 Merge Sequence | 按画布顺序生成组；每行独立 In，普通 Cancel 自动进下一行 |
| 拖 Action 到组 | 追加一行；上下按钮调整自动链顺序 |
| 改 Action.Trigger | 全图边标签刷新；同路由冲突重跑校验 |
| 双击节点 | 打开 Action Editor 编时间轴 / Trigger |

### 8.3 校验（最低集）

1. Entry 存在。  
2. 每个 Action 必须且只能有一个 Normal、最多一个 Perfect；边要求来源 Action 存在对应类型窗口。
3. 同一 `(fromNodeId, routeKind)` 下，出边目标 Trigger 唯一。
4. 目标节点存在；目标 Action 已配置合法 Trigger。  
5. 顺序组 Id / 成员唯一，组内 Action 均有 CancelWindow。

### 8.4 分期

当前编辑器支持拖入、双窗口连线、顺序组、多行入口、自动链、存盘与冲突校验。

---

## 9. 配置示例

### 9.1 Perfect 差异派生

```text
Actions:
  Attack_01.Trigger          = Attack Pressed
  Attack_02.Trigger          = Attack Pressed
  BranchPerfect.Trigger      = Attack Pressed

Edges:  // 无 input 字段
  (N1, Normal)  → N2
  (N1, Perfect) → BranchPerfect
  Recovery Phase → Attack Entry（隐式，不保存边）
```

### 9.2 循环 A1→A2→A3→A4→A2…

各 Attack 均为 `Attack Pressed`；合并为顺序组后自动生成 `(Ni, Normal) → N(i+1)`，末段可显式回到 N2。

### 9.3 未来：长按攻击

```text
Charge_Slash.Trigger = Attack Held
(N1, Normal) → N2_Quick          // Attack Pressed
(N1, Normal) → N_Charge          // Attack Held —— 同路由不同 kind，合法
```

---

## 10. 与现有类型对照

| 现有 | 本方案 |
|------|--------|
| `CancelWindow.allowedInputs` | **删除**；由目标 `ActionDefinition.Trigger` 推导 |
| 边上 `inputId`（前一版草案） | **删除**；Trigger 在目标 Action |
| `ActionInputTrigger`（仅 Pressed） | 扩展 Held / Released；挂到 `ActionDefinition.Trigger.kind` |
| `ActionRequest(InputId, Trigger)` | 与 `ActionDefinition.Trigger` 对齐 |
| `ComboActionResolver.steps[]` | 已删除；改为 `ActionGraph` 显式边 + SharedRoute |
| `IndexOfStep(action)` | `CurrentNodeId` + CancelWindowType + Trigger 匹配 |
| 无 Graph 编辑器 | 双窗口类型连 Action；顺序组自动生成 Normal 链 |

---

## 11. 分期落地

| 阶段 | 内容 | 验收 |
|------|------|------|
| **P0** | `ActionDefinition.Trigger`；双窗口 `ActionGraph` + Resolver；Session/Context；Graph 编辑器 | ① 环可玩 ② Normal/Perfect 派生 ③ 编辑器无需选 Input |
| **P1** | 校验器完善；Directional 与 Dodge 节点再解析打通 | 闪避取消方向正确；冲突 Trigger 编辑期报错 |
| **P2** | `ActionInputTrigger.Held/Released` + 缓冲/按住状态；边 conditions | 长按攻击/闪避；挥空分支 |
| **P3** | GraphView 体验、播放中高亮当前槽/边 | 策划日常只维护图 |

### 11.1 建议影响文件（实现时）

**新增：** `ActionGraph`、`GraphActionResolver`、`ActionTrigger`（或嵌在 Definition 内）、`Editor/Combat/ActionGraph/`

**修改：**

- `ActionDefinition` — 增加 `Trigger`
- `ActionInputTrigger` — 扩展 Held / Released（P2）
- `CancelWindowNotifyState` — `CancelWindowType.Normal / Perfect` 独立窗口
- `ActionSession` / `ActionResolveContext` / `ActionExecutor` — 游标、双窗口与同 Trigger Perfect 优先
- 文档与 TECHNICAL 同步

**不改：** 连招拓扑不写进 `ActionDefinition`（只加 Trigger 元数据）；玩法资产由 Editor 人工配置。

---

## 12. 风险与约束

1. **nodeId + routeKind** 是连段身份与普通/Perfect 差异的必要条件。
2. **同路由同 Trigger** 必须校验禁止。
3. **改 Action.Trigger** 会影响所有指向该 Action 的边的匹配语义——属预期；编辑器应提示引用计数。  
4. **Held / Pressed 同时满足** 需在 P2 定优先级。  
5. **Directional Dodge**：图上连的是「带 Dodge Trigger 的节点」，真正左右闪由 Directional Resolver 二次解析（§6.4）。  
6. **无兼容层**：切 Graph 后删除 `allowedInputs` 进位双轨与线性 Combo 主路径假设。

---

## 13. 结论

最终模型：`ActionDefinition.Trigger` 定义输入语义；Normal / Perfect CancelWindow 独立定义时间门；图边按窗口类型连接目标 Action。重叠时同一 Trigger 优先 Perfect；顺序组用 Normal 自动生成链，同时每行独立 In 支持精确进入分支。

---

## 变更日志

| 日期 | 说明 |
|------|------|
| 2026-07-14 | 初稿：问题分析、图模型、Cancel 减负、分期与风险 |
| 2026-07-14 | 补充：多 CancelWindow 差异派生；ActionGraph 编辑器硬需求 |
| 2026-07-14 | **采纳 Trigger 归属 ActionDefinition**：边去掉 inputId；删除 Cancel.allowedInputs 主路径；编辑器仅按窗连 Action；对齐 ActionInputTrigger 扩展 Held/Released |
| 2026-07-14 | **P0 落地**：`ActionTrigger`、`ActionGraph`/`GraphActionResolver`、`ActionResolveResult` 图游标、Cancel 槽=时间轴 Id、Executor 按槽边候选输入、Inspector + GraphView 编辑器；移除 `allowedInputs` |
| 2026-07-26 | **Perfect 独立窗口**：删除 perfectFrame；Normal 必需、Perfect 可选；重叠且同 Trigger 时 Perfect 优先 |
| 2026-07-14 | **多入口**：删除 `GraphActionResolver` 与 `ActionEntry`；`PlayerActionSet` 直接挂 `ActionGraph`；节点 `Is Entry` + Trigger 同时支持攻击/闪避起手；可选 `VariantResolver`（Directional） |
