# DiavoloGame 动作系统帧同步导向重构方案

> 基准：`develop`（ActionGraph / Timeline / CharacterActor / 敌人 AI 初版已落地）  
> 制定日期：2026-07-30  
> 目标：以**帧同步（Lockstep）多人 PVE**为未来方向，重构动作与角色模拟核；保留现有选招/窗口/意图语义  
> 相关文档：[ENEMY_SYSTEM_INTEGRATION_PLAN.md](./ENEMY_SYSTEM_INTEGRATION_PLAN.md)、[ENEMY_BEHAVIOR_TREE_PLAN.md](./ENEMY_BEHAVIOR_TREE_PLAN.md)

---

## 1. 结论摘要

1. **现状适合单机 ACT，不适合直接帧同步**；差距在时钟、确定性数学、逻辑/表现耦合，不在「有没有 Graph」。
2. **可保留**：`GameplayIntent`、ActionGraph、Cancel/Phase 窗口语义、外层控制权（Locomotion/Action/Hit/Death）、AI 只产意图。
3. **必须重构**：`Tick(deltaTime)` → **固定逻辑帧**；Root Motion / `CharacterController` 退出逻辑权威；Hitbox 与位移改为**整数帧 + 确定性几何**。
4. **目标架构**：`Simulation`（确定性）与 `Presentation`（可抖）分离；网络层最后接，先让单机跑在同一套逻辑核上。
5. **迁移策略**：分阶段切核，每阶段可玩验收；**禁止**长期「旧 Executor + 新逻辑核」双轨并存（阶段内允许适配层，阶段结束删除旧路径）。

---

## 2. 现状诊断（帧同步视角）

| 模块 | 现状 | 帧同步风险 |
|------|------|------------|
| 时钟 | `CharacterActor.Tick(Time.deltaTime)` | 端间步进不一致 |
| 招式时间 | `ActionSession.ElapsedSeconds` + float | 累计误差、不可复现 |
| 位移 | Root Motion → `CharacterController.Move` | PhysX/CC 非确定性 |
| 动画 | Playable 直接驱动逻辑位移 | 逻辑依赖表现采样 |
| Hitbox | 跟播放会话 / 挂点 Transform | 依赖本机骨骼与浮点 |
| 输入 | Intent 管线正确，但跟渲染帧采集 | 需改为「输入帧」对齐逻辑帧 |
| Locomotion | 内层 FSM + float 计时 | 需改整数帧与确定性转向 |
| AI | Brain → `AIInputSource` | 方向对；须在逻辑帧步进且无本机随机 |
| 网络 | 无 | 需在逻辑核稳定后接入 |

**已具备的帧同步友好点：**

- Intent / Buffer / Driver / Resolver 分层清晰  
- Timeline 已有「逻辑帧」概念（`SampleRate`、`TotalFrames`、`UpdateFrame`）  
- 编辑器 Scrub 与 Play 共用 `UpdateFrame` 的方向正确，可升级为唯一逻辑推进 API  

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
│  InputFrameBuffer (per player/AI)           │
│  CharacterSim (控制权 + 位移 + 碰撞查询)      │
│  ActionSim (Graph/Timeline/Cancel @ int)    │
│  CombatSim (Hitbox vs Hurtbox @ int)        │
│  AiSim (意图输出，同帧规则)                   │
└──────────────────▲──────────────────────────┘
                   │ InputFrame
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
| 表现跟随 | 动画 CrossFade、镜头、震屏不得写回逻辑位置 |
| 可哈希校验 | 每 N 帧对关键 Sim 状态做确定性 Hash，便于对局校验 |
| Domain 无 Unity 物理权威 | 逻辑碰撞自研或确定性库；Unity CC 仅作表现代理（过渡期） |

### 3.3 目标 Tick 形态

```text
// 权威（固定 30 或 60 Hz）
void SimulationWorld.Step(int frame)
{
    CollectOrWaitInputFrame(frame);      // 单机：本机+AI；联网：齐帧
    for each actor:
        actor.Ai?.Step(frame);           // 写 InputFrame 槽
        actor.ApplyInput(frame);
        actor.ActionSim.Step(frame);
        actor.LocomotionSim.Step(frame);
        actor.IntegrateMotion(frame);    // 确定性位移
    CombatSim.DetectHits(frame);
    CombatSim.ApplyDamageAndReactions(frame);
    PublishSnapshots(frame);
}

// 表现（Update/LateUpdate）
Presentation.Interpolate(prevSnapshot, nextSnapshot, alpha);
```

单机阶段也必须走 `Step(frame)`，用本地累积时间凑整帧（accumulator），禁止逻辑再直接吃 `deltaTime`。

---

## 4. 保留 / 改造 / 删除

### 4.1 保留（语义与资产）

- `GameplayIntentType`、Intent 优先级、缓冲窗口语义  
- `ActionGraph`（Entry / Normal / Perfect / SharedRoute / Directional）  
- `ActionTimeline` 窗口类型（Phase / Hitbox / Cancel / Movement / Rotation）  
- `ActionExecutionPolicy`（interruptPriority、是否使用烘焙位移等）  
- 外层控制权：`Locomotion / Action / Hit / Death`  
- AI「只产意图」约束  

### 4.2 改造（核心 API）

| 旧 | 新 |
|----|----|
| `ActionExecutor.Tick(dt)` | `ActionSim.Step(frame)` / `AdvanceToFrame(frame)` |
| `ElapsedSeconds` 权威 | `CurrentFrame`（int）权威；秒仅表现换算 |
| `CharacterRootMotionDriver` 逻辑位移 | **烘焙逐帧位移表**（定点数或 scaled int）+ 逻辑积分 |
| `CharacterController.Move` 权威 | `CharacterMotorSim`（确定性胶囊/圆盘） |
| Hitbox 跟挂点 Transform | 逻辑骨骼/挂点表或帧盒数据（相对角色根） |
| `AIInputSource` 跟 Update | AI 在 `Step` 内写该逻辑帧的 `InputFrame` |
| FacingProxy + 相机相对 | 逻辑面朝用确定性朝向；相机仅表现 |

### 4.3 删除（阶段末）

- 逻辑路径上的 `OnAnimatorMove` → Motor  
- 逻辑路径上的变长 `deltaTime` 累计招式时间  
- 「表现采样 pose 决定命中盒」的权威路径  
- 长期 Adapter 双轨（见迁移纪律）  

---

## 5. 子系统重构设计

### 5.1 时钟与输入

```text
SimulationConfig
├─ logicHz = 60              // 或 30，需全项目统一
└─ maxFrameCatchUp

InputFrame
├─ frame
├─ actorId
├─ moveAxes                  // 量化：例如 sbyte/short，禁止裸 float 上传
├─ buttonsPressed/Held/Released bitset
└─ (可选) aimYawQuantized

InputFrameBuffer
├─ SetLocal(frame, frameData)
├─ Get(frame, actorId)       // 缺帧策略：后期联网；单机必有
```

采集：

- 玩家：`InputReader` 在渲染帧采样 → **量化**写入「下一逻辑帧」槽  
- AI：在 `Step(frame)` 开头根据黑板写同一格式 `InputFrame`  
- IntentProducer：改为消费 `InputFrame`，输出仍为 `GameplayIntent`（可保留）  

### 5.2 ActionSim（原 ActionExecutor 逻辑核）

```text
ActionSimState
├─ currentActionId
├─ currentFrame              // int，权威
├─ graphId + nodeId
├─ segmentIndex
├─ hitConfirmed
├─ pendingCancelIntent
└─ controlState              // 与外层 CharacterSim 同步
```

步进：

```text
Step(frame):
  if !active: try start from intents
  else:
    currentFrame++
    EvaluateWindows(currentFrame)      // Cancel/Phase/Hitbox active set
    ApplyScriptedOrBakedMotion(frame)
    TryCancel / TryAutoTransition / Recovery
    if currentFrame >= totalFrames: EndAction()
```

`UpdateFrame` 与 Play Mode **合并为** `AdvanceToFrame` / `Step`，编辑器 Scrub 直接打逻辑核。

多段动画：

- 仍用 `animationSegments` 的帧区间映射  
- **不**引入 Action 内层 SM  
- 表现层按 `currentFrame` 选 Clip 局部时间播放  

### 5.3 位移：从 Root Motion 到帧表

**目标：** 逻辑位移不读 Animator。

流水线：

```text
Editor 烘焙（可扩现有 LocomotionRootMotionBaker）
  AnimationClip / 招式段
  → 每逻辑帧 Δxz（相对起手朝向或角色本地 +Z）
  → ActionMotionTable / LocomotionMotionTable 资产

Runtime ActionSim
  → 查表取 Δ
  → 按 RootMotionPlanarMode 投影（ForwardOnly / FullPlanar）
  → CharacterMotorSim.TryMove
```

过渡期允许：

- 表现仍播原 Clip  
- 逻辑只信表  
- 校验工具：Editor 对比「表位移 vs 原 RM」误差报告  

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
3. 过渡：仍调 Unity CC **仅单机**，但接口先换成 `IMotorSim`，联网前必须替换  

### 5.5 CombatSim（命中）

```text
每逻辑帧：
  收集 active Hitbox（相对根的 OBB/胶囊，来自 Timeline 帧数据）
  对异阵营 Hurtbox 做确定性相交
  生成 HitEvent（attacker, target, payload, frame）
  统一 Resolve：伤害、Reaction、HitStop（逻辑暂停用「冻结逻辑帧推进」或击中方/被击方 stun 计数）
```

卡肉：

- **逻辑 HitStop**：双方 `stunFrames` 或全局 `freezeFrames`（确定性）  
- **表现 HitStop**：动画 Speed=0（非权威）  

### 5.6 LocomotionSim

保留相位语义（Idle/Start/Gait/Pivot/Stop），计时改为：

```text
runHoldFrames / gapFrames / pivotFrames
```

动画 Key 仍由相位输出给表现层；逻辑只输出相位与 wish 速度。

### 5.7 角色控制权

外层仍四态，但状态存在 `CharacterSim`：

```text
Locomotion / Action / Hit / Death
```

转换条件全部基于逻辑帧与 Intent，不读动画 `HasFinished` 的表现结果；  
招式结束以 `currentFrame >= totalFrames` 或 Transition 为准。

### 5.8 AI

- Brain/BT 在 `AiSim.Step(frame)` 运行  
- 输出写入 `InputFrame`（Move 量化 + Attack 位）  
- 禁止 `Time`/`Random.value`；用 `DeterministicRandom(seed, frame, actorId)`  
- 寻路（后期）：NavMesh 查询结果需缓存为确定性或改用逻辑网格  

### 5.9 表现桥 PresentationBridge

```text
订阅 Snapshot：
  position/facing/actionId/actionFrame/locomotionPhase/gait
→ CharacterAnimationService.Play / PlayClip / Seek
→ VFX/SFX 按逻辑帧事件队列播放（允许丢帧表现，不许回写）
```

镜头、震屏、卡肉画面效果只读事件，不改 Sim。

---

## 6. 数据与资产影响

| 资产 | 变化 |
|------|------|
| `ActionDefinition` | Timeline 继续；新增或外挂 `ActionMotionTable`（逐帧 Δ） |
| `ActionGraph` | 语义不变；条件改为帧/量化输入 |
| `GameplayIntentProfile` | 增加量化规则或平行 `SimInputProfile` |
| Locomotion Profile | 相位阈值改 frames；RM 烘焙表复用扩展 |
| Enemy BT/Brain | 步进改逻辑帧；数值改 frames |

编辑器：

- Action Editor Scrub = 打 `ActionSim`  
- 新增「运动表烘焙/校验」窗口  
- 可选：对局 Hash 调试面板（单机双实例回放）  

**Agent 不直接改 `.asset`**；烘焙与迁表由 Editor 工具 + 人工执行。

---

## 7. 目录建议

```text
Assets/Scripts/Domain/Simulation/
  SimulationClock.cs
  SimulationWorld.cs
  Input/
    InputFrame.cs
    InputFrameBuffer.cs
    InputQuantizer.cs
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
    AiSim.cs                // 包一层现有 Brain/BT

Assets/Scripts/Presentation/
  CharacterPresentationBridge.cs
  ...现有 Animation/VFX/Camera
```

旧 `ActionExecutor`：在 Phase L2 结束后删除逻辑权威职责，或降级为 Presentation 适配器后尽快删除。

---

## 8. 分阶段实施

### Phase L0 — 时钟切核（单机仍可玩）

**目标：** 全角色改为 fixed-step 驱动，行为手感可暂时近似。

- [ ] `SimulationClock` + accumulator  
- [ ] `CharacterActor` / `EnemyHandle` 改为只被 `World.Step` 调用  
- [ ] Intent 采集与 Step 对齐（输入落到逻辑帧）  
- [ ] 仍可暂时用 float 内部，但 **禁止**逻辑直接 `Time.deltaTime`  

**验收：** 单机玩法可打；暂停/低帧率下逻辑步进稳定（降速不甩帧逻辑）。

### Phase L1 — Action 整数帧权威

- [ ] `ActionSession.CurrentFrame` 为权威；`ElapsedSeconds` 派生  
- [ ] Cancel/Phase/Hitbox/Recovery 全部 `IsActiveAtFrame`  
- [ ] `Tick(dt)` 改为每逻辑帧 `Step()` 推进 1 帧  
- [ ] 编辑器 Scrub 走同一 `AdvanceToFrame`  

**验收：** 连招窗口、Perfect、Recovery 重开与现网一致（允许 ±0 帧，以逻辑帧为准重调资产）。

### Phase L2 — 位移与命中脱表现

- [ ] 招式运动表烘焙工具 + 运行时查表  
- [ ] `IMotorSim`；逻辑不再 `OnAnimatorMove`  
- [ ] Hitbox 逻辑坐标（相对根）  
- [ ] HitStop 逻辑帧冻结  

**验收：** 关闭 Animator 仍能完成「位移 + 出伤 + 受击状态」的逻辑回放（无皮测试）。

### Phase L3 — 确定性数学与校验

- [ ] 位置/角度量化或定点数  
- [ ] `DeterministicRandom`  
- [ ] 状态 Hash（transform、actionId、frame、hp）  
- [ ] 单机「双端影子模拟」：同输入两份 World，Hash 必同  

**验收：** 固定操作脚本回放 N 次 Hash 一致；两份 World 同步 Step 一致。

### Phase L4 — 表现完全跟随

- [ ] PresentationBridge 只读 Snapshot  
- [ ] 动画 Seek/Play 按逻辑帧  
- [ ] 相机/震屏/VFX 事件队列  

**验收：** 逻辑加速（追帧）时玩法正确，仅表现可能快进。

### Phase L5 — 帧同步网络（未来）

- [ ] 输入收集与齐帧  
- [ ] 锁步推进 / 追帧  
- [ ] 断线、晚加入策略（PVE 可主机补帧或房间制）  
- [ ] 反作弊：定期 Hash 校验  

**验收：** 2 人 + 若干 AI PVE 副本；故意制造延迟仍逻辑一致（表现可预测缓冲）。

---

## 9. 与敌人 / 行为树的关系

| 模块 | 要求 |
|------|------|
| `AIInputSource` | 改为写 `InputFrame`，勿再假装设备再绕一圈（可保留适配一期） |
| Brain / BT | 必须在 `AiSim.Step` 内调用；Running 节点以逻辑帧计时 |
| 寻路 | 放在 L3 之后；输出量化方向进 InputFrame |
| 文档 | BT 方案继续有效，但 Tick 时钟改为逻辑帧 |

---

## 10. 风险与对策

| 风险 | 对策 |
|------|------|
| 手感因切固定帧变化 | L0 选 60Hz；用输入缓冲帧补偿；关键窗口按帧重调 |
| RM 与表不一致 | 烘焙校验报告；大位移招优先人工修表 |
| 定点数工作量大 | L1–L2 先 scaled int（毫米）；L3 再收紧 |
| 双轨永久化 | 每阶段定义删除清单；L2 结束移除逻辑 RM |
| 范围膨胀 | L5 前不做正式联网；先影子模拟 |
| Unity 物理诱惑 | Code Review：Simulation 程序集禁止 `Physics.*` / `CharacterController.Move` 权威调用 |

---

## 11. 明确非目标

- 本方案不实现具体网络库选型定案（Photon/自研/NGF 等放 L5 评估）  
- 不引入完整 UE 式 Prediction+Rollback（格斗向）；PVE 锁步以齐帧为主  
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

---

## 13. 建议落地顺序（执行时）

1. **立刻可做**：Phase L0 时钟 + 输入对齐（不动资产）。  
2. **并行工具**：运动表烘焙原型（一条测试招）。  
3. **然后**：L1 整数帧 ActionSim → L2 脱表现命中/位移。  
4. **BT/寻路**：挂在逻辑帧上实现，避免将来重改时钟。  
5. **联网**：仅在 L3 Hash 稳定后启动 L5。  

---

## 14. 一句话

把现在的动作系统从「**以动画播放会话为中心的单机执行器**」升级为「**以固定逻辑帧与输入帧为中心的确定性模拟核 + 表现桥**」；Graph/取消/意图保留为内容语言，Executor/RM/CC 让出权威，才能走向帧同步 PVE。
