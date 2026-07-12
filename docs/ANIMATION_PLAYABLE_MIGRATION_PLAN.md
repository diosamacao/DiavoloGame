# 动画系统迁移方案 — 自研薄层 Playable

> 状态：**已实施（代码）** — 2026-07-12  
> 资产待办：各 `CharacterAnimationProfile` 需在 Inspector 将原 State 对应 Clip 拖入 `Clip` 槽后才能进 Play Mode（`ValidateClips` 会拦截）。  
> 目标：废弃 Animator Controller 配置；Action + Locomotion **同一切换**到 Clip 驱动 Playable；门面可替换为 Animancer。

---

## 1. 背景与问题

### 1.1 现状

| 路径 | 实现 | 问题 |
|------|------|------|
| Locomotion | `AnimationKey` → Profile **状态名** → `Animator.CrossFade` | 依赖 Controller 状态图 |
| Action | `ActionDefinition.AnimationClip` → `PlayClip(clip.name)` | 仍按**状态名** CrossFade，Controller 必须有同名 State |
| HitStop | `CombatActorEntry.Animator.speed = 0` | 直连 Animator，换后端需改多处 |
| Root Motion | `OnAnimatorMove` + `applyRootMotion` | 可继续用，但需在 Playable 下回归验证 |
| 动作结束 | `ActionSession` / 时间轴帧（不依赖 `HasFinishedClip`） | `HasFinishedClip` 几乎闲置，可收口到播放器查询 |

文档约定「AC 只管 Locomotion、招式只引用 Clip」，运行时并未真正 Clip 直播。每加一招仍要维护 Controller。

### 1.2 目标状态

```text
LocomotionState / ActionExecutor
        │
        ▼
CharacterAnimationService          ← 调用层唯一门面（API 稳定）
        │
        ▼
IAnimationPlayback                 ← 可替换后端契约
        │
   ┌────┴────┐
   ▼         ▼
Playable     （未来）Animancer
Backend      Backend
        │
        ▼
Animator（仅作 PlayableGraph 输出目标 + Avatar + Root Motion 消息）
无 RuntimeAnimatorController 业务依赖
```

### 1.3 非目标（本方案不做）

- 不引入 Animancer 包（仅预留接口）
- 不重做 `ActionTimeline` / Notify（动画层只负责播 Clip）
- 不做多层 Avatar Mask、Additive、复杂 BlendTree 编辑器
- 不做 Animation Event 替代 Notify
- 不保留「Controller CrossFade」与「Playable」双轨运行路径

---

## 2. 设计原则

1. **调用层零感**：`LocomotionState`、`ActionExecutor`、`ActionState`、`CombatModeService` 尽量不改签名；只认 `CharacterAnimationService`。
2. **后端可替换**：所有 Graph / Mixer / ClipPlayable 细节锁在 `IAnimationPlayback` 实现内；未来 `AnimancerAnimationPlayback` 只换工厂装配。
3. **Action + Locomotion 同一切换**：一次合并删除状态名 CrossFade；禁止半迁移（招式 Playable、Locomotion 仍 Controller）。
4. **数据 Clip 化**：Profile 与 Action 统一为 `AnimationClip` 引用，不再映射 Animator 状态名。
5. **表现控制收敛**：Speed / Pause（卡肉）走动画门面，禁止业务层直接改 `Animator.speed`。
6. **无兼容层**：按项目规则删除旧状态名 API 与 Controller 依赖；资产在 Editor 中一次改完。

---

## 3. 接口设计（Animancer 可替换）

### 3.1 后端契约 `IAnimationPlayback`

新建：`Assets/Scripts/Domain/Character/Animation/IAnimationPlayback.cs`

```csharp
/// <summary>动画播放后端；Playable / Animancer 等实现此契约，门面不感知具体 Graph。</summary>
public interface IAnimationPlayback : System.IDisposable
{
    /// <summary>当前是否有有效输出目标。</summary>
    bool IsValid { get; }

    /// <summary>播放倍率；0 = 冻结（卡肉），1 = 正常。</summary>
    float Speed { get; set; }

    /// <summary>以固定秒数淡入播放 Clip；打断当前混合。</summary>
    void Play(AnimationClip clip, float fadeDuration);

    /// <summary>当前主 Clip（淡入目标）；无则 null。</summary>
    AnimationClip CurrentClip { get; }

    /// <summary>主 Clip 归一化时间；循环 Clip 可 > 1。</summary>
    float NormalizedTime { get; }

    /// <summary>主 Clip 是否已播完至少一遍（非 Transition 中且 normalizedTime >= 1）。</summary>
    bool HasFinished { get; }

    /// <summary>每帧推进（若实现依赖手动 Evaluate）；Graph 自动推进时可为空实现。</summary>
    void Tick(float deltaTime);
}
```

**刻意不放进契约的内容**（避免绑死某一插件）：

- Animator 层索引、StateHash、Controller
- AnimancerState / Playable 句柄
- 事件回调（时序仍由 Action 时间轴拥有）

### 3.2 门面 `CharacterAnimationService`（保留对外 API）

对外保持 / 微调：

| API | 行为 | 调用方 |
|-----|------|--------|
| `Play(AnimationKey, float? fade)` | Profile 取 Clip → `_playback.Play`；`_locked` 时忽略 | `LocomotionState` |
| `PlayClip(AnimationClip, float fade)` | 直接 `_playback.Play`；清 `_currentKey` | `ActionExecutor` |
| `SetLocked(bool)` | 阻止 Locomotion `Play` | `ActionState` |
| `SetProfile` / `ResetPlaybackState` | 切模式后强制重选 key | `CombatModeService` / `ActionState` |
| `HasFinishedClip(AnimationClip)` | 委托 `CurrentClip` + `HasFinished` | 预留/校验 |
| **新增** `SetSpeed(float)` / `Speed` | 委托 `_playback.Speed` | `HitStopController` |
| **新增** `Dispose()` | 销毁 Graph | Actor 销毁路径 |

**删除**：

- 对 `Animator.CrossFadeInFixedTime` / `GetCurrentAnimatorStateInfo` 的直接依赖
- 构造参数中的 `animatorLayerIndex`（Playable 单输出层；Animancer 后端若需层再在实现内处理）

**可选保留**：`Animator` 属性只读暴露（Root Motion / 调试）；业务卡肉不再用它改 speed。

构造改为：

```csharp
public CharacterAnimationService(
    IAnimationPlayback playback,
    CharacterAnimationProfile profile)
```

工厂负责 `new PlayableAnimationPlayback(animator)` 再注入。

### 3.3 未来 Animancer 替换步骤（本阶段只预留）

1. 新增 `AnimancerAnimationPlayback : IAnimationPlayback`
2. `CharacterActorFactory` 一行切换创建后端
3. 门面与 `LocomotionState` / `ActionExecutor` **零改**

---

## 4. Playable 后端（薄层范围）

### 4.1 类与文件

| 文件 | 职责 |
|------|------|
| `PlayableAnimationPlayback.cs` | 实现 `IAnimationPlayback`；持有 Graph / Mixer / ClipPlayable |
| （可选）`AnimationPlaybackFactory.cs` | 集中 `Create(Animator)`，方便日后切 Animancer |

路径：`Assets/Scripts/Domain/Character/Animation/`

### 4.2 Graph 结构（最小）

```text
PlayableGraph (DirectorUpdateMode.GameTime)
  └── AnimationMixerPlayable (inputCount = 2)
        ├── [0] AnimationClipPlayable 上一层（淡出）
        └── [1] AnimationClipPlayable 当前层（淡入）
  └── AnimationPlayableOutput → Animator
```

行为约定：

- `Play(clip, fade)`：新 Clip 接到 input 1；旧接到 0；在 `fade` 秒内权重 1→0 / 0→1；fade≤0 则立刻切。
- 同 Clip 重复 `Play`：Locomotion 门面已用 `_currentKey` 去重；招式连段由 Executor 决定是否重播（同 Clip 也应允许重启，后端按「强制重播」处理）。
- `Speed`：写 Graph 的 `SetSpeed` 或 Mixer/Clip 的 speed（与 `Animator.speed` 二选一，**统一走 Graph**，避免双控）。
- `Dispose`：`graph.Destroy()`；角色销毁 / 场景卸载必须调用。
- Animator：`runtimeAnimatorController = null`（或空 Controller 资产）；**禁止**再依赖 Controller 状态。

### 4.3 不做的薄层之外功能

| 延后 | 说明 |
|------|------|
| 1D 连续 Blend（Walk↔Run） | 初期仍用三 Clip CrossFade；手感不够再加 Mixer 权重 |
| 多层 / Mask | 上半身独立层等，Animancer 或二期再做 |
| 手动 `Evaluate` 编辑器 scrub | Action Editor 继续 `AnimationMode.SampleAnimationClip` |

### 4.4 Root Motion

- 继续 `CharacterRootMotionDriver` + `OnAnimatorMove`。
- Playable 输出到 Animator 时，开启 `applyRootMotion` 应仍产生 `deltaPosition`（实施阶段必测）。
- 若实测无效：在 `PlayableAnimationPlayback.Tick` 中读 `AnimationClipPlayable` 的 root motion 曲线并暴露 `deltaPosition`，由 Driver 消费——作为 **Plan B**，仅当 Plan A 失败才做，不预留死代码双轨。

### 4.5 HitStop

| 现状 | 目标 |
|------|------|
| `HitStopController` → `entry.Animator.speed` | → `entry` 上的动画门面 / `IAnimationPlayback.Speed` |

建议改动：

1. `CombatActorEntry` 增加 `CharacterAnimationService Animation`（或 `IAnimationPlayback`），卡肉用 `Animation.SetSpeed(0)`。
2. `Animator` 字段可暂留调试，卡肉路径不再写 `Animator.speed`。
3. `CharacterActorFactory.Register` 传入 animation 引用。

同步：`ActionExecutor.SetHitStopPaused` 逻辑帧暂停保持不变（逻辑与表现分离）。

---

## 5. 数据迁移

### 5.1 `CharacterAnimationProfile`

**现字段**

```csharp
AnimationKey Key;
string StateName;
```

**改为**

```csharp
AnimationKey Key;
AnimationClip Clip;
```

API：

- 删除 `GetStateName`
- 新增 `TryGetClip(AnimationKey key, out AnimationClip clip)` / `GetClip`（缺省打 Error 并返回 null）

`defaultCrossFadeDuration` 保留。

### 5.2 Editor 资产（人工，Agent 不改 `.asset`）

需在 Unity Inspector 中完成：

1. 打开各 `CharacterAnimationProfile`（含战斗模式切换用的多份 Profile）。
2. 将原 StateName 对应 Clip 拖入 `Clip` 槽（从原 Animator Controller / FBX 取）。
3. 确认 `CombatModeProfile` 引用的每份 Locomotion Profile 均已填 Clip。
4. 模型 Prefab：可清空 Animator 的 Controller 引用（或留空 Controller）；Play Mode 验证无报错。

### 5.3 `CharacterConfig`

- 删除或废弃 `animatorLayerIndex`（无后端消费则删字段，避免假配置）。
- `ValidateForPlayer` 可增加：Profile 内 Idle/Walk/Run Clip 非空校验（可选，建议做）。

### 5.4 Action

- `ActionDefinition.animationClip` **已是 Clip**，无需改数据结构。
- 去掉「Controller 里必须有同名 State」的隐性约束；文档同步更新。

---

## 6. 调用链变更（同一切换）

### 6.1 装配

```text
CharacterActorFactory.Create
  → Animator（必需，输出目标）
  → IAnimationPlayback = new PlayableAnimationPlayback(animator)
  → CharacterAnimationService(playback, profile)
  → …其余不变
  → CombatActorSystem.Register(..., animation)  // 供 HitStop
  → 角色销毁时 animation.Dispose() / playback.Dispose()
```

### 6.2 运行时（目标）

```text
LocomotionState.Tick
  → Animation.Play(Idle|Walk|Run)     // Profile → Clip → Playable

ActionExecutor.BeginAction
  → Animation.PlayClip(def.Clip, fade)

ActionState Enter/Exit
  → SetLocked / ResetPlaybackState    // 不变

HitStopController
  → Animation.SetSpeed(0 / restore)   // 不再写 Animator.speed

CombatModeService 切模式
  → SetProfile + ResetPlaybackState   // 不变
```

### 6.3 删除的旧路径

- `CrossFadeInFixedTime(stateHash / clip.name)`
- Profile `StateName` 字符串映射
- 「招式依赖 Controller 状态」文档与约定
- HitStop 直写 `Animator.speed`（改为门面）

---

## 7. 实施阶段（可合并为一个 PR，逻辑顺序如下）

> 用户要求 Action + Locomotion **同时**迁移：下列阶段是开发顺序，**合并前必须全部完成**，中间不提交「半 Controller 半 Playable」的可玩构建。

### Phase A — 契约与后端骨架

- [x] 新增 `IAnimationPlayback`
- [x] 实现 `PlayableAnimationPlayback`（Play / Fade / Speed / NormalizedTime / HasFinished / Dispose）
- [ ] 单元级或 Play Mode 沙盒：空场景单 Animator 上切两个 Clip（需资产绑定后人工验证）

### Phase B — Profile 与门面

- [x] `CharacterAnimationProfile` 改为 Clip 映射；删 `GetStateName`
- [x] `CharacterAnimationService` 改为持有 `IAnimationPlayback`；实现 `SetSpeed`
- [x] 删除 `animatorLayerIndex` 消费链

### Phase C — 装配、HitStop、生命周期

- [x] `CharacterActorFactory` 注入 Playable 后端
- [x] `CombatActorSystem` / `HitStopController` 改走 `SetSpeed`
- [x] Actor/Owner 销毁时 `Dispose` Graph（`PlayerController.OnDestroy` → `CharacterActor.Dispose`）
- [ ] Root Motion 回归（有 `useRootMotion` 的招式）— 待 Play Mode 验证

### Phase D — 资产与 Controller 脱钩（Editor 人工）

- [ ] 填写所有 Locomotion Profile Clip
- [ ] 模型 Animator Controller 置空（运行时也会清空实例 Controller）
- [ ] 全招式 Play Mode 走查（起手 / 连段 / 取消 / 模式切换）

### Phase E — 文档与规范

- [x] 更新 `ARCHITECTURE.md` / `TECHNICAL.md` / `CONVENTIONS.md` / `docs/ACTION_SYSTEM.md`
- [x] `ROADMAP.md` 记「Playable 薄层；Animancer 可替换」
- [x] 本计划文首状态改为已实施

---

## 8. 影响文件清单

### 8.1 代码（预期修改 / 新增）

| 路径 | 变更 |
|------|------|
| `Domain/Character/Animation/IAnimationPlayback.cs` | **新增** |
| `Domain/Character/Animation/PlayableAnimationPlayback.cs` | **新增** |
| `Domain/Character/Animation/CharacterAnimationService.cs` | 门面改后端；`SetSpeed` |
| `Domain/Character/Animation/CharacterAnimationProfile.cs` | StateName → Clip |
| `Domain/Character/CharacterConfig.cs` | 去掉 layerIndex（若无用） |
| `Domain/Character/CharacterActorFactory.cs` | 创建 Playback；Dispose；Register animation |
| `App/Systems/Combat/CombatActorSystem.cs` | Entry 带 Animation |
| `App/Controllers/Combat/HitStopController.cs` | `SetSpeed` |
| `Domain/Character/Animation/CharacterRootMotionDriver.cs` | 仅当 Plan B 需要时改 |
| `CharacterActor` / 销毁路径 | 确保 Dispose（若尚无统一销毁） |

### 8.2 调用层（预期少改或零改）

- `LocomotionState` — API 不变
- `ActionExecutor` — API 不变
- `ActionState` — API 不变
- `CombatModeService` — API 不变

### 8.3 资产（人工）

- `Assets/Data/**/*AnimationProfile*.asset`
- 角色 Model Prefab 上 Animator Controller 引用
- **不**要求改 `ActionDefinition` 字段结构

### 8.4 文档

- `.cursor/skills/actgame-architecture/*` 动画相关节
- `docs/ACTION_SYSTEM.md`、`docs/ACTION_EDITOR.md` 中「AC 仅 Locomotion」表述改为「无 Controller，全 Clip + Playable」

---

## 9. 风险与缓解

| 风险 | 缓解 |
|------|------|
| Playable 下 Root Motion 异常 | Phase C 专项测；失败再上 Plan B |
| 清空 Controller 后 Avatar/T-Pose | 确认 Animator 有 Avatar；Playable 输出连接正确 |
| Profile 漏绑 Clip | Validate + Play 时 LogError；走查清单按角色/模式列 Profile |
| Graph 未 Destroy 泄漏 | Factory/Actor 销毁路径强制 Dispose；进 Play 多次进退场景检查 |
| Fade 手感与旧 CrossFade 不一致 | 沿用 Profile / ActionDefinition 的 fade 秒数；必要时微调默认 0.15 |
| HitStop 只冻逻辑不冻骨骼 | 必须走 Playback.Speed，禁止只 `SetHitStopPaused` |
| 同 Clip 连段不重播 | 后端 `Play` 对同引用强制重建/重设 time=0 |

---

## 10. 验收清单（合并前全部勾选）

- [ ] Idle / Walk / Run 切换正常，无 Controller
- [ ] 任意招式起手、连段、Cancel 动画正确
- [ ] 战斗模式切换后 Locomotion Profile Clip 生效
- [ ] 卡肉期间骨骼冻结，结束后恢复
- [ ] Root Motion 招式位移正确；非 Root Motion 招式不滑步异常
- [ ] 退出 Play / 销毁角色无 PlayableGraph 泄漏日志
- [ ] 代码中无 `CrossFadeInFixedTime` / Profile `StateName` / HitStop 写 `Animator.speed`
- [ ] `IAnimationPlayback` 已隔离；门面可说明「换 Animancer 只换 Backend」

---

## 11. 未决问题（实施前确认）— 已拍板

1. **角色销毁点**：`PlayerController.OnDestroy` → `CharacterActor.Dispose()` ✅
2. **空 Controller**：运行时 `PlayableAnimationPlayback` 清空实例 Controller；Prefab 可另清 ✅
3. **循环 Locomotion**：`HasFinished` 对 `isLooping` 返回 false；Locomotion 不依赖结束判定 ✅
4. **Factory**：暂直接 `new PlayableAnimationPlayback`，未加独立工厂类 ✅

---

## 12. 决策记录

| 日期 | 决策 | 理由 |
|------|------|------|
| 2026-07-12 | 自研薄 Playable，不引入 Animancer | 需求面窄；Action 时序已在自研时间轴；预留 `IAnimationPlayback` |
| 2026-07-12 | Action + Locomotion 同一切换 | 避免双轨与假完成；Profile 一并 Clip 化 |
| 2026-07-12 | HitStop 改走门面 Speed | Animator / Playable / Animancer 行为一致 |
| 2026-07-12 | 不保留 Controller CrossFade 兼容 | 符合 no-legacy-compatibility |

---

## 13. 下一步

用户确认本方案（尤其 §11 未决）后，按 Phase A→E 实施；实施期间本文件与架构文档同步更新。
