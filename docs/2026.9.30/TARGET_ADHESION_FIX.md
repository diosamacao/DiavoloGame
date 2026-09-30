# TargetAdhesion 指定落点修复

2026-09-30。按用户要求替换旧吸附规则，沿用原有窗口字段，无资产迁移或 Prefab 接线。

## 行为

- 捕获时固定敌我连线偏移轴；正偏移为连线远侧，穿过目标后不翻面。
- 窗内将基础位移重映射到目标，按剩余基础路程保持快慢节奏；无路程时按剩余帧均摊。最后一帧优先精确收敛。
- MaxCorrectionMmPerFrame 只约束中间帧；过小可造成末帧突变。捕获距离和角度只在捕获时检查。
- 目标移动则落点平移；目标切换重新捕获。丢失目标时按 StopOnTargetLost 恢复基础位移或继续向最后落点移动。
- 窗外基础位移继续执行。保证范围是“窗口末帧、有效目标、无碰撞阻挡”；静态墙体、SoftBody 分离及后续 MotionCommand 仍有效，不强制穿墙。

## 源码变更

- `Assets/Scripts/Domain/Simulation/Motion/ActionMotionAdhesion.cs`：捕获状态、完整位移重映射、烘焙路程权重；删除旧 TryComputeCorrectionMm 单向叠加路径。
- `Assets/Scripts/Domain/Simulation/Motion/ActionMotionAdhesionParams.cs`：明确捕获轴与末帧限幅语义。
- `Assets/Scripts/Domain/Character/Combat/CharacterActionGameplayStep.cs`：按动作/窗口/目标管理捕获状态，重映射后只移动一次；窗外保留原始电机入口。
- `Assets/Scripts/Domain/Combat/Actions/Definitions/Timeline/MotionModifierNotifyState.cs`：更新偏移、限幅、捕获及目标丢失说明。
- `Assets/Scripts/Domain/Combat/Actions/Definitions/Timeline/MotionModifierMode.cs`：更新 TargetAdhesion 职责注释。
- `Assets/Scripts/Editor/Combat/Motion/ActionMotionAdhesionSceneDrawing.cs`：使用相同算法重放预览，保留捕获后的 Desired 点，不在穿敌后翻面。
- `Assets/Scripts/Editor/Combat/ActionEditor/Inspectors/ActionNotifySelectionDrawer.cs`：显示新规则及碰撞/窗外边界。
- `Assets/Tests/EditMode/Simulation/ActionMotionAdhesionTests.cs`：替换旧规则断言，覆盖方向、偏移、过冲、捕获条件、末帧、窗口外与节奏。
- `Assets/Tests/Editor/Character/ActionEditorViewportTests.cs`：新增预览与纯计算逐帧一致及窗外恢复位移检查。
- `.agents/skills/actgame-architecture/TECHNICAL.md`：同步行为和限制。

## 验证

- Roslyn 补充编译 Simulation、Combat、Character、Enemy、App、Editor（含 Editor 测试）通过。使用 `.utmp/authoring-compile/ACTGame.Simulation.rsp` 与现有 `.utmp/check_authoring_compile.py`；不是 Unity 编译结论。
- 实际 `ActionMotionAdhesionTests` 在 Unity 随附 Mono 下通过临时反射运行器执行：12 个 NUnit 测试用例通过。最初用 .NET 9 执行遇到旧版 NUnit CallContext 不兼容，改用 Mono 后通过；未将环境错误当作功能失败。
- 只读加载 Unagi_Attack_Counter 当前烘焙数组及吸附参数：敌人 Z=2000/4000/7000 mm 时，第 25 帧结果 Z=4000/6000/9000 mm，均符合 +2000 mm。3 个真实数据案例通过，合计 15 项补充检查通过。
- 临时验证程序位于 `.utmp/adhesion-check/Program.cs`（另有编译配置和产物），未新增 Python 文件，未改写角色资源。
- Unity MCP 不可用且项目已有 Editor 进程，没有启动 batch mode；未宣称 Unity Test Runner 或 Play Mode 验收通过。

## 剩余 Editor 验收

1. 等待 Unity 编译完成，确认 Console 无编译错误。
2. Test Runner → EditMode → `ACTGame.Simulation.EditModeTests / ActionMotionAdhesionTests`；`Assembly-CSharp-Editor / ActionEditorViewportTests.AdhesionPreviewMatchesSimulationAndResumesBaseOutsideWindow`。
3. ActionEditor 打开 Unagi_Attack_Counter，选中 MotionModifier_1，假敌置于前方 2/4/7 m，Scrub 到 25 帧检查绿色落点与黄色 Desired 重合；26 帧后按基础位移继续。
4. Gameplay 场景 Play → 切入 Unagi 格挡成功 → 派生攻击；检查近/远敌人、移动目标、穿敌、墙边及卡肉。联机同时检查 Host 与本机预测角色。角色分离若影响末帧落点，确认 SoftBodySuppress 覆盖吸附窗口。
