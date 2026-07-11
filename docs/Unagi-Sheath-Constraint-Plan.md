# Unagi（星见雅）刀鞘收纳穿模修正计划

> 目标：在不重做整套攻击动画的前提下，用程序约束 / 弱 IK 消除收刀段刀与鞘穿模。  
> 依据仓库：本仓库 ACTGame（资源同 `Assets/Art/Arts/雅/`）  
> 日期：2026-07-11  
> 修订：约束方向改为「**刀骨为真值 → 约束刀鞘**」

---

## 1. 背景与问题

Unagi 攻击动画的收招过程中，右手持刀、左手持鞘，配合将刀收入刀鞘。由于动画关键帧不够精确，收刀段出现刀刃与刀鞘穿模。

本计划采用「**刀轴为真值 → 约束鞘骨 → 左手可选跟随**」的方案，只覆盖穿模窗口：

- **保留**刀与右手的动画表演（攻击收招的视觉主体）
- **修正**鞘的位姿，使鞘口/鞘轴贴合当前刀刃，消除穿模

---

## 2. 仓库现状摘要

| 项 | 结论 |
|---|---|
| 角色目录 | `Assets/Art/Arts/雅/` |
| 动画资源 | `UnagiEnd.fbx`（Git LFS，约 330MB） |
| Prefab / Controller | `星见雅.prefab`、`雅.controller`（LFS） |
| 武器模型 | `wuqi01.FBX` |
| Rig 类型 | **Generic**（非 Humanoid），根骨 `Bip001` |
| Clip 数量 | 约 92 条 |
| 独立拔刀/收刀 Clip | **无** |
| 收刀相关事件 | `Attack_03` 上有 `YaShouDao`（约 `time ≈ 0.39`） |
| 收招段 | 各 `Attack_*_End`（穿模重点区间） |
| 现有运行时 | `CharacterAnimationService` 仅 CrossFade / `PlayClip`，**无收刀约束 / IK** |
| 招式驱动 | Action 时间轴 + `PlayClip`（非依赖 Animator End 状态机） |

> 本地需 `git lfs pull` 才能导入真实 FBX。

---

## 3. 骨骼与挂点约定

### 3.1 现有骨骼（来自 `UnagiEnd.fbx.meta`）

```
Bip001
├── Bn_Weapon
│   ├── Bn_scabbardA_01 → Bn_scabbardA_02 → Bn_scabbardA_03
│   └── Bn_scabbardB_01 → Bn_scabbardB_02 → Bn_scabbardB_03
└── Bn_katana_hilt → Bn_katana_hilt2 / Bn_katana_burst*

手部：Bip001 R Hand / Bip001 L Hand
网格：Unagi_Weapon01 ~ Unagi_Weapon05（挂在角色根）
```

要点：`Bn_katana_hilt` 与 `Bn_Weapon`（鞘链）同为 `Bip001` 子级，**均不挂在手上**；动画独立驱动刀与鞘。

### 3.2 计划新增挂点（Prefab 上，Editor 人工添加）

| 挂点名 | 建议挂载 | 用途 |
|---|---|---|
| `BladeTip` | `Bn_katana_hilt` 子节点 | **真值**：刀尖世界坐标 |
| `BladeHilt` / 刀柄采样 | `Bn_katana_hilt` | **真值**：刀柄；与 Tip 构成刀轴 |
| `SheathMouth` | `Bn_scabbardA_01`（或实际贴合的鞘链） | 鞘口；约束时对齐到刀轴上的目标点 |
| `SheathEnd` | `Bn_scabbardA_03` | 鞘底；与 Mouth 构成鞘轴，用于对齐旋转 |
| `SheathRoot`（可选） | `Bn_scabbardA_01` 或 `Bn_Weapon` | 实际写入的约束 Transform |

**验收前必须确认**：Idle / 入鞘完成时，实际贴合网格的是 **A 链还是 B 链**（必要时两条链同权约束，或只驱动蒙皮主链）。

---

## 4. 方案选型

| 方案 | 说明 | 本项目建议 |
|---|---|---|
| A. 程序导轨约束鞘骨 | 收刀窗口内把鞘口/鞘轴吸附到**当前刀轴** | **主方案（先做）** |
| B. Generic 左手弱 IK | 左手跟随鞘握持点（鞘被拉开后补手） | 方案 A 左手脱节时再加 |
| C. 重做 End 动画 | 美术重导关键帧 | 仅作长期备选 |
| D. 物理碰撞 | Rigidbody / Collider | **不做** |
| ~~旧 A'. 约束刀骨~~ | ~~鞘轴真值，拉刀进鞘~~ | **已废弃**（易破坏刀/右手表演） |

**原则**：先定刀姿态（动画真值），再让鞘去贴合；不要先定鞘再指望刀不穿模。

---

## 5. 技术设计

### 5.1 组件

新增运行时组件（建议路径）：

`Assets/Scripts/Domain/Character/Weapon/UnagiSheathConstraint.cs`

职责：

- 引用 `bladeTip`、`bladeHilt`（或 `katanaHilt`）、`sheathMouth`、`sheathEnd`、写入目标 `sheathRoot`
- 维护 `weight ∈ [0,1]` 与插入曲线 `insertCurve`
- **不写** `Bn_katana_hilt`；只改鞘相关 Transform
- 在 `LateUpdate`（Animator 之后）混合：

```text
bladeAxis = normalize(BladeTip.position - BladeHilt.position)

# 插入进度：鞘口沿刀轴从「刀尖外侧」滑向「靠近刀柄」
t         = insertCurve.Evaluate(normalizedProgress)
mouthOnBlade = lerp(BladeTip + 口外偏移, 沿 bladeAxis 朝柄侧深度, t)

# 目标：鞘轴平行于刀轴，鞘口落在 mouthOnBlade
targetRot = 使 (SheathEnd - SheathMouth) 对齐 ±bladeAxis（入鞘方向按模型约定选号）
targetPos = 由 mouthOnBlade 反推 SheathRoot 世界坐标
            （用 SheathMouth 相对 SheathRoot 的局部偏移）

final = Lerp/Slerp(动画鞘姿态, target, weight)
写入 SheathRoot（Bn_scabbard*_01 或经确认的根）
```

实现注意：

1. **用挂点局部偏移反推 Root**，不要直接把 Root 设到刀尖，否则鞘口会对不齐。
2. **刀完全不动**；若动画里刀本身在动，鞘每帧跟随当前刀轴即可。
3. 若 A/B 双链都影响网格，对两条链做同一套轴对齐，或只驱动权重更大的一条并验证。

### 5.2 触发方式（本仓库优先级）

1. **战斗 Action 时间轴（推荐）**  
   新增 `SheathConstraintNotifyState`（或等价窗口），在对应 End / 收刀 Action 上标穿模帧区间；`IActionNotifyConsumer` 驱动 weight 淡入/淡出/打断清零。与现有 VFX/Hitbox Notify 同管线。
2. **StateMachineBehaviour（备用）**  
   仅当实机仍走 `雅.controller` 的 `*_End` 状态、且未走 Action `PlayClip` 时使用。
3. **动画事件 `YaShouDao`**  
   仅作补充起点；目前只见于 `Attack_03`，**不能**单独覆盖全部收刀路径。

### 5.3 权重与时间线（建议拆三段）

| 阶段 | 归一化时间（示例） | weight | 行为 |
|---|---|---|---|
| 接近 | 0.00 – 0.35 | 0 → 0.4 | 鞘口朝刀尖靠拢，轻修轴角 |
| 插入 | 0.35 – 0.85 | 0.4 → 1.0 | 强锁刀轴，鞘口沿刀轴推进 |
| 完成 | 0.85 – 1.00 | 1.0 → 0 | 保持贴合后淡出回动画/Idle |

具体数值以本地采样后的穿模窗口为准。行为语义是「**鞘去套刀**」，不是「刀去钻鞘」。

### 5.4 手部 IK（可选二期）

- 使用 **Animation Rigging**（Two Bone IK），目标为 Generic 骨骼 Transform，**不要**走 Humanoid 手 IK。
- **优先左手**：目标 = 鞘握持点（鞘被程序拉开后，左手最容易脱节）。
- 右手一般可保持动画（刀未动）；仅当握柄仍穿模时再补右手。
- IK weight 跟随鞘约束 weight。

### 5.5 与现有架构的关系

- **不改** `CharacterAnimationService` 主播放路径。
- 约束组件挂在模型实例上（`MonoBehaviour`），由 Action Notify（或备用 SMB/事件）设 weight。
- `CharacterConfig` / Combat Profile 一期可不改；窗口写在对应 `ActionDefinition` 时间轴上。

---

## 6. 实施阶段

### Phase 0 — 环境与资源确认（0.5d）

- [ ] 确认 LFS 资源可用，`星见雅.prefab` 中 `Bn_katana_hilt` / `Bn_scabbard*` 可见
- [ ] 确认实机招式是否走 Action `PlayClip`（决定主触发用 Notify 还是 SMB）
- [ ] 确认当前运行时是否已挂 Unagi 模型

### Phase 1 — 问题量化（0.5–1d）

- [ ] 逐条播放：`Attack_01_End` … `Attack_06_End`、Branch/Rush 相关 End
- [ ] 用 Gizmo 画**刀轴**、**鞘轴**；记录穿模起止帧与最大夹角/偏移
- [ ] 确认实际使用的鞘链（A / B）及蒙皮是否跟 `Bn_scabbard*`
- [ ] 输出「Clip → 穿模窗口」表，作为曲线与 Notify 区间依据
- [ ] 目视评估：强拉鞘后左手脱节是否可接受（决定是否需要 Phase 4）

### Phase 2 — 程序导轨约束鞘（1–2d）

- [ ] Prefab 添加 `BladeTip` / `SheathMouth` / `SheathEnd`（Editor 人工）
- [ ] 实现 `UnagiSheathConstraint`（只写鞘，不写刀）
- [ ] 实现 `SheathConstraintNotifyState` + Consumer（主路径）
- [ ] 先绑定 1 条最严重的 End Action，调通 weight / insertCurve
- [ ] 扩展到全部需要收刀的 End Action
- [ ] 备用：SMB / `YaShouDao` 仅在确认实机路径需要时再接

### Phase 3 — 观感打磨（0.5–1d）

- [ ] 淡入淡出，避免鞘突然“吸”到刀上
- [ ] 连招取消 / 受击打断时 weight 清零，鞘不卡在错误位姿
- [ ] 多机位检查（近景侧面最容易露穿模）

### Phase 4 — 可选左手 IK（1d，按需）

- [ ] 引入 / 启用 Animation Rigging
- [ ] 左手 Two Bone IK + 权重跟随鞘约束
- [ ] 对比仅鞘约束 vs 鞘+左手 IK，决定是否合入

### Phase 5 — 收尾

- [ ] 配置可序列化（曲线、挂点、入鞘方向符号）便于换武器长度
- [ ] 调试开关：Scene 绘制刀轴/鞘轴/weight
- [ ] 同步 TECHNICAL / 本计划验收结论

---

## 7. 验收标准

1. 所有常规 `Attack_*_End` 收刀段，侧面近景无刀刃穿出鞘壁。
2. 约束期间刀与右手表演无明显被拉扯；鞘无爆炸式跳动。
3. 连招取消 / 受击打断后，鞘不卡在错误旋转/位置。
4. Idle 与持刀战斗态切换后，刀/鞘父子关系与可见性正常（若有换挂逻辑）。
5. 不依赖 Humanoid；在 Generic Rig 下稳定运行。

---

## 8. 风险与对策

| 风险 | 对策 |
|---|---|
| A/B 鞘链选错 | Phase 1 用网格对齐确认；必要时双链同驱 |
| 鞘蒙皮不跟 `Bn_scabbard*` | 拖骨验证；确认写入骨是否影响 `Unagi_Weapon*` 鞘部分 |
| 强拉鞘导致左手脱节 | 短窗口 + 曲线；不够再上 Phase 4 左手 IK |
| 只见 `YaShouDao` | 以 Action Notify 覆盖全部收刀 Action |
| 入鞘方向（±刀轴）搞反 | Prefab 上暴露 `axisSign` / 预览 Gizmo |
| 约束 `Bn_Weapon` 误伤其它子级 | 优先写 `Bn_scabbard*_01`，避免整棵 `Bn_Weapon` |
| 强约束破坏表演 | 只用短窗口；接近段动画主导 |

---

## 9. 非目标（本期不做）

- 重导全部 92 条动画
- Humanoid 重定向
- 物理碰撞收刀
- 改 School_Katana_Girl 资源
- 大范围重构 Combat / AnimationProfile
- ~~以鞘为真值约束刀骨~~（已明确不做）

---

## 10. 关键资源索引

```
Assets/Art/Arts/雅/UnagiEnd.fbx
Assets/Art/Arts/雅/Avatar_Female_Size02_Unagi_UI.fbx
Assets/Art/Arts/雅/wuqi01.FBX
Assets/Art/Arts/雅/星见雅.prefab
Assets/Art/Arts/雅/雅.controller
Assets/Scripts/Domain/Character/Animation/CharacterAnimationService.cs
Assets/Scripts/Domain/Combat/Actions/Definitions/Timeline/  （Notify 扩展点）
```

相关 Clip 前缀：`Avatar_Female_Size02_Unagi_Ani_`  
收招重点：`Attack_01_End` … `Attack_06_End` 及 Branch / Rush / Counter 的 End  
收刀事件：`YaShouDao`（见于 `Attack_03`，仅补充）

---

## 11. 下一步

1. 本修订评审通过后，进入 **Phase 0–1**（资源确认 + 穿模采样表，重点看鞘蒙皮与左手脱节）。
2. Phase 1 产出采样表后，冻结曲线默认值并实现 `UnagiSheathConstraint`（刀真值 / 写鞘）。
3. Prefab 挂点在 Unity Editor 中人工添加；脚本在 `Assets/Scripts/**` 落地。
