# ACTGame 技术文档

> Last updated: 2026-10-01（新增 ActionGraph Cancel→Entry 隐式路由与连线转折点，补充编译通过；Unity / Play 待验收。此前 EndpointSigned、Action 输入移动、自动过渡及路径阻挡状态保留）
> 说明：记录**已实现功能**及其**实现方案**。架构分层见 [ARCHITECTURE.md](ARCHITECTURE.md)；编码约定见 [CONVENTIONS.md](CONVENTIONS.md)。

## 功能索引

| 功能 | 状态 | 入口 / 核心类 | 关键资源 |
|------|------|---------------|----------|
| Action 内输入移动 / 四向动画 | 🟡 代码落地，待 Unity / Play | `ActionInputMovement`、`CharacterActionGameplayStep`、`ActionInputMovementPlayer` | Input Movement 轨道配置窗口与四个 Inplace Clip；不改生产资产 |
| 三人阵容 / 单键换人 / 死亡接替 | ✅ P-SW0～P-SW1 Graph、资产与 Play 已验收（2026-09-19） | `PartyLoadout`、`PartyCombatCoordinator`、`PartyDeathSwitchPolicy`、`ActGameGuest` | 各角色 Graph 已配置 `SwitchIn/SwitchOut` Entry |
| 极限支援 / 接触弹刀 | ✅ P-SW2 + P-PR + 卡肉 Graph、资产与 Play 已验收（2026-09-19） | `WorldAssistCueBoard`、`IssueParried`、`ParriedActionPolicy` | Guard/Success、Parried 规则、进攻盒 Id 与连续弹刀窗已配置 |
| Wave4 位移（Adhesion / SoftBody / Relocate） | ✅ 已实现（吸附已验收；Relocate 已接线） | `ActionMotionAdhesion` + `ActionMotionResolver` + Bridge | Branch_02 吸附已配；Relocate 按需加 MotionCommand 轨；相机不在本 Wave |
| 动作突刺路径阻挡 | 🟡 代码已实现，待 Unity / Play 验收 | `ActionBodySweep` + `CharacterBodyObstacleQuery` + `CharacterMotor.MoveActionMm` | 两份 Vivian 动作启用配置与验收见实施记录 |
| 命中受击 Cue（VFX/SFX） | ✅ 已实现（A2 打击感验收 2026-08-09） | `HitImpactController` + `HitFeedbackSettings` | 接触点落点 + 随机旋转；普攻 Cue 已验 |
| 逻辑 Hurtbox 调试线框 | ✅ 已实现 | `CombatHurtboxDebugSettings` + `CombatHurtboxDebugVisualizer` | F4 开关（F3 HUD 显示状态） |
| Playable Additive 探针 | ✅ P-HR0 Play 已验收 | `PlayableAnimationPlayback.PlayAdditive` + F6 | Listen 无头敌人打 Observer Proxy；HUD 拖 `Hit_Shake` |
| 受击档位裁定 | ✅ P-HR0～P-HR4 已验收（2026-09-04） | `CharacterReactionService` + `HitFlinchPlaybackController` | 冲击对韧性；Listen 客机 Flinch Additive；不宣称公网 |
| 固定帧模拟宿主 | ✅ L0A 已实现 | `SimulationHost`、`SimulationWorld`、`SimActorId` | 60Hz，无资产 |
| Wave0 动作审计 / 锚点可视化 / Debug HUD | ✅ 已实现 | `ActionDefinitionAuditUtility`、`CharacterAnchorGizmoDrawer`、`CombatDebugHudController` | 菜单 `ACTGame/Action/Validate Motion Sources`；场景挂 HUD |
| 结构与内容总门禁 | ✅ CS7 已实现 | `StructureValidationBatch`、`StructureAuditRuleSet`、`ci.ps1` | BatchMode 阻断；EditMode；可选 Dedicated READY smoke |
| 角色朝向调试箭头 | ✅ Play 实心箭 | `CharacterFacingDebugVisualizer` + `ICharacterFacingDebugTarget` | 本体 / 客机他人幽灵各一份；黄=wish 品红=模型 |
| 动作位移投影 / BaseMotionMode / 相机滤左右 | ✅ 代码已实现，端点投影待 Unity 验证 | `EndpointSigned`、`ActionBaseMotionMode`、`CameraManager.lateralFollowFactor` | 整个 Action 的起终点连线作为投影轴，保留推进/回撤；模式数值 2 替换 ForwardSigned，原始表无需重烘焙 |
| 视觉残差 / VisualMotionRoot | ✅ 代码已实现，端点闭合待 Unity 验证 | `CharacterVisualMotionBridge`、`TryGetVisualResidualMm` | 原始累计减投影累计；完整动作末帧为零，提前取消仍 BlendToZero；相机可跟随净侧向位移 |
| Wave3 玩法资源 / 同键 EX | 🟡 资产待绑；运行时已迁 Numeric | `NumericCostGate`、`ActionResourceSpec`、`ActionEnergyFormSelector` | Spec 填表；Graph 双 Entry |
| GAS-lite 数值重构 | ✅ G0～G5 完成 | `NumericSystem`、`DamageNumericCalculator`、`CharacterVitality` | Effect SO 壳 |
| 完美闪避反击（Wave 3.4） | ✅ 代码路由完成 | `PerfectDodgeAttack`、Pipeline 武装、Begin 清缓冲 | Graph Counter Entry（Editor） |
| 第三人称移动 | ✅ 已实现 | `PlayerController` + `CharacterActor` + `CharacterConfig` | Scene Empty + CharacterConfig |
| 输入（量化帧 + 语义意图） | ✅ L0B + C-AT0 代码已实现 | `InputFrameBuffer`、`InputReader`、`InputManager`、`GameplayIntentProducer` | MoveReferenceYaw 已闭包；Parry 已随 P-SW2 验收，TargetSwitch 仅在需要手动切敌时再绑定 |
| 组队 PVE 状态同步 / 权威进程 | 🟡 W10 已验收 / W11 待最终验收 | `ReplicationBuildOptions` + `GraphNodeKey` + Replication V2 | V2 FakeActionGame、10+ Actor / 兴趣 / Owner 预算测试已补并编译；待 Unity Test Runner + Play 关闭 R2 |
| 敌人木桩 AI 开关 | ✅ 已实现并验收 | `EnemyBrainProfile.enableCombatActions` + `Monster_EDF` | 2026-08-08 Play：Hit_Shake / 高 HP / 不追打 |
| CombatMode→Graph | ✅ Phase B | `CombatModeEntry.actionGraph` / `ActiveGraph` | 已删 PlayerActionSet 与一次性迁移器 |
| Input + Locomotion 收敛 | ✅ CS7 | `ClientRuntimeConfiguration` + Catalog GameplayIntent；Mode→`LocomotionProfile` | Config 不再挂 Input/Intent/Locomotion；Legacy timing 字段已删 |
| 状态机框架 | ✅ 已实现 | `StateMachine<,>`、`CharacterStateMachine` | — |
| 架构通信框架 | ✅ 已实现 | `ACTGameArchitecture`、`ArchitectureSystemBase`、`AppControllerBase`、Command / Query / Event | — |
| Locomotion 动画驱动 | ✅ 已实现 | `LocomotionStateMachine` + `LocomotionState` | AnimationProfile + `CharacterLocomotionProfile` |
| Locomotion 起步/急停/转身 | ✅ Play 2026-08-12 | 内层相位 + L-DIR1～5 + Pivot 两段式 | 旧 Phase D 减速曲线不做 |
| Sprint 倾身 / 相机跟朝向 | ✅ Play 2026-08-12 | `SprintLeanModel` + `CameraManager` Follow Facing | 出招时暂停跟朝向 |
| 第三人称相机 | 🟡 Director + SkillShot Spline 代码完成；Lock-On 暂时舍弃 | `CameraManager` + `CameraDirector` + `CameraShotPlayer` | CM2 + Unity Splines 2.8.4；C-SP Test/Editor/Play、UI 展示舱与 Cutscene 接入待做 |
| 唯一战斗目标 | 🟡 代码完成、输入资产与 Play 待验 | `CharacterTargetingState` + `DeterministicTargetResolver` | 自动最近、滞回保持、Action 中 TargetSwitch |
| 动作系统（整数帧 / 选招 / 取消 / 连段 / 高优打断 / 战斗模式） | ✅ L1B 已实现（Play Mode 待回归） | `ActionSim` + `CharacterActionPresentationBridge` + `ActionFrameQuery` | 60Hz Action + `ActionGraph` |
| Action Editor（时间轴编辑） | 🟡 骨架/部分 | `ActionEditorWindow` + `ActionTimeline` 手动加轨/窗口 | Menu：`ACT/Action Editor` |
| 攻击 / 战斗判定 | ✅ L0C 延迟结算已实现 | `CombatHitPipeline` + `CombatDamageCalculator` + `CharacterReactionService` | SimHitKey；HitPayload；Hit/Death 状态 |
| 敌人 AI / 行为树 | ✅ 8.10 + 对峙已验收 | Runner + GraphEditor；Desire/Request；节点时间填秒 | 待优化：`docs/2026.8.11/ENEMY_BEHAVIOR_TREE_BACKLOG_PLAN.md` |
| UI | ⬜ 未实现 | — | `UI/` 占位 |

状态图例：✅ 可玩可用 · 🟡 有类/占位但未接完 · ⬜ 未开始

---

## 0.-1 结构与内容总门禁（CS7）

### 功能说明

结构稳定化后的 Assembly 边界、源码禁用模式和 Gameplay Content 深校验通过一个 BatchMode 入口统一阻断，避免只运行部分菜单得到假通过。

### 实现方案

| 项 | 方案 |
|----|------|
| 统一入口 | `StructureValidationBatch.RunAll`；BatchMode 下任一 Error 使用退出码 1 |
| 结构范围 | `StructureAuditRuleSet` 校验 asmdef 白名单、反向依赖、旧协议、运行时 Find/Resources、静默 catch 与 public 类型数量 |
| 大类门禁 | 生产运行时文件阈值 450 行；超限须拆分或登记单一聚合门面职责 |
| 内容范围 | 全库 Action Motion/60Hz、CharacterConfig→CombatMode/Locomotion/Reaction、EnemyDefinition/BehaviorTree |
| 本地 CI | 根目录 `ci.ps1`：统一门禁 → 全量 EditMode → 配置了 Dedicated 出包时 READY smoke |

### 运行流程

```text
./ci.ps1
  → Unity -executeMethod StructureValidationBatch.RunAll
      → StructureAuditRuleSet.AuditProject
      → ActionDefinitionAuditUtility.AuditProject
      → CharacterConfig.ValidateGameplayContent
      → EnemyBehaviorTreeSetupMenu.AuditProject
  → Unity -runTests -testPlatform EditMode
  → tools/dedicated/smoke-ready.ps1（可选）
```

### 已知限制

- `ci.ps1` 运行前需关闭正在占用该工程的 Unity Editor。
- Dedicated smoke 需要预先构建可执行文件并设置 `ACTGAME_DEDICATED_EXE`；未设置时明确跳过，不伪装通过。
- Listen + Client 游玩回归仍是人工总出口，脚本不能替代网络表现与 Inspector 绑定验收。

### 相关文件

- `Assets/Scripts/Editor/Architecture/StructureValidationBatch.cs`
- `Assets/Scripts/Editor/Architecture/StructureAuditRuleSet.cs`
- `Assets/Scripts/Editor/Enemy/EnemyBehaviorTreeSetupMenu.cs`
- `ci.ps1`

---

## 0.0 三人阵容 / 单键换人（P-SW0 + P-SW1）

### 功能说明

玩家座位由 `PartyLoadout` 声明最多三个角色；空格按槽序切到下一名可用角色。新角色落到旧角色局部右侧 0.6m，立即接管输入并请求 `SwitchIn`；旧角色空闲时立即播放 `SwitchOut`，已有 Action 时在首次 Recovery 停止原招并转入 `SwitchOut`，最终只在 `SwitchOut` 自身 Recovery 隐藏。

### 实现方案

| 项 | 方案 |
|----|------|
| 角色身份 | `CharacterId`；Ordinal 字符串值 |
| 角色定义 | `CharacterDefinition` 引用现有 `CharacterConfig`，并声明 `CharacterAssistStyle` |
| 阵容 | `PartyLoadout` 保存 1～3 个 Definition 引用和 `StartingSlot`；空槽合法、Id 不可重复 |
| 输入 | `InputButton.SwitchCharacter` 固定 bit 9；`InputReader` 可选采样同名 Input Action |
| 顺序选择 | `PartySlotSelector` 从 Active 后一槽正序绕回，只接受 `Inactive` |
| 普通切裁定 | `PartyCombatCoordinator.TryResolveSwitchIn` 输出 `DualPresence`，旧槽 Active→Exiting，新槽 Inactive→Active |
| 死亡收尾 | `PartyDeathSwitchPolicy/Gate` 最短等待 15 帧；`DeathSequenceComplete` 为主信号，缺信号时按死亡 Action 总帧 + 15 强制提交 |
| 死亡接替 | `PartyCombatCoordinator.TryResolveActiveDeath` 原子写死亡槽 `Dead`，下一存活槽 `Active`；无存活槽则 `IsPartyWiped`，禁止复用 `Exiting/SwitchOut` |
| 角色侧生命周期 | `CharacterPartyLifecycle` 独占槽状态、普通退场、Assist 意图/卡肉转移、动作起手事件和换人落位；`CharacterActor` 不保留旧字段或转发方法 |
| 普通切落点 | `PartySwitchPlacement` 按旧角色 Motor 朝向取局部右侧 600mm；`CharacterPartyLifecycle.PlaceForNormalSwitchFrom` 从旧位置经新角色静态碰撞世界解析后落地 |
| 退场时序 | `CharacterPartyLifecycle.BeginExit/AdvanceAfterPostCombat`：空闲立即注入 `SwitchOut`；切人输入到达时已在 Recovery 则在下一次 Action Step 前立即交接，否则到首次 Recovery 停止并排队 `SwitchOut`；交接前必须离开 `Hit`（Driver 只从 Locomotion 起 SwitchOut）；`IsExitReady` 只认 SwitchOut 实例的 Recovery |
| 运行时槽 | `PlayerPartyRuntime` / `ActGameGuest` 均按非空槽创建独立 `CharacterActor`；`PlayerController` 只装配本机 Runtime 与设备输入；Inactive 空输入且不参与软碰撞/受击 |
| 稳定身份 | 每槽独立 `SimActorId` / `NetEntityId`；禁止单 Actor 热换 Config |
| Owner 复制 | 应用载荷下发槽 ActorId、ActiveSlot、累计命令 ACK；角色快照 `FlagsPacked` 逐槽纠正本机 `PartyState`，自有后台槽不会创建 Observer Proxy |
| 状态复制 | `PartyMemberState` 编入角色快照 `FlagsPacked` 低三位；Observer 仅显示 Active / Exiting；PartyWiped V2 Meta 尚未接 |
| 内容预填 | Client / Server 登记 Loadout 全部角色 Archetype 与动作 |

### 运行时流程

```text
InputReader.Sample → InputFrame.SwitchCharacter
  → ClientCommand（仍是一座位一条输入流）
  → DedicatedAuthorityWorld → AuthorityStepCoordinator 预合并未应用命令
  → ActGameGuest.TryResolveSwitch
      → from.PartyLifecycle.BeginExit；to.PartyLifecycle = Active
      → from 空闲（含纯帧 Hurt）：离开 Hit 后 PartyLifecycle.QueueExternalIntent(SwitchOut)
      → from 有招/受击招：首次 Recovery 后 Stop 并离开 Hit，再由 PartyLifecycle 排队 SwitchOut
      → SwitchOut 首次 Recovery：PartyLifecycle.CompleteExit
      → to 从旧槽逻辑根沿旧角色局部右向偏移 600mm（静态碰撞解析）
      → to 优先朝向 SelectedTarget
      → to.PartyLifecycle.QueueExternalIntent(SwitchIn)
  → 输入写入新 Active ActorId
  → SimulationWorld.Step（Active + Exiting + Inactive 独立 Actor）
  → ReplicationLifecycle/Snapshot（全部槽快照 + Owner ActiveSlot Meta）

客户端同帧：
ActClientRoomGameplay.StepPrediction → OwnerPredictionCoordinator.StepPrediction
  → PlayerPartyRuntime.StepPrediction
  → Active 收输入；Exiting/Inactive 收空输入
  → 累计 ACK 未覆盖预测切人帧时，不用旧快照撤销切人

Active 死亡：
CharacterActor.ResolvePostCombat → DeathSequenceComplete
  → AuthorityStepCoordinator.OnAfterLogicStep
  → PartyDeathSwitchGate（至少 15 帧；cap = DeathAction.TotalFrames + 15）
  → ActGameGuest：死亡槽直接 Dead；下一槽直接 Active + SwitchIn
      → 无下一槽：PartyWiped，继续 ACK 但清空玩法输入
  → 同帧 Capture：ActiveSlot + 各槽 FlagsPacked
  → PlayerController 镜像同一门禁，并由 ActiveSlot / FlagsPacked 纠正
```

### 已知限制

- `SwitchIn/SwitchOut` Graph 与正式资产已于 2026-09-19 完成用户 Play 验收。
- 原 Action 没有 Recovery Phase 时，等其自然结束后再切 `SwitchOut`，避免在 Startup/Active 中硬掐。
- `Hit` 不能转 `Action`；退场交接若不停在 Hit，`SwitchOut` 意图被丢掉，槽永久 `Exiting`，无法切回。
- `SwitchOut` 必须配置 Recovery Phase；缺失时角色不会被静默隐藏，便于暴露资产错误。
- P-SW0～P-SW2 的 Graph、Timeline 资产与 Play 已于 2026-09-19 用户验收；未在本次文档回写中重新运行自动化 Test Runner。
- PartyWiped 已进入 V2 Snapshot Meta；队灭 UI/Match 结果仍留给后续功能阶段。

### 相关文件

- `Assets/Scripts/Domain/Party/*`
- `Assets/Scripts/Domain/Simulation/Party/*`
- `Assets/Scripts/App/Controllers/Gameplay/PlayerController.cs`
- `Assets/Scripts/App/Networking/Content/GameContentBootstrap.cs`
- `Assets/Scripts/App/Networking/Content/GameContentCatalog.cs`
- `Assets/Scripts/App/Networking/Adapters/ActGameSessionHandler.cs`
- `Assets/Scripts/App/Networking/Services/DedicatedAuthorityWorld.cs`
- `Assets/Scripts/Domain/Networking/ActReplicationSnapshotMeta*.cs`
- `Assets/Scripts/Domain/Simulation/Input/InputButton.cs`
- `Assets/Scripts/Infrastructure/Input/InputReader.cs`
- `docs/2026.8.30/PARTY_SWITCH_ASSIST_PLAN.md`

### 变更日志

- 2026-09-16：新增 Party 死亡 15 帧门禁、确定性 Action cap、AfterLogicStep 自动接替/队灭、Owner FlagsPacked 槽状态纠正与队灭输入门禁。

---

## 0.1 极限支援 / 接触弹刀（P-SW2）

### 功能说明

金光窗内切近战：下场当帧消失，上场先播 `AssistParry` Guard（举刀、无敌、接触窗，无 clang）。敌人 Active Hitbox 打中该窗后，玩家切 `AssistParrySuccess`（clang）并武装支援突击；攻击者强制进现有 `HitState`。0 点或类型不匹配按红光换人闪。远程上场走 `AssistEvade`。

### 实现方案

| 项 | 方案 |
|----|------|
| 闪光 | 敌人 Timeline `AssistCueNotifyState`（Gold/Red、`requiresRanged`、弹刀/回避偏移）；`WorldAssistCueBoard` 步进后收集，切人读上一拍 |
| 支援点 | `PartyAssistPoints`：默认上限 6、开局 3、耗 1、Ult +3；数值由 `PartyLoadout` 的支援点字段覆盖 |
| 裁定 | `PartyCombatCoordinator.TryResolveSwitch`：无 Cue → DualPresence；Gold 且能花 → InstantReplace + 扣点；0 点 / 远程点名不匹配 → Red `SwitchPerfectDodge`；突击武装中拒绝 |
| 上场意图 | `PartySwitchApplication`：`AssistParry` / `AssistEvade` / `SwitchPerfectDodge`；不播 SwitchIn/Out |
| 本体弹刀 | `InputButton.Parry` → `GameplayIntentType.Parry`；Producer 按下边沿产出，不走 Coordinator、不耗支援点；Graph Entry 起同一套 Guard |
| 接触 | `IHitAbsorbQuery.IsInAssistParryWindow`；管道优先于 PD 与无敌：玩家不 OnHit，攻击者 `IssueParried` |
| 被弹刀 | `HitPayload.ParriedActionPolicy`：`Interrupt` 才 `ResolveParried(parriedReactionId)` + LightStun 边沿 + `EnterHit`；`Continue` 不停招、不写边沿。选片：`Parried+Id` → Parried 默认 → Hit 默认。禁止冲击力裁定 / `HitReactionKind.Parried` |
| 卡肉 | `ResolvePending` 裁定后唯一 `ApplyConfirmedHitStop`。真伤只冻进攻实例且仍受 `UseHitStop` 门控；弹刀帧只读当前招 `AssistParryWindow.hitStopFrames`（无窗回退 8），`ArmAssistParryHitStopCarry` 把剩余帧带到 Success |
| Dedicated Owner | 吸收结果随 `ReplicatedHitEvent` 可靠下行；按 `PlayerController.Party.Actors[].SimulationId` 找目标，Meta 早到/晚到均经有界 pending 重试；成功应用或确认非本阵容后才完成去重 |
| 突击 | `Flags.ArmAssistFollowUp`；Producer 攻击族 Pressed 优先于 PD 派生 `AssistFollowUp`；切人当帧不武装 |

### 关键参数

| 参数 | 值 |
|------|-----|
| 支援点 | `PartyLoadout`：max / starting / assistCost / ultimateGrant（默认 6 / 3 / 1 / 3） |
| 突击窗 | 复用 `CharacterNumericConfig.PerfectDodgeCounterFrames`（默认 45） |
| Success 取消优先级 | 96（高于 Guard 92、突击 94） |

### 运行时流程

```text
切人弹刀：SwitchCharacter → Coordinator → Queue AssistParry
本体弹刀：InputButton.Parry → Producer.Parry → Graph Guard（不切人、不扣点）
敌人 Hitbox → Pipeline.IsInAssistParryWindow
  → IssueParried（Interrupt 才 EnterHit；Continue 只冻当前招）
  → NotifyAssistParryContact → ArmAssistFollowUp
       首次 → 排队 AssistParrySuccess
       已在 Success → 不重切
  → ApplyConfirmedHitStop(BothSides) + ArmAssistParryHitStopCarry
下一帧 TryPriorityInterrupt(仅放行 Success) → Begin 后补 freeze → AssistParrySuccess 停在 frame 0
攻击键 → Producer AssistFollowUp
```

### 已知限制

- Guard / Success / AssistFollowUp / 敌人 Cue / `Parried` 反应片与本体 `Parry` 输入/Graph 已于 2026-09-19 完成用户 Play 验收；Agent 仍不直接修改 `Assets/Data/**`
- 弹刀卡肉帧在 Guard/Success 的 `AssistParryWindow.hitStopFrames` 上配置；不再读进攻盒 UseHitStop
- 被弹选片：进攻盒 `Parried Reaction Id` + 敌人 `ReactionSet` 的 `Parried` 规则；`Continue` 盒不停招
- 连续自动弹刀：Success 上继续铺 `AssistParryWindow`；已在 Success 不重切 clang
- 无受击片时攻击者 `ActionSim` 已 Stop，该侧不写 freeze（不另开 HitState 冻帧）
- 接触成功后 Success 可能晚一帧（`QueueExternalIntent` 下一次 Step 才进 buffer）
- 客机不预测「敌人打玩家」几何卡肉；本机预测只镜像权威接触列表
- P-SW3 击飞快速支援、P-SW4 连携 +1、P-SW5 Cue/点数复制未做
- 禁止 `Time.timeScale`；回避支援极限视域未接
- 不宣称公网

### 相关文件

- `Assets/Scripts/Domain/Simulation/Party/WorldAssistCueBoard.cs`
- `Assets/Scripts/Domain/Simulation/Party/PartyAssistPoints.cs`
- `Assets/Scripts/Domain/Character/Reactions/CharacterReactionService.cs`
- `Assets/Scripts/Domain/Combat/Hitbox/CombatHitPipeline.cs`
- `Assets/Scripts/Domain/Combat/Hitbox/HitPayload.cs`
- `Assets/Scripts/Domain/Combat/Hitbox/ParriedActionPolicy.cs`
- `Assets/Scripts/Domain/Combat/Hitbox/AssistParryHitStop.cs`
- `Assets/Scripts/Domain/Input/GameplayIntentProducer.cs`
- `Assets/Scripts/Domain/Simulation/Input/GameplayIntentType.cs`
- `Assets/Scripts/Domain/Combat/Actions/Definitions/Timeline/AssistCueNotifyState.cs`
- `Assets/Scripts/Domain/Combat/Actions/Definitions/Timeline/AssistParryWindowNotifyState.cs`
- `Assets/Tests/EditMode/Simulation/PartyAssistResolveTests.cs`
- `Assets/Tests/EditMode/Domain/SelfParryIntentTests.cs`
- `Assets/Tests/Editor/Combat/SelfParryProducerTests.cs`
- `Assets/Tests/Editor/Combat/AssistParryPipelineTests.cs`
- `Assets/Tests/Editor/Combat/AssistParryHitStopTests.cs`
- `Assets/Tests/Editor/Combat/AssistParryHitStopCarryTests.cs`
- `docs/2026.8.30/PARTY_SWITCH_ASSIST_PLAN.md`
- `docs/2026.9.5/ASSIST_PARRY_HITSTOP_PLAN.md`
- `docs/2026.9.6/ASSIST_PARRY_OUTCOME_PLAN.md`

---

## 0. 架构通信框架

### 功能说明

参考 QFramework 的分层方式，项目通过 `ACTGameArchitecture` 统一管理跨系统通信；进入 IOC 的对象必须实现对应契约或基类。

### 实现方案

| 项 | 方案 |
|----|------|
| 架构入口 | `ACTGameArchitecture.Interface` 懒加载注册默认 System |
| System | `ArchitectureSystemBase` + `IArchitectureSystem`，通过 `RegisterSystem` 进入 IOC |
| Controller | `AppControllerBase` + `IArchitectureController`，Unity 表现入口通过能力方法访问架构层 |
| Command | `ArchitectureCommandBase` + `IArchitectureCommand`，表达一次会改变状态的业务行为 |
| Query | `ArchitectureQueryBase<TResult>` + `IArchitectureQuery<TResult>`，表达无副作用读取 |
| Event | `IArchitectureEvent` 标记接口，限制可分发事件类型 |
| Editor 校验 | `ArchitectureBoundaryValidator` 检查 App/Systems、App/Controllers、App/Events 与 Domain 单例访问 |

### 运行时流程

```
AppControllerBase
  → SendCommand / SendQuery / RegisterEvent
  → ACTGameArchitecture
      → ArchitectureSystemBase / ArchitectureCommandBase / ArchitectureQueryBase
      → IArchitectureEvent
```

### 已知限制

- 生产业务已拆为显式 asmdef：Core / Input / Combat / Character / Enemy / Networking / Infrastructure / App / Server / Previews 与 ACTNet；运行时代码不再依赖默认 `Assembly-CSharp` 跨层可见性。
- Model / Utility 容器已具备 API，但当前暂无业务 Model / Utility 注册。

### 相关文件

- `Assets/Scripts/App/Architecture/*`
- `Assets/Scripts/Editor/Architecture/ArchitectureBoundaryValidator.cs`

---

## 0.1 固定帧模拟宿主

### 功能说明

玩家与敌人不再由各自 Controller 的 `Update` 分散推进；场景唯一 `SimulationHost` 将渲染帧时间累积为 60Hz 固定逻辑帧，并由 `SimulationWorld` 按稳定 `SimActorId` 顺序 Step。

### 实现方案

| 项 | 方案 |
|----|------|
| Unity 入口 | `CombatWorldController` 自动确保同物体存在一个 `SimulationHost` |
| 固定频率 | `SimulationConfig.DefaultLogicHz = 60` |
| 追帧 | `FixedStepAccumulator` 单渲染帧最多 8 Step；超额欠账保留，不丢逻辑时间 |
| Actor 身份 | World 从 1 单调分配 `SimActorId`，会话内不复用 |
| Actor 顺序 | `CharacterActor` / `EnemyHandle` 实现 `ISimulationActor`，按注册 Id 升序执行；角色入口内部唯一转交 `CharacterSimulationPipeline` |
| 渲染输入 | `IRenderFrameSampler` 每渲染帧汇聚设备边沿；无逻辑 Step 时 Pressed/Released 保留到下一 Step |
| 输入帧 | `InputFrame` 使用 sbyte Move、MoveReferenceYaw、稳定按钮 bitset、frame 与 SimActorId；World 持有有界 `InputFrameBuffer`（`MaxHistoryFrames=64`） |
| 输入阶段 | 每帧先调用 `ISimulationInputProducer`；AI 基于 Actor Step 前的 N-1 已提交状态写 N 帧输入 |
| 命中阶段 | 全体 Actor 只 Collect；`CombatHitPipeline` 按 `SimHitKey` 排序后统一 Resolve |
| PostCombat | `ISimulationPostCombatActor` 在结算后处理 OnHitConfirm/OnWhiff 与自然结束 |
| Commit | 当前死亡目标注销与敌人 Despawn 固定在 Combat/PostCombat 后执行 |
| 表现插值 | 模型位于运行时 `CharacterPresentationRoot`；Host LateUpdate 按 accumulator alpha 插值前后逻辑 Pose |
| 相机跟随 | `CameraManager` 跟随玩家表现锚点，不直接追阶梯式权威 Transform |
| 生命周期 | Controller 在 OnEnable 注册、OnDisable/OnDestroy 注销；禁用对象不会继续模拟 |
| 测试 | `ACTGame.Simulation.EditModeTests` 覆盖 Id、accumulator/alpha、注册/注销、Step 与 Render 转发 |

### 运行时流程

```
SimulationHost.Update
  → SimulationWorld.SampleRenderFrame
      → InputReader 量化并合并到 CurrentFrame + 1
  → FixedStepAccumulator.ConsumeSteps(Time.deltaTime)
  → 重复 N 次 SimulationWorld.Step
      → ISimulationInputProducer.ProduceInput（AI）
      → InputFrameBuffer.ResolveLocal
      → CharacterActor.Step → CharacterSimulationPipeline.Step / EnemyHandle.Step（Control / Motion / Hit Collect）
  → CombatHitPipeline.ResolveBeforePostCombat（稳定排序、伤害、Reaction、ConfirmHit）
  → SimulationWorld.ResolvePostCombat
      → CharacterActor.ResolvePostCombat → CharacterSimulationPipeline.ResolvePostCombat（自动 Transition / 动作结束）
  → CombatHitPipeline.CompleteFrame（Transition frame 0 命中 + 只读 App 结果）
  → CommitEnemyLifecycle（死亡注销与 Despawn Command）
SimulationHost.LateUpdate
  → SimulationWorld.Render(alpha)
  → CharacterPresentationBridge 插值模型锚点
  → CameraManager.LateUpdate 跟随同一表现帧
```

### 已知限制

- L0B 已切换量化输入与整数帧 Hold/Buffer/AI 冷却；完整脱设备玩法回放仍需 Play Mode 确认。
- L0C 已删除同步 `ApplyHitCommand` 与 `GetInstanceID()` 去重；真实多命中、互杀及交换注册顺序仍需 Play Mode 验收。
- L1B：动作权威在纯 `ActionSim`；全部 ActionDefinition 已为 60Hz。剩余为 Play Mode / Test Runner 人工验收；Player 占位 Action 无动画段时 `IsSimulationReady=false`。
- L2/M0–M1：运动表烘焙 + 运行时查表。`bakeStatus=Ok` 时表现桥按帧取本地 Δ 经 MotorSim 移动；Wave 2.5 已删除 Animator RM 回退。
- L2 HitStop：`hitStopFrames` 经 Pipeline 写入 `ActionSim.freezeFrames`；冻结期间不推进动作帧/位移；骨骼由表现桥读 Snapshot，VFX 由 `SimulationLogicStepEvent` 递减。
- L2 Locomotion：Stop/Pivot 根位移按 `ActionSim.LogicHz` 整数帧取轨，不再用 `NormalizedTime`。
- L2 MotorSim：水平+竖直毫米权威；`TickVertical` 整数重力/着地；逻辑路径不再 `CharacterController.Move`；CC 保持禁用（禁止 Sync 后 re-enable，否则 PhysX 挤出地面呈悬空）。
- L2 静态碰撞：`StaticCollisionBake`（菜单 `ACTGame/Collision/Bake Static From Scene...`）→ `SimStaticCollisionWorld` AABB 滑墙；`CombatWorldController` 绑定资产，未绑定则 `OpenField`。地面薄板/名含 Floor·Ground·Terrain 只写 GroundY，不进水平硬挡；墙体才投影 AABB。Mesh 墙仍用包围盒（保守）；无斜坡。
- L2/M2：`Bake All` / `Bake Dirty Only` + Inspector Dirty 黄条 + `ACTGame/Motion/Validate Motion Dirty`。
- L2 软弹开：`SimulationWorld` 帧末按 Id 序对 `ISimSoftBodyParticipant` 执行 `SoftBodySeparation`（默认 factor=500‰、迭代 3）；按 `softBodyMass` 分配推力，`softBodyImmovable` 像墙；死亡不参与。
- L2 命中：`SimCombatPose` 从 MotorSim 取水平根；Hitbox 挂点只提供相对根局部 TRS；Hurtbox 用 `GetLogicalHurtbox`；自身排除用 `SimActorId`。
- 联网定案：Dedicated 权威状态同步；上行 `InputFrame`，下行 V2 Lifecycle/Snapshot/Event；命中只在权威 Pipeline。锁步 L5 已取消。阅读：[`docs/2026.8.23/NETSYNC_FROM_JOIN_TO_HIT.md`](../../../docs/2026.8.23/NETSYNC_FROM_JOIN_TO_HIT.md)。纠偏合同：[`docs/2026.8.15/UE_ALIGNED_CLIENT_PREDICTION_PLAN.md`](../../../docs/2026.8.15/UE_ALIGNED_CLIENT_PREDICTION_PLAN.md)。

### 相关文件

- `Assets/Scripts/Domain/Simulation/*`
- `Assets/Scripts/App/Controllers/Gameplay/SimulationHost.cs`
- `Assets/Scripts/App/Controllers/Combat/CombatWorldController.cs`
- `Assets/Scripts/Domain/Character/Presentation/CharacterPresentationBridge.cs`
- `Assets/Tests/EditMode/Simulation/*`

---

## 组队 PVE · NS0 LocalPlayer

### 功能说明

场景不再假设全场只有一个 `PlayerController`；相机、HUD、刷怪与敌人感知通过花名册查询本机玩家或全部玩家根。

### 实现方案

| 项 | 方案 |
|----|------|
| 本机入口 | `ILocalPlayer`：客机 `PlayerController` 是预测输入/相机拥有者，权威侧 `RemotePlayerSeat` 表示每个连接的阵容 |
| 登记 | `LocalPlayerService`（Architecture System）；预测座位不进入敌人感知根列表，权威 `RemotePlayerSeat` 进入 |
| 查询 | `GetLocalPlayerQuery` / `GetPlayerRootsQuery` |
| 仇恨 | `EnemyPerception` 在玩家根列表中取水平最近；`RemotePlayerSeat` 暴露稳定感知锚点，换人时锚点重挂到当前 Active 槽位根 |
| 禁止 | 玩法 `FindObjectOfType<PlayerController>()`（仅 Editor Gizmo） |

### 关键参数

无新 SerializeField 必填项。`EnemySpawnController.target` / `EnemyController.target` 为空即走花名册。

### 运行时流程

```
Client PlayerController.Awake → LocalPlayerService.Register(this, isLocalOwner: true)
Authority ActGameSessionHandler.TryCreateGuest → Register(RemotePlayerSeat)
ActGameGuest.TryResolveSwitch → RemotePlayerSeat.Bind(to.Actor, to.Root)
CameraManager / HUD → GetLocalPlayerQuery
EnemyPerception.Capture → GetPlayerRootsQuery → 最近根
```

### 已知限制

- 2026-08-18 Unity 编译、Test Runner 与双进程 Play 已确认
- 客机预测座位不进入敌人权威感知；Dedicated/Host 由每连接 `RemotePlayerSeat` 提供当前 Active 槽感知根

### 相关文件

- `Assets/Scripts/Domain/Character/ILocalPlayer.cs`
- `Assets/Scripts/App/Systems/Player/LocalPlayerService.cs`
- `Assets/Scripts/App/Queries/Player/GetLocalPlayerQuery.cs`
- `Assets/Scripts/App/Queries/Player/GetPlayerRootsQuery.cs`
- `Assets/Scripts/Domain/Enemy/EnemyPerception.cs`
- `Assets/Tests/Editor/Enemy/EnemyPerceptionTests.cs`

---

## 组队 PVE · NS1 复制快照

### 功能说明

权威世界把完整角色状态拆为 V2 Lifecycle 与 Snapshot；Spawn/Despawn 可靠有序，Update 不可靠并以 Lifecycle Sequence 为屏障。

### 实现方案

| 项 | 方案 |
|----|------|
| 下行 | `ReplicationLifecycle` + `ReplicationSnapshot` + `ReplicationEvent`；角色线格式委托 `CharacterSnapshotSchemaV2` |
| 上行 | `ClientCommand`（frameHint + playerId + `InputFrame`） |
| 组装 | `ReplicationSnapshotBuilder.FromAuthority`（Motor + Action 快照 + 传入 healthMilli） |
| 字节 | `ActorReplicationSnapshotCodec` 是角色字段布局唯一真源；`ReplicationProtocolV2Codec` 编码 Lifecycle/Snapshot，`RoomCodec` 仅保留上行命令 |
| W3/W4 业务适配 | `ActCharacterSnapshotSchema` 接入生产 Schema Registry 并复用纯 C# V2 线格式；`GameContentCatalog` 持有冻结 Archetype、配置与动作 Catalog |
| W4 权威适配 | `ActAuthorityReplicationAdapter` 独占远端输入灌入、Gameplay Actor Capture 与 FrameHits ActionId 映射；RoomHost 只调度并构建/发送 Frame |
| W4 加入适配 | `ActGameSessionHandler` 创建/销毁 Guest Authority Actor；RoomHost 注入 App 注册委托并独占 `ServerSession.Accept/Reject` |
| W4 Owner 适配 | `ActOwnerReplicationAdapter` 独占 Owner HP、Action Ack、Locomotion Reconcile、Hit/Death 硬吸和预测历史；Client Room 只转发快照 |
| W4 Observer 适配 | `ActObserverReplicationAdapter` 独占 Schema/Archetype 校验、Proxy Spawn/Update/Despawn、TargetSystem 与 View 生命周期；`ActRemoteProxyFactory` 是唯一装配入口 |
| W4/CS5 内容真源 | `GameContentBootstrap.ValidateAndBuild` 一次构建 `GameContentCatalog`；唯一持有 GameplayIntent、Action Catalog、Character Archetype 与 Unity 配置映射，Room/Adapter 只查询 |
| W4 Capture 真源 | `ActCharacterSnapshotSchema.Capture` 统一 CharacterActor → Snapshot 与 V2 编解码；独立 `CharacterReplicationCapture` 已删除 |
| W4 Room 边界 | `ListenServerBootstrap` / `ReplicationRoomClient` 仅做组合或 Session 调度与 HUD；不再引用 Character/配置/Proxy/Hit Cue 具体类型 |
| W4 Gameplay Service | `DedicatedAuthorityWorld` 只组合 Authority 三协调器；`ActClientRoomGameplay` 只组合 `OwnerPredictionCoordinator` / `ObserverReplicationCoordinator` / `ReplicatedFeedbackCoordinator`；内容由 Composition Root 注入 |
| 通用身份 | `NetConnectionId` / `NetPlayerId` / `NetEntityId` / `NetArchetypeId`；`SimActorNetIdAdapter` 显式映射 Simulation Actor |
| 版本基础 | `NetworkProtocolVersion` + 128 位 `ContentFingerprint` 已定义；握手切换留在 Content Manifest Wave |
| 传输 | `INetTransport` / `LoopbackTransport` / `UdpTransport`（`ACTNet.Transport`，按 ConnectionId 定向） |

### 关键参数

Loopback `LatencyMs` 默认 0。`actionId` 由 `ActionReplicationCatalog` 按资产名稳定哈希。

### 运行时流程

```
ActAuthorityReplicationAdapter
  → RoomRemoteInputMerge → InputFrameBuffer
  → ActCharacterSnapshotSchema.Capture/Encode → ReplicationEntityState full set
  → ActReplicationSnapshotMeta + ReplicatedHitEvent
  → ReplicationServer.PrepareTickDelta → ReplicationProtocolV2Codec
  → Session/Transport → ReplicationClient.ApplyLifecycle/ApplySnapshot
  → ActClientRoomGameplay → Owner reconcile / Observer Proxy / ACT 表现
```

### 已知限制

- 水平速度 P0 可为 0；空闲相位由 Capture 填 `AnimationKey`
- NS5 已单轨切到 `UdpTransport`；`LoopbackTransport` 支持一服多客和确定性延迟，不再挂 Host 预览
- W0～W4、GF0～GF4 与 M1 已于 2026-08-18 完成 Test Runner、架构守卫和双进程回归
- W5 Dedicated Bootstrap 已于 2026-08-19 用户验收
- W6 Headless Authority / Content Fingerprint 已于 2026-08-19 用户验收
- W7 Match / 每连接 Replication 已于 2026-08-19 用户验收
- W8 Dedicated 启动覆盖 / READY / 出包已于 2026-08-19 用户验收；M2 关闭

### 相关文件

- `Assets/Scripts/Domain/Simulation/Replication/*`
- `Assets/Scripts/Domain/Networking/*`
- `Assets/Scripts/Framework/ACTNet/Core/*`
- `Assets/Scripts/Domain/Networking/Identity/SimActorNetIdAdapter.cs`
- `Assets/Scripts/Framework/ACTNet/Transport/*`
- `Assets/Tests/EditMode/Simulation/ActorReplicationSnapshotTests.cs`
- `Assets/Tests/EditMode/ACTNet/Transport/LoopbackTransportTests.cs`

---

## 组队 PVE · NS2 RemoteProxy

### 功能说明

客机用 `RemoteCharacterProxy` 播他人与敌人：只跟 V2 Snapshot 的 Character 记录，不跑第二份命中。Host 同机 ±2m 预览已删除。

### 实现方案

| 项 | 方案 |
|----|------|
| 捕获 | `ActCharacterSnapshotSchema.Capture` + `GameContentCatalog.Actions.RequireId` |
| 传输 | 房间 `UdpTransport`；Loopback 仅单测 |
| 应用 | `RemoteCharacterProxy`：位姿写 Motor；招式/特殊 Locomotion 跟 `RemotePlaybackClock`；Idle 无新快照时按渲染时钟维持纯表现循环；关掉 Animator RM |
| 朝向调试 | 客机幽灵挂同一套黄/品红箭；wish 走快照 `moveV*`，与延迟位姿成对 |
| 插值 | 复用 `CharacterPresentationBridge.Render(alpha)` |
| 装配 | `ActRemoteProxyFactory`，**不**走 `CharacterActorFactory` |
| 入口 | `LocalClientRuntime` 分发 Lifecycle/Snapshot 给 `ActClientRoomGameplay`，由 `ObserverReplicationCoordinator` 原子提交 Meta、Owner 与 Observer |

### 关键参数

无 Host 预览 SerializeField。房间延迟即 UDP RTT，不另加 Loopback。

### 运行时流程

```
Host.Step → AfterLogicStep
  → AuthorityReplicationPublisher → ReplicationServer.PrepareTickDelta → UDP
客机 Pump → ObserverReplicationCoordinator → RemoteProxy.ApplySnapshot
LateUpdate → ObserverReplicationCoordinator.Render → RemotePlaybackClock
```

### 已知限制

- PivotTurn 根朝向仍只跟快照 facing（不在幽灵侧重跑 AnimAuth）；Clip 已按权威归一化时间 Seek
- Catalog 已改为资产名稳定 Id（NS5）
- Observer 正常 Action→Locomotion（含移动取消）使用 AnimationProfile 默认 CrossFade；只有播放头吸附或 Locomotion 特殊相位间切换才硬切
- 幽灵不进权威花名册、无 Hurtbox Collect；同一动作按前后 ActionFrame 补齐 VFX/SFX，首次看到新动作时从 frame -1 补到当前帧，避免弹刀成功等起手特效因首包已到 frame 1+ 而永久漏播；同动作重启仍只跨当前帧。本机 `CharacterActor` 与远端 Proxy 在阵容成员隐藏前都由 `IActionVisibilityResetConsumer` 回收仍挂在角色下的 VFX；远端重新显形时另清空旧 `SnapshotTimeline` 并重置插值双端
- 多种敌人通过 `NetArchetypeId` 精确解析各自配置；未知 Archetype 明确拒绝，不做首敌回退

### 相关文件

- `Assets/Scripts/Domain/Character/Replication/*`
- `Assets/Scripts/App/Presentation/RemoteCharacterProxy.cs`
- `Assets/Scripts/App/Networking/Services/ActClientRoomGameplay.cs`
- `Assets/Scripts/App/Controllers/Gameplay/SimulationHost.cs`
- `Assets/Tests/EditMode/Simulation/ReplicationPoseApplierTests.cs`
- `Assets/Tests/Editor/Replication/ActionReplicationCatalogTests.cs`
- `Assets/Tests/Editor/Replication/RemoteCharacterProxyTests.cs`
- `Assets/Tests/Editor/Replication/ReplicationPresentationAlignTests.cs`

---

## 组队 PVE · NS3 预测位移

### 功能说明

Listen 本机与远端客机都用本地 `InputFrame` 立刻推进位移。**房间走跑由 Autonomous `CharacterActor.Step` 写 MotorSim**；`Predict`/`ApplyInput` 仅留单测。历史和解由 `PredictionCoordinator` 编排，角色侧 Restore+Replay 端口为 `CharacterPredictionRuntime`，2m Gate 在 `ActCharacterPredictionModel`。已删除 Runner / 猜片 / Host 同机预览。

### 实现方案

| 项 | 方案 |
|----|------|
| 走跑步进 | Autonomous `CharacterActor.Step`（正常预测）；`CharacterPredictionRuntime.ReplayTick`（纠偏重放，同一套内层机） |
| 互撞 | 不进 World；`AutonomousSoftBodySolver` 把本机从只读幽灵圆盘推出 |
| 出招位移 | 表现桥烘焙 + TargetAdhesion / Relocate（WorldQuery 读只读 Proxy Pose） |
| 缓存 | `PredictionCoordinator.Record` → CommandHistory + StateHistory |
| 和解 | 模型算策略，Coordinator 执行 Ack / Restore / Replay。≤ 2m 只 Ack；无 replay：≤ 50mm；刚吸附 8 包内 ≤ 150mm 也只 Ack |
| Listen / Client | 场景 `PlayerController` 为 Autonomous；权威玩家在 Headless Guest |

### 关键参数

| 参数 | 默认 | 说明 |
|------|------|------|
| 走/跑/冲刺 | 4000 / 7000 / 9000 mm/s | 与 Motor 默认一致；纠偏阈用毫米 |
| `rotationSmoothTimeSeconds` | 0.2 | 与 `CharacterMotorConfig` FollowInput 同参 |
| `runThresholdMilli` | 600 | 输入幅度 0.6 |
| `reconcileThresholdMm` | 50 | 仅无 replay / 单测；房间走跑不用 |
| `AutonomousHardSnapMm` | 2000 | 走跑+Runner 默认硬吸阈；50mm 每包重放会卡顿 |
| `SnapGraceMaxErrorMm` / 宽限包数 | 150 / 8 | 吸附后避免立刻连吸 |

### 运行时流程

```
本机 InputFrame
  → 走跑：Runner.Tick + RecordAutonomous
  → 出招/受击：Runner.Exit + PredictAligned
权威 Tick
  → Reconcile：走跑 ≤2m 或宽限内 Ack / 超 2m RestoreFromAuthority + ReplayTick
  → 仅走跑 Snapped 或 Hit/Death 后 SnapPresentationToSimulation
  → 出招/闪避：只 Exit Runner，位姿由 AfterLogicStep ApplySnapshot 插值
表现：走跑 SyncAutonomousLocomotion；出招 ApplySnapshot
```

### 已知限制

- Lean 不进 Snapshot，仅本机从 Actor 倾身模型推进
- 客机本机对他人/敌人幽灵做只读软弹开（幽灵不可推动）；不进 `SimulationWorld`
- 出招预测见下一节 NS4 / 方案 UE4
- 客机相机绕圈依赖 `HasMoveIntent`（设备采样），不得读空的 `ILocalPlayer.Input`
- 客机出招/闪避由只读 ActionSim 本机起手；位移仍跟快照插值（不跑 ActionMotionResolver）

### 相关文件

- `Assets/Scripts/Framework/ACTNet/Prediction/*`
- `Assets/Scripts/Domain/Simulation/Prediction/*`
- `Assets/Scripts/Domain/Character/Prediction/CharacterPredictionRuntime.cs`
- `Assets/Scripts/App/Networking/Services/ActClientRoomGameplay.cs`
- `Assets/Tests/EditMode/ACTNet/Prediction/FakeLinearEntityPredictionTests.cs`
- `Assets/Tests/EditMode/Simulation/PredictedLocomotionReconcileTests.cs`

---

## 组队 PVE · NS4 出招预测与权威命中

### 功能说明

本地预测出招只播 Clip；伤害、硬直、HP 只认权威 `CombatHitPipeline`。客机他人/敌人 `RemoteCharacterProxy` 跟延迟 Snapshot，受击只出现一次。

### 实现方案

| 项 | 方案 |
|----|------|
| 出招预测 | Autonomous `CharacterActor` + `ActionSim` + 表现桥；Ack 用 `PredictedActionAckQueue` |
| 取消 | 该帧权威 ActionId=0 时走 `CharacterPredictionRuntime.StopAutonomousAction`；Vitality Hit/Death 仍走 Actor 反应入口；连招超前不 Cancel |
| 表现所有权 | 本机 Clip 由 Actor 桥推进；禁止对自 `ApplySnapshot`；仅受击走 `EnterHit` |
| 卡肉 | 客机 `PredictedHitStopConsumer` 几何重叠后 `RequestHitStop`；禁止用延迟权威 Freeze 再拖时钟。伤害只信权威下行 |
| 跟招 | 已删除 `FollowAuthorityAction`；本机 Clip 只跟本地 ActionSim |
| 命中下行 | 本帧 `ReplicatedHitEvent` 走 `ActRoomMessageType.ReplicationEvent` 可靠通道；Snapshot Meta 不带 hits。`VitalityReplicationEdge` 仍在角色快照 |
| 他人/敌人 | `RemoteCharacterProxy` 跟 `SnapshotTimeline` 延迟取样；`VitalityEdge.Hit` 或动作帧回绕时硬切重播受击 |
| Listen | 本机也走 Owner 预测；`HitboxFrameConsumer` 只挂权威工厂 |

### 关键参数

无 Host 预览参数。卡肉与 Ack 见客机 `PredictedHitStopConsumer` / `PredictedActionAckQueue`。

### 运行时流程

```
权威 Step → Pipeline.Collect/Resolve → Vitality 边沿
AfterLogicStep → Capture 全员 → Snapshot UDP；本帧 CopyHits → FlushEvents
客机：本机 Actor.Step + Ack；他人/敌人 Timeline 取样 + RemoteProxy
```

### 已知限制

- 客机本机跑 ActionSim；仍不 Collect、不写 Numeric
- Clip 与 VFX/SFX 由本机 `CharacterActionPresentationBridge` 派发
- 受击火花走复制落点 + Hitbox Feedback
- 本机招打完后不得再用延迟快照重播同一招的 Clip/VFX
- Relocate/Adhesion 客机读只读 Proxy 逻辑 Pose；不 Collect

### 相关文件

- `Assets/Scripts/Domain/Character/CharacterActor.cs`
- `Assets/Scripts/Domain/Simulation/Prediction/PredictedActionAckQueue.cs`
- `Assets/Scripts/App/Networking/Services/ActClientRoomGameplay.cs`
- `Assets/Tests/EditMode/Simulation/PredictedActionReconcileTests.cs`

---

## 组队 PVE · NS5 最小 2 人房间

### 功能说明

Listen 与 Dedicated 共用 `DedicatedServerRuntime`。Listen 另加本机 `LocalClientRuntime`（127.0.0.1 UDP）；房主场景座位是 Autonomous，权威玩家只在 Guest 座位。客机预测自己、用 RemoteProxy 看队友与敌人；敌人与命中只在权威世界。

### 实现方案

| 项 | 方案 |
|----|------|
| 角色 | 默认 Listen Host；ParrelSync 克隆自动 Client；菜单可切 Dedicated（`Use Dedicated Server`） |
| 传输 / Session | `UdpTransport` 按 `NetConnectionId` 定向收发；`ServerSession/ClientSession` 独占信封、Join、Heartbeat、Kick，`RoomCodec` 只编 ACT 应用正文 |
| Listen | `ListenServerBootstrap` = `DedicatedServerRuntime` + `LocalClientRuntime`；本机也走 Command / Snapshot / ACK |
| Dedicated | `DedicatedServerBootstrap` → 同一 `DedicatedServerRuntime`；Runtime 只管 Session/Match/Poll/Flush，角色状态解释委托 `DedicatedAuthorityWorld`；JoinAccept 无房主实体 |
| Client | 薄 `ReplicationRoomClient` 驱动 `LocalClientRuntime`；`ActClientRoomGameplay` 只组合 Owner Prediction、Observer Replication、Replicated Feedback 三个协调器 |
| App 装配 | Controller 禁止运行时 Scene Find；战斗入口取 `CombatWorldController` 生命周期锚点，相机取同物体/Root 子树，调试目标取 `TargetSystem + SimulationHost` 注册表 |
| 动作 Id | `ActionReplicationCatalog` 按资产名稳定哈希，两端 Prefill Graph 节点、`VariantResolver` 变体与反应 |
| 掉线 | `ServerSession.ConnectionRegistry` 按连接记录活动时刻；10s 超时仅 Kick 对应连接 |
| HUD | F3 Room 行：角色 / 状态 / authorityFrame / RTT / jitter；Net 行追加 Tick/Command 字节、Proxy、pending、loss‰、delay、snap、replay |

### 关键参数

| 参数 | 默认 | 说明 |
|------|------|------|
| `listenPort` | 7777 | Host 绑定 / Client 连接 |
| `contentVersion` | 1 | 双方必须一致，否则拒收 |
| 空闲超时 | 10000ms | 定案：待机 10s 后剔除 |
| 迟到窗口 | 8 逻辑帧 | 更旧的 FrameHint 丢弃（不与权威帧比较） |
| 输入冗余 | 3 条/包 | 最近 FrameHint 重发；Host 跳过已应用 Hint |

### 运行时流程

完整往返（入房、每帧序、客机攻击、线格式）见 [`docs/2026.8.23/NETSYNC_FROM_JOIN_TO_HIT.md`](../../../docs/2026.8.23/NETSYNC_FROM_JOIN_TO_HIT.md)。

```
Listen：LocalClient Poll/采样 → 按 PeekAdvanceSteps 发命令预测 → DedicatedServerRuntime.Poll → LocalClient 再 Drain 同拍快照
Client：ReplicationRoomClient Poll → LocalClient 采样 → 逻辑步构命令发送 → Actor.Step → 收帧 Restore+Replay/Proxy
```

### 已知限制

- 客机连招下一段在本机 Cancel 窗起手；权威未起手则 Stop
- 客机 CameraLock：Proxy 只读进 TargetSystem，范围内自动选中后可开；2026-08-18 双进程 Play 已验收
- 多种敌人按稳定 Archetype 精确生成对应幽灵，不使用首敌配置回退
- Dedicated Editor Play 与玩家 Dedicated Build 均已验收（M2）
- Listen 本机从场景出生点会被首帧 Snapshot 吸到 Match 槽位（槽位 × 2000mm）
- 同进程 TargetSystem 可能同时看到 Headless 权威 Hurtbox 与 Observer Proxy；感知根已排除预测座位
- 未做匹配、排位、Host 迁移
- UDP 仍不可靠；冗余 3 条降低丢边沿，不能保证 0 丢包
- 客机刀光/音效由本机表现桥按预测帧派发；跟权威卡肉招时禁止重派点事件
- UE2：走跑超阈 Restore+Replay；烘焙 Stop/Pivot 游标用归一化时间近似，未加 `locomotionMotionFrame`
- 客机闪避由 Actor ActionSim + Directional 本机起手；结束时按 `SprintAfterDodge` 接片；烘焙位移会跑，Relocate 不跑

### 相关文件

- `Assets/Scripts/Domain/Character/Replication/ReplicationSeat.cs`
- `Assets/Scripts/Domain/Character/Replication/LocomotionSavedState.cs`
- `Assets/Scripts/Domain/Simulation/Prediction/IPredictedLocomotionReplay.cs`
- `Assets/Scripts/Framework/ACTNet/Transport/UdpTransport.cs`
- `Assets/Scripts/Domain/Simulation/Replication/RoomCodec.cs`
- `Assets/Scripts/Domain/Simulation/Replication/RoomRemoteInputMerge.cs`
- `Assets/Scripts/Domain/Character/Replication/ReplicationPresentationAlign.cs`
- `Assets/Scripts/App/Controllers/Gameplay/ListenServerBootstrap.cs`
- `Assets/Scripts/App/Controllers/Gameplay/ReplicationRoomClient.cs`
- `Assets/Scripts/App/Networking/Services/LocalClientRuntime.cs`
- `Assets/Scripts/App/Networking/Services/ActClientRoomGameplay.cs`
- `Assets/Scripts/App/Networking/Content/GameContentBootstrap.cs`
- `Assets/Scripts/App/Networking/Content/GameContentCatalog.cs`
- `Assets/Scripts/App/Presentation/HitImpactCuePlayer.cs`
- `Assets/Scripts/Editor/Net/ReplicationRoomMenu.cs`
- `Assets/Tests/EditMode/Simulation/RoomCodecTests.cs`
- `Assets/Tests/EditMode/ACTNet/Session/SessionIntegrationTests.cs`
- `Assets/Tests/EditMode/ACTNet/Transport/UdpTransportTests.cs`
- `docs/2026.8.23/NETSYNC_FROM_JOIN_TO_HIT.md`

---

## 组队 PVE · W5 Dedicated Bootstrap

### 功能说明

无本地玩家的 Dedicated 进程可 Listening 并 Accept 远端玩家；身份与出生由 Match 分配，不再依赖房主 Actor。

### 实现方案

| 项 | 方案 |
|----|------|
| 进程角色 | `NetProcessRole.DedicatedServer`；`ReplicationRole.DedicatedServer` 仅作场景入口枚举 |
| 程序集 | `ACTGame.Server`：不引用 PlayerController / InputReader / Camera / HUD / Room Facade |
| 启动 | `CombatWorldController` 只 `EnsureDedicatedBootstrap()`；先 `ServerLaunchConfigResolver` 再 `TryStart` |
| 退出码 | `ServerExitCode.ConfigFailed=10`、`BindFailed=20`；玩家构建 `Application.Quit` |
| 身份 | `MatchCoordinator` 分配 PlayerId / EntityId / Team / Spawn（槽位 × 2000mm X） |
| 每连接 | `DedicatedPlayerRuntime` 持 Hint ACK；`AuthorityReplicationPublisher` 按连接持有 `ReplicationServer` 与 Prepare/Commit 票据 |
| JoinAccept | `AuthorityEntityId` 可为 Invalid；线格式 0 |

### 关键参数

| 参数 | 默认 | 说明 |
|------|------|------|
| `MaxRemotePlayers` | 4 | Listen 本机也占一席；Dedicated / Listen 同一容量 |
| `firstPlayerId` | 1 | 不再预留 Guest=2 |
| 出生间距 | 2000mm | X 轴；不读 Host Root |

### 运行时流程

```
CombatWorldController.Awake（Dedicated）
  → DedicatedServerBootstrap.Configure
  → DedicatedServerRuntime.TryStart(UdpTransport, ServerLaunchConfig)
  → Update：Poll Session → Match Accept → 每连接 ACK
```

### 已知限制

- W5 当时不步进 World；W6 已步进，W7 已下发 Frame
- 特殊 Listen Host Room 已删；权威只走 `DedicatedServerRuntime`

### 相关文件

- `Assets/Scripts/App/Server/DedicatedServerRuntime.cs`
- `Assets/Scripts/App/Server/DedicatedServerBootstrap.cs`
- `Assets/Scripts/App/Server/MatchCoordinator.cs`
- `Assets/Tests/EditMode/ACTGame/Server/DedicatedServerRuntimeTests.cs`

---

## 组队 PVE · W7 Dedicated Match / Replication

### 功能说明

无本地玩家的 Dedicated 进入 Playing 后，按连接下发与 Listen 相同的 V2 Lifecycle/Snapshot/Event；Owner 预测、Observer Proxy 复用 W4 Client Adapter。对局可结束并回到 Lobby；玩家构建在 `ExitOnMatchEnd` 时接着退出进程。

### 实现方案

| 项 | 方案 |
|----|------|
| Match | `DedicatedMatchPhase`：Lobby → Starting → Playing → Ending → Cleanup → Lobby |
| Join | Playing 可晚加入；Ending 之后拒收 |
| 实体 Id | JoinAccept 写 World `SimulationId`，供 Client `CanPredict` 对齐 |
| 命令 | 只灌本连接 PlayerId；冗余批合并进下一权威帧；下行 appliedHint=本批第一条 Hint |
| 构帧 | `AfterLogicStep` Capture + 每连接 `ReplicationServer.PrepareTickDelta`；Runtime 发送成功后 Commit |
| 命中 | 本帧事件走 `EventReliableOrdered`；Client `SimHitKey` 去重只播一次 |
| 结束 | `ActRoomMessageType.MatchEnd=8` + Kick；Client 先 Drain 再 Sync Session |

### 关键参数

| 参数 | 默认 | 说明 |
|------|------|------|
| MatchEnd 类型 | 8 | 避开 Session Kick=7 |
| ReplicationEvent 类型 | 9 | 可靠命中事件包 |

### 运行时流程

```
DedicatedServerRuntime.Poll
  → DrainJoins / DrainCommands（Merge 进下一权威帧）
  → Advance → StepOnce → AfterLogicStep Capture/构帧（appliedHint=FirstAppliedHint）
  → FlushReplication + FlushEvents
  → 空房或 RequestMatchEnd → MatchEnd + Kick → Lobby
```

### 已知限制

- Dedicated Build 与 W10 100ms RTT / 20ms jitter / 5% loss Play 已验收；命中已改可靠事件单轨

### 相关文件

- `Assets/Scripts/App/Server/DedicatedServerRuntime.cs`
- `Assets/Scripts/App/Networking/Services/DedicatedAuthorityWorld.cs`
- `Assets/Scripts/App/Networking/Services/AuthorityGuestRegistry.cs`
- `Assets/Scripts/App/Networking/Services/AuthorityStepCoordinator.cs`
- `Assets/Scripts/App/Networking/Services/AuthorityReplicationPublisher.cs`
- `Assets/Scripts/Domain/Simulation/Replication/RoomCodec.cs`
- `Assets/Tests/EditMode/ACTGame/Server/DedicatedServerRuntimeTests.cs`

---

## 组队 PVE · W8 Dedicated 启动与进程生命周期

### 功能说明

Dedicated 进程按 CLI / 环境变量 / 配置文件覆盖监听与生命周期；监听成功打 READY；空 Lobby 超时或对局结束后可退出。Editor Play 不退出 Unity，并保持回 Lobby 再入房。

### 实现方案

| 项 | 方案 |
|----|------|
| 覆盖 | `ServerLaunchConfigResolver`：CLI > Env > File > Default |
| 空房 | `EmptyLobbyTimeoutMs`；仅从未有人加入的 Lobby；0=不超时 |
| 对局结束 | `ExitOnMatchEnd` 时 EmptyRoom / Completed 后 `ShouldExit` |
| Ready | `IsReady`；日志 `READY port=… role=DedicatedServer` |
| Editor | `CombatWorldController` 强制超时 0 且不退出进程 |
| 玩家构建 | 默认 `ExitOnMatchEnd=true`；Bootstrap `Application.Quit` |

### 关键参数

| 参数 | 默认 | 说明 |
|------|------|------|
| `EmptyLobbyTimeoutMs` | 0 | 无人到访才计时 |
| `ExitOnMatchEnd` | Editor false / 玩家构建 true | Editor 不可被 CLI 打开 |
| 退出码 | 0 / 10 / 20 | 正常 / 配置 / 绑定 |

### 运行时流程

```
CreateDefault → TryResolve → Bootstrap.TryStart → READY
Poll → 空房超时或 ExitOnMatchEnd → ShouldExit
玩家构建 Application.Quit(0)；Editor 只 Dispose
```

### 已知限制

- CI 自动出包、脚本化双 Client MatchEnd 烟测后置，不挡 M2
- 内容指纹仍由场景扫描；CLI 改 `contentVersion` 后会重算指纹
- Listen 已改为同一 `DedicatedServerRuntime` + `LocalClientRuntime`（W9 用户验收 2026-08-20）

### 相关文件

- `Assets/Scripts/App/Server/ServerLaunchConfigResolver.cs`
- `Assets/Scripts/App/Server/DedicatedServerRuntime.cs`
- `Assets/Scripts/App/Server/DedicatedServerBootstrap.cs`
- `Assets/Tests/EditMode/ACTGame/Server/ServerLaunchConfigResolverTests.cs`
- `docs/2026.8.19/DEDICATED_SERVER_LAUNCH.md`

---

## 组队 PVE · W9 Listen 组合收敛

### 功能说明

Listen 不再有特殊 Host 本机玩家。本机进程组合同一 `DedicatedServerRuntime` 与 `LocalClientRuntime`；房主在 Server 是 Authority Guest，在本机是 Owner/Presentation。

### 实现方案

| 项 | 方案 |
|----|------|
| 组合 | `ListenServerBootstrap` 拥有 Runtime + LocalClient；不挂 `DedicatedServerBootstrap` |
| 回环 | 本机 `ClientSession` 连 `127.0.0.1:实际绑定端口` |
| 帧序 | Poll/采样 → 按 `PeekAdvanceSteps` 发命令预测 → `Server.Poll` → 再 Drain |
| 座位 | `PlayerController` Listen/Client 只装 Autonomous；Dedicated 禁用 |
| 敌人 | Listen / Dedicated 权威 `AuthorityHeadless`；可见体走 Observer。Listen 播放头 delay=1（禁止 delay=0 贴死最新快照）；Clip 只跟采样 to；走跑 Urgent 每 Tick；出招 Tick 走片 |
| Capture | 只拍 Guest + 敌人，不再拍场景 LocalPlayer |

### 关键参数

| 参数 | 默认 | 说明 |
|------|------|------|
| `MaxRemotePlayers` | 4 | 含本机 Join |
| `ExitOnMatchEnd` | false | Listen 不因对局结束退 Editor |
| Bootstrap 执行序 | -210 | 先于 `SimulationHost` -100 |

### 运行时流程

```
CombatWorldController.Awake（ListenHost）
  → DedicatedAuthorityWorld + ListenServerBootstrap.Configure
  → TryStart DedicatedServerRuntime
  → Start：LocalClient 连 127.0.0.1
Update：PollAndApply → SampleRenderInput → 按 PeekAdvanceSteps 发命令预测 → Server.Poll → PollAndApply
```

### 已知限制

- 本机预测必须按权威步数，禁止每个渲染帧 `StepPrediction`（否则连段加速、移动被快照拉回）
- 同进程 TargetSystem 可能同时登记 Headless Hurtbox 与 Observer Proxy
- 本机出生先被 Snapshot 吸到 Match 槽位

### 相关文件

- `Assets/Scripts/App/Controllers/Gameplay/ListenServerBootstrap.cs`
- `Assets/Scripts/App/Networking/Services/LocalClientRuntime.cs`
- `Assets/Scripts/App/Server/DedicatedServerRuntime.cs`

---

## 组队 PVE · W10 通用预测 / 可靠通道 / 网络时间

### 功能说明

预测算法骨架可复用；ACT 2m Gate / 连招 / Hit-Death 仍归业务层。Control/Event 可靠有序，命中不再用帧内 8 条冗余。远端 Proxy 保持稳定播放延迟并有限追赶。公网 Play 未验收。

### 实现方案

| 项 | 方案 |
|----|------|
| 通用协调 | `PredictionCoordinator` + Command/State History；不读 ActionId |
| ACT 策略 | `ActCharacterPredictionModel.ResolvePolicy` |
| 远端 | `SnapshotTimeline` 丢旧 Tick；`NetworkTimeEstimator` 只以 jitter + 发送间隔/余量算 delayTicks；`RemotePlaybackClock` 以 1.2 倍有限追赶 |
| 通道 | Session 包装 `ChannelMuxTransport`；Snapshot 名称保留但 packet-level 不丢旧，同 Tick batch 由 `ReplicationClient` 应用层判定 |
| 命中 | `ActReplicationEventCodec` + `EventReliableOrdered` |
| MTU | 默认 1400；超限拒绝 |

### 关键参数

| 参数 | 默认 | 说明 |
|------|------|------|
| `TransportMtuGate.DefaultMaxDatagramBytes` | 1400 | 含 9 字节通道头 |
| Mux 重传间隔 | 50ms | Control/Event |
| 插值延迟 | 远端：jitter + 2 个发送间隔 | 钳 16～150ms，至少 1 Tick；latest 已到达，不重复扣 RTT/2 |

### 运行时流程

```
Owner：Record → PeekError → ResolvePolicy → ReceiveAuthority
Observer：TryPush → ApplySnapshot(判定/Notify) → RemotePlaybackClock → PresentSampledPlayback + Render(alpha)
Hit：CopyHits(本帧) → FlushEvents → ApplyReplicationEvents → 去重播放
AssistParry：可靠吸收事件 → PartyActors 稳定 Id / pending → Owner NotifyAssistParryContact → 下一逻辑帧 AssistParrySuccess
```

### 已知限制

- W10 出口已于 2026-09-19 由用户完成 Play 验收
- 超 MTU 只拒绝不拆包（W11）
- 不得称公网可用

### 相关文件

- `Assets/Scripts/Framework/ACTNet/Prediction/*`
- `Assets/Scripts/Framework/ACTNet/Transport/ChannelMuxTransport.cs`
- `Assets/Scripts/Framework/ACTNet/Transport/TransportMtuGate.cs`
- `Assets/Scripts/Domain/Simulation/Prediction/ActCharacterPredictionModel.cs`
- `Assets/Scripts/Domain/Networking/ActReplicationEventCodec.cs`
- `Assets/Scripts/App/Server/DedicatedEventSend.cs`

---

## 组队 PVE · W11 Delta / Relevancy / FakeActionGame

### 功能说明

复制不再每连接每 Tick 全量 Update。未变实体跳过；敌人按 40m 兴趣裁剪；Update 有字节预算；Owner 优先刷新。Graph 节点线上改为稳定整数。FakeActionGame 证明框架可不引用 ACT Character。

### 实现方案

| 项 | 方案 |
|----|------|
| 未变跳过 | `ReplicationServer` 对比上次已发送 payload |
| 节拍 / 预算 | `ReplicationServer`：`Compact` 使用 2 Tick 普通节拍与连接级 1200B Update 预算；Owner/Urgent 优先，装不下者保持旧 baseline 后续补发；MaxSilence=30 |
| 兴趣 | `ReplicationInterest`：Owner/玩家 Always；敌人平面距离 |
| 恢复 | `Rejected` 按 500ms 冷却重发 `ReplicationRecover`；成功快照清冷却。协议闩不把后续 ForceFull 当失败 |
| 节点 | `GraphNodeKey.FromStableName`（FNV-1a） |
| 第二用例 | `Assets/Tests/EditMode/ACTNet/FakeActionGame/` |

### 关键参数

| 参数 | 默认 | 说明 |
|------|------|------|
| `SnapshotIntervalTicks` | 2 | 非 Owner 刷新间隔 |
| `MaxUpdateBytes` | 1200 | 仅约束 Update |
| `DefaultRadiusMm` | 40000 | 敌人兴趣半径 |
| `GraphNodeKey` | int32 | 空名=0 |

### 运行时流程

```
Capture → CopyRelevantStates → PrepareTickDelta
Rejected → ReplicationRecoveryPolicy 冷却内最多一次 Reset+Recover
  → 到期仍 Rejected 才再发；Applied Snapshot 清冷却
  → Server RequestFullRecovery → ForceFull Spawn + Meta
  → ApplySnapshot=Applied 即过 Meta 屏障，不看旧 RecoveryRequested 闩
ApplyUpdates → ApplySnapshot(立即写判定/受击/Notify，不切 Clip)
Observer.Render → RemotePlaybackClock（Listen delay=1）
  → SetPresentationBracket → PresentSampledPlayback（片子只跟采样 to）→ Render(alpha)
```

### 已知限制

- W10 已于 2026-09-19 用户验收；W11 V2 fixture 与预算实现已补且生成工程编译通过，仍待 Unity Test Runner / Editor Play 后关闭 R2
- 无字段级 change mask、无超 MTU 拆包
- `RoomCodec` 仍在 Simulation；现行边界允许 `ACTGame.App` Composition 组合 `ACTGame.Networking` 与 `ACTNet.*`，Gameplay Domain 不直接编排网络框架
- 远端隔步快照：播放头插值锚点并驱动 Clip delta；停头即停片、1.2 倍追赶时同步追赶，只在切招/切段 Seek；落到走跑必须再 Play；走跑/出招/受击 `Urgent` 每 Tick 下发；Notify 随快照到达立即派发
- 不得称 R2 完成或公网可用

### 相关文件

- `Assets/Scripts/Framework/ACTNet/Replication/ReplicationBuildOptions.cs`
- `Assets/Scripts/Framework/ACTNet/Replication/ReplicationInterest.cs`
- `Assets/Scripts/Framework/ACTNet/Prediction/RemotePlaybackClock.cs`
- `Assets/Scripts/App/Presentation/RemoteCharacterProxy.cs`
- `Assets/Scripts/Domain/Simulation/Replication/GraphNodeKey.cs`
- `docs/2026.8.23/NETSYNC_FROM_JOIN_TO_HIT.md`

---

## 1. 第三人称移动

### 功能说明

玩家通过 WASD 相对**相机朝向**移动；摇杆/键盘输入幅度影响移动速度；角色平滑转向移动方向；含简易重力与贴地。

### 实现方案

| 项 | 方案 |
|----|------|
| 碰撞体 | `CharacterController`（非 Rigidbody） |
| 位移执行 | `LocomotionStateMachine` 各相位 → `CharacterMotor.ApplyLocomotion` |
| 方向计算 | InputFrame 本地 Vector2 + `MoveReferenceYawQuantized` → 世界 XZ wish；Motor 不读 Camera Transform |
| 速度 | `moveInputMagnitude × speed`；幅度 > `runThreshold` 用 `runSpeed`，否则 `walkSpeed` |
| 旋转 | `SmoothDampAngle` 显式传入固定 `1/60s`，绕 Y 轴对齐移动方向 |
| 重力 | `CharacterMotorSim.TickVertical`（mm/s² ÷ logicHz）；着地钳 `GroundYMm` |

### 关键参数（Prefab 默认）

| 字段 | 默认值 | 含义 |
|------|--------|------|
| `walkSpeed` | 4 | 走速 |
| `runSpeed` | 7 | 跑速 |
| `runThreshold` | 0.6 | 输入幅度超过此值视为跑 |
| `rotationSmoothTime` | 0.12 | 转向平滑时间 |
| `gravity` | -20 | 重力加速度 |
| `groundedGravity` | -2 | 着地时 Y 速度 |

### 运行时流程

```
SimulationWorld.Step
  → InputFrameBuffer.ResolveLocal
  → InputManager.IngestFrame
  → ResolveWorldMoveDirection(localMove, MoveReferenceYawQuantized)
  → 有方向：SmoothDamp 旋转 + Move(水平)
  → ApplyGravity：Move(垂直)
```

### 对外暴露（供状态机）

- `MoveInputMagnitude`、`RunThreshold`、`IsGrounded` — 由当前 State 从 `CharacterMotor` 同步到 `CharacterContext`

### 已知限制

- Locomotion 水平移动由内层相位 State → `ApplyLocomotion` 拥有；重力仍由 `CharacterActor` 每帧统一推进
- 玩家 Orbit yaw 在渲染采样边界 staged 到下一 InputFrame；追帧/回放只读已记录 yaw
- AI `LocomotionDesire` 显式携带 reference yaw，不伪装玩家相机

### 相关文件

- `Assets/Scripts/App/Controllers/Gameplay/PlayerController.cs`
- `Assets/Prefabs/Player/Player_KatanaGirl.prefab`

---

## 2. 输入系统

### 功能说明

使用 Unity **Input System** 在玩家设备边界采样，立即量化为带逻辑帧与 SimActorId 的 `InputFrame`，再由 `GameplayIntentProducer` 转换为设备无关意图；AI 移动 / 出招走独立命令源。

### 实现方案

| 项 | 方案 |
|----|------|
| 资产 | `GameInputActions.inputactions` |
| 形态 | `InputReader` 实现 `ILocalInputSampler`；AI 通过 `IMoveIntentSource` / `IActionEntryRequestSource` 注入，不伪装设备 |
| 绑定 | Move 从 Player Map 读取并量化为 sbyte；Orbit yaw 量化为 MoveReferenceYaw；TargetSwitch 映射固定按钮；Look/CameraLock 仅供相机表现 |
| 生命周期 | OnEnable/OnDisable 启用/禁用整个 Asset |
| 输入历史 | `InputFrameBuffer` 按 `(frame, actorId)` 保存，硬上限 64 帧；多渲染样本边沿 OR、连续状态取最后值 |
| 追帧展开 | 缺少下一设备样本时只延续 Move/Held；Pressed/Released 不重复、不从 Held 推导 |
| 原始中枢 | `InputManager` 摄入量化帧，提供移动反解值与 Pressed/Held/Released bit 查询 |
| 语义生产 | `GameplayIntentProducer`：SprintAttack、DodgeAttack、PressedThenLong；Hold 按整数帧累计；`Parry` 按下边沿硬产出，不经 Profile |
| 语义缓冲 | `GameplayIntentBuffer`：当帧事件 + 整数帧 TTL 的 Action Cancel 缓冲 |
| 消费方 | `CharacterActionDriver` 消费动作意图；Locomotion 继续消费连续 Move 快照 |

### 绑定摘要

| Action | 类型 | 主要绑定 |
|--------|------|----------|
| Move | Vector2 | WASD 复合键；Gamepad 左 Stick |
| Look | Vector2 | 鼠标 Delta；Gamepad 右 Stick |
| Attack | Button | Pressed→Attack（Sprint 时 SprintAttack；Dodge Action 中为 DodgeAttack）；HoldReached→LongPressedAttack；Released→AttackRelease |
| Dodge | Button | Pressed→Dodge |
| TargetSwitchLeft / Right | Button | Pressed→InputFrame 固定 bit；Locomotion/Action 中均可切换 SelectedTarget |
| SwitchCharacter | Button | Pressed→座位协调器；从玩法 InputFrame 剥掉，不进 Producer |
| Parry | Button | Pressed→`GameplayIntentType.Parry`（Producer 硬产出）；不走切人、不耗支援点。资产名必须为 `Parry` |
| CameraLock | Button | 本地表现开关；无 SelectedTarget 时无效，不进入 InputFrame |

### 错误处理

固定路径缺少 `InputActionAsset` 时 Client Runtime Configuration 立即失败；缺少或无效 `GameplayIntentProfile` 时 Content Catalog 不冻结。Intent 规则纳入 Content Fingerprint 并显式注入 Actor Factory。木桩：Brain `enableCombatActions=false`。L0B 帧阈值：Intent 缓冲常见 60；EnemyBrainProfile 建议攻击冷却 72、失败重试 12、朝向刷新 6。

### 相关文件

- `Assets/Scripts/Domain/Simulation/Input/*`
- `Assets/Scripts/Infrastructure/Input/InputReader.cs`
- `Assets/Scripts/Domain/Input/GameplayIntent*.cs`
- `Assets/Scripts/Domain/Character/Commands/*`
- `Assets/Scripts/Input/GameInputActions.inputactions`

---

## 3. 角色状态机

### 功能说明

状态机驱动角色逻辑；`CharacterActor.Step` 保留固定 60Hz 世界契约入口，实际摄入输入与 Tick 当前 State 的顺序由 `CharacterSimulationPipeline` 独占。

### 实现方案

**Core 层（无 Unity 依赖）**

```
StateMachine<TStateId, TContext>
  RegisterState → Initialize(context, initial) → Tick / TryChangeState
```

- `StateBase` 默认 `CanTransitionTo`：仅允许转到**枚举值更大**的状态（Locomotion=10 → Action=60 → Hit=80 → Death=100）
- 同 ID 或转换被拒时 `TryChangeState` 返回 false

**Character 层**

- `CharacterStateMachine` 是纯 C# 宿主：构造时组装 `CharacterContext`，注册 State，初始 `Locomotion`
- 每次 `CharacterActor.Step` 转交 `CharacterSimulationPipeline.Step`，Pipeline 调 `_machine.Tick(1/60f)`

**Player 层**

- `CharacterActor`：角色聚合根与 SimulationWorld 契约入口
- `CharacterSimulationPipeline`：采集输入、处理动作路由、推进重力，再 Tick `CharacterStateMachine`

### 已注册状态

| State | Id | Enter | Tick | Exit |
|-------|-----|-------|------|------|
| `LocomotionState` | 10 | `LocomotionStateMachine.Enter` | `LocomotionStateMachine.Tick` | `LocomotionStateMachine.Exit` |
| `ActionState` | 60 | `Animation.SetLocked(true)` | `ActionRotationDriver.Tick`；不重复推进 Action | Unlock + ResetPlaybackState |

### 运行时流程（玩家）

```
SimulationWorld.Step
  → CharacterActor.Step
  → CharacterSimulationPipeline.Step
  → InputFrameBuffer.ResolveLocal → InputManager.IngestFrame
  → GameplayIntentProducer.Step
  → CharacterActionDriver.ProcessGameplayInput
  → CharacterMotor.TickGravity
  → ActionSim.Step（若会话激活；每 World 帧唯一一次）
  → CharacterStateMachine.Tick
      → LocomotionState.Tick → LocomotionStateMachine（转换→ExecuteFrame）→ Motor + Animation
      → ActionState.Tick → ActionRotationDriver.Tick
```

### 相关文件

- `Assets/Scripts/Core/StateMachine/*`
- `Assets/Scripts/Domain/Character/StateMachine/*`
- `Assets/Scripts/Domain/Character/CharacterActor.cs`
- `Assets/Scripts/Domain/Character/CharacterSimulationPipeline.cs`

---

## 4. Locomotion 动画与相位

### 功能说明

顶层仍为 `Locomotion` 状态；内部由 `LocomotionStateMachine` 驱动 Idle / Start / Gait / PivotTurn / Stop。升档与 Pivot 许可由 **`LocomotionGaitPolicy`**（嵌在 Profile）求值；播片经 **`ILocomotionAnimResolver`**（Walk 横移可解析 `WalkLeft`/`WalkRight`）。敌我差异靠不同 Profile 资产，State 内无身份分支。

### 实现方案

| 项 | 方案 |
|----|------|
| 内层机 | `LocomotionStateMachine` + `LocomotionContext`；Tick = 转换后 `ExecuteFrame` |
| 唯一时钟 | `LocomotionContext.PhaseFrame`；切相位或切 AnimationKey 清零，每逻辑步末只推进一次 |
| 步态策略 | `LocomotionGaitPolicy`：MaxGait / AllowPivot / SprintAfterRunFrames |
| 选片 | `DefaultLocomotionAnimResolver`：gait + `MoveIntent` → `AnimationKey` |
| 相位 State | `Idle/Start/Gait/PivotTurn/StopLocomotionState` |
| 逻辑键 | Idle/Walk/WalkLeft/WalkRight/WalkStart/WalkStartLeft/WalkStartRight/Run/Sprint/Start/StartEnd/PivotTurn/StopL/StopR |
| 移动朝向 | Profile.`FacingMode`：`FollowMove`（玩家）/ `FaceCamera`（八向敌）；经 `ResolveMotorRotationMode` |
| 选片 | `LocomotionAnimSet`（Loop/Start×cardinal→Key）+ `DirectionModel` + Gait cardinal 滞回；Clip 在 AnimationProfile |
| 起步 | Start 闩 `ActiveStartGait`/`ActiveStartCardinal`；升档/降档不认 WalkStart* Key 族 |
| FaceTarget | 仅 Profile 声明时读 `LocomotionFacingTargetSource`（SelectedTarget）；玩家 FollowMove 不因自动选敌升格；Motor `FaceTarget`；选片 wish→本地；Pivot 关 |
| FollowInput 位移 | 沿**当前朝向**；朝向以 `CharacterConfig.RotationSmoothTime` 追 wish（单参控制 W→WD 转向时长） |
| 起步选片 | Walk 横向 → `WalkStartLeft/Right`（缺则 `WalkStart`→`Start`）；正向 `WalkStart`；Run → `Start` |
| 映射 | `CharacterLocomotionProfile` → `AnimationClip` |
| 相位参数 | `CharacterLocomotionProfile.clipTimings[]`：每 AnimationKey 明确 duration/loop/exit/handoff frames |
| 脚步 | `LocomotionFootCycle` 按 `PhaseFrame + durationFrames` 采样整数帧标记 |
| 门面 | `CharacterAnimationService.SampleLocomotion(AnimationKey, PhaseFrame, timing)` |
| Root Motion | StartEnd/Stop/Pivot 烘焙轨直接按 `PhaseFrame` 取 Δ，不维护第二游标 |
| Pivot handoff | `LocomotionClipTiming.HandoffFrame` |

### 相位规则（摘要）

```
Idle + 有输入                         → Start（必经）
Start 播完                            → Gait(Walk|Run)，受 MaxGait 钳制
Gait：Policy.Evaluate（跑输入累计）   → Run→Sprint（仅 MaxGait≥Sprint）
Gait 播片                             → AnimResolver（Walk+横向 → WalkLeft/Right）
Start 松输入                          → Stop（StartEnd / StopL/R）
Pivot：Policy.AllowsPivot(Sprint) + |yaw|≥pivotAngle
Stop 任意时刻再输入                  → Start
Dodge 恢复                            → Gait（PendingGait 经 MaxGait 钳制）
```

### 关键参数（LocomotionProfile 默认）

| 字段 | 默认 | 含义 |
|------|------|------|
| `idleInputThreshold` | 0.01 | 静止判定 |
| `stopMinSpeedFactor` | 0.5 | Gait→Stop 相对 runSpeed |
| `pivotAngleDegrees` | 135 | Pivot 夹角 |
| `gaitPolicy.maxGait` | Sprint | 玩家 Full；敌人近战建议 Run |
| `gaitPolicy.allowPivot` | true | 仅 Sprint 可 Pivot |
| `gaitPolicy.sprintAfterRunFrames` | 180 | Run→Sprint 累计逻辑帧 |
| `gaitInputGapGraceFrames` | 9 | Gait 松手宽限逻辑帧 |
| Motor `sprintSpeed` | 9 | 冲刺水平速度 |
| `sprintLean.maxLeanDeg` | 8 | L-DIR4 Visual 倾身；FaceCamera 路径不启用 |
| `sprintLean.leanEngageSmoothTime` | 0.22 | 切入满倾平滑（秒） |
| `sprintLean.leanRecoverSmoothTime` | 0.28 | 回正到 0 平滑（秒） |
| Camera `cameraFollowFacingSmoothTime` | 0.35 | L-DIR5 绕圈；越大弯越缓 |
| Camera `followFacingBackwardDeadzone` | 0.2 | 相机相对 Move.y 低于 -此值则不跟朝向 |

### Profile 配置（Katana / 敌人）

| AnimationKey | 说明 |
|--------------|------|
| Idle / Walk / Run | 基础循环 |
| WalkLeft / WalkRight | 对峙横移（敌人战斗 Profile 必绑） |
| Start / StartEnd / PivotTurn / StopL / StopR | 玩家相位；StartEnd=Run_Start_End |

资产：`Assets/Data/CharacterLocomotion/`。现有 Profile 必须由用户在 Inspector 分别点击 Timing Baker 与 Root Motion Bake；运行时不提供 duration/handoff/落脚或旧根运动轨回退。`LocomotionTimingAudit` 同时检查 Timing 和已启用的 StartEnd/StopL/StopR/PivotTurn 根运动轨。

### Action 状态下的动画锁

进入 `ActionState` 时 `SetLocked(true)`；`LocomotionStateMachine.Exit` 冻结落脚采样。Exit Action 后回 Locomotion 从 Idle 再起（可消费 Resume）。

### 已知限制

- 急停减速曲线等旧 Phase D：**明确不做**（2026-08-12）；Stop/Pivot 靠烘焙根位移
- Start/Stop/Pivot Clip、落脚标记与根运动轨需人工配置并通过只读审计
- V2 `ActorReplicationSnapshot` 显式携带 AnimationKey 与 `LocomotionPhaseFrame`；Owner 仅在需要 Reconcile 时恢复，Observer 特殊相位跟播放头，Idle 循环不反写模拟

### 相关文件

- `Assets/Scripts/Domain/Character/Locomotion/*`
- `Assets/Scripts/Domain/Character/Animation/*`
- `Assets/Scripts/Domain/Character/StateMachine/States/LocomotionState.cs`

---

## 5. 第三人称相机

### 功能说明

Cinemachine 2 第三人称跟随；鼠标控制 yaw/pitch；碰撞遮挡；启动时锁定光标。`CameraRig` 对 `CameraRoot` 做滤左右 / SmoothDamp，并支持 Action Camera 窗的 `FollowHold`。`CameraDirector` 持有 Free / SkillShot / Cutscene 优先级栈；`CameraShotPlayer` 按本机逻辑动作帧求值内嵌官方 Spline，并在 Director 内部 A/B VCam 间切段。代码中的 CameraLock 槽当前不启用。

### 实现方案

**层级结构（运行时创建或复用）**

```
Player
  └── CameraRoot (y = 1.4)     ← 角色跟随目标（硬绑角色）

CameraManager + CameraRig + CameraDirector + CameraShotPlayer（场景对象）
  └── CameraOrbitPivot         ← CameraRig 写 SmoothDamp / FollowHold
        └── CameraPitchPivot   ← pitch 旋转
              └── CM ThirdPerson (CinemachineVirtualCamera)
                    Follow = pitchPivot, LookAt = orbitPivot
```

**Virtual Camera 组件**

- `CinemachineTransposer`：后方 `-followDistance`，LockToTarget，无 damping（平滑在 Orbit 层完成）
- `CinemachineHardLookAt`：注视平滑后的 `orbitPivot`
- `CinemachineCollider`：Default 层遮挡，PreserveCameraHeight

**跟随平滑 / 演出镜头**

- `CameraManager.LateUpdate`：先跟朝向，再 `CameraRig.Sync`，最后 `StageMoveReferenceYaw`
- 首帧、`followSmoothTime <= 0`、或距离超过 `SnapDistance`(3) 时直接吸附
- Active 角色表现根切换后 0.2s 内改用 0.04s SmoothDamp，并完整吸收横向位移；结束后恢复日常滤左右参数
- 对外提供 `SnapFollowToTarget()` 供传送等硬重置
- Action Timeline 的 `cameraShotStates` 是唯一镜头窗真源；`CameraShotSequence` / Preset SO 不存在
- `CameraShotPlayer` 只读 `ActionSimSnapshot.CurrentFrame`；Camera 窗不进入 `EnumerateStates()`，因此 Sim Runner 不执行
- `holdFollow` 钉住进入窗时的 FollowAnchor；窗口退出后从钉点平滑追回
- `CameraShotNotifyState.positionSpline`（官方 `UnityEngine.Splines.Spline`）是机位位置唯一真源；恒速模式按各 Bezier 段 `GetCurveLength` 累计弧长，再通过 `GetCurveInterpolation` 定位段内进度，禁止使用计算首尾直线距离的 `GetPointAtLinearDistance`
- `splineCurveRule` 提供 Linear / ArcUp / ArcDown / ArcLeft / ArcRight 端点预设；由 `CameraSplineCurveRuleUtility` 把首尾点编译为两 Knot Spline；Custom 才保留任意 Knot/Tangent
- `CameraTransformBinding` 只提供 Character / SelectedTarget / World 根来源；空 `AnchorId` 为 Root，自定义部位经 `CameraAnchorProvider` 映射
- Dynamic Binding 逐帧读取 Transform；Snapshot Binding 只在窗口进入时捕获；解析失败不回退固定节点名
- `CameraDirector` 固定复用两台无 Body 的 CM2 SkillShot VCam；换段 Ping-Pong Blend，并更新世界 Pose / FOV / 可选 Impulse
- Action Editor Scene 的预设规则只显示并编辑首尾端点，提供明确起点/终点选择；Custom 才开放全部 Knot、E 旋转、Tangent、插入/删除/平滑；`Scene 构图 → 选中点` 会把 Scene Camera 位置反解到 Reference Binding，并沿 Scene forward 以当前焦距生成 LookAt Binding 局部观察点；FOV 可选择 Keep Shot、Scene View 或 Custom，后两者在当前预览帧写入/覆盖 FOV Curve Key
- Camera 窗选中时按当前预览帧求值出的 Position/LookAt/FOV 绘制视锥；Knot Rotation 仍只控制切线局部朝向
- `ActionEditorCameraView` 是实际构图唯一预览入口：菜单 `ACT/Action Camera View` 或 Scene 浮窗打开；隐藏 Camera 复用 Main Camera 设置并渲染当前场景到缓存 RenderTexture，自动跟随 Shot/Frame/Position/LookAt/FOV，SceneView 保持自由导航
- Camera Inspector 只展示 Spline Knot 数量与 Closed，不再暴露未使用的 Int/Float/Float4/Object 扩展数据；旧 `Debug Scene Camera` 字段与 `SceneView.LookAtDirect` 接管路径已删除；Clipboard 仍递归复制 Spline 隐藏 MetaData
- 旧 `CameraDebugAnchorVisualizer`、`CameraDebugGizmoDrawer` 及常驻 Frustum/LookAt 定位图形已删除，不再干扰 Spline 编辑

**输入 / 跟朝向**

- `CameraManager` 引用玩家 `PlayerController`，通过 `PlayerController.LookInput` 获取非权威视角输入
- Update 累加 yaw/pitch；有 Look 时启动 `lookOverrideResumeDelay` 暂停跟朝向
- 前进/侧移且无抢权、且未在播招：`SmoothDampAngle(yaw → PresentationRoot yaw)`；**禁止**写 Motor 朝向
- 是否在移动：读 `ILocalPlayer.HasMoveIntent`。客机无 Actor，`Input` 为空，禁止再判 `local.Input.HasMoveIntent`
- 是否后退：读本机设备 `ILocalPlayer.MoveInput.y`；低于 `followFacingBackwardDeadzone`（默认 -0.2）则暂停跟朝向，避免后退 wish 与镜头互追转圈
- 出招/闪避/受击：读 `IsPresentingAction`，暂停跟朝向，避免连闪甩镜头
- 朝向源优先 `PresentationRoot`（客机预测体插值锚点）；空座位 `transform` 不转，跟它无法 A/D 绕圈
- `CameraLock` / `CameraMode.LockOn` 为暂留未启用入口；当前产品范围不绑定按键、不建设 LockOn VCam，且不得影响 Targeting/Action/InputFrame

**初始化**

- 确保 Main Camera 有 `CinemachineBrain`
- 按 Tag `Player` 查找 followTarget（若未指定）
- 销毁 legacy `CinemachineFreeLook`（若存在）

### 关键参数（Inspector 默认）

| 字段 | 典型值 | 含义 |
|------|--------|------|
| `cameraRootHeight` | 1.4 | 锚点高度 |
| `followDistance` | 4 | 相机距离 |
| `followSmoothTime` | 0.1 | Orbit 追 CameraRoot 的平滑时间 |
| `switchFollowSmoothTime` | 0.04 | 换人后的快速跟随平滑时间 |
| `switchFollowDuration` | 0.2 | 快速跟随保持时间；期间横向跟随系数临时为 1 |
| `initialPitch` | 15 | 初始俯角 |
| `horizontalSensitivity` | 0.15 | 水平灵敏度 |
| `verticalSensitivity` | 0.15 | 垂直灵敏度 |
| `topClamp` / `bottomClamp` | 70 / -60 | 俯角限制 |
| `invertY` | true | Y 轴反转 |
| `lockCursorOnStart` | true | 启动锁定鼠标 |

### 与移动的协作

`CameraManager` 只把最终 Orbit yaw 提交到 `InputReader` staged 槽；`InputReader.Sample` 固化为 `MoveReferenceYawQuantized`，Motor 仅消费该输入字段。

### 已知限制

- 平滑仅抹平位置顿挫；未按 Action/Locomotion 切换不同 `followSmoothTime`（可后续做方案 C）
- LookAt 已切到 `orbitPivot`，角色急速冲刺时镜头会略滞后于角色身体
- Lock-On 暂时舍弃；Director 当前保留未启用栈槽，但 LockOn VCam、TargetGroup 与切敌 Blend 不进入近期待办
- SkillShot Spline / FollowHold / Scene 预览已通过 Unity 脚本编译；Test Runner 与 Play 仍待人工验收
- 自定义 `AnchorId` 需要用户在角色 Prefab 配置 `CameraAnchorProvider`；解析失败时 Shot 不抢权
- LookAt 首版仍是 `lookAtBinding + lookAtLocalPosition`，未提供第二条观察点 Spline 或 Roll 曲线

### 相关文件

- `Assets/Scripts/App/Controllers/Camera/CameraManager.cs`
- `Assets/Scripts/App/Controllers/Camera/CameraRig.cs`
- `Assets/Scripts/App/Controllers/Camera/CameraDirector.cs`
- `Assets/Scripts/App/Controllers/Camera/CameraShotPlayer.cs`
- `Assets/Scripts/App/Controllers/Camera/CameraAnchorProvider.cs`
- `Assets/Scripts/Domain/Camera/CameraSplineEvaluator.cs`
- `Assets/Scripts/Domain/Camera/CameraShotPoseResolver.cs`
- `Assets/Scripts/Domain/Combat/Actions/Definitions/Timeline/CameraShotNotifyState.cs`
- `Assets/Scripts/Editor/Combat/ActionEditor/ActionEditorCameraShotPreview.cs`

### 5.1 唯一战斗目标

**功能说明：** 角色在范围内自动维护一个 `SelectedTargetId`；玩家可在 Locomotion/Action 中左右切换，动作后续旋转、吸附和重定位立即读取新目标。

**实现方案：**

| 项 | 方案 |
|----|------|
| 角色状态 | `CharacterTargetingState` 唯一持有 SelectedTargetId，在 Action 路由前 Step |
| 纯解析 | `DeterministicTargetResolver` 只读整数位置、team/alive 与 SimActorId |
| 自动选择 | 当前无效时最近距离；等距取较小 SimActorId |
| 保持 | 当前目标在 retainRange 内保持，不被新近敌人抢走 |
| 切换 | `TargetSwitchLeft/Right` Pressed + MoveReferenceYaw 环绕排序；Action 中同样生效 |
| 消费 | ActionRotation、TargetAdhesion、MotionCommand、Camera/UI 共读；Locomotion FaceTarget 仅 Profile 声明时消费 |
| 相机 | `ILocalCameraTargetSource` 只映射 Id→ITargetable；CameraLock 不写回 |

**关键参数：** `CharacterCombatConfig.TargetAcquireRangeMeters` 默认/旧资产回退 12m；`TargetRetainRangeMeters` 默认/非法值回退 Acquire+1.5m。

**运行时流程：**

```
InputFrame → CharacterTargetingState.Step
  → DeterministicTargetResolver → SelectedTargetId
  → Action/Locomotion/Motion 本逻辑帧只读
  → Camera/UI 只读表现映射
```

**已知限制：** `Parry` 已随 P-SW2 完成用户验收；`TargetSwitchLeft/Right` 仅在需要手动切敌时再绑定；`CameraLock` 不进入当前范围。完整 Snapshot Restore 归 L3。

**相关文件：**

- `Assets/Scripts/Domain/Simulation/Targeting/*`
- `Assets/Scripts/Domain/Combat/Targeting/CharacterTargetingState.cs`
- `Assets/Scripts/Domain/Combat/Targeting/ILocalCameraTargetSource.cs`

---

## 6. 玩家角色装配

### 功能说明

Scene 中创建 Empty GameObject，挂载 `PlayerController` 并指定 `CharacterConfig` 后，运行时创建模型、`CharacterController` 与纯 C# runtime 服务图。

### CharacterConfig

| 字段 | 作用 |
|------|------|
| `ModelPrefab` | 实例化为玩家根节点子物体，要求子层级能找到 Animator |
| `DefaultLocomotionProfile` | 默认 Idle/Walk/Run 动画映射 |
| `InputActions` | Player ActionMap 输入资产 |
| `Motor` | 移动速度、重力、CharacterController 高度/半径/中心 |
| `CombatProfile` | 战斗模式、出招表与技能入口 |
| `Combat` | teamId、Hitbox/VFX 挂点名、索敌起点名 |

### 运行时装配

- `PlayerController.Awake` 校验 `CharacterConfig`，创建 `InputReader`，调用 `CharacterActorFactory.Create`
- 实例化模型 Prefab，查找 Animator
- Player 根只补齐 Unity 必需的 `CharacterController`
- 构造纯 C# `InputReader`、`CharacterAnimationService`、`CombatModeService`、`ActionResolverService`、`ActionSim`、`CharacterActionDriver`、`CharacterStateMachine`
- 注册纯 C# `HitboxFrameConsumer` 为 Logic Tick 消费者；注册 `ActionVfxPlayer` 为 `IActionNotifyConsumer`
- `PlayerController.OnEnable` 向 `SimulationHost` 注册 `CharacterActor`，OnDisable 对称注销
- `CharacterActor.Step` 只保留世界入口并转交 `CharacterSimulationPipeline`；Pipeline 统一输入、动作路由、重力、状态机和 PostCombat 顺序

### Editor 操作

在 Unity Editor 中创建 `CharacterConfig` 资产，填写模型 Prefab、内嵌 CombatModes（ActionGraph + Locomotion Profile）；Scene 内只需要 Empty + `PlayerController` + 该配置引用。

---

## 7. 动作系统

> 运行时细节以本节与 [ACTION_SYSTEM_LOCKSTEP_REFACTOR_PLAN.md](../../../docs/ACTION_SYSTEM_LOCKSTEP_REFACTOR_PLAN.md) 为准；排期见 MASTER。

### 功能说明

多战斗模式下，玩家通过 `ActionGraph` Entry×Intent 起手攻击/闪避；输入、索敌、起手行为、Cancel 与自动衔接统一由 Graph 节点/边描述。L1B 后 `ActionSim.CurrentFrame` 为纯模拟权威，表现桥只读 Snapshot/Event，Action 内容固定为 60Hz。

### 实现方案

| 项 | 方案 |
|----|------|
| 起手 / 缓冲 | `GameplayIntentBuffer` → `CharacterActionDriver` → `ActionResolverService.TryResolveStart` → `ActionSim.TryStart` |
| 节点 Intent | `ActionGraphNode.Intent = GameplayIntentType`；ActionDefinition 不保存输入语义 |
| 选招策略 | `ActionGraph` Entry / Normal 与 Perfect CancelWindow 边 / `ActionGraphSharedRoute`；顺序组按类型聚合子节点 |
| 六向闪避 | `DirectionalActionResolver` 统一解析前、后、左前、左后、右前、右后；前后扇区半角默认 `30°`，纯左/右输入偏向前侧变体 |
| Cancel 下一招 | 每招一个 Normal、可选一个 Perfect；窗口重叠且同 Intent 时 Perfect 优先 |
| 自动衔接 | `ActionGraphNode.AutomaticTransitions`，支持 AnimationEnd / AtFrame / OnHitConfirm / OnWhiff；Graph Editor 每条规则提供独立橙色 Auto 输出，连到目标 In 后保存目标 Id，普通节点与顺序组子节点均支持；未连线表示结束动作，Inspector 不再手填目标 |
| 高优硬打断 | Action 态：`TryResolveStart(PriorityInterrupt)` → `ActionSim.TryInterrupt`（候选 `interruptPriority` 严格大于当前，且 `IsInterruptibleAtFrame`） |
| 时间轴数据 | `ActionDefinition.Timeline`：`ActionNotify` 点事件（Event/VFX/SFX）+ `ActionNotifyState` 区间窗口 |
| 移动取消 | `CharacterActionDriver` + `CancelWindowNotifyState(Movement)` |
| 招式旋转 | `ActionRotationDriver` + `RotationNotifyState`；节点只声明是否消费 SelectedTarget/平滑覆盖，目标由角色逐帧提供 |
| Runtime Logic Tick | `SimulationWorld` → `CharacterActor.Step` → `CharacterSimulationPipeline.Step` → 唯一 `ActionSim.Step`；窗口、Graph 与结束只读整数帧 |
| 逻辑 / 表现边界 | `ActionSimSnapshot` / `ActionSimEvent` → `CharacterActionPresentationBridge` → Clip Seek / Timeline |
| 命中回流 | `HitboxFrameConsumer` Collect → `CombatHitPipeline` 帧末 Resolve → `IActionHitReceiver.NotifyHit` |
| 命中去重 | 单次动作会话内按 `(HitboxIndex, TargetSimActorId)` 去重；排序键为纯模拟 `SimHitKey` |
| 自动衔接 | 普通 Tick 不再提前解析无输入 Transition；结算后 PostCombat 保持 OnHitConfirm 同帧生效 |
| 帧边界切招 | Cancel / Recovery / 自动衔接在判定帧只排队；下一 World 帧提交目标 frame 0 |
| 卡肉边界 | `AttackHitEvent` 只冻结动画/VFX 表现；禁止 Event Handler 回写 `ActionSim` |
| Graph 策略编辑 | Graph Editor 在普通节点和顺序组子节点内嵌策略折叠区，直接编辑 Intent、索敌、起手行为、战斗模式切换与自动衔接 |
| Motor | `CharacterMotor`（Locomotion 位移）+ `CharacterSimulationPipeline`（重力调度） |

### 关键参数（打断）

Cancel→Entry 范围收紧（2026-10-01）：隐式 Entry 仅在「当前来源节点 + 当前 Normal/Perfect 通道」完全没有显式边时启用；已有边即使 Intent 不匹配、目标失效，也不隐式回 Entry。`CollectCancelCandidateIntents` 同步采用此门槛。共享路由继续优先于隐式 Entry，不受此新增门槛禁用；重叠窗口仍沿用延后 Perfect Entry 的规则。此规则替代此前“显式边未命中即可回 Entry”的描述。Graph 工具提示同步，未修改角色资产。

Cancel 重叠窗口修复（2026-10-01）：`ActionSimResolverBridge` 使用快照当前帧查询 Normal 窗口；解析 Perfect 且 Normal 同时生效时，将 `ActionResolveContext.AllowCancelEntryFallback` 设为 false，`ActionGraph.TryResolveCancel` 此轮只解析显式边/共享路由。保持 ActionSim 的逐 Intent 优先级和 Perfect→Normal 顺序，Entry 由 Normal 最后兜底；仅 Perfect 生效时仍允许 Entry。修复隐式 Entry 抢占 Unagi 普攻 3→5 的问题，无资产迁移。回归 `CancelEntry_PreservesRoutePriorityIncludingOverlappingWindows` 已补充编译，Unity Test Runner / Play 尚待验收。

Graph 布线辅助（2026-10-01）：`ActionGraphGrid` 替换未显式铺满画布的默认 GridBackground，绘制 20 单位细网格和 100 单位主网格，跟随内容空间平移/缩放；细格小于 8 屏幕像素时隐藏。转折点新增与拖动共用 `RoutedActionGraphEdge.SnapPoint`，逐轴优先吸附两端端口及同一连线其它点，容差固定 8 屏幕像素，其余轴落到 20 单位网格；Alt 临时绕过。没有新增运行时字段。补充编译通过，新增缩放阈值、最近坐标、负坐标与绕过测试，Unity Test Runner / 视觉验收尚未执行。

Graph 隐式路由与连线布局（2026-10-01）：有效 Normal / Perfect Cancel 窗内，`ActionGraph.TryResolveCancel` 依次解析显式边、共享路由、匹配输入 Intent 的 Entry；`CollectCancelCandidateIntents` 同时收集 Entry 意图，保证输入缓冲能够选中隐式去向。没有有效 Cancel 上下文或来源节点时不执行隐式路由；Intent=None 的 AI Entry 不参与输入匹配。Entry 继续复用原有变体与 Special 能量选形逻辑。此规则对全部 Graph 生效，无须增加实体边或迁移资产。编辑器顶部展示规则摘要，隐式关系不铺设重复连线。

显式 Cancel / Auto 连线使用 `RoutedActionGraphEdge`：双击线段插入转折点，左键拖动、右键点位删除；无点时显示原生曲线，有点时显示折线。`ActionGraph.editorEdgeLayouts` 默认空数组，按可视端口键保存图内容空间坐标，不进入运行时路由；保存会清理已删除连线的布局。支持 Undo、保存重开与折线路径拾取/框选。限制：自动规则端口键包含规则序号，重排规则或改变分组拓扑后应重新检查布线；目前 Unity 画布视觉与鼠标交互尚待验收。相关源码：`ActionGraph.cs`、`ActionGraphEditorWindow.cs`；回归类 `ActionGraphAutomaticTransitionTests` 覆盖优先级、窗口门槛、布局往返、删除与 Undo，已补充编译、未运行 Unity Test Runner。

自动过渡作者流程（2026-10-01）：Graph Editor 点击 `+ <NodeId> Auto Transition`，编辑 Condition / AtFrame 起始帧 / Priority，再从对应 Auto 输出拖到目标 In。每条规则最多一个目标，多分支添加多条规则；断线保留规则并恢复“条件满足时结束动作”，彻底取消需删除规则。已有目标自动还原为连线，无需迁移资产；失效目标保留并显示警告文字，避免保存时无意改成停止。分组内部与自环允许连线。运行时继续读取同一份 `AutomaticTransitions`，不新增 Cancel 边或第二套拓扑。相关实现：`Assets/Scripts/Editor/Combat/ActionGraph/ActionGraphEditorWindow.cs`、`ActionGraphInspector.cs`。验证：新增 `ActionGraphAutomaticTransitionTests`，尚未执行 Unity Test Runner；需人工检查 Save/Reload、Undo/Redo、断线与自动衔接 Play。

| 参数 | 默认 | 说明 |
|------|------|------|
| `ActionExecutionPolicy.interruptPriority` | `0` | 越大越优先；同级不互硬打断 |
| `ActionPhaseNotifyState.interruptible` | `true` | Startup/Active/Recovery 覆盖时参与硬打断；Invincible/SuperArmor 标签不参与 |
| Recovery `allowMovementCancel` | `true` | 有移动输入时退出 Action 返回 Locomotion |
| Recovery `allowEntryRestart` | `true` | 有效动作缓冲按当前 Graph Entry 重开 |
| 无 Phase 覆盖帧 | — | `IsInterruptibleAtFrame` 返回 `true`（默认可硬打断） |
| `GameplayIntentProfile.actionBufferDurationFrames` | `60` | Action 内预输入有效逻辑帧数；过期后不再于 Recovery/收招误触发 |

### 运行时流程（高优打断 + Logic Tick）

```
CharacterActionDriver.ProcessGameplayInput（Action 态）
  → TryPriorityInterrupt(intent)
      → ActionResolverService.TryResolveStart(Origin=PriorityInterrupt)  // Graph Entry
      → ActionSim.TryInterrupt
  → 失败则 Buffer(intent)  // 留给 CancelWindow

CharacterActor.Step
  → 唯一调用 ActionSim.Step
      → CurrentFrame + 1 → ActionSimEvent
  → CharacterActionPresentationBridge.ApplyStep
          → HitboxFrameConsumer.OnCombatFrameAdvanced（只 Collect）
          → ActionTimelineRunner.Dispatch
              → PlayVfxNotify 点触发 → ActionVfxPlayer.OnActionNotify（Resolve attachPointId + 显式 playbackSpeed）
              → PlaySfxNotify 点触发 → ActionSfxPlayer.OnActionNotify（pitch = playbackSpeed）
              → 其他 ActionNotifyState Enter/Tick/Exit
      → CancelWindow / Recovery Entry
          → CancelWindow：同一意图先 Perfect 后 Normal，成功后只排队
          → Recovery Phase：按窗口开关排队 Graph Entry 软重开
SimulationHost 帧末
  → CombatHitPipeline 稳定排序并统一伤害/Reaction/ConfirmHit
  → CharacterActor.ResolvePostCombat
      → Graph 自动衔接排队；自然结束按 TotalFrames 停止
      → NotifyActionEnded → ActionSfxPlayer.OnActionEnded → 0.1s 音量淡出后 Stop
  → PublishAttackHitCommand → AttackHitEvent（仅表现）
      → HitImpactController：Feedback 受击 VFX/SFX（完美闪避 / 弹刀吞伤跳过）
      → CameraShakeController / HitStopController（既有）
```

### 7.1 命中受击 Cue（A2）

**功能说明：** Confirm 命中后在逻辑接触点播特效与音效；挥空不播；完美闪避 / 弹刀吞伤不播受击 Cue。

**实现方案：**

| 层 | 组件 |
|----|------|
| 接触点 | `HitboxMath.EstimateContactPointOnHurtbox`（攻击盒中心→受击盒最近点） |
| 配置 | `HitFeedbackSettings`：VFX/SFX、相对接触点偏移、随机欧拉范围 |
| 事件 | `AttackHitEvent.HitPoint` / `AbsorbedByPerfectDodge` / `AbsorbedByAssistParry` / `HitStopFrames` |
| App | `HitImpactController`：落点=接触点+Offset；`LookRotation * Random.Euler` |
| 调试 | F4 → `CombatHurtboxDebugSettings.ShowHurtboxes` 画逻辑 Hurtbox |
| 卡肉 | App `CombatFeedbackSystem` 单向调用 `VFXManager.BeginHitStop/EndHitStop`；Manager 按 SpawnOwner 暂停对应池实例，Domain VFX 不反向访问 App |

**关键参数：** `hitImpactWorldOffset` 默认 `0`（相对接触点）；`randomizeImpactRotation` 默认开，Y `0～360`；SFX Volume `0～1`。

**已知限制：** 单 Hurtbox/角色，无多部位表；新招仍须人工绑 Feedback Prefab/Clip。A2 打击感验收 ✅ 2026-08-09。

### 7.2 受击档位裁定（冲击力 × 韧性）

**功能说明：** 档位只由本刀冲击力对目标当前韧性算出。不足则 Flinch（扣血不断招、叠 Additive）；压过则 LightStun 起进 Hit。Shake 不 Play 走跑/出招主轨，不 `SetLocked`。

**实现方案：**

| 项 | 方案 |
|----|------|
| 档位 | `HitReactionKind`：None / Flinch / LightStun / HeavyStun / Launch / Death |
| 裁定 | `CharacterReactionResolver.ResolveKind(冲击力, 韧性)` → `HitReactionCommand` |
| 冲击力 | `HitPayload.interruptLevel` |
| 韧性 | `CharacterCombatConfig.baseInterruptResist` + 当前帧 Phase `interruptResistBonus` |
| SuperArmor | 非 Death 最多 Flinch |
| 执行 | `CharacterReactionService`：Flinch 不 `EnterHit` / 不 `NotifyHit`；Stun+ 旧路径 |
| 表现 | `CharacterActor.IssueFlinch` → `HitFlinchPlaybackController` → `PlayAdditive` |
| 边沿 | Flinch 把 `VitalityEdge` 清成 None，避免幽灵把底轨当受击重播 |
| 删除 | `desiredReaction` 不再参与裁定，避免同一刀被锁死在 Flinch |
| 旧入口 | `ResolveHit` 仅快照硬吸（Stun 边沿） |

**关键参数：**

| 参数 | 默认 | 含义 |
|------|------|------|
| `HitPayload.interruptLevel` | 1 | 冲击力 |
| `CharacterCombatConfig.baseInterruptResist` | 1 | 站立韧性；精英填 3 |
| `ActionPhaseNotifyState.interruptResistBonus` | 0 | 出招窗韧性加成 |
| `HeavyStunExcess` | 2 | 冲击 − 韧性 ≥ 2 → HeavyStun |
| `LaunchExcess` | 4 | 冲击 − 韧性 ≥ 4 → Launch |
| Flinch 键 | `AnimationKey.HitShake` | Additive；不进 Locomotion |

**运行时流程：**

```
CombatHitPipeline.OnHit → Vitality.HitReceived
  → CharacterReactionService
       Resolve(冲击力 vs 韧性 + SuperArmor)
       ConfirmHitReaction
       Flinch → Actor.IssueFlinch → HitFlinchPlaybackController / HitFlinchPresentation.PlayAdditive
       Stun+  → NotifyHit + EnterHit
       None   → 只保留已结算伤害
  PublishResolvedHit → ReplicatedHitEvent(ReactionKind) → ReplicatedFeedbackCoordinator → Proxy Additive
```

**已知限制：** 失衡条 / 击飞抛物线物理仍本轮不做。韧性与冲击数字可继续在 Editor 调。Listen 已验，不宣称公网 / W10。方案已关闭：`docs/2026.9.3/HIT_REACTION_IMPLEMENTATION_PLAN.md`。

**相关文件：**

- `Assets/Scripts/Domain/Simulation/Reactions/HitReactionKind.cs`
- `Assets/Scripts/Domain/Character/Reactions/HitReactionCommand.cs`
- `Assets/Scripts/Domain/Combat/Hitbox/HitReactionResolveQuery.cs`
- `Assets/Scripts/Domain/Character/Reactions/CharacterReactionResolver.cs`
- `Assets/Scripts/Domain/Character/Reactions/CharacterReactionService.cs`
- `Assets/Scripts/Domain/Character/CharacterConfig.cs`（`baseInterruptResist`）
- `Assets/Scripts/Domain/Combat/Actions/Definitions/Timeline/ActionPhaseNotifyState.cs`
- `Assets/Scripts/App/Controllers/Combat/HitFlinchPlaybackController.cs`
- `Assets/Scripts/App/Controllers/Combat/HitFlinchPresentation.cs`
- `Assets/Scripts/Domain/Networking/ActReplicatedHitEventCodec.cs`
- `Assets/Scripts/App/Networking/Services/ActClientRoomGameplay.cs`
- `Assets/Scripts/App/Events/Combat/HitFlinchEvent.cs`
- `Assets/Tests/Editor/Combat/HitReactionResolverTests.cs`

VFX 生命周期：`ActionVfxPlayer` 在招式结束 / 连招切招时**不**强制 Despawn；池化实例由 `VfxPooledInstance` 按粒子与 Animator clip 的较长自然时长（含 `playbackSpeed` / 卡肉冻结）自行回池。Spawn 时 `Rebind`+从头 `Play` Animator；`VFXManager` 维护活跃租约和当前攻击者卡肉状态，新生成实例在绑定 SpawnOwner 后立即继承暂停状态。无 `VFXManager` 时回退 `Destroy(lifetime)`。C1 展示包源资产位于 `Assets/Resources/Effect-C1/EffectPackage/`（Prefab 经 `PlayVfxNotify` 引用，非 `Resources.Load` 硬编码路径）。

SFX 生命周期：`ActionSfxPlayer` 使用 `ActionSfx` 下多声道 `AudioSource`（与脚步声隔离）；`OnActionEnded` 对仍在播的声道做 **0.1s（unscaled）** 淡出；连招新 `PlaySfx` 走空闲声道，不 Cancel 正在淡出的旧声道。

编辑器 Scrub 使用 `ActionEditorPreviewSession` 做 Pose/VFX 预览，并与 Runtime 共用无副作用 `ActionFrameQuery` 的段映射、窗口与点事件规则；不执行 `ActionSim.Step`。

**编辑器交互（2026-08-04）**：

- VFX/SFX/Event 点事件在时间轴上绘制为**菱形**（热区按轨高，不随 1 帧条宽缩小）
- Timeline 顶栏 **Zoom**（1×–16×）+ Ctrl/Cmd+滚轮；放大后横向滚动以精确拖帧
- Scrub / 播放 / 工具栏改帧时，playhead 超出 Zoom 可视区会**自动平移**时间轴视图
- Scene Hitbox 线框与 VFX Prefab 按 **Preview Frame** 驱动：粒子 `Simulate` + **Animator Clip 同时间采样**（对齐 showcase 的 playOnAwake + Animator）；Hitbox 仅在窗口激活时可见；无需选中时间轴窗口
- Scene 烘焙根运动：`ActionMotionTrajectorySceneDrawing` 画 Full/Gameplay/Residual 轨迹 + 当前帧落点；`BaseMotionMode=BakedMotion` 时 `ActionEditorPreviewSession` 按表挪动预览根并写 VisualMotionRoot 残差（离开预览还原）
- Hitbox / VFX 均支持 `parentToAttachPoint`：勾选跟随挂点；取消则在进入/触发帧冻结世界空间（运行时 `HitboxFrameConsumer` 缓存 OBB）
- 右侧 Inspector：`ActionNotifySelectionDrawer` 纵向 ScrollView，Hitbox 长表单可滚到底部编辑
- Create：选角色文件夹（如 Unagi），自动保存到其子目录 `ActionDefinition`（无则创建；已有旧名 `ActioniDefinition` 则复用）；默认名可改；左侧列表按文件夹分组
- 时间轴多选：Ctrl 点选 / Shift 同轨范围选；Ctrl+C/V 复制粘贴（可跨 Action，按预览帧对齐）；Delete 删多选
- 同类型多选：右侧改任一字段（含 Hit Payload / VFX Prefab 等）批量写回全部选中窗口；混合类型仅改主选中项
- 拖拽框选：轨道路面空白拖拽矩形多选窗口；Ctrl/Cmd 叠加；单击空白清空

### ActionEditor 对齐状态（2026-08-02）

| 对齐度 | 项 |
|--------|-----|
| ✅ | Runtime `UpdateFrame` 已删除；`ICombatFrameConsumer`、`ActionTimelineRunner`、`ActionNotify` / `ActionNotifyState` 保持整数帧 Schema |
| ✅ | 命中回流、`OnHitConfirm` / `OnWhiff` Transition 条件 |
| ✅ | `CharacterActionDriver` 角色无关输入路由 |
| ✅ | Hitbox/VFX/Cancel/Movement/Rotation 已收敛到 `ActionTimeline`，删除旧双轨数组 |
| ✅ | `ActionEditorWindow`：手动加轨、轨头纵向拖拽排序、窗口拖拽；VFX/SFX 为单帧点事件菱形，Phase 为区间窗口；时间轴缩放 |
| ✅ | Scene 预览按 Scrub 帧显示全部激活 Hitbox / 已触发 VFX（`ActionEditorVfxPreviewExtension` 多实例） |
| ✅ | `ActionSfxPlayer` 运行时点触发；招式结束/打断时 0.1s 淡出；`CharacterAttachPointResolver` 供 VFX/Hitbox 共用 |
| ⬜ | 伤害结算、Hit 状态、GM 热重载 |

### 已知限制

- 现有资产需要在 Unity Editor 的 Phase 轨重建原 `phases[]`，并为 Recovery 配置移动取消 / Entry 重开开关；Agent 未直接修改 `.asset`
- ActionDefinition 的作者 sampleRate 字段已删除；SampleRate 统一返回 ActionSim.LogicHz=60。资产迁移已验证全部 48 个动作为 60Hz，窗口与裁切数据未改动。
- 硬打断与 Recovery 软重开走 Graph Entry，不要求 Cancel 边；独特连招进位仍依赖 Combo Window + 显式边
- 旧 `perfectFrame`、Cancel 槽 Id 与同类型多窗口不再受支持；资产需整理为一个 Normal 与可选一个 Perfect
- Scene 玩家入口已改为 Empty + `PlayerController` + `CharacterConfig`

### Editor 操作（Prefab）

创建 `CharacterConfig` 后，在 Scene 空物体的 `PlayerController` 上指定该资产；Play Mode 验证：起手、连段、移动取消、索敌旋转、`ActionTimeline` 中的 Hitbox/VFX 与预期一致。

### 相关文件

- `Assets/Scripts/Domain/Combat/Actions/{Definitions,Resolution,Execution,Frames}/*`
- `Assets/Scripts/App/Controllers/Gameplay/PlayerController.cs`
- `docs/ACTION_EDITOR.md`、`docs/ACTION_SYSTEM_LOCKSTEP_REFACTOR_PLAN.md`

---

## 8. 敌人 AI、伤害与生成

### 功能说明

敌人复用玩家的 CharacterActor、Locomotion、ActionGraph 与 Hitbox 管线。`EnemyBrain` 为门闩宿主，决策经 `IEnemyBehaviorRunner`；Hit/Death 不进树。  
**现状：** EnemyBrain 写 `ActionEntryRequestBuffer` 与 `LocomotionDesireBuffer`；角色服务图通过只读接口消费，`ProduceInput` 仅写空 `InputFrame`。契约见 `docs/ENEMY_BEHAVIOR_TREE_PLAN.md` §3.4。

### 实现方案

| 项 | 方案 |
|----|------|
| 配置 | `EnemyDefinition` 组合 CharacterConfig、BrainProfile、**BehaviorTree**、独立 teamId 与 HP；战斗半径/幅度终态迁节点（E-CFG） |
| 可替换契约 | `IEnemyBehaviorTreeAsset.CreateRunner` → `IEnemyBehaviorRunner`；Brain 不持有具体树类型；输出槽终态见 BT PLAN §3.4.2 |
| 行为树资产 | 仅 `customRoot`（SerializeReference）+ `graphLayout`；无 Kind/代码预设种树 |
| 条件节点 | UE 风格**单子装饰**（`ConditionalDecoratorNode`）；失败 Abort Self（Reset 子树） |
| Graph 数据 | `graphLayout` + `nodeGuid`；Mapper Flatten/Rebuild；Validator |
| Graph 编辑器 | 宿主牌连线；Condition/Decorator **叠徽章**；Save 展开回装饰链；A1 已落地；待优化见 `docs/2026.8.11/ENEMY_BEHAVIOR_TREE_BACKLOG_PLAN.md` |
| AI 输出 | Runner → Brain：`LocomotionDesireBuffer` + `ActionEntryRequestBuffer`；无 `AIInputWriter` |
| 分层边界 | Character / Combat 仅依赖 `IMoveIntentSource` / `IActionEntryRequestSource`；EnemyActorFactory 构造注入，Actor 无 Enemy Bind/分支 |
| 招式池 | `RandomSelector`（权重；RNG 可注入/`blackboard.Rng`）；样例 `CreateCombatPool`；**无** `PulseAttack`/`AttackPulse` |
| 冷却 | `CooldownGate` 填秒并暂存；Brain 确认后生效；失败写 `action_entry_retry`；对峙用 `CooldownNotReady` |
| 追击 / 对峙 | Attack→CD→Strafe(CdNotReady)→Chase(CdReady)；节点时间填秒；可选 DistanceBand |
| 攻击占用 | `WaitWhileInAction`：起手后 Running 至离 Action，期间清 Move；**勿**被 IsLocomotion/CdReady 包在 Wait 外层 |
| 敌人步态 | 独立 LocomotionProfile + `GaitPolicy.MaxGait=Run`；对峙 WalkLeft/Right；Run→Walk 硬切 |
| 木桩 | `enableCombatActions=false` 时不建 Runner；Hit 门闩仍消化 |
| 伤害与反应 | 同前：`CharacterReactionService` → `NotifyHit` / `NotifyDeath` → Runner.Reset |
| 生成 | `EnemySpawnController` → `SpawnEnemyCommand` → `EnemyController`；`EnemySpawnSystem` 限制存活数 |

### 关键参数

| 参数 | 默认 | 含义 |
|------|------|------|
| `HitPayload.baseDamage` | 10 | 单个 Hitbox 的基础伤害 |
| `CharacterCombatConfig.maxHealth` | 100 | 玩家默认生命值 |
| `CharacterCombatConfig.reactions` | 空规则集，默认硬直 0.35s | Resolver 按反应类型与 HitReactionId 选择表现 Action；无动作时使用规则集硬直时长 |
| `HurtboxDefinition.localOffset` | (0, 0.9, 0) | 标准人形受击框中心，角色根位于脚底 |
| `EnemyDefinition.teamId` | 1 | 敌人阵营；不继承复用 CharacterConfig 的玩家阵营 |
| `EnemyBrainProfile.enableCombatActions` | true | 木桩关行动 |
| `EnemyBrainProfile.deathDespawnDelaySeconds` | 0.5 | 死亡回收等待 |
| BT `AggroGate.enter/exit` | 10 / 14（样例） | 仇恨滞回（原 Profile 半径） |
| BT `InAttackRange.distance` / Move `stopDistance` | 2 / 1.2（样例） | 攻击与贴身停步 |
| BT Move/Strafe/BackOff `magnitude` | 1 / 0.35 / 1（样例） | 移动幅度 |
| BT `CooldownGate` basic_attack | 1.2s（样例→72 帧） | 普攻冷却；节点填秒 |
| BT `CooldownReady` / `CooldownNotReady` | basic_attack | CD 毕追击 / CD 中对峙 |
| BT `DistanceBand` min dwell | 0.1s（样例） | 可选滞回驻留；节点填秒 |
| BT `WaitSeconds` | 0.5s 默认 | 等待 Task；节点填秒 |

### 运行时流程

```
SimulationWorld.Step
  → EnemyHandle.ProduceInput
       → EnemyBrain.Step（门闩 / 填黑板 / Runner.Tick / 提交 Desire + Entry Request）
       → InputFrameBuffer 写空帧（维持统一 Actor Step 时序）
  → EnemyHandle.Step → CharacterActor.Step(InputFrame)
       → Locomotion/CharacterMotor 读 IMoveIntentSource
       → CharacterActionDriver 消费 IActionEntryRequestSource

CombatHitPipeline（全体 Actor Step 后）
  → CharacterReactionService → EnterHit / EnterDeath
  → EnemyBrain.NotifyHit / NotifyDeath → Runner.Reset
```

### 已知限制

- **真敌**须挂已配置 `customRoot` 的 `EnemyBehaviorTree` SO；空根或未挂树在 Combat Actions 开启时会失败。
- Desire + Request 命令轨已落地（E-MOVE2）；闪避/技能须用 `RequestCombatAction` 指 Entry。
- **E-CFG1：** 战斗距离/幅度在节点；旧树资产须人工补 `AggroGate` + 节点参数，否则仇恨/幅度可能用默认或不进仇。
- 追击直线趋近（`StraightPathQuery`）；寻路见演进计划 E4。
- 旧 Kind 预设资产若 `customRoot` 为空，须在 Graph 编辑器中重新搭树并 Save（无 Fill/默认种树）。
- 旧「Sequence 下挂叶子 Condition」树须改为 Condition→子树装饰链后 Save，否则 Validate 报 child 为空 / 行为不符。
- Graph Undo 以 Save 的 `RegisterCompleteObjectUndo` 为主；画布即时删节点未做完整 Undo 栈。
- 死亡回收当前使用 Destroy；对象池可后续替换。
- EditMode 样例树仅存于 `EnemyBehaviorTreeDefFactory`（测试用），不进运行时资产默认路径。

### 相关文件

- `Assets/Scripts/Domain/Enemy/*`（含 `BehaviorTree/Serialization/`）
- `Assets/Scripts/Editor/Enemy/BehaviorTree/*`
- `Assets/Scripts/Domain/Character/Commands/*`
- `Assets/Scripts/Domain/Combat/Actions/Execution/ActionEntryRequest*.cs`
- `Assets/Tests/Editor/Enemy/EnemyBehaviorTreeTests.cs`
- `docs/ENEMY_BEHAVIOR_TREE_PLAN.md`
- `docs/2026.8.11/ENEMY_BEHAVIOR_TREE_BACKLOG_PLAN.md`

---

## 9. 预留 / 未完成功能

### CharacterStateType 预留枚举

- `Hit = 80`、`Death = 100` — 已有通用 State；玩家受击/死亡动画资产尚未配置

### 空模块目录

`UI/` — 仅 `.gitkeep`；Enemy 与 Combat Damage 已实现

---

## Action 内输入移动（2026-10-01，待 Unity 验收）

**共享进度修复：** `animationTimeMode` 默认 `FollowActionSegment`，Move 使用当前 Animation Segment 的片内时间（含裁剪起点与远端小数帧），不受窗口起点或换向时间影响，也不独立循环。`FromDirectionChange` 用于独立移动循环，此时才显示/应用 Loop Move。窗口需在 Cancel 段之前结束，四向 Clip 必须覆盖对应的采样范围；不自动拉伸短片。Editor 预览与本机/Observer 共用采样方法。新增裁剪、晚入窗口、换向、松手和 Cancel 边界测试，Unity 集成验证待运行。

**轨道窗口增量：** 已移除 ExecutionPolicy 内嵌配置、独立 Pose 和 Movement Tail Frames。用相邻且不重叠的窗口表达不同阶段；关闭 Override Movement Animation 的窗口只移动、不覆盖原动画。松手使用 ActionFrameQuery 当前帧，动作时钟持续推进。

**功能：** 固定时长 Action 的指定帧范围内允许 XZ 输入移动，有输入时覆盖前后左右动画，无输入时采样当前帧的 Action 原动画。范围结束恢复原动作段；移动不会创建新动作或延长 TotalFrames，适配当前 Vivian 的 Pose＋Cancel 两段结构。

| 职责 | 实现 |
|---|---|
| 配置与门禁 | `ActionTimeline.InputMovementStates`、`ActionDefinition.GetInputMovementError`；缺片/越界/冲突拒绝起手 |
| 固定帧位移 | `CharacterActionGameplayStep` → `CharacterMotor.ResolveActionMoveWish/MoveActionMm`；复用角色相对方向解析与身体碰撞 |
| 单一主轨 | 本机 Sink 与 Observer 共用 `ActionInputMovementPlayer`，Tick 混合后 Seek，方向状态不变不重播 |
| 复制/预测 | 快照增加方向及切片起始帧；Owner 重放已记录输入请求的电机碰撞，不推进 Action/扣费/Notify |
| Editor | Input Movement 轨道窗口配置；检查页方向预览；动作页错误提示和 Undo 清理冲突烘焙表 |

**参数：** 窗口 StartFrame～EndFrame 为闭区间且不超过 TotalFrames，不使用 -1；2500mm/s、输入门槛 .2、最短方向驻留 3 帧、混合 .08 秒；动画默认跟随 Action 段，仅独立起播模式使用 Loop Move。开启覆盖时需绑定四个 Clip，无独立 Pose。窗口禁止重叠及 Movement Cancel；输入移动与烘焙表、脚本位移、吸附、MotionCommand 互斥。

**运行时：** `CharacterSimulationPipeline.Step` 摄入 InputFrame → ActionSim → Gameplay 解析一次输入位移 → Motor 碰撞 → Sink 采样方向片。只有真实 FrameAdvanced 才产生输入位移，防止卡肉最后一帧重复移动。窗口外/退出恢复正常动作或 Locomotion。

**限制：** 不处理悬空高度；跨普通攻击的完整回滚未实现。Unity 编译、Editor 集成测试和网络 Play 未验收；已通过独立编译、6 项窗口测试及 35 项纯逻辑回归。当前资产未修改，需 Editor 布线；源文件列表与步骤见[实施记录](../../../../docs/2026.10.1/ACTION_INPUT_MOVEMENT_IMPLEMENTATION.md)。

### 输入移动连续换向混合（2026-10-01，待 Unity 验收）

切段修正：Input Movement 的 `crossFadeSeconds` 仅用于同一动画段内切方向/恢复原片，进入新 Action 或新段使用 `ResolveSegmentCrossFade`。首段无 Override 时继承整招淡入，后续段无 Override 时为 0；移除未开启 Override 却仍读取旧段淡入值的分支。显式 Override 保留，包括 0。

后续复现修正：移除普通 Action/Observer 的 Tick→Seek 双求值，改用 `IAnimationPlayback.Sample(time,delta)`：混合推进、共享组定位后只 Evaluate 一次；定位组在求值时禁止额外推进，其它淡出片/Additive 正常推进。Input Movement 也复用此接口；即时 Seek 保留给明确的立即定位调用。`ACTGame.Animation.SwitchClip/Seek/Tick` 标记用于测量建图/采样耗时；卡顿根因与最终表现仍待 Unity 实测。

编辑器恢复帧区间绘制：闭区间 [start,end] 占 [start,end+1]，相邻动画条无空隙。帧标签/播放头/点事件定位在该帧区间右边界（frame+1），末帧 N-1 可到轨道最右边 N；鼠标反算对应减 1。区间左柄与右柄分别按自身边界换算，数据仍是 0～N-1，没有增删动作帧。

`PlayableAnimationPlayback` 用多片混合替换双槽提升：换片时记录当前各片权重，在 `crossFadeSeconds` 内线性插值到目标。中断过渡不会先把旧目标提到满权；淡出完成释放旧片，同一非空时钟组内反复选择同片会复用 Playable。普通 Play 不传组时仍从头起播。

`IAnimationPlayback.Play` / `CharacterAnimationService.PlayClip` 的可选 `timeGroup` 仅表示表现层共享时钟，不进入模拟/复制。`ActionInputMovementPlayer` 为 FollowActionSegment 的每个 Action 段生成独立身份，方向切换和松手沿用；新动作、跨段、restart 重建。Seek 同步当前组内全部淡入/淡出片段，保留其它组的时间；FromDirectionChange 不共享时钟。0.5 秒配置及窗口资产不变，卡肉仍暂停权重推进。

回归测试：`PlayableMovementBlendTests` 覆盖中断权重、反复左右/松手、片段复用释放、组间时间隔离与冻结/硬切；`ActionInputMovementTests` 覆盖调用端时钟身份生命周期。Unity Test Runner 与 Vivian 实机效果仍待验收。

## 变更日志

### EndpointSigned 动作轨迹（2026-10-01）

功能：逻辑轨迹沿完整 Action 的起终点连线移动，模型保留偏离连线的摆动，正常播完时不再因横向残差归零产生回移。

实现：`RootMotionBakeUtility` 仍保存原始 XZ 毫米差分；`ActionMotionBakeService` 完成播放范围裁剪与多段拼接后，`ActionBakedMotion` 以整表累计终点 E 为轴，对每个累计位置 P 求 `G = E * dot(P,E) / dot(E,E)`。比例不钳制，保留后退和越过终点后的回撤。累计位置取整后再差分，残差为 P-G，末帧严格为零；E=0 时 G=0。

参数与迁移：`EndpointSigned=2` 替换原 ForwardSigned，无旧算法分支；`FullPlanar=0` 保持完整轨迹。原模式 2 的就绪表不需要重烘焙，模式 0 不自动切换。未修改任何动作资产或 Prefab。`ServerContentManifest` 增加算法指纹标记，新旧构建不得混用。

运行时：`TryGetDelta` 供电机与吸附路程使用，`TryGetVisualResidualMm` 供本机/Observer 模型使用；Editor 轨迹预览共用查表。原始烘焙校验同时检查 XZ 数据，避免遗漏投影排除的分量。

限制：闭合保证针对完整 Action 的本地烘焙轨迹；提前取消仍使用既有 BlendToZero，同一 Action 中间片段边界不保证残差为零；运行时朝向变化、碰撞、吸附及骨骼姿态差异不由该投影消除。相机会跟随动作的净侧向移动，但不跟随垂直于端点轴的视觉摆动。

相关文件：`Domain/Simulation/Motion/ActionBakedMotion.cs`、`ActionMotionPlanarMode.cs`、`ActionMotionAdhesion.cs`，`Editor/Combat/Motion/ActionMotionTrajectorySceneDrawing.cs`、`RootMotionBakeUtility.cs`，`App/Networking/Content/ServerContentManifest.cs`（均位于 `Assets/Scripts/`）。独立 C# 编译及 ActionBakedMotionTests/ActionMotionAdhesionTests 共 25 项通过；Unity Test Runner 与 Attack_End→Idle、连招取消、联机表现仍待验收。

- 2026-10-01：EndpointSigned 替换 ForwardSigned（序列化值仍为 2）；整张 Action 表投影至起终点连线，累计量化后差分，完整末帧残差为零；预览/吸附共用查表，Join 指纹隔离旧算法。独立 .NET Framework 编译及 25 项纯逻辑用例通过，Unity 编译/Test Runner/Play 待验收。

- 2026-10-01：输入移动支持保留现有混合权重的连续换向，并同步同段淡出片的 Action 时钟；移除双槽提升路径，新增 Playable 图回归测试，Unity 验收待完成。

- 2026-10-01：Action 输入移动增加帧范围、方向驻留与 Loop/Hold；复用 Motor 碰撞并复制方向时钟，Owner 重放已解析的输入请求。独立编译及 35 项纯逻辑测试通过；Unity 验收、资产布线未完成。

- 2026-10-01：新增 Cancel→Entry 隐式路由与双击加点、拖动布线；补充编译通过，Unity 交互与 Test Runner 待验收。
- 2026-10-01：ActionGraph 自动过渡目标改为逐规则端口连线，支持顺序组子节点和既有数据回显；移除手填目标 UI。新增定向测试，Unity 编译、Test Runner 和 Play 验收待执行。

| 日期 | 变更 |
|------|------|
| 2026-09-19 | W11 证据审计：旧 `FakeActionGameLoopbackTests` 已随 V1 清理删除；V2 FakeActionGame / 10+ Actor / 兴趣与 Owner 预算证据需重建后才能关闭 R2 |
| 2026-09-19 | 三人换人 / 接触弹刀 P-SW0～P-SW2、P-PR 与卡肉 Graph/资产/Play 用户验收完成；Lock-On 暂时舍弃；学习与工程实践轨提前 |
| 2026-09-19 | 结构稳定化方案完成：用户验收关闭 CS0～CS7、Safety、统一门禁与总回归出口；后续转入 Network Reliability / 功能修复 Backlog |
| 2026-09-18 | Safety：`InputFrameBuffer` 硬上限 64 帧；`ApplySnapshot` 失败返回 Rejected；Recover 500ms 冷却重试；恢复后成功 ForceFull 可重新发布 Meta |
| 2026-09-18 | 结构稳定化 CS7：新增统一 BatchMode 结构/内容审计与根目录 `ci.ps1`，增加 450 行职责门禁；删除 Locomotion Legacy 字段/Baker 和五个一次性 Combat/Action 迁移器 |
| 2026-09-18 | 结构稳定化 CS6：新增 Action/Character Presentation Sink 与 Headless Null Sink；Hitbox/位移归 `CharacterActionGameplayStep`，具体 Playable/VFX/SFX/VisualResidual/RemoteProxy 和 Actor Factory 迁入 App |
| 2026-09-18 | 结构稳定化 CS2B：Character 专属 Combat 集成与 Party 归 Character，Camera 归 Combat；新增 Combat/Character/Enemy 终态 asmdef 并删除 Domain.Gameplay 粗程序集 |
| 2026-09-17 | CS2B 预迁移门禁：新增行为树 SerializeReference 强制重序列化与 YAML 审计，先统一 Assembly-CSharp/Gameplay 两批资产再切换终态 Enemy 程序集 |
| 2026-09-17 | 结构稳定化 CS5.3：新增 Client Runtime Configuration 单次加载 InputAction；GameplayIntent 归冻结 Catalog 并纳入指纹；删除两套静态 Settings、Editor 首项 fallback 与迁移器 |
| 2026-09-17 | Content 校验诊断：Locomotion Timing/RootMotion 错误输出 Profile、AnimationProfile、Clip、轨帧数与引用方，Console 上下文直接指向可修复的 Profile 资产 |
| 2026-09-17 | 结构稳定化 CS5.1/5.2：新增 `GameContentBootstrap.ValidateAndBuild` 与冻结 `GameContentCatalog`；在场景装载完成后单次 Build，集中校验全部 CombatMode/Graph Action、60Hz 动画段、Locomotion Timing 与启用的 RootMotion；Capture/Join 删除动态登记，并移除三条旧入口 |
| 2026-09-17 | 结构稳定化 CS4 收口：`DedicatedServerRuntime` 锁定为 Session/Match/Poll/Flush 宿主；运行时 Controller 删除 Scene Find 与自行创建 World 的旧 fallback，统一走 Composition Root、同物体组件或 Architecture/Simulation 注册表 |
| 2026-09-17 | 结构稳定化 CS4：`ActClientRoomGameplay` 拆为 `OwnerPredictionCoordinator`、`ObserverReplicationCoordinator`、`ReplicatedFeedbackCoordinator`；Room Gameplay 收敛为 Client 组合门面 |
| 2026-09-17 | 结构稳定化 CS4：`DedicatedAuthorityWorld` 拆为 `AuthorityGuestRegistry`、`AuthorityStepCoordinator`、`AuthorityReplicationPublisher`；World 收敛为 `IDedicatedAuthorityWorld` 组合门面 |
| 2026-09-17 | 结构稳定化 CS4：提取 `PlayerPartyRuntime`，集中本机三槽创建/释放、预测切人、死亡接替与权威槽同步；`PlayerController` 删除 Party 数组、协调器和旧转发方法 |
| 2026-09-17 | 修复 Observer 移动取消表现：正常 Action→Locomotion 使用默认 CrossFade，不再因离开动作或进入 Start 而零时长硬切；播放头吸附仍保持硬切 |
| 2026-09-17 | 结构稳定化 CS3：提取 `CharacterPredictionRuntime`，独占权威 Locomotion Restore、未确认输入 Replay 与动作 ACK 取消；Owner Adapter 不再把 Actor 当 Replay 实现 |
| 2026-09-17 | 结构稳定化 CS3：提取 `CharacterPartyLifecycle`，集中槽状态、普通退场、Assist 队列/卡肉转移、动作起手事件和换人落位；调用点迁移后删除 Actor 旧入口 |
| 2026-09-17 | CS2A 边界收口：生产 asmdef 改为逐程序集精确引用白名单，未知模块和白名单外同层引用默认拒绝；Gameplay 意图上下文恢复 `internal` |
| 2026-09-17 | 结构稳定化 CS3：提取 `CharacterSimulationPipeline`，集中固定帧 Step/PostCombat 顺序、输入快照和动作横移调试采样；`CharacterActor` 保留 World 契约入口 |
| 2026-09-17 | CS2A 数据迁移：行为树具体节点声明从旧 `Assembly-CSharp` 到 `ACTGame.Domain.Gameplay` 的 `MovedFrom` 映射；新增生产行为树 ManagedReference 完整性测试，资产本体保持只读 |
| 2026-09-17 | 结构稳定化 CS2A：建立 Core/Input/Gameplay/Infrastructure/App 编译边界；Input 角色条件改为注入；`BufferedIntentDebug` 下沉 Simulation；VFX 卡肉改为 App→VFXManager 单向端口 |
| 2026-09-17 | Observer 首见新动作时补齐起手 VFX/SFX，修复弹刀成功特效漏播；Idle 在无持续 Snapshot 时按渲染时钟循环；Timing Audit 增加 Root Motion 有效性检查 |
| 2026-09-16 | 修复 Owner 弹刀跨通道身份竞态；远端播放头有限追赶、缓冲不重复扣 RTT/2、Clip 跟播放 Tick；Idle Capture 固定 PhaseFrame=0；非 Urgent 30Hz；Snapshot Mux 丢旧下沉到应用层 |
| 2026-09-16 | Locomotion L0/L1/L2：PhaseFrame 唯一时钟；ClipTiming/Gait/落脚/Start/Pivot/RootMotion 全帧化；新增手工 Baker/Audit，V1 不复用旧时间字段 |
| 2026-08-31 | Party P-SW0 / P-SW1 骨架：CharacterId/Definition/Loadout、单键 SwitchCharacter、顺序槽位协调器；PlayerController 切到 Loadout，三 Actor 与联网尚未接 |
| 2026-09-01 | Party P-SW1：每槽稳定 Actor/网络实体、Active/Exiting 输入与显隐、SwitchIn 外部意图、Owner 阵容载荷、累计切人 ACK 和 Debug HUD；Editor Graph/Test/Play 待验 |
| 2026-09-01 | Party 普通退场改为双规则：空闲播完整 SwitchOut；有招进入首次 Recovery 后停止并隐藏，不再等待整个 Action |
| 2026-09-01 | Party 退场最终规则：所有普通退场均进入 SwitchOut；原招 Recovery 仅负责切入，只有 SwitchOut 自身 Recovery 可隐藏 |
| 2026-09-01 | Party 普通登场落点改为旧角色局部右侧 600mm；客户端预测与 Dedicated 权威共用确定性算法，并经静态碰撞解析 |
| 2026-09-01 | 相机检测 Active 角色表现根切换后短暂启用 0.04s 快速平滑与完整横向跟随，0.2s 后恢复日常参数 |
| 2026-09-02 | 修复切人 VFX 泄漏：已处于 Recovery 的退场原招不再多推进一帧；RemoteProxy 新动作/重启不再从 frame -1 补播整段历史 Notify |
| 2026-09-02 | 修复 Observer 二次登场残留：远端角色隐藏前回收所属 VFX；重新显形时清空退场前插值历史并直接落到当前权威位置 |
| 2026-09-02 | 本机阵容生命周期接入同一可见性清理接口：`CharacterActor` 转入 Inactive/Dead/Empty 前回收所属 VFX，避免本机再次 SwitchIn 时复活旧特效 |
| 2026-09-02 | 修复 P-SW1 后敌人感知根停在出生点：`RemotePlayerSeat` 使用稳定锚点并在普通切人时重挂到当前权威槽位根 |
| 2026-09-06 | 被弹选片走 `Parried+Id`；`ParriedActionPolicy.Continue` 不停招；Success 二次接触不重切 |
| 2026-09-06 | 弹刀卡肉帧改读招架窗；新增本体 `Parry` 意图（不走切人） |
| 2026-09-05 | 弹刀卡肉：Pipeline 结算后唯一 `RequestHitStop`；双方冻，Success 续冻；事件拆开 PD/弹刀 |
| 2026-09-05 | F3 Party 行显示支援点；上限/开局/消耗/Ult 回复由 PartyLoadout 配置 |
| 2026-09-05 | Hurt 中普通切人：交接 SwitchOut 前必须离开 Hit，避免意图被丢掉、槽永久 Exiting |
| 2026-09-05 | Observer 收招钉招尾：片子只跟播放头；最新快照不再提前 Play(Walk)；落到走跑必须再 Play |
| 2026-09-05 | Observer 走跑掉帧：删除 Listen delay=0 贴最新快照；改回 RemotePlaybackClock delay=1；走跑相位改为 Urgent |
| 2026-09-05 | Observer 出招改为 Tick 走片：仅切招/回绕或偏差超约 1 逻辑帧 Seek，避免整数帧台阶掉帧 |
| 2026-09-05 | Observer 出招表现对齐采样快照：Listen delay=0 + Host alpha；远端仍按 RTT 播放头；Clip/残差不再墙钟空跑 |
| 2026-09-04 | P-SW2 代码已接：两条 Action（Guard + Success）、Cue/点数、`IssueParried`、突击派生；Play 待验 |
| 2026-09-04 | 受击 P-HR0～P-HR4 全计划 Play 验收关闭；Listen 客机 Flinch Additive 已验 |
| 2026-09-03 | 受击档改为冲击力对韧性；删除 `desiredReaction`；不足 Flinch，持平起 LightStun |
| 2026-09-03 | P-HR3：`baseInterruptResist` + Phase `interruptResistBonus` 进 Service；OnValidate/菜单只补空字段；轻击 Play 已验 |
| 2026-09-03 | P-HR2：Flinch 不停招、不锁 Locomotion；`HitFlinchPlaybackController` 只 `PlayAdditive`；Stun+ 仍 EnterHit |
| 2026-06-17 | 初版：移动、输入、状态机、动画、相机、Prefab 文档化 |
| 2026-06-17 | 动作系统 §7：ComboSequence、CombatMode、ACTION_EDITOR 对齐摘要 |
| 2026-06-21 | ActionEditor 准备重构：CharacterActionDriver、UpdateFrame、Phase/Event 骨架、命中回流 |
| 2026-06-23 | QFramework 风格架构改造：CharacterActor、ActionExecutor、ACTGameArchitecture、ApplyHitCommand、AttackHitEvent、TargetSystem |
| 2026-06-29 | QFramework 式强类型契约：System/Controller/Command/Query/Event 基类与 Editor 边界校验，命中与索敌 Domain 入口移除架构单例依赖 |
| 2026-07-05 | 动作系统 Resolver 重构：`ActionResolver`(Single/Combo/Directional) + `ActionResolverService` 承接起手/连段/Dodge 方向/Cancel 选招；`ActionExecutor` 收敛为纯播放器；`Combat/Actions` 分 Definitions/Resolution/Execution/Frames；`IActionComboInput`→`IActionInputBuffer` |
| 2026-07-09 | ActionNotify 时间轴重构：新增 `ActionTimeline` / `ActionNotify` / `ActionNotifyState` / `ActionTimelineRunner`；Hitbox/VFX/Cancel/Movement/Rotation 改为统一 Timeline 数据真源并删除旧字段路径 |
| 2026-07-10 | VFX/SFX 改为区间窗口（`naturalDurationSeconds` / `playbackSpeed`）；新增 `ActionEditorWindow` 手动加轨与拖拽编辑；`ActionVfxPlayer` 改窗口 Enter/Exit 消费 |
| 2026-07-13 | VFX/SFX 改回点事件：显式 `playbackSpeed`、`PlayVfxNotify.attachPointId`、`CharacterAttachPointResolver`、`ActionSfxPlayer`；删除窗口派生倍率路径 |
| 2026-07-12 | 动画改为 Clip + 薄层 Playable：`IAnimationPlayback` / `PlayableAnimationPlayback`；Profile 映射 Clip；HitStop 走 `SetSpeed`；废弃 Animator Controller 业务依赖 |
| 2026-07-12 | `ActionDefinition` 多段 `ActionAnimationSegment[]`：同招顺序播多 Clip；`ActionExecutor` 段边界自动切；旧 `animationClip` OnValidate 迁入 segments |
| 2026-07-13 | 相机方案 B：`CameraOrbitPivot` 对 `CameraRoot` SmoothDamp；LookAt 改为 `orbitPivot`；新增 `followSmoothTime` / `SnapFollowToTarget` |
| 2026-07-16 | VFX：连招切招不再强制回收；`VfxPooledInstance` 按自然生命周期（含 playbackSpeed）自行回池 |
| 2026-07-18 | Locomotion Phase/FootCycle：`LocomotionService`（Start/Gait/PivotTurn/Stop）、落脚脚步、`ApplyLocomotion` |
| 2026-07-18 | 拆分 Run/Sprint：满输入先进 Run，持续 `sprintAfterRunSeconds` 后 Sprint；Pivot 仅 Sprint |
| 2026-07-18 | Locomotion 方案 B：Stop/Pivot 烘焙根位移轨（`LocomotionRootMotionBaker`）+ 运行时采样驱动 |
| 2026-07-19 | Stop 全程可取消进 Start；移除 `stopCancelNormalized`；Pivot→Stop 用转身目标朝向 |
| 2026-07-19 | Start 急停播 `StartEnd`（Run_Start_End）；Gait/Pivot 仍用 StopL/R；烘焙轨含 StartEnd |
| 2026-07-19 | 输入语义化：GameplayIntentProfile/Producer/Buffer；Action Trigger 改为枚举；SprintAttack、PressedThenLong、Dodge 后直入 Sprint |
| 2026-07-19 | 动作优先级打断：`interruptPriority` + `TryInterrupt`；Action 态高优走 Graph Entry 硬切；CancelWindow 连招路径不变；`IsInterruptibleAtFrame` 无 Phase 默认可打断 |
| 2026-07-19 | `DirectionalActionResolver` 改为统一六向闪避解析；删除纯左/纯右字段及 Locomotion 起手强制前闪/转向旧路径 |
| 2026-07-19 | Action Graph 可视化节点新增 `Variant Resolver` 编辑与保存；修复 Graph Editor Save 未写回 Resolver 引用的问题 |
| 2026-07-22 | TurnBack 固定锁根 0.08 秒后，将实时输入相对初始折返输入的方向差叠加到角色根；避免绝对输入朝向与 Clip 自带约 180° 转身重复累加，烘焙位移同步重定向 |
| 2026-07-22 | Locomotion 内层改为纯状态机：删除 `LocomotionService`；新增 `LocomotionStateMachine` / `LocomotionContext` / 五相位 State；`CharacterContext.LocomotionStateMachine` |
| 2026-07-23 | `ActionSfxPlayer`：专用 `ActionSfx` AudioSource；`OnActionEnded`（打断/切招/自然结束）`Stop` 未播完动作音效 |
| 2026-07-22 | 新增 `GameplayIntentType.AttackRelease`（攻击键松开语义）；供蓄力释放等 Action.Trigger 使用；Profile 需映射 Released→AttackRelease |
| 2026-07-23 | Cancel 同槽多缓冲意图按 `GameplayIntentCancelPriority` 降序解析（LongPressedAttack &gt; Attack），避免连段边抢赢蓄力 |
| 2026-07-23 | 蓄力修复：自动 Transition 回写 Graph 游标；连段 Cancel 保留 LongPressedAttack；Locomotion 起手清残留 AttackRelease 防秒放 |
| 2026-07-25 | ActionGraph 稀疏路由：显式边仅保留独特拓扑；新增 SharedRoute、Recovery Phase→Entry、Directional 逻辑节点；删除 Recovery Cancel 与 ComboResolver；输入缓冲增加 0.15s 过期 |
| 2026-07-25 | Phase 收敛到 `ActionTimeline.phaseStates`；Action Editor 开放 Phase 轨；Recovery 窗口集成移动取消与 Entry 重开；删除独立 `ActionPhase` 数据路径 |
| 2026-07-25 | Action Editor 手动轨道支持拖拽换序：轨头手柄、插入线、松开写回 `timeline.tracks`，完整支持 Undo |
| 2026-07-26 | Perfect 独立窗口：CancelWindowType=Normal/Perfect；允许重叠，同一 Trigger 优先 Perfect；删除 perfectFrame 分割路径 |
| 2026-07-25 | 新增 DodgeAttack 语义：GameplayIntentProfile 通过 IsDodging 条件将闪避 Action 中的 Attack Pressed 映射为闪避攻击 |
| 2026-07-29 | 敌人系统接入：EnemyDefinition/BrainProfile、五态 AI、AIInputSource、共享 CharacterActor、伤害/Hit/Death 闭环、Spawn/Despawn 与玩家对称 Hurtbox |
| 2026-07-29 | 敌人联调修正：EnemyDefinition 独立持有 teamId；默认 Hurtbox 中心抬高；CharacterConfig 增加玩家受击/死亡 Action |
| 2026-07-29 | 动作配置收敛：删除 EnemyDefinition 的 hitStunAction/deathAction；玩家与敌人统一读取 CharacterConfig.Combat |
| 2026-07-29 | 命中反馈修正：Hit 与实际扣血解耦；自击过滤覆盖完整角色层级；玩家镜头只响应玩家主动命中 |
| 2026-07-29 | ActionDefinition 职责重构：输入/索敌/起手/自动衔接迁到 ActionGraphNode；伤害与反馈迁到 HitPayload；Controller 通过 CharacterReactionResolver 选择受击/死亡 Action |
| 2026-07-30 | 角色反应链路收敛：CharacterReactionService 统一玩家/敌人 Health 事件；Resolver 直接产出状态请求；默认硬直时长归 CharacterReactionSet，删除 CharacterConfig/EnemyBrainProfile 双真源 |
| 2026-07-30 | Graph Editor 增加节点内联策略编辑；命中去重改为每个 Hitbox 窗口×目标一次；HitState 支持每次有效命中强制重入并保留启动失败硬直回退 |
| 2026-07-31 | Lockstep L0A：新增 60Hz SimulationHost/World、稳定 SimActorId 与纯 C# asmdef；玩家/敌人删除分散 Controller Tick，渲染输入边沿先汇聚再由固定帧消费 |
| 2026-07-31 | 修复 L0A 移动抖动：新增 CharacterPresentationBridge 前后 Pose 插值与表现锚点，相机改跟随插值根；SmoothDampAngle 显式使用固定逻辑步长 |
| 2026-07-31 | 修复固定帧后攻击转向变慢：ActionRotationDriver 不再隐式读取 Time.deltaTime，并在退出 Action 时清空旧旋转速度 |
| 2026-08-01 | Lockstep L0B：删除 PlayerInputFrame/ICharacterInputSource/AIInputSource；新增量化 InputFrame、输入历史与 World Input Produce 阶段；Hold/Buffer/AI 冷却改整数帧 |
| 2026-08-01 | Lockstep L0C：Hitbox 改为 Collect→稳定排序→帧末 Resolve；新增 SimHitKey/PostCombat，删除 ApplyHitCommand、InstanceId 去重与 Event→ActionExecutor 卡肉回写 |
| 2026-08-01 | Lockstep L1A：ActionSession 整数帧权威、ActionFrameClock 30→60 整数换帧、单次 Action Step、下一 World 帧切招，以及 Hit/Death 整数帧收尾 |
| 2026-08-01 | Lockstep L1B：纯 `ActionSim` + Snapshot/Event 表现边界、共享 `ActionFrameQuery`、60Hz 迁移工具；删除 ActionExecutor/Session 与 30Hz Runtime 路径 |
| 2026-08-02 | L1B 收口：确认全部 ActionDefinition 为 60Hz；新增 Validate Readiness；Editor/VFX 默认采样率改为 `ActionSim.LogicHz` |
| 2026-08-02 | L2/M0：`ActionBakedMotion` + 双文件夹命名匹配烘焙（`ACTGame/Motion/Bake From Folders...`）；不生成 InPlace |
| 2026-08-02 | L2/M1：表现桥查表位移；表就绪禁用 OnAnimatorMove；`ActionMotionRuntimePolicy` |
| 2026-08-02 | 运动表取消烘焙/施加 yaw；朝向仅 ActionRotation（索敌/输入）；位移烘焙不再用 RootQ 投影 |
| 2026-08-02 | L2 HitStop：`ActionSim.freezeFrames` + Pipeline `RequestHitStop`；删除 HitStop 秒制倒计时 |
| 2026-08-02 | L2 Locomotion：`LocomotionRootMotionTrack.TryGetFrameDelta` + Player 整数帧；删除 NormalizedTime 位移权威 |
| 2026-08-02 | L2 MotorSim：`CharacterMotorSim` 水平权威；Locomotion/动作表/RM 经 Motor；CC 仅临时重力与 XZ 跟随 |
| 2026-08-02 | 锁步方案定案：角色互撞软弹开；联网完整预测回滚（撤销「仅齐帧」非目标） |
| 2026-08-02 | L2 软弹开落地：`SoftBodySeparation` + World 帧末；CharacterActor/EnemyHandle 参与 |
| 2026-08-02 | 软弹开质量比 + `softBodyImmovable`（大体型怪像墙） |
| 2026-08-02 | L2 逻辑 Hitbox：`SimCombatPose` + MotorSim 根；删除 Transform 世界盒权威与层级自伤判断 |
| 2026-08-02 | Action Editor UX：点事件菱形、时间轴 Zoom（含 Ctrl+滚轮）、VFX Scene 预览按 Scrub 帧多实例驱动（无需选中窗口） |
| 2026-08-04 | Action Editor UX：playhead 自动跟视口、Create 选文件夹+默认命名、左侧列表按文件夹分组 |
| 2026-08-04 | L2 静态碰撞：`SimStaticCollisionWorld` + `StaticCollisionBake` Editor 烘焙；Host 共享 CollisionWorld |
| 2026-08-04 | L2 重力迁出 CC：`CharacterMotorSim` 竖直权威；`CharacterMotor` 只 Sync 根位姿 |
| 2026-08-04 | L2/M2：Bake Dirty Only、Dirty 指纹黄条、Validate Motion Dirty 菜单 |
| 2026-08-06 | Wave 0：`ActionMotionSourceClassifier` 全库审计；烘焙轨迹 Scene 预览；Motor/相机锚点 Gizmo；`CombatDebugHudController` + `CharacterDebugSnapshot`（N0） |
| 2026-08-06 | Wave 1：`ForwardSigned`；`ActionBaseMotionMode`+迁移；相机 `lateralFollowFactor`；Motor 读 Orbit `PlanarForward` |
| 2026-08-06 | Wave 2 核心：`CharacterVisualMotionRoot` + 残差派生；Gameplay→Motor，Residual→模型；未删 RM 回退 |
| 2026-08-06 | Wave 3：`CharacterResourceSim`/Gate/Spec；Pipeline GrantOnHit；`Special`/`Ultimate` Intent；同键 EX 选形；HUD Next Special；卡肉跳过资源 Step |
| 2026-08-07 | GAS G1：`Domain/Combat/Numeric`（AttributeSet/Aggregator/Flags/NumericSystem）；EditMode `NumericSystemTests`；未接 Actor/Pipeline |
| 2026-08-07 | GAS G2：`EffectDefinition`/`EffectContainer`（Instant/Duration/Periodic + 叠层）；`NumericDebugSnapshot`；`EffectContainerTests` |
| 2026-08-07 | GAS G3：`NumericCostGate`+Spec 编译器；Factory/Host/Pipeline/Vitality；删 ResourceSim/旧 Health；完美窗/无敌早退 |
| 2026-08-07 | GAS G4：`DamageNumericCalculator`；Outgoing/IncomingDamageMult；DOT Health handler 无 Reaction |
| 2026-08-08 | GAS G5：旧权威删除确认；Snapshot/HUD（Effects/Flags/ATK）；文档完成态；Resources 仅作者壳 |
| 2026-08-08 | Wave 3.4：`PerfectDodgeAttack` Intent；Producer 缓冲内劫持攻击键；Begin 清 Flags；Cancel 优先级 93 |
| 2026-08-08 | Wave 2.5：删 Action `useRootMotion`/`LegacyResolve`/`ForwardOnly` 与 Animator RM→Motor |
| 2026-08-08 | A2：`HitFeedbackSettings` 受击 VFX/SFX；`HitImpactController` 订 `AttackHitEvent`；PD 吞伤跳过 Cue |
| 2026-08-08 | Action Editor：同类型多选窗口支持右侧属性批量应用 |
| 2026-08-08 | Action Editor：轨道路面拖拽框选多窗口 |
| 2026-08-08 | `ActionSfxPlayer`：打断/结束改为 0.1s 音量淡出（`ActionSfxFadeDriver`） |
| 2026-08-08 | 受击 Cue：接触点=攻击盒中心→Hurtbox 最近点；随机旋转；F4 Hurtbox 线框 |
| 2026-08-08 | 动作 SFX 多声道淡出（连招不掐断） |
| 2026-08-08 | Action Editor：Scrub 展示烘焙根运动轨迹/位移；右侧 Inspector 纵向滚动 |
| 2026-08-09 | Hitbox `parentToAttachPoint`：世界空间冻结盒（对齐 VFX）；编辑器预览同步 |
| 2026-08-09 | Wave 4 P0～P2：TargetAdhesion（连线动态+剩余帧均摊）+ SoftBodySuppress 接线；Editor MotionModifier 轨；Relocate 未接 |
| 2026-08-09 | Action Editor：选中 MotionModifier 时 Scene 假敌球 + Adhesion 修正轨迹/预览根 |
| 2026-08-09 | TargetAdhesion 方案 A：只补朝向前方缺口，过冲不倒拖 |
| 2026-08-09 | Wave 4 位移切片 + 打击感优化：Branch_02 Editor 验收收口 |
| 2026-08-09 | Wave 4 P3：MotionCommand → ActionMotionResolver 接线（Relocate/SnapFacing） |
| 2026-08-09 | Wave 4 出口收窄为位移；Wave 5 撤出大招演出；LockOn/SkillShot/Finisher 全归 Camera 篇 |
| 2026-08-09 | A2 打击感（命中 VFX/SFX）验收；日计划下一项改为 A5 BT |
| 2026-08-09 | BT-1：`IEnemyBehaviorRunner` + 自研近战树；删 EnemyBrain Idle/Chase/Attack switch |
| 2026-08-09 | BT-2 调试：NamedNode 路径、EnemyController Gizmo/日志、Create/Validate 菜单 |
| 2026-08-09 | BT-1 Play 验收；BT-2 Custom SerializeReference 节点定义 + Inspector Fill |
| 2026-08-09 | 修复 Action→Idle 模型抖动：`BlendToZero` 期间禁止 `ApplyLogicLocalPose` 回写；回锚结束前后快照一并清零；新动作起手取消未完成 Blend |
| 2026-08-09 | VFX 池化支持 Animator：Spawn Rebind/从头播、寿命取粒子与 clip 较长者、playbackSpeed/卡肉同步 `Animator.speed`；导入 C1 包至 `Assets/Resources/Effect-C1/` |
| 2026-08-09 | Action Editor VFX 预览：`SampleAt` 同步粒子 Simulate 与 Animator Clip 采样；`Restart` 对齐 playOnAwake + Animator 从头播 |
| 2026-08-09 | BT-E1：`AIInputWriter` 多按钮脉冲；`CooldownTable`/`CooldownGate`；BackOff/Strafe/PulseDodge；Kite 预设；删单字段攻击 CD 与 `BasicAttackCooldownReady*` |
| 2026-08-09 | BT-E2：`EnemyBehaviorGraphLayout` + `GraphMapper` Flatten/Rebuild + `Validator`；Custom `Wrap` 始终带 Debug 名 |
| 2026-08-09 | BT-E3：`EnemyBehaviorTreeEditorWindow` GraphView MVP（调色板/连线/Inspector/Save/模板/Play 高亮） |
| 2026-08-10 | 文档：敌人 AI 输出槽终态改为 Desire+Request；OPT B1/B3 并入 8.10；代码仍为 AIInputWriter 过渡 |
| 2026-08-10 | E-CFG1：BT 节点自带距离/幅度；AggroGate；薄 BrainProfile；删黑板 Profile 读参 |
| 2026-08-10 | E-ST1：DistanceBand 滞回条件 + CreateMeleeStanceLoop 样例 |
| 2026-08-10 | E-REQ1：EnemyCombatRequest + RequestCombatAction + Driver 按 Entry 起手 |
| 2026-08-10 | E-REQ2：RandomSelector + CreateCombatPool；删除敌人 PulseAttack/AttackPulse 路径 |
| 2026-08-10 | BT Editor：RequestCombatAction Entry 下拉；Graph 只读反查 EnemyDefinition→CombatProfile（删 EditorActionGraph / Action 反查） |
| 2026-08-10 | E-MOVE1：EnemyLocomotionDesire + Buffer；Brain 停 SetMove；Actor 覆盖 MoveIntent |
| 2026-08-10 | E-MOVE2：删除 AIInputWriter 与 PulseDodge/Heavy/Skill；ProduceInput=Empty |
| 2026-08-11 | 命令源分层：LocomotionDesire / ActionEntryRequest 上提为通用契约；CharacterActor 删除 Enemy Buffer 与 InputManager 覆盖路径 |
| 2026-08-11 | 对峙循环：CdReady Chase / CdNotReady Strafe；CooldownGate/Dwell/Wait 节点改秒制 |
| 2026-08-11 | L-DIR4：`SprintLeanModel`→`VisualMotionRoot` Roll；L-DIR5：`CameraManager` yaw 跟朝向绕圈 |
| 2026-08-11 | FollowInput：水平位移沿当前朝向；与 `RotationSmoothTime` 单参决定 W→WD 转向时长 |
| 2026-08-12 | PivotTurn 两段式：AnimAuth（bake pos+yaw）→ InputAuth（FollowInput）；删 PivotTarget/偏移转向 |
| 2026-08-12 | L-DIR1：`FacingMode` + `LocomotionAnimSet` + `DirectionModel`；循环选片不再硬编码 WalkLeft/Right |
| 2026-08-12 | L-DIR2：AnimSet Start 表；`ActiveStartGait`；Gait `cardinalMinDwellFrames` 滞回 |
| 2026-08-12 | L-DIR3：FaceTarget 旋转+软锁；本地 cardinal；锁定禁 Pivot；相机跟朝向关闭 |
| 2026-08-12 | Locomotion Play 验收关闭（L-DIR1～5 + Pivot）；旧案 Phase D 减速曲线不做 |
| 2026-08-13 | C-AT0～3 代码重构：MoveReferenceYaw 入 InputFrame；唯一 SelectedTarget + Action 中切敌；删除 PlanarBasis、CombatTargetLock、ActionTargetId 与表现 late-bind |
| 2026-08-13 | FollowMove 不再因 SelectedTarget 自动升格 FaceTarget；玩家锁面仅攻击窗口/显式 FaceTarget Profile |
| 2026-08-14 | 联网主路径更正为状态同步（删 TECHNICAL 内过期 FramePacket 句）；补服务器代码规范索引 |
| 2026-08-14 | NS0：`ILocalPlayer` / `LocalPlayerService`；敌人感知最近玩家根；玩法删除 Find 唯一 PlayerController |
| 2026-08-14 | NS1：复制 Snapshot/Tick/Command + Codec + Loopback；EditMode 往返与 60 帧单调 |
| 2026-08-14 | NS2：RemoteProxy + Loopback 同机幽灵；Host AfterLogicStep 打包；不 Collect |
| 2026-08-14 | 朝向调试箭头解耦为 `ICharacterFacingDebugTarget`；幽灵同步黄/品红箭（wish 走 moveV*） |
| 2026-08-15 | 幽灵 Locomotion：复制归一化时间并硬切 Seek；工厂关掉 Animator RM |
| 2026-08-15 | NS3：PredictedLocomotionDriver + 纠偏单测 + 同机预测预览；Host 不预测 |
| 2026-08-15 | NS3 表现：FollowInput 转向、Sprint 倾身拷贝、出招/转身贴齐权威以免 10Hz 吸附 |
| 2026-08-15 | NS3 Play 验收关闭；NS4：PredictedActionDriver + 命中/生命边沿复制 + 敌人幽灵 |
| 2026-08-15 | NS4 Play 验收关闭；NS5：UDP 房间 + Listen Host/Client + 稳定 actionId + 10s 空闲剔除 |
| 2026-08-15 | NS5：ParrelSync 克隆自动当 Client（反射探测，不硬引用包） |
| 2026-08-15 | NS5 客机：FrameHint 不再当权威帧丢包；相机改跟预测体；本机走跑/起手 Clip |
| 2026-08-15 | NS5 客机手感：渲染帧合并输入、命令批冗余、CarryForward 不下发旧 Hint、转身/出招贴齐、走跑 Tick |
| 2026-08-15 | NS5 客机表现：单步 Apply、Proxy 过点 VFX/SFX、命中下行落点、克隆端受击 Cue |
| 2026-08-15 | NS5 客机 Locomotion：本地 GaitPolicy 升 Sprint；松手等权威 Stop；Idle↔走跑淡入 |
| 2026-08-15 | UE1：客机本机 `AutonomousLocomotionRunner`；删除房间/预览 `ResolveSelfKey`；纠偏相位重放仍待 UE2 |
| 2026-08-15 | UE1 复验：走跑禁止 `SyncRootPoseFromSim`；Prefill 含 Directional 变体；走跑硬吸 2m |
| 2026-08-15 | UE2：`LocomotionSavedState` + `IPredictedLocomotionReplay` Restore/Replay；闪避后 SprintAfterDodge |
| 2026-08-15 | UE3：删除 `PredictedLocomotionVisual` 猜片；相位判断并入 `ReplicationPresentationAlign` |
| 2026-08-15 | 客机 L-DIR5：`HasMoveIntent` 走设备采样；纠偏后 `SnapPresentationToSimulation`；吸附宽限 150mm/8 包 |
| 2026-08-15 | 走跑+Runner 纠偏默认 2m：禁止 50mm 每包 Restore+Replay（客机卡顿） |
| 2026-08-15 | 出招/闪避禁止每包 SnapPresentation；`IsPresentingAction` 时相机暂停跟朝向 |
| 2026-08-15 | UE4：`AutonomousActionRunner` 只读 ActionSim；删除 `PredictedActionDriver` |
| 2026-08-15 | 客机出招表现：自然结束不重播延迟招；连招超前不误 Cancel；权威卡肉暂停本机推帧 |
| 2026-08-15 | 按已落地代码整理网络同步实现说明（后续阅读入口迁到 `NETSYNC_FROM_JOIN_TO_HIT`） |
| 2026-08-15 | CA1：客机同一 `CharacterActor` + `ReplicationSeat.Autonomous`；删除 Runner / CreateAutonomous |
| 2026-08-15 | CA2：`RemoteCharacterProxy` 只读 ITargetable 进 TargetSystem；OnHit 空操作 |
| 2026-08-15 | 客机注入 WorldQuery：TargetAdhesion / Relocate / SoftBodySuppress 与 Host 同一套桥 |
| 2026-08-15 | 客机穿敌吸附/关碰撞窗与权威卡肉：纠偏只 Ack，禁止 2m 硬吸拉回 |
| 2026-08-15 | 客机预测卡肉：`PredictedHitStopConsumer`；删除权威 Freeze 拖时钟 / FollowAuthorityAction |
| 2026-08-15 | 删除 Host 同机预览：`RemoteGhostViewController` / `PredictedClientPreviewController` / `SetAutonomousPredictMode` |
| 2026-08-17 | NetSync W0：新增 Codec Golden Bytes / Room 执行顺序测试；F3 HUD 增加 Tick/Command 字节、Proxy 与预测 pending 基线观测 |
| 2026-08-17 | NetSync W1：新增零依赖 `ACTNet.Core` 身份/版本/结果/Metrics/有界小端 Buffer；Room/Replication Codec 切换 Core 并删除重复私有 Reader/Writer |
| 2026-08-17 | NetSync W2 Transport：新增 `ACTNet.Transport`、多连接 Loopback 与 ConnectionId UDP；Host/Client 单轨切换 `INetTransport`，删除方向固化旧接口与实现 |
| 2026-08-17 | NetSync W2 Session：新增 `ACTNet.Session`、连接/玩家注册表、Join/Heartbeat/Kick 状态机与 FakeGame 三连接测试；Composition Root 注入 Session，删除 Room 控制 DTO、握手 switch 与 IdleTracker |
| 2026-08-17 | NetSync W3 Runtime 基础：新增纯 C# `ACTNet.Replication`，提供 Version 1 Frame Codec、Schema Registry、Server full-set 生命周期差分、Client 原子应用与 Sequence 丢旧；尚未切换 Character 生产路径 |
| 2026-08-18 | NetSync W3 Character Adapter：Snapshot 字段布局收敛为 `ActorReplicationSnapshotCodec`；新增纯 C# `ACTGame.Networking`、`CharacterSnapshotSchemaV1` 与 stableKey Archetype Catalog；尚未切换 Host/Client |
| 2026-08-18 | NetSync W3 生产切换：Host/Client 单轨使用 `ReplicationFrame` 显式生命周期与 Sequence；hint/hits 迁入 V1 ApplicationPayload；删除 `AuthorityTick`、缺 Tick 即销毁和首敌配置回退 |
| 2026-08-18 | NetSync W3 出口测试：Replication Runtime 用真实 V1 Frame Codec 覆盖中间 Update 整帧丢失、乱序旧帧和双 Archetype；生产 Play 已验收 |
| 2026-08-18 | NetSync W4 Authority Adapter 首切片：远端 InputFrame 灌入、权威角色 Capture 与 FrameHits ActionId 补齐迁出 RoomHost；删除 Room 内对应 Gameplay 实现，仅保留单轨调用与 Frame/Session 编排 |
| 2026-08-18 | NetSync W4 Session Handler 切片：Guest Authority Actor 创建、App/Simulation 注册及断线逆序清理迁出 RoomHost；Room 通过最小服务委托注入 Architecture 能力，仍独占 Session Accept/Reject |
| 2026-08-18 | NetSync W4 Owner Adapter 切片：Owner ActorId 门禁、HP 覆盖、Action Ack、Locomotion Reconcile、Hit/Death 硬吸和预测历史迁出 RoomClient；Adapter 边界使用 SimActorId，避免网络身份类型泄漏到 ACT 预测接口 |
| 2026-08-18 | NetSync W4 Observer/Proxy 切片：Schema/Archetype 校验、Proxy 显式生命周期、TargetSystem 与 View 清理迁出 RoomClient；删除 Domain 旧 Factory 类型/文件，App 层 `ActRemoteProxyFactory` 成为唯一装配入口 |
| 2026-08-18 | NetSync W4 ActContentRegistry 切片：动作 Catalog、角色 Archetype 与 Unity 配置映射合并为唯一内容真源；删除 `CharacterReplicationContentRegistry`，Host/Client 与 Adapter 不再独立持有 Action Catalog |
| 2026-08-18 | NetSync W4 Character Schema Capture 切片：`ActCharacterSnapshotSchema` 统一 CharacterActor Capture 与 V1 编解码并注册到生产 Schema Registry；删除独立 `CharacterReplicationCapture` |
| 2026-08-18 | NetSync W4 Room Facade：新增 Host/Client Gameplay 与内容预填 Service；Room 删除 Character/Config/Proxy/Hit Cue/HitStop 具体实现，仅保留 Session 收发、固定帧调度与 HUD；新增 W4 架构边界守卫 |
| 2026-08-18 | NetSync W4/M1 验收关闭：Authority/Owner/Observer、Room 架构守卫、Golden Bytes 与双进程移动/战斗/CameraLock/断线回归通过；网络层分离完成，下一阶段为尚未开始的 W5 Dedicated |
| 2026-08-18 | M1 网络层分离验收关闭；实现阅读入口后迁到 `NETSYNC_FROM_JOIN_TO_HIT` |
| 2026-08-19 | NetSync W5：`ACTGame.Server` Dedicated Bootstrap / Match / 每连接 ACK；Listen Host 改 N Guest；JoinAccept 允许无房主实体；权威 World 仍属 W6 |
| 2026-08-19 | NetSync W6：`ServerSimulationRunner` + Headless `CharacterPresentationMode`；Capture 改读模拟 Locomotion 时钟；`ServerContentManifest` 指纹加入 Join；Dedicated 创建权威 Actor 并步进 |
| 2026-08-19 | NetSync W7：Dedicated Match 状态机、每连接 `ReplicationFrame`、`MatchEnd`、JoinAccept 改写 SimulationId；Owner 预测复用 W4 Adapter |
| 2026-08-19 | 修复 Dedicated 客机无法操作：Join 先于 Drain；入房立刻发首帧 Spawn，避免 Owner 被建成 Proxy 导致 `CanPredict` 不开 |
| 2026-08-19 | Dedicated Play：命令按 Hint 逐步灌入；Headless `Play` 仍记 CurrentKey；Dodge 期间推迟 2m 硬吸 |
| 2026-08-19 | Dedicated Play 复验：取消逐步灌入（观察者延迟）；Merge 进下一帧 + 首 Hint 和解；吸附/闪避整段推迟硬吸且不掐本机招 |
| 2026-08-19 | W7 Editor Play 用户验收；W8：`ServerLaunchConfigResolver` CLI/Env/File、READY、空房超时与对局结束退出（Editor 不 Quit） |
| 2026-08-19 | W8 Dedicated 出包 + H-DS-D 用户验收；M2 / LAN DS-Demo 关闭 |
| 2026-08-20 | NetSync W9：Listen = `DedicatedServerRuntime` + `LocalClientRuntime`；删除 `ReplicationRoomHost` / `ActHostRoomGameplay` 与 Host 本机 Capture |
| 2026-08-20 | W9 Listen 组合用户验收；本机预测按 `PeekAdvanceSteps` 对齐 60Hz |
| 2026-08-20 | NetSync W10 代码切面：`ACTNet.Prediction`、ChannelMux、可靠命中事件、SnapshotTimeline；出口待 Play |
| 2026-08-22 | NetSync W11 代码切面：Delta/兴趣/预算、`GraphNodeKey`、Recover、FakeActionGame；R2 出口未关 |
| 2026-09-19 | NetSync W10 用户验收完成；W11 / R2 保持开放，继续补 V2 FakeActionGame 与 10+ Actor / 兴趣 / Owner 预算证据 |
| 2026-09-19 | NetSync W11 收尾代码：恢复 `FakeActionGameV2LoopbackTests`；生产接回 `ReplicationBuildOptions.Compact`；连接级 Update 预算、Owner 优先和延后重试具备测试；生成工程 0 error，待 Test Runner / Play |
| 2026-08-22 | 远端隔步快照：时间线改为向后括号取样；Proxy 按跳过 Tick 补动画时间 |
| 2026-08-22 | 方案 B：`RemotePlaybackClock` + `TickAnimation`；不再用本机 InterpolationAlpha 取样远端 |
| 2026-08-22 | 远端战斗立即提交：判定/受击/Notify 不等播放头；Urgent 破节拍 |
| 2026-08-23 | 新增现行联网阅读入口 `docs/2026.8.23/NETSYNC_FROM_JOIN_TO_HIT.md`（Join→命中调用链） |
| 2026-08-23 | 文档整理：删除已关闭波次备忘 / 被替代方案；TECHNICAL 交叉引用改指现行入口 |
| 2026-08-26 | 相机排期交叉引用改挂 `docs/2026.8.26/CAMERA_SYSTEM_PLAN.md`（Director / SkillShot / UI 展示舱）；实现未改 |
| 2026-08-26 | L-DIR5：相机相对后退（`MoveInput.y`）暂停跟朝向，避免按住 S 与镜头互追转圈 |
| 2026-08-29 | Camera CS0～CS3 代码：Timeline Camera 轨、Rig FollowHold、Director/SkillShot 池、FOV/Dolly/Impulse、Action Editor Scene 预览；待 Editor 验收 |
| 2026-08-30 | Camera C-SP0～C-SP3：保留 Cinemachine 2，接入 Unity Splines 2.8.4；Spline/Binding、A/B VCam、Knot/Tangent/Helix 编辑替换旧 offset/Dolly/VcamKey/Anchor |
| 2026-08-30 | Camera Editor 收敛：删除 Helix 生成器与旧定位 Gizmo；Knot 独立点击热区、W/E 位移/旋转、CameraWindow Debug Scene Camera |
| 2026-08-30 | Camera Editor 可读性：隐藏官方 Spline 未使用扩展数据；按当前预览帧 Position/LookAt/FOV 绘制视锥，不改变朝向真源 |
| 2026-08-30 | Camera Spline 端点规则：新增 Linear/上下左右 Arc 预设；非 Custom 只拖首尾端点，放大选点并隐藏无效首尾切线 |
| 2026-08-30 | Action Camera View：新增独立可停靠 RenderTexture 实际构图窗口；删除会量化闪跳且妨碍编辑的 SceneView Debug 接管路径 |
| 2026-08-30 | Camera Spline 恒速修复：改为逐 Bezier 段累计弧长并查表定位，避免高曲率路径在窗口结束帧前提前到达终点 |
| 2026-08-14 | SprintLean 从静止向右倾改走 engage；GaitPolicy Run 计时加 0.1ms 容差；量化单测不再用非精确 2.5mm |
| 2026-08-09 | BT：删除 `EnemyBehaviorTreeKind` / Presets / Fill / Create Default；运行时仅 `customRoot.Build()` |
| 2026-08-09 | BT：Condition 改为 UE 风格单子装饰 + Abort Self；不再作为 Sequence 叶子条件 |
| 2026-08-09 | BT Graph：装饰/条件改为宿主顶部徽章（UE 表现）；运行真源仍为装饰链 |
| 2026-08-09 | L-GP：`LocomotionGaitPolicy` + `DefaultLocomotionAnimResolver`（WalkLeft/Right）；`strafeMoveMagnitude`；升档/选片无身份 if |

---

## Wave 4 — TargetAdhesion / SoftBodySuppress

### 功能说明

攻击吸附窗口：首次捕获固定玩家→敌人的偏移轴，后续目标平移时落点一起平移，穿敌不翻面。窗口内按本帧基础位移路程占剩余路程的比例重映射位移，无剩余路程时按帧均摊；末帧收敛到目标落点。正偏移表示捕获连线远侧，不表示敌人朝向定义的背后。中间帧修正限幅，末帧精确收敛优先；仍通过碰撞电机移动。SoftBody 抑制窗内不参与角色互撞、仍碰静物墙。

### 实现方案

| 项 | 方案 |
|----|------|
| 顺序 | 计算 BaseDelta → TargetAdhesion 重映射 → 单次 Motor 移动 → MotionCommand（Relocate）→ SoftBodySeparation |
| 纯计算 | `ActionMotionAdhesion.TryComputeDisplacementMm` + 捕获 State；Command 经 `ActionMotionResolver` |
| 目标 | `CharacterTargetingState.SelectedTargetId`；动作中切敌后下一逻辑帧改读新目标 |
| Pose | `ActionMotionWorldQuery` → `IHurtboxTarget.GetLogicalCombatPose`（含朝向） |
| SoftBody | Modifier 窗 / Relocate 落地 `SetSoftBodySuppressFrames` |
| 数据 | `motionModifierStates` + `motionCommandNotifies` |
| Editor | MotionModifier / MotionCommand 轨；Scene 与隔离视口共用假敌、Desired 与吸附轨迹，顶部假敌 X/Z 为相对预览原点的米制坐标；Scene 仍可拖动假敌 |

### 运行时流程

```
CharacterTargetingState.Step → SelectedTargetId
ApplyStep：SoftBodySuppress 刷新（含卡肉帧）
  → ResolveBaseDisplacement → TryApplyTargetAdhesion → MoveWorldMm → MotionCommand（Resolver.Teleport + Facing）
  → 无窗口/未捕获：ApplyBaseDisplacement 保持原基础移动入口
  → SyncRootPoseFromSim
SimulationWorld 帧末 SoftBodySeparation（抑制者不参与）
客机：同一 Bridge；WorldQuery 读 Proxy.GetLogicalCombatPose；帧末 AutonomousSoftBodySolver（抑制者不参与）
客机纠偏：窗内 / 权威卡肉 / Dodge 进行中走 ActionMotionReconcileGate，禁止 2m 硬吸
```

### 已知限制

- SkillShot / UI 展示舱不在 Wave 4/5；排期见 `docs/2026.8.26/CAMERA_SYSTEM_PLAN.md`。Lock-On 于 2026-09-19 暂时舍弃
- Relocate 挡墙精细候选（FindNearestValid 首版≈ ResolveMove）可后续加强
- 共线退化（玩家与敌人水平重合）本帧不吸
- 2026-09-30 新吸附语义尚待 Play 验收；Relocate 需在招上配 MotionCommand 点事件后 Play 验。
- 捕获距离/角度仅在首次捕获检查；动作起止、窗口更换、换目标时重置捕获状态，帧回退时重新捕获。卡肉不调用位移计算。
- 目标丢失且 StopOnTargetLost=true 时恢复基础位移；false 时继续向最后已知目标落点收敛。墙体、角色分离以及后续 MotionCommand 可限制/改变最终位置。窗外基础位移恢复，需停留时应让吸附窗口覆盖对应移动段。
- MaxCorrectionMmPerFrame 只限制中间帧，过小可导致末帧大幅修正；末帧不绕过静态碰撞。旧的动态连线、仅向前追加且过冲不拉回路径已删除。
- 客机 Adhesion desired 读 Proxy MotorSim（有 Tick 延迟），落点相对 Host 可能有 RTT 级偏差
- 2026-09-30：`ActionEditorWindow` 两种视口均显示吸附调试；`ActionMotionAdhesionSceneDrawing` 从零帧重放同一捕获/重映射计算，使用与电机一致的平面量化。隔离 RT 不绘制 Handles.Label，固定 GUI 图例说明颜色。预览不模拟场景碰撞或运行时目标切换；SoftBodySuppress 仅显示假敌。角色资产未改写。验证记录见 `docs/2026.9.30/TARGET_ADHESION_FIX.md`。
- 客机不 Collect；卡肉由本机几何预测，伤害只信权威下行；穿敌窗 / Dodge 进行中禁止 2m 硬吸（`ActionMotionReconcileGate`）

### 相关文件

- `Assets/Scripts/Domain/Simulation/Motion/ActionMotionAdhesion.cs`
- `Assets/Scripts/Domain/Character/Presentation/CharacterActionPresentationBridge.cs`
- `Assets/Scripts/Domain/Combat/Actions/Definitions/Timeline/MotionModifierNotifyState.cs`
- `Assets/Tests/EditMode/Simulation/ActionMotionAdhesionTests.cs`

---

## GAS G0～G5 — Numeric 完成态

### 功能说明

Attribute + Effect + Flags 为唯一数值权威；Gate/Pipeline/Hurtbox/Reaction/伤害公式已切换；旧 ResourceSim/Health 已删除；F3 HUD 展示 ATK/DEF/倍率/Effects/反击缓冲。

### 实现方案

| 项 | 方案 |
|----|------|
| 中枢 | `NumericSystem`（Factory 装配；Host 注册；Actor.Step） |
| 扣费/回填 | `NumericCostGate` + `ActionResourceSpecEffectCompiler` |
| 生命边沿 | `CharacterVitality` → Reaction Hit/Death |
| 命中 | Pipeline：完美窗/无敌早退 → OnHit → Grant Effect |
| 伤害 | `DamageNumericCalculator`（Attack/Defense + Out/In 倍率） |
| 配置 | `CharacterNumericConfig.FromResourceConfig`（作者壳 Config） |
| Snapshot | `NumericDebugSnapshot` / `CharacterDebugSnapshot` → `CombatDebugHudController` |

### 已知限制

- 完美闪避慢动作表现事件未做
- HitStop / EffectNotifyState 未进 Effect
- Effect 尚无 ScriptableObject 资产壳（程序 `Create*`）
- Graph Counter Entry / Dodge 完美窗资产需 Editor 人工

### 相关文件

- `Assets/Scripts/Domain/Combat/Numeric/*`
- `Assets/Scripts/Domain/Combat/Actions/Execution/NumericCostGate.cs`
- `Assets/Scripts/Domain/Character/Reactions/CharacterVitality.cs`
- `Assets/Tests/EditMode/Domain/*Numeric*` / `EffectContainerTests` / `DamageNumericCalculatorTests` / `ActionSimResourceGateTests` / `PerfectDodgeAttackTests`

---

## Wave 0 — 观测与保护网

### 功能说明

不改手感：全库归类动作位移源（Baked/Scripted/None/Conflict），Scene 对照烘焙轨迹与 Motor/相机锚点，Play Mode 左上角可读 Intent/Buffer/HP/Lock/横向峰峰值。

### 实现方案

| 项 | 方案 |
|----|------|
| 位移源归类 | `ActionMotionSourceClassifier`（Simulation）+ Editor `ActionDefinitionAuditUtility` |
| 轨迹预览 | Action Inspector「Show Baked Trajectory」→ `ActionMotionTrajectorySceneDrawing` |
| 锚点 Gizmo | Editor `CharacterAnchorGizmoDrawer`（DrawGizmo → PlayerController） |
| Debug HUD | `CharacterActor.BuildDebugSnapshot` + `CombatDebugHudController`（F3） |

### 运行时流程

```
菜单 Validate Motion Sources → 报告窗口（不改资产）
Play：Actor.Step 更新 ActionLateralPeakMm
LateUpdate：HUD 采样 Snapshot → OnGUI 绘制
```

### 已知限制

- 0.4 人工基线手记可选，不阻塞
- Wave 2.5 已删除 Animator RM 回退

### 相关文件

- `Assets/Scripts/Domain/Simulation/Motion/ActionMotionSourceClassifier.cs`
- `Assets/Scripts/Editor/Combat/Motion/ActionDefinitionAuditUtility.cs`
- `Assets/Scripts/App/Controllers/Debug/CombatDebugHudController.cs`
- `docs/2026.8.6/MASTER_IMPLEMENTATION_PLAN.md`

---

## Wave 3 — 技能资源循环

### 功能说明

绝区零式单角色资源：Energy / Decibel / DodgeCharges；起手 Gate 扣费；ConfirmHit 回填；Special 同键按能量选 EX。

### 实现方案

| 项 | 方案 |
|----|------|
| 数值权威 | `NumericSystem` + `CharacterVitality` |
| 价签 | `ActionDefinition.ResourceSpec`（`ActionResourceSpec`） |
| 扣费 | `NumericCostGate` → Spec→Instant Cost Effect |
| 回能 | Pipeline ConfirmHit → `ActionResourceSpecEffectCompiler.ApplyGrant` |
| 同键 EX | `GameplayIntentType.Special` + `ActionEnergyFormSelector`；Graph 多 Entry |
| 观测 | HUD：EX/Decibel/Dodge + `Next Special`（读 Numeric） |

### 运行时流程

```
Intent Special → Graph 收集 Entry → EnergyFormSelector(Ex if CanAfford else Special)
  → ActionSim.TryStart → NumericCostGate.CommitCost
ConfirmHit → ApplyGrant（挥空不回）
Actor.Step：非卡肉时 NumericSystem.Step
```

### 已知限制

- Graph Counter Entry（`Intent=PerfectDodgeAttack`）与 Dodge 完美窗轨需 Editor 人工
- 正式招费用 / Graph Special 双 Entry / Ultimate 资产需 Editor 人工
- 完美闪避慢动作表现未做
- Wave 2.5：Action RM 回退已删（Locomotion Stop/Pivot 仍可选用 RM）

### 相关文件

- `Assets/Scripts/Domain/Combat/Numeric/*`
- `Assets/Scripts/Domain/Combat/Resources/*`（价签）
- `Assets/Scripts/Domain/Input/GameplayIntentProducer.cs`
- `Assets/Scripts/Domain/Combat/Actions/Execution/NumericCostGate.cs`
- `Assets/Tests/EditMode/Domain/ActionSimResourceGateTests.cs` / `ActionEnergyFormSelectionTests.cs` / `PerfectDodgeAttackTests.cs`
- `docs/2026.8.7/GAS_STYLE_COMBAT_REFACTOR_PLAN.md`

## 角色作者工作台（代码落地，验收待执行）

功能：从角色/敌人/身体配置入口完成移动、动作、图、反应、检查和角色范围烘焙。菜单 `ACT/Character Workbench`，根资产 Inspector 也提供入口。

| 实现 | 责任 |
|---|---|
| CharacterAuthoringService | 创建独立空身份、身体、默认动作图与移动配置；单动作与批量草稿按角色 ID + 用途命名并绑定 Graph/Reaction；失败清理本批新资产 |
| CharacterAuthoringWindow | 当前角色/模式决定动作范围，RM 目录按 Config GUID 与项目保存，烘焙展示输入匹配和共享影响 |
| ActionEditorPreviewViewport | 主工作区内的 PreviewRenderUtility 隔离模型；采样由 ActionEditorPreviewSession 管理，退出释放 |
| ActionGraphValidator | 唯一图结构算法；终结动作不要求 Cancel，按 NodeId 的 AI Entry 可使用 None，多个 None Entry 无警告；仅非空输入 Intent 检查冲突 |
| CharacterValidationPanel | 聚合既有校验日志与图问题；图支持节点定位，非图问题部分仍定位到配置分组 |

参数：固定 ActionSim.LogicHz=60；resourceSpec 在 Action Editor 可编辑；Timing.duration/loop 为生成值，exit/handoff 仍由作者调整。Timing Bake 拒绝缩短 Clip 导致的越界，也拒绝重复/无效旧时序。

运行路径：ActorFactory → Config.CombatModes → CombatModeService / CharacterAnimationService；网络内容收集经 ActionReplicationCatalog 读取相同模式引用。动作名、CharacterId、Graph 节点身份不在迁移中重编号。

限制：工作台不是 PartyLoadout 自动装配器；草稿不猜测命中/取消/连线。非图问题尚未全部转换为 Domain 结构化细粒度字段；移动 RootMotion 新增持久来源指纹，待 Editor 验证。补充 Roslyn 编译与文件语义检查通过，不代表 Unity 编译或 Play 通过。详细状态见 `docs/2026.9.28/CHARACTER_AUTHORING_EXECUTION_REPORT.md`。

相关源码目录：`Assets/Scripts/Editor/Character`、`Assets/Scripts/Domain/Character/Combat`、`Assets/Scripts/Domain/Combat/Actions/Validation`；测试 `Assets/Tests/Editor/Character/CharacterAuthoringTests.cs`。

变更日志：2026-09-28 合并模式/动画配置层，新增角色作者入口，保留待验收状态。


2026-09-28 验收更新：最新 Unity EditMode 定向测试 62/62 通过（新增 16 项）。全项目审计 85 项原有结构/内容问题仍未关闭；新增源码结构违规为 0，Graph 误报已修复。以 CHARACTER_AUTHORING_EXECUTION_REPORT 和 UNITY_RESULTS.xml 为准，不能将测试通过等同于 Play/全内容验收通过。

## 2026-09-29：根位移配置校验与修复

功能：动作烘焙采用实际播放段的 60Hz 帧范围，避免浮点时长取整差异导致表比动画多帧。

| 实现 | 职责 |
|---|---|
| ActionMotionBakeRange.TryResolve | 委托 ActionAnimationSegment.TryGetFrameRange 解析播放范围；RM 太短直接拒绝，较长只取所需范围 |
| ActionMotionBakeRange.Fingerprint | Clip 内容、裁切起止帧及 PlaybackRangeV2 版本共同构成来源标识，覆盖同长度裁切平移 |
| ActionMotionBakeService.BakeAction | 逐段配对、采样和拼接；全表帧数必须等于 ActionDefinition.TotalFrames 才能写回 |
| ActionMotionDirtyUtility | 与 Baker 共用范围和指纹，不在 Inspector 中采样动画曲线 |
| MotionContentRepair.Run | 只读计划或带资产/meta备份的修复；覆盖无效表与过期 BakedMotion 表；RM 误绑定只接受唯一、同长且可反查原 RM 的 InPlace |

运行链：播放段范围 → 唯一 InPlace/RM 配对 → 逐段烘焙 → 范围截取 → 总帧断言 → 保存目标资产。原有按 RM 全长决定播放段尾的分支已移除。

本轮资产：31 个帧数错误、2 个空表及 1 个移动 Profile（4 条轨）修复；另更新 13 个原本帧数正确的过期表，共 47 个资产。动作时序、消耗和位移模式保持不变，3 个误绑播放 Clip 改回已有 InPlace。34 项配置错误归零，综合检查仍有 51 项既有 StructureAudit；Unity EditMode 71/71。

限制：自动配对限 Assets/Art/Characters 下具有唯一 Root 目录的现有组织，歧义拒绝写入；没有自动修改命中/取消/资源配置。尚未完成 Gameplay 中攻击、反击、受击及急停转身的人工 Play 手感验收。证据：docs/2026.9.29/CONFIGURATION_REPAIR_REPORT.md。

相关代码：Assets/Scripts/Editor/Combat/Motion/ActionMotionBakeRange.cs、ActionMotionBakeService.cs、ActionMotionDirtyUtility.cs、MotionContentRepair.cs；回归：Assets/Tests/Editor/Character/ActionMotionBakeRangeTests.cs。
## 2026-09-29：结构审计与异常反馈

功能：源码结构、程序集归属和生产内容完成本轮清理，失败路径具备明确反馈。

- StructureAuditRuleSet：按类型名与泛型元数去重同名条件声明；UIFramework 只允许 UnityEngine.UI，ACTGame.UI 只允许 UIFramework；ActionSim 登记单职责，移除已拆分的 NodeDef 集合登记。
- CombatWorldController.TryReadLaunchConfigFile：预期 IO/权限/路径异常记录类型，仍由 ServerLaunchConfigResolver 转为 ConfigFailed，不输出配置正文。
- ActionTimelineClipboard.Paste：损坏 JSON 返回前记录警告，保留动作原内容。
- ChannelMuxTransport.TryDecode：验证完整 9 字节固定头、版本、kind、声明长度与实际剩余长度，非法数据报由 HandleIncoming 计入丢包；移除宽泛异常捕获，不为坏包逐条刷错误日志。
- CharacterAuthoringValidationRunner.RunAll：在已打开的 Editor 内执行全部 EditMode 测试并存档结果，不启动同项目的第二个批处理实例。

验证：新增 5 个结构规则用例、6 个坏包用例；修正 Windows 路径分隔符导致的程序集归属误判，并为两个 UI 示例脚本补齐显式程序集。结构与内容审计 0 项，直接相关测试 41/41，全量 639/656。

限制：17 项全量测试失败单列在 STRUCTURE_REPAIR_REMAINING_TEST_FAILURES.json；未逐项修复，未执行人工 Play 或发布构建。类型拆分进行 Editor/发布条件共 330 次声明等价验证，1,617 个已有序列化资产保持原字节。

相关代码与逐文件说明：docs/2026.9.29/STRUCTURE_REPAIR_FILE_CHANGES.md。

### 空角色创建与用途目录（2026-09-29）

功能：CharacterCreateWindow 创建空白角色，不再接受模板；选中角色不会成为配置来源。

| 实现 | 责任 |
|---|---|
| CharacterAssetLayout | 默认父目录、ID 校验、用途命名、动作保存位置与空目录清理 |
| CharacterAuthoringService.CreateCharacter | 创建 4 个全新资产并连接一个 Default 模式，允许显式指定模型 |
| CharacterActionCreateWindow / CharacterActionBatchWindow | 输入用途并预览角色前缀与 Actions / Reactions 保存目录 |

默认父目录 `Assets/Data/Characters`；角色根 `<Id>/`，身份 `<Id>_Character.asset`，身体 `Config/<Id>_Config.asset`，图 `Graphs/<Id>_Default_Graph.asset`，移动 `Locomotion/<Id>_Default_Locomotion.asset`；Actions、Reactions 初始为空。编辑器路径：CharacterCreateWindow → CreateCharacter → CharacterAssetLayout；创建后进入 CharacterAuthoringWindow。新动作使用 `<Id>_<用途>`，例如 `<Id>_Attack_01`。

限制：不生成动画、图节点、反应动作、行为树或 PartyLoadout，不绕过内容校验。默认数值使用各 ScriptableObject 类型初始化值；旧模板引用不会带入。既有平铺配置只将后续动作放到其 Actions / Reactions 子目录，不迁移现有资产。

验证：Unity 当前 Editor 定向 EditMode 76/76 通过，结构与内容审计 0；创建测试 11 项通过。完整结果 `docs/2026.9.29/EMPTY_CHARACTER_TEST_RESULTS.xml`，操作及文件清单见 `EMPTY_CHARACTER_CREATION_REPORT.md`。本次未重复全套 EditMode 或 Play 验收。


### 既有角色配置迁移（2026-09-29）

CharacterAssetMigration.Execute 消费显式 CharacterAssetMigrationManifest：先检查源 SHA256、GUID、未保存状态与目的冲突，再用 AssetDatabase.MoveAsset 保留身份，仅修改文本名称行后重新导入；失败逆序回滚。清单由实际角色引用关系确定，不能按旧目录推断所有权。62 项配置迁移，玩家 Unagi 与敌人 UnagiEnemy 共用的移动与受击仍在 Shared 保持同一引用。独立动作创建器同步使用 Actions/Reactions，去除旧拼写目录分支与沿用旧动作名的逻辑。

验证：定向 Unity EditMode 85/85，62 项 GUID/.meta/字段保护通过，其他 491 项序列化资产未改。全项目审计保留 NewCharacter 空草稿原有 3 个失败资产，迁移无新增问题。动作及原型 name 修改影响网络稳定 ID，两端内容须同步更新；图 NodeId 和 BT Entry 字符串保持原值。完整 Gameplay 与双端联机 Play 仍待人工检查。文件清单与结果见 docs/2026.9.29/CHARACTER_LAYOUT_MIGRATION_REPORT.md。


2026-09-29 目录与来源提示：四角色基础目录全部补齐，空目录用 .gitkeep 随版本控制保留。工作台总览/移动/连招/反应显示资产路径、共享引用数量与影响的身体配置，提供定位；FindOwners 扫描缓存最多 2 秒。缺失引用与共享引用明确区分。Unity 定向 87/87、当前审计 0；已有配置及 .meta 哈希未变。细节见 docs/2026.9.29/CHARACTER_BASE_FOLDERS_REPORT.md。


2026-09-29：ActionEditor 场景模型选择回归已修复：角色上下文不再禁用 Transform 选择；增加隔离预览按钮。同角色打开不同动作及上下文重载保留场景选择。Unity 定向 89/89、审计 0；完整拖拽和场景动画恢复交互待人工验收。报告 docs/2026.9.29/ACTION_SCENE_PREVIEW_FIX.md。


## ActionEditor 工作区与精确打点（2026-09-29）

功能：上方动作库/模型/属性、下方完整时间轴；场景与隔离显式切换，当前动作保存与只读校验可在同窗完成。

| 组件 | 责任 |
|---|---|
| ActionEditorWorkspaceView | UI Toolkit 分栏、折叠资产列表与尺寸持久化 |
| ActionEditorPreviewViewport | 临时隔离模型、网格、旋转/平移/缩放/F 聚焦 |
| ActionEditorPreviewSession | 单一采样会话，切目标/动作恢复采样，Play 不抢运行时 |
| ActionTimelineSnapping | 8px 磁吸，最近距离/同距优先级，Alt 保留整数帧而关闭吸附 |
| ActionAnimationSegmentCommands | 多 Clip 插入与源帧修剪，原子边界检查 |
| ActionEditorSfxPreview | 显式原音试听、默认静音；不随拖帧自动触发 |

参数：固定 60Hz，预览倍率 0.25/0.5/1/1.5/2，仅影响 Editor 时钟；磁吸容差 8px；三分栏和上下分栏由 UI Toolkit viewDataKey 持久化。
2026-09-29 预览回归修复：隔离绘制使用显式 `Handles.DrawingScope(Color.white, Matrix4x4.identity)` 保存与恢复矩阵/颜色，另恢复相机与深度比较；无参结构体构造会在 Dispose 时清零全局矩阵，禁止使用。三维辅助线仅绘入预览 RT，轨迹图例在 EndPreview 后由 GUI 固定绘制，模型与辅助线使用同一 FOV。隔离灯光采用相机相对主光 1.2、补光 0.8、环境光 RGB(0.35,0.37,0.4)。时间轴 MouseUp/Ignore 只结束窗口拖拽，避免单击事件时触发磁吸改帧。回归覆盖实际双 IMGUIContainer 重绘、状态恢复、VFX/SFX 点击与拖动及四个相机朝向的补光。

流程：选择动作 → 单一 SerializedObject → 时间轴/属性命令 → Undo 写回；播放帧 → Session.Tick → Sampler + VFX 扩展 → 内嵌渲染或 Scene。当前校验复用 ActionDefinition.ValidateContent 与 ActionDefinitionAuditUtility，位移烘焙页通过 ActionMotionBakePanel 复用 ActionMotionDirtyUtility 和 ActionMotionBakeService，展示 RM 来源、模式与指纹状态；成功后重新绑定预览并刷新轨迹。Inspector 共用此面板，角色 RM 路径与工作台共享，独立动作按资产 GUID 保存路径偏好。

限制：场景模式画面在 Scene；SFX 试听为源音频原速，游戏音量/pitch 不由试听模拟；Camera Shot 保留既有 Scene/Action Camera View。没有动画分层或分段变速。自动回归和资源哈希证据见实施报告，人工战斗/联机核对不由 EditMode 结果替代。

相关文件：`Assets/Scripts/Editor/Combat/ActionEditor/`，`Assets/Tests/Editor/Character/ActionEditorAlignmentTests.cs`、`ActionEditorViewportTests.cs`。2026-09-29 变更：删除独立 CharacterAuthoringPreviewWindow、手工三列分隔路径与无用布局常量，保留全部业务轨道与场景目标选择。

## 动作突刺连续路径阻挡（2026-09-30）

**功能说明：** ActionExecutionPolicy 的 `StopOnContact` 让连续平面动作位移停在最早接触的实体或静态墙体前，动画和 Hitbox 帧继续；被截断位移不保存、不补偿。代码已接入，Unity / Play 尚待验收。

| 实现 | 职责 |
|---|---|
| `ActionBodySweep.Resolve` | 纯 C# 点扫膨胀圆盘与静态 AABB，最早接触；毫米量化后重新检测，不滑墙 |
| `ISimCollisionWorld.SweepFraction` / `Depenetrate` | 复用静态烘焙数据，恢复非法起点并沿完整直线查询 |
| `CharacterMotorSim.TryMoveActionMm` | 求解后单次提交安全终点，不再调用分轴滑墙 |
| `CharacterBodyObstacleQuery` | 从注册目标上的 `ISimBodyObstacleSource` 获取已提交逻辑体积；排除自身、稳定排序、拒绝重复 Id |
| `CharacterActorFactory` | 按座位注入 Authority / Observer 来源筛选，防止 Listen 同 Id 双份实体混入 |
| `CharacterMotor.MoveActionMm` | 基础和吸附位移统一提交；策略 0 保留原有软分离及滑墙 |

**关键参数：** `ActionExecutionPolicy.bodyCollisionMode` 默认 `SoftSeparationOnly(0)`，`StopOnContact(1)` 为显式启用；`bodyContactSkinMm` 默认 20mm。身体半径来自 MotorSim，不取 Hurtbox 或模型；BodyMode/Skin/RulesVersion 加入 ServerContentManifest。无敌与 SoftBodySuppress 不自动关闭身体阻挡，死亡/离场/停用不提供实体。

**运行时顺序：** GameplayStep 生成基础世界位移 → TargetAdhesion 修正 → MoveActionMm → MotorSim 安全提交 → 原 Hitbox Collect → 世界软分离。客户端帧末软分离复用 Actor 的同一身体来源；Observer 只读。F3 `ActionBody` 显示最近提交的起点、期望终点和 blocker，实际结果读 Motor。ActionEditor 选择 StopOnContact 即显示假敌位置/半径与安全路径，无须先建立吸附窗口。

**限制：** XZ 圆盘、逐 Actor 提交时读取当前逻辑位置；不保证未启用策略的另一角色高速穿入或瞬移后的完整两体 CCD。StopOnContact 碰墙停止，走跑仍滑墙。吸附精确落点服从身体安全。初始重叠允许离开，不恢复未知历史接触侧。预测远端数据有延迟，不增加动作 Replay 或网络位置容忍度。预览不含场景墙体及网络时间差；无角色上下文时自身半径为 280mm，假敌半径可调。现有内容指纹尚非全部 Gameplay 参数的完整序列化，本次明确覆盖新增碰撞字段及算法版本。

**文件与验证：** [方案](../../../../docs/2026.9.30/ACTION_DASH_COLLISION_PLAN.md)、[逐文件实施记录](../../../../docs/2026.9.30/ACTION_DASH_COLLISION_IMPLEMENTATION.md)。新增 `ActionBodySweepTests`、`ActionDashCollisionIntegrationTests`、`ActionDashPredictionTests`；独立 Mono 可运行部分已验证，Unity 场景测试未执行。原 `ApplyBaseDisplacement` 动作旁路和重复 Proxy 采集已删除。

**变更日志：** 2026-09-30 接入动作级路径阻挡、源筛选、指纹和预览；不修改既有吸附算法、Root Motion 烘焙或场景布线。

## 动作创建动画浏览器（2026-09-29）

功能：两个创建窗口支持角色动画目录、名称/路径搜索、FBX 子动画、拖放与选前预览。
实现：CharacterAnimationSourcePreferences 将动画与 RM 来源目录 GUID 保存在本机 EditorPrefs，范围为项目 + 角色/独立动作；原 RM 路径键首次有效读取后迁移并删除。ActionAnimationPickerPanel 通过 Unity SearchService.ShowPicker 展示动作库候选；默认进入动作库页，名称用 NaturalCompare 自然排序并写入 SearchItem.score。首次通过 Search 异步枚举逐文件读取当前目录，完整结果按目录缓存、projectChanged 后失效；GUID/local ID 区分同名子资源，名称与路径搜索不重复读取资产标识。单文件加载仍为 Unity 同步 API。原手绘候选列表与全项目开关已移除。CharacterActionBatchWindow 共用原生选择入口并支持目录全量去重追加。
参数：60Hz 帧数、220px 可交互隔离预览；目录选择入口包含子目录，未设置来源时禁用。单个与批量动画 ObjectField 的小圆圈在配置动作库时直接打开目录限定选择，未配置时保留默认选择；直接拖片保留。批量使用 Unity ReorderableList 维护待创建清单，重复选择按钮已移除。
流程：选择目录 → 搜索/拖片 → 临时 ActionDefinition 与模型预览 → 用途命名 → 既有创建服务写入并定位。预览独占 AnimationMode，关闭/切片/Play 释放，正式资源不在选片时写入。
限制：来源偏好只在本机；独立入口需要模型资产；实际 FBX 蒙皮效果需 Editor 检查。仍不自动推断战斗窗口，不自动烘焙。
相关：Assets/Scripts/Editor/Character/CharacterAnimationSourcePreferences.cs、Combat/ActionEditor/ActionAnimationPickerPanel.cs；回归 ActionEditorViewportTests。移除两个窗口重复选片 ObjectField、跨窗口 FolderKey 依赖。

2026-09-29 多动画创建：ActionAnimationPickerPanel 使用原生 Search 多选动作并通过可排序清单明确播放顺序；CharacterActionCreateWindow 与 ActionDefinitionCreateWindow 提交有序 Clip 集合，两个创建服务共用 ActionAnimationSegmentCommands.InitializeDraft 初始化全部 animationSegments 与累计总帧数。角色入口只绑定一个动作；批量入口继续一片一动作。命中/取消窗口不自动推断，创建不自动烘焙。回归 CharacterAuthoringCreationTests，说明 docs/2026.9.29/ACTION_MULTI_CLIP_CREATE.md。

2026-09-30 RunTest 修复：SimulationStepKernel 直接以 double 的 1 / LogicHz 累计时间，修正 50ms 少算一帧；PartySwitchPlacement 普通换人右侧偏移统一为方案规定的 600mm。ChannelMuxTransport 在重传前清理底层连接表已移除的连接，ServerSession 同步清理玩家并通知 Gameplay；UDP 静默断线仍依赖心跳超时。ServerLaunchConfigResolver 将负数参数交给配置校验，不再忽略。吸附预览测试改用有效动画段，绑定失败测试显式断言日志。独立编译及 38 项 Mono/NUnit 辅助检查通过，Unity Test Runner 尚待复跑；详见 docs/2026.9.30/TEST_FAILURE_FIXES.md。
