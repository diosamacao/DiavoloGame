# ACTGame — ARPG Demo 项目清单

> 本文档用于跟踪 ACTGame 基本 ARPG Demo 的系统开发与资源准备进度。  
> 最后更新：2026-06-11

---

## 1. 项目目标

### 1.1 Demo 阶段（M1–M4）

构建一个 **可演示、可重复游玩** 的第三人称动作 RPG Demo，具备：

- 1 名可操控角色（School Katana Girl）
- 1 种近战敌人（Humanoid Bot）
- 普攻连段 + 闪避 + 至少 1 个技能
- 第三人称相机与基础 HUD
- 单张战斗场景（`Gameplay.unity`）内的胜负流程

**Demo 明确不做：** 装备系统、任务对话、大地图探索、完整存档、多人联机。

### 1.2 长期目标 — 动作编辑器（M5+）

最终要实现 **Action Editor（动作编辑器）**：在 Unity 内以时间轴 + 可视化的方式为角色配置战斗动作（帧阶段、Hitbox、事件、取消窗口、连招图），运行时由统一的 `ActionRuntimeController` 驱动，玩家与敌人共用同一套数据格式。

> 详细设计见 [ACTION_EDITOR.md](./ACTION_EDITOR.md)

**关键策略：** Demo 阶段即采用 `ActionDefinition` 数据驱动（可先手写 SO），避免把招式逻辑写死在状态机代码里，为后续编辑器铺路。

---

## 2. 当前项目状态

### 2.1 已完成

| 类别 | 内容 | 路径 |
|------|------|------|
| 项目骨架 | 脚本/数据/场景目录规划 | `Assets/_Game/` |
| 玩家美术 | 模型、材质、动画、Prefab | `Assets/_Game/Art/Characters/SchoolKatanaGirl/` |
| 敌人美术 | 模型、材质、Prefab | `Assets/_Game/Art/Enemies/HumanoidBot/` |
| 场景 | Gameplay 场景占位 | `Assets/_Game/Scenes/Gameplay.unity` |
| 第三方许可 | 资产包说明 | `docs/THIRD_PARTY_LICENSES.md` |

### 2.2 可用动画资源（玩家）

| 分类 | 动画 |
|------|------|
| Locomotion | Idle, Walk, Run, CrouchIdle, CrouchWalk, CrouchJog |
| 战斗 | Attack1, Attack2, Attack3, Evade, Hit1, Hit2, Stun, Die |
| 技能 | Sp_Skill1, Sp_Skill2, Sp_Skill3 |
| 位移 | Quickshift_F, Quickshift_B, Quickshift_L, Quickshift_R |
| 其他 | Take, Put |

### 2.3 待开发

- 全部 Gameplay 脚本（`Assets/_Game/Scripts/` 各目录目前仅有占位）
- 战斗逻辑、AI、UI、数据配置
- 场景布置、刷怪、胜负流程

---

## 3. 推荐技术栈

| 包 / 工具 | 用途 | 状态 |
|-----------|------|------|
| Unity Input System | 现代输入映射 | ⬜ 待引入 |
| Cinemachine | 第三人称跟随 / 锁定相机 | ⬜ 待引入 |
| AI Navigation (NavMesh) | 敌人寻路 | ⬜ 待引入 |
| TextMeshPro | UI 文字 | ✅ 已包含 |
| ScriptableObject | 角色/攻击/技能数据配置 | ⬜ 待实现 |
| Unity GraphView | ActionGraph 连招节点图（编辑器） | ⬜ M7 待引入 |
| AnimationMode / Preview | 编辑态动画与 Hitbox 预览 | ⬜ M6 待实现 |

---

## 4. 系统清单

优先级说明：

- **P0** — Demo 最小可玩闭环，必须先完成
- **P1** — 提升手感与演示效果，第二迭代
- **P2** — 完整 ARPG 方向，Demo 之后
- **P3** — 动作编辑器及配套工具链（长期目标）

---

### 4.1 核心框架 — P0

**目录：** `Assets/_Game/Scripts/Core/`  
**数据：** `Assets/_Game/Data/Config/`

- [ ] 游戏生命周期管理（启动 / 暂停 / 重启 / 退出）
- [ ] 场景加载与切换（Boot → Gameplay）
- [ ] 全局事件总线（伤害、死亡、胜利等解耦通信）
- [ ] 时间控制（`TimeScale`、HitStop 顿帧）
- [ ] 对象池（特效、伤害数字；Demo 可先做简化版）
- [ ] 全局 GameConfig（重力、输入缓冲时间、HitStop 时长等）

---

### 4.2 输入系统 — P0

**目录：** `Assets/_Game/Scripts/Input/`

- [ ] Input Actions 资产（移动、攻击、闪避、技能、锁定、暂停）
- [ ] 输入读取层（与 Player / UI 解耦）
- [ ] 输入缓冲（攻击 / 闪避预输入窗口）
- [ ] 键鼠支持（Demo 首发）
- [ ] 手柄支持 — P1

---

### 4.3 玩家控制器 — P0

**目录：** `Assets/_Game/Scripts/Player/`

- [ ] 第三人称移动（CharacterController 或 Rigidbody + 地面检测）
- [ ] 相机相对方向移动
- [ ] Animator 参数驱动（Speed、IsGrounded、AttackIndex 等）
- [ ] Root Motion 开关（攻击 / 闪避位移策略）
- [ ] 玩家引用组件聚合（Health、Combat、Animator、Input）

---

### 4.4 玩家状态机 — P0

**目录：** `Assets/_Game/Scripts/Player/`

| 状态 | 说明 | 优先级 |
|------|------|--------|
| Locomotion | 待机 / 走 / 跑 | P0 |
| Action | 招式执行（委托 `ActionRuntimeController`） | P0 |
| Hit / Stun | 受击硬直 | P0 |
| Death | 死亡动画 + 触发 Game Over | P0 |
| Crouch | 蹲伏移动 | P1 |

- [ ] 状态基类与切换规则（优先级、互斥；**招式取消由 ActionDefinition 驱动**）
- [ ] Locomotion ↔ Action 切换（Attack / Dodge / Skill 均作为 Action 播放）
- [ ] 连段超时重置逻辑 — P1

---

### 4.5 战斗系统 — P0

**目录：** `Assets/_Game/Scripts/Combat/`、`Scripts/Combat/Actions/`  
**数据：** `Assets/_Game/Data/Combat/`、`Data/Combat/Actions/`

> 战斗数据 **优先按 ActionDefinition 格式设计**（见 [ACTION_EDITOR.md](./ACTION_EDITOR.md)），Demo 可手写 SO，后续由编辑器配置。

- [ ] 属性组件（HP、Attack、Defense、Stamina）
- [ ] 伤害计算（基础公式 + 暴击 / 减伤）
- [ ] Hitbox / Hurtbox 判定（基于 ActionDefinition 帧区间，非散落的 Animation Event）
- [ ] 受击反馈（HitStop、击退、Hit 动画触发）
- [ ] 无敌帧（I-Frame）管理
- [ ] 目标选取（最近敌人 / 锁定目标）
- [ ] `CharacterStats` ScriptableObject
- [ ] **`ActionDefinition` SO**（动画、阶段、Hitbox 区间、事件、取消窗口）— P0 核心数据
- [ ] **`ActionRuntimeController`**（统一招式播放与逐帧驱动）— P0
- [ ] `nextActionId` / 简单连段字段（M2）；完整 `ActionGraph` — P3
- [ ] `CharacterCombatProfile`（角色引用动作库 + 默认 Graph）— P1

---

### 4.6 敌人系统 — P0

**目录：** `Assets/_Game/Scripts/Enemy/`  
**Prefab：** `Assets/_Game/Prefabs/Enemies/`

- [ ] 敌人 AI 状态机（Idle → Patrol → Chase → Attack → Hit → Death）
- [ ] 感知系统（视野 / 距离检测、丢失目标）
- [ ] 近战攻击（前摇 / 后摇 / 伤害窗口）
- [ ] 受击与死亡（复用 Combat 模块）
- [ ] NavMesh 寻路集成
- [ ] 敌人数据配置（引用 `CharacterStats` / `ActionDefinition`）
- [ ] 攻击组合 / 绕侧行为 — P1

---

### 4.7 摄像机 — P0

**目录：** `Assets/_Game/Scripts/Camera/`

- [ ] 第三人称跟随（平滑、碰撞避让）
- [ ] 战斗视角（软锁定 / Orbit）— P1
- [ ] 硬锁定与目标切换 — P1
- [ ] Cinemachine Virtual Camera 配置

---

### 4.8 UI — P0

**目录：** `Assets/_Game/Scripts/UI/`  
**Prefab：** `Assets/_Game/Prefabs/UI/`

- [ ] 玩家 HUD（HP 条、体力 / 技能 CD）
- [ ] 敌人头顶血条（World Space）
- [ ] 伤害数字飘字 — P1
- [ ] 暂停菜单
- [ ] 死亡界面（Restart / Quit）
- [ ] 胜利界面（清场完成）

---

### 4.9 关卡与流程 — P0

**场景：** `Assets/_Game/Scenes/Gameplay.unity`

- [ ] 玩家出生点
- [ ] 敌人刷怪点 / 触发区域
- [ ] 场景边界（空气墙或 Kill Zone）
- [ ] 胜负条件（玩家死亡 / 敌人全灭）
- [ ] 环境美术布置（Training Dome 等）— P1
- [ ] 简单 BGM / 环境音效触发 — P1

---

### 4.10 特效与音效 — P1

**目录：** `Assets/_Game/Audio/`、`Assets/_Game/Art/VFX/`（待建）

- [ ] 攻击挥砍轨迹 / 命中特效
- [ ] 闪避残影或 dust 特效
- [ ] 攻击 / 受击 / 闪避 / 技能 SFX
- [ ] 战斗 BGM
- [ ] 音效与动画事件绑定

---

### 4.11 锁定与目标 — P1

**目录：** `Assets/_Game/Scripts/Camera/` 或 `Combat/`

- [ ] 软锁定（攻击自动朝向最近敌人）
- [ ] 硬锁定（按键切换目标）
- [ ] 锁定 UI 指示器

---

### 4.12 完整 ARPG 扩展 — P2

以下系统留待 Demo 验证后再规划：

- [ ] 等级与经验
- [ ] 技能树 / 技能升级
- [ ] 装备与武器切换
- [ ] 背包、掉落、拾取
- [ ] 任务与 NPC 对话
- [ ] 小地图与区域探索
- [ ] 经济系统（金币、商店）
- [ ] 弹反（Parry）、处决、Boss 阶段
- [ ] 存档与进度持久化

---

### 4.13 动作编辑器 — P3

**目录：** `Assets/_Game/Scripts/Editor/ActionEditor/`  
**设计文档：** [ACTION_EDITOR.md](./ACTION_EDITOR.md)

#### Phase A — 数据层（与 M2–M3 并行）

- [ ] `ActionDefinition`、`ActionPhase`、`ActionEvent`、`HitboxKeyframe`、`CancelWindow` 类型定义
- [ ] SO 创建菜单与示例资产（Player Attack1–3、Evade、Skill1；Enemy Attack）
- [ ] `ActionRuntimeController` 运行时逐帧执行
- [ ] 从 Animation Event 迁移到 ActionEvent 的辅助脚本

#### Phase B — 基础 Editor 窗口（M5）

- [ ] `ActionEditorWindow`（动作列表 + 增强 Inspector）
- [ ] 绑定 Clip 自动计算帧数
- [ ] Phase / Event / Hitbox 列表增删改

#### Phase C — 时间轴与预览（M6）

- [ ] 帧 scrubber 与播放控制
- [ ] Scene 视图 Hitbox Gizmo 随帧预览
- [ ] 编辑态动画采样预览
- [ ] 多轨道时间轴 UI（Phases / Hitboxes / Events / Cancels）

#### Phase D — 连招图与工具链（M7）

- [ ] `ActionGraph` 可视化节点图编辑器
- [ ] 动作模板复制、JSON 导入导出
- [ ] Animator 参数映射表
- [ ] 校验：未闭合 Hitbox、空 Active 帧、孤立节点

#### Phase E — 运行时调试（M7+）

- [ ] Play Mode Overlay（当前帧 / 阶段 / Hitbox）
- [ ] 战斗回放调试录制

---

## 5. 开发里程碑

### Milestone 1 — 能走、能看（预计 1 周）

- [ ] 引入 Input System、Cinemachine
- [ ] 玩家移动 + 相机跟随
- [ ] Animator Locomotion（Idle / Walk / Run）
- [ ] Gameplay 场景基础地面与出生点

**验收标准：** 角色可在场景中自由移动，相机稳定跟随。

---

### Milestone 2 — 能打（预计 1–2 周）

- [ ] Combat 核心（HP、Hitbox、伤害）
- [ ] `ActionDefinition` 数据结构 + `ActionRuntimeController`（简化版）
- [ ] 玩家 Attack 连段 + Dodge
- [ ] 敌人基础 AI（追击 + 单次攻击）
- [ ] 受击 / 死亡流程

**验收标准：** 玩家与 1 名敌人可完成一次完整攻防循环。

---

### Milestone 3 — 能玩（预计 1 周）

- [ ] 刷 3–5 名敌人
- [ ] HUD（HP 条）
- [ ] 胜负界面与重启
- [ ] 至少 1 个技能（Sp_Skill1）
- [ ] ScriptableObject 数据配置

**验收标准：** 从零进入场景到清场或死亡，流程完整可重复。

---

### Milestone 4 — 打磨（预计 1 周，P1）

- [ ] HitStop、击退、伤害数字
- [ ] 锁定 / 目标切换
- [ ] 特效与音效
- [ ] 输入缓冲与连段窗口调参
- [ ] Quickshift 位移技能

**验收标准：** 战斗手感达到可对外演示水准。

---

### Milestone 5 — 动作数据工具（预计 2 周，P3）

- [ ] `ActionDefinition` 全套数据结构落地
- [ ] 现有招式迁移为 SO 资产
- [ ] `ActionEditorWindow` 基础版
- [ ] 敌人招式共用 `ActionRuntimeController`

**验收标准：** 可在 Editor 内编辑一条新普攻并进入 Play Mode 验证，无需改代码。

---

### Milestone 6 — 可视化预览（预计 2–3 周，P3）

- [ ] 时间轴轨道 UI
- [ ] Hitbox 逐帧 Scene 预览
- [ ] 编辑态动画 scrub

**验收标准：** 调 Hitbox 帧区间时，Scene 视图实时可见判定框变化。

---

### Milestone 7 — 连招图与生产化（预计 2–3 周，P3）

- [ ] `ActionGraph` 节点图
- [ ] 导入导出与校验工具
- [ ] Play Mode 调试 Overlay

**验收标准：** 新角色可通过「复制模板动作 + Graph 连线」在 1 天内配置完整近战套路。

---

## 6. 目录与职责映射

```
Assets/_Game/
├── Scripts/
│   ├── Core/       # GameManager、EventBus、ObjectPool、SceneLoader
│   ├── Input/      # InputReader、InputActions 绑定
│   ├── Player/     # PlayerController、PlayerStateMachine（薄层）
│   ├── Combat/
│   │   ├── Actions/              # ActionDefinition、ActionRuntimeController
│   │   └── ...                   # Health、Damage、Hitbox
│   ├── Enemy/      # EnemyAI、Spawner（招式委托 ActionRuntime）
│   ├── Camera/     # CameraController、LockOn
│   ├── UI/         # HUD、PauseMenu、GameOverScreen
│   └── Editor/
│       └── ActionEditor/         # 动作编辑器（M5+）
├── Data/
│   ├── Characters/ # CharacterStats、CharacterCombatProfile SO
│   ├── Combat/
│   │   ├── Actions/              # 各 ActionDefinition 资产
│   │   └── Graphs/               # ActionGraph 连招图
│   └── Config/     # GameConfig SO
├── Prefabs/
│   ├── Characters/ # 玩家运行时 Prefab
│   ├── Enemies/    # 敌人运行时 Prefab
│   ├── UI/         # Canvas、HUD Prefab
│   └── Systems/    # GameManager、Spawner 等
├── Scenes/
│   └── Gameplay.unity
├── Art/            # 已有 — 角色、敌人、环境
├── Audio/          # BGM、SFX（待填充）
└── Settings/       # URP、Input、Physics 等项目设置
```

---

## 7. 系统依赖关系

```
Core 框架
  ├── Input 输入
  │     └── Player 控制器
  │           └── Player 状态机（Locomotion / Hit / Death）
  │                 └── ActionRuntimeController ← ActionDefinition SO
  │                       └── Combat（Hitbox、伤害、反馈）
  │                             ├── Enemy AI（同样使用 ActionRuntime）
  │                             └── UI HUD
  ├── Camera 相机
  └── Data SO 配置 ──→ ActionDefinition / CharacterCombatProfile
Scene 关卡 ──→ Player 出生 + Enemy 刷怪

Action Editor（M5+）──编辑──→ ActionDefinition / ActionGraph SO
```

**推荐实现顺序：**

1. Core + Input  
2. Player 移动 + Camera  
3. Animator Locomotion  
4. **ActionDefinition 数据结构 + ActionRuntimeController（简化版）**  
5. Combat（Hitbox + 伤害，读取 Action 帧数据）  
6. 手写 Player / Enemy 招式 SO（Attack1–3、Evade、Skill1）  
7. Enemy AI + 受击 / 死亡  
8. UI + 胜负流程  
9. VFX / SFX + 手感打磨  
10. **Action Editor 窗口 → 时间轴预览 → ActionGraph**（M5–M7）

---

## 8. Demo 范围边界

| 包含 | 不包含 |
|------|--------|
| 1 playable 角色 | 多角色切换 |
| 1 敌人类型 × 3–5 只 | 多种敌人与 Boss |
| 普攻 3 连 + 闪避 + 1 技能 | 完整技能树 |
| 单张战斗场景 | 多关卡 / 开放世界 |
| 基础 HUD + 胜负 UI | 装备 / 背包 UI |
| ActionDefinition 手写 SO | 完整 Action Editor UI（M5+） |

---

## 9. 进度跟踪

> 完成某项后，将 `[ ]` 改为 `[x]`，并在下方记录日期或备注。

| 里程碑 | 状态 | 完成日期 | 备注 |
|--------|------|----------|------|
| M1 — 能走、能看 | ⬜ 未开始 | | |
| M2 — 能打 | ⬜ 未开始 | | |
| M3 — 能玩 | ⬜ 未开始 | | |
| M4 — 打磨 | ⬜ 未开始 | | |
| M5 — 动作数据工具 | ⬜ 未开始 | | ActionEditor 基础 |
| M6 — 可视化预览 | ⬜ 未开始 | | Hitbox 逐帧预览 |
| M7 — 连招图与生产化 | ⬜ 未开始 | | ActionGraph |

---

## 10. 相关文档

- [动作编辑器设计](./ACTION_EDITOR.md)
- [第三方资产许可](./THIRD_PARTY_LICENSES.md)
