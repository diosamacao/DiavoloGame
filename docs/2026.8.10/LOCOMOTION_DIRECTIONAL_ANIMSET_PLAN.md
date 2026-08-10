# Locomotion：相位与方向解耦 · AnimSet · 锁定八向就绪 — 优化方案

> 制定：2026-08-10  
> 角色：**Character Locomotion 下一阶段结构真源（先文档，后实现）**  
> 相关：  
> - 既有相位：`DiavoloGame/docs/LOCOMOTION_OPTIMIZATION_PLAN.md`（Idle/Start/Gait/Pivot/Stop）  
> - 步态策略：`DiavoloGame/docs/2026.8.9/LOCOMOTION_GAIT_POLICY_PLAN.md`（GaitPolicy / WalkLeft·Right Resolver）  
> - 敌人移动命令：[`ENEMY_BT_DISCRETE_COMBAT_AND_CONFIG_PLAN.md`](./ENEMY_BT_DISCRETE_COMBAT_AND_CONFIG_PLAN.md)（`LocomotionDesire`）  
> - 锁步 / 表现边界：`ACTION_SYSTEM_LOCKSTEP_REFACTOR_PLAN`、`INPLACE_ROOTMOTION_MOTION_TABLE_PLAN`  
> - 参考心智：UE Lyra（Cardinal + Orientation Warping）、ALS / 经典 Strafe、Unity 2D BlendSpace  
> - 格式 skill：`DiavoloGame/.cursor/skills/actgame-design-plan`  
> 装配链：`WishMove + FacingMode → LocomotionStateMachine(相位) → GaitPolicy → DirectionModel → AnimSet/Blend → Play`  
>  
> **同步说明：** 本文写于 `onlyQuestion/docs/2026.8.10/`；合入游戏仓时放到 `DiavoloGame/docs/2026.8.10/`，并将上文 `DiavoloGame/docs/...` 改为相对链接。

---

## 0. 一句话

Locomotion **相位状态机只表达行为**（Idle/Start/Gait/Stop/Pivot），**方向与锁定是参数与数据**（`FacingMode` + Cardinal/Angle + `LocomotionAnimSet`）；禁止为八向新建 8×Start / 8×Loop / 8×Stop 状态类，禁止继续用膨胀的 `AnimationKey` 枚举承载每个方向变体；玩家锁定与敌人对峙共用同一选片管道。

---

## 1. 问题与动机

### 1.1 现状基线

```text
CharacterActor.Step
  → InputFrame /（未来）LocomotionDesire
  → LocomotionStateMachine
       Idle | Start | Gait | PivotTurn | Stop   ← 各一 C# 相位类
       GaitPolicy.Evaluate → Walk/Run/Sprint
       DefaultLocomotionAnimResolver
         Walk + 横向主导 → WalkLeft / WalkRight
         Start → WalkStartLeft/Right / WalkStart / Start
  → CharacterAnimationService.Play(AnimationKey)
  → Motor.ApplyLocomotion(RotationMode…)
```

| 点 | 现状 |
|----|------|
| 相位 | 五类：`Idle/Start/Gait/PivotTurn/StopLocomotionState`；行为语义正确 |
| 步态 | `LocomotionGaitPolicy` 外置；敌我靠 Profile，State 无身份 if |
| 选片 | 离散 `AnimationKey`：Idle/Walk/WalkLeft/WalkRight/WalkStart*/Run/Sprint/Start/StartEnd/StopL/R/PivotTurn |
| 朝向 | Profile.`GaitRotationMode`：`FollowInput` / `FaceCamera`（敌对峙） |
| 循环横移 | 仅左右；无后向、无对角线、无完整八向 |
| 起步 | Start 相位内闩 Key；含 Walk↔Run 升档/降档特例 |
| 急停 | 按落脚 `StopL`/`StopR`，非按移动方向 |
| 播放 | 单 Clip `Play(Key)`；无 Cardinal 表、无 2D Blend、无 Orientation Warp |

### 1.2 痛点

1. **扩展单位错位**：每加一种移动表现 ≈ 新 `AnimationKey` + Profile 槽 + Resolver 分支 +（常）Start 特例；玩家状态扩展很重。  
2. **方向污染相位**：`StartLocomotionState` 已含 WalkStart 族、升跑直切、降走重闩；方向逻辑渗进行为类。  
3. **组合爆炸预期**：锁定八向若沿用「一方向一 Key / 一方向一状态」，将出现 8 走循环 + 8 起步 + 8 停止的资产与配置面（甚至误导成 24 个状态类）。  
4. **自由移动 ≠ 锁定 strafing**：自由跟输入转 + Pivot；锁定面朝目标 + 本地 wish。现有 `FaceCamera` 只够敌人对峙，撑不住玩家完整锁定八向。  
5. **与命令轨脱节风险**：敌人即将走 `LocomotionDesire`；若 Locomotion 仍只懂「假摇杆 + 横向 Key」，双轨手感会对不齐。

### 1.3 目标

| 目标 | 说明 |
|------|------|
| 结构 | 相位类数量稳定；方向进 `DirectionModel` + `LocomotionAnimSet`（或 Loop BlendSpace） |
| 锁定就绪 | `FacingMode=FaceTarget` 时，同一套相位 + 四向/八向选片可播 strafing，无需新状态类 |
| 配置收敛 | 人维护 AnimSet / Blend 资产；不再每方向改枚举与 State |
| 正交 | 与 GaitPolicy、敌人 `LocomotionDesire`、Action 顶层边界正交 |
| 不做 | 八向各一套相位类；Motion Matching 主路径；长期 Key 枚举与 AnimSet 双真源；Agent 改 `.asset`/Clip；本阶段强制上完整骨骼 Orientation Warp（可选后续） |

---

## 2. 设计原则

1. **相位 = 行为，方向 = 参数**：Idle/Start/Gait/Stop/Pivot 只回答「现在能不能松手急停 / 要不要 Pivot」；不回答「播左还是右后」。  
2. **差异在资产与 Policy**：敌我、自由/锁定靠 Profile（FacingMode、AnimSet、MaxGait），禁止 `if (isEnemy)` / `if (lockOn)` 散落 State。  
3. **单一选片入口**：迁完后 Gait/Start 只调 `ILocomotionAnimResolver`（或后继 `LocomotionAnimSet.Resolve`）；删除旁路 `Play(AnimationKey.Walk)` 业务路径。  
4. **逻辑与表现分工**：逻辑帧权威输出 `phase + gait + localWish + facingMode +（可选 cardinal）`；Clip 混合权重可在表现层，但锁步位移仍走既有 Motor / 烘焙轨。  
5. **零长期兼容**：WalkLeft/Right 与 WalkStartLeft/Right 迁入 AnimSet 后删除枚举业务用法与 Resolver 横向硬编码。  
6. **成熟心智优先**：对齐 Lyra「少相位 + Cardinal 选片（对角线后续可 Warp）」；不默认上 Motion Matching。  
7. **Pivot 属于自由移动**：锁定 strafing 默认 `AllowPivot=false` 或由 Policy 关闭；不与八向选片缠成新相位。  
8. **急停两轴分开**：落脚（StopL/R）与移动 cardinal 不在同一阶段强行笛卡尔积；首版急停保持落脚轴，方向急停另开可选阶段。

---

## 3. 目标架构

### 3.1 总览

```text
                    ┌─ 玩家：InputFrame.Move → 相机相对 wish
Wish 来源 ──────────┤
                    └─ 敌人：LocomotionDesire.localMove（E-MOVE）

FacingMode（Profile / 战斗模式 / Desire.faceTarget）
  FollowMove | FaceTarget | FaceCamera

LocomotionStateMachine（相位，O(相位) 固定）
  Idle → Start → Gait ⇄ Pivot? → Stop
           │
           ├─ GaitPolicy → LocomotionGait
           └─ DirectionModel
                  localWish → Cardinal4（定案首版）或 Angle
                       │
                       ▼
              LocomotionAnimSet.Resolve(gait, phase, cardinal[, slot])
                       │
                       ▼
              Presentation：Play(clip) 或 Loop BlendSpace 采样
```

### 3.2 相位保持（不膨胀）

| Phase | 职责 | 与方向关系 |
|-------|------|------------|
| `Idle` | 静止 | 无 |
| `Start` | 必经起步；松手→Stop | **只闩** AnimSet 解析出的 Start Clip，不换相位类 |
| `Gait` | 循环；升档/宽限急停 | 每帧（或滞回后）按 Cardinal 选 Loop |
| `PivotTurn` | 自由移动大角度折返 | 通常单 Clip；锁定模式 Policy 关闭 |
| `Stop` | 急停收束 | 首版仍 StopL/R（落脚）；不强制×八向 |

**定案：不加** `StartForwardState` / `GaitStrafeLeftState` 等方向相位。

### 3.3 FacingMode（定案）

| 模式 | 旋转参考 | 典型用途 |
|------|----------|----------|
| `FollowMove` | 跟 wish 世界方向（现 FollowInput） | 玩家探索 |
| `FaceTarget` | 面朝锁定/索敌目标 | 玩家锁定八向 |
| `FaceCamera` | 面朝假相机/对峙前向 | 敌人对峙（现网） |

- 写入点：`CharacterLocomotionProfile` 默认 + 运行时可被 CombatMode / Desire 覆盖（单一写入契约，禁止多处抢写）。  
- 替换并吸收现 `GaitRotationMode` 语义；迁完删除旧枚举业务名（或改名为 FacingMode 别名一期后删）。

### 3.4 DirectionModel（定案首版：Cardinal4）

```text
enum MoveCardinal { Forward, Back, Left, Right }

Resolve(localWish, epsilon):
  无意义输入 → None
  取 atan2 / 主导轴 → 最近 cardinal（死区 + 可选滞回帧，防对角线抖动）
```

| 决议 | 内容 |
|------|------|
| 首版覆盖 | **四向**（Lyra 同级）；对角线先吸附最近 cardinal |
| 八向 | 可选阶段 L-DIR3：Octant8 或 Loop 2D BlendSpace |
| 滞回 | Cardinal 切换最短驻留（建议与对峙 DistanceBand 同类想法，防抖） |

禁止：在 State 内手写 `if (x>0 && y>0) Play(WalkFL)` 散落。

### 3.5 LocomotionAnimSet（定案数据真源）

人维护（或挂在 `CharacterLocomotionProfile` 内）：

```text
LocomotionAnimSet
├─ Walk
│   ├─ Loop[Fwd, Back, Left, Right]     // Clip 引用；缺则回退 Fwd→Walk 旧键迁移期一次性
│   ├─ Start[Fwd, Back, Left, Right]    // 可缺；缺则回退 Fwd Start → 旧 Start
│   └─ （Stop 方向表：本方案默认不做，见原则 #8）
├─ Run
│   ├─ Loop[Fwd]（锁定后再扩 Back/Left/Right）
│   └─ Start[Fwd]（现 Start / RunStart）
├─ Sprint.Loop[Fwd]
└─ Shared：Idle, StartEnd, PivotTurn, StopL, StopR
```

解析契约：

```text
ResolveLoop(gait, cardinal) → ClipRef / PresentationKey
ResolveStart(gait, cardinal) → ClipRef
ResolveShared(kind) → ClipRef   // Idle, StopL/R, Pivot…
```

**`AnimationKey` 命运：**

| 阶段 | 做法 |
|------|------|
| L-DIR1 | AnimSet 为真源；旧 Key 仅作迁移别名或删除 |
| 完成态 | 业务选片不再 `switch(AnimationKey)` 扩方向；枚举可缩为 Shared 少数项或废弃 |

### 3.6 选片与相位协作

```text
Start.Enter:
  cardinal = DirectionModel.Resolve(localWish)  // 闩定，Start 中途不因微抖换片（可保留升跑直切）
  clip = AnimSet.ResolveStart(gait, cardinal)
  Play(clip)

Gait.ExecuteFrame:
  cardinal = DirectionModel.ResolveWithHysteresis(localWish)
  clip = AnimSet.ResolveLoop(gait, cardinal)
  Play(clip)  // 换 cardinal 用 InterruptFade，非新相位
```

Walk↔Run 升档/降档逻辑留在相位 + GaitPolicy；**不再**以 `AnimationKey.WalkStart*` 族硬编码判断，改为「当前 Start 槽是否属于 Walk 档」。

### 3.7 层边界

| 层 | 职责 | 不负责 |
|----|------|--------|
| 输入 / Desire | wish 平面向量、Face 旗 | AnimationKey、Cardinal |
| Phase SM | 起停 Pivot、松手、必经 Start | 八向枚举、绑 Clip |
| GaitPolicy | MaxGait、Sprint 计时、AllowPivot | 读仇恨、选左右片 |
| DirectionModel | wish→Cardinal/Angle | Motor 位移 |
| AnimSet / Resolver | (gait,phase,cardinal)→Clip | 改 InputFrame |
| Presentation | Play / 可选 Blend / 未来 Warp | 回写逻辑相位 |

### 3.8 与敌人 / 锁定的对齐

| 场景 | FacingMode | AnimSet 期望 |
|------|------------|--------------|
| 玩家探索 | FollowMove | Walk/Run 以 Fwd 为主即可 |
| 玩家锁定 | FaceTarget | Walk（及后续 Run）四向 Loop/Start |
| 敌人对峙 | FaceCamera 或 FaceTarget | Walk Left/Right（+ 可选 Back）；即现 WalkL/R 迁入表 |
| 敌人追击 | FollowMove 或 FaceTarget | Run Fwd |

`LocomotionDesire`（E-MOVE）只提供 wish + face；本方案提供消费侧统一管道。

### 3.9 方案对比（为何不定案别的）

| 方案 | 结论 |
|------|------|
| 八向各一套相位类 | ❌ 明确禁止；工程与测试爆炸 |
| 仅膨胀 AnimationKey | ❌ 现状痛点延续 |
| **Cardinal4 + AnimSet（定案）** | ✅ 与 Lyra 同级；锁步友好；可迁现网 L/R |
| Loop 2D BlendSpace | ⚪ L-DIR3 可选；手感更好，表现层加重 |
| Cardinal + Orientation Warp | ⚪ 更远期；减对角线美术，需骨骼/Warp 基建 |
| Motion Matching | ❌ 本阶段不做；与整数帧/表驱动主路径冲突大 |

---

## 4. 范围声明

| 阶段 | 包含 | 不包含 |
|------|------|--------|
| L-DIR1 | FacingMode 统一；DirectionModel Cardinal4；AnimSet 骨架；Walk L/R 迁入；删横向 Key 业务双轨 | 玩家锁定玩法完整 UX、BlendSpace、Warp |
| L-DIR2 | Start 四向表；Start 相位去 Key 族特例；Cardinal 滞回；敌人/玩家 Profile 接线契约 | 方向×落脚 Stop 笛卡尔积 |
| L-DIR3 | 玩家锁定 FaceTarget 可玩；可选 Octant8 或 Loop BlendSpace；Run strafing 表 | Motion Matching、完整 Lyra Warp |
| 全程不做 | Agent 改 Prefab/`.asset`/Clip；身份 if；长期旧 Resolver 与 AnimSet 并行 |

---

## 5. 分阶段交付（任务 / 验收 / 出口）

> 勾选：未开始 `[ ]`；完成后 `[x]` 并在出口注明日期。

---

### L-DIR1 — FacingMode + Cardinal + AnimSet 骨架

**任务**

- [ ] 新增 `LocomotionFacingMode`（FollowMove / FaceTarget / FaceCamera）；Profile 挂载；Motor 旋转从该模式解析  
- [ ] **吸收** 现 `GaitRotationMode`；迁完删除旧名业务路径  
- [ ] 新增 `MoveCardinal` + `LocomotionDirectionModel`（含 ε 死区）  
- [ ] 新增 `LocomotionAnimSet`（或 Profile 内嵌表）：Walk Loop 四槽 + Shared  
- [ ] `ILocomotionAnimResolver` 改为查 AnimSet；WalkLeft/Right **迁入** Left/Right 槽  
- [ ] **删除** Resolver 内横向硬编码与 `ResolveGaitAnimationKey` 残留双轨（若仍有）  
- [ ] EditMode：方向矩阵 → Cardinal → 期望槽；缺片回退 Fwd  
- [ ] TECHNICAL / 本文件勾选  

**验收**

- [ ] `rg`：Gait/Start 播片不直写 `AnimationKey.WalkLeft/Right` 业务分支  
- [ ] 敌人对峙：表绑 Left/Right 后表现与迁前一致（或更好）  
- [ ] 玩家未绑 Back/Left/Right 时回退 Fwd，不炸  
- [ ] State 内无 `isEnemy` / `lockOn` 身份分支  
- [ ] Unity 编译 / EditMode 在 Editor 确认通过  

**出口：** 选片真源 = AnimSet；方向参数化起步。→ **未达成**

---

### L-DIR2 — Start 表化 + 相位去向特例

**任务**

- [ ] AnimSet 增加 Walk/Run `Start[cardinal]`；缺省回退链写死在 AnimSet（非 State）  
- [ ] `StartLocomotionState`：**删除** `WalkStartLeft/Right/WalkStart` Key 族判断；改为 AnimSet + gait 槽位元数据  
- [ ] Start 进入闩 cardinal；升跑/降走仍可打断，但选片只调 AnimSet  
- [ ] Cardinal 切换滞回（Gait 循环）：`minDwellFrames` 可配  
- [ ] **删除**（或废弃）`AnimationKey.WalkStart*` / `WalkLeft` / `WalkRight` 的业务依赖  
- [ ] EditMode：Start 闩定、Gait 滞回、缺片回退  

**验收**

- [ ] 新增一个方向槽 **零** 新相位类、**零** 新 State 文件  
- [ ] Start 中微抖不换片；松手仍→Stop  
- [ ] TECHNICAL：起步选片一行改为 AnimSet  

**出口：** 相位类不再含方向 Key 特例。→ **未达成**

---

### L-DIR3 — 锁定八向就绪（可玩切片）

**任务**

- [ ] 玩家锁定：`FacingMode=FaceTarget` 接线（CombatMode / 锁定服务单一入口）  
- [ ] wish 转角色本地；DirectionModel 在锁定下稳定出 cardinal  
- [ ] 任选其一落地（**只留一种**为完成态，实施前锁）：  
  - **A（默认）**：Cardinal4 + 完整 Walk Start/Loop 表（对角线吸附）  
  - **B**：Gait Loop 改 2D BlendSpace（Start 仍表驱动）  
- [ ] Run strafing：按需扩 AnimSet（可先 Walk-only 锁定）  
- [ ] Pivot：锁定时 Policy `AllowPivot=false`（或等价）  
- [ ] Play 清单 + EditMode  
- [ ] 文档：与 E-MOVE `LocomotionDesire` croplink  

**验收**

- [ ] Play：锁定下面朝目标，前后左右移动播对应循环（斜向可吸附）  
- [ ] 解锁回 FollowMove：不残留 FaceTarget  
- [ ] 无新增方向相位类；无 Intent/Input 枚举为方向服务  
- [ ] 敌人 Desire 路径与玩家锁定路径共用 DirectionModel + AnimSet  

**出口：** 锁定 strafing 可玩且工程扩展 O(1)。→ **未达成**

---

## 6. 迁移与兼容

### 6.1 保留 / 迁入

| 保留 | 迁入 |
|------|------|
| 五相位 SM、FootCycle、StopL/R、Pivot、GaitPolicy | WalkLeft/Right → AnimSet.Walk.Loop[Left/Right] |
| Motor.ApplyLocomotion、烘焙 Stop/Pivot 轨 | WalkStart* → AnimSet.Walk.Start[*] |
| 敌人独立 LocomotionProfile | GaitRotationMode → FacingMode |
| 顶层 Locomotion ↔ Action 边界 | 未来 LocomotionDesire 只喂 wish+face |

### 6.2 明确删除

| 删除 | 阶段 | 原因 |
|------|------|------|
| Resolver 横向主导硬编码为唯一左右真源 | L-DIR1 | 改 AnimSet |
| Start 内 `IsWalkStartFamily(AnimationKey)` 等 Key 族业务 | L-DIR2 | 改槽位/gait |
| 为八向新增的相位 State 类（禁止出现） | 全程 | 结构禁令 |
| 长期「Key 枚举 + AnimSet」双配 | L-DIR2 末 | 零长期兼容 |
| 锁定专用复制 `EnemyLocomotionSM` | 全程 | 差异在 Profile |

### 6.3 玩家 / 敌人

- **玩家：** 默认 AnimSet 可只填 Fwd；锁定角色再填四向。  
- **敌人：** 对峙 Profile 填 Left/Right（+ 可选 Back）；MaxGait 仍 Run。  
- Agent **不改** `.asset`；Editor 清单见 §9。

---

## 7. 目录与文件预期（增量）

```text
Assets/Scripts/Domain/Character/Locomotion/
  LocomotionFacingMode.cs              // 新（或替换 GaitRotationMode）
  MoveCardinal.cs                      // 新
  LocomotionDirectionModel.cs          // 新
  LocomotionAnimSet.cs                 // 新（或嵌 Profile）
  ILocomotionAnimResolver.cs           // 改：查 AnimSet
  DefaultLocomotionAnimResolver.cs     // 改薄
  CharacterLocomotionProfile.cs        // + FacingMode + AnimSet
  States/StartLocomotionState.cs       // 去 Key 族
  States/GaitLocomotionState.cs        // 经 DirectionModel

Assets/Scripts/Domain/Character/Animation/
  AnimationKey.cs                      // 收缩 Shared；方向键废弃

Assets/Tests/EditMode/.../
  LocomotionDirectionModelTests.cs
  LocomotionAnimSetTests.cs

docs/2026.8.10/LOCOMOTION_DIRECTIONAL_ANIMSET_PLAN.md
```

---

## 8. 风险与对策

| 风险 | 对策 |
|------|------|
| 对角线吸附手感硬 | Cardinal 滞回；L-DIR3 可选 BlendSpace |
| 缺片角色滑步假走 | 回退 Fwd + EditMode/Validator 报缺失；锁定验收强制绑四向 |
| Start 闩 cardinal 与即时转向冲突 | 保持 Follow/Face 旋转与播片分离；升跑直切规则单测锁住 |
| 与 E-MOVE 并行冲突 | Desire 只产 wish+face；本方案只改消费侧；接口先对齐 |
| 旧 AnimationKey 资产大量引用 | 迁移期 AnimSet 槽可从旧 Key 解析 Clip；完成后删 Key |
| 误做 24 状态类 | Code Review / Validator：新增 `*LocomotionState` 须证明是新**行为**相位 |
| Orientation Warp 期待过高 | 本方案不阻塞；Warp 单开后续文档 |

---

## 9. Editor 人工步骤

### 9.1 L-DIR1

1. 复制/打开角色 `CharacterLocomotionProfile`。  
2. 填 AnimSet：Walk.Loop Fwd=原 Walk；Left/Right=原 WalkLeft/Right（敌人必填）。  
3. FacingMode：玩家 FollowMove；敌人对峙 FaceCamera（或 FaceTarget）。  
4. Play：对峙左右走与迁前一致。

### 9.2 L-DIR2

1. 为需要起步分向的角色填 Walk.Start[Left/Right/Fwd]。  
2. 可清空 AnimationProfile 上废弃横向 Key 槽（确认 AnimSet 已接管）。  

### 9.3 L-DIR3

1. 锁定用角色填满 Walk 四向 Loop（Start 按需）。  
2. CombatMode / 锁定开关验证 FaceTarget 切换。  
3. Play：锁定下前后左右循环可辨；解锁恢复 FollowMove。  

**Agent 不改 Prefab / `.asset` / Clip。**

---

## 10. 推荐开工顺序

```text
L-DIR1（FacingMode + Cardinal + AnimSet，迁走 L/R）
  → 人工：敌人/玩家 Profile 填表
  → L-DIR2（Start 表化，相位去 Key 族）
  → （可与 E-MOVE1 并行接口对齐）
  → L-DIR3（玩家锁定可玩；可选 BlendSpace）
  → 总出口 Play 清单
```

**最小可感切片：** L-DIR1 单独可合并（对峙仍左右走，结构已换真源）。  
**产品锁定切片：** L-DIR1+2+3。

**与敌人方案协同：**

```text
E-MOVE1（Desire 通道）∥ L-DIR1（消费侧 AnimSet）
  → 两者接口：Desire.localMove + face → 本管道
```

---

## 11. 成功标准（方案完成）

同时满足：

1. L-DIR1 / L-DIR2 / L-DIR3 出口均为已达成。  
2. 新增一个移动方向 **不** 新增 `LocomotionPhase` / 相位 State 类。  
3. 玩家锁定 strafing Play 可辨；解锁无残留。  
4. 无身份 if；无 Key 枚举与 AnimSet 双真源；WalkLeft/Right 业务路径已删。  
5. 与 `LocomotionDesire` 消费路径一致（若 E-MOVE 已落地）。

---

## 12. 变更日志

| 日期 | 说明 |
|------|------|
| 2026-08-10 | 初版：相位/方向解耦；Cardinal4 + AnimSet 定案；L-DIR1～3；对齐 Lyra 心智与现网 GaitPolicy/WalkL/R |
