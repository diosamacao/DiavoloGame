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
- **动作系统目录分层**：`Combat/Actions/` 下按职责分四个子目录——`Definitions/`（动作数据 Schema）、`Resolution/`（输入 → 动作选招）、`Execution/`（播放/帧推进/输入路由/旋转）、`Frames/`（Logic Tick 帧上下文契约）；核心脚本不散落在 `Actions/` 根目录
- 跨系统通信使用 `ACTGameArchitecture` 的 Command / Query / Event；动作帧内部可保留 `ActionExecutor` → `ICombatFrameConsumer` 的强时序直连
- 架构事件必须实现 `IArchitectureEvent`；架构查询继承 `ArchitectureQueryBase<TResult>` 或实现 `IArchitectureQuery<TResult>`

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

## 动画约定

- 逻辑层使用 `AnimationKey`，不直接硬编码 Animator 状态名字符串（映射在 Profile）
- 播放走纯 C# `CharacterAnimationService.Play`；需要独占时 `SetLocked(true)`
- Locomotion：`applyRootMotion = false`，位移由 `CharacterController` + `CharacterMotor` 负责
- Action：`ActionDefinition.useRootMotion = true` 时由 `CharacterRootMotionDriver` 在 `OnAnimatorMove` 中把 `deltaPosition` 写入 `CharacterController`；`useRootMotion = false` 时可用 `displacementDistance` 脚本位移
- 同 key 不重复 CrossFade（Controller 内部去重）

## 输入约定

- 使用 Input System + `.inputactions` 资产；Action Map 命名 `Player`
- **采集**：输入源实现 `ICharacterInputSource`，玩家使用纯 C# `InputReader`，敌人可使用 AI 输入源
- **中枢**：`InputManager` 由 `CharacterActor` 持有；`IngestFrame` + `RegisterPressed(string inputId, Action)`
- **离散 id**：Input System Action **名**；出招表 `ActionEntry` → `ActionResolver`（`input` + `resolver` 两字段，缺一即无效）
- **选招层**：起手 / 连段进位 / Dodge 方向分派 / Cancel 下一招统一由 `ActionResolverService` → `ActionResolver`（`Single` / `Combo` / `Directional`）解析；`ActionExecutor` 不做输入查表也不做 `CombatActionType` 特判
- **连招**：Cancel 窗只配帧/输入；下一招由 `ComboActionResolver` 进位（非 Cancel 内 targetAction）
- **战斗模式**：`CombatModeProfile` + `CombatModeService`；与 `PlayerActionSet` 解耦
- **缓冲**：招式中 `Buffer(inputId)`；`ActionExecutor` 经 `IActionInputBuffer` 在 `CancelWindow` 内消费
- 其它系统不直接读 `InputReader` 做玩法判断（移动执行在 `CharacterMotor` / State）
- **玩家装配**：`InputActionAsset` 由 `CharacterConfig` 注入 `InputReader`，不在玩家 Prefab 上重复配置

## 相机约定

- 运行时由 CameraManager 搭建层级（CameraRoot → Orbit → Pitch）
- Follow 目标为 CameraRoot（非角色 Transform 本身）
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
