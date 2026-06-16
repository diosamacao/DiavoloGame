# ACTGame 设计方向与重构路线图

> 优先级：P0 阻塞体验 → P1 架构健康 → P2 扩展预备

## 设计原则（长期）

1. **状态机驱动角色表现**：动画、动作阶段、可取消窗口由 State 负责
2. **Controller 只管物理与输入桥接**：PlayerController 提供移动能力数据，逐步减少「业务判断」
3. **Combat 与 Character 解耦**：Hitbox/Hurtbox 在 Combat/，Character State 只触发战斗事件
4. **数据驱动**：数值、动画映射、技能表进 ScriptableObject（Assets/Data/）
5. **小步可验证**：每步可在 Play Mode 单独验证移动/动画/相机

## 进行中的结构迁移

### [P1] 移动职责迁移

**现状**：`PlayerController.Update` 执行位移；`LocomotionState` 只选动画 key。

**目标**：LocomotionState（或 Motor 服务类）成为移动决策点；Controller 降为 Motor 执行层，或合并进 Context.Motor 封装。

**阶段建议**：
- Phase 1：把 `ResolveLocomotionKey` 用到的输入判断与 Controller 对齐文档，避免两处阈值不一致
- Phase 2：移动向量计算移入 State 或 `CharacterMotor`  helper
- Phase 3：PlayerController 精简为只读 Motor 接口

**状态**：未开始

### [P1] ActionState 与战斗管线

**现状**：`ActionState` 占位；Combat/ 目录空。

**目标**：攻击输入 → ActionState → 动画 Lock → Combat Hitbox 窗口。

**依赖**：Input Action（Attack）、AnimationKey 扩展、Hitbox 组件

**状态**：未开始

## 待建设模块

| 模块 | 优先级 | 说明 |
|------|--------|------|
| Enemy/ + AI StateMachine | P2 | 继承 CharacterStateMachine，Context 填 AI 意图 |
| Combat/ | P1 | 伤害、Hitbox、IFrame |
| UI/ | P2 | HUD、血条 |
| Data/Config | P1 | CharacterAnimationProfile 等已有，扩展技能/角色表 |
| 事件总线 | P2 | 轻量 C# event 或 ScriptableObject Event；定稿前不引入第三方 |

## Tech Debt 观察清单

- [ ] `PlayerController` 与 `LocomotionState` 双处感知移动输入（Controller 算移动，State 算动画）
- [ ] `CharacterStateMachine` 与 `PlayerStateMachine` 的 RequireComponent 链需在 Prefab 上验证
- [ ] CameraManager 运行时创建对象，场景重复加载时的生命周期需确认
- [ ] 无 asmdef，全项目单一 Assembly-CSharp（规模大后再拆）

## 已完成

（架构 skill 创建时填入）

- [x] 2026-06-17：建立 Core 泛型状态机 + Character/Player 分层
- [x] 2026-06-17：CharacterAnimationController + Profile 映射模式
- [x] 2026-06-17：InputReader + CameraManager 组件化

## 决策记录

| 日期 | 决策 | 理由 |
|------|------|------|
| 2026-06-17 | 不用 namespace，用文件夹分层 | 当前规模小，减少样板 |
| 2026-06-17 | CharacterController 非 Rigidbody | ACT 地面移动更可控 |
| 2026-06-17 | 状态机 Core 不引用 UnityEngine | 可测试性与分层清晰 |
