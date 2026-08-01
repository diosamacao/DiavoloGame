# InPlace Clip + RootMotion 位移表改造方案

> 基准：帧同步方案 [ACTION_SYSTEM_LOCKSTEP_REFACTOR_PLAN.md](./ACTION_SYSTEM_LOCKSTEP_REFACTOR_PLAN.md)（尤其 Phase L2）  
> 制定日期：2026-07-31 · **修订：2026-08-01**  
> 目标：逻辑位移与动画播放解耦——**表现播已有 InPlace Clip，权威位移查从 RootMotion Clip 烘焙的帧表**；不引入泛化 `Animation` 播放器类  
> 适用仓库：ACTGame；在锁步重构进入 L2 时作为 RootMotion 改造唯一实施细则

---

## 1. 结论摘要

1. **锁步不能把运行时 Animator Root Motion 当权威**；可用动画里的运动**数据**（烘焙），不能用播放时采样的 `deltaPosition`。
2. 每个可驱动位移的动作在运行时仍是一对数据：
   - **Presentation Clip**：项目中**已有**的 InPlace 动画（美术交付，工具不生成）
   - **MotionTable**：60Hz 逐帧 Δ（scaled-int），从**配对的 RootMotion Clip**采样烘焙
3. **配对规则**：按文件/Clip 命名部分匹配。例：InPlace `Attack_01_Inplace` ↔ RootMotion `Unagi|Attack_01`（或同 stem 的 `Attack_01`）。
4. **烘焙主入口**：Editor 窗口选择 **InPlace 文件夹** + **RootMotion 文件夹**，在两目录内自动扫描、匹配、烘焙；不从 RM 反向生成 InPlace。
5. **不要**新建名为 `Animation` 的运行时大类；表数据优先**内嵌在 ActionDefinition / Profile** 并自动写回，避免多一份手配 SO。
6. 落地挂在帧同步 **L2**；烘焙工具可与 L0/L1 并行；运行时查表以 `ActionSim.currentFrame` 为准。

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

### 2.2 资产现状（本修订的前提）

项目已分目录交付两套动画（以 Unagi 为例）：

```text
Assets/Art/Arts/Unagi/Inplace/     // 表现用，无根位移（或根位移已剥离）
Assets/Art/Arts/Unagi/RootMotion/  // 含根位移，仅作烘焙源，不进运行时权威
```

命名惯例：

| 侧 | 示例 |
|----|------|
| InPlace 文件 / Clip | `Attack_01_Inplace.fbx` → Clip `Attack_01_Inplace` |
| RootMotion 文件 / Clip | `Attack_01.fbx` → Clip 常为 `Unagi\|Attack_01` 或 `Attack_01` |

旧方案「从 RM Clip **生成** InPlace」作废：美术已提供 InPlace，Baker **只读引用、不写出新 Clip**。

### 2.3 目标形态

```text
【权威 Simulation】
  actionFrame / locomotionFrame (int)
  → MotionTable[frame] → Δxz / Δyaw (scaled-int)   // 来自 RootMotion 烘焙
  → 按 facing 转到世界 → MotorSim.TryMove

【表现 Presentation】
  同一 frame → 选已有 InPlace Clip + 局部时间
  → CharacterAnimationService.PlayClip / Seek
  → 模型跟 Snapshot 插值（已有 CharacterPresentationBridge）
```

---

## 3. 设计原则

| 原则 | 说明 |
|------|------|
| 单一逻辑频率 | 表采样率 = `SimulationConfig.LogicHz`（60）；与 Timeline 迁移后一致 |
| 数据配对，职责分离 | InPlace 只服务表现；Table 只服务逻辑；禁止运行时互相推导权威 |
| **InPlace 现成、不生成** | Baker 绝不 `CreateAsset` / bakeIntoPose 产出新 InPlace；缺 InPlace 则报失败 |
| **位移只信 RootMotion** | Δ 只从配对到的 RM Clip 采样；InPlace 上若有根曲线也忽略 |
| **双文件夹匹配** | 烘焙时显式指定 InPlace 根目录与 RootMotion 根目录，只在二者范围内找配对 |
| **命名自动映射** | 人无需手拖「InPlace ↔ RM」对；匹配失败进报告 |
| 表自动写回 | Bake 成功后写回 Definition / Profile；禁止「烘完再逐 Action 手拖 Table」成主流程 |
| 不新建 Animation 播放器 | 播放仍走 `CharacterAnimationService`；新增的是资产与查表 API |
| 无双轨长期并存 | 某招式接入表后，删除该路径上的逻辑 `OnAnimatorMove` |
| Agent 不直接改 `.asset` | 批量烘焙由 Editor 菜单/窗口执行；人只点 Bake 或处理失败列表 |

---

## 4. 数据模型

### 4.0 人配什么 vs 机器写什么

| 角色 | 做什么 |
|------|--------|
| 美术 | 交付成对目录：InPlace + RootMotion，命名遵守 stem 约定 |
| 人（策划/程序） | 在 `ActionDefinition` / Locomotion Profile 上指定**InPlace** Clip（表现源）；Timeline / 段帧区间照旧 |
| 人（烘焙） | 打开 Bake 窗口，选 InPlace 文件夹 + RootMotion 文件夹，点 Bake |
| 机器（Baker） | 按命名匹配 → 从 RM 采样 Δ → 写回 `bakedMotion`；**不生成** InPlace |
| 人（例外） | 只处理匹配失败 / 校验失败列表 |

**禁止成为主流程：**

- 从 RM 生成 InPlace 再挂回  
- 单独烘一个 Table SO 再手拖进 Action  
- 手填「RM Clip 引用」字段作为日常操作（调试预览除外）

### 4.1 运动表数据：优先内嵌

**定案 A（推荐）：表数据嵌在 `ActionDefinition` 内**

```text
ActionDefinition
├─ animationSegments[]          // 人配：InPlace clip + 帧区间（表现源）
├─ bakedMotion                  // 机器写：整招一张表
│   ├─ logicHz / frameCount
│   ├─ positionDeltaMm[]
│   ├─ yawDeltaMilliDeg[]
│   ├─ inplaceContentHash       // InPlace Clip 指纹
│   ├─ rootMotionContentHash    // 配对 RM Clip 指纹
│   ├─ matchedRootMotionName    // 调试：实际匹配到的 RM Clip 名
│   └─ bakeStatus (None/Ok/Failed)
└─ timeline / graph（既有）
```

运行时只读 `bakedMotion`；Inspector 上只读展示（帧数、累计位移、匹配到的 RM 名、上次烘焙时间）。

**定案 B（备选）：旁路自动资产** — 表很大时用约定路径旁路 SO，仍禁止手拖。默认 **定案 A**。

### 4.2 核心结构：`ActionBakedMotion`

```text
ActionBakedMotion（Serializable，非独立播放器类）
├─ logicHz = 60
├─ frameCount
├─ space = LocalCharacter
├─ planarMode
├─ positionDeltaMm[]
├─ yawDeltaMilliDeg[]
├─ inplaceContentHash
├─ rootMotionContentHash
├─ matchedRootMotionName
└─ bakeStatus
```

| 情况 | 表内容 |
|------|--------|
| 有位移招式 | Δ 来自配对 RM Clip 差分 |
| 纯站桩 / 无 RM 配对且策略允许零表 | Δ 全 0（须显式策略或报告，避免静默丢位移） |

查表 API：

```text
bool TryGetDelta(int frame, out SimVec2 deltaMm, out int yawMilliDeg)
// 越界钳制到 last（Editor 可告警）
```

### 4.3 段与整招表

```text
ActionDefinition
├─ animationSegments[]     // 人：InPlace Clip
└─ bakedMotion             // 机器：整招一张表（各段按权威时间轴拼接对应 RM 的 Δ）
```

多段：每段用该段 InPlace 的 stem 去 RootMotion 文件夹找配对，再拼接进同一张 `bakedMotion`。

### 4.4 Locomotion：同样「Profile + 自动回写」

人维护 `AnimationKey → InPlace Clip`。  
`Bake Locomotion Profile`：用同一套命名匹配在指定 RootMotion 文件夹取 Δ，写回 Profile 内嵌字段。

```text
CharacterLocomotionProfile
├─ key → inplaceClip（人配）
└─ bakedEntries[]（机器写：Δ 数组 + matched RM 名 + hash）
```

### 4.5 明确不引入的类型 / 流程

| 不采用 | 原因 |
|--------|------|
| 从 RM **生成** InPlace Clip | 美术已交付；生成会造成双份表现源 |
| `class Animation` 同时持 Clip + 表 + Play | 与 AnimationService 重叠 |
| 运行时从 InPlace 反推位移 | InPlace 无可用根位移 |
| 人手拖 RM 引用作为主配置 | 命名匹配已覆盖；手拖仅调试 |

调试结构（Editor-only）：

```text
SimClipBakePair（Editor）
├─ inplaceClip
├─ rootMotionClip      // 匹配结果
├─ motionTable
└─ errorReport
```

---

## 5. 烘焙管线

### 5.1 输入 / 输出

```text
输入：
  InPlace 文件夹（DefaultAsset / 路径）
  RootMotion 文件夹（DefaultAsset / 路径）
  可选：过滤子集（Selected Clips / 引用到的 ActionDefinition）
  logicHz = 60
  planarMode、是否导出 yaw

输出：
  1) 匹配报告（成功对 / 未匹配 InPlace / 未匹配 RM / 歧义）
  2) 每对成功：从 RM 采样的 MotionTable → 写回引用该 InPlace 的 Action/Profile
  3) 校验报告（相对 RM 源的累计/单帧误差）
  —— 不输出、不覆盖任何 InPlace Clip 资产 ——
```

### 5.2 命名匹配规则（定案）

**目标示例**

| InPlace | 规范化 stem | RootMotion 候选（命中其一即可） |
|---------|-------------|-------------------------------|
| `Attack_01_Inplace` | `Attack_01` | `Attack_01`、`Unagi\|Attack_01` |
| `Attack_01_End_Inplace` | `Attack_01_End` | `Attack_01_End`、`Unagi\|Attack_01_End` |

**步骤**

```text
1) 收集 InPlace 文件夹（含子目录）内全部 AnimationClip
2) 收集 RootMotion 文件夹（含子目录）内全部 AnimationClip
3) 对每个 InPlace Clip：
   a. 取 clip.name（或资产主名），去掉末尾后缀（大小写不敏感）：
        "_Inplace" | "_InPlace" | "_inplace"
      → stem
      若无上述后缀 → 记入「命名不合规」，跳过自动匹配
   b. 在 RootMotion 集合中按优先级找唯一最佳匹配：
        P0  clip.name == stem
        P1  clip.name 以 "|" + stem 结尾（如 Unagi|Attack_01）
        P2  所属 FBX/文件名（无扩展名）== stem
        P3  部分匹配兜底：clip.name 包含 stem，且去掉角色前缀后相等
            （用于 Unagi|Attack_01 等；禁止 stem 过短导致误伤，见下）
   c. 同一优先级多个命中 → 歧义失败，不自动挑选
   d. 零命中 → 未匹配，列入报告
4) 反向：RootMotion 有、InPlace 无 → 报告「孤儿 RM」（可选警告，不阻断整批）
```

**部分匹配约束（防误匹配）**

- stem 长度建议 ≥ 3；更短必须 P0/P1/P2 精确命中  
- P3 仅当「去掉 `|` 前前缀后的 token」== stem，不得用随意 substring（避免 `Attack_01` 误配 `Attack_01_End`）  
- `Attack_01` 与 `Attack_01_End` 是不同 stem，必须各自配对

### 5.3 算法要点（逻辑表，源 = RootMotion Clip）

```text
pair = Match(inplaceClip, rootMotionClip)
for frame in 0 .. totalFrames-1:
  t0 = frame / logicHz
  t1 = (frame+1) / logicHz
  sample root transform of rootMotionClip at t0, t1
  deltaLocal = Inverse(rootYaw0) * (pos1 - pos0)
  quantize to mm
  yawDelta = wrap(yaw1 - yaw0) → milli-deg
```

注意：

- 权威帧数优先对齐 **Action Timeline / 段帧区间**；若仅文件夹批烘尚无 Action，则按 RM Clip 时长 × logicHz 生成临时表，待 Action Bake 时按段重切/重采样。  
- InPlace 与 RM **时长不一致**：以 Action 权威帧数为准重采样 RM；差值超阈值记入报告。  
- 循环 Locomotion：末帧→首帧衔接误差写入报告。  
- ForwardOnly：与现有 `RootMotionPlanarMode` 语义对齐，只在烘焙或查表一处投影。

### 5.4 ~~InPlace Clip 生成策略~~（已废弃）

| 旧策略 | 状态 |
|--------|------|
| A. bakeIntoPose + 清 root 曲线 | **废弃** — 不生成 InPlace |
| B. 复制 Clip 后删 RootT/RootQ | **废弃** |
| C. 暂用源 RM 仅表现 | **废弃** — 表现固定用已有 InPlace |

美术侧若需新招：同时导出 InPlace 与 RootMotion 到对应文件夹，命名遵守 `_Inplace` / stem 规则。

### 5.5 Editor 工具入口

```text
【主入口 — 文件夹批烘】
菜单：ACTGame / Motion / Bake From Folders…
窗口字段：
  InPlace Folder     // 例 Assets/Art/Arts/Unagi/Inplace
  RootMotion Folder  // 例 Assets/Art/Arts/Unagi/RootMotion
  planarMode / exportYaw / logicHz
  [Preview Matches]  // 只列出配对与歧义，不写资产
  [Bake Matched]     // 烘焙并写回引用这些 InPlace 的 Action/Profile
  [Bake Dirty Only]

【副入口 — Action / Profile】
ActionDefinition Inspector
  [Bake Motion]      // 用窗口记住的两文件夹（或 Inspector 上同款 Folder 字段）做匹配后写回本招
CharacterLocomotionProfile
  [Bake Locomotion Motion]

【调试】
  单对 Clip 预览（指定 InPlace + 可选手动 RM 覆盖，仅预览误差）
```

批处理伪代码：

```text
BakeFromFolders(inplaceDir, rmDir):
  pairs, failures = BuildPairs(inplaceDir, rmDir)
  show Preview / Report
  for each pair in pairs:
      consumers = FindActionsOrProfilesReferencing(pair.inplaceClip)
      table = SampleAndQuantize(pair.rootMotionClip, logicHz=60)
      report = CompareToSourceRootMotion(pair.rootMotionClip, table)
      if report.ok and consumers.Any:
          for def in consumers:
              WriteBakedMotion(def, table, pair)
      elif report.ok and !consumers.Any:
          记入「已烘焙但无引用方」（可选：写旁路预览资产，默认只报告）
      else:
          bakeStatus = Failed，记入失败列表

BakeAction(def):
  for segment in def.animationSegments:
      pair = MatchInFolders(segment.inplaceClip, rememberedFolders)
      if !pair.ok: fail segment
  table = ConcatSample(pairs, def.timeline)
  write back def.bakedMotion
```

脏检测：InPlace hash、配对 RM hash、段帧区间、logicHz 任一变更 → dirty 黄条 / 进 `Bake Dirty`。

复用现有 `LocomotionRootMotionBaker` 曲线采样，抽出 `RootMotionBakeUtility`；**删除/不实现**任何 `EnsureInPlaceClip` / 生成路径。

### 5.6 校验门槛（建议）

| 指标 | 建议阈值（可调） |
|------|------------------|
| 水平累计误差（相对 RM 源） | &lt; 2cm / 招（或相对位移 &lt; 1%） |
| 单帧最大误差 | &lt; 5mm |
| yaw 累计 | &lt; 1°（若启用 yaw 表） |
| InPlace vs RM 时长差 | &lt; 1 逻辑帧（或相对 &lt; 2%），否则警告 |

超阈值：该对 `Failed`，整批其它对继续；结束弹失败/未匹配列表。

---

## 5.7 目标体验（对照）

| 不要 | 本方案 |
|------|--------|
| 从 RM 生成 InPlace | 使用 `Inplace/` 已有资源 |
| 每个动画手拖 RM 引用 | 双文件夹 + 命名自动匹配 |
| 烘完再去 Action 拖 Table | 自动写回 `bakedMotion` |
| 为锁步多学一套配置 | 人侧：配 InPlace + 点文件夹 Bake |

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
           MotorSim.TryMove(worldDelta)
       else:
           assert / Editor 拦截：未 Bake 成功禁止进包
  → PresentationBridge
       按 Snapshot.actionId/frame 播 InPlace Clip（已有资产）
```

### 6.2 Action 路径

| 旧 | 新 |
|----|----|
| `CharacterRootMotionDriver` 在逻辑路径读 Animator | 删除逻辑路径 |
| 表现播 RM Clip | 表现播人配的 InPlace Clip |
| 逻辑位移跟 Animator | `ActionSim` 查 `bakedMotion`（RM 烘焙结果） |

Cancel / 换招：旧招结束在帧 N，新招下一 World 帧从 frame 0 查新表。

### 6.3 Locomotion 路径

```text
LocomotionSim
  → 相位 + gaitFrame
  → table[key].Get(gaitFrame % len) → MotorSim
Presentation
  → Play(AnimationKey) 对应 InPlace Clip
```

同一 Key 不得「表 + 程序速度」双加。

### 6.4 HitStop / 暂停

- 逻辑 `freezeFrames > 0`：不推进 `currentFrame`，不取下一帧 Δ。  
- 表现：动画 Speed=0。

### 6.5 表现桥

- `CharacterPresentationBridge` 职责不变。  
- Seek 按 `actionFrame / logicHz`；Clip **必须**是 InPlace，避免视觉根与逻辑根双重位移。

---

## 7. 目录与代码落点

```text
Assets/Scripts/Domain/Combat/Actions/Definitions/
  ActionBakedMotion.cs

Assets/Scripts/Domain/Simulation/Locomotion/
  LocomotionBakedMotion.cs

Assets/Scripts/Editor/Combat/Motion/
  RootMotionBakeUtility.cs          // 从 RM Clip 采样（复用 LocomotionRootMotionBaker）
  MotionClipPairMatcher.cs          // 双文件夹扫描 + 命名匹配
  FolderMotionBakeWindow.cs         // InPlace/RM 文件夹选择 + Preview/Bake
  ActionMotionBakeService.cs
  ActionDefinitionMotionInspector.cs
  LocomotionMotionBakeService.cs
```

资产目录（人维护，非 Generated）：

```text
Assets/Art/Arts/<Character>/Inplace/
Assets/Art/Arts/<Character>/RootMotion/
```

**删除旧方案中的约定：** `Assets/Art/.../Generated/InPlace/` 作为机器生成输出目录——不再作为主路径；若仓库中已有 Generated 残留，实施阶段清理引用后删除（不保留兼容生成逻辑）。

| 模块 | 改动 |
|------|------|
| `ActionDefinition` / Segment | 人配 InPlace；增加机器字段 `bakedMotion` |
| `ActionSim` | 逻辑位移查 `bakedMotion` |
| `CharacterRootMotionDriver` | 移出逻辑权威；阶段末删除 |
| `CharacterAnimationService` | 只播 InPlace |
| `Locomotion*` | 相位帧索引 + 文件夹匹配烘焙表 |

---

## 8. 分阶段实施

### Phase M0 — 匹配 + 烘焙原型（可与 L0/L1 并行）

- [ ] `MotionClipPairMatcher`：双文件夹扫描 + stem 规则 + Preview Matches  
- [ ] `ActionBakedMotion` 内嵌字段  
- [ ] `BakeFromFolders`：对匹配成功的对从 RM 采样写表（先写测试 Action 或报告）  
- [ ] **不实现**任何 InPlace 生成  
- [ ] 校验报告；一条测试攻击招跑通  

**验收：** 选择 Unagi 的 Inplace + RootMotion 两文件夹，能 Preview 出 `Attack_01_Inplace` ↔ `Unagi|Attack_01`；Bake 后测试招位移来自 RM 表，表现 Clip 仍是原 InPlace 资产。

### Phase M1 — 单招切逻辑查表

- [ ] 测试招：按 `currentFrame` 查表驱动 Motor  
- [ ] 该招禁用逻辑 `OnAnimatorMove`  
- [ ] 表现确认播的是既有 InPlace  

### Phase M2 — Action 批量迁移

- [ ] `Bake All` / `Bake Dirty`（基于文件夹记忆路径或全局约定）  
- [ ] 指纹 dirty + Inspector 黄条  
- [ ] CI：dirty / Failed / 未匹配  
- [ ] 删除 Action 逻辑 RM 路径  

### Phase M3 — Locomotion 表化

- [ ] 同一匹配器服务 Locomotion Profile  
- [ ] 相位整数帧索引  
- [ ] 删除 Locomotion 逻辑 RM / NormalizedTime 权威采样  

### Phase M4 — 收口

- [ ] 删除 `CharacterRootMotionDriver` 逻辑用法  
- [ ] `UseRootMotion` 迁为「是否使用运动表 / planarMode」  
- [ ] 清理 Generated/InPlace 残留与旧生成 API（若有）  

---

## 9. 与帧同步各 Phase 的依赖

```text
L0A 固定时钟          ← 已具备
L0B 输入量化          ← 不阻塞烘焙
L1  Action 整数帧     ← 查表索引依赖
L2  脱表现位移/命中   ← 本方案主体（M1–M3）
L3  Hash / 定点收紧   ← 表已是 scaled-int
L4  表现完全跟随      ← InPlace Seek 按 frame
L5  联网              ← 位移确定性后才能锁步
```

```text
建议顺序：
  M0 文件夹匹配 + 从 RM 烘表（现在可做）
  → L1 整数帧
  → M1 单招切表
  → M2/M3 批量
  → L2 其余
```

---

## 10. 风险与对策

| 风险 | 对策 |
|------|------|
| 命名不一致导致未匹配 | Preview Matches 先行；失败列表；禁止模糊 substring 误配 `Attack_01`/`Attack_01_End` |
| FBX 内 Clip 名为 `角色\|动作` | P1 规则专门覆盖 |
| InPlace 与 RM 时长不一致 | 重采样 + 时长差阈值警告 |
| 表与旧 RM 手感差 | 校验报告；关键招修源 RM 或少量调 Δ |
| 双倍移动（表+程序速度 / 表+Animator RM） | 接入表后禁用逻辑 RM；代码审查单源位移 |
| 误用 InPlace 曲线当位移源 | API 只接受匹配到的 RM Clip 采样 |

---

## 11. 明确非目标

- 不实现完整运行时「从任意 Clip 动态提取 RM」作为权威  
- **不**从 RootMotion 生成或改写 InPlace 资产  
- 不引入 Action 内层动画状态机  
- 不在本方案内完成 MotorSim 碰撞网格  
- 不要求一阶段全招完美；先一条测试招 + 文件夹 Preview 打通  

---

## 12. 决策记录

| 日期 | 决策 | 理由 |
|------|------|------|
| 2026-07-31 | 表现 InPlace + 逻辑 MotionTable 配对 | 锁步确定性与手感来源分离 |
| 2026-07-31 | 不新建泛化 Animation 运行时类 | 避免与 AnimationService 重叠 |
| 2026-07-31 | Action 整招一张 motionTable 为主 | 与 `currentFrame` 对齐，简化 Cancel |
| 2026-07-31 | 位移单位 scaled-int 毫米 | 与锁步 L2/L3 一致 |
| 2026-07-31 | 表内嵌 ActionDefinition + Bake 自动写回 | 避免逐 Action 手配 Table |
| **2026-08-01** | **使用已有 InPlace，禁止 Baker 生成 InPlace** | 美术已交付 `Inplace/`；避免双份表现源与生成漂移 |
| **2026-08-01** | **Δ 只从 RootMotion Clip 烘焙** | InPlace 无可用根位移；位移手感以 RM 为准 |
| **2026-08-01** | **双文件夹选择 + 命名部分匹配自动配对** | `Attack_01_Inplace` ↔ `Unagi\|Attack_01`；降低手配成本 |
| **2026-08-01** | **Bake 主入口改为 Folder 窗口** | 在指定 InPlace/RM 目录内扫描匹配；Action Bake 复用同一匹配器 |

---

## 13. 一句话

把 Root Motion 从「**播放时采出来的位移**」改成「**制作时从 RM Clip 烤进表的逐帧 Δ**」；表现始终播**已有** InPlace，Baker 只负责在选定的 InPlace / RootMotion 两文件夹里按命名自动配对并写回运动表——**不生成 InPlace，不手配 Table**。
