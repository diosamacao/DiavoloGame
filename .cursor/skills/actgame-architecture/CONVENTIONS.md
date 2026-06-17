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
- 播放走 `CharacterAnimationController.Play`；需要独占时 `SetLocked(true)`
- `applyRootMotion = false`；位移由 CharacterController 负责
- 同 key 不重复 CrossFade（Controller 内部去重）

## 输入约定

- 使用 Input System + `.inputactions` 资产
- Action Map 命名：`Player`；Move/Look 等 Action 名与 `InputReader` 一致
- InputReader 挂在角色 Prefab 上；其他系统通过 SerializeField 引用，不用静态全局

## 相机约定

- 运行时由 CameraManager 搭建层级（CameraRoot → Orbit → Pitch）
- Follow 目标为 CameraRoot（非角色 Transform 本身）
- 灵敏度、Clamp 等 tunable 值 SerializeField 暴露

## 注释与语言

- 用户可见 Debug 信息可用中文
- 代码标识符英文；仅在非 obvious 逻辑处写简短中文/英文注释
- 不写「此文件由 AI 生成」类元注释

## 禁止 / 避免

| 避免 | 原因 |
|------|------|
| Agent 修改美术资产、ScriptableObject 或 Prefab（`Assets/Art/**`、`Assets/Data/**`、`Assets/Prefabs/**`、`.asset`、`.prefab` 等） | 由用户在 Unity Editor 中人工操作；见 `.cursor/rules/no-art-asset-edits.mdc` |
| 改代码后不跑 linter、带着相关报错结束任务 | 必须在当前对话内 `ReadLints` 并修复；见 `.cursor/rules/lint-after-code-changes.mdc` |
| Core → Character/Player 引用 | 破坏分层 |
| 在 State 里直接读 InputReader | 应经 Context 快照，便于 AI/回放测试 |
| 静态 Service Locator 式 Input/Singleton | 与当前组件化方向不一致 |
| 在 Controller 与 State 重复同一业务判断 | 职责漂移；选一处为 source of truth |
| 大范围命名空间重构（未经计划） | 全项目 diff 噪声 |

## 已废弃模式

（暂无；出现旧模式迁移后在此记录）
