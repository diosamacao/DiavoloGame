# ACTGame — 动作编辑器实现方案

> 最后更新：2026-07-13  
> 状态：可开工（数据层 `ActionTimeline` / `ActionNotify` 已落地；VFX/SFX 为点事件）  
> 相关文档：[`ACTION_EDITOR.md`](./ACTION_EDITOR.md)（愿景与调研）、[`ACTION_SYSTEM.md`](./ACTION_SYSTEM.md)（运行时）

---

## 1. 目标与边界

### 1.1 目标

在 Unity Editor 内提供一体化 `ActionEditorWindow`，使策划/程序可以：

1. 浏览并选择 `ActionDefinition` 资产
2. **手动添加轨道**，在轨道内添加**类型化窗口/点事件**，右侧编辑细节，轨道内拖动位置（区间可调长短）
3. VFX / SFX 为**帧事件**触发播放；Inspector 显式调节 `playbackSpeed`；支持自定义 `attachPointId`
4. 按逻辑帧 Scrub / 播放，预览动画 Pose、Hitbox、VFX（触发后按倍率逐帧）
5. 在 Scene 中拖拽编辑框体与 VFX 变换
6. 保存后 Play Mode 直接验证（无需改 C#）

### 1.2 非目标（本方案不做）

| 不做 | 原因 |
|------|------|
| 引入 NBC / Flux / Unity Timeline 作为数据真源 | 与现有逻辑帧模型冲突 |
| 用 GraphView 替代单招帧编辑 | 粒度不对；连招图后置 |
| 编辑器内完整 1v1 战斗模拟 | 成本高，非阻塞 |
| Agent 直接改 `.asset` / Prefab | 资产由 Editor 人工或专用迁移菜单处理 |

### 1.3 成功标准（v1）

- [ ] 可手动添加轨道，并在轨内添加指定类型的窗口/点事件
- [ ] 选中后右侧可编辑全部细节；区间可拖起止，VFX/SFX 仅拖触发帧
- [ ] VFX/SFX 可调显式 `playbackSpeed`；Scrub 按倍率预览粒子
- [ ] 不改代码即可新建普攻并配置 Hitbox / VFX / Cancel / Movement / Rotation
- [ ] Scrub 时动画与 Hitbox / VFX 同步；Cancel 轨道分色可见
- [ ] Scene Handles 改框写回 `timeline`，支持 Undo
- [ ] 编辑器帧语义与 `ActionExecutor.UpdateFrame` / `ActionTimelineRunner` 一致
- [ ] 校验器能标出：缺 Clip、Active 无 Hitbox、区间 `end<start`、Cancel 空输入、VFX 缺 Prefab

---

## 2. 现状基线（2026-07-10）

### 2.1 已具备（可复用）

| 模块 | 路径 | 作用 |
|------|------|------|
| 数据真源 | `ActionDefinition.Timeline` | 明确类型列表：VFX / Hitbox / Hurtbox / Cancel / Movement / Rotation / ActionEvent |
| 运行时派发 | `ActionTimelineRunner` | 点事件跨帧触发；区间 Enter/Tick/Exit |
| Logic Tick | `ActionExecutor.UpdateFrame` | Play Mode 与编辑器应共用的帧推进入口 |
| Inspector 预览 | `ActionDefinitionHitboxEditor` | 帧滑条 + Hitbox/VFX Scene Handles |
| 预览会话 | `ActionEditorPreviewSession` | `AnimationMode` 采样 + `IActionEditorPreviewExtension` |
| 动画采样 | `ActionEditorAnimationSampler` | `AnimationMode.SampleAnimationClip`（比裸 `SampleAnimation` 更安全） |
| VFX 预览 | `ActionEditorVfxPreviewExtension` | Prefab 实例 + `ParticleSystem.Simulate`（当前为**选中常驻**，非按帧触发） |

### 2.2 缺口

| 缺口 | 说明 |
|------|------|
| `ActionEditorWindow` | 无一体化窗口 |
| 多轨 Frameline | 无统一时间轴 UI |
| 播放控制 | 无 ▶/⏸/步进；无 `EditorApplication.update` 按 `sampleRate` 推进 |
| Scrub ↔ Runtime 同链 | 当前仅 AnimationMode 采样，未走 `UpdateFrame` / Timeline 跨帧规则 |
| VFX 预览语义 | 点事件 + 显式 `playbackSpeed` + `attachPointId` Scrub |
| Hurtbox / Cancel / Movement / Rotation / Phase 可视化 | Action Editor 多轨窗口；Phase 可直接创建、拖拽与配置 |
| 校验 / 模板复制 / 热重载 | 未实现 |
| 旧资产迁移 | 旧字段已删；现有 `.asset` 需人工或菜单迁入 `timeline` |

---

## 3. 参考项目结论（实现选型）

| 来源 | 采用 | 不采用 |
|------|------|--------|
| **XMLib.AM** | Scene 攻受击框 Handles、帧导航、stayRange 思路 | JSON TextAsset 管线；无 VFX 预览可忽略 |
| **Unity-ACT-Skill-Editor** | 顶部帧条、`EditorApplication.update` 按帧率推进、切帧 Instantiate + `Simulate` | 900+ 行单体窗口 |
| **Joker** | UIToolkit 多轨骨架、RootMotion 分段 `SampleAnimation` 累加 | Odin 字典当唯一模型；无特效预览 |
| **ARPG-Demo Action** | 编辑器迷你播放器按逻辑帧统一 Tick 动画/特效/音效 | 整包迁入；Skill Flow 做帧编辑 |
| **NBC ActionEditor** | `PreviewBase` 插件：Enter/Update/Exit + `Simulate(time)` | Track/Clip 秒级真源、整包依赖 |
| **ACT-Game-Action-System** | Cancel 语义参考 | 无编辑器 JSON 流程 |
| **ARPG Combo Graph** | 后置连招图 | 替代单招编辑器 |

### 3.1 参考项目：动画 / 特效实时预览怎么做

各项目**都不在编辑器里跑完整战斗运行时**，而是 Edit Mode 专用路径：

```
当前逻辑帧 / 时间
  → 强制采样骨骼 Pose（SampleAnimation 或 AnimationMode）
  → 按配置刷新特效（Instantiate + ParticleSystem.Simulate）
  → （可选）音效 / 框体 Gizmo
```

| 项目 | 动画 | 特效 | 驱动 |
|------|------|------|------|
| Unity-ACT | 裸 `clip.SampleAnimation(model, t)` | 切帧 `InstantiatePrefab`，`update` 里 `Simulate(累计时间)` | OnGUI + `EditorApplication.update` |
| NBC | 裸 `SampleAnimation`（`PlayAnimationPreview`） | Enter 实例化，Update `Simulate(相对片段时间)`，Exit 隐藏 | `AssetPlayer.Sample(time)` → Preview 插件 |
| Joker | 裸 Sample + 多段累加 RootMotion | ❌ | 选帧 → `TickView` |
| XMLib | 裸 Sample（固定 ~30fps） | ❌ | 选帧回调 |
| ARPG Action | Animancer `EditModeSampleAnimation` | 区间内保活实例，`Simulate(相对时间)`；离开销毁 | `ActionClipEditorPlayer.PlayAt/Tick` |
| **ACTGame 现状** | **`AnimationMode.SampleAnimationClip`** | **选中常驻** Prefab + Simulate | PreviewSession Tick |

**特效策略（本方案定稿，见 §4.2.1 / §4.6.3）：**

采用 **点事件触发 + Scrub 按显式倍率 Simulate**。VFX/SFX 不再使用区间窗口保活模型。

**动画采样选型：** 参考项目多用裸 `SampleAnimation`；ACTGame **继续用 `AnimationMode`**（不永久污染 Scene、可禁用 Animator 防冲突）。多窗口互斥沿用现有 `s_globalActive`。

**技术选型定稿：**

| 模块 | 选型 |
|------|------|
| 主窗口 | `EditorWindow` + IMGUI 手动 `Rect`（时间轴）；侧栏/属性可用 `EditorGUILayout` |
| 时间轴 | 自研 Frameline，条目 = `ActionTimelineItem` |
| 预览动画 | 继续 `AnimationMode`（不改用裸 Sample） |
| 预览特效 | VFX/SFX 为**帧事件**；显式 `playbackSpeed`；Scrub `Simulate(t * speed)` |
| 预览调度 | 按逻辑帧驱动扩展；点事件与区间分别对齐 Runner |
| 框体编辑 | Scene `Handles` + `Undo.RecordObject` |
| 数据写入 | `SerializedObject` / `SerializedProperty` 写 `timeline.*` |
| 连招图 | Phase D 再做 GraphView，独立窗口 |

---

## 4. 架构设计

### 4.1 分层

```
┌─────────────────────────────────────────────────────────┐
│ ActionEditorWindow（UI 壳：列表 / 工具栏 / 布局）         │
├──────────────┬──────────────────────┬───────────────────┤
│ ActionList   │ ActionTimelineView   │ SelectionInspector│
│ (资产浏览)   │ (Frameline 多轨)      │ (选中 Item 属性)  │
├──────────────┴──────────────────────┴───────────────────┤
│ ActionEditorPreviewSession（已有）                       │
│   ├ AnimationMode 采样                                   │
│   ├ ActionEditorFramePlayer（新增：按帧调度预览扩展）     │
│   └ IActionEditorPreviewExtension                        │
│         ├ HitboxPreviewExtension                         │
│         ├ VfxPreviewExtension（点事件 + 倍率 Simulate）   │
│         ├ HurtboxPreviewExtension                        │
│         └ CancelOverlayExtension（可选）                 │
└─────────────────────────────────────────────────────────┘
                          │ 读写
                          ▼
              ActionDefinition.Timeline（唯一真源）
                          │
                          ▼
         ActionExecutor / ActionTimelineRunner（运行时）
```

### 4.2 数据约定

- **唯一真源**：`ActionDefinition.timeline`
- **时间轴条目**：统一为可拖拽的**窗口/标记**（`ActionTimelineItem`：`startFrame` / `endFrame` / `priority` / `trackName`）
- **区间窗口**（`ActionNotifyState`）：Hitbox / Hurtbox / Cancel / Movement / Rotation
- **点事件**（`ActionNotify`）：ActionEvent / **PlayVfx** / **PlaySfx**（`startFrame == endFrame`）
- **非帧字段**：仅 `transitions[]` 保持独立；Phase 已迁入 `ActionTimeline.phaseStates`
- **禁止**：再引入 `hitboxes[]` / `vfxEvents[]` / 散字段位移 / 单例 `RotationWindow`

#### 4.2.1 VFX / SFX 点事件数据

VFX/SFX 为 **单帧点事件**（`ActionNotify`，`startFrame == endFrame`），不可在时间轴上拉时长。

| 字段 | 含义 |
|------|------|
| `startFrame`（=`endFrame`） | 触发帧 |
| `prefab` / `audioClip` | 资源引用 |
| `playbackSpeed` | **显式**播放倍率（默认 1）；Inspector 可调；不由窗口长度派生 |
| `attachPointId`（VFX） | 模型子节点名；空则角色默认挂点 |
| 变换（VFX） | `localOffset` / `localEulerAngles` / `localScale` / `parentToAttachPoint` |

- 创建时：落在当前预览帧，长度固定 1 帧，`playbackSpeed = 1`
- 拖动：仅平移触发帧；禁止左右拉边
- Scrub：`t = max(0, previewFrame - triggerFrame) / sampleRate`；`Simulate(t * playbackSpeed)`
- 运行时：跨过触发帧生成/播放一次；招式结束清理 VFX 残留

> 已从区间窗口模型切回点事件，不保留「窗口时长 → 派生倍率」双轨。

### 4.3 轨道与窗口操作逻辑（核心 UX）

#### 4.3.1 手动添加轨道

- 时间轴区域默认**可以为空**，不自动铺满全部类型轨
- 工具栏或轨头区域提供 **「添加轨道」**：
  - 选择轨道类型：Hitbox / Hurtbox / VFX / SFX / Cancel / Movement / Rotation / Phase / Event…
  - 新建一条空轨，写入 `trackName`（可默认 `Hitbox_1`、`VFX_1`）
- 允许同类型多条轨（例如两个 Hitbox 轨），用 `trackName` 区分
- 轨头支持：重命名、删除轨（删轨前确认；轨内窗口一并删除或提示迁移）、显示/隐藏

#### 4.3.2 在轨道内添加窗口

- 轨头或轨空白处右键 / `+` → **「添加窗口」**
- 弹出类型菜单（受当前轨类型约束）：
  - Hitbox 轨 → 仅 `HitboxNotifyState`
  - VFX 轨 → 仅 `PlayVfxNotifyState`
  - Cancel 轨 → 仅 `CancelWindowNotifyState`
  - 若采用「通用轨」，则添加时必须**指定窗口类型**
- 新窗口默认落在当前预览帧附近，长度：
  - 普通区间：默认若干帧（如 5）
  - VFX/SFX/Event：固定 1 帧（点事件）

#### 4.3.3 轨道内拖拽

| 操作 | 行为 |
|------|------|
| 拖窗口中部 | 平移：`start/end` 同增量，长度不变 |
| 拖左边缘 | 改 `startFrame`（可设最小长度 1 帧） |
| 拖右边缘 | 改 `endFrame` |
| VFX/SFX | 只平移触发帧；**禁止**拉边改时长 |
| 其它区间 | 左右边改 `startFrame`/`endFrame` |
| 选中 | 高亮；右侧 Inspector 绑定该窗口 SerializedProperty |
| Delete | 删除选中窗口（Undo） |
| 多选（可选 v1.1） | 暂不做 |

约束：

- `0 <= startFrame <= endFrame < totalFrames`
- 同轨窗口允许重叠（Hitbox 多框常见）；校验器可警告完全重合
- 所有修改走 `Undo.RecordObject` + `SerializedProperty`

#### 4.3.4 右侧细节面板

选中窗口后，右侧显示类型专用字段，例如：

| 窗口类型 | 右侧可编辑 |
|----------|------------|
| Hitbox | id、shape、offset、size、damageWeight、attachPointId… |
| Hurtbox | id、shape、offset、size… |
| VFX | Prefab、变换、自然时长（只读显示）、当前窗口时长、**播放倍率（只读或可反推改 endFrame）** |
| SFX | AudioClip、音量、自然时长、窗口时长、播放倍率 |
| Cancel | cancelType、allowedInputs、priority |
| Movement | displacementDistance |
| Rotation | smoothTimeOverride |

帧区间除轨道拖拽外，右侧也提供 `startFrame` / `endFrame` 数字框，双向同步。

### 4.4 轨道类型与数据映射

| 轨道类型 | 数据源 | 窗口类型 | 颜色建议 |
|----------|--------|----------|----------|
| Phase | `timeline.phaseStates` | 区间 | 灰 / 蓝 / 绿；Recovery 集成移动取消与 Entry 重开 |
| Hitbox | `timeline.hitboxStates` | 区间 | 橙红 |
| Hurtbox | `timeline.hurtboxStates` | 区间 | 蓝 |
| VFX | `timeline.playVfxNotifies` | **点事件** | 青 |
| SFX | `timeline.playSfxStates`（元素为 `PlaySfxNotify`） | **点事件** | 品红 |
| Event | `timeline.actionEvents` | 点或短窗 | 黄 |
| Cancel | `timeline.cancelWindowStates` | 区间 | Action=紫 / Movement=青 |
| Movement | `timeline.movementStates` | 区间 | 绿 |
| Rotation | `timeline.rotationStates` | 区间 | 黄绿 |

### 4.5 预览语义（目标行为）

| 模式 | 行为 |
|------|------|
| Scrub | 设 `previewFrame` → AnimationMode 采样 Pose → 画当前帧生效 Hitbox/Hurtbox → 按窗口推进 VFX/SFX |
| Play | `EditorApplication.update` 按 `1/sampleRate` 累加帧，循环或到末停止 |
| Step | `frame ± 1`，同样走 FramePlayer |
| Scene 编辑 | 选中 Hitbox/VFX 时 Handles 写回；改完 `SetDirty` + Undo |
| Play Mode | 编辑器预览 Session 应停止，避免与运行时抢 Animator |

**一致性目标（分两步）：**

1. **Phase 1–2**：AnimationMode + 扩展；帧索引对齐 `sampleRate`；VFX 可暂选中常驻调位置
2. **Phase 3**：`ActionEditorFramePlayer` + VFX/SFX **窗口内按倍率 Simulate/播放**

### 4.6 动画 / 特效预览实现规格

#### 4.6.1 调用链（目标）

```
ActionEditorWindow
  → 设置 previewFrame（Scrub / Play / Step）
  → ActionEditorPreviewSession.Tick()
       → ActionEditorAnimationSampler.Sample(...)
       → ActionEditorFramePlayer.Advance(previousFrame, currentFrame)
            → 查询当前帧落入的窗口 / 跨帧进入的窗口
            → Extensions:
                 ├ Hitbox / Hurtbox：IsActive 高亮 + Handles
                 ├ VFX：选中实例常显；触发帧后 Simulate((frame-trigger)/rate * playbackSpeed)
                 └ SFX：触发帧预览播放（可选）；pitch = playbackSpeed
```

#### 4.6.2 动画

| 项 | 规格 |
|----|------|
| API | `AnimationMode` 采样（禁止裸 Sample 作主路径） |
| 时间 | `previewFrame / SampleRate` |
| RootMotion | 可选，Phase 3 后置 |

#### 4.6.3 VFX / SFX 点事件预览（显式倍率）

| 项 | 规格 |
|----|------|
| 触发前 | `SimulateAt(0)` 或保持隐藏姿态 |
| 触发后 Scrub | `localTime = (previewFrame - triggerFrame) / sampleRate`；`Simulate(localTime * playbackSpeed)` |
| 播放倍率 | Inspector 显式 `playbackSpeed`；估测自然时长只读，不驱动倍率 |
| 挂点 | `attachPointId` 经 `ActionEditorPreviewAttachPoint.Resolve` / 运行时 `CharacterAttachPointResolver` |
| 选中编辑辅模式 | 强制显示选中 VFX + Handles |
| Play Mode | 不跑编辑器预览 |

运行时：`ActionVfxPlayer` / `ActionSfxPlayer` 在点事件触发时生成/播放一次，应用同一 `playbackSpeed`。

#### 4.6.4 播放推进

同前：`EditorApplication.update` 按 `sampleRate` 推进；帧率取自 `ActionDefinition.SampleRate`。

#### 4.6.5 与运行时对照

| | 运行时 | 编辑器预览 |
|--|--------|------------|
| 帧推进 | `ActionExecutor` | Window Play / Scrub |
| 窗口规则 | `ActionTimelineRunner` Enter/Tick/Exit | `ActionEditorFramePlayer` |
| VFX | 窗口内按 `playbackSpeed` 播放 | Instantiate + `Simulate(t * speed)` |
| Hitbox | 真实检测 | 仅 Gizmo |
## 5. 目录与类规划

```
Assets/Scripts/Editor/Combat/
├── ActionDefinitionHitboxEditor.cs      # 保留：SO 选中时的轻量预览入口
├── ActionEditorPreview.cs               # 已有 Session / Sampler / VfxExtension
├── ActionVfxEditorPreview.cs
├── HitboxSceneDrawing.cs
├── ActionVfxSceneDrawing.cs
└── ActionEditor/                        # 新增
    ├── ActionEditorWindow.cs            # 主窗口 MenuItem
    ├── ActionEditorStyles.cs            # 颜色、轨道高度、常量
    ├── ActionListPanel.cs               # 左侧资产列表
    ├── ActionToolbar.cs                 # 播放/步进/帧显示/预览角色
    ├── Timeline/
    │   ├── ActionTimelineView.cs        # Frameline 总控
    │   ├── ActionTrackView.cs           # 单轨绘制与命中测试
    │   ├── ActionClipView.cs            # 单片段条块
    │   └── ActionTimelineCommands.cs    # 增删改片段（Undo）
    ├── Inspectors/
    │   └── ActionNotifySelectionDrawer.cs
    ├── Preview/
    │   ├── ActionEditorFramePlayer.cs   # 按帧调度预览（Phase 3）
    │   ├── HitboxPreviewExtension.cs    # 从 HitboxEditor 抽离
    │   └── HurtboxPreviewExtension.cs
    ├── Validation/
    │   └── ActionDefinitionValidator.cs
    └── Migration/
        └── ActionTimelineMigrationMenu.cs  # 可选：资产迁移辅助
```

运行时目录不变：`Domain/Combat/Actions/Definitions/Timeline/*`。

---

## 6. 分阶段实现计划

### Phase 0 — 资产与入口准备（0.5～1 天）

**目标：** 编辑器有数据可编。

| 任务 | 说明 | 验收 |
|------|------|------|
| 确认现有招式资产 | 在 Unity 中检查 `ActionDefinition` 是否已填 `timeline` | 至少 1 条攻击有 Hitbox 或 VFX |
| 迁移说明 | 文档列出旧字段 → 新字段对照（见 §8） | 策划可手工迁 |
| （可选）迁移菜单 | `Tools/ACT/Migrate Action Timeline` 只读扫描并报告缺失项 | 不强制自动改资产 |

**不做：** Agent 直接改 `Assets/Data/**`。

---

### Phase 1 — `ActionEditorWindow` 骨架（3～5 天）

**目标：** 一体化窗口能选招、Scrub、复用现有预览。

| 任务 | 细节 |
|------|------|
| `ActionEditorWindow` | `MenuItem("ACT/Action Editor")`；三栏布局 |
| `ActionListPanel` | `AssetDatabase.FindAssets("t:ActionDefinition")`；搜索过滤 |
| `ActionToolbar` | Preview Character、Frame Slider、▶/⏸/◀/▶、循环开关 |
| 接入预览 | 复用 `ActionEditorPreviewSession`；选中动作时 `SetAction` |
| 属性区 | 先 `Editor.CreateEditor(action)` 或绘制 `timeline` 关键 SerializedProperty |
| 与 CustomEditor 关系 | 保留 `ActionDefinitionHitboxEditor`；窗口为主入口，CustomEditor 为快捷预览 |

**验收：**

- [ ] 打开窗口可选任意 `ActionDefinition`
- [ ] 拖入场景角色后 Scrub 可见 Pose
- [ ] Hitbox/VFX 开关与 Scene 预览仍可用（可先委托现有绘制逻辑）

**参考：** Unity-ACT 帧条 + ACTGame 现有 PreviewSession。

---

### Phase 2 — 多轨 Frameline + 窗口交互（5～8 天）

**目标：** 实现「手动加轨 → 轨内加类型窗口 → 右侧编辑 → 拖位置/长短」。

| 任务 | 细节 |
|------|------|
| 添加轨道 | 工具栏「添加轨道」选类型；生成空轨 + 默认 `trackName` |
| 删除/重命名/排序轨 | 轨头菜单；删轨确认；拖动轨头手柄纵向换序并显示插入线 |
| 添加窗口 | 轨内 `+` / 右键 → 指定类型 → 插入对应数组 |
| 拖拽交互 | 中部平移；左右改 `start/end`；最小 1 帧；夹紧到 `totalFrames` |
| 右侧 Inspector | `ActionNotifySelectionDrawer` 按类型画字段；帧数字与轨道双向同步 |
| VFX/SFX 默认长度 | 创建时读自然时长 → 换算帧数作为初始窗口长 |
| VFX/SFX 拖长度 | 只改区间；右侧只读显示 `playbackSpeed = natural / window` |
| `ActionTimelineCommands` | 全部 Undo |
| Cancel 分色 | Action / Movement |

**验收：**

- [ ] 可从零添加 Hitbox 轨并放入窗口，拖长短后保存
- [x] 手动轨道可通过轨头拖拽排序，Undo/Redo 正常
- [ ] 选中窗口右侧可改 Hitbox size / VFX Prefab 等
- [ ] 创建 VFX 窗口时默认长度≈自然时长；拖长后倍率 &lt; 1，拖短后倍率 &gt; 1
- [ ] Undo/Redo 正常

**参考：** Joker 多轨 + XMLib 帧导航；交互模型按本项目 §4.3，不照搬参考项目「自动铺轨」。

---

### Phase 3 — 预览增强与帧一致性（3～5 天）

**目标：** 动画/特效实时预览；VFX/SFX 在窗口内按倍率播放（见 §4.6）。

| 任务 | 细节 |
|------|------|
| 播放推进 | ▶/⏸：`EditorApplication.update` 按 `1/sampleRate` 推进 |
| `ActionEditorFramePlayer` | 窗口 Enter/Tick/Exit；驱动扩展 |
| Hitbox / Hurtbox Extension | 当前帧高亮 + Handles |
| VFX 窗口预览 | 进入实例化；`Simulate(localTime * playbackSpeed)`；离开清理 |
| SFX 窗口预览 | 进入预览播放；速度随倍率（编辑器 AudioUtil 或 AudioSource.pitch） |
| 选中编辑辅模式 | 强制显示选中 VFX + Handles |
| 数据改造 | `PlayVfxNotify`/`PlaySfxNotify` 点事件；显式 `playbackSpeed` + `attachPointId`；`ActionVfxPlayer`/`ActionSfxPlayer` 点触发 |
| Play Mode 互斥 | 进入 Play 时 EndSession |

**验收：**

- [ ] ▶ 播放时 Pose 连续变化
- [ ] Scrub 进入 VFX 窗口粒子出现；拖长窗口后同进度更慢播完
- [ ] 拖短窗口后粒子加快，在窗口结束前播完自然内容
- [ ] Hitbox/Hurtbox 当前帧高亮
- [ ] 跨帧跳转不漏 Enter/Exit

**参考：** ARPG 窗口内 Simulate；Unity-ACT 播放推进；倍率语义为本项目定稿。

---

### Phase 4 — 校验、模板与工作流（2～3 天）

| 任务 | 细节 |
|------|------|
| `ActionDefinitionValidator` | 缺 Clip；`totalFrames<=0`；Active 无 Hitbox；区间 `end<start`；Cancel.Action 无输入；VFX/SFX 缺资源；自然时长≤0 |
| 窗口内错误列表 | 点击定位到轨/片段 |
| Duplicate 招式 | 复制 SO + 重命名 id |
| 模板 | 从模板资产复制（如 `Template_AttackLight`） |
| Play Mode 提示 | 保存后提示「进 Play 验证」；可选简单热重载说明 |

**验收：**

- [ ] 故意配错能在窗口看到错误
- [ ] Duplicate 后新资产可独立编辑

---

### Phase 5 — 连招图（后置，可选）

| 任务 | 说明 |
|------|------|
| `ActionGraph` SO | 节点 = ActionId；边 = 输入 / Cancel 条件 |
| GraphView 窗口 | 独立于帧编辑器 |
| 与运行时 | 输出仍走 `ActionResolver` / `CancelWindowNotifyState`，不双轨选招 |

**前置：** Phase 2 完成且主流程招式已迁入 Timeline。

---

## 7. 关键交互与实现要点

### 7.1 窗口布局（目标）

```
┌────────────────────────────────────────────────────────────────┐
│ ACT Action Editor          [+轨道] [▶][⏸] Frame 12/45  1.0×VFX │
├────────────┬───────────────────────────────┬───────────────────┤
│ Actions    │ Preview Character / 开关      │ Selection         │
│ □ Attack1  ├───────────────────────────────┤ HitboxNotifyState │
│ ■ Attack2  │ Timeline                      │ start/end         │
│ □ Dodge    │ [Hitbox_1] [====HB1====]      │ offset / size     │
│            │ [VFX_1]    [--slash 0.5×--]   │                   │
│            │ [Cancel]   [Atk][==Move==]    │ 或 VFX:           │
│            │                               │ Prefab / 自然时长 │
│            │                               │ 窗口时长 / 倍率   │
└────────────┴───────────────────────────────┴───────────────────┘
```

操作摘要：

1. **[+轨道]** → 选类型 → 出现空轨  
2. 轨上 **[+窗口]** → 选类型 → 出现可拖条块  
3. 拖中部平移、拖边改长短；VFX/SFX 改长短 = 改播放倍率  
4. 选中后右侧编辑细节  

### 7.2 写入规则

```csharp
// 伪代码：所有片段修改必须可 Undo
Undo.RecordObject(action, "Edit Timeline Item");
serializedObject.Update();
// 修改 SerializedProperty
serializedObject.ApplyModifiedProperties();
EditorUtility.SetDirty(action);
```

禁止直接改内存字段却不 `SetDirty`，否则丢数据。

### 7.3 预览挂点与 VFX 双模式

- 短期：Preview Character 根节点
- 中期：解析 `attachPointId` / 模型挂点名
- VFX：
  - **窗口预览**：跟 Scrub/Play，按倍率 Simulate
  - **选中编辑**：强制显示 + Handles 调位置

### 7.4 与 `ActionDefinitionHitboxEditor` 的关系

| 阶段 | 策略 |
|------|------|
| Phase 1 | 两者并存；共享 PreviewSession |
| Phase 2+ | 公共绘制抽到 Extension；CustomEditor 变薄 |
| 最终 | 主工作流在 Window |

---

## 8. 资产迁移对照

旧字段（已从代码删除）→ 新 Timeline 字段：

| 旧 | 新 |
|----|----|
| `hitboxes[]` | `timeline.hitboxStates[]` |
| `vfxEvents[]` / 旧窗口 VFX | `timeline.playVfxNotifies` 点事件（`attachPointId` + 显式 `playbackSpeed`） |
| （无） | `timeline.playSfxStates` 点事件（`PlaySfxNotify`） |
| `cancelWindows[]` | `timeline.cancelWindowStates[]` |
| `rotationWindow`（单例） | `timeline.rotationStates[]`（可多段） |
| `displacementDistance` + start/end | `timeline.movementStates[]` |
| `actionEvents[]`（顶层） | `timeline.actionEvents[]` |

**注意：** 已有 `.asset` 在字段删除后可能丢失旧数据。若本地仍有旧版本备份，应用迁移菜单或手工重配。新招式一律只写 `timeline`。

---

## 9. 风险与对策

| 风险 | 对策 |
|------|------|
| 时间轴 UI 工作量大 | Phase 1 骨架；Phase 2 先做加轨/加窗/拖拽三件套 |
| Phase 与 Timeline 双真源 | 已删除独立 `phases[]`；统一由 Phase 轨创建和拖拽 |
| 自然时长读取不准 | 缓存字段；允许右侧手动覆盖 naturalDuration |
| 预览与运行时不一致 | FramePlayer 与 Runner 共用窗口 Enter/Tick/Exit；VFX 同用 playbackSpeed |
| Undo 丢失 | 强制 SerializedProperty / RecordObject |
| 多窗口 AnimationMode 冲突 | `s_globalActive` Session 互斥 |
| 资产空白 | Phase 0 保证至少一条可预览招式 |
| 范围膨胀到连招图 | Phase 5 后置 |

---

## 10. 里程碑与估时

| 里程碑 | 内容 | 估时 | 依赖 |
|--------|------|------|------|
| **E0** | 资产确认 / 迁移说明 | 0.5–1 天 | — |
| **E1** | Window 骨架 + Scrub | 3–5 天 | E0 |
| **E2** | 多轨 Frameline | 5–8 天 | E1 |
| **E3** | 预览增强 + 帧一致性 | 3–5 天 | E2 |
| **E4** | 校验 / 模板 | 2–3 天 | E2 |
| **E5** | 连招 GraphView（可选） | 1–2 周 | E4 + 主招式稳定 |

合计（E0–E4）：约 **2.5～4.5 周**（单人，含联调）。

---

## 11. 建议开工顺序（下一步）

1. **E1**：`ActionEditorWindow` + 列表 + 工具栏 + PreviewSession  
2. **E2**：手动加轨 / 加类型窗口 / 右侧 Inspector / 拖位置与长短；VFX 默认自然时长  
3. **E3**：FramePlayer + VFX/SFX 窗口倍率预览；`PlayVfx` 迁为区间 State + 运行时对齐  
4. **E4**：校验器与 Duplicate  

---

## 12. 变更日志

| 日期 | 变更 |
|------|------|
| 2026-07-10 | 初版：基于 ActionNotify 时间轴重构后的实现方案 |
| 2026-07-10 | 增补参考项目动画·特效预览对照与 FramePlayer 规格 |
| 2026-07-10 | **交互定稿**：手动加轨；轨内加类型窗口；右侧编辑；拖位置/长短；VFX/SFX 自然时长为 1.0×，拖长度=播放倍率；VFX 从点事件改为区间窗口 |
