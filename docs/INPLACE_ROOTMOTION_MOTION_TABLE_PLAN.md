# InPlace Clip + RootMotion 位移表改造方案

> 基准：帧同步方案 [ACTION_SYSTEM_LOCKSTEP_REFACTOR_PLAN.md](./ACTION_SYSTEM_LOCKSTEP_REFACTOR_PLAN.md)（尤其 Phase L2）  
> 制定日期：2026-07-31  
> 目标：逻辑位移与动画播放解耦——**表现播 InPlace Clip，权威位移查烘焙帧表**；不引入泛化 `Animation` 播放器类  
> 适用仓库：ACTGame；在锁步重构进入 L2 时作为 RootMotion 改造唯一实施细则

---

## 1. 结论摘要

1. **锁步不能把运行时 Animator Root Motion 当权威**；可用动画里的运动**数据**（烘焙），不能用播放时采样的 `deltaPosition`。
2. 每个可驱动位移的动作在运行时仍是一对数据：
   - **Presentation Clip**：InPlace  
   - **MotionTable**：60Hz 逐帧 Δ（scaled-int）  
   但这对数据**不是策划手配出来的**，而是工具从现有源 Clip **批量生成并自动回写**。
3. **工作流定案（反手工）：以 `ActionDefinition` 为唯一入口**——人只维护现在就会配的「源动画 / 段」；一键或脏标记批烘后，表与 InPlace 引用自动写入，禁止「逐 Clip 烘完再逐个 Action 拖引用」成为主流程。
4. **不要**新建名为 `Animation` 的运行时大类；表数据优先**内嵌在 ActionDefinition**（或按约定路径旁路生成并自动挂上），避免多一份要手配的 SO。
5. 落地挂在帧同步 **L2**；烘焙工具可与 L0/L1 并行；运行时查表以 `ActionSim.currentFrame` 为准。

---

## 2. 问题与动机

### 2.1 现状（帧同步视角）

```text
Action / Locomotion
  → 播带 Root Motion 的 Clip
  → OnAnimatorMove / CharacterRootMotionDriver
  → CharacterController.Move
```

| 风险 | 说明 |
|------|------|
| 非确定性 | 依赖 Animator 采样、混合、卡肉 speed、PhysX/CC |
| 逻辑绑表现 | 表现 CrossFade 会影响权威位移 |
| 跨端不可复现 | 同输入无法保证同位置 |
| 与固定逻辑帧冲突 | RM 跟播放会话时间，不是稳定 `frameIndex` |

### 2.2 目标形态

```text
【权威 Simulation】
  actionFrame / locomotionFrame (int)
  → MotionTable[frame] → Δxz / Δyaw (scaled-int)
  → 按 facing 转到世界 → MotorSim.TryMove

【表现 Presentation】
  同一 frame → 选 InPlace Clip + 局部时间
  → CharacterAnimationService.PlayClip / Seek
  → 模型跟 Snapshot 插值（已有 CharacterPresentationBridge）
```

---

## 3. 设计原则

| 原则 | 说明 |
|------|------|
| 单一逻辑频率 | 表采样率 = `SimulationConfig.LogicHz`（60）；与 Timeline 迁移后一致 |
| 数据配对，职责分离 | Clip 只服务表现；Table 只服务逻辑；禁止运行时互相推导权威 |
| **配置零增量** | 策划/程序日常不增加「配 MotionTable」步骤；只配源 Clip（现状），其余机器生成 |
| **Action 入口批烘** | Baker 读 `ActionDefinition`（段、时长、Timeline 帧数），写回同资产；不以「单 Clip 窗口」为主入口 |
| 不新建 Animation 播放器 | 播放仍走 `CharacterAnimationService`；新增的是**资产与查表 API** |
| 源 Clip 可保留 RM | 美术继续按 RM 制作；工具从源 Clip 生成 InPlace + 表 |
| 无双轨长期并存 | 某招式接入表后，删除该路径上的逻辑 `OnAnimatorMove` |
| Agent 不直接改 `.asset` | 批量烘焙由 Editor 菜单执行；人只点「Bake」或处理校验失败列表 |

---

## 4. 数据模型

### 4.0 人配什么 vs 机器写什么（先读）

| 角色 | 做什么 |
|------|--------|
| 人（现状几乎不变） | 在 `ActionDefinition` 上指定**源** Clip / 段 / Timeline（和现在做招一样） |
| 机器（Baker） | 读源 Clip → 生成/覆盖 InPlace Clip 与逐帧 Δ → **自动写回** Definition |
| 人（例外） | 只处理「校验失败」列表：某招误差超标时打开报告修源动画或调阈值 |

**禁止成为主流程：** 单独打开 Baker → 选一个 Clip → 生成 SO → 再回到 ActionDefinition 拖进字段。

### 4.1 运动表数据：优先内嵌，避免第二份手配资产

**定案 A（推荐）：表数据嵌在 `ActionDefinition` 内**

```text
ActionDefinition
├─ animationSegments[]          // 人配：sourceClip（可含 RM）+ 帧区间
├─ bakedMotion                  // 机器写：整招一张表（可序列化 class，不必独立 SO）
│   ├─ logicHz / frameCount
│   ├─ positionDeltaMm[]
│   ├─ yawDeltaMilliDeg[]
│   ├─ sourceContentHash        // 源 Clip+时长指纹，用于脏检测
│   └─ bakeStatus (None/Ok/Failed)
├─ （Editor）presentationClips[] // 机器写：各段 InPlace 引用；或覆盖 segment.clip
└─ timeline / graph（既有）
```

运行时只读 `bakedMotion`；Inspector 上 `bakedMotion` **只读展示**（帧数、累计位移、上次烘焙时间），不提供「拖另一个 Table」作为主操作。

**定案 B（备选）：旁路自动资产**

若表很大、想单独 diff：

```text
Assets/.../Unagi_Attack_01.asset
Assets/.../Unagi_Attack_01.Motion.asset   // 约定路径，Baker 创建/覆盖
```

Baker 写完后 `definition.motionTable = 该资产`，**仍禁止手拖**。缺文件或指纹不一致 → 标红「需 Bake」。

本方案默认 **定案 A**；仅在表体积或版本控制有压力时用 B。

### 4.2 核心结构：`ActionBakedMotion`（可内嵌）

```text
ActionBakedMotion（Serializable，非独立播放器类）
├─ logicHz = 60
├─ frameCount                 // 与权威动作帧数对齐
├─ space = LocalCharacter
├─ planarMode
├─ positionDeltaMm[]
├─ yawDeltaMilliDeg[]
├─ sourceContentHash
└─ bakeStatus
```

**InPlace 招式**：Δ 全 0。  
**有位移招式**：Δ 来自源 RM 差分。

查表 API：

```text
bool TryGetDelta(int frame, out SimVec2 deltaMm, out int yawMilliDeg)
// 越界钳制到 last（Editor 可告警）
```

### 4.3 段与整招表

```text
ActionDefinition
├─ animationSegments[]     // 人：源 Clip；Bake 后可把播放引用切到 InPlace
└─ bakedMotion             // 机器：整招一张表（主路径）
```

多段：Baker 按权威时间轴**拼接**各段源 RM 差分进同一张 `bakedMotion`，Cancel 只认一个 `currentFrame`。  
不要求「一段一表、一段一手配」。

### 4.3 Locomotion：同样「Profile 入口 + 自动回写」

人只维护 `CharacterLocomotionProfile` / `AnimationKey → 源 Clip`（现状）。  
`Bake Locomotion Profile` 把各 Key 的 Δ 与 InPlace **写回 Profile 内嵌字段**，不另手配 Table 库。

```text
CharacterLocomotionProfile
├─ key → sourceClip（人配）
└─ bakedEntries[]（机器写：Δ 数组 + InPlace 引用 + hash）
```

废弃运行时 `NormalizedTime` 浮点采样作为权威。

### 4.4 明确不引入的类型

| 不采用 | 原因 |
|--------|------|
| `class Animation` 同时持 Clip + 表 + Play | 与 `CharacterAnimationService` 重叠，易把逻辑又绑回播放 |
| 长期平行 `SimAnimation` vs 旧 Clip 双字段无删除期限 | 违反 no-legacy |
| 运行时从 InPlace Clip 反推位移 | InPlace 无根位移，推不出来 |

若需命名「绑定」调试结构，可用 Editor-only：

```text
SimClipBakeResult（Editor）
├─ inplaceClip
├─ motionTable
└─ errorReport
```

不进入 Runtime Domain 播放路径。

---

## 5. 烘焙管线

### 5.1 输入 / 输出

```text
输入：
  源 AnimationClip（允许含 Root Motion）
  logicHz = 60
  planarMode、是否导出 yaw、是否生成 InPlace Clip

输出：
  1) Presentation Clip（InPlace，自动生成并挂回）
  2) `ActionDefinition.bakedMotion`（或旁路自动资产）
  3) 校验报告（累计位移误差、最大单帧误差）
```

### 5.2 算法要点（逻辑表）

```text
for frame in 0 .. totalFrames-1:
  t0 = frame / logicHz
  t1 = (frame+1) / logicHz
  sample root transform at t0, t1（或曲线差分）
  deltaLocal = Inverse(rootYaw0) * (pos1 - pos0)   // 水平
  quantize to mm
  yawDelta = wrap(yaw1 - yaw0) → milli-deg
```

注意：

- 与 Timeline `TotalFrames` / 段边界一致；Action 30Hz 资产须先按锁步方案迁到 60Hz 再烘焙，或烘焙工具内按迁移规则倍帧。
- 循环 Locomotion：最后一帧到第一帧的衔接误差写入报告，必要时手动修末帧。
- ForwardOnly：烘焙后或查表时把 Δ 投影到本地 +Z（与现有 `RootMotionPlanarMode` 语义对齐，选一处做，避免双投影）。

### 5.3 InPlace Clip 生成策略

任选其一（项目定一，工具写死）：

| 策略 | 说明 |
|------|------|
| **A. bakeIntoPose + 清 root 曲线** | 根位移进骨骼，根曲线置 0（常见） |
| **B. 复制 Clip 后删除 RootT/RootQ** | 脚下可能滑动，需美术验收 |
| **C. 暂用源 Clip 仅表现，逻辑已信表** | 过渡期允许；可能「滑步」视觉，L2 验收前应切到 A/B |

**推荐默认 A**，与「表现 InPlace + 逻辑表位移」最干净。

### 5.4 Editor 工具入口（低摩擦主路径）

```text
【主入口 — 用这个】
ActionDefinition Inspector
  [Bake Motion]              // 烘当前招，写回自身
  [Bake Motion + InPlace]

菜单
  ACTGame / Motion / Bake All Actions In Project
  ACTGame / Motion / Bake Dirty Actions Only
  ACTGame / Motion / Bake Selected Actions

【副入口 — 调试用】
  单 Clip 预览烘焙（不写 Action，仅看曲线/误差）
```

批处理伪代码：

```text
BakeAction(def):
  sources = def.animationSegments 的 sourceClip + 帧区间
  table = SampleAndQuantize(sources, logicHz=60)
  report = CompareToSourceRootMotion(sources, table)
  if report.ok:
      def.bakedMotion = table
      for each segment:
          def.segments[i].playClip = EnsureInPlaceClip(segment.sourceClip)
      def.bakedMotion.bakeStatus = Ok
      SetDirty(def); Save
  else:
      def.bakedMotion.bakeStatus = Failed
      记入失败列表（不打断整批，最后弹窗汇总）

BakeDirty:
  for def in all ActionDefinitions:
      if Hash(sources) != def.bakedMotion.sourceContentHash:
          BakeAction(def)
```

脏检测：源 Clip 导入/修改、段帧区间变更、logicHz 变更 → `sourceContentHash` 不匹配 → Inspector 黄条「Motion dirty — Bake」或进 `Bake Dirty` 队列。

可选增强（后期）：

- `OnPostprocessAllAssets`：Clip 变更时自动把引用到的 Action 标 dirty（不立刻全量烘，避免导入卡顿）  
- CI：`bakeStatus != Ok` 或 dirty 则构建失败  

复用 / 扩展现有 `LocomotionRootMotionBaker`，抽出 `RootMotionBakeUtility`（Editor）。

### 5.5 校验门槛（建议）

| 指标 | 建议阈值（可调） |
|------|------------------|
| 水平累计误差 | &lt; 2cm / 招（或相对位移 &lt; 1%） |
| 单帧最大误差 | &lt; 5mm |
| yaw 累计 | &lt; 1°（若启用 yaw 表） |

超阈值：该招 `bakeStatus = Failed`，**整批其它招继续**；结束弹出失败列表。不要求人先手建 Table。

---

## 5.6 目标体验（对照「太麻烦」）

| 麻烦做法（不要） | 本方案做法 |
|------------------|------------|
| 每个动画单独开窗口烘焙 | `Bake All` / `Bake Dirty` 扫全部 ActionDefinition |
| 烘完再去 Action 上拖 Table | **自动写回** `bakedMotion` / 旁路资产引用 |
| 每段再配一张表 | 整招一张表，段只提供源 Clip |
| 换 Clip 后忘记更新表 | 指纹 dirty + 黄条 / CI 拦截 |
| 为锁步多学一套配置语言 | 人侧配置面 ≈ 现在（源 Clip + Timeline） |

---

## 6. 运行时集成（基于帧同步）

### 6.1 与 Simulation 的关系

```text
SimulationWorld.Step(frame)
  → Character / ActionSim
       currentFrame++
       if bakedMotion.bakeStatus == Ok:
           d = bakedMotion.Get(currentFrame)
           worldDelta = Rotate(facing, d)
           MotorSim.TryMove(worldDelta)      // L2：确定性；过渡可 IMotorSim→CC
       else:
           assert / Editor 拦截：未 Bake 成功禁止进包
  → PresentationBridge
       按 Snapshot.actionId/frame 播 InPlace Clip
```

### 6.2 Action 路径

| 旧 | 新 |
|----|----|
| `CharacterRootMotionDriver` 在逻辑路径读 Animator | 删除逻辑路径；Driver 可删或仅 Editor 预览 |
| `ActionExecutor` 间接依赖 RM | `ActionSim` 查 `bakedMotion` |
| Movement NotifyState 的 `speed * dt` | 改为帧表或「附加帧位移」配置（整数），禁止裸 float dt 权威 |

Cancel / 换招：

- 旧招结束在帧 N，新招从下一 World 帧 frame 0（与锁步方案帧边界一致）。
- 查新表 index 0，不混用旧表残余。

### 6.3 Locomotion 路径

```text
LocomotionSim
  → 相位 + gaitFrame
  → table[key].Get(gaitFrame % len) → MotorSim
Presentation
  → Play(AnimationKey) 对应 InPlace Clip
```

逻辑 wish 速度与表位移关系定案：

- **表驱动步态**（Run/Walk 循环）：水平位移以表为准，输入只影响朝向/相位切换。  
- 或 **输入速度 + 零表**（纯程序移动）：仅 InPlace 动画。  
- 同一 Key 不得「表 + 程序速度」双加，避免双倍移动。

### 6.4 HitStop / 暂停

- 逻辑 `freezeFrames > 0`：不推进 `currentFrame`，**不取下一帧 Δ**（本帧位移 0）。  
- 表现：动画 Speed=0，与现方案一致。

### 6.5 表现桥

- 已有 `CharacterPresentationBridge` 负责 Pose 插值；本方案不改其职责。  
- 动画 Seek 按 `actionFrame / logicHz` 换算本地时间；Clip 必须是 InPlace，避免视觉根与逻辑根双重位移。

---

## 7. 目录与代码落点

```text
Assets/Scripts/Domain/Combat/Actions/Definitions/   // 或 Simulation/Action/
  ActionBakedMotion.cs          // 可序列化表数据 + TryGetDelta
  （嵌在 ActionDefinition 字段 bakedMotion）

Assets/Scripts/Domain/Simulation/Locomotion/
  LocomotionBakedMotion.cs      // Profile 内嵌或旁路，同样自动回写

Assets/Scripts/Editor/Combat/Motion/
  RootMotionBakeUtility.cs
  ActionMotionBakeService.cs    // BakeAction / BakeAll / BakeDirty
  ActionDefinitionMotionInspector.cs  // Bake 按钮 + dirty 黄条
  LocomotionMotionBakeService.cs
```

InPlace Clip 约定输出目录（机器生成，可进版本库）：

```text
Assets/Art/.../Generated/InPlace/   // 或与源 Clip 同目录 *.InPlace.anim
```

修改点（概念清单）：

| 模块 | 改动 |
|------|------|
| `ActionDefinition` / Segment | 增加 `bakedMotion`（机器字段）；源 Clip 仍人配；Bake 后播放指向 InPlace |
| `ActionExecutor` → `ActionSim` | 逻辑位移改查 `bakedMotion` |
| `CharacterRootMotionDriver` | 移出逻辑权威；阶段末删除 |
| `CharacterMotor` / `MotorSim` | 接收 actionDelta 来自表 |
| `CharacterAnimationService` | 只播 InPlace；不读 RM 写 Motor |
| `Locomotion*` | 相位帧索引 + 自动烘焙表 |

---

## 8. 分阶段实施

### Phase M0 — 烘焙原型（可与 L0/L1 并行）

- [ ] `ActionBakedMotion` 内嵌字段 + EditMode 序列化测试  
- [ ] `BakeAction(def)`：读段源 Clip → 写回 `bakedMotion` + 生成 InPlace  
- [ ] Action Inspector 上 **一个 Bake 按钮**（不是先配 Table）  
- [ ] 校验报告；一条测试攻击招跑通  

**验收：** 测试招上只点 Bake，无需手建/手拖任何 MotionTable 资产；误差在门槛内。

### Phase M1 — 单招切逻辑查表（依赖 L1 整数帧或固定 60Hz Step）

- [ ] 测试招：按 `currentFrame` 查 `bakedMotion` 驱动 Motor  
- [ ] 该招禁用逻辑 `OnAnimatorMove`  
- [ ] 表现改播 Bake 生成的 InPlace  

**验收：** 关闭 Animator 写入位移后，逻辑位置仍按表前进。

### Phase M2 — Action 批量迁移（主效率阶段）

- [ ] `Bake All` / `Bake Dirty`  
- [ ] 源变更指纹 dirty + Inspector 黄条  
- [ ] CI：dirty 或 `bakeStatus != Ok` 失败  
- [ ] 删除 Action 逻辑 RM 路径  

**验收：** 全项目战斗招一键 Bake Dirty 可完成；**无**「逐条手配 Table」步骤；回放轨迹稳定。

### Phase M3 — Locomotion 表化

- [ ] 扩展 Locomotion Baker → `LocomotionMotionTable`  
- [ ] 相位用整数帧索引  
- [ ] 删除 Locomotion 逻辑 RM / NormalizedTime 权威采样  

**验收：** Walk/Run 循环无漂移踩步；与锁步 L2 验收合并。

### Phase M4 — 收口

- [ ] 删除 `CharacterRootMotionDriver` 逻辑用法  
- [ ] `UseRootMotion` 策略迁为「是否使用运动表 / planarMode」  
- [ ] 内容 Hash 纳入 `bakedMotion`  

---

## 9. 与帧同步各 Phase 的依赖

```text
L0A 固定时钟          ← 已具备（feat_FrameSync）
L0B 输入量化          ← 不阻塞烘焙
L1  Action 整数帧     ← 查表索引依赖（强烈建议 M1 前完成）
L2  脱表现位移/命中   ← 本方案主体（M1–M3）
L3  Hash / 定点收紧   ← 表已是 scaled-int，利于 Hash
L4  表现完全跟随      ← InPlace Seek 按 frame
L5  联网              ← 位移已确定性后才能锁步
```

```text
建议顺序：
  M0 烘焙原型（现在可做）
  → L1 整数帧
  → M1 单招切表
  → M2/M3 批量
  → L2 其余（Hitbox 逻辑坐标、MotorSim）
```

---

## 10. 风险与对策

| 风险 | 对策 |
|------|------|
| InPlace 后脚滑 | 优先 bakeIntoPose；美术抽查；表与 Clip 同源同帧率 |
| 30Hz Timeline 与 60Hz 表不一致 | 先迁 60Hz 再烘焙；禁止两套帧率解释 |
| Cancel 后位移突变 | 遵守「新招下一 World 帧从 frame 0」；不做同帧多表混合 |
| 表与旧 RM 手感差 | 校验报告 + 关键招人工修帧；允许少量帧手工调 Δ |
| 双倍移动（表+程序速度） | 代码审查：同一控制权只走一条水平位移源 |
| 资产膨胀 | 表用 short/int 紧凑数组；Locomotion 循环轨共享 |

---

## 11. 明确非目标

- 不实现完整运行时「从任意 Clip 动态提取 RM」作为权威  
- 不引入 Action 内层动画状态机  
- 不在本方案内完成 MotorSim 碰撞网格（见锁步 L2，可并行）  
- 不要求一阶段全招完美；先一条测试招打通管线  

---

## 12. 决策记录

| 日期 | 决策 | 理由 |
|------|------|------|
| 2026-07-31 | 表现 InPlace + 逻辑 MotionTable 配对 | 锁步确定性与手感来源分离 |
| 2026-07-31 | 不新建泛化 Animation 运行时类 | 避免与 AnimationService 重叠；扩展既有 Definition/Segment |
| 2026-07-31 | Action 整招一张 motionTable 为主 | 与 `currentFrame` 权威对齐，简化 Cancel |
| 2026-07-31 | 位移单位 scaled-int 毫米 | 与锁步 L2/L3 一致，便于 Hash |
| 2026-07-31 | 烘焙源可为含 RM 的原 Clip | 降低美术管线冲击 |
| 2026-07-31 | 表内嵌 ActionDefinition + Bake 自动回写 | 避免逐动画手烘、逐 Action 手配 Table |
| 2026-07-31 | 主入口为 Bake All / Dirty，单 Clip 仅调试 | 配置零增量，人侧仍只维护源 Clip |

---

## 13. 一句话

把 Root Motion 从「**播放时采出来的位移**」改成「**制作时烤进表的逐帧 Δ**」；对人只保留「配源动画 + 点 Bake/Bake Dirty」，**禁止**逐 Clip 手烘再逐 Action 拖配置——InPlace 与表都是机器写回 `ActionDefinition` 的产物。
