# 角色稳定移动锚点优化方案

> 基准：`develop`（2026-08-06）  
> 修订：2026-08-06 — 对齐总案 Wave、收束首版轨迹模式、补 Editor 步骤  
> 目标：角色逻辑控制器只沿稳定、可碰撞、可回滚的主要路径移动；动画中的高频左右摆动保留为视觉表现，不再直接推动角色权威根和相机。  
> **排期真源：** [MASTER_IMPLEMENTATION_PLAN.md](./MASTER_IMPLEMENTATION_PLAN.md)  
> 关联：[ACTION_DEFINITION_OPTIMIZATION_PLAN.md](./ACTION_DEFINITION_OPTIMIZATION_PLAN.md)、[CAMERA_SYSTEM_PLAN.md](./CAMERA_SYSTEM_PLAN.md)

## 1. 结论

当前问题不是 Unity `CharacterController` 被动画骨骼直接拖动。

- `CharacterController` 创建后被禁用，不再调用 `CharacterController.Move`。
- 真正的位置权威是 `CharacterMotorSim`。
- `CharacterMotor.SyncRootFromSim()` 每个逻辑步把 `MotorSim` 坐标写回玩家根 Transform。
- 动作位移仍来源于动画：
  - 已烘焙动作：`ActionBakedMotion` 的逐帧 X/Z 位移进入 `MotorSim`。
  - 未烘焙动作：`OnAnimatorMove()` 的 `Animator.deltaPosition` 进入 `MotorSim`。
- 因而动画 Root Motion 中的左右摆动、回拉和高频位移，会间接成为控制器、碰撞体和相机锚点的真实移动。

现有相机 `SmoothDamp` 只能降低抖动，不能区分：

1. 角色真正需要沿场景移动的主要轨迹；
2. 仅用于动作张力的模型局部摆动。

推荐把一条动画根轨迹拆成两部分：

```text
原始动画轨迹 FullTrajectory
  ├─ GameplayTrajectory     // 权威主要路径：MotorSim、碰撞、受击体、锁定
  └─ VisualResidual         // 视觉残差：模型左右摆动、回拉、夸张重心变化
```

运行时关系：

```text
GameplayTrajectory ──> CharacterMotorSim ──> SimulationRoot
                                                │
                                                ├─> PresentationRoot（固定帧插值）
                                                │      └─> VisualMotionRoot（视觉残差）
                                                │             └─> Model / Animator
                                                │
                                                └─> 碰撞、目标锁定、角色受击体

PresentationRoot ──> StableCameraAnchor ──> Camera Orbit
```

这项优化应在数据烘焙阶段提取主要路径。不要在 `MotorSim` 运行时使用 `SmoothDamp` 或浮点低通滤波，否则会改变碰撞结果、增加状态量并破坏回滚确定性。

---

## 2. 当前实现分析

### 2.1 角色层级

`CharacterActorFactory` 当前创建：

```text
Player / SimulationRoot
├─ CharacterController（disabled）
└─ CharacterPresentationRoot
   └─ ModelPrefab
      └─ Animator
```

职责：

| 对象 | 当前职责 |
|---|---|
| `CharacterMotorSim` | 水平毫米坐标、Y、高度速度、朝向与碰撞权威 |
| `Player/SimulationRoot` | `MotorSim` 的 Unity 镜像 |
| `CharacterController` | 仅保留半径/高度配置，目前禁用 |
| `CharacterPresentationRoot` | 在前后两个逻辑 Pose 间做渲染插值 |
| `Model/Animator` | 播放动画和提供骨骼挂点 |

因此后续文档中的“控制器锚点”应明确指：

```text
CharacterMotorSim + SimulationRoot
```

而不是已禁用的 Unity `CharacterController` 组件。

### 2.2 普通移动链路

```text
InputFrame / InputManager
  -> LocomotionStateMachine
  -> CharacterMotor.ApplyLocomotion()
  -> CharacterMotorSim.TryMoveWorldMeters()
  -> 碰撞解析
  -> CharacterMotor.SyncRootFromSim()
```

普通移动按输入方向和速度生成轨迹，通常不会包含动画左右摆动。

### 2.3 动作位移链路

已烘焙动作：

```text
ActionSim 当前逻辑帧
  -> CharacterActionPresentationBridge.ApplyBakedMotionDisplacement()
  -> ActionBakedMotion.TryGetDelta()
  -> CharacterMotor.MoveLocalMm()
  -> CharacterMotorSim.TryMoveLocalMm()
  -> SimulationRoot
```

未烘焙动作：

```text
Animator.OnAnimatorMove()
  -> Animator.deltaPosition
  -> CharacterMotor.MovePlanar()
  -> CharacterMotorSim
  -> SimulationRoot
```

所以用户观察到的现象基本准确，但准确表述是：

> 权威根没有跟随骨骼 Transform；它在消费由动画根轨迹生成的位移数据。动画根轨迹中的高频横摆被当成了游戏逻辑位移。

### 2.4 相机为何随之横跳

当前相机链路：

```text
SimulationRoot
  -> CharacterPresentationBridge
  -> PresentationRoot
  -> CameraRoot
  -> OrbitPivot SmoothDamp
  -> Cinemachine VirtualCamera
```

`PresentationRoot` 只解决固定 60 Hz 位姿的阶梯感。`CameraManager` 又对完整 XYZ 做一次 `SmoothDamp`，但源位置本身仍包含动作横摆，因此：

- 低频、大幅横摆仍会被镜头明显追随；
- 高频横摆会使镜头持续改变追赶方向；
- 动作结束回到原轴线时，镜头可能继续惯性追赶；
- 增大 `followSmoothTime` 会带来正常移动时的镜头滞后。

### 2.5 现有 `ForwardOnly` 的问题

当前 `ActionBakedMotion.TryGetDelta()` 对每帧执行：

```text
mag = sqrt(dx² + dz²)
dx = 0
dz = sign(originalDz) * mag
```

它不是“提取主要前进路径”，而是把横向位移的长度改造成前进位移。

例如：

```text
原始：dx = 100 mm, dz = 0 mm
结果：dx = 0 mm,   dz = 100 mm
```

后果：

- 纯左右摆动会凭空增加前进距离；
- 高频横摆会形成高频前进速度波动；
- 原轨迹终点和新轨迹终点没有稳定对应关系；
- 动画越夸张，控制器可能前进得越多。

该模式应迁移为基于“累计轨迹”的投影，不能继续对每帧 Delta 保模长转换。

---

## 3. 设计边界

### 3.1 必须由稳定锚点承载

- 静态场景碰撞；
- 角色间软碰撞；
- 角色受击体中心；
- 目标锁定距离和选敌距离；
- AI 导航位置；
- 网络快照、预测和回滚状态；
- 相机的基础跟随位置；
- 攻击吸附、冲刺和瞬移等明确的玩法位移。

### 3.2 可以留在视觉残差

- 挥击时肩、胯和整个人体的左右压重心；
- 出招前的小幅后拉；
- 连段中的高频左右摆动；
- 不承担躲避、穿越或攻击距离语义的局部位移；
- 仅用于构图和打击张力的小幅旋转。

### 3.3 不能自动隐藏的玩法位移

以下位移必须进入 `GameplayTrajectory`，不能全部塞进视觉残差：

- 侧闪、横移斩和绕背；
- 能实际避开攻击的后撤；
- 改变攻击距离的突进；
- 穿过目标或越过障碍物；
- 需要参与碰撞和命中判定的位移。

判断标准不是“动画看起来是否夸张”，而是“该位移是否改变玩法空间关系”。

---

## 4. 新数据模型

### 4.1 轨迹策略

新增：

```csharp
public enum ActionGameplayTrajectoryMode
{
    Exact,             // 完整原始水平轨迹进入逻辑
    ForwardSigned,     // 只取原始累计轨迹的本地 Z
    Stationary,        // 逻辑根不移动，全部成为视觉残差
    Authored           // 设计师显式编辑的逻辑轨迹
}
```

**首版仅上表四态。** `DominantAxis`、运行时 `Smoothed` 不做；离线 RDP/低通只可作为生成 `Authored` 初稿的 Editor 工具，烘焙后仍是整数表。

推荐用法：

| 动作类型 | 默认策略 |
|---|---|
| 原地普攻、蓄力、受击 | `Stationary` |
| 直线连击、短突进 | `ForwardSigned` |
| 侧闪、横移斩、绕背 | `Exact` 或 `Authored` |
| 斜向/复杂 Boss 位移 | `Authored`（可用离线简化生成初稿） |

### 4.2 烘焙结果

将动作运动数据明确拆为：

```csharp
[Serializable]
public sealed class ActionBakedTrajectory  // 由现有 ActionBakedMotion 演进，禁止并行两套表
{
    public int logicHz;                    // 仅校验，必须 = 60
    public int frameCount;                 // 必须 = Action.TotalFrames
    public ActionBaseMotionMode baseMotionMode;       // None | BakedMotion | ScriptedTimeline
    public ActionGameplayTrajectoryMode gameplayTrajectoryMode;

    // 逻辑权威：逐帧 Delta，整数毫米（BakedMotion 时有效）
    public int[] gameplayDeltaMmX;
    public int[] gameplayDeltaMmZ;

    // 纯表现：相对逻辑根的逐帧局部偏移（绝对采样，不累计误差）
    public int[] visualResidualMmX;
    public int[] visualResidualMmZ;
    // yaw 残差：首版可空；有消费方再烘焙

    public string sourceHash;
    public int dataVersion;
}
```

两组数据采用不同语义：

- `gameplayDelta` 是逐帧增量，直接进入 `MotorSim`；
- `visualResidual` 是绝对局部偏移，直接按动作帧采样，不累计误差。

不要让运行时再次从 `visualResidual` 推导逻辑位移。

### 4.3 正确的轨迹分解

编辑器先采样完整累计轨迹：

```text
Full[frame] = 动画根相对动作起点的累计局部 Pose
```

再按策略得到：

```text
Gameplay[frame] = ExtractMainPath(Full, mode, authoredSettings)
Residual[frame] = Inverse(Gameplay[frame]) * Full[frame]
GameplayDelta[frame] = Inverse(Gameplay[frame-1]) * Gameplay[frame]
```

首版只处理 X/Z 时可简化为向量相减；数据结构仍应保留未来旋转扩展空间。

`ForwardSigned` 必须使用：

```text
GameplayPosition[frame] = (0, FullPosition[frame].z)
```

不能使用每帧 `sqrt(dx² + dz²)`。

### 4.4 `Smoothed` 的离线提取

推荐顺序：

1. 对累计轨迹进行 Ramer–Douglas–Peucker 简化或可控窗口低通；
2. 固定动作起点和终点；
3. 把简化曲线重采样到每个逻辑帧；
4. 量化为毫米；
5. 将量化余数分配到后续帧，确保最终累计位移精确；
6. 保存整数结果及内容哈希。

禁止运行时按渲染帧滤波。相同动作资产在不同帧率、不同机器上必须产生相同的逻辑轨迹。

---

## 5. 新运行时层级

调整为：

```text
Player / SimulationRoot                 // MotorSim 权威
├─ CharacterController（保留配置，disabled）
└─ CharacterPresentationRoot            // 固定帧插值
   ├─ CameraRoot                        // 相机源点，不含视觉残差
   └─ CharacterVisualMotionRoot         // 动作视觉残差
      └─ ModelPrefab
         └─ Animator
```

### 5.1 `CharacterMotorSim`

继续只消费：

- Locomotion 位移；
- `gameplayDelta`；
- `MotionModifier` 修改后的最终逻辑轨迹；
- 明确的 Teleport / Warp。

它不感知模型、动画和视觉残差。

### 5.2 `CharacterVisualMotionBridge`

新增纯表现服务：

```csharp
public sealed class CharacterVisualMotionBridge
{
    public void BeginAction(ActionBakedTrajectory trajectory);
    public void CaptureSimulationFrame(int actionFrame);
    public void Render(float interpolationAlpha);
    public void EndAction(VisualResidualExitPolicy exitPolicy);
}
```

职责：

- 从当前/上一动作帧读取 `visualResidual`；
- 在渲染阶段插值后写入 `CharacterVisualMotionRoot.localPosition/localRotation`；
- 动作取消、受击或切段时处理残差退出；
- 不写 `SimulationRoot`、`MotorSim` 或 `CharacterController`。

### 5.3 残差退出策略

新增：

```csharp
public enum VisualResidualExitPolicy
{
    RequireZeroAtEnd,
    BlendToZero,
    SnapToZero
}
```

默认：

- 正常结束：`RequireZeroAtEnd`，烘焙检查末帧残差；
- 被取消或受击打断：`BlendToZero`，用短时表现插值回原点；
- 传送、换角色、死亡：`SnapToZero`。

`BlendToZero` 只移动模型，不移动逻辑根，因此不会影响确定性。

---

## 6. 与动作位移扩展的关系

稳定主要路径与 [ACTION_DEFINITION_OPTIMIZATION_PLAN.md](./ACTION_DEFINITION_OPTIMIZATION_PLAN.md) 中的运动命令分工如下：

```text
ActionBakedTrajectory.GameplayDelta
  -> MotionModifier（吸附、距离裁剪、目标修正）
  -> MotionCommand
  -> CharacterMotorSim
```

视觉残差走独立路径：

```text
ActionBakedTrajectory.VisualResidual
  -> CharacterVisualMotionBridge
  -> CharacterVisualMotionRoot
```

示例：

### 攻击吸附

- 先读取该帧 `gameplayDelta`；
- `AdhesionModifier` 根据目标距离调整前进量；
- 最终结果进入碰撞解析；
- 视觉残差仍相对修正后的逻辑根播放。

### 瞬移到敌人身后

- 指定逻辑帧生成 `TeleportBehindTargetCommand`；
- `MotorSim` 校验落点并更新稳定锚点；
- `PresentationRoot` 按传送规则吸附；
- `VisualMotionRoot` 重置或按镜头演出策略处理；
- 不能把这类位移归为视觉残差。

### 横移斩

- 横向位移具有玩法意义；
- 使用 `Exact` 或 `Authored`；
- 相机可以额外抑制横向跟随，但控制器必须真实横移。

---

## 7. 命中盒、受击体与碰撞

### 7.1 角色身体

以下始终跟随 `GameplayRoot`：

- 静态碰撞圆；
- 角色软碰撞；
- 受击体主中心；
- 目标选择与距离；
- AI 和导航位置。

视觉残差不能推动这些对象，否则又会恢复原问题。

### 7.2 攻击命中盒

攻击命中盒需要视觉对齐，但不能长期依赖实时 Animator Transform。

**Wave 2～4（短期，不阻塞锚点闭环）：**

- `HitboxFrameConsumer` 仍可读取骨骼挂点相对 `SimulationRoot` 的局部 Pose；
- 由于模型位于 `VisualMotionRoot` 下，计算出的局部挂点会包含视觉残差；
- 必须保证逻辑步采样时 `VisualMotionRoot` 处于该逻辑动作帧，而不是上一渲染帧。

**Wave 5 后置（独立里程碑 M4，不阻塞 Wave 2 出口）：**

```text
动画骨骼 / 挂点轨迹
  -> Editor 烘焙
  -> 每逻辑帧局部整数/定点 Hitbox Pose
  -> SimCombatPose + GameplayRoot 合成世界命中盒
```

这样攻击盒能跟随武器和身体动作，同时不读取运行时 Animator，也适合锁步与回滚。

### 7.3 视觉残差安全阈值

模型离逻辑胶囊过远会产生“看见角色在那里，但碰不到”的错觉。烘焙器应报告：

- 最大横向残差；
- 最大前后残差；
- 残差超过胶囊半径的持续帧数；
- 命中窗口期间的最大残差；
- 动作结束残差。

建议初始警告值：

```text
普通攻击最大水平残差：0.25 m
命中窗口最大水平残差：0.35 m
正常结束残差：0.02 m
```

这些是项目调参起点，不是固定规则。超限动作应把一部分位移提升到 `GameplayTrajectory`，或显式调整命中/受击设计。

---

## 8. 相机配合

该方案会从源头移除大部分非玩法横摆，但相机仍应保留独立稳定层。

推荐链路：

```text
PresentationRoot
  -> CameraRoot
  -> StableFollowAnchor
  -> Orbit/Pitch
  -> Cinemachine
```

规则：

- `CameraRoot` 必须是 `PresentationRoot` 的直接子节点，不能放在 `VisualMotionRoot` 下；
- 日常相机跟随 `StableFollowAnchor`；
- `StableFollowAnchor` 可使用 [CAMERA_SYSTEM_PLAN.md](./CAMERA_SYSTEM_PLAN.md) 中的横向吸收系数（Wave 1 止血；Wave 2 后降为构图缓冲）；
- 传送、切场景、锁定切换使用显式 Snap；
- 镜头震动只进入 Cinemachine/Impulse 表现层；
- 相机滤波不能反向修改 GameplayRoot。

两层各自解决不同问题：

| 层 | 解决的问题 |
|---|---|
| 轨迹分解 | 控制器、碰撞、相机源点不再跟随动画噪声 |
| 相机稳定锚点 | 对合法横移、软碰撞修正和网络校正做镜头构图缓冲 |

只增加相机 `SmoothDamp` 会掩盖镜头症状，但控制器和碰撞仍会左右横跳。

---

## 9. 文件级改造建议

### 修改

```text
Assets/Scripts/Domain/Simulation/Motion/ActionBakedMotion.cs
Assets/Scripts/Domain/Character/CharacterActorFactory.cs
Assets/Scripts/Domain/Character/CharacterActor.cs
Assets/Scripts/Domain/Character/Presentation/CharacterActionPresentationBridge.cs
Assets/Scripts/App/Controllers/Camera/CameraManager.cs
```

### 新增

```text
Assets/Scripts/Domain/Simulation/Motion/ActionBakedTrajectory.cs
Assets/Scripts/Domain/Simulation/Motion/ActionGameplayTrajectoryMode.cs
Assets/Scripts/Domain/Character/Presentation/CharacterVisualMotionBridge.cs
Assets/Scripts/Editor/ActionMotion/ActionTrajectoryExtractor.cs
Assets/Scripts/Editor/ActionMotion/ActionTrajectoryPreview.cs
Assets/Tests/EditMode/ActionTrajectoryExtractorTests.cs
Assets/Tests/PlayMode/CharacterStableAnchorTests.cs
```

### 逐步废弃

```text
ActionMotionPlanarMode.ForwardOnly       // 当前逐帧保模长语义
CharacterRootMotionDriver               // 所有正式动作完成烘焙后移除
Animator Root Motion 作为逻辑兜底         // 仅迁移期允许
```

`CharacterActionPresentationBridge` 当前同时执行动画、时间轴和权威位移。结合动作系统优化计划，最终应由 `ActionRuntimeCoordinator` 执行 `gameplayDelta -> MotionCommand -> MotorSim`，表现桥只接收结果和视觉事件。

---

## 10. 实施阶段（映射总案 Wave）

> 勾选与开工顺序以 [MASTER_IMPLEMENTATION_PLAN.md](./MASTER_IMPLEMENTATION_PLAN.md) 为准。

### M0：诊断可视化 → **Wave 0**

- 绘制原始动画累计轨迹；
- 绘制当前烘焙后逻辑轨迹；
- 绘制 `MotorSim` 圆和相机锚点；
- 显示每帧 `raw dx/dz`、`gameplay dx/dz` 和残差；
- 选取一段高频横摆动作记录控制器峰峰值。

完成标准：可以证明横跳来自运动表或 Animator Root Motion，而不是骨骼 Transform、软碰撞或相机自身。

### M1：修正 `ForwardOnly` → **Wave 1**

- 引入数据版本；
- 新增 `ForwardSigned`；
- 在累计轨迹上提取 Z，再做差分；
- 旧 `ForwardOnly` 资产标记为需重烘焙；
- 不在运行时静默改变旧资产语义。

完成标准：纯横摆动画不会产生额外前进距离。

### M2：烘焙轨迹分解 → **Wave 2**

- 生成 `GameplayDelta`；
- 生成 `VisualResidual`；
- 提供 `Exact / ForwardSigned / Stationary / Authored`；
- 输出终点误差、残差峰值和超限报告；
- Scene/Inspector 同时预览 Full、Gameplay 和 Residual。

完成标准：设计师能在编辑器中直接判断“角色逻辑走哪里、模型相对锚点怎么摆”。

### M3：视觉根运行时 → **Wave 2**

- 工厂增加 `CharacterVisualMotionRoot`；
- 模型改挂在该节点下；
- 新增 `CharacterVisualMotionBridge`；
- 处理正常结束、取消、受击和传送；
- 相机与身体受击体保持跟随 `PresentationRoot` / 逻辑根（`CameraRoot` 不在 Visual 下）。

完成标准：模型保留原动作张力，`MotorSim` 不再复制无玩法意义的横摆。

### M4：命中盒确定性 → **Wave 5 后置**（不阻塞 Wave 2 出口）

- Wave 2 内仅校正逻辑步 VisualResidual 采样时序；
- 烘焙武器/骨骼挂点轨迹；
- `HitboxFrameConsumer` 不再读取实时 Animator Transform；
- 回归命中位置和视觉一致性。

完成标准：相同输入重放得到相同命中结果。

### M5：正式资产迁移与删 RM 回退 → **Wave 2 出口**

- 批量迁移正式 Action；
- 移除正式动作的 Animator Root Motion 兜底；
- 校验全部运动表哈希；
- 保留开发期明确报错，不做隐式回退。

完成标准：相机稳定、逻辑确定、所有正式动作只有一个权威位移源。

### 10.1 Editor 人工步骤（M3/M5 代码完成后）

1. 打开玩家 Prefab：在 `CharacterPresentationRoot` 下确认 `CameraRoot` 与 `CharacterVisualMotionRoot` 并列；Model 仅在 Visual 下。  
2. 对基准横摆招与全部旧 `UseRootMotion` 正式招执行重烘焙（Gameplay + Residual）。  
3. 用 Scene Gizmo 核对 Full / Gameplay / Residual；残差超限者改 `Stationary`/`ForwardSigned` 或把玩法位移升为 Exact/Authored。  
4. Play：关闭 Animator 组件，确认仍能位移与命中；再开启核对视觉摆动。  
5. 跑全库运动表校验，Error=0 后再进 Wave 3。

---

## 11. 测试与验收

### 编辑器测试

- `ForwardSigned` 不把纯 X 位移转换成 Z；
- 各模式起点严格为零；
- `Gameplay + Residual` 能重建原始轨迹；
- 量化后逻辑终点误差不超过 1 mm；
- 同一输入资产重复烘焙得到相同哈希；
- 数据版本不匹配时拒绝进入正式构建。

### 运行时测试

- 高频左右摆动普攻：`MotorSim` 横向峰峰值在配置阈值内；
- 横移斩：保留设计要求的逻辑横移和碰撞；
- 动作取消：模型平滑回锚点，逻辑根不被拖动；
- HitStop：逻辑轨迹和视觉残差都停在同一动作帧；
- 攻击吸附：只修改 GameplayTrajectory；
- 瞬移：逻辑根、PresentationRoot 和相机按策略 Snap；
- 低帧率和高帧率下逻辑终点一致；
- 录制同一输入重放，逐帧 `MotorSim` 哈希一致。

### 镜头验收

- 原地连击不再推动基础相机锚点左右往返；
- 正常跑动的相机延迟不因攻击防抖而显著增大；
- 合法侧闪仍能被镜头按配置追随；
- 镜头震动与角色轨迹互不污染。

---

## 12. 推荐落地顺序

跨系统顺序见 [MASTER_IMPLEMENTATION_PLAN.md](./MASTER_IMPLEMENTATION_PLAN.md) Wave 0～2。本域最小闭环：

```text
M0 可视化
  -> M1 修正 ForwardOnly
  -> M2 Gameplay/Residual 双轨迹
  -> M3 VisualMotionRoot
  -> 与 Action A2 同步删除 RM 权威回退
  -> 相机跟 PresentationRoot/CameraRoot（无残差）
```

命中挂点烘焙（M4）后置到总案 Wave 5，不阻塞本闭环。

最终原则：

> 动画可以围绕角色锚点做夸张运动；只有经过动作资产明确批准的主要路径，才能移动角色的逻辑控制器。
