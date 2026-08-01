# ACTGame 动作系统帧同步导向重构方案

> 基准：`develop`（ActionGraph / Timeline / CharacterActor / 敌人 AI 初版已落地）  
> 制定日期：2026-07-30  
> 最近实施：2026-08-01（L0A/L0B/L0C/L1A 代码完成；第 2 节保留重构前基线诊断）
> 目标：以**帧同步（Lockstep）多人 PVE**为未来方向，重构动作与角色模拟核；保留现有选招/窗口/意图语义  
> 相关文档：[ENEMY_SYSTEM_INTEGRATION_PLAN.md](./ENEMY_SYSTEM_INTEGRATION_PLAN.md)、[ENEMY_BEHAVIOR_TREE_PLAN.md](./ENEMY_BEHAVIOR_TREE_PLAN.md)

---

## 1. 结论摘要

1. **现状适合单机 ACT，不适合直接帧同步**；差距在时钟、确定性数学、逻辑/表现耦合，不在「有没有 Graph」。
2. **可保留**：`GameplayIntent`、ActionGraph、Cancel/Phase 窗口语义、外层控制权（Locomotion/Action/Hit/Death）、AI 只产意图。
3. **必须重构**：`Tick(deltaTime)` → **固定逻辑帧**；Root Motion / `CharacterController` 退出逻辑权威；Hitbox 与位移改为**整数帧 + 确定性几何**。
4. **目标架构**：`Simulation`（确定性）与 `Presentation`（可抖）分离；网络层最后接，先让单机跑在同一套逻辑核上。
5. **迁移策略**：分阶段按职责直接切换，每阶段可玩验收；同一职责一旦接入新核，立即删除旧入口、旧字段与旧调用链。
6. **参考边界**：`DemoClient` / `DemoServer` 是「客户端权威玩家 + 服务端权威怪物 + 状态广播」的混合同步，**不是 Lockstep**；只借鉴 Room、远端 View 代理与运动烘焙，不复制其 Transform/伤害上报链。

---

## 2. 现状诊断（帧同步视角）

| 模块 | 现状 | 帧同步风险 |
|------|------|------------|
| 世界时钟 | 玩家与每个敌人分别由 `MonoBehaviour.Update` 调 `Tick(Time.deltaTime)` | Actor 更新顺序依赖 Unity，端间不可复现 |
| 招式时间 | `ActionSession.ElapsedSeconds` 是权威；`CurrentFrame` 仅由秒换算 | Graph、Rotation、Movement、Segment、结束判定仍受 float 影响 |
| Runtime / Editor | Runtime 走 `Tick(dt)`；`UpdateFrame` 无调用；Editor 独立采样动画 Pose | 三条推进语义不等价，不能直接视为统一 Logic Tick |
| 位移 | Root Motion → `CharacterController.Move` | PhysX/CC 非确定性 |
| 动画 | Playable 直接驱动逻辑位移 | 逻辑依赖表现采样 |
| Hitbox / 受击 | Action 帧回调内立即查 Transform/Physics 并同步改变目标状态 | 依赖骨骼 Pose 与 Actor Tick 先后顺序 |
| 输入 | 已有 `PlayerInputFrame`，但含 float `Vector2` / 字符串离散输入；Hold 与 Buffer 用秒 | 需直接演进为量化、带帧号的统一输入格式，禁止平行双类型 |
| Locomotion | 内层 FSM 正确；相位、转向、烘焙轨采样仍用 float/NormalizedTime | 需整数帧计时、量化朝向与按帧运动表 |
| AI | Brain → `AIInputSource` → `PlayerInputFrame`，冷却与 FacingProxy 为 float | 方向正确；须在 World 内按逻辑帧产同格式输入 |
| HitStop | 配置已有 `hitStopFrames`，运行时却用 `Time.unscaledDeltaTime` 换算秒 | 只暂停攻击者 Action/动画，不是确定性逻辑冻结 |
| 身份 | 命中去重使用 Unity `GetInstanceID()` | 跨进程不稳定，不能进入 Hash |
| Timeline 频率 | Action 默认 30Hz；Locomotion 烘焙默认 60Hz | 与全局 Simulation Hz 未定义映射 |
| 网络 | 无 | 需在逻辑核稳定后接入 |

**已具备的帧同步友好点：**

- Intent / Buffer / Driver / Resolver 分层清晰。
- Timeline 已有 `SampleRate`、`TotalFrames` 与窗口 `IsActiveAtFrame`；Cancel / Phase / Hitbox 已部分帧化。
- `ActionGraph`、Timeline 窗口语义、外层四态与 AI 只产输入的边界可以保留。
- Locomotion 已有运动烘焙工具与运行时轨，可作为逐帧运动表的原型，但现有 NormalizedTime 采样不能直接作为确定性运行时。

**必须纠正的现状认知：**

- `UpdateFrame` 目前不是生产入口，也没有与 Editor Scrub 共用；目标是**建立**单一 `Step`，不是给现有 API 改名。
- `ActionExecutor` 直接依赖动画、Transform、Root Motion 与 `CharacterController`，不能整体搬进 Simulation；必须提取纯逻辑核。
- 当前命中在攻击者 Tick 中同步回调目标；建立 `World.Step` 时必须同时定义「收集 → 稳定排序 → 帧末结算」边界。

---

## 3. 目标架构

### 3.1 分层

```text
┌─────────────────────────────────────────────┐
│ Presentation（非权威，可插值/抖）              │
│  AnimationPlayback / VFX / SFX / Camera     │
│  读取 SimulationSnapshot 插值显示            │
└──────────────────▲──────────────────────────┘
                   │ Snapshot / ViewCmd
┌──────────────────┴──────────────────────────┐
│ Simulation（权威，确定性）                    │
│  SimulationClock (fixed frame)              │
│  InputFrameBuffer / FramePacket             │
│  CharacterSim (控制权 + 位移 + 碰撞查询)      │
│  ActionSim (Graph/Timeline/Cancel @ int)    │
│  CombatSim (Hitbox vs Hurtbox @ int)        │
│  AiSim (意图输出，同帧规则)                   │
└──────────────────▲──────────────────────────┘
                   │ AuthoritativeFramePacket
┌──────────────────┴──────────────────────────┐
│ Net（后期）                                   │
│  收集/广播 InputFrame，确认帧，校验 Hash       │
└─────────────────────────────────────────────┘
```

### 3.2 原则

| 原则 | 说明 |
|------|------|
| 单一逻辑时钟 | 全场角色同一 `frameIndex` 推进 |
| 输入驱动 | 逻辑只消费已对齐的 `InputFrame`，不读本机设备 |
| 整数帧招式 | 招式进度用 `int currentFrame`，不用 float 秒累计做权威 |
| 稳定执行顺序 | Actor、HitEvent、Spawn/Despawn 均按稳定 `SimActorId` 排序 |
| 表现跟随 | 动画 CrossFade、镜头、震屏不得写回逻辑位置 |
| 可哈希校验 | 每 N 帧对关键 Sim 状态做确定性 Hash，便于对局校验 |
| Domain 无 Unity 物理权威 | 逻辑碰撞自研或确定性库；Unity CC 仅作表现代理（过渡期） |
| Simulation 与 Net Tick 分离 | 逻辑频率、发包频率、渲染频率是三个概念；不得用网络定时器冒充逻辑帧 |

### 3.3 目标 Tick 形态

```text
// 权威（全项目固定 60 Hz）
void SimulationWorld.Step(in AuthoritativeFramePacket packet)
{
    BeginFrame(packet.Frame);
    ApplyPlayerInputs(packet);                 // Host 已保证该帧完整，不在 Step 内等待网络
    StepAiInStableActorOrder(packet.Frame);    // 读 N-1 快照，写 N 帧 AI InputFrame
    ApplyAiInputsInStableActorOrder(packet.Frame);
    StepControlAndActions(packet.Frame);       // Locomotion / Action / Hit / Death
    IntegrateMotionInStableActorOrder(packet.Frame);
    CombatSim.CollectHits(packet.Frame);
    CombatSim.SortAndResolve(packet.Frame);    // actorId / hitboxId / targetId 稳定排序
    CommitSpawnDespawn(packet.Frame);
    PublishSnapshots(packet.Frame);
    ComputeStateHashIfNeeded(packet.Frame);
}

// 表现（Update/LateUpdate）
Presentation.Interpolate(prevSnapshot, nextSnapshot, alpha);
```

单机阶段也必须走 `Step(packet)`，由 `SimulationHost` 用本地 accumulator 凑整帧并生成完整本地 Packet；联网阶段由 Room 在收齐/裁决输入后生成权威 Packet。追帧时可一渲染帧执行多个 `Step`，但单次 `Step` 永远只推进一个逻辑帧。等待网络属于 Host / Net 职责，不进入纯逻辑 `SimulationWorld`。

**同帧语义：**

- AI 在第 N 帧读取已提交的 N-1 快照并写 N 帧输入，避免 Actor 间读写顺序影响结果。
- 动作/位移全部完成后再统一收集命中；伤害、受击、死亡在同一帧稳定排序后提交。
- App Event 仅发布结果给表现层；不得在 Event Handler 中反向修改 Sim 状态。

---

## 4. 保留 / 改造 / 删除

### 4.1 保留（语义与资产）

- `GameplayIntentType`、Intent 优先级、缓冲窗口语义  
- `ActionGraph`（Entry / Normal / Perfect / SharedRoute / Directional）  
- `ActionTimeline` 窗口类型（Phase / Hitbox / Cancel / Movement / Rotation）  
- `ActionExecutionPolicy.interruptPriority`；`UseRootMotion` 在 L2 迁为运动表策略后删除
- 外层控制权：`Locomotion / Action / Hit / Death`  
- AI「只产意图」约束  

### 4.2 改造（核心 API）

| 旧 | 新 |
|----|----|
| `PlayerController.Update` / `EnemyController.Update` 各自 Tick | `SimulationHost` 唯一驱动 `SimulationWorld.Step(packet)` |
| `ActionExecutor` 同时拥有逻辑与表现 | `ActionSim` 纯逻辑 + `CharacterPresentationBridge` 只读表现 |
| `Tick(dt)` / `UpdateFrame` / Editor 采样三轨 | 单一 `ActionSim.Step()`；Editor 只用只读 Seek/Preview 接口 |
| `ElapsedSeconds` 权威 | `CurrentFrame`（int）权威；秒仅表现换算 |
| `CharacterRootMotionDriver` 逻辑位移 | **烘焙逐帧位移表**（定点数或 scaled int）+ 逻辑积分 |
| `CharacterController.Move` 权威 | `CharacterMotorSim`（确定性胶囊/圆盘） |
| Hitbox 跟挂点 Transform | 逻辑骨骼/挂点表或帧盒数据（相对角色根） |
| `PlayerInputFrame`（float/string） | 原地替换为量化 `InputFrame`；玩家/AI/回放/网络共用 |
| `AIInputSource` 跟 Update | AI 在 `Step` 内写该逻辑帧的 `InputFrame`；不再伪装设备 |
| FacingProxy + 相机相对 | 逻辑面朝用确定性朝向；相机仅表现 |
| `GetInstanceID()` 作为命中身份 | World 分配并持久化 `SimActorId` |
| 同步命中回调 | `CombatSim` 收集后按稳定键排序并统一结算 |

### 4.3 删除（阶段末）

- 逻辑路径上的 `OnAnimatorMove` → Motor  
- 逻辑路径上的变长 `deltaTime` 累计招式时间  
- 「表现采样 pose 决定命中盒」的权威路径  
- `PlayerController` / `EnemyController` 直接 Tick Actor 的入口
- `ActionExecutor.Tick`、孤立 `UpdateFrame` 与 Editor 独立逻辑派发三轨
- `HitStopController` 的秒计时逻辑权威与 `GetInstanceID()` 命中身份
- 长期 Adapter 双轨（见迁移纪律）  

---

## 5. 子系统重构设计

### 5.1 时钟与输入

```text
SimulationConfig
├─ logicHz = 60              // 本方案定案：动作、Locomotion、AI、HitStop 共用
└─ maxFrameCatchUp

InputFrame
├─ frame
├─ actorId                   // 稳定 SimActorId
├─ moveAxes                  // 量化：例如 sbyte/short，禁止裸 float 上传
├─ buttonsPressed/Held/Released bitset
└─ (可选) aimYawQuantized

InputFrameBuffer
├─ SetLocal(frame, frameData)
├─ SetAuthoritative(frame, framePacket)
├─ Get(frame, actorId)       // 单机必有；联网只读权威 FramePacket
└─ KeepHistory(snapshotFrame..currentFrame) // 追帧、重连与回放
```

L0B 当前代码落地为 `InputFrameBuffer.Set / MergeLocalSample / ResolveLocal`：本地缺采样追帧时只延续上一帧 Move/Held，Pressed/Released 永不推导或重复。`SetAuthoritative` 与 `FramePacket` 的权威覆盖语义留到 L5 接入 Room 时补齐，避免单机阶段提前建立伪网络双轨。

采集：

- 玩家：`InputReader` 在渲染帧采样 → **量化**写入「下一逻辑帧」槽  
- AI：在 `Step(frame)` 开头根据 N-1 快照写同一格式 `InputFrame`
- `GameplayIntentProducer` 改为消费 `InputFrame`，输出仍为 `GameplayIntent`
- Hold、Buffer、AI 冷却、Hit 硬直、Locomotion 阈值全部以整数帧计时

**类型迁移纪律：**

- 直接将现有 `PlayerInputFrame` 职责迁为 `InputFrame` 并替换调用点；不新增长期平行 `SimInputFrame`。
- `GameplayIntentProfile` 继续作为输入语义唯一配置源，阈值由秒迁为帧；不新增平行 `SimInputProfile`。
- `logicHz` 与 Action Timeline 统一为 **60Hz**。现有 30Hz Action 资产由 Editor 迁移：闭区间 `[start,end]` → `[2×start, 2×end+1]`，点事件与 AtFrame → `2×frame`；Locomotion 现有 60Hz 烘焙轨改为整数帧直接索引。

**联网缺帧策略（L5）：**

- 客户端只能推进到最新完整权威 `FramePacket`；等待超过预算时停 Sim，不伪造 Pressed。
- Held 可由服务器按上一帧状态展开，但展开结果必须进入权威 FramePacket，所有端消费完全相同的数据。
- 发包频率允许低于 60Hz（批量携带多帧输入），但 Simulation 仍逐帧 Step。

### 5.2 ActionSim（从 ActionExecutor 提取纯逻辑核）

```text
ActionSimState
├─ currentActionId
├─ currentFrame              // int，权威
├─ graphId + nodeId
├─ segmentIndex
├─ hitConfirmed
├─ pendingCancelIntent
├─ hitStopFramesRemaining
└─ controlState              // 与外层 CharacterSim 同步
```

步进：

```text
Step():
  if !active:
    TryStartFromIntents()                // 成功后本次 Step 立即执行新动作 frame 0
  if active:
    EvaluateWindows(currentFrame)      // Cancel/Phase/Hitbox active set
    EmitMotionDelta(currentFrame)
    QueueCancelOrTransition()
    if transitionQueued:
      CommitTransitionForNextWorldFrame()
    else if currentFrame + 1 >= totalFrames:
      EndAction()
    else:
      currentFrame++
```

帧边界定案：动作起手当帧执行 frame 0；在 frame N 判定出的 Cancel / 自动衔接只提交目标，目标动作 frame 0 从下一 World 帧开始，禁止同一 Step 递归推进多招。

迁移范围不能只改 Session：

- `ActionSession`、Graph Transition、Movement、Rotation、Segment、结束判定、Hit/Death 回退全部改读整数帧。
- Cancel / Phase / Hitbox 已有帧查询，迁移后保留为唯一窗口查询。
- Runtime 删除 `Tick(dt)` 与孤立 `UpdateFrame`，只保留逐帧 `Step()`。
- Editor Scrub 不执行带副作用的 Runtime Step；使用同一帧查询与段映射规则生成只读 Preview，不维护第二套窗口/转换算法。

`ActionSim` 不引用 `Transform`、`Animator`、`AnimationClip`、`CharacterController`、`Physics` 或 `ACTGameArchitecture`；动画/VFX/SFX 通过 `SimulationSnapshot` 与 `SimEvent` 交给表现层。

多段动画：

- 仍用 `animationSegments` 的帧区间映射  
- **不**引入 Action 内层 SM  
- 表现层按 `currentFrame` 选 Clip 局部时间播放  

### 5.3 位移：从 Root Motion 到帧表

**目标：** 逻辑位移不读 Animator。

具体数据结构、双文件夹命名匹配、批烘验收以
[INPLACE_ROOTMOTION_MOTION_TABLE_PLAN.md](./INPLACE_ROOTMOTION_MOTION_TABLE_PLAN.md)
为唯一实施细则。本节只保留锁步侧边界。

流水线：

```text
Editor 烘焙（复用现有 LocomotionRootMotionBaker 思路）
  选择 InPlace 文件夹 + RootMotion 文件夹
  → 按命名匹配（Attack_01_Inplace ↔ Unagi|Attack_01）
  → 从 RM Clip 以 60Hz 采样每逻辑帧 Δxz / Δyaw（角色本地坐标）
  → 量化为 scaled-int
  → 默认内嵌回写 ActionDefinition / CharacterLocomotionProfile 的运动表
  → 表现 Clip 使用已有 InPlace（不生成、不改写）

Runtime ActionSim
  → 查表取 Δ
  → 按运动策略投影（ForwardOnly / FullPlanar）
  → CharacterMotorSim.TryMove
```

过渡期允许：

- 表现播已有 InPlace  
- 逻辑只信表  
- 校验工具：Editor 对比「表位移 vs RM 源」误差报告  

人侧维护 InPlace Clip/段与 Timeline；Baker 在选定文件夹内自动配对 RM 并写回 MotionTable，禁止「逐 Clip 手烘再逐 Action 拖表」，也禁止从 RM 生成 InPlace。

现有 Locomotion 烘焙轨仍由 `Animation.NormalizedTime` 浮点采样；L2 必须改为 `locomotionFrame` 整数索引，不能只复用现状运行时。

### 5.4 CharacterMotorSim

```text
职责：
  水平速度/位移积分、重力（确定性）、简单地面、与静态碰撞
输入：
  wishDir（量化）、actionDelta、knockbackDelta
输出：
  position (int 或定点数)、facingYaw（量化）
```

实现选项（按成本）：

1. **2D 圆盘 + 网格/凸包障碍**（PVE 场地够用，优先）  
2. 定点数 3D 胶囊（成本高）  
3. L0–L1 过渡：仍调 Unity CC **仅用于保持单机可玩**；L2 结束必须删除该权威路径

场景静态碰撞数据需由 Editor 烘焙为确定性网格/凸包；运行时 Simulation 禁止查询 `Physics`。`CharacterController` 只跟随 Snapshot 做表现代理，不参与 Sim 位置裁决。

### 5.5 CombatSim（命中）

```text
每逻辑帧：
  收集 active Hitbox（相对根的 OBB/胶囊，来自 Timeline 帧数据）
  对异阵营 Hurtbox 做确定性相交
  生成 HitEvent（frame, attackerId, hitboxIndex, targetId, payload）
  按 (attackerId, hitboxIndex, targetId) 稳定排序
  统一 Resolve：伤害、Reaction、HitStop、死亡与 Spawn/Despawn 请求
```

- 命中去重身份改为 `(actionInstanceId, hitboxIndex, targetSimActorId)`，禁止使用 Unity InstanceId。
- 同帧互杀、死亡后剩余 HitEvent、重复受击重入必须写成纯规则并进入 Hash。
- L0C 已删除 `ApplyHitCommand`；`PublishAttackHitCommand` 只把整批结算后的 `AttackHitEvent` 发布给表现订阅者，不得回写 Sim。
- L0C 过渡实现使用 `CombatHitPipeline`：Transform OBB 检测仍在 Assembly-CSharp，但排序键已收敛为纯 C# `SimHitKey(frame, attackerId, actionInstanceId, hitboxIndex, targetId)`。
- 当前 `HurtboxNotifyState` 未进入运行时判定；L2 需明确选择静态 Character Hurtbox 或 Timeline 动态 Hurtbox，不能保留两个权威源。

卡肉：

- **逻辑 HitStop**：优先采用双方 Actor `freezeFrames`；是否冻结受击方由 Payload 明确配置
- **表现 HitStop**：动画 Speed=0（非权威）
- L0C 已删除 `AttackHitEvent → ActionExecutor.SetHitStopPaused` 回写；当前 `Time.unscaledDeltaTime` 仅控制表现冻结时长
- L2 将 `hitStopFrames` 迁入 Sim 计数，并删除秒换算的逻辑语义

### 5.6 LocomotionSim

保留相位语义（Idle/Start/Gait/Pivot/Stop），计时改为：

```text
runHoldFrames / gapFrames / pivotFrames
```

动画 Key 仍由相位输出给表现层；逻辑只输出相位、wish 速度、运动表帧与量化朝向。`SmoothDampAngle`、`Vector3.Angle` 与动画 `HasFinishedCurrent` 不得进入 Sim。

### 5.7 角色控制权

外层仍四态，但状态存在 `CharacterSim`：

```text
Locomotion / Action / Hit / Death
```

转换条件全部基于逻辑帧与 Intent，不读动画 `HasFinished` 的表现结果；  
招式结束以 `currentFrame >= totalFrames` 或 Transition 为准。

Hit / Death 状态同样必须读取 `ActionSim` 帧状态或 `stunFrames`，不得再以 `IActionExecutor.IsPlaying` 的表现会话作为权威结束条件。

### 5.8 AI

- 当前是 `EnemyBrain` FSM；未来 BT 与现有 Brain 都必须在 `AiSim.Step(frame)` 运行
- 输出写入 `InputFrame`（Move 量化 + Attack 位）  
- 禁止 `Time`/`Random.value`；用 `DeterministicRandom(seed, frame, actorId)`  
- 寻路（后期）：NavMesh 查询结果需缓存为确定性或改用逻辑网格  
- L0–L4：所有 World 以相同 N-1 Snapshot、seed 与规则本地计算 AI 输入
- L5：服务器运行同构影子 Sim 并校验 Hash；不采用 Demo 式「服务器 float 跑怪物、客户端只收 Transform」

### 5.9 表现桥 PresentationBridge

```text
订阅 Snapshot：
  frame/position/facing/actionId/actionFrame/locomotionPhase/gait/controlState
→ CharacterAnimationService.Play / PlayClip / Seek
→ VFX/SFX 按逻辑帧事件队列播放（允许丢帧表现，不许回写）
```

镜头、震屏、卡肉画面效果只读事件，不改 Sim。

远端角色可借鉴 Demo `SimRoleCtrl` 的 View Proxy 思路，但插值数据源必须是本地确定性 Sim 的相邻 Snapshot，而不是网络 Transform 快照。追帧期间表现可跳帧或合并事件，逻辑事件不可丢。

### 5.10 Snapshot、序列化与 Hash

`SimulationSnapshot` 至少包含：

```text
frame / worldSeed / contentVersion
actors[]（按 SimActorId 排序）
  position / facing / velocity
  controlState / locomotionState
  actionId / graphNodeId / actionFrame
  hp / freezeFrames / rngState
pendingSpawnDespawn
```

- Snapshot 序列化字段顺序必须固定；禁止直接 Hash Dictionary、Unity Object、浮点 Transform 或引用地址。
- 每帧可计算轻量本地 Hash，每 N 帧上报服务器校验；出现差异时记录最近 InputFrame、SimEvent 与首个不同字段。
- Snapshot 同时服务于测试、回放、断线恢复和晚加入，不能为网络另造第二套状态模型。

### 5.11 Net / Room 职责

服务器不是 Demo 式 Transform 中继，而是 Lockstep 权威帧协调者：

```text
Client InputFrame batch
  → Room 校验 actorId / frame 范围 / bitset 合法性
  → 收齐或执行服务器缺帧策略
  → 生成 AuthoritativeFramePacket
  → 广播给所有客户端
  → 服务器影子 SimulationWorld 同步 Step + Hash
```

- 客户端不上传 Transform、ActionName、伤害结果或 Hitbox 结果。
- 网络层不直接写 Sim；只把权威 FramePacket 放入 `InputFrameBuffer`。
- Snapshot 只用于加入、恢复与差异诊断，不作为正常每帧状态广播。
- 房间保存有限长度的权威 InputFrame 环形历史与周期 Snapshot；保留长度由最大重连窗口决定。

---

## 6. 数据与资产影响

| 资产 | 变化 |
|------|------|
| `ActionDefinition` | Timeline 继续；统一到 60Hz；外挂 `ActionMotionTable`（逐帧量化 Δ） |
| `ActionGraph` | 语义不变；条件改为帧/量化输入；运行时用稳定 Id，不用资产实例身份 |
| `GameplayIntentProfile` | 原位增加量化与帧阈值配置；禁止平行 `SimInputProfile` |
| Locomotion Profile | 相位阈值改 frames；RM 轨改为 60Hz 整数帧索引 |
| Enemy BT/Brain | 步进改逻辑帧；数值改 frames |
| 静态碰撞 | Editor 烘焙确定性 2D 网格/凸包数据 |

编辑器：

- Action Editor Scrub 复用 `ActionSim` 的纯帧查询/段映射规则，禁止执行 Runtime 副作用
- 新增「运动表烘焙/校验」窗口  
- 新增 30Hz → 60Hz Timeline / Profile 迁移工具（闭区间 `[start,end]` → `[2×start, 2×end+1]`；点事件 `frame` → `2×frame`）
- 对局 Hash 调试面板与双实例回放工具

**Agent 不直接改 `.asset`**；烘焙与迁表由 Editor 工具 + 人工执行。

**迁移完成条件：**

- 现有 Action、Locomotion、Intent Profile 均由 Editor 工具一次性迁移并人工保存。
- 迁移后删除旧秒字段、旧 30Hz 解释与运行时 fallback；不保留双字段兼容。
- 资产稳定 Id、运动表和碰撞烘焙数据必须纳入版本控制与对局内容版本 Hash。

---

## 7. 目录建议

```text
Assets/Scripts/Domain/Simulation/
  SimulationWorld.cs
  SimulationConfig.cs
  SimulationSnapshot.cs
  SimEvent.cs
  Identity/
    SimActorId.cs
  Input/
    InputFrame.cs
    InputFrameBuffer.cs
    InputQuantizer.cs
    AuthoritativeFramePacket.cs
  Math/                     // 定点数或 scaled-int 向量
    SimFixed.cs / SimVec2.cs
  Character/
    CharacterSim.cs
    CharacterMotorSim.cs
    CharacterControlState.cs
  Action/
    ActionSim.cs            // 取代逻辑侧 ActionExecutor
    ActionMotionTable.cs
  Combat/
    CombatSim.cs
    HitboxSim.cs
  Ai/
    AiSim.cs                // 现有 Brain / 未来 BT 的确定性宿主
  Replay/
    SimulationStateSerializer.cs
    SimulationHasher.cs

Assets/Scripts/App/Controllers/Gameplay/
  SimulationHost.cs         // Unity accumulator 与 World 生命周期入口

Assets/Scripts/Presentation/
  CharacterPresentationBridge.cs
  ...现有 Animation/VFX/Camera
```

`SimulationWorld` 是 Domain 纯 C# 对象，不注册为依赖 Unity Tick 的 `ArchitectureSystemBase`。`SimulationHost` 是唯一 Unity 驱动入口；跨系统结果在帧末通过 App 层发布。

旧 `ActionExecutor` 不降级为长期 Presentation Adapter：L1 提取 `ActionSim` 后，将必要播放能力迁到 `CharacterPresentationBridge`，随后删除旧类与调用链。

---

## 8. 分阶段实施

### Phase L0A — World 与时钟切核（代码完成，Editor 验收待确认）

**目标：** 建立全场唯一 fixed-step 入口，先消除多 `MonoBehaviour.Update` 的 Actor 顺序差异。

- [x] 2026-07-31：`SimulationHost` + 60Hz accumulator + `SimulationWorld`
- [x] 2026-07-31：World 分配稳定 `SimActorId`，按 Id 推进 Actor
- [x] 2026-07-31：`CharacterActor` / `EnemyHandle` 只被 `World.Step` 调用
- [x] 2026-07-31：移除 `PlayerController.Update` / `EnemyController.Update` 的直接 Tick
- [x] 2026-07-31：单步严格推进一帧；渲染低帧时由 Host 执行多次 Step
- [x] 2026-07-31：渲染帧边沿先汇聚再由 Step 单次消费，避免高 FPS 漏输入和追帧重复 Pressed
- [x] 2026-07-31：模型与相机跟随前后逻辑 Pose 插值，修复固定帧 Transform 的阶梯抖动

**验收：** EditMode 已覆盖 accumulator、Id、注册/注销与稳定 Step 顺序；单机可玩性及 30/60/144 渲染 FPS 的真实角色状态序列仍需在 Unity Editor Play Mode 确认。

**阶段删除：** Controller 分散 Tick 路径、Actor 读取 `Time.deltaTime` 的入口。

### Phase L0B — 输入帧边界（代码完成，Editor 验收待确认）

**目标：** 玩家、AI、回放未来共用同一量化输入格式。

- [x] 2026-08-01：删除 `PlayerInputFrame`，统一为 `InputFrame(frame, actorId, sbyte axes, button bitset, yaw)`
- [x] 2026-08-01：`InputReader` 仅在设备边界量化，并把多次渲染采样合并到下一逻辑帧槽
- [x] 2026-08-01：World 增加 Input Produce 阶段；`EnemyBrain` 在 Actor Step 前读 N-1 已提交状态并写 N 帧 `InputFrame`
- [x] 2026-08-01：`GameplayIntentProducer`、`GameplayIntentBuffer` 与 AI 攻击/重试/重定向冷却改为整数帧
- [x] 2026-08-01：确定展开规则——Move/Held 可在本地追帧延续，Pressed/Released 仅原始帧携带且不推导
- [x] 2026-08-01：删除 `ICharacterInputSource` / `AIInputSource.CaptureFrame` 设备伪装链，AI 改为 `AIInputWriter`

**验收：** EditMode 已覆盖量化、bitset、多渲染采样边沿合并、追帧连续状态展开、输入历史回放与 World 先产后消顺序；脱离设备完整重放起手、长按、闪避攻击与移动仍需 Unity Editor Play Mode 确认。

**阶段删除：** 已删除 AI 伪装设备的 `CaptureFrame` 路径、`PlayerInputFrame`、string 离散输入语义、秒制 Hold/Buffer 与 AI 输入冷却。

### Phase L0C — 同帧流水线与延迟结算（代码完成，Editor 验收待确认）

**目标：** 消除攻击者 Tick 中同步修改目标状态的顺序依赖。

- [x] 2026-08-01：Host/World 明确 Input Produce → Actor Control/Motion/Collect → Combat Resolve → PostCombat → Commit 顺序；Snapshot 发布点留待 L3
- [x] 2026-08-01：Hitbox 检测只写临时 `CombatHitPipeline`，不再立即调用目标
- [x] 2026-08-01：命中按 `SimHitKey(frame, attackerId, actionInstanceId, hitboxIndex, targetId)` 稳定排序后统一执行伤害/Reaction/命中确认
- [x] 2026-08-01：当前生产路径的死亡注销与 Despawn 固定在 Combat/PostCombat 后 Commit；Sim 内尚无 Spawn 请求入口
- [x] 2026-08-01：删除 `ApplyHitCommand`；`PublishAttackHitCommand` / `AttackHitEvent` 只接收整帧已结算结果
- [x] 2026-08-01：自动 Transition 与自然结束迁到 `ISimulationPostCombatActor`，保持 `OnHitConfirm` 在命中所属逻辑帧生效
- [x] 2026-08-01：删除 App HitStop 对 `ActionExecutor` 的暂停回写；L2 再引入权威 `freezeFrames`

**验收：** EditMode 已覆盖 `SimHitKey` 稳定排序/身份与 World PostCombat 顺序；交换真实玩家/敌人注册顺序后的同帧命中、受击、死亡结果，以及多命中/互杀规则仍需 Unity Editor Play Mode 确认。

**阶段删除：** 已删除 `HitboxFrameConsumer → ApplyHitCommand → 目标立即 EnterHit` 同步权威链、`GetInstanceID()` 命中去重，以及 `AttackHitEvent → ActionExecutor` 逻辑卡肉回写。

### Phase L1A — Action 整数帧权威（代码完成，Editor 验收待确认）

- [x] 2026-08-01：过渡 `ActionSession.CurrentFrame` 建立整数帧权威；该过渡类型已在 L1B 删除并由 `ActionSim.CurrentFrame` 接管
- [x] 2026-08-01：Cancel/Phase/Hitbox/Recovery 只接受整数帧查询
- [x] 2026-08-01：Graph Transition、Movement、Rotation、Segment 与结束判定全部改读整数帧
- [x] 2026-08-01：Hit / Death 使用 `DurationFrames` 与动作会话结束 Id 收尾，不再读取 `IsPlaying` 或秒倒计时
- [x] 2026-08-01：`CharacterActor` 成为每 World 帧唯一动作推进点；State 不再各自推进动作
- [x] 2026-08-01：Cancel / Recovery / 自动衔接只在判定帧排队，目标动作 frame 0 于下一 World 帧提交
- [x] 2026-08-01：L1A 曾用整数余数承接 30Hz 过渡资产；L1B 已删除该运行时解释并强制 60Hz

**验收：** L1A 的整数帧边界已由后续 `ActionSimTests` 继续覆盖；连招窗口、Perfect、Recovery 重开、自动衔接、Movement 与 Rotation 的完整录制重放仍需 Unity Editor Play Mode 确认。

**阶段删除：** 已删除 Runtime 秒制窗口/段查询、`ActionDefinition.FrameAt`、各 State 内重复动作推进，以及 Hit/Death 的秒倒计时与 `IsPlaying` 退出路径；L1B 进一步删除整个过渡执行器。

### Phase L1B — Action 逻辑 / 表现拆分（代码与资产 Hz 完成，Play Mode 验收待确认）

- [x] 2026-08-01：在 `ACTGame.Simulation` 提取无 Unity 依赖的 `ActionSim`、Snapshot、事件与内容/图/解析契约
- [x] 2026-08-01：`CharacterActionPresentationBridge` 只读 Sim 事件与 Snapshot，按整数帧播放/Seek Clip，并承接 L2 前暂留的 RootMotion、脚本位移与 Transform Hitbox 边界
- [x] 2026-08-01：新增无副作用 `ActionFrameQuery`；Runtime 动画段与两个 Action Editor 预览入口复用相同段/窗口/点事件规则
- [x] 2026-08-01：Runtime 只接受 60Hz Action；新增 `ACT/Tools/Migrate Action Assets 30Hz to 60Hz`，按闭区间/点事件规则迁移动画段、Timeline、HitStop 与 Graph AtFrame
- [x] 2026-08-02：仓库内全部 `ActionDefinition`（40）已为 `sampleRate=60`，无 `sampleRate=30` 残留；Migrate 为幂等空跑。新增 `ACT/Tools/Validate Action 60Hz Readiness`；Editor/VFX 估时 fallback 统一为 `ActionSim.LogicHz`
- [ ] Play Mode 回归：连招 / Perfect / Recovery / Hitbox / VFX·SFX / 位移；Unity Test Runner 跑 `ActionSim*` EditMode

**验收：** 纯 `ActionSimTests` 已覆盖起手、Cancel 延迟提交、HitConfirm 自动衔接、自然结束与高优打断；资产侧 Hz 迁移已完成。Unity Test Runner 与 Play Mode 手感/窗口一致性仍待人工确认。Player 侧若干 Action 仍为无动画占位（`IsSimulationReady=false`），不影响 Unagi 主路径，属内容补齐而非 L1B 架构缺口。

**阶段删除：** 已删除 `ActionExecutor`、`ActionSession`、`IActionExecutor`、`IActionHitReceiver`、`ActionFrameClock`、30Hz Runtime fallback，以及 Editor 的独立动画段/Hitbox 活跃查询。`ActionSim` 已无 Animation/Transform/CharacterController 依赖；L2 将删除表现桥内暂留的 RootMotion、脚本位移与 Transform Hitbox 权威路径。

### Phase L2 — 位移、命中与 HitStop 脱表现

- [x] 2026-08-02：招式运动表烘焙工具（M0：文件夹匹配 + 写回 `ActionDefinition.bakedMotion`）
- [x] 2026-08-02：运行时查表位移（M1：表就绪禁用 OnAnimatorMove，按 `currentFrame` 取表经 CC；MotorSim 待后续）
- [ ] Locomotion 运动轨改整数帧索引
- [ ] `CharacterMotorSim` + 静态碰撞烘焙；逻辑不再 `OnAnimatorMove` / CC / Physics
- [ ] Hitbox/Hurtbox 逻辑坐标与确定性相交
- [ ] HitStop 直接使用逻辑帧计数
- [ ] 命中身份改为 Sim Id，不再用 `GetInstanceID()`

**验收：** 关闭 Animator 仍能完成「位移 + 出伤 + 受击状态」的逻辑回放（无皮测试）。

**阶段删除：** Action/Locomotion 逻辑 Root Motion、Movement `speed * dt`、Transform 挂点权威、Unity Physics/CC 权威、秒制 HitStop。

### Phase L3 — 确定性数学与校验

- [ ] 位置/角度/速度/运动表统一 scaled-int 或定点数
- [ ] `DeterministicRandom`  
- [ ] 状态序列化与 Hash（SimActorId、position、facing、control、action、frame、hp、rng）
- [ ] 单机「双端影子模拟」：同输入两份 World，Hash 必同  
- [ ] 内容版本 Hash：Graph、Timeline、运动表、碰撞烘焙数据

**验收：** 固定操作脚本回放 N 次 Hash 一致；两份 World 从初始状态或中途 Snapshot 恢复后逐帧 Hash 一致。

### Phase L4 — 表现完全跟随

- [ ] PresentationBridge 只读 Snapshot  
- [ ] 动画 Seek/Play 按逻辑帧  
- [ ] 相机/震屏/VFX 事件队列  
- [ ] 追帧时合并/丢弃纯表现事件，不影响逻辑事件

**验收：** 逻辑加速（追帧）时玩法正确，仅表现可能快进。

### Phase L5 — 帧同步网络（未来）

- [ ] 输入收集与齐帧  
- [ ] 服务器生成权威 `FramePacket`，客户端只消费完整帧
- [ ] 锁步推进、批量输入发包与追帧预算
- [ ] 服务器同构影子 Sim + 定期 Hash 校验
- [ ] 断线重连：权威 Snapshot + Input 环形历史 + 内容版本校验
- [ ] 晚加入策略：默认仅房间准备阶段允许；战中加入需完整 Snapshot 恢复
- [ ] 超时策略：等待、踢出或 AI 接管必须由服务器在指定帧发布命令

**验收：** 2 人 + 若干 AI PVE 副本；延迟/抖动/短断线后逐帧 Hash 一致；客户端伪造 Transform 或伤害包无协议入口。

---

## 9. 与敌人 / 行为树的关系

| 模块 | 要求 |
|------|------|
| `AIInputSource` | L0B 直接替换为写 `InputFrame`，阶段结束删除设备模拟路径 |
| 当前 Brain / 未来 BT | 必须在 `AiSim.Step` 内调用；Running 节点以逻辑帧计时 |
| 感知 | 只读 N-1 `SimulationSnapshot`，禁止查询 Scene Transform / Physics |
| 随机 | 使用 World seed + actorId + 确定性 RNG 状态，状态进入 Snapshot / Hash |
| 寻路 | 放在 L3 之后；逻辑网格算法与开放列表 tie-break 必须稳定，输出量化方向 |
| 文档 | BT 方案继续有效，但 Tick 时钟改为逻辑帧 |

AI 所有权定案：L0–L4 单机与双 World 测试中，各 World 独立运行同构 AI；L5 服务器也运行同一 AI 与完整影子 Sim 做 Hash 校验。正常帧包只广播玩家输入与必要房间命令，不为每只敌人广播 Transform。

---

## 10. 风险与对策

| 风险 | 对策 |
|------|------|
| 手感因切固定帧变化 | L0 选 60Hz；用输入缓冲帧补偿；关键窗口按帧重调 |
| Action 30Hz 与 Simulation 60Hz 冲突 | Editor 按闭区间规则一次性迁移；迁移后删除 30Hz Runtime 解释 |
| RM 与表不一致 | 烘焙校验报告；大位移招优先人工修表 |
| 定点数工作量大 | L1–L2 先 scaled int（毫米）；L3 再收紧 |
| World 已固定帧但命中仍顺序依赖 | L0C 先做延迟收集与稳定排序，再进入确定性几何 |
| 双轨永久化 | 每阶段均有删除清单；阶段验收不允许旧入口仍可运行 |
| 范围膨胀 | L5 前不做正式联网；先影子模拟 |
| Unity 物理诱惑 | Code Review：Simulation 程序集禁止 `Physics.*` / `CharacterController.Move` 权威调用 |
| 误把状态同步 Demo 当 Lockstep | L5 协议门禁：禁止 Transform/ActionName/伤害结果作为常规上行消息 |
| Hash 只能发现不能恢复 | L3 同时建设确定性 Snapshot 序列化与字段级差异报告 |
| 断线恢复状态过大 | 周期 Snapshot + 有界 Input 环形历史，战中晚加入默认关闭 |

---

## 11. 明确非目标

- 本方案不实现具体网络库选型定案（Photon/自研/NGF 等放 L5 评估）  
- 不引入完整 UE 式 Prediction+Rollback（格斗向）；PVE 锁步以齐帧为主  
- 不采用 Demo 式客户端权威 Transform、客户端 Physics 命中或客户端伤害上报
- 不把每帧状态快照广播作为正常同步主路径
- 不为帧同步再给 Action 加内层动画 SM  
- 不要求一阶段定点数完美，但要求接口与权威归属先正确  

---

## 12. 决策记录

| 日期 | 决策 | 理由 |
|------|------|------|
| 2026-07-30 | 以帧同步为未来方向重构模拟核 | 用户明确多人 PVE 方向 |
| 2026-07-30 | 先单机 fixed-step + 影子校验，后联网 | 降低联调成本，避免假同步 |
| 2026-07-30 | 保留 Graph/Intent/Timeline 语义 | 已验证的玩法结构，避免重写内容语言 |
| 2026-07-30 | 逻辑位移改帧表，表现保留 Clip | 拆开确定性与手感表现 |
| 2026-07-30 | Action 多段仍用 segments+整数帧 | 与既有结论一致，避免内层 SM |
| 2026-07-30 | 阶段结束删旧权威路径 | 符合 no-legacy-compatibility |
| 2026-07-30 | 全项目 Simulation / Timeline 定为 60Hz | ACT 手感优先；消除 Action 30Hz 与 Locomotion 60Hz 双频解释 |
| 2026-07-30 | AI 读取 N-1 Snapshot、写 N 帧输入 | 消除 Actor 更新顺序对同帧决策的影响 |
| 2026-07-30 | 服务端生成权威 FramePacket 并运行影子 Sim | 同时满足齐帧、Hash 校验与 PVE 房间管理 |
| 2026-07-30 | DemoClient / DemoServer 仅作局部工程参考 | 其核心是混合状态同步，不是 Lockstep |

---

## 13. 建议落地顺序（执行时）

1. **L0A**：先建 World、稳定 SimActorId 与唯一 Tick；不动资产。
2. **L0B**：原位替换输入帧并帧化 Intent / Buffer / AI 冷却。
3. **L0C**：建立延迟 HitEvent 与稳定帧末结算，消除 Actor 顺序依赖。
4. **L1A–L1B**：Action 全链路整数帧化，再拆 ActionSim / Presentation；同时提供 30→60Hz 迁移工具。
5. **L2**：一条测试招先验证运动表，再统一迁移 Action/Locomotion/Hitbox/Motor/HitStop。
6. **L3**：定点/量化、Snapshot、Hash、双 World 与中途恢复测试。
7. **BT/寻路**：只接入已存在的逻辑帧与确定性网格，不再接 Unity NavMesh 权威路径。
8. **L4–L5**：表现完全跟随后再接正式网络；禁止先做 Demo 式 Transform 同步“临时上线”。

---

## 14. 一句话

把现在的动作系统从「**以动画播放会话为中心的单机执行器**」升级为「**以固定逻辑帧与输入帧为中心的确定性模拟核 + 表现桥**」；Graph/取消/意图保留为内容语言，Executor/RM/CC 让出权威，才能走向帧同步 PVE。

---

## 15. DemoClient / DemoServer 对照结论

### 15.1 Demo 实际模型

```text
DemoClient 本地玩家
  Update + deltaTime + Animator RM + CharacterController
  → 每 16ms 上传 Transform
  → 客户端 Physics 检测玩家打怪并上报伤害

DemoServer Room（20ms Tick）
  → 直接接受玩家 Pos/Rot
  → 服务端 float 推进怪物 AI / 导航 / 怪物打人
  → 广播玩家与怪物状态快照

其他客户端
  → 远端玩家 Lerp
  → 怪物位置快照表现
```

它没有 `frameIndex`、量化 `InputFrame`、齐帧、追帧、回滚、状态 Hash 或确定性物理，因此不得称为客户端帧同步。

### 15.2 可借鉴

- `SimRoleCtrl` 的远端 View Proxy 思路 → `CharacterPresentationBridge`。
- `Room.Broadcast`、Session 生命周期与消息队列 → L5 Net / Room 基础设施。
- 服务端固定 Tick 宿主结构 → 网络线程调度参考，但不能替代 `SimulationClock`。
- 服务端动画 Root Motion 采样 → `ActionMotionTable` 烘焙工具参考。

### 15.3 禁止照搬

- 客户端上传 Transform 作为玩家权威状态。
- 玩家打怪在客户端 `Physics` 检测并上传伤害/暴击。
- 用 16ms 发包或 20ms Room Tick 冒充统一逻辑帧。
- 通过 `ActionName`、Parry/Dodge 独立消息同步战斗状态。
- 玩家与怪物在不同端执行不同命中规则。
- 仅用 Lerp 掩盖状态误差，缺少 Hash 与恢复。

### 15.4 Lockstep 协议门禁

L5 上行常规消息只允许：

- 玩家 `InputFrame` 批次。
- Hash / 诊断信息。
- 连接、房间与确认控制消息。

禁止常规上行：

- Transform / Velocity 权威快照。
- ActionName / 当前动作结果。
- HitEvent、伤害、暴击、受击或死亡结果。

这些结果必须由各端同一 `SimulationWorld` 从权威 FramePacket 推导，并由服务器影子 Sim 校验。
