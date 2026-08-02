# ACTGame — 动作编辑器（Action Editor）设计文档

> 本文档描述 ACTGame 长期目标：**用可视化编辑器为角色配置战斗动作**，而非在代码或 Animator 里硬编码每一招。  
> 最后更新：2026-08-02（点事件菱形 / 时间轴 Zoom / Scrub 帧 Scene 预览）
> **落地实现方案（分阶段、目录、验收）见：** [`ACTION_EDITOR_IMPLEMENTATION.md`](./ACTION_EDITOR_IMPLEMENTATION.md)

> 当前实现边界：`ActionDefinition` 只保存动画、Timeline 与 ExecutionPolicy；输入 Intent、索敌、起手行为和自动衔接在 `ActionGraphNode`；伤害、HitReactionId、镜头与卡肉反馈在 `HitPayload`；受击/死亡 Action 由 `CharacterReactionResolver` 选择并经 `CharacterReactionService` 统一进入 Actor。下文旧版调研示例中的 `damageWeight`、Action 内 Transition/Trigger 仅保留为历史设计背景，不是当前 Schema。

> `ACT/Combat/Action Graph Editor` 的普通节点和顺序组子节点都内嵌 `Node Policy` 折叠区，可直接配置 Input Intent、Entry、Variant Resolver、Target Lock、Start Behaviors、战斗模式切换与 Automatic Transitions，不使用独立侧栏。

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
2. **帧精确** — 以动画帧（或 normalized time）为最小编辑单位；**编辑器帧 = 运行时 Logic Tick**
3. **编辑态可预览** — Scene 视图回放 Hitbox、位移、事件
4. **渐进式落地** — Demo 先用 SO + 手动填表，再逐步替换为编辑器 UI
5. **角色无关** — 同一套动作数据格式可用于玩家、敌人、Boss
6. **数值与逻辑分离** — 编辑器管帧数据与连招逻辑；总伤害、CD 等由配表（Excel / SO 数值表）管理
7. **项目专用，非万能工具** — 按 ACTGame 架构裁剪，不追求通用 MMO / 格斗引擎

### 1.3 参考方向（非照搬）

| 产品 / 工具 | 可借鉴点 | 不宜照搬 |
|-------------|----------|----------|
| 格斗游戏 Frame Data | Startup / Active / Recovery 分段 | 回合制对战、回滚网络整套方案 |
| Unreal Montage + Notify | 时间轴挂事件、预览播放 | Notify 散在动画资产、跨角色复用弱 |
| Unity Timeline | 轨道式编辑、预览 | 战斗语义扩展成本高 |
| Flux / Slate（Asset Store） | 时间轴 UI 交互、Clip 生命周期 | 插件运行时与自研状态机绑定深 |
| Combat Editor（开源） | SO + 多轨道 + 编辑态 Gizmo + SequencePlayer | 直接引入依赖 |
| Unreal Combo Graph | 连招节点图、自动注入 Notify、输入边展示 | GAS 整套能力系统 |
| Game Creator 2 Melee | 三相攻击、Motion Warp、Melee Clip Inspector | 强依赖 GC2 运行时 |
| UFE（格斗引擎） | 帧数据编辑器、Hitbox 预览、校验 | 2.5D 格斗架构、商业授权 |
| Gordon ACT 编辑器经验（见 §2.3） | ActInfo/SkillInfo 分层、Trigger+SkillCtrl 扩展、一体化 UGUI 编辑器 | Protobuf 二进制、横版自研碰撞全套 |
| Odin / 自定义 Editor | 复杂 SO 列表编辑、多态子类显示 | — |

---

## 2. 方案调研与选型结论

> 2026-06-17 调研摘要：对比市面成熟方案与国内 ACT 实战经验，确认 ACTGame 技术路线。

### 2.1 市面方案分类

| 类别 | 代表 | 数据载体 | 编辑形态 | 适用场景 |
|------|------|----------|----------|----------|
| 格斗引擎型 | UFE | 插件自有 Move 数据 | 专用 Move/Character Editor | 2D/2.5D 格斗 |
| 商业中间件 | GC2 Melee、Hitbox Studio Pro、CLine Action Editor 3 | Melee Clip / 烘焙到 Animation Event / JSON | Inspector 或时间轴 | 快速原型或特定品类 |
| 时间轴/图编辑器 | Combat Editor、Combo Graph、SkillEditorDemo | SO / Timeline 导出 .asset | 多轨道时间轴或节点图 | 技能编排、连招可视化 |
| 动画事件烘焙型 | Animation Events、UE Anim Notify | 存在 Clip 上 | Unity/UE 原生动画窗口 | 简单项目或迁移过渡 |
| 自研 SO + 自定义 Editor | Hellblade 式内部工具、Gordon 方案 | SO / Protobuf / XML | 一体化 EditorWindow + Frameline | 与自研状态机深度集成 |

### 2.2 四种技术路线对比

| 路线 | 数据载体 | 编辑体验 | 运行时 | ACTGame 结论 |
|------|----------|----------|--------|--------------|
| **A. SO 帧表 + 自研 Editor** | `ActionDefinition` SO | 专用窗口 + 时间轴 + Gizmo | `ActionExecutor` | ✅ **采用** |
| **B. Timeline 扩展** | Timeline Asset + Custom Track | 复用 Timeline UI | PlayableGraph / 自研 Player | 🟡 仅借鉴轨道 UI，不引入依赖 |
| **C. Animation Event 烘焙** | 写在 Clip 上 | 简单 | 监听 Animator 事件 | 🟡 Phase B 迁移辅助 only |
| **D. 第三方全栈** | 插件格式 | 开箱即用 | 插件 Runtime | ❌ 与 `CharacterStateMachine` 冲突 |

**选型理由（ACTGame 特有约束）：**

- 已有 `CharacterStateMachine` + 薄层 `ActionState`，`CharacterAnimationService.PlayClip()` 已支持招式直播
- 约定 **无 Animator Controller 业务依赖**，Locomotion/招式均由 `ActionDefinition` / Profile 引用 Clip，经 Playable 播放
- 玩家 / 敌人共用 `ActionExecutor`，不绑定单一品类插件
- 当前规模适合 **ScriptableObject**；若后续要热更再考虑从 SO 导出二进制

### 2.3 国内实战经验映射（Gordon ACT 编辑器）

参考资料：[ACT技能编辑器的制作经验分享](https://www.gameres.com/811422.html)（知乎 [p/38001896](https://zhuanlan.zhihu.com/p/38001896)）

| 该文模块 | ACTGame 对应 | 备注 |
|----------|--------------|------|
| `ActInfo` + 逐帧 `FrameInfo` | `ActionDefinition` + `HitboxKeyframe` / `HurtboxKeyframe` | 需同时支持攻击框与受击框 |
| `SkillInfo`（多 `ActInfo` 拼帧） | `ActionDefinition` + `ActionAnimationSegment[]` | ✅ 多段顺序播放已落地 |
| `ChangeCtrl` | `CancelWindow` + `ActionTransition` | 帧范围 + 输入 → 目标招式 |
| `SkillCtrl` + `Trigger` | `ActionEvent` + `Custom` 扩展通道 | M5 起为 Custom 预留 Trigger+Ctrl 子类模式 |
| `HitInfo` | `Combat/` 层命中反应配置 | 编辑器不承载完整伤害公式 |
| `ActorCfg` | `CharacterCombatProfile` | 角色战斗根配置 |
| Frameline 时间轴 UI | Phase C 多轨道时间轴 | — |
| UGUI 一体化编辑器 | `ActionEditorWindow` | 避免 Prefab 分散拖拽 |
| GM 热重载配置 | Phase B/E 提前落地 | 不必等完整 Editor 内战斗模拟 |
| Excel 总伤害 + Hit 系数 | `damageWeight` 字段 + 数值表 | 见 §2.5 |

**该文核心经验纳入设计：**

1. **数据先行** — 先推演 Editor 配置路径与 Runtime 执行路径，再写代码
2. **Logic Tick = 编辑器帧** — 预览与 Play Mode 共用 `UpdateFrame(frameIndex)`
3. **扩展搭积木** — 新功能优先加事件/条件子类，而非改底层框架
4. **预览可折中** — Phase B 即可做 Play Mode GM 重载；完整 Overlay 放 Phase E
5. **Undo/Redo 可延后** — M5 以 Copy/Paste + 模板复制为主

### 2.4 功能优先级矩阵

#### Must Have（M2–M5）

| 功能 | 说明 |
|------|------|
| `ActionDefinition` 数据模型 | id、clip、fps、总帧、类型、标签 |
| `ActionPhase` | Startup / Active / Recovery + 无敌 / 霸体 |
| `HitboxKeyframe` | 骨骼挂点 + 形状 + 起止帧 + `damageWeight` |
| `HurtboxKeyframe` | 受击框；无框帧不可被击中 |
| `CancelWindow` | 动作取消 / 移动取消 + `cancelType` + 优先级 |
| `ActionTransition` | 动作结束与分支衔接（`AnimationEnd` / `OnHit` 等） |
| `ActionPhase` 打断规则 | 各阶段 `interruptible` + 受击反应引用 |
| `ActionEvent` | VFX / SFX / 位移 / 顿帧等帧触发 |
| `ActionExecutor` | 逐帧驱动 Animator + Combat + 取消 |
| `ActionEditorWindow` 基础版 | 列表 + Inspector 增强 + 自动算帧数 |

#### Should Have（M6）

| 功能 | 说明 |
|------|------|
| 帧 Scrubber + 播放控制 | 逐帧步进 |
| Scene Hitbox / Hurtbox Gizmo | 随帧变化 |
| 多轨道时间轴 | Phases / Hitboxes / Events / Cancels |
| 编辑态动画预览 | `AnimationMode` 或 Preview Rig |
| 数据校验 | 未闭合 Hitbox、Active 无伤害、clip 缺失 |

#### Nice to Have（M7+）

| 功能 | 说明 |
|------|------|
| `ActionGraph` 节点图 | GraphView 连招 / 受击分支 |
| 模板复制 / JSON 导入导出 | 新角色快速量产 |
| Play Mode Overlay | 当前帧、阶段、框体调试 |
| `ActionAnimationSegment[]` 多 Clip 拼招 | ✅ 已实现：同招顺序播多段，无需另建后摇 Action |
| Motion Warp | 吸附目标（参考 GC2） |

#### 明确延后

- 格斗回滚网络帧同步
- 完整 Editor 内双人对战模拟
- M5 全局 Undo/Redo
- 2D 精灵动画编辑

### 2.5 数值与逻辑分离约定

| 配置方 | 内容 | 载体 |
|--------|------|------|
| 数值策划 | 招式总伤害、CD、消耗、基础属性 | Excel / `Assets/Data/` 数值 SO |
| 动作策划 | 各 Hitbox `damageWeight`（同招式系数之和 = 1.0）、帧区间、事件、连招 | `ActionDefinition` + 编辑器 |
| 程序 | `finalDamage = tableDamage × hitWeight`、命中反应、Buff | `Combat/` |

---

## 3. 核心概念模型

```
CharacterCombatProfile          # 角色战斗配置（引用动作库、起始招式 id）
    └── ActionGraph               # 可选：连招 / 状态转移图
            └── ActionNode        # 节点 = ActionDefinition 引用 + 转移条件
ActionDefinition                # 单个战斗动作（核心资产）
    ├── ActionAnimationSegment[]  # 多段 AnimationClip 顺序拼接（攻击+后摇等）
    ├── ActionPhase[]           # 阶段：Startup / Active / Recovery / ...
    ├── ActionTimeline          # 统一时间轴（Notify / Cancel / Hitbox …）
    └── ActionTransition[]      # 结束衔接与分支：换另一条 Action（连段等）
ActionExecutor                  # 动作执行器（Player / Enemy 共用）
    └── 读取 ActionDefinition，按段播 Clip + Combat；Logic Tick 与编辑器帧一致
```

### 3.1 ActionDefinition（动作定义）

单个可执行战斗动作的最小完整单元。示例：`Attack1`、`Evade`、`Sp_Skill1`。

**基础字段：**

| 字段 | 说明 |
|------|------|
| 资产文件名 | 唯一标识与编辑器显示名（如 `player_attack_1`） |
| `animationSegments` | 顺序动画段；每段含 `clip` / `startFrame` / `endFrame` / `crossFadeDuration` |
| `sampleRate` | 采样率（默认与动画 import 一致，显式对齐 Logic Tick） |
| `totalFrames` | 总帧数（各段有效帧累加，OnValidate 自动写） |
| `actionType` | Attack / Dodge / Skill / Hit / Death / Locomotion ... |
| `tags` | 如 `light_attack`, `invincible`, `guard_break` |

### 3.2 ActionPhase（动作阶段）

将一条动作按 **ACT 战斗语义** 分段。核心三相为 **前摇（Startup）→ 有效（Active）→ 后摇（Recovery）**；无敌与霸体为**覆盖在帧区间上的属性标记**，与三相正交。

#### 核心三相

| 阶段 | 俗称 | 常见含义 | 典型配置 |
|------|------|----------|----------|
| `Startup` | 前摇 | 无攻击判定；通常可被打断 | `interruptible: true` |
| `Active` | 动作中 / 有效帧 | Hitbox 开启、造成伤害 | 配合 Hitbox 区间 |
| `Recovery` | 后摇 | 判定关闭、动作硬直；**取消窗口主要落在此段** | 配合 `CancelWindow[]` |

#### 覆盖属性（非独立时间段）

| 标记 | 说明 |
|------|------|
| `Invincible` | 无敌帧（I-Frame），可与任意三相区间重叠 |
| `SuperArmor` | 霸体：受击不切入 Hit 状态，但仍可扣血（规则由 Combat 定） |

每阶段 / 标记用 `[startFrame, endFrame]` 表示。时间轴 **Phases 轨道** 至少展示 Startup / Active / Recovery 三段。

#### 阶段打断规则（受击衔接）

除霸体 / 无敌外，各阶段可配置是否允许被攻击打断，以及打断后进入的受击动作：

| 字段 | 说明 |
|------|------|
| `interruptible` | 该区间内被命中时是否打断当前招式 |
| `interruptActionId` | 打断后播放的受击 `ActionDefinition`（`actionType: Hit`）；空则由 Combat 按 HitInfo 选取 |

典型约定（可调）：

- **Startup**：可打断 → 轻受击
- **Active**：默认不可打断；无霸体时可被重击打断
- **Recovery**：可打断 → 重受击 / 浮空（由 HitInfo 决定具体 `interruptActionId`）

> **设计约定：** 可取消性（连段、闪避、走路）**不**用 Phase 类型表达，统一由 `CancelWindow` 配置。已废弃将 `Cancel` 作为 Phase 类型的做法，避免与取消窗口混淆。

### 3.3 ActionEvent（时间轴事件）

在指定帧或帧范围内触发的逻辑，**不在代码里散写 Animation Event**。

| 事件类型 | 说明 |
|----------|------|
| `SpawnHitbox` / `DisableHitbox` | 开启 / 关闭指定 Hitbox |
| `PlayVFX` | 播放特效 |
| `PlaySFX` | 播放音效 |
| `ApplyImpulse` | 位移 / 击退 |
| `CameraShake` | 镜头震动 |
| `HitStop` | 命中顿帧 |
| `ChangePhase` | 标记阶段切换（可选，也可由 Phase 推导） |
| `Custom` | 扩展钩子；M5 起对齐 **Trigger + SkillCtrl** 子类模式 |

**扩展约定（借鉴 Gordon 方案）：** `Custom` 事件可携带 `triggerType` + `ctrlType` + 参数，运行时反射或工厂实例化对应 Ctrler；新增表现逻辑以加子类为主。

### 3.4 HitboxKeyframe（攻击判定框）

| 字段 | 说明 |
|------|------|
| `hitboxId` | 如 `weapon_blade`、`kick` |
| `startFrame` / `endFrame` | 生效区间（默认编辑方式） |
| `shape` | Box / Capsule / Sphere |
| `localOffset` / `localRotation` / `size` | 相对骨骼或挂点的局部变换 |
| `attachBone` | 可选骨骼名（如 `Hand_R`） |
| `damageWeight` | 该 Hit 占招式总伤害的比例（对接数值表，同招之和 = 1.0） |
| `hitEffect` | 命中特效 / 音效引用 |

编辑器内应在 Scene 视图 **逐帧 scrub** 显示 Gizmo。碰撞检测建议 **自研 Hitbox 查询**，不必依赖完整 Physics（可控、便于 Debug）。

### 3.5 HurtboxKeyframe（受击框）

| 字段 | 说明 |
|------|------|
| `hurtboxId` | 如 `body`、`head` |
| `startFrame` / `endFrame` | 生效区间 |
| `shape` / `localOffset` / `size` / `attachBone` | 同 Hitbox |
| `hurtboxType` | 可选：Body / Head / Weak（影响命中反应） |

**规则：** 当前帧无受击框 → 角色不可被击中（ACT 惯例）。

### 3.6 CancelWindow（取消窗口）

描述 **招式播放过程中**（通常在 Recovery 后摇的不同子区间）玩家输入能否提前结束 / 切换行为。语义等价于 Gordon 方案的 `ChangeCtrl`。

#### 取消类型（`cancelType`）

| 类型 | 行为 | `targetActionId` |
|------|------|------------------|
| `Action` | **动作取消** — 切到另一个 `ActionDefinition`（连段、闪避、技能） | 必填（或走 ActionGraph 默认边） |
| `Movement` | **移动取消** — 结束当前招式，退出 `ActionState`，回到 `Locomotion`；动画可 CrossFade 截断或播完剩余后摇 | 为空；由 `ActionExecutor` 通知状态机切 Locomotion |

后摇常见配置模式：

```
Recovery [19───────────────41]
         │ early cancel │ move cancel only │
         ├─ Action: Attack/Dodge ─┤        │
         │              ├─ Movement: Move ─┤
         │              │  committed       │
```

同一帧区间可配置多个 `CancelWindow`，用 `priority` 解决重叠；输入缓冲在 `CancelWindow` 窗口内消费（见 §5.1）。

| 字段 | 说明 |
|------|------|
| `startFrame` / `endFrame` | 窗口范围 |
| `cancelType` | `Action` / `Movement` |
| `allowedInputs` | `Attack`、`Dodge`、`Skill1`、`Move` 等 |
| `targetActionId` | 动作取消的目标招式；移动取消时为空 |
| `priority` | 多窗口重叠时的优先级（数值越大越优先） |

### 3.7 ActionTransition（结束衔接与分支）

描述 **动作自然结束或战斗事件触发时** 的衔接，与 `CancelWindow`（播放中提前取消）互补。

| 字段 | 说明 |
|------|------|
| `condition` | 触发条件（见下表） |
| `targetActionId` | 目标 `ActionDefinition`；`null` 表示回 `Locomotion` 或战斗待机（由 Graph 默认节点决定） |
| `priority` | 多条件同帧触发时的优先级 |

| `condition` | 说明 | 示例 |
|---------------|------|------|
| `AnimationEnd` | Clip 播完 | 回 Locomotion / 战斗待机 |
| `OnHitConfirm` | 本招至少命中一次 | 自动进下一段连招（无需再按攻击） |
| `OnWhiff` | 全程未命中 | 加长后摇或切挥空收招动作 |
| `OnBlocked` | 被格挡 | 切弹刀 / 硬直收招 |
| `OnInterrupted` | 被受击打断（与 Phase.interrupt 配合） | 通常由 Combat 驱动，较少写在 Transition |

**与 CancelWindow 的分工：**

| 时机 | 机制 |
|------|------|
| 播放中 + 玩家输入 | `CancelWindow` |
| 播放中 + 被击中 | `ActionPhaseNotifyState.interruptible` + Combat → `Hit` 状态 / 受击 `ActionDefinition` |
| 播放结束或命中/挥空等 | `ActionTransition` |

### 3.8 ActionGraph（连招 / 状态图，可选）

用于描述 **动作之间的转移**，而非在 `PlayerStateMachine` 里写死 if-else。

```
[Idle] --Attack输入--> [Attack1] --窗口内Attack--> [Attack2] --窗口内Attack--> [Attack3]
[Attack*] --Dodge输入--> [Evade]
[Any] --受击--> [Hit] --恢复--> [Idle]
```

节点 = `ActionDefinition` 引用，边 = 输入 / 条件 / 自动连段。参考 Combo Graph 的输入边可视化。

**受击与收招：** `actionType: Hit` 的表现仍复用 `ActionDefinition` 格式，但由 `CharacterReactionResolver` 选择并经共享 `CharacterReactionService` 播放，不进入主动出招图。HitState 播放结束后回 Locomotion；主动动作的自动收招只配置在 `ActionGraphNode.AutomaticTransitions`。

### 3.9 ActionAnimationSegment（多动画拼招）

| 字段 | 说明 |
|------|------|
| `clip` | AnimationClip |
| `startFrame` / `endFrame` | 使用该 Clip 的逻辑帧范围（`endFrame < 0` = 到末尾） |
| `crossFadeDuration` | 切入本段淡入；首段可回退 `ActionDefinition.crossFadeDuration` |

同一招式由多段动画顺序拼接（例如攻击主段 + 后摇），`totalFrames` 为各段有效帧之和；时间轴 Notify/Cancel 仍用全局逻辑帧。换另一条招式仍用 `ActionTransition`。

### 3.10 动作生命周期与衔接（总览）

单条招式从进入到退出的完整链路：

```
进入招式 (ActionGraph / 输入)
    │
    ▼
┌─ Startup ─┬─ Active ─┬────── Recovery ──────────────────────┐
│  前摇      │  有效帧   │  early ActionCancel │ late MoveCancel │
│  可被打断  │  Hitbox  │  (连段/闪避)         │ (走路取消)       │
└───────────┴──────────┴──────────────────────┴─────────────────┘
    │ 受击且 interruptible          │ CancelWindow (Action/Movement)
    ▼                               ▼
 Hit 受击 ActionDefinition      下一招 / Locomotion
    │
    ▼
 ActionTransition (AnimationEnd / OnHit / OnWhiff / OnBlocked)
    │
    ▼
 下一招 / 战斗待机 / Locomotion
```

**状态机分工（`CharacterStateType`）：**

| 状态 | 职责 |
|------|------|
| `Action` | 攻击、闪避、技能等主动招式；`ActionState` 锁定 Locomotion 动画 |
| `Hit` | 受击硬直、浮空、倒地；播放 `actionType: Hit` 的 `ActionDefinition` |
| `Locomotion` | 移动；**移动取消**从 `Action` 退出后进入 |

`ActionExecutor` 负责在 `Action` / `Hit` 状态下逐帧推进；是否切状态由 Cancel、Interrupt、Transition 三类规则共同决定。

---

## 4. 编辑器功能规划

### 4.1 界面布局（目标形态）

```
┌─────────────────────────────────────────────────────────────────┐
│ Action Editor — player_attack_1                          [Preview]│
├──────────────┬──────────────────────────────────────────────────┤
│ Action List  │  Animation Preview (Scene / Game View)           │
│ ├ Attack1    │  [◀ ◼ ▶]  Frame: 12 / 45    [☑ Hit] [☑ Hurt]   │
│ ├ Attack2    ├──────────────────────────────────────────────────┤
│ ├ Evade      │  Timeline Tracks (Frameline)                     │
│ └ Sp_Skill1  │  ├ Phases      [===Startup==|==Active==|Rec=]   │
│              │  ├ Hitboxes    [----[HB1]-------[HB2]----------] │
│ Character:   │  ├ Hurtboxes   [========body========]            │
│ Katana Girl  │  ├ Events      |*VFX    *SFX        *Shake|      │
│              │  ├ Cancels     [Act:Attack][Act:Dodge][Mov:Move──] │
│              │  └ Invincible  [=========]                       │
├──────────────┴──────────────────────────────────────────────────┤
│ Inspector — 选中帧 / 事件 / Hitbox / Hurtbox 属性编辑            │
└─────────────────────────────────────────────────────────────────┘
```

**布局原则：** 一体化 `EditorWindow`（UGUI 或 IMGUI 手动布局），策划在单窗口完成选招、编辑、预览，避免多 Prefab 分散拖拽。

### 4.2 功能分期

#### Phase A — 数据层（无自定义 UI，M2–M3 并行）

- [ ] 定义 `ActionDefinition`、`ActionPhase`、`ActionEvent`、`HitboxKeyframe`、`HurtboxKeyframe`、`CancelWindow`（含 `cancelType`）、`ActionTransition` 等类型
- [ ] ScriptableObject 资产创建菜单（`Assets/Data/Combat/Actions/`）
- [ ] 运行时 `ActionExecutor` 读取 SO，**Logic Tick 与帧索引对齐**
- [ ] 用 Inspector 手动填 Attack1–3、Evade 数据验证格式
- [ ] Hitbox 简化版：每动作 1 个固定区间即可跑通 Demo

#### Phase B — 基础 Editor 窗口（M5）

- [ ] `ActionEditorWindow`：动作列表 + 选中动作 Inspector 增强（`ReorderableList` 或 Odin）
- [ ] 绑定 AnimationClip，自动计算 `totalFrames` / `sampleRate`
- [ ] Phase / Event / Hitbox / Hurtbox / Cancel（区分 Action / Movement）/ Transition 列表增删改
- [ ] 从 Animation Clip 导入已有 Animation Events（迁移辅助）
- [ ] **Play Mode GM 热重载**：编辑保存后清空 Runtime Cache，进战斗即加载新配置
- [ ] 模板复制 / Duplicate 招式资产
- [ ] `Custom` 事件预留 Trigger + SkillCtrl 扩展点

#### Phase C — 时间轴与预览（M6）

- [ ] 帧 scrubber + 播放控制（逐帧步进）
- [ ] Scene 视图 Hitbox / Hurtbox Gizmo 预览（随帧变化）
- [ ] 编辑态动画采样（`AnimationMode` / `PreviewRenderUtility` / `ActionPreviewRig`）
- [ ] 多轨道 Frameline：Phases / Hitboxes / Hurtboxes / Events / Cancels（Action·Movement 分色）/ Invincible / Transitions
- [ ] 基础校验：未闭合 Hitbox、Active 无 Hitbox、clip 缺失

#### Phase D — 连招图与批量工具（M7）

- [ ] `ActionGraph` 节点图编辑器（Unity GraphView）
- [ ] 复制动作模板、批量导出 / 导入 JSON
- [ ] 与 Animator Controller 参数映射表（Locomotion only）
- [ ] 数值表对接：`damageWeight` 总和校验
- [ ] 校验工具：孤立节点、空 Active 帧、权重不守恒

#### Phase E — 运行时调试（M7+）

- [ ] Play Mode Overlay：当前帧、阶段、Hitbox / Hurtbox
- [ ] 录制战斗回放帧数据（调试用）
- [ ] 与 `ActionDefinition` diff 对比
- [ ] 可选：Editor 内简易 1v1 预览（非阻塞项）

### 4.3 Editor 实现技术选型

| 模块 | 选型 | 说明 |
|------|------|------|
| 主窗口 | `EditorWindow` + IMGUI（M5）→ 可选迁 UIToolkit | 时间轴宜手动布局 Rect，不用 `EditorGUILayout` 自动排 |
| 时间轴 | 自研 Frameline 轨道 | 借鉴 Combat Editor / Gordon Frameline，不扩展 Unity Timeline |
| 连招图 | Unity GraphView | Phase D |
| 框体预览 | `Handles` + `AnimationMode.SampleAnimationClip` | 与 Runtime 同一套采样骨骼 |
| Inspector | 自定义 `Editor` | 规模大后再评估 Odin |
| 预览 Rig | `ActionPreviewRig.prefab` | 与玩家骨骼层级一致 |

---

## 5. 运行时架构

### 5.1 执行流程

```
输入 / AI 决策
    → ActionGraph 解析下一动作 id（或 CancelWindow 直接指定）
    → ActionExecutor.Play(actionId)
        → 加载 ActionDefinition
        → CharacterAnimationService.PlayClip(clip)  // Playable 后端
        → 每 Logic Tick：UpdateFrame(frameIndex)   // 与编辑器 scrub 同一套逻辑
            → 检查 Phase 变化（Startup / Active / Recovery）
            → 评估 ActionEvent（含 Custom Trigger+Ctrl）
            → 更新 Hitbox / Hurtbox 查询体
            → Combat 命中 → 若当前 Phase.interruptible → 切 Hit 受击动作 / Hit 状态
            → 检查 CancelWindow + 输入缓冲
                → cancelType: Action  → Play(targetActionId)
                → cancelType: Movement → 结束招式，状态机切 Locomotion
    → 动作结束或 OnHit / OnWhiff 等 → ActionTransition
        → targetActionId 或 null（Locomotion / 战斗待机）
```

**输入缓冲：** `InputReader` 在招式播放全程缓存输入；`ActionExecutor` 仅在有效 `CancelWindow` 内消费，避免过早连段。

### 5.2 与现有模块的关系

| 模块 | 关系 |
|------|------|
| `PlayerStateMachine` | 薄层：`Locomotion` / `Hit` / `Death`；`Action` 锁定动画交给 ActionRuntime；**移动取消**从 `Action` 退出回 `Locomotion` |
| `Combat/` | 消费 Hitbox + `damageWeight` × 数值表；`HitInfo` + `Phase.interruptible` 驱动受击反应与 `interruptActionId` |
| `Input/` | 输入写入 Buffer；ActionRuntime 在 CancelWindow 内消费 |
| `Enemy/` | AI 输出 `actionId`，同一套 ActionRuntime |
| `Animator` | **仅 Locomotion**；招式 clip 由 `ActionDefinition` 引用、`PlayClip` 播放 |

### 5.3 Demo 阶段的折中

Demo（M1–M4）不等待编辑器，但 **数据结构按 ActionDefinition 设计**：

- 手写 5–8 个 Action SO（Attack1–3、Evade、Skill1、EnemyAttack）
- Hitbox 简化：每动作 1 个固定区间；Hurtbox 可用全身单区间
- 连段先用 `nextActionId` 或代码，M5 迁到 CancelWindow / ActionTransition
- M2 可先不做 `Movement` 取消与 `OnWhiff` 分支，但 Schema 预留 `cancelType` / `ActionTransition.condition`
- 伤害先用固定值，M7 接 `damageWeight` + 数值表

---

## 6. 目录规划

```
Assets/
├── Scripts/
│   ├── Combat/
│   │   ├── Actions/                    # 运行时
│   │   │   ├── ActionDefinition.cs
│   │   │   ├── Timeline/ActionPhaseNotifyState.cs
│   │   │   ├── ActionEvent.cs
│   │   │   ├── HitboxKeyframe.cs
│   │   │   ├── HurtboxKeyframe.cs
│   │   │   ├── CancelWindow.cs
│   │   │   ├── ActionTransition.cs
│   │   │   ├── ActionGraph.cs
│   │   │   └── ActionExecutor.cs
│   │   └── ...                         # Health, Damage, HitReaction 等
│   └── Editor/
│       └── ActionEditor/
│           ├── ActionEditorWindow.cs
│           ├── ActionTimelineView.cs
│           ├── HitboxPreviewDrawer.cs
│           ├── ActionGraphEditor.cs
│           └── ActionDefinitionInspector.cs
├── Data/
│   └── Combat/
│       ├── Actions/                    # ActionDefinition 资产
│       │   ├── Player/
│       │   └── Enemy/
│       └── Graphs/                     # ActionGraph 资产
└── Prefabs/
    └── Systems/
        └── ActionPreviewRig.prefab
```

> 注：与 `PROJECT_CHECKLIST.md` 中 `Assets/_Game/` 前缀对齐时，以项目实际目录为准；实现阶段统一迁移。

---

## 7. 数据示例（概念）

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
    interruptible: true
    interruptActionId: player_hit_light
  - type: Active
    startFrame: 9
    endFrame: 18
    interruptible: false
  - type: Recovery
    startFrame: 19
    endFrame: 41
    interruptible: true
    interruptActionId: player_hit_heavy
hitboxes:
  - id: katana_blade
    attachBone: Hand_R
    startFrame: 10
    endFrame: 16
    shape: Box
    size: [1.2, 0.1, 0.3]
    damageWeight: 1.0
hurtboxes:
  - id: body
    attachBone: Spine
    startFrame: 0
    endFrame: 41
    shape: Box
    size: [0.5, 1.0, 0.3]
events:
  - frame: 9
    type: PlaySFX
    payload: sfx_slash_light
  - frame: 12
    type: PlayVFX
    payload: vfx_slash_trail
cancelWindows:
  # 后摇前半：动作取消（连段）
  - startFrame: 20
    endFrame: 32
    cancelType: Action
    allowedInputs: [Attack]
    targetActionId: player_attack_2
    priority: 10
  # 后摇中段：动作取消（闪避）
  - startFrame: 15
    endFrame: 25
    cancelType: Action
    allowedInputs: [Dodge]
    targetActionId: player_evade
    priority: 5
  # 后摇末段：移动取消（走路）
  - startFrame: 33
    endFrame: 41
    cancelType: Movement
    allowedInputs: [Move]
    targetActionId: null
    priority: 1
transitions:
  - condition: AnimationEnd
    targetActionId: null              # 无输入时回 Locomotion
  - condition: OnHitConfirm
    targetActionId: player_attack_2   # 命中自动衔接（可与 Cancel 并存，priority 高者优先）
    priority: 20
  - condition: OnWhiff
    targetActionId: player_attack_1_whiff_recovery
    priority: 10
# 数值表（Excel / SO，非 ActionDefinition 字段）:
# skill_damage[player_attack_1] = 100  →  runtime: 100 * damageWeight

# --- 受击收招示例（actionType: Hit）---
# id: player_hit_light
# actionType: Hit
# animationClip: Hit_Light
# phases: [ Recovery only, interruptible: false ]
# transitions: [ AnimationEnd → null ]
```

---

## 8. 里程碑（动作编辑器专项）

| 里程碑 | 目标 | 依赖 |
|--------|------|------|
| **M2'** | `ActionDefinition` SO + 简化 `ActionExecutor`，手写 Attack1 可运行 | M2 战斗原型 |
| **M5** | `ActionEditorWindow` 基础版 + GM 热重载 + 招式迁移 | M3 Demo 可玩 |
| **M6** | Frameline 时间轴 + Hitbox/Hurtbox Scene 预览 | M5 |
| **M7** | ActionGraph + 数值权重校验 + 导入导出 | M6 |
| **M8** | 全角色招式迁入编辑器；敌人共用 ActionRuntime | M7 |

---

## 9. 风险与对策

| 风险 | 对策 |
|------|------|
| 编辑器开发量大 | Phase A→E 分阶段；M2 仅 SO 手写；M5 不做完整时间轴 |
| 与 Animator Controller 双重维护 | 招式与 Locomotion 均只引用 Clip；运行时 Playable 驱动，无 Controller 状态图 |
| Hitbox 逐帧数据膨胀 | 默认 **区间** 编辑；关键招再逐帧微调 |
| 预览与运行时不一致 | 共用 `ActionExecutor.UpdateFrame`；Logic Tick = 编辑器帧 |
| 策划学习成本 | 模板复制 + 文档示例 + 一体化窗口 |
| 无 Undo/Redo | M5 用 Duplicate / Copy；M7 再评估全局 Undo |
| 数值与帧数据耦合 | `damageWeight` 在编辑器，总伤害在配表；Combat 层结算 |
| 取消类型混淆 | `CancelWindow.cancelType` 明确 Action / Movement；Phase 不再使用 Cancel 类型 |
| 后摇只有连段无走路 | Recovery 末段配 `Movement` 取消窗；编辑器 Cancels 轨道分色校验 |
| 挥空 / 命中无分支 | `ActionTransition` 支持 `OnHitConfirm` / `OnWhiff`；M4 可暂硬编码 |
| 引入 Flux/Slate 依赖 | 仅借鉴 UI，不引入插件运行时 |

---

## 10. 成功标准

动作编辑器 v1 完成时，应满足：

1. 策划 / 程序可在 **不修改 C# 代码** 的情况下，新建一条普攻并配置三相、Hitbox、受击框、**动作/移动取消**、结束衔接与特效事件
2. 编辑态可 scrub 预览 Hitbox / Hurtbox 与动画同步；Cancels 轨道区分 Action / Movement
3. 玩家与敌人共用 `ActionExecutor`；受击招式同为 `ActionDefinition`（`actionType: Hit`）
4. Attack1–3、Evade、Sp_Skill1 全部迁移为 `ActionDefinition` 资产
5. Play Mode 下通过 GM 热重载即可验证编辑结果，无需重启 Editor
6. 后摇至少可配置一段 **动作取消** 与一段 **移动取消**（或文档化为何某招不需要）

---

## 11. 相关文档与外部参考

### 项目内

- [项目清单](./PROJECT_CHECKLIST.md) — 总体开发与里程碑
- [第三方资产许可](./THIRD_PARTY_LICENSES.md)
- `.cursor/skills/actgame-architecture/ROADMAP.md` — ActionState 与 Combat 管线

### 外部参考（调研来源）

| 资料 | 链接 | 要点 |
|------|------|------|
| ACT 技能编辑器制作经验（Gordon） | [GameRes 镜像](https://www.gameres.com/811422.html) / [知乎 p/38001896](https://zhuanlan.zhihu.com/p/38001896) | ActInfo/SkillInfo、Trigger+SkillCtrl、Frameline |
| Combat Editor（开源） | [GitHub](https://github.com/ksjsnnx/Combat-Editor) | SO 多轨道、SequencePlayer、Gizmo 预览 |
| Combo Graph（UE） | [官网](https://combo-graph.github.io/) | 连招图、自动 Notify、输入边 |
| UWA：Timeline 技能编辑器思路 | [博客](https://blog.uwa4d.com/archives/TechSharing_228.html) | Slate/Flux 选型、Clip 生命周期 |
| Frame-specific attacks in Unity | [Game Developer](https://www.gamedeveloper.com/design/frame-specific-attacks-in-unity) | AnimationClipExtended、FrameChecker |
| Game Creator Melee 文档 | [docs](https://docs.gamecreator.io/melee/) | 三相攻击、Motion Warp（UX 参考） |

---

## 12. 变更日志

| 日期 | 变更 |
|------|------|
| 2026-06-11 | 初版：愿景、数据模型、Phase A–E、目录与里程碑 |
| 2026-06-17 | 方案调研与选型结论（§2）；新增 Hurtbox、damageWeight、ActionSegment；Gordon 方案映射；功能优先级矩阵；数值分离约定；GM 热重载；Editor 技术选型表；扩展参考链接与变更日志 |
| 2026-06-17 | 动作阶段与衔接：三相模型细化；`CancelWindow.cancelType`（Action/Movement）；`ActionTransition` 分支条件；Phase 打断规则；受击 `actionType: Hit`；§3.10 生命周期总览；示例与运行时流程更新 |
| 2026-07-12 | `ActionAnimationSegment[]` 多 Clip 顺序播放落地；§3.1/§3.9 更新为已实现 |
