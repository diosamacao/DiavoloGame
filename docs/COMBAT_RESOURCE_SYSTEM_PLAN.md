# 战斗资源系统 + 运行时调试 HUD — 实现方案

> 状态：**方案待实施**  
> 创建：2026-08-02  
> 参考：绝区零能量 / 喧响 / 支援点分层；本仓库锁步核见 [ACTION_SYSTEM_LOCKSTEP_REFACTOR_PLAN.md](./ACTION_SYSTEM_LOCKSTEP_REFACTOR_PLAN.md)  
> 关联旧稿：[COMBAT_ATTRIBUTES_DAMAGE_PLAN.md](./COMBAT_ATTRIBUTES_DAMAGE_PLAN.md)（命中链路描述已过期，以 `CombatHitPipeline` 为准；本方案**不**重做整套伤害公式，只做资源与闸门）

---

## 1. 结论摘要

1. 新增 **逻辑帧权威** 的角色/队伍资源模拟（能量 EX、喧响、闪避充能），禁止 `Time.deltaTime` / `Time.time` 作权威。
2. 在起手 / 高优打断 / Cancel 真正切招前统一走 **`IActionResourceGate`**；命中确认后走事件回填。
3. 同键双形态（可选）：能量够走 EX 节点，不够走普通特殊技——挂 Graph/Resolver，不改 Cancel 窗口语义。
4. 运行时提供 **左上角调试 Overlay**（开发开关），打印 EX、喧响、闪避次数、当帧意图、缓冲意图、锁定敌人、当前 Action/帧等；只读 Snapshot，禁止回写 Sim。
5. 分阶段落地：R0 调试可观测面 → R1 能量+闸门 → R2 闪避充能 → R3 喧响 → R4 同键双形态 / 专属资源。

---

## 2. 背景与缺口

### 2.1 已有（可挂接）

| 模块 | 用途 |
|------|------|
| `SimulationHost` / `SimulationWorld` 60Hz | 资源 `Step`、充能计时 |
| `CharacterActionDriver` | Locomotion 起手、高优打断 |
| `ActionSim` CommitPendingDecision | Cancel/自动衔接真正 Begin |
| `CombatHitPipeline` | 命中确认后 Grant |
| `GameplayIntentBuffer` | 当帧意图 + Cancel 缓冲 |
| `CombatTargetLock` | 锁定目标 |
| `ActionSim.Snapshot` | 当前招 / 帧 / 冻结 |

### 2.2 缺口

- 无能量 / 喧响 / 充能 Domain 类型
- `ActionExecutionPolicy` 无 cost / grant 字段
- `HitPayload` 无 onHit 回资源
- `CombatTargetLock` / `GameplayIntentBuffer` 未从 `CharacterActor` 对外暴露调试只读面
- 无运行时屏幕调试 HUD（现有多为 `Debug.Log`）

### 2.3 与旧「属性伤害方案」边界

| 本方案做 | 本方案不做（留给属性伤害方案演进） |
|----------|-------------------------------------|
| EX / 喧响 / 闪避充能 | 完整 ATK/DEF 公式、修饰器栈 |
| 起手扣费与命中回填 | 元素异常积蓄、失衡条（可后续复用事件总线） |
| 调试 Overlay | 正式战斗 UI（血条美术） |

---

## 3. 目标资源模型（首版对齐绝区零骨架）

### 3.1 资源一览

| 资源 | 归属 | 用途 | 首版规则（可配） |
|------|------|------|------------------|
| **Energy（EX）** | 角色 | 强化特殊技消耗 | max=120；接战每帧被动回复；命中可回；起手扣 |
| **Decibel（喧响）** | 角色（单机先个人条；多角色再升队伍） | 终结技门槛 | max=3000；命中/特定招式增加；放大招清空 |
| **DodgeCharges（闪避次数）** | 角色 | 闪避起手消耗 | max=2；耗 1 次；`rechargeFrames=60`（1s@60Hz）逐格回复 |

> 命名：代码用英文 `Energy` / `Decibel` / `DodgeCharges`；HUD 中文可显示「EX / 喧响 / 闪避」。

### 3.2 权威与时钟

```text
ResourceSim.Step(fixedDelta 隐含 1 逻辑帧)
  → 被动 Energy += energyRegenPerFrame（仅接战）
  → DodgeCharges 充能计时器 ++，满则 charges++

禁止：
  Time.deltaTime / Coroutine 倒计时作为权威
  表现层 OnGUI 修改资源
```

接战判定（首版简化）：`CharacterStateType` 为 Action/Hit，或本帧曾产生/承受命中后的 `inCombatHoldFrames` 倒计时（可配，如 180 帧=3s）。

### 3.3 事件回填（ResourceEvent）

```text
OnLogicFrame          → 被动回能、充能
OnActionBegin(cost)   → Spend（已在 Gate Commit）
OnHitConfirmed        → Grant(energyOnHit, decibelOnHit) 按 Action/Payload 配置
OnUltimateBegin       → Decibel = 0；可选 DodgeCharges++
OnWhiff（可选）       → 默认不回能（产品可改）
```

---

## 4. 架构与目录

```text
Assets/Scripts/Domain/Combat/Resources/
  ResourceId.cs                 // Energy / Decibel / DodgeCharges（可扩展）
  CharacterResourceSim.cs       // 单角色权威状态 + Step/Spend/Grant
  CharacterResourceConfig.cs    // 可序列化默认值（嵌 CharacterConfig 或独立 SO）
  ActionResourceSpec.cs         // 挂 ActionDefinition：cost / onHitGrant / tags
  IActionResourceGate.cs
  ActionResourceGate.cs         // CanAfford / CommitCost
  ResourceCombatFlags.cs        // InCombat 等

Assets/Scripts/Domain/Character/
  CharacterDebugSnapshot.cs     // 只读聚合：资源 + 意图 + 锁定 + Action 帧
  （CharacterActor 暴露 BuildDebugSnapshot()）

Assets/Scripts/App/Controllers/Debug/
  CombatDebugHudController.cs   // OnGUI 左上角；开发开关

Assets/Scripts/App/Events/Combat/（可选）
  ResourcesChangedEvent.cs      // 正式 UI 用；调试 HUD 可直读 Snapshot
```

分层铁律：

- `CharacterResourceSim` / Gate：**Domain 纯 C#**，不访问 Architecture。
- `CombatDebugHudController`：App 层，只读 `PlayerController` / Query 拿到的 Snapshot。
- 禁止 Debug HUD 调用 `Spend/Grant`。

---

## 5. 配置设计

### 5.1 `CharacterResourceConfig`（角色默认）

```csharp
// 示意字段
int maxEnergy = 120;
int energyRegenPerFrame = 0;          // 或 milli：每帧 20 = 1.2/s @60Hz → 用 milli 更贴绝区零
int energyRegenMilliPerFrame = 20;    // 累计到 1000 进 1 点，避免 float
int maxDecibel = 3000;
int maxDodgeCharges = 2;
int dodgeRechargeFrames = 60;         // 1s
int combatHoldFrames = 180;
```

挂载：`CharacterConfig` 新增嵌套字段，或独立 `CharacterResourceProfile` SO（二选一，**禁止双轨**）。推荐嵌套进 `CharacterConfig` 减少资产碎片。

### 5.2 `ActionResourceSpec`（每招）

挂 `ActionDefinition`（HideInInspector 旁或 ExecutionPolicy 旁新块）：

| 字段 | 含义 |
|------|------|
| `energyCost` | 起手/切到本招时扣除；0=不扣 |
| `energyGrantOnHit` | 每次有效命中回复 |
| `decibelGrantOnHit` | 每次有效命中喧响 |
| `consumeDodgeCharge` | 起手消耗 1 闪避次数 |
| `clearsDecibelOnStart` | 终结技：起手清空喧响 |
| `requiresDecibelFull` | 起手要求喧响满 |
| `resourceTag` | Basic / Special / EX / Ult / Dodge…（Gate/调试用） |

**产品定案（写入实现时遵守）：**

1. Cancel 连招切到新招时：**按新招 `energyCost` 扣费**；不够则**不提交切招**，保留当前招，缓冲意图保留到过期。  
2. 回能默认 **仅 ConfirmHit**（挥空不回）。  
3. 闪避：**时间充能**（每格独立计时或单槽计时，首版采用「缺几次就攒几次，一次 timer」）。

### 5.3 同键双形态（R4）

```text
Intent.Special（或现有技能意图）:
  Gate.HasEnergy(exCost) → Graph 解析 EX 节点
  else → 解析普通 Special 节点
```

实现落点：`ActionResolverService.TryResolveStart` 前注入「能量是否足够」到 `ActionResolveContext`，或 Graph Entry 条件节点。首版可硬编码「某 Intent + 能量分支」，再收拢为配置。

---

## 6. 运行时接入点（必须改的调用链）

### 6.1 装配

`CharacterActorFactory`：

```text
resourceSim = new CharacterResourceSim(config.Resources)
gate = new ActionResourceGate(resourceSim)
注入 CharacterActionDriver / ActionSim 侧（见下）
CharacterActor 持有 resourceSim + intentBuffer + targetLock 引用供调试
```

今日缺口：`GameplayIntentBuffer` / `CombatTargetLock` 只在工厂局部变量——需变为 `CharacterActor` 只读属性（或 Debug 专用访问器），否则 HUD 读不到缓冲与锁定。

### 6.2 起手 / 打断

`CharacterActionDriver.TryStartFromLocomotion` / `TryPriorityInterrupt`：

```text
resolve → gate.CanAfford(spec) → actionSim.TryStart
  成功 → gate.CommitCost(spec)
  失败 → return（不进 Action）
```

### 6.3 Cancel 切招

`ActionSim.CommitPendingDecision` 在 `Begin(next)` **之前**：

```text
if (!gate.CanAfford(nextSpec)) { 丢弃 pending 或保留？→ 定案：丢弃本次 pending 切招，不 Begin }
else { Begin; CommitCost }
```

Gate 需能从 `IActionSimContent` 取到 `ActionResourceSpec`（`ActionDefinition` 实现扩展属性或接口 `IActionResourceContent`）。

### 6.4 命中回填

`CombatHitPipeline` 在 ConfirmHit / 伤害结算成功后：

```text
attackerResourceSim.GrantOnHit(action.ResourceSpec)
```

攻击者 `CharacterResourceSim` 需能从 Pipeline 触达：经 `hitReceiver` 旁路、或 `ResolvedCombatHit` 增加 attacker 回调、或 Host 在 Publish 前根据 `SimActorId` 查表。推荐：**Pipeline 构造时注入 `Func<SimActorId, CharacterResourceSim>`**，避免 App Command 回写。

### 6.5 每帧 Step

`CharacterActor.Step` 内，在意图处理前后合适位置：

```text
_resourceSim.NotifyCombatActivity(...可选)
_resourceSim.Step()  // 被动回能 + 闪避充能
```

---

## 7. 运行时调试 HUD（本方案必交付）

### 7.1 目标

Play Mode 左上角常驻（可开关）文本面板，例如：

```text
[Combat Debug]
State: Action | Frame: 12/40 | Action: Attack_02 | Freeze: 0
EX: 86/120  (+regen 20m/f)   Decibel: 1240/3000
Dodge: 1/2  (recharge 37f)
InCombat: YES (hold 142f)
FrameIntents: Attack
Buffers: Attack(8f) Dodge(3f)
Lock: YES | Enemy_Foo | SimId=3 | Dist=2.14
Motor: (1.20, 0.00, -3.45) mm-facing=90000
SoftBody: mass=100 immovable=false
```

### 7.2 实现选型（定案）

| 方案 | 结论 |
|------|------|
| UGUI Canvas Prefab | 需改 Prefab/场景，违反资产只读习惯 → **不做** |
| IMGUI `OnGUI` | 无资产、适合调试 → **采用** |
| UI Toolkit Runtime | 可后补，非首版 |

类：`CombatDebugHudController : AppControllerBase`

- 场景挂在与 `SimulationHost` 同级或玩家旁（运行时 AddComponent 亦可，由 Player 生成）
- `[SerializeField] bool showDebugHud = true`（或 `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD` 默认开）
- `OnGUI` 用 `GUI.Label` / `GUI.Box`，左上角 `new Rect(8, 8, 420, …)`，黑半透明底

### 7.3 数据源：`CharacterDebugSnapshot`

每逻辑帧或每渲染帧由 `CharacterActor.BuildDebugSnapshot()` 填充（只读拷贝）：

```csharp
public readonly struct CharacterDebugSnapshot
{
    public CharacterStateType State;
    public string ActionName;
    public int ActionFrame, ActionTotalFrames, FreezeFrames;
    public int Energy, MaxEnergy, EnergyRegenMilliPerFrame;
    public int Decibel, MaxDecibel;
    public int DodgeCharges, MaxDodgeCharges, DodgeRechargeFramesLeft;
    public bool InCombat;
    public int CombatHoldFramesLeft;
    public string FrameIntents;      // "Attack, Dodge"
    public string BufferedIntents;   // "Attack(8f), Dodge(3f)"
    public bool HasLock;
    public string LockDisplayName;   // Transform.name 或 Definition.DisplayName
    public int LockSimId;
    public float LockDistance;
    public Vector3 MotorPositionMeters;
    public int FacingMilliDeg;
    public int SoftBodyMass;
    public bool SoftBodyImmovable;
}
```

暴露需求（工厂/Actor 改造）：

| 成员 | 现状 | 改造 |
|------|------|------|
| `CharacterActor.ActionSim` | 已有 | 读 Snapshot |
| `CharacterResourceSim` | 无 | 新增持有 |
| `GameplayIntentBuffer` | 工厂局部 | Actor 只读属性 + Buffer 增加 `EnumerateBuffered(remaining)` |
| `CombatTargetLock` | 工厂局部 | Actor 只读属性 |
| `CharacterMotor.Sim` | 已有 `MotorSim` | 位置/质量 |
| 锁定显示名 | `ITargetable` 无 Name | 用 `TargetTransform.name`；敌人可用 `EnemyHandle.Definition.DisplayName` 若 HUD 持有 Handle |

`GameplayIntentBuffer` 需新增调试枚举 API（不破坏现有消费语义）：

```csharp
void CopyBufferedForDebug(List<(GameplayIntentType intent, int framesLeft)> dst);
```

### 7.4 HUD 刷新时机

- **推荐**：`LateUpdate` 采样 `BuildDebugSnapshot()` 缓存，`OnGUI` 只画缓存（避免 OnGUI 多次调用重复分配）。
- 逻辑帧字段与表现帧可能差 1 帧，调试可接受；若要对齐逻辑帧，可订阅 `SimulationLogicStepEvent` 刷新。

### 7.5 开关

- Inspector：`showDebugHud`
- 可选快捷键：`F3` 切换（仅 Editor / Development Build）
- Shipping：整类包在 `#if DEVELOPMENT_BUILD \|\| UNITY_EDITOR`

---

## 8. 分阶段实施

### Phase R0 — 调试可观测面（无资源逻辑）

**目标：** 左上角先能看到状态 / 缓冲 / 锁定 / Motor。

- [ ] `GameplayIntentBuffer` 调试枚举 API  
- [ ] `CharacterActor` 暴露 `IntentBuffer`、`TargetLock`；`BuildDebugSnapshot()`（资源字段暂 0）  
- [ ] `CombatDebugHudController` OnGUI  
- [ ] Player 场景挂载或工厂自动挂  

**验收：** Play Mode 攻击时能看到 FrameIntents、Buffers、Lock、Action 帧变化。

### Phase R1 — Energy + Gate + 命中回能

- [ ] `CharacterResourceSim` / Config / `ActionResourceSpec`  
- [ ] Gate 接入 Driver 起手/打断 + ActionSim 切招  
- [ ] Pipeline 命中 Grant  
- [ ] 被动回能（接战）  
- [ ] HUD 显示 EX  

**验收：** 配 `energyCost=30` 的技能蓝不够起不来；普攻命中 +2；左上角 EX 同步变化。

### Phase R2 — 闪避充能

- [ ] DodgeCharges + rechargeFrames  
- [ ] Dodge Action `consumeDodgeCharge`  
- [ ] HUD 显示次数与充能帧  

**验收：** 两次闪避后第三次失败；约 1s 恢复一格。

### Phase R3 — 喧响

- [ ] Decibel 累加 / 满值 / 终结技清空  
- [ ] HUD 显示喧响  

**验收：** 命中涨喧响；满值才能起 Ult；起手清零。

### Phase R4 — 同键双形态（可选）

- [ ] Special Intent 能量分支解析  
- [ ] HUD 标注下一发将是 EX 还是普通  

---

## 9. 测试计划

| 测试 | 类型 |
|------|------|
| `CharacterResourceSim` Spend/Grant/充能帧 | EditMode（可进 Simulation 或 Combat 测试程序集） |
| Gate：不够费拒绝 / 够费扣除 | EditMode |
| 命中回能不在 Collect 阶段触发 | EditMode 或手册 |
| HUD | Play Mode 目视 |

---

## 10. 风险与定案备忘

| 风险 | 对策 |
|------|------|
| Cancel 扣费导致连招「卡手」 | 定案：不够不切招，缓冲保留 |
| 双轨 cost（Policy 与 Spec） | 只保留 `ActionResourceSpec` |
| Debug 分配 GC | Snapshot 复用 List；字符串 `StringBuilder` |
| 锁步预测回滚 | 资源进未来 Snapshot；R1 起字段固定顺序 |
| 旧 COMBAT_ATTRIBUTES 文档过期 | 伤害公式另开更新；命中入口以 Pipeline 为准 |

---

## 11. 决策记录

| 日期 | 决策 | 理由 |
|------|------|------|
| 2026-08-02 | 资源权威在逻辑帧 ResourceSim | 对齐锁步；禁表现倒计时 |
| 2026-08-02 | 调试 HUD 用 OnGUI，不做正式 UGUI Prefab | 资产只读；交付快 |
| 2026-08-02 | 首版喧响挂角色个人条 | 当前单角色 ACT；多角色再抽 TeamResourceSim |
| 2026-08-02 | 闪避用时间充能；回能仅 ConfirmHit | 贴近用户举例与绝区零「命中回能」主路径 |
| 2026-08-02 | R0 先做 HUD 再做资源 | 可观测优先，降低联调成本 |

---

## 12. 一句话

用 **ResourceSim + Gate + 命中/帧事件回填** 补上绝区零式能量循环，并用 **左上角 OnGUI Debug Snapshot** 把 EX、喧响、闪避、输入缓冲、锁定敌人打到屏幕上；先 R0 看见，再 R1～R3 逐项接真逻辑。
