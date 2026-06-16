# ACTGame — 动作编辑器（Action Editor）设计文档

> 本文档描述 ACTGame 长期目标：**用可视化编辑器为角色配置战斗动作**，而非在代码或 Animator 里硬编码每一招。  
> 最后更新：2026-06-11

---

## 1. 愿景与定位

### 1.1 要解决什么问题

传统 ACT / ARPG 战斗开发常见痛点：

| 痛点 | 表现 |
|------|------|
| 帧数据分散 | Hitbox 开闭、无敌帧、可取消窗口写在 Animation Event 或代码里，难以总览 |
| 调参成本高 | 改一帧判定需要重新进 Play Mode 反复试 |
| 角色复用差 | 换角色 / 换动画就要复制粘贴大量配置 |
| 策划不可参与 | 战斗策划无法独立迭代连招、技能窗口 |

**动作编辑器** 的目标：在 Unity Editor 内，以 **时间轴 + 可视化** 的方式，为每个战斗动作配置完整运行时数据，并能在编辑态 **预览 / 调试**。

### 1.2 设计原则

1. **数据驱动** — 运行时只读 `ActionDefinition`，不写死招式逻辑
2. **帧精确** — 以动画帧（或 normalized time）为最小编辑单位
3. **编辑态可预览** — Scene 视图回放 Hitbox、位移、事件
4. **渐进式落地** — Demo 先用 SO + 手动填表，再逐步替换为编辑器 UI
5. **角色无关** — 同一套动作数据格式可用于玩家、敌人、Boss

### 1.3 参考方向（非照搬）

| 产品 / 工具 | 可借鉴点 |
|-------------|----------|
| 格斗游戏 Frame Data | startup / active / recovery 分段 |
| Unreal Montage + Notify | 动画时间轴上挂事件 |
| Unity Timeline | 轨道式编辑、预览播放 |
| Odin Inspector / 自定义 Editor | 复杂 SO 的友好编辑界面 |
| 部分国产 ACT 内部工具 | 连招图、Hitbox 逐帧预览 |

---

## 2. 核心概念模型

```
CharacterCombatProfile          # 角色战斗配置（引用动作库）
    └── ActionGraph               # 可选：连招 / 状态转移图
            └── ActionNode        # 节点 = 一个 ActionDefinition + 转移条件
ActionDefinition                # 单个战斗动作（核心资产）
    ├── AnimationClip 引用
    ├── ActionPhase[]           # 阶段：Startup / Active / Recovery / ...
    ├── ActionEvent[]           # 时间轴事件
    ├── HitboxKeyframe[]        # 逐帧 / 区间 Hitbox
    ├── MovementCurve           # 位移 / Root Motion 覆盖
    ├── CancelWindow[]          # 可取消到其他动作的窗口
    └── ActionTransition[]      # 连段 / 分支条件
ActionRuntimeController         # 运行时执行器（Player / Enemy 共用）
    └── 读取 ActionDefinition，驱动 Animator + Combat
```

### 2.1 ActionDefinition（动作定义）

单个可执行战斗动作的最小完整单元。示例：`Attack1`、`Evade`、`Sp_Skill1`。

**基础字段：**

| 字段 | 说明 |
|------|------|
| `id` | 唯一标识，如 `player_attack_1` |
| `displayName` | 显示名 |
| `animationClip` | 绑定的 AnimationClip |
| `sampleRate` | 采样率（默认 30 或 60 fps，与动画 import 一致） |
| `totalFrames` | 总帧数（可由 clip 自动计算） |
| `actionType` | Attack / Dodge / Skill / Hit / Death / Locomotion ... |
| `tags` | 如 `light_attack`, `invincible`, `guard_break` |

### 2.2 ActionPhase（动作阶段）

将一条动作按战斗语义分段，便于策划理解：

| 阶段 | 常见含义 | 典型配置 |
|------|----------|----------|
| Startup | 前摇 | 不可命中、可被打断 |
| Active | 有效帧 | Hitbox 开启、产生伤害 |
| Recovery | 后摇 | Hitbox 关闭、硬直 |
| Invincible | 无敌 | I-Frame 标记 |
| SuperArmor | 霸体 | 受击不硬直 |
| Cancel | 可取消 | 允许转入其他动作 |

每阶段用 `[startFrame, endFrame]` 表示。

### 2.3 ActionEvent（时间轴事件）

在指定帧触发的逻辑，**不在代码里散写 Animation Event**。

| 事件类型 | 说明 |
|----------|------|
| `SpawnHitbox` / `DisableHitbox` | 开启 / 关闭指定 Hitbox |
| `PlayVFX` | 播放特效 |
| `PlaySFX` | 播放音效 |
| `ApplyImpulse` | 位移 / 击退 |
| `CameraShake` | 镜头震动 |
| `HitStop` | 命中顿帧 |
| `ChangePhase` | 标记阶段切换（可选，也可由 Phase 推导） |
| `Custom` | 扩展钩子（字符串 key + 参数） |

### 2.4 HitboxKeyframe（判定框关键帧）

| 字段 | 说明 |
|------|------|
| `hitboxId` | 如 `weapon_blade`、`kick` |
| `startFrame` / `endFrame` | 生效区间 |
| `shape` | Box / Capsule / Sphere |
| `localOffset` / `localRotation` / `size` | 相对骨骼或挂点的局部变换 |
| `attachBone` | 可选骨骼名（如 `Hand_R`） |
| `damageMultiplier` | 该段伤害倍率 |
| `hitEffect` | 命中特效 / 音效引用 |

编辑器内应在 Scene 视图 **逐帧 scrub** 显示 Gizmo。

### 2.5 CancelWindow（取消窗口）

| 字段 | 说明 |
|------|------|
| `startFrame` / `endFrame` | 窗口范围 |
| `allowedInputs` | 如 Attack、Dodge、Skill1 |
| `targetActionId` | 取消后进入的动作；空则按 ActionGraph 默认规则 |
| `priority` | 多窗口重叠时的优先级 |

### 2.6 ActionGraph（连招 / 状态图，可选）

用于描述 **动作之间的转移**，而非在 `PlayerStateMachine` 里写死 if-else。

```
[Idle] --Attack输入--> [Attack1] --窗口内Attack--> [Attack2] --窗口内Attack--> [Attack3]
[Attack*] --Dodge输入--> [Evade]
[Any] --受击--> [Hit] --恢复--> [Idle]
```

节点 = `ActionDefinition`，边 = 输入 / 条件 / 自动连段。

---

## 3. 编辑器功能规划

### 3.1 界面布局（目标形态）

```
┌─────────────────────────────────────────────────────────────────┐
│ Action Editor — player_attack_1                          [Preview]│
├──────────────┬──────────────────────────────────────────────────┤
│ Action List  │  Animation Preview (Scene / Game View)           │
│ ├ Attack1    │  [◀ ◼ ▶]  Frame: 12 / 45    [☑ Hitbox] [☑ Root]│
│ ├ Attack2    ├──────────────────────────────────────────────────┤
│ ├ Evade      │  Timeline Tracks                                 │
│ └ Sp_Skill1  │  ├ Phases      [===Startup==|==Active==|Rec=]   │
│              │  ├ Hitboxes    [----[HB1]-------[HB2]----------] │
│ Character:   │  ├ Events      |*VFX    *SFX        *Shake|      │
│ Katana Girl  │  ├ Cancels     [---Dodge---][--Attack--]         │
│              │  └ Invincible  [=========]                       │
├──────────────┴──────────────────────────────────────────────────┤
│ Inspector — 选中帧 / 事件 / Hitbox 的属性编辑                      │
└─────────────────────────────────────────────────────────────────┘
```

### 3.2 功能分期

#### Phase A — 数据层（无自定义 UI，M2–M3 并行）

- [ ] 定义 `ActionDefinition`、`ActionPhase`、`ActionEvent` 等 Serializable 类型
- [ ] ScriptableObject 资产创建菜单
- [ ] 运行时 `ActionRuntimeController` 读取 SO 执行
- [ ] 用 Inspector 手动填 Attack1–3、Evade 数据验证格式

#### Phase B — 基础 Editor 窗口（M5）

- [ ] `ActionEditorWindow`：动作列表 + 选中动作 Inspector 增强
- [ ] 绑定 AnimationClip，自动计算帧数
- [ ] Phase / Event 列表的增删改
- [ ] 从 Animation Clip 导入已有 Animation Events（迁移辅助）

#### Phase C — 时间轴与预览（M6）

- [ ] 帧 scrubber + 播放控制
- [ ] Scene 视图 Hitbox Gizmo 预览（随帧变化）
- [ ] 编辑态动画采样（`AnimationMode` / `PreviewRenderUtility`）
- [ ] Phase / Hitbox / Cancel 轨道可视化

#### Phase D — 连招图与批量工具（M7）

- [ ] `ActionGraph` 节点图编辑器（可用 Unity GraphView）
- [ ] 复制动作模板、批量导出 / 导入 JSON
- [ ] 与 Animator Controller 参数映射表
- [ ] 校验工具：未闭合 Hitbox、空 Active 帧、孤立节点

#### Phase E — 运行时调试（M7+）

- [ ] Play Mode 下 Overlay 显示当前帧、阶段、Hitbox
- [ ] 录制一次战斗回放帧数据（调试用）
- [ ] 与 `ActionDefinition` diff 对比

---

## 4. 运行时架构

### 4.1 执行流程

```
输入 / AI 决策
    → ActionGraph 解析下一动作 id
    → ActionRuntimeController.Play(actionId)
        → 加载 ActionDefinition
        → Animator.CrossFade / Play(clip)
        → 每帧 UpdateFrame(frameIndex)
            → 检查 Phase 变化
            → 触发 ActionEvent
            → 更新 Hitbox 碰撞体
            → 检查 CancelWindow + 缓冲输入
    → 动作结束 → 回到 Locomotion 或 Graph 默认节点
```

### 4.2 与现有模块的关系

| 模块 | 关系 |
|------|------|
| PlayerStateMachine | 薄层：只负责 Locomotion / 受击 / 死亡等 **非招式** 状态；招式交给 ActionRuntime |
| Combat | 消费 Hitbox 帧数据，执行伤害、HitStop、击退 |
| Input | 输入写入 Buffer；ActionRuntime 在 CancelWindow 内消费 |
| Enemy AI | AI 决策输出 `actionId`，同一套 ActionRuntime 执行 |
| Animator | 仅负责播放 clip；参数由 ActionRuntime 驱动（可选） |

### 4.3 Demo 阶段的折中

Demo（M1–M4）不等待编辑器，但 **数据结构按 ActionDefinition 设计**：

- 手写 5–8 个 Action SO（Attack1–3、Evade、Skill1、EnemyAttack）
- Hitbox 可用简化版：每动作 1 个固定区间，而非逐帧
- 连段先用代码或简单 `nextActionId` 字段，M5 再迁到 ActionGraph

这样 Demo 可玩，且后续编辑器有明确迁移路径。

---

## 5. 目录规划

```
Assets/_Game/
├── Scripts/
│   ├── Combat/
│   │   ├── Actions/                    # 运行时
│   │   │   ├── ActionDefinition.cs     # SO 定义
│   │   │   ├── ActionPhase.cs
│   │   │   ├── ActionEvent.cs
│   │   │   ├── HitboxKeyframe.cs
│   │   │   ├── CancelWindow.cs
│   │   │   ├── ActionGraph.cs
│   │   │   └── ActionRuntimeController.cs
│   │   └── ...                         # Health, Damage 等
│   └── Editor/
│       └── ActionEditor/               # 编辑器专用
│           ├── ActionEditorWindow.cs
│           ├── ActionTimelineView.cs
│           ├── HitboxPreviewDrawer.cs
│           ├── ActionGraphEditor.cs
│           └── ActionDefinitionInspector.cs
├── Data/
│   └── Combat/
│       ├── Actions/                    # 各 ActionDefinition 资产
│       │   ├── Player/
│       │   └── Enemy/
│       └── Graphs/                     # ActionGraph 资产
│           ├── PlayerComboGraph.asset
│           └── EnemyMeleeGraph.asset
└── Prefabs/
    └── Systems/
        └── ActionPreviewRig.prefab       # 编辑器预览用 Rig
```

---

## 6. 数据示例（概念）

```yaml
# ActionDefinition: player_attack_1
id: player_attack_1
animationClip: Attack1
sampleRate: 30
totalFrames: 42
actionType: Attack
phases:
  - type: Startup
    startFrame: 0
    endFrame: 8
  - type: Active
    startFrame: 9
    endFrame: 18
  - type: Recovery
    startFrame: 19
    endFrame: 41
hitboxes:
  - id: katana_blade
    attachBone: Hand_R
    startFrame: 10
    endFrame: 16
    shape: Box
    size: [1.2, 0.1, 0.3]
    damageMultiplier: 1.0
events:
  - frame: 9
    type: PlaySFX
    payload: sfx_slash_light
  - frame: 12
    type: PlayVFX
    payload: vfx_slash_trail
cancelWindows:
  - startFrame: 20
    endFrame: 35
    allowedInputs: [Attack]
    targetActionId: player_attack_2
  - startFrame: 15
    endFrame: 25
    allowedInputs: [Dodge]
    targetActionId: player_evade
transitions:
  - condition: AnimationEnd
    targetActionId: null   # 回 Locomotion
```

---

## 7. 里程碑（动作编辑器专项）

| 里程碑 | 目标 | 依赖 |
|--------|------|------|
| **M2'** | 定义 ActionDefinition SO，手写 Player Attack1 并可运行 | M2 战斗原型 |
| **M5** | ActionEditorWindow 基础版 + Inspector 增强 | M3 Demo 可玩 |
| **M6** | 时间轴 + Hitbox Scene 预览 | M5 |
| **M7** | ActionGraph + 校验 / 导入导出 | M6 |
| **M8** | 全角色招式迁入编辑器；敌人共用 | M7 |

---

## 8. 风险与对策

| 风险 | 对策 |
|------|------|
| 编辑器开发量大 | 分 Phase A→E；Demo 仅用 SO 手写 |
| 与 Animator Controller 双重维护 | 约定：招式 clip 只由 ActionDefinition 引用；AC 只管 Locomotion |
| Hitbox 逐帧数据膨胀 | 默认用 **区间** 编辑；仅关键招逐帧微调 |
| 预览与运行时不一致 | 共用同一套 `ActionRuntimeController.UpdateFrame` 逻辑 |
| 策划学习成本 | 提供模板动作 + 复制 + 文档内示例 |

---

## 9. 成功标准

动作编辑器 v1 完成时，应满足：

1. 策划 / 程序可在 **不修改 C# 代码** 的情况下，新建一条普攻并配置 Hitbox、连段、特效事件
2. 编辑态可 scrub 预览 Hitbox 与动画同步
3. 玩家与敌人共用 `ActionRuntimeController`
4. 项目中现有 Attack1–3、Evade、Sp_Skill1 全部迁移为 ActionDefinition 资产

---

## 10. 相关文档

- [项目清单](./PROJECT_CHECKLIST.md) — 总体开发与里程碑
- [第三方资产许可](./THIRD_PARTY_LICENSES.md)
