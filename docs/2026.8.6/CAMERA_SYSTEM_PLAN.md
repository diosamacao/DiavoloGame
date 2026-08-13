# ACTGame 相机系统方案

> 基准：`develop`（`CameraManager` + `CameraShakeController` + Cinemachine 2.10 + `PresentationRoot`）  
> 制定日期：2026-08-05  
> 目标：在**不改动逻辑权威**的前提下，建成可扩展的 ACT 战斗相机（多锚点 Rig、模式导演、Lock-On、反馈通道、**多段技能/大招演出机位**）  
> 产品参考：魂系 Lock-On；**绝区零式**大招多段定制镜头（特写 → 回身、FOV/推拉、段内震屏）  
> **排期真源：** [MASTER_IMPLEMENTATION_PLAN.md](./MASTER_IMPLEMENTATION_PLAN.md)  
> 相关文档：[CHARACTER_MOVEMENT_ANCHOR_OPTIMIZATION_PLAN.md](./CHARACTER_MOVEMENT_ANCHOR_OPTIMIZATION_PLAN.md)、[ACTION_SYSTEM_LOCKSTEP_REFACTOR_PLAN.md](../ACTION_SYSTEM_LOCKSTEP_REFACTOR_PLAN.md)、[ENEMY_SYSTEM_INTEGRATION_PLAN.md](../ENEMY_SYSTEM_INTEGRATION_PLAN.md)、[SKILL_AND_RESOURCE_SYSTEM_PLAN.md](./SKILL_AND_RESOURCE_SYSTEM_PLAN.md)  
> 修订：2026-08-05 — 补强 §5.5 技能演出镜头  
> 修订：2026-08-06 — 对齐 VisualMotionRoot 层级；滤左右定位为 Wave 1 临时止血  
> 修订：2026-08-09 — **LockOn / Predict / SkillShot / Finisher 排期全部由本文自管**：不再挂 MASTER Wave 4/5；C1～C4 按本篇 Phase 独立推进  
> 修订：2026-08-11 — 铁律 #6：自由移动 Orbit yaw 可跟随角色朝向（交叉 Locomotion L-DIR5）；禁止反写 Motor
> 修订：2026-08-13 — C-AT0～3 已切换为 MoveReferenceYaw + 唯一 SelectedTarget；CameraLock 仅为本地表现。旧 CombatTargetLock/PlanarBasis 描述已废止。

---

## 1. 结论摘要

1. **现状**已是可用的「探索第三人称 + 避障 + 命中 Impulse」，且 C-AT 权威前置已完成；缺的是**导演层、多机位、Lock-On 构图**，以及**按招式时间轴驱动的多段演出机位**。
2. **定案骨架**：`CameraDirector`（模式栈） + **多锚点 Rig** + **多 VirtualCamera** + Impulse/FOV 反馈 + **`CameraShotSequence`（技能镜头轨）**。
3. **锚点流水线**：`PresentationRoot` → `CameraRoot` → `FollowAnchor`（可滤左右）→ 可选 `PredictAnchor` → `Orbit/Pitch` → 日常 VCam；演出另用 Face/Body/Custom 锚点或独立 VCam。
4. **大招/特殊技镜头**（绝区零向）：按 Action 逻辑帧或 Timeline 窗口切换 Shot（如脸部特写 → 回身第三人称），段内可叠震动、FOV 拉近拉远；**表现可定制，命中仍走 Sim**。
5. **Look / 震屏 / Blend / Shot 仅表现层**；禁止相机写回 Motor/命中权威。移动前向读 Orbit Yaw（演出抢权期间可冻结 Look）。
6. TargetSwitch 属于玩法 InputFrame；CameraLock / Shot 属于本地表现，不进模拟 Snapshot。
7. **参考**：Cinemachine（多 VCam、Blend、Impulse、Target Group、Timeline 协作）；绝区零「招式定制镜头作第二动画师」的产品思路（多段引导视线，而非单一固定机位）。

---

## 2. 现状诊断

| 模块 | 现状 | 问题 / 风险 |
|------|------|-------------|
| 跟随 | `CameraManager`：Orbit SmoothDamp → `CameraRoot`（挂 `PresentationRoot`） | 完整 XYZ 跟随，侧向攻击位移易带镜头 |
| 旋转 | Update 累加 yaw/pitch → Orbit/Pitch 枢轴 | 正确；无锁定时输入重映射 |
| VCam | 单机位 Transposer + HardLookAt + Collider | 无 Free/LockOn 切换与 Blend |
| 震屏 | `CameraShakeController` 订 `AttackHitEvent`（仅玩家进攻命中） | 缺受击/弹刀/落地等通道；未 Feedback 化 |
| 索敌 | `CharacterTargetingState.SelectedTargetId` | Camera 只读映射已接；LockOn VCam 未做 |
| 时序 | `SimulationHost.LateUpdate(-100)` 先 `World.Render`，再相机 | ✅ 保留 |
| 文档 | 无独立相机方案；清单中 Cinemachine 条目可能过期 | 以本文为准 |

**已具备、应保留：**

- 跟随插值表现根，避免逻辑 60Hz 阶梯抖  
- Look 不进锁步权威  
- Orbit/Pitch 与 Follow/LookAt 分离  
- `CinemachineCollider`、Impulse Profile 资产链  

---

## 3. 目标架构

### 3.1 分层

```text
┌─────────────────────────────────────────────────────────┐
│ App / Presentation                                        │
│  CameraDirector（模式、优先级、只读 SelectedTarget）       │
│  CameraRig（多锚点写入）                                  │
│  CameraManager（输入 Look、光标；可降为 Rig 驱动器）         │
│  多 VCam + CinemachineBrain（Blend / Collider / Impulse） │
│  CameraShake / CameraFeedback（事件 → Impulse/FOV）       │
└──────────────────────────▲──────────────────────────────┘
                           │ 只读：PresentationRoot、LookInput、
                           │      ILocalCameraTargetSource、Hit 事件
┌──────────────────────────┴──────────────────────────────┐
│ Domain（权威，相机不写回）                                 │
│  CharacterActor / PresentationBridge / MotorSim           │
│  CharacterTargetingState / CombatHitPipeline              │
└─────────────────────────────────────────────────────────┘
```

### 3.2 多锚点 Rig（定案）

```text
Player
└── CharacterPresentationRoot          // 已有：逻辑 Pose 插值
    ├── CameraRoot                     // 胸口高度；无视觉残差（日常相机源）
    ├── LookAtPoint（可选新建）         // 头/准星高度，供 Composer
    └── CharacterVisualMotionRoot      // Wave 2：动作视觉残差（见 Anchor 篇）
        └── Model / Animator

【CameraRig 下，由 LateUpdate 写入世界坐标】
FollowAnchor      // 稳定跟随：可只吸收「玩家朝向前向」位移 + Y
PredictAnchor     // 可选：Follow + 水平前伸 / 速度超前
OrbitPivot        // 已有：位置←Follow或Predict；旋转=Yaw
└── PitchPivot    // 已有：本地 Pitch
    └── Shoulder  // 可选：肩射左右偏置

LockGroup（CinemachineTargetGroup，锁定时）
  members: Player LookAtPoint + Enemy LockPoint
```

**铁律补充：** `CameraRoot` / 日常 LookAt **不得**挂在 `VisualMotionRoot` 下。

| 锚点 | 写入规则 | 谁消费 |
|------|----------|--------|
| `CameraRoot` | 随 PresentationRoot，固定 local 高度；不含视觉残差 | Follow 算法输入 |
| `FollowAnchor` | 见 §5.1；滤左右 / SmoothDamp | Free 模式 Orbit 位置源 |
| `PredictAnchor` | Follow + `forward * lead`（+ 可选速度项） | 疾跑/探索构图 |
| `Orbit/Pitch` | Look 输入；位置跟 Follow 或 Predict | Free VCam Follow/LookAt |
| `LockGroup` | 锁定时维护成员 | LockOn VCam LookAt |

**调试（已落地骨架）：** Scene Gizmo `CameraDebugGizmoDrawer`（挂 `CameraManager`；Inspector `drawCameraDebugGizmos`）。颜色区分 Sim / Presentation / Visual / CameraRoot / FollowAnchor / Orbit / Pitch / MainCamera；红虚线=滤掉的左右残差；左上角图例含 Yaw/Pitch/Lateral。角色 Motor 圆仍由 `CharacterAnchorGizmoDrawer` 绘制。Predict / LockGroup 待对应功能接入后再补。

### 3.3 虚拟相机与模式（含优先级栈）

| 模式 | 优先级（高者覆盖） | VCam / 驱动 | Follow / LookAt | Look 输入 |
|------|-------------------|-------------|-----------------|-----------|
| `Free` | 10 | 日常第三人称 | PitchPivot / Orbit | 全开 |
| `LockOn` | 20 | LockOn + TargetGroup | 玩家+敌同框 | 关或微距 |
| `SkillShot` | 80 | 招式 `CameraShotSequence` | 按段：Face / Body / Custom | 默认关闭 |
| `Finisher` / `Cutscene` | 90～100 | Timeline + CM 轨 | 演出绑定 | 关闭 |

```text
日常：Free 或 LockOn
放大招：Director.Push(SkillShot) — 覆盖日常，不销毁 Free/Lock 状态
  Shot0: FaceCloseup（脸/头锚点）
  Shot1: ReturnBody + FOV punch + Impulse
大招结束 / 被高优打断：Pop → 恢复进入前的 Free 或 LockOn
```

切换：`CinemachineBrain` Blend + Priority；进出 LockOn / Shot 启用 **Inherit Position**（可按段配置硬切）。  
**旧稿 `ActionCue` 并入 `SkillShot`**，不再作为独立含糊模式。

### 3.4 与官方/开源对照（采纳点）

| 来源 | 采纳 |
|------|------|
| Cinemachine Target Group + 双 VCam | LockOn 同框与模式切换 |
| Starter Assets 枢轴模型 | 转目标、不硬拧 Main Camera |
| 论坛魂锁定论 | 勿在单个 FreeLook 上改 LookAt 硬锁 |
| 社区 Lock 样例 | 选敌评分、锁定 UI、切目标——不整包替换架构 |

---

## 4. 铁律（权威边界）

1. **禁止**用相机最终 Transform 作为命中、弹刀、移动权威朝向。  
2. **Motor 相机相对移动**只读 `InputFrame.MoveReferenceYawQuantized`；Camera 只 staged Orbit yaw，不直接写 Motor。
3. Look、FOV、Impulse、Blend **可抖、可丢**；不进 `InputFrame` / Sim Hash。  
4. 锁定**目标选择**以 `CharacterTargetingState.SelectedTargetId` 为唯一真源；相机不得维护第二套「当前敌人」。
5. 不在逻辑 `Step` 内驱动相机；仅 `LateUpdate`（且晚于 `World.Render`）。  
6. **Orbit yaw 可只读跟随角色移动朝向**（自由移动绕圈，见 [`../2026.8.10/LOCOMOTION_DIRECTIONAL_ANIMSET_PLAN.md`](../2026.8.10/LOCOMOTION_DIRECTIONAL_ANIMSET_PLAN.md) **L-DIR5**）：相机平滑改自己的 yaw 并 staged 到下一 InputFrame；**禁止**反写 Motor/Sim 朝向；Look 输入优先于自动跟随；仅 `CameraLockEnabled` 时关闭。

---

## 5. 子系统设计

### 5.1 FollowAnchor：前向跟随 / 忽略左右（定案可配）

目标：攻击侧向窜、侧步时镜头稳；前向突进仍跟随。

**与锚点拆分的关系（定案）：**

- **Wave 1：** 允许用 `lateralFollowFactor≈0` 临时止血（源点仍可能含逻辑横摆）。
- **Wave 2+：** 源头由 Gameplay/Residual 拆分去掉无效横摆；滤左右**降为**对合法横移、软碰撞与网络校正的构图缓冲，**不得**替代轨迹拆分。

```text
每帧（Render 之后）:
  source = CameraRoot.position
  forward = 玩家水平朝向（PresentationRoot.forward 投 XZ，归一化）
  delta = source - followPosition

  forwardPart = Dot(delta, forward) * forward
  verticalPart = (0, delta.y, 0)
  lateralPart = delta - forwardPart - verticalPart

  absorbed = forwardPart + verticalPart + lateralPart * lateralFollowFactor
  // lateralFollowFactor: 0 = 完全忽略左右；建议默认 0～0.2

  followPosition = SmoothDamp(followPosition, followPosition + absorbed, ...)
  距离 > SnapDistance 或传送 → 直接吸附 source
```

- 朝向取**角色表现朝向**，不用镜头 forward。  
- `lateralFollowFactor`、`followSmoothTime` 暴露到 Inspector / Profile SO。  
- LockOn 模式下可改用「直接跟 CameraRoot」或 Group，关闭滤左右。

### 5.2 PredictAnchor（可选，P1）

```text
predict = followPosition
        + flatForward * lookAheadDistance
        + flatVelocity * lookAheadTime   // clamp 速度贡献
```

- 仅 Free / 疾跑启用；LockOn、Action 中可减小或关闭。  
- Orbit 位置源：`usePredict ? Predict : Follow`（配置或模式决定）。

### 5.3 Lock-On

**输入 / 玩法（C-AT 完成态）：**

- 范围内由 CharacterTargetingState 自动维护 SelectedTarget；锁定键只开关 CameraLock
- `TargetSwitchLeft/Right` 经 InputFrame 切换目标，动作期间同样有效
- 目标死亡/超距后确定性补选；范围内无目标时 CameraLock 自动解除

**相机：**

1. 通过 `ILocalCameraTargetSource` 只读当前 SelectedTarget 与表现 LockPoint。
2. 激活 LockOn VCam；填充 `CinemachineTargetGroup`（玩家 + 敌人，Weight/Radius 可配）。  
3. 角色 strafing / 朝向由 Locomotion/Action 消费 SelectedTarget；相机只负责构图。
4. UI：锁定准星跟随敌人 `LockPoint`（纯表现）。

推荐 Body/Aim（CM2）：LockOn VCam 用 Transposer + GroupComposer（或 FramingTransposer + Composer），Screen 参数从中心保守调起。

### 5.4 反馈（CameraFeedback）

| 来源 | 效果 |
|------|------|
| 玩家命中（现有） | Impulse（保留） |
| 玩家受击 | 可选更轻 Impulse / 短 FOV |
| 弹刀 / 完美闪避成功 | 专用 Profile |
| 落地重击 | 可选 |

入口收敛为 `CameraFeedback`（或扩展现 `CameraShakeController`），订阅已结算事件；**禁止**在 Collect 阶段震屏。

### 5.5 技能 / 大招多段演出机位（`SkillShot`，对标绝区零向）

#### 5.5.1 需求陈述（本方案必须覆盖）

| 需求 | 说明 | 仅靠 Free/LockOn？ |
|------|------|-------------------|
| 放大招切机位 | 进入特殊技时抢过日常/锁定相机 | 否 |
| **多段镜头** | 如第 1 段贴脸特写，第 1 段结束切回身侧/过肩 | 否 |
| 段内反馈 | 特定帧震动、FOV 拉近/拉远、短推轨 | 否（Impulse 单独不够） |
| 结束后恢复 | 回到进入前的 Free 或 LockOn，可带 Blend | 需导演栈 |
| 招式定制 | 不同大招/角色不同 Shot 表（绝区零「定制镜头」） | 需数据驱动 |
| 可玩性 | 演出期间逻辑仍 Step；可配置是否冻结玩家 Look | 表现层策略 |

**结论：** 初版 §5.5「轻量 Cue」不足以描述上述需求；以本节 **`CameraShotSequence` + Director 模式栈** 为准。

#### 5.5.2 产品参考（绝区零思路，非照搬管线）

- 大招镜头多为**按招式定制的多段构图**，引导视线（脸/武器/纵深/尺度），而不是全程同一个第三人称。  
- 段与段之间可硬切或短 Blend；常叠加推拉、震动强化打击感。  
- 工程映射：每段 = 一个 Shot（VCam 绑定 + 锚点 + FOV/Impulse/Blend 参数），由 Action **逻辑帧窗口**或 Timeline 信号触发。

#### 5.5.3 数据模型

数据挂在独立 `ActionCameraShotSequence` SO（或纯序列化结构），由 `ActionDefinition` **引用**；避免把大段演出参数平铺进 Action 顶层。表现配置可放 `Domain/Camera` 或 `App` 旁路资产——**运行时只读，绝不写回 Sim**。

```text
CameraShotSequence
├─ restoreMode: PreviousGameplay | ForceFree | ForceLockOn
├─ suppressLookInput: bool
├─ suppressLockOnVCam: bool          // 演出期间隐藏日常 Lock 机位
├─ shots[]: CameraShot
│    ├─ id / debugName
│    ├─ enterFrame / exitFrame       // 相对本 Action 的逻辑帧 [闭区间或半开，定案用闭]
│    ├─ vcamKey 或 Prefab 引用       // FaceCloseup / BodyReturn / Custom_XXX
│    ├─ followAnchor: Face | Chest | CameraRoot | CustomTransformPath
│    ├─ lookAtAnchor: 同上或 EnemyLock | None
│    ├─ blendIn / blendOut           // 秒或帧；可 0=硬切
│    ├─ inheritPosition: bool
│    ├─ fovOverride / fovPunch       // 绝对 FOV 或相对脉冲曲线
│    ├─ dollyOffset                  // 相对 Follow 的本地前后/肩偏（米）
│    ├─ impulseOnEnter: CameraShakeProfile（可空）
│    └─ noiseGain（可选）
└─ (可选) timelineAsset              // 超复杂演出走 Timeline，仍由 Director 抢权
```

**两段大招示例（用户场景）：**

```text
Action: Ult_X
  Shot0: enter=0  exit=24
         follow=Face, lookAt=FaceForward
         fov=35, blendIn=0.05, impulse=Ult_Whoosh
  Shot1: enter=25 exit=end
         follow=Chest/CameraRoot, lookAt=Orbit或敌
         fov=50→55 punch, blendIn=0.12, impulse=Ult_Impact
  restoreMode=PreviousGameplay
```

#### 5.5.4 运行时驱动

```text
ActionSim 推进 currentFrame（权威）
  → 表现桥 / CameraShotPlayer 只读 (actionId, currentFrame)
  → 若帧落入某 Shot 窗口且与上一活跃 Shot 不同:
       Director.SetSkillShot(shot)  // Priority=80
       应用 FOV / ImpulseOnEnter / Blend
  → Action 结束或被打断:
       Director.ClearSkillShot()
       按 restoreMode 回到 Free/LockOn
```

- **时钟**：Shot 的 enter/exit 跟 **Action 逻辑帧**对齐（与 Timeline Notify 一致），禁止用 `normalizedTime` 当权威。  
- **打断**：更高优 `Finisher`/`Cutscene` 可覆盖 SkillShot；Action Cancel 到其他招式时清空或切换到新招 Sequence。  
- **FreezeFrames（卡肉）**：逻辑冻结时 `currentFrame` 不前进 → Shot 窗口保持；表现 Impulse 可另算。  
- **多人**：只本地玩家大招播 SkillShot；远端可降级为第三人称 + 轻 Impulse（后期定）。

#### 5.5.5 锚点扩展（演出用）

在角色 Prefab / Presentation 下增加（或运行时解析）：

| 锚点 | 用途 |
|------|------|
| `FaceAnchor` | 脸部特写 Follow/LookAt |
| `ChestAnchor` | 回身默认（可复用 CameraRoot） |
| `WeaponAnchor` | 武器特写（可选） |
| `UltCustom_*` | 单招定制空物体（动画师摆） |

脸部特写 VCam：短焦 FOV + 靠近 FaceAnchor；注意 Collider 在特写段可减弱或忽略，避免墙体把脸拍穿。

#### 5.5.6 与 Timeline 的分工

| 复杂度 | 方案 |
|--------|------|
| 2～4 段、跟动作帧走 | **`CameraShotSequence` + 逻辑帧窗口（主路径）** |
| 过场、QTE、超长定制轨 | Unity Timeline + Cinemachine Track，Director 仅负责 Push/Pop 模式 |
| 禁止 | 在 Animator StateMachine 里散落切相机，无统一恢复 |

#### 5.5.7 Action Editor 集成

- Action Timeline 增加 **Camera** 轨道：可视化 Shot 块（enter/exit）。  
- Scrub 预览：Editor 下可 scrub 逻辑帧看 VCam 切换（只读，不跑 Impulse 或可选预览）。  
- 校验：Shot 窗口重叠报错；exit &lt; enter 报错；缺锚点黄条。

### 5.6 轻量命中反馈 vs 技能 Shot

| | CameraFeedback（§5.4） | SkillShot（§5.5） |
|--|----------------------|-------------------|
| 触发 | 命中/受击/弹刀事件 | Action 帧窗口 |
| 时长 | 极短 | 可跨整段大招 |
| 机位 | 通常不切 VCam | **切 VCam / 锚点** |
| 用途 | 打击感点缀 | 绝区零向演出 |

二者可叠加：Shot1 进入时 Impulse + FOV punch。

### 5.7 目录建议

```text
Assets/Scripts/App/Controllers/Camera/
  CameraManager.cs              // 保留：Look、光标、Brain；驱动 Rig
  CameraDirector.cs             // 模式栈：Free/LockOn/SkillShot/Cutscene
  CameraRig.cs                  // 日常锚点写入
  CameraShotPlayer.cs           // 读 Action 帧 → 应用 ShotSequence
  CameraShakeController.cs      // 保留或收拢进 Feedback
  CameraFeedback.cs             // 事件震屏/FOV

Assets/Scripts/Domain/Camera/
  CameraShakeProfile.cs
  CameraShakeSettings.cs
  CameraFollowProfile.cs
  CameraShot.cs / CameraShotSequence.cs   // 数据（纯结构或 SO）

Assets/Scripts/Domain/Combat/Actions/.../
  CameraNotifyState.cs          // 或 Timeline 窗口类型，绑定 ShotSequence

Assets/Scripts/Domain/Combat/Targeting/
  CharacterTargetingState.cs
  ILocalCameraTargetSource.cs
```

场景：日常 VCam + LockOn VCam + **可复用的 Face/Body Skill VCam 池**（按 `vcamKey` 租用，避免每招实例化爆炸）。

---

## 6. 分阶段实施（映射总案 Wave）

> 勾选与开工顺序以 [MASTER_IMPLEMENTATION_PLAN.md](./MASTER_IMPLEMENTATION_PLAN.md) 为准。

### Phase C0 — 锚点 Rig + 输入权威前置（不切 LockOn）→ **Wave 1 / C-AT**

- [ ] 抽出 `CameraRig`：显式 `FollowAnchor`（可先与 Orbit 位置合一）  
- [ ] 实现 §5.1 `lateralFollowFactor`（默认 0 或 0.1；定位见上）  
- [x] Motor / 移动前向改读 `InputFrame.MoveReferenceYawQuantized`
- [ ] Editor gizmo：CameraRoot / Follow / Orbit  
- [ ] 保留现有单 VCam、震屏、PresentationRoot 时序  

**验收：** 侧向攻击位移时镜头明显更稳；前向突进仍跟随；传送仍 Snap。

### Phase C1 — Lock-On 双机位（**本篇排期，不挂 Wave 4**）

- [ ] 新增 LockOn VCam + TargetGroup  
- [ ] `CameraDirector`：`Free` / `LockOn` + Brain Blend + Inherit Position  
- [ ] 通过 `ILocalCameraTargetSource` 单向只读 SelectedTarget；CameraLock 不选择目标
- [ ] 锁定 UI 指示  
- [ ] LockOn 下 Look 输入策略（关或微距）  

**验收：** 锁定后可绕敌 strafing 且人、敌基本同框；解锁回 Free 无硬切跳变。

### Phase C2 — Predict + 反馈扩展（可选，本篇排期）

- [ ] `PredictAnchor` 与疾跑/速度 lead  
- [ ] 受击 / 弹刀等 Impulse 或 FOV Punch（§5.4）  

**验收：** 疾跑构图略看前方；弹刀有独立镜头反馈且不进逻辑。

### Phase C3 — 多段技能 / 大招 `SkillShot`（**本篇排期，不挂 Wave 5**）

- [ ] `CameraShot` / `CameraShotSequence` 数据 + Action 引用  
- [ ] `FaceAnchor` / 复用 Chest；Face + Body 两套可租用 VCam  
- [ ] `CameraShotPlayer`：按 `ActionSim.CurrentFrame` 切 Shot  
- [ ] `CameraDirector` 模式栈 Push/Pop；`restoreMode=PreviousGameplay`  
- [ ] 段内 FOV override/punch + `impulseOnEnter`  
- [ ] 一条测试大招：段0 脸部特写 → 段1 回身 + 震/拉镜  
- [ ] Action Editor Camera 轨道（可视 Shot 块，可后置一迭代）  

**验收：** 放大招自动特写，第一段结束后回身；Look 被抑制；招式结束或打断后恢复进入前 Free/LockOn；逻辑命中不依赖镜头。

### Phase C4 — 重度演出（后置）

- [ ] Timeline + CM Track 的 Finisher/过场  
- [ ] 关卡触发高优先级 VCam  
- [ ] 每角色定制 UltCustom 锚点与 VCam 池扩展  

**验收：** 演出结束正确回到 Gameplay 模式；与 SkillShot 优先级不互相卡死。

---

## 7. 配置与调参（首版暴露）

| 参数 | 建议起点 | 说明 |
|------|----------|------|
| `lateralFollowFactor` | 0～0.15 | 0=忽略左右 |
| `followSmoothTime` | 0.08～0.15 | 与现 Orbit 平滑协调 |
| `lookAheadDistance` | 0.3～1.0 m | Predict |
| `lookAheadTime` | 0～0.1 s | 速度超前 |
| LockOn Group Weight | 玩家 1 / 敌 1 | 按体型调 Radius |
| Blend Free↔Lock | 0.2～0.4 s | 防晕 |
| SkillShot Face FOV | 30～40 | 特写 |
| SkillShot Body FOV | 50～60 | 回身 |
| Shot blendIn | 0～0.15 s | 绝区零感可偏硬切 |

---

## 8. 测试计划

| 用例 | 类型 |
|------|------|
| 站立转视角 / Pitch 夹角 | Play Mode |
| 前向冲刺 vs 侧向攻击位移时镜头对比 | Play Mode |
| 逻辑帧插值后相机不阶梯抖 | Play Mode（30/60/144 FPS） |
| 锁定 / 解锁 / 切目标 / 目标死亡丢失 | Play Mode |
| 命中震屏仍仅结算后触发 | 回归 |
| 挤墙时移动方向不跟歪镜头 | Play Mode |
| **两段大招：脸特写 → 回身 + 震/FOV** | Play Mode |
| 大招中 Cancel / 受击打断后模式恢复 | Play Mode |
| 大招中卡肉：Shot 不因表现时间错位提前切 | Play Mode |
| LockOn 中放大招，结束后仍回到 LockOn | Play Mode |

---

## 9. 风险与对策

| 风险 | 对策 |
|------|------|
| 滤左右导致横向走位人出画 | `lateralFollowFactor>0` 或疾跑时提高；LockOn 用 Group |
| Free↔Lock 跳变 | Inherit Position + 共享 Follow 高度；Blend 调参 |
| TargetGroup 过渡「低头」 | Group 成员高度一致；LookAt 用胸口/头 |
| 双套锁定目标 | 只认 `CharacterTargetingState.SelectedTargetId`；CameraLock 只保存模式 bool |
| **SkillShot 与逻辑帧不同步** | enter/exit 只认 ActionSim 帧；禁 normalizedTime 权威 |
| **特写穿墙 / 脸被 Collider 顶飞** | 特写段降低或关闭 Collider；锚点略外偏 |
| **每招一个 VCam Prefab 爆炸** | `vcamKey` 租用池 + 少量定制 |
| **演出抢权后回不去 LockOn** | Director 栈保存 `PreviousGameplay` |
| 与锁步文档 CameraFeedback 命名不一致 | 以本文为准 |
| CM 2→3 API 差异 | 本方案按 2.10；升级单开迁移 |

---

## 10. 明确非目标

- 不在相机侧重写 `PresentationRoot` 插值算法；**逻辑横摆治理由 Movement Anchor Wave 2 负责**，相机只做跟随与构图  
- 不弃用 Cinemachine 全自研弹簧臂（C0–C2）  
- 不把 Look / Shot / 锚点写入 `InputFrame` / Sim  
- 不做正式战斗 HUD 美术（锁定点可用简单 UI）  
- 不在本阶段做联机观战相机  
- **不要求 C0 就达到绝区零单角色全定制镜头产能**；C3 先打通「两段式大招」管线，定制量随内容迭代  
- 不在 Animator 状态机里散落切相机  
- **不以滤左右替代** Gameplay/Residual 轨迹拆分  
- Agent **不直接改** `.asset` / Prefab 微调数值；代码落地后列 Editor 手工步骤  

---

## 11. 决策记录

| 日期 | 决策 | 理由 |
|------|------|------|
| 2026-08-05 | 混合架构：Director + 多锚点 + 多 VCam | 对齐业界 ACT；复用现有 CM 与 PresentationRoot |
| 2026-08-05 | Follow 可配置忽略左右位移 | 稳攻击侧向；前向仍跟随 |
| 2026-08-05 | 索敌真源唯一（已于 2026-08-13 落为 CharacterTargetingState） | 避免相机与战斗朝向不一致 |
| 2026-08-13 | 移动前向改读 InputFrame.MoveReferenceYaw | 避免 Collider 挤墙与渲染时序进入逻辑 |
| 2026-08-05 | 联机暂缓；相机纯表现优先落地 | 与战斗框架并行，不堵网络选型 |
| 2026-08-05 | **补强多段 `SkillShot` / `CameraShotSequence`** | 原 ActionCue 过薄；覆盖「脸特写→回身+震/FOV」等绝区零向大招 |
| 2026-08-05 | Shot 窗口对齐 Action 逻辑帧 | 与卡肉/Cancel 一致；避免动画归一化时间漂移 |
| 2026-08-05 | 日常用帧窗口 Sequence；超重演出才上 Timeline | 降低普招/大招配置成本 |
| 2026-08-06 | 滤左右=Wave1 止血；Wave2 后降为缓冲 | 与锚点拆分分工，避免治标当治本 |
| 2026-08-06 | CameraRoot 与 VisualMotionRoot 并列 | 防止残差进镜头源 |

---

## 12. Editor 人工步骤（实现后）

1. 确认场景 `SimulationHost` 与 `CameraManager` 并存；Play 时 PresentationRoot 非空。  
2. 为玩家/敌人配置 LockPoint；玩家加 `FaceAnchor`。  
3. 调 `CameraFollowProfile`：侧向攻击时看 Follow gizmo 是否少左右跳。  
4. 配 Free↔LockOn Blend；开/关锁定检查 Inherit Position。  
5. 回归命中震屏 Profile 与 HitPayload 引用。  
6. **配一条测试 Ult 的 `CameraShotSequence`（两段）并 Play 验收特写→回身。**  
7. 在 LockOn 状态下放大招，确认结束后仍锁定。  

---

## 13. 成功标准

- [ ] Free 模式：前向跟、左右可滤、避障与震屏不回归  
- [ ] LockOn：只读 SelectedTarget，双人基本同框，进出与切敌平滑
- [ ] 多锚点在 Scene 可调试；职责不再全堆在 `CameraRoot`  
- [ ] Motor 不再依赖挤墙后的 `Camera.main.forward`  
- [ ] 无相机写回 Sim / 命中权威  
- [ ] **多段大招 SkillShot：可特写、可回身、可段内震/FOV，结束后恢复 Gameplay 模式**  

---

## 14. 一句话

把现有「单 VCam + 追 PresentationRoot」升级为 **多锚点 Rig + CameraDirector 模式栈（Free/LockOn/SkillShot）+ 事件反馈**；大招用 **逻辑帧驱动的多段 `CameraShotSequence`** 做绝区零向演出，逻辑仍只认 Actor 与 TargetLock，相机永远是晚于 `World.Render` 的表现层。
