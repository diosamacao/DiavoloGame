# 受击系统实现方案

> 制定：2026-09-03  
> 角色：**受击档位 + Playable Additive 的施工计划**（不是实现真源；落地后改 TECHNICAL）  
> 目标工程：`DiavoloGame`（Unity 2022.3 / URP，`NetSync` 分支）  
> 相关：  
> - 架构拆解：[`../2026.8.12/PROJECT_ARCHITECTURE_BREAKDOWN.md`](../2026.8.12/PROJECT_ARCHITECTURE_BREAKDOWN.md) §8～9  
> - 功能真源：`.cursor/skills/actgame-architecture/TECHNICAL.md`（命中管道、Reaction、Playable）  
> - 计划体例对照：[`../2026.8.24/UI_BACKPACK_PLAN.md`](../2026.8.24/UI_BACKPACK_PLAN.md) §6、[`../2026.8.17/NETSYNC_DEDICATED_SERVER_SEPARATION_PLAN.md`](../2026.8.17/NETSYNC_DEDICATED_SERVER_SEPARATION_PLAN.md) §17  
> 前置：现行有效命中一律 `EnterHit` 断招；轻击要不停招、用 Additive 叠 `Hit_Shake`。  
> **第一步只验表现：** Playable Additive 叠在正在播的 Action/Locomotion 上是否不断招、看起来是否正常。P-HR0 Play 已于 2026-09-03 确认。  
> **计划状态：已关闭（2026-09-04）** — P-HR0～P-HR4 用户 Play 全部验收。失衡条 / 击飞物理仍本轮不做。

---

## 0. 一句话

受击拆成 **裁定** 和 **执行**。裁定只产出一档反馈；只有「硬直及以上」才进顶层 `Hit` 并停 `ActionSim`。微颤只发表现事件，由 Playable 层 1 Additive 叠在当前招上。

**开工顺序反过来：** 先用调试入口证明 Additive 可行，再改命中裁定。

```text
Pipeline 结算伤害 / 无敌 / 吞伤
  → HitReactionResolver（打断等级 vs 抗打断 + 期望反馈）
  → HitReactionCommand
       None
       Flinch     → 不停招，Additive Hit_Shake
       LightStun / HeavyStun / Launch → EnterHit，停招
       Death      → EnterDeath
```

不要再做一台和 `Action=60` 抢权的顶层受击状态机。

---

## 1. 目标与非目标

### 目标

- 轻击：敌人继续当前 Action，表现叠 Idle 派生的 `Hit_Shake`。
- 重击 / 击飞：仍走现有 `Hit` / 受击 Action。
- 玩家与敌人走同一套裁定，表现入口不同（本机桥 / Proxy）。
- 权威、预测、`GraphNodeKey`、伤害公式不换。
- 敌人只有**断招**时才 `Brain.NotifyHit` → 树 Reset。

### 非目标（本轮不做）

- 绝区零完整失衡条 / 连携 / 部位破坏。
- 击退位移表、击飞抛物线物理（Launch 先用现有受击 Action + 时长）。
- Animator Controller Additive Layer。
- 把 Shake 写进 Snapshot / Hash。

---

## 2. 现行必须改掉的行为

| 现在 | 问题 |
|------|------|
| 有效命中 → `CharacterReactionService` → `EnterHit` | 轻击也断招 |
| `HitState` 强制重入 | 只服务整段替换受击 |
| 无 Action 则硬直 0.35s | 仍占用 `Hit` 态 |
| `NotifyHit` 一律 Reset BT | 轻击打断 AI 攻击循环 |
| `PlayableAnimationPlayback` 单 Clip Seek | 没有 Additive 层 |

伤害、无敌早退、完美吞伤、DOT 无 Reaction、卡肉 `freezeFrames`：**保留**。

---

## 3. 数据

### 3.1 反馈档（裁定结果）

```text
enum HitReactionKind
{
    None        = 0,  // 无动作反馈（仍可扣血）
    Flinch      = 1,  // 微颤：不停招
    LightStun   = 2,  // 轻击退：进 Hit
    HeavyStun   = 3,  // 重击退：进 Hit
    Launch      = 4,  // 击飞：进 Hit
    Death       = 5,
}
```

`Flinch` 以下不断招。`LightStun` 及以上断招。

### 3.2 攻击侧（写在 Hitbox / HitPayload）

| 字段 | 默认 | 含义 |
|------|------|------|
| `interruptLevel` | 1 | 冲击力。与目标韧性比较后出档，不再读 `desiredReaction` |
| `HitReactionId` | 现有 | 断招时选哪份受击 Action |
| `flinchClipKey` | 可选空 | 空则用角色默认 `Hit_Shake` |

旧资产未填冲击力按 1。档位只由冲击力 − 韧性决定。

### 3.3 受击侧

| 来源 | 字段 | 含义 |
|------|------|------|
| `CharacterCombatConfig` / 敌人 Numeric | `baseInterruptResist` | 站立韧性。杂兵 1，精英 3，Boss 5（可调） |
| `ActionPhaseNotifyState` | `interruptResistBonus` | 出招窗内韧性加成。现有 `SuperArmor` = 本窗最多 Flinch |
| `Invincible` | — | 管道早退，不进裁定 |

本轮不单独做 Buff 抗打断表；有 Numeric Flag 再加成即可。

### 3.4 裁定输出

```text
struct HitReactionCommand
{
    HitReactionKind Kind;
    ActionDefinition StunAction; // 仅 Stun+
    int StunFrames;              // 无 Action 时的 Hit 时长
    AnimationKey FlinchKey;      // 仅 Flinch
}
```

Resolver **只出 Command**，不切状态、不播动画。

---

## 4. 裁定算法（纯函数，权威 / 预测共用）

在 `CharacterReactionResolver` 里收口，禁止 Pipeline 或 Actor 再写一套 if。

```text
1. 已死或本帧致命 → Death
2. 无敌 / 完美吞伤 → None（管道本就不该叫到这里）
3. DOT / 无 HitPayload 的数值伤 → None
4. toughness = baseInterruptResist + 当前 Phase.bonus
   SuperArmor 窗：Flinch（非死亡）
5. excess = interruptLevel − toughness
   excess < 0  → Flinch
   excess < 2  → LightStun（HardHit）
   excess < 4  → HeavyStun
   excess >= 4 → Launch
6. 填 Command：Stun+ 查 CharacterReactionSet + HitReactionId
```

同帧多刀：管道已按 `SimHitKey` 排序。对同一目标：

```text
先算每刀 Command
合并：取 Kind 最大的一档
Flinch 可合并为一次（同帧只触发一条 Shake）
Stun+ 只 EnterHit 一次，用最高档的 Action
```

不要同帧既 Flinch 又 EnterHit。

---

## 5. 执行

### 5.1 Actor（逻辑）

`CharacterActor` / `CharacterReactionService` 按 Command 分支：

| Kind | 逻辑 | AI |
|------|------|-----|
| None | 只已结算的伤害 | 不通知树 |
| Flinch | **不** `TryChangeState(Hit)`，**不** `ActionSim.Stop` | **不** `NotifyHit` |
| LightStun+ | 现有 `EnterHit`：停招、锁动画、走 HitState | `NotifyHit` → Reset |
| Death | `EnterDeath` | `NotifyDeath` |

`HitState` 只服务 Stun+。时长：有受击 Action 跟 Action 帧；没有则用 `StunFrames`。连击重入规则只对 Stun+ 保留。

Flinch 期间再被 Stun：照常 `EnterHit`，Additive 由表现桥在进 Hit 时清掉。

### 5.2 表现事件

新增只读事件（或扩 `AttackHitEvent`）：

```text
HitFlinchEvent
  TargetSimActorId
  FlinchKey
  SourceHitKey   // 去重
```

权威发、客机 Proxy 订。**禁止** Event Handler 回写 `ActionSim`（和现有 Cue / HitStop 合同一样）。

本机敌人无预测：Observer Proxy 收事件后播 Additive。  
本机玩家被轻击（少见）：自己的表现桥播 Additive，状态仍是 Action/Locomotion。

### 5.3 Playable Additive（P-HR0 先落地，调试触发）

**P-HR0 只接这一层 + 调试入口，不接命中。** 改 `PlayableAnimationPlayback`（或并排 Flinch 层，由表现桥调）：

```text
AnimationLayerMixerPlayable
  输入0  Override  当前 Action / Locomotion Clip（现有 Seek + SetSpeed）
  输入1  Additive  Hit_Shake Clip
         SetLayerAdditive(1, true)
         可选 AvatarMask（脊柱 / 胸 / 头）
```

| 项 | 约定 |
|----|------|
| 触发 | `PlayFlinch(clip)`：层 1 权重 1，从 frame 0 重 Seek |
| 结束 | Clip 播完或固定 N 帧后权重淡到 0 |
| 连击 | 每次 Flinch 重 Seek，不叠多条 Clip |
| 卡肉 | 层 1 与层 0 共用 `SetSpeed`（权威 HitStop 时一起停） |
| 切招 / EnterHit / 隐藏 | 权重立刻 0 |
| Root Motion | Shake **不**进 MotorSim / 运动表 |
| Hash / Snapshot | 不写 Shake 时间 |

`Hit_Shake` 按 Additive 导入：参考姿势 = 第 0 帧或 T-Pose。上半身 Mask。不要把整段 Idle 当 Override 去盖攻击。

接口不要泄漏 Mixer：`IAnimationPlayback` 加 `PlayAdditive(AnimationClip, AvatarMask, fade)` / `StopAdditive()`。现有 `Play` / `Seek` / `SetSpeed` 不变。

---

## 6. 和现有层怎么接

```text
CombatHitPipeline.Resolve
  → 伤害 / Vitality
  → ReactionService.Issue(Resolver.Resolve(...))
       Flinch → 发布 HitFlinchEvent
       Stun+  → actor.EnterHit(...)
       Death  → actor.EnterDeath(...)

表现
  HitImpactController     仍播受击 Cue（接触点 VFX）
  新 FlinchPlayback       订 HitFlinchEvent
  HitState                只在 Stun+ 时 Seek 受击 Action
```

Cue（火花）和 Flinch（骨架抖）分开：吞伤仍可跳过二者；Flinch 无火花也可以。

Phase `interruptible` 继续只管 **玩家出招被更高优 Intent 硬切**，不要和受击抗打断共用一个字段。受击用 `interruptResistBonus` / `SuperArmor`。

---

## 7. 联网

| 路径 | 做法 |
|------|------|
| 伤害 / 是否进 Hit | 权威 Vitality + 快照 ActionId / 状态 |
| Flinch | 走现有可靠命中事件通道，带 `ReactionKind` 或独立 Flinch 标记 |
| Owner 被 Stun | 现有 `EnterHit` 硬吸，不改 |
| Owner 被 Flinch | **不要**走受击硬吸；ActionId 不变，只播 Additive |
| Observer | 快照继续 Seek 当前招；另收 Flinch 事件叠层 |
| 预测和解 | Flinch 不产生 Action Ack，不 Restore |

旧客户端若只认「有 hit 就 EnterHit」，升级协议时缺省 Kind=LightStun，避免旧包把轻击当断招。Demo 可一次切完，不必双版本。

---

## 8. 资产怎么填（第一版）

**敌人 `CharacterCombatConfig`**

- 木桩 / 杂兵：`baseInterruptResist = 1`
- 精英：`3`
- 出招 Active 窗：`interruptResistBonus = +2` 或勾 SuperArmor

**玩家普攻 Hitbox**

- 轻段：`interruptLevel = 1` 或 `2`（打韧性 1 杂兵进 LightStun，打韧性 3 精英只 Flinch）
- 重段 / 技能：`interruptLevel >= 3`，填 `HitReactionId`

**动画**

- `Hit_Shake`：短、无根移、Additive 导入
- 受击断招片：继续挂 ReactionSet（现有路径）

未改的旧盒子冲击力 1：打韧性 1 的杂兵会断招；打韧性 3 的精英只 Flinch。`desiredReaction` 已删除，不再参与裁定。

---

## 9. 阶段总表

| 阶段 | 做什么 | 完成标准（出口） | 未通过则 |
|------|--------|------------------|----------|
| **P-HR0 Additive 探针** | Playable 加 Additive 层；调试键/菜单对**正在出招的敌人**播 `Hit_Shake`；**不改**命中与 `EnterHit` | Play：底轨招式连续、能看到抖、逻辑态不变 | **停。** 不进 P-HR1。改 Clip / Mask / 参考姿势后再验 |
| **P-HR1 裁定纯函数** | `HitReactionKind` / Command / Resolver + EditMode | 单测档位与 SuperArmor 稳定 | 不接 Service |
| **P-HR2 执行分支** | Flinch 不 `EnterHit`、不 Reset BT；Stun+ 旧路径；接 P-HR0 的 `PlayAdditive` | 真命中轻击：不停招 + 抖；重击仍进 Hit | 不改 Payload 资产、不动网络 |
| **P-HR3 等级表** | `interruptLevel` / 抗打断 / Phase bonus；旧资产默认值 | 精英出招中普攻只抖，技能能断 | — |
| **P-HR4 复制** | 命中事件带 Kind；Proxy Additive；F3 档位 | Listen：一端断招一端只抖，ActionId 一致 | 已验收（2026-09-04）；不宣称公网 / W10 |

对照 UI 计划的 U0：P-HR0 是「空白 DebugPanel」——只证明通道，不做完整受击业务。

---

## 10. 分阶段待办与验收

勾选在实现仓进行。本文件是计划，不是施工日志。

### P-HR0 — Additive 不断招探针（第一步，先做这个）

**目的：** 只回答「Idle 派生的 `Hit_Shake` 用 `AnimationLayerMixerPlayable.SetLayerAdditive` 叠在当前 Action/Locomotion 上，会不会掐底轨、看起来像不像微颤」。命中管道、Resolver、HP、BT **一律不改**。挨打仍会 `EnterHit`（旧行为）；探针用**调试触发**，不要靠打敌人来验 Additive。

**待办**

- [x] 读清 `IAnimationPlayback` / `PlayableAnimationPlayback` 现图（几个 Mixer、谁 Seek、HitStop 怎么 `SetSpeed`）。
- [x] 层 0 保持现有 Override Seek；增加 `AnimationLayerMixerPlayable`，层 1 `SetLayerAdditive(1, true)`。
- [x] `IAnimationPlayback` 增加 `PlayAdditive(clip, mask, fade)` / `StopAdditive()`，不把 Mixer 类型漏出 Domain 逻辑。
- [x] `Hit_Shake` 导入为 Additive（参考第 0 帧或 T-Pose）；去掉 Root Motion；准备上半身 `AvatarMask`（脊柱 / 胸 / 头）。**Editor 人工**（Play 已确认能看见抖）
- [x] 调试入口：Play 下 **F6**，或菜单 `ACTGame/Combat/Debug Play Flinch Additive`（选中敌人优先）。不改 Hitbox。
- [x] 触发时目标必须已在播 Action 或 Locomotion；禁止为了探针 `EnterHit` / `ActionSim.Stop`。
- [x] 切招、进 Hit、角色隐藏时 `StopAdditive`（避免探针残留到下一招）。
- [x] F3 / Console：`State`、`Action`、`ActionFrame`、Additive 权重；触发前后各打一行 `[P-HR0]`。

**验收（Play，本机 Listen / 单机即可）**

- [x] 敌人正在播攻击（或木桩 Idle 走循环）：按调试键后**底轨不重头、不冻结、不切受击 Action**。
- [x] `CharacterStateType` 仍是 `Action` 或 `Locomotion`，不是 `Hit`。
- [x] `ActionSim.CurrentFrame`（或等价）在 Shake 期间继续 +1（卡肉除外）。
- [x] 能看出上半身/脊柱轻抖，不是整段 Idle 盖住出招、不是 T-Pose 抽一下。
- [x] 拳头 / 武器轨迹与未按调试键时同一套攻击盒时间轴（逻辑位移不变）。
- [x] 连按调试键：Shake 从 0 重播，不叠多条、不把骨架拉飞。
- [x] 调试后让敌人吃**真受击**（旧 `EnterHit`）：Shake 立刻停，受击片仍按旧路径播。
- [x] Motor / 烘焙位移无额外平移；脚不因 Shake 根曲线搓地。
- [x] 关闭调试入口后，不打人则不再自己抖。

**失败怎么处理（未通过不得进 P-HR1）**

| 现象 | 先做 |
|------|------|
| 底轨被替换成 Idle / 抽回站立 | Clip 不是增量；重设 Additive Reference，或不要用 Override 混 |
| 手臂「回到 Idle 握姿」 | 加/收紧 AvatarMask，去掉手臂 |
| 看起来完全没抖 | 权重要 1；确认 `SetLayerAdditive` 而不是 `AnimationMixerPlayable` |
| 一播就位移 | 关 Shake Root Motion；确认没写 Motor |
| 一播就进 Hit | 误接了 Reaction；撤回，探针不得走 `EnterHit` |

P-HR0 出口：**人眼 + F3 状态**，不要求 EditMode 反应单测，不要求联网。→ **已达成（2026-09-03）**

---

### P-HR1 — 裁定纯函数

**待办**

- [x] 增加 `HitReactionKind`、`HitReactionCommand`。
- [x] `CharacterReactionResolver.Resolve` 按本文 §4 出 Command，不切状态。
- [x] EditMode：§11 裁定用例。

**验收**

- [x] 冲击 < 韧性 → `Flinch`（不再读 `desiredReaction`）。
- [x] 冲击 ≥ 韧性 → `LightStun`（HardHit）。
- [x] SuperArmor → 非 Death 最多 `Flinch`。
- [x] 同帧 Flinch+LightStun 合并为 LightStun。
- [x] DOT → `None`。
- [x] 旧盒子冲击 1 打韧性 1 → `LightStun`。
- [x] 后续阶段已接 Service；本阶段单测出口已关闭。

**出口：** 单测覆盖 §4 档位。→ **已达成（2026-09-03 代码 / 2026-09-04 全计划 Play 收口）**。

---

### P-HR2 — 执行分支（真命中接上 Additive）

**待办**

- [x] `ReactionService`：`Flinch` 不 `EnterHit`、不 `ActionSim.Stop`、不 `Brain.NotifyHit`。
- [x] `Stun+` / `Death` 走现有路径。
- [x] 发布 `HitFlinchEvent`，`HitFlinchPlaybackController` 调 `PlayAdditive`（不 Play 主轨 / 不锁走跑）。
- [x] 禁止 Event Handler 回写 `ActionSim`。

**验收**

- [x] 轻击盒子打木桩：HP 下降，状态仍是 Action/Locomotion，攻击播完。
- [x] 同时看到与 P-HR0 同质量的 Shake；走跑 Clip 不重头、不冻结。
- [x] 行为树不因轻击 Reset（攻击循环不被拆掉）。
- [x] 重击盒子：仍 `EnterHit`，树 Reset，Shake 被清。
- [x] 轻击过程中吃重击：立刻进 Hit。
- [x] 完美吞伤 / 无敌：无 Flinch、无 Stun。

**出口：** 轻击不停招 + Shake，重击仍 EnterHit。→ **已验收（2026-09-04）**。

---

### P-HR3 — 打断等级与资产

**待办**

- [x] `HitPayload.interruptLevel` / `desiredReaction`；旧资产默认值。（C# 默认 LightStun + level 1；OnValidate / 菜单只补空字段）
- [x] `baseInterruptResist`；Phase `interruptResistBonus` 或 SuperArmor。（Service 读 Config + 当前帧加成）
- [x] 档位由冲击力对韧性算出；`desiredReaction` 已从裁定删除。轻/重段只调 `interruptLevel`。**Editor 填冲击力与韧性，代码不改 Data/**
- [x] 迁移菜单或 OnValidate 填默认，避免空字段。

**验收**

- [x] 精英 `resist=3` 出招中：普攻（level 1）只抖不断招。
- [x] 同精英：技能（level≥3）能进 Hit。
- [x] 杂兵 `resist=1` + 未改旧盒子：仍断招（接近改前手感）。
- [x] Phase SuperArmor 窗内普攻只抖。

**出口：** 冲击力对韧性 + 资产表。→ **已验收（2026-09-04）**。

---

### P-HR4 — 复制与观测

**待办**

- [x] 可靠命中事件带 `HitReactionKind`（`ActReplicatedHitEventCodec` V2）。
- [x] Observer Proxy：Action 快照连续 Seek，Flinch 另叠 Additive（`ActClientRoomGameplay.PlayReplicatedHits`）。
- [x] Owner Flinch **不**走受击硬吸（`ConfirmHitReaction` → Flinch 时 `VitalityEdge.None`）。
- [x] F3：`ReactionKind` / Additive 权重（`CharacterDebugSnapshot` + Proxy 裁档）。

**验收**

- [x] Listen 双端：轻击时两边 ActionId/帧连续，客机看得到抖。
- [x] 重击仍硬吸进 Hit，与改前一致。
- [x] Flinch 不产生错误 Action Ack / Restore。
- [x] 不宣称公网 / W10 出口。

**出口：** Listen 观测与 Flinch 复制。→ **已验收（2026-09-04）**。本轮到此结束。

---

## 11. 测试用例汇总

P-HR0 以 Play 人眼为主。以下从 P-HR1 起。

### EditMode（P-HR1+）

- [x] 冲击力 < 韧性 → `Flinch`
- [x] 冲击力 ≥ 韧性 → `LightStun`（HardHit）
- [x] 超出 2 → HeavyStun；超出 4 → Launch
- [x] SuperArmor → 非 Death 最多 Flinch
- [x] 同帧 Flinch+LightStun → 只 LightStun
- [x] DOT → None
- [x] 旧盒子冲击 1 打杂兵韧性 1 → LightStun
- [x] Attack01 冲击 2：杂兵 1 HardHit，精英 3 Flinch

### Play（P-HR2+）

- [x] 轻击：不停招、Shake、BT 不 Reset
- [x] 重击：进 Hit，树 Reset
- [x] 出招中连打轻击：招打完，Shake 可重触发
- [x] 轻击中吃重击：立刻 EnterHit，Shake 清
- [x] （P-HR4）客机：攻击 Clip 连续，Shake 跟事件，无误硬吸

---

## 12. 开工顺序（给自己勾）

1. [x] **P-HR0**：接 Mixer + 调试键，只验 Additive（Play 已确认 2026-09-03）。
2. [x] P-HR0 验收全勾后，才开 P-HR1 Resolver。
3. [x] P-HR1 单测绿，再改 Service（P-HR2）。
4. [x] P-HR2 Play 通过，再填盒子 / 抗打断（P-HR3）。
5. [x] 本机手感稳了再动复制（P-HR4 Listen 双端 Play 已验收 2026-09-04）。
6. [x] 停。失衡条、击飞物理不进本轮。

每阶段结束能 Play 一次再往下。不要先改 Hitbox 再发现 Additive 叠出来是 Idle 抽搐。

---

## 13. 明确不做

| 做法 | 原因 |
|------|------|
| 轻击 EnterHit 再马上切回 Action | 停招一帧，BT/预测脏 |
| `Hit_Shake` 做成 Graph Action | 占 ActionSim 游标 |
| `GameplayIntentUpgrade` 式窗改写受击 | 受击不是输入意图 |
| 轻击 Reset 行为树 | 攻击循环被普攻拆掉 |
| Shake 进 Snapshot | 表现细节，两端不必对拍肩膀 |
| 用 `AnimationMixerPlayable` 当 Additive | 那是覆盖插值 |
| 失衡条和本轮绑在一起 | 另一条资源，以后用 Numeric |

---

## 14. 相关文件（实现时）

- `Assets/Scripts/Domain/Character/Reactions/*`（Service / Resolver / Set / Vitality）
- `Assets/Scripts/Domain/Character/StateMachine/States/HitState.cs`（仅 Stun+）
- `Assets/Scripts/Domain/Character/CharacterActor.cs`（EnterHit 门闩）
- `Assets/Scripts/Domain/Combat` 下 `HitPayload` / Pipeline
- `Assets/Scripts/Domain/Combat/Actions/Definitions/Timeline` Phase（resist bonus）
- `PlayableAnimationPlayback` / `IAnimationPlayback`
- `CharacterActionPresentationBridge`
- `HitImpactController` / 复制命中事件
- `EnemyBrain.NotifyHit`
- `.cursor/skills/actgame-architecture/TECHNICAL.md` §8～9（落地后改）

---

## 变更记录

| 日期 | 说明 |
|------|------|
| 2026-09-03 | 初版：档位裁定、Flinch 不停招、Playable Additive、打断/抗打断 |
| 2026-09-03 | 对齐以往计划体例：分阶段待办/验收勾选；**P-HR0 改为 Additive 探针且必须先做**；原 Resolver 顺延为 P-HR1 |
| 2026-09-03 | P-HR0 代码：LayerMixer Additive、`PlayAdditive`、F6/菜单探针；Play 验收与 `Hit_Shake` 资产仍待 Editor |
| 2026-09-03 | P-HR0 Play 已确认 Additive；P-HR1：`HitReactionKind` / Command / `Resolve` + `HitReactionResolverTests`；Service 未改 |
| 2026-09-03 | P-HR2：Service 按 Command 分支；Flinch 发 `HitFlinchEvent` + `PlayAdditive`；不锁 Locomotion；Flinch 清 Vitality Hit 边沿 |
| 2026-09-03 | P-HR4 代码：命中事件 V2 带 `ReactionKind`；客机 Proxy Flinch Additive；F3 裁档；Listen 双端待 Play |
| 2026-09-03 | 裁定改为冲击力对韧性：不足 Flinch，持平起 LightStun；删除 `desiredReaction` 双轨 |
| 2026-09-04 | 用户验收：P-HR0～P-HR4 全部计划关闭；失衡条 / 击飞物理仍本轮不做 |
