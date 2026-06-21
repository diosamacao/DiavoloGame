# ACTGame 设计方向与重构路线图

> 优先级：P0 阻塞体验 → P1 架构健康 → P2 扩展预备

## 设计原则（长期）

1. **状态机驱动角色表现**：动画、动作阶段、可取消窗口由 State 负责
2. **Controller 作为装配与 Motor 入口**：PlayerController 通过 CharacterConfig 生成角色运行时；招式路由在 CharacterActionDriver
3. **Combat 与 Character 解耦**：Hitbox 拉取 `IActionRuntime`；Character State 只 Tick Runtime
4. **Logic Tick = 编辑器帧**：`UpdateFrame` 统一 Play Mode 与 ActionEditor Scrub
5. **数据驱动**：数值、动画映射、技能表进 ScriptableObject（Assets/Data/）
6. **小步可验证**：每步可在 Play Mode 单独验证移动/动画/战斗

## 进行中的结构迁移

### [P1] 移动职责迁移

**现状**：`PlayerController.Update` 执行 Locomotion 位移；`LocomotionState` 只选动画 key。

**目标**：LocomotionState（或 Motor 服务类）成为移动决策点；Controller 降为 Motor 执行层。

**状态**：未开始（招式侧职责迁移已完成，见下）

### [P1] ActionEditor 准备 — 动作系统职责收敛 ✅ 2026-06-21

**已完成**：

- `CharacterActionDriver`：离散输入起手/缓冲、移动取消（角色无关）
- `ActionRotationDriver`：RotationWindow + 索敌
- `ActionRuntimeController.UpdateFrame` + `ICombatFrameConsumer`（Hitbox/VFX 统一 Logic Tick）
- `ActionPhase` / `ActionEvent` 类型骨架写入 `ActionDefinition`
- `IActionHitReceiver` 命中回流 + `OnHitConfirm` / `OnWhiff` Transition
- `IActionRuntime` 迁至 `Combat/Actions/`

**下一步（ActionEditor M5 前）**：

- [ ] `ActionEditorWindow` 基础版（列表 + Scrub 调 `UpdateFrame`）
- [x] 2026-06-21：ActionEvent 运行时派发入口（Hitbox/VFX 仍兼容旧数组）
- [ ] `ActionDefinition` 子 SO 拆分（CombatData / PresentationData，可选）

### [P1] 战斗闭环

**现状**：Hitbox OBB + 命中反馈（震屏/卡肉）；`OnHitConfirm` Transition 已可配置。

**待做**：伤害结算、`Hit` 状态、受击 `ActionDefinition` 衔接。

**状态**：部分完成

## 待建设模块

| 模块 | 优先级 | 说明 |
|------|--------|------|
| ActionEditorWindow | P1 | Frameline + Scrub 对接 `UpdateFrame` |
| Enemy/ + AI | P2 | 复用 `CharacterActionDriver` + `ActionRuntimeController` |
| UI/ | P2 | HUD、血条 |
| 事件总线 | P2 | 轻量 C# event；定稿前不引入第三方 |

## Tech Debt 观察清单

- [ ] `PlayerController` 与 `LocomotionState` 双处感知移动输入
- [x] 2026-06-21：Prefab 手动挂载 `CharacterActionDriver`、`ActionRotationDriver` 改为 `CharacterConfig` + `PlayerController` 运行时装配
- [ ] `TargetRegistry` 仍为静态全局列表，后续可替换为空间分区 / 场景实例注册表
- [ ] 无 asmdef，全项目单一 Assembly-CSharp

## 已完成

- [x] 2026-06-17：建立 Core 泛型状态机 + Character/Player 分层
- [x] 2026-06-17：CharacterAnimationController + Profile 映射模式
- [x] 2026-06-17：InputReader + CameraManager 组件化
- [x] 2026-06-17：动作系统 Phase A（ActionRuntime、Combo、CombatMode、Hitbox 骨架）
- [x] 2026-06-21：ActionEditor 准备重构（CharacterActionDriver、UpdateFrame、Phase/Event 骨架、命中回流）
- [x] 2026-06-21：CharacterConfig 装配入口、ActionSession、TargetRegistry / HitDetectionSystem / TargetingSystem 骨架

## 决策记录

| 日期 | 决策 | 理由 |
|------|------|------|
| 2026-06-17 | 不用 namespace，用文件夹分层 | 当前规模小，减少样板 |
| 2026-06-17 | CharacterController 非 Rigidbody | ACT 地面移动更可控 |
| 2026-06-17 | 状态机 Core 不引用 UnityEngine | 可测试性与分层清晰 |
| 2026-06-21 | 连招保持 `ActionComboSequence` 线性 | 近期无分支图需求 |
| 2026-06-21 | 输入路由命名 `CharacterActionDriver` | 敌人复用同一组件 |
