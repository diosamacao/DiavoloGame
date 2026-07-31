# ACTGame 编码规范

> 从现有代码归纳；随项目演进由 actgame-architecture skill 维护。

## 命名

| 类型 | 约定 | 示例 |
|------|------|------|
| 类/文件 | PascalCase，文件名与类名一致 | `PlayerController.cs` |
| 私有字段 | `_camelCase` | `_player`, `_machine` |
| 序列化字段 | `[SerializeField]` + camelCase | `walkSpeed` |
| 常量 | PascalCase 或 UPPER（Core 内 const 用小写 camel 亦可，与邻代码一致） | `MoveInputThreshold` |
| 枚举 | PascalCase 成员 | `CharacterStateType.Locomotion` |
| 接口 | `I` 前缀 | `ICharacterStateMachine` |

## 目录与类放置

- **一个 public 类一个文件**；辅助私有类可同文件
- **按职责分目录**，不用 C# namespace 区分模块
- 新 MonoBehaviour 放在对应 gameplay 目录（Player/Enemy/Camera），共享逻辑放 Character/ 或 Core/
- 占位目录保留 `.gitkeep`，实现后删除 `.gitkeep`
- 角色装配配置集中在 `CharacterConfig`；Scene 玩家入口只挂 `PlayerController` 并引用配置资产
- 玩家根对象运行时禁止挂载业务脚本；除 `PlayerController` 与 Unity 必需组件（如 `CharacterController`）外，角色业务必须是纯 C# runtime/service
- 新架构代码遵循 `Controller / System / Model / Command / Event / Query / Actor / Executor / Service` 后缀语义；禁止新增泛化 `Runtime` 后缀业务类
- `Controller` 仅用于 `App/Controllers` 下的 Unity 入口 `MonoBehaviour`，并继承 `AppControllerBase` 或实现 `IArchitectureController`
- `System` 后缀仅用于 `App/Systems` 下注册进 `ACTGameArchitecture` 的架构系统，并继承 `ArchitectureSystemBase` 或实现 `IArchitectureSystem`
- `Domain` 纯 C# 业务类优先使用 `Service` / `Actor` / `Executor` / `Resolver` / `Detector` / `Consumer`，不得直接访问 `ACTGameArchitecture.Interface`
- **动作系统目录分层**：`Combat/Actions/` 下按职责分四个子目录——`Definitions/`（动作数据 Schema，含 `Definitions/Timeline/`）、`Resolution/`（输入 → 动作选招）、`Execution/`（播放/帧推进/输入路由/旋转）、`Frames/`（Logic Tick 帧上下文契约）；核心脚本不散落在 `Actions/` 根目录
- **动作时间轴数据**：帧相关配置以 `ActionDefinition.Timeline` 为唯一真源；点事件用 `ActionNotify`（Event/VFX/SFX），区间窗口用 `ActionNotifyState`（Phase/Hitbox/Hurtbox/Cancel/Movement/Rotation）；禁止在 `ActionDefinition` 重新引入独立 `phases[]` 等双轨帧数据
- 跨系统通信使用 `ACTGameArchitecture` 的 Command / Query / Event；动作帧内部可保留 `ActionExecutor` → `ICombatFrameConsumer` / `IActionNotifyConsumer` 的强时序直连
- 架构事件必须实现 `IArchitectureEvent`；架构查询继承 `ArchitectureQueryBase<TResult>` 或实现 `IArchitectureQuery<TResult>`
- **固定帧唯一入口**：角色业务只实现 `ISimulationActor.Step`，由 `SimulationHost → SimulationWorld` 以 60Hz 推进；Controller 禁止新增 `Update → Actor.Tick` 旁路
- **模拟身份**：World Actor 使用会话内单调 `SimActorId` 排序；禁止用 Unity `GetInstanceID()` 作为模拟顺序、命中身份或未来网络身份
- **启停生命周期**：Controller 在 `OnEnable` 注册 World、`OnDisable/OnDestroy` 对称注销；禁用 GameObject 不得继续被模拟
- **渲染输入汇聚**：本地设备 Actor 通过 `IRenderFrameSampler` 缓存渲染帧边沿，逻辑 Step 不直接依赖 Unity 渲染帧是否恰好发生
- **量化输入唯一格式**：玩家、AI、回放与未来网络统一使用 `InputFrame`；Move 为 sbyte、按钮为固定 bitset，禁止恢复 float/string `PlayerInputFrame`
- **输入阶段先于 Actor**：World 每帧先调用 `ISimulationInputProducer`，再按 Id 消费同帧 `InputFrame`；AI 决策不得在自身 CharacterActor.Step 中旁路写输入
- **边沿展开**：同一目标帧的多次渲染采样只对 Pressed/Released 做 OR；本地追帧可延续 Move/Held，但禁止从 Held 推导或重复边沿
- **模拟/表现 Pose 分离**：权威根只在 `SimulationWorld.Step` 改变；模型与相机通过 `CharacterPresentationBridge` 插值，Render 禁止回写碰撞、命中或 Hash 状态

## Unity 组件模式

```csharp
[RequireComponent(typeof(SomeComponent))]
public class MyBehaviour : MonoBehaviour
{
    [Header("Group Name")]
    [SerializeField] float someValue = 1f;

    SomeComponent _dependency;

    void Awake() => _dependency = GetComponent<SomeComponent>();
}
```

- 依赖用 `GetComponent` 在 Awake 解析，避免每帧 Find
- 需要运行时装配的业务逻辑优先做纯 C# 构造函数注入；不要用 `AddComponent` 伪装装配
- 可选引用用 `[SerializeField]` + null 回退（如 `Camera.main`）
- 缺失关键引用：`Debug.LogError` + `enabled = false`（见 InputReader）

## 状态机约定

1. **Core 层**保持泛型，不出现 `UnityEngine` 引用
2. **Character 层**：
   - Context 只存数据与组件引用，不写复杂逻辑
   - State 的 `Tick` 写状态内逻辑；`CanTransitionTo` 写转换条件
   - 新 State 在 `CharacterStateMachine.RegisterStates()` 或子类 override 中注册
3. **子类 StateMachine**（如 Player）只负责 `UpdateContext()` 填充 Context，不重复 Tick 逻辑
4. 状态切换对外用 `TryChangeState`；内部强制切换才用 `force: true`
5. **Locomotion 内层机**：顶层保持 `CharacterStateType.Locomotion`；相位用嵌套 `LocomotionStateMachine`（`LocomotionPhase`）。相位 `CanTransitionTo` 默认全开，由各态 `Tick` 主动切；`Tick` 只做转换，`ExecuteFrame` 做 Motor/动画（由宿主在转换后调用）。禁止再引入手写 `_phase` 袋式 `LocomotionService`

## 动画约定

- 逻辑层使用 `AnimationKey`，Profile 映射到 `AnimationClip`（不映射 Animator 状态名）
- 播放走纯 C# `CharacterAnimationService`；后端为 `IAnimationPlayback`（当前 `PlayableAnimationPlayback`，可换 Animancer）
- 需要独占时 `SetLocked(true)`；卡肉用 `SetSpeed(0)`，禁止业务直写 `Animator.speed`
- Locomotion：`applyRootMotion = false`，位移由 `CharacterController` + `CharacterMotor` 负责
- Action：`ActionDefinition.ExecutionPolicy.UseRootMotion = true` 时由 `CharacterRootMotionDriver` 写入 `CharacterController`；关闭时可用 `MovementNotifyState` 脚本位移
- 同 key 不重复 Play（门面 `_currentKey` 去重）；无 Animator Controller 业务依赖
- 角色销毁时 `CharacterActor.Dispose()` 释放 PlayableGraph

## 输入约定

- 使用 Input System + `.inputactions` 资产；Action Map 命名 `Player`
- **采集**：玩家设备边界实现 `ILocalInputSampler` 并写下一逻辑帧槽；AI 使用 `AIInputWriter` 在 World Input Produce 阶段直接构造 `InputFrame`
- **原始中枢**：`InputManager` 由 `CharacterActor` 持有；摄入量化 Move 与 Pressed/Held/Released bitset，不承担动作缓冲
- **相机 Look**：渲染帧 Look 不进入玩法 InputFrame，由 `PlayerController.LookInput` 直接提供给 CameraManager
- **设备映射**：`GameplayIntentProfile` 是 InputActionReference、长按阈值与上下文映射的唯一配置源
- **语义生产**：`GameplayIntentProducer` 在 `InputManager.IngestFrame` 后输出 `GameplayIntentType`
- **上下文意图**：SprintAttack / DodgeAttack 由 `GameplayIntentProfile` 条件映射产生；闪避攻击使用 `IsDodging + Attack Pressed`，禁止在 Driver 中按键名特判
- **选招层**：`ActionResolverService` → 当前模式 `ActionGraph`（多 Entry × Intent 起手 + Cancel 边）；`DirectionalActionResolver` 可作为节点 `VariantResolver`
- **节点 Intent**：`GameplayIntentType` 只保存在 `ActionGraphNode.Intent`；`ActionDefinition` 禁止声明输入或选招字段
- **连招图**：一张 `ActionGraph` 可同时含攻击/闪避等多个 Entry；边按 `CancelWindowType.Normal / Perfect` 解析，重复的「同来源 Intent + 同类型 + 同意图」使用 `ActionGraphSharedRoute`
- **流程唯一真源**：自动衔接、索敌和起手副作用都在 `ActionGraphNode`；禁止在 `ActionDefinition` 重新引入 Transition、TargetLock 或 StartBehavior
- **Graph 策略编辑**：普通节点与顺序组子节点都在 Graph 节点内部展开策略编辑；新增节点必须显式清空数组槽继承的旧策略
- **顺序组**：组内 Action 按行顺序自动生成 Normal Cancel 链；每行保留独立 In；组级 Normal / Perfect 出口分别展开到配置对应窗口的全部子节点
- **变体节点**：Directional 等 Resolver 只改变实际播放 Action，不改变逻辑 Graph 节点；同语义六向变体禁止复制节点和出边
- **线性连招**：`ComboActionResolver` / `ComboLeafPolicy` 已删除；`ActionGraph` 是唯一连招拓扑真源
- **战斗模式**：`CombatModeProfile` + `CombatModeService`；`PlayerActionSet` 绑定一张 Graph
- **缓冲**：招式中 `Buffer(GameplayIntentType)`；`ActionExecutor` 经 `IActionInputBuffer` 在 `CancelWindow` 内消费
- **Locomotion 边界**：连续 Move 不枚举化；Action→Locomotion 特殊恢复使用一次性 `LocomotionResumeRequest`
- **后摇窗口**：Timeline 的 `ActionPhaseNotifyState(Recovery)` 同时配置 `allowMovementCancel` 与 `allowEntryRestart`；禁止创建 Recovery CancelWindow、独立 phases 或回根显式边
- **CancelWindow**：每个 Action 必须且只能有一个 Normal，可选一个 Perfect；两个窗口可重叠，同一 Intent 始终优先 Perfect；禁止重新引入分割帧、槽 Id 或同类型多窗口
- 其它系统不直接读 `InputReader` 做玩法判断（移动执行在 `CharacterMotor` / State）
- **玩家装配**：`InputActionAsset` 与 `GameplayIntentProfile` 由 `CharacterConfig` 注入，不在玩家 Prefab 上重复配置
- **AI 输入**：`AIInputWriter` 必须写与玩家相同布局的 `InputFrame`；Brain 禁止直接调用 `ActionExecutor.TryStart/TryInterrupt`
- **输入计时**：Hold、Action Buffer 与 AI 攻击/重试/刷新冷却只使用整数逻辑帧；禁止重新引入秒制输入 TTL
- **AI 移动**：相机相对 Motor 通过 facing proxy + `Move=(0, magnitude)` 复用，禁止另建敌人移动栈

## 伤害与受击约定

- 伤害、`HitReactionId`、镜头震动与卡肉统一由 `HitboxNotifyState.Payload` 持有；反馈系统禁止从 `ActionDefinition` 反查
- 命中去重身份使用 ActionTimeline 中的 Hitbox 窗口下标；同一窗口对同一目标一次，不以可重复的显示字符串作为运行时键
- 受击反应由“有效命中”驱动，不得依赖最终伤害必须大于 0；扣血与 Hit 状态是两条职责
- 玩家和敌人都使用 `CharacterHurtboxTarget` 注册到 `TargetSystem`；HitDetector 在几何检测前排除自身、同阵营与死亡目标
- 自身过滤必须覆盖角色根与全部父子层级，禁止模型子节点上的 Hurtbox 生成自击事件
- 玩家阵营由 `CharacterConfig.Combat.teamId` 持有；敌人阵营由 `EnemyDefinition.teamId` 独立持有，禁止从可复用身体配置隐式继承
- 受击/死亡映射统一由 `CharacterConfig.Combat.Reactions` 持有；`EnemyDefinition` 禁止重复声明 Action 引用
- `CharacterReactionService` 是玩家/敌人 Health 事件到 Actor 的唯一桥接；Controller 只负责构造并注入 Resolver，禁止各自重复订阅受击/死亡事件
- `CharacterReactionResolver` 直接产出 `CharacterReactionRequest`；默认硬直时长只保存在 `CharacterReactionSet`，State、BrainProfile 与 ActionDefinition 禁止重复配置
- 每次非致命有效命中都可强制重入 HitState；死亡状态是唯一不可被后续受击覆盖的反应终态
- 死亡时先注销 `TargetSystem / CombatActorSystem`，死亡表现完成后再 Despawn

## 相机约定

- 运行时由 CameraManager 搭建层级（CameraRoot → Orbit → Pitch）
- Orbit 用 SmoothDamp 追 CameraRoot；VCam Follow/LookAt 走平滑 Orbit（非直接硬锁角色 Transform）
- 灵敏度、Clamp 等 tunable 值 SerializeField 暴露

## 注释与语言

- 用户可见 Debug 信息可用中文
- 代码标识符英文
- **Agent 新增/改动代码**：须满足 `.cursor/rules/lint-after-code-changes.mdc`（类型顶、public/protected 成员、非 obvious 逻辑均要有注释）
- **人工维护的旧代码**：仅在 non-obvious 逻辑处补简短中文/英文注释即可
- 不写「此文件由 AI 生成」类元注释

## 禁止 / 避免

| 避免 | 原因 |
|------|------|
| Agent 修改美术资产、ScriptableObject 或 Prefab（`Assets/Art/**`、`Assets/Data/**`、`Assets/Prefabs/**`、`.asset`、`.prefab` 等） | 由用户在 Unity Editor 中人工操作；**例外**：Agent 可创建/编写 Shader 源码（`.shader` 等）；见 `no-art-asset-edits.mdc` |
| 改代码后不跑 linter、带着相关报错结束任务 | 必须工具调用 `ReadLints` 并在回复附「代码收尾」清单；见 `lint-after-code-changes.mdc` |
| 改代码后不补充注释就结束任务 | 须满足该规则中的最低注释要求 + 「代码收尾」清单；见 `lint-after-code-changes.mdc` |
| 声称「已完成」但未附「代码收尾」清单 | 有代码改动时清单为强制项；见 `lint-after-code-changes.mdc` |
| 重写逻辑/重构框架时保留旧兼容层（Legacy/Fallback 双轨） | 默认直接切新并删除旧路径；仅用户明确要求才允许阶段迁移；见 `no-legacy-compatibility.mdc` |
| Core → Character/Player 引用 | 破坏分层 |
| 在 State 里直接读 InputReader | 应经 Context 快照，便于 AI/回放测试 |
| 静态 Service Locator 式 Input/Singleton | 与当前组件化方向不一致 |
| 在 Controller 与 State 重复同一业务判断 | 职责漂移；选一处为 source of truth |
| 大范围命名空间重构（未经计划） | 全项目 diff 噪声 |
| 为单个角色运行时业务堆多个必须手工挂载的脚本 | 优先用 `CharacterConfig` + Bootstrap / Facade 装配，跨角色逻辑迁到 System |
| 在 `PlayerController.Awake` 中为业务类 `AddComponent` | 这只是把 Prefab 堆脚本改成运行时堆脚本，必须改为纯 C# service |
| 新增 static event / static registry 作为跨系统业务入口 | 破坏 QFramework 风格架构边界；应使用 Command / Event / System |
| 在 `Domain/**` 中直接调用 `ACTGameArchitecture.Interface` | 破坏下层不依赖上层的分层规则；应由 App 层通过 Query、Command 或构造注入提供依赖 |

## 已废弃模式

（暂无；出现旧模式迁移后在此记录）
