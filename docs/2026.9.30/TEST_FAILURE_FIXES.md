# 2026-09-30 RunTest 失败修复

范围：用户提供的 11 个失败实例（四个方向换人落点分别计数）。本次仅修复对应实现和测试构造，不推进姿态框架迁移。

## 原因与逐文件修改

| 文件 | 修改与原因 |
| --- | --- |
| `Assets/Scripts/Domain/Simulation/SimulationStepKernel.cs` | 以 `1d / LogicHz` 构造累计器；float 的 1/60 转为 double 后略大，50ms 原来只能消费两帧。保留其他模拟 API 的 float 类型。 |
| `Assets/Scripts/Domain/Simulation/Party/PartySwitchPlacement.cs` | 普通换人右侧偏移恢复为 600mm，与既有方案、注释和四方向断言一致。原常量为 1000mm。 |
| `Assets/Scripts/Framework/ACTNet/Transport/ChannelMuxTransport.cs` | 在 Poll 重传前根据底层连接表移除断开连接的可靠状态及已交付队列，避免向已消失连接重传。 |
| `Assets/Scripts/Framework/ACTNet/Session/ServerSession.cs` | 在超时扫描时先同步底层已消失连接，通过现有 DisconnectInternal 清理玩家注册并通知 Gameplay；保留其他在线玩家。UDP 未感知的静默断线仍走原心跳超时。 |
| `Assets/Scripts/App/Server/ServerLaunchConfigResolver.cs` | `-1` 等负数参数不再被误识别为下一选项；交给现有配置校验拒绝，避免静默采用默认值。 |
| `Assets/Tests/Editor/Character/ActionEditorViewportTests.cs` | 吸附预览用真实临时动画段构造 11 帧动作，显式断言总帧数；不再写入被 OnValidate 重算的 totalFrames。保留窗外恢复位移的原断言。 |
| `Assets/Tests/EditMode/ACTGame/Server/DedicatedServerRuntimeTests.cs` | 绑定失败用例通过 LogAssert.Expect 接收预期错误，不移除生产错误日志。 |
| `Assets/Tests/EditMode/ACTGame/Server/ACTGame.Server.EditModeTests.asmdef` | 允许 UnityEngine 引用，用于 LogType 和 Unity Test Framework 日志断言。 |
| `Assets/Tests/EditMode/Simulation/SimulationStepKernelTests.cs` | 增加连续 1000 次 1ms 输入，验证 Peek 与 Consume 一致且恰好消费 60 帧。 |
| `Assets/Tests/EditMode/ACTNet/Transport/ChannelMuxTransportTests.cs` | 增加一端关闭且留有待重传包时，另一端仍正常可靠收包的回归。 |
| `Assets/Tests/EditMode/ACTNet/Session/SessionIntegrationTests.cs` | 增加 Client.Dispose 后下一 Poll 清理该玩家、只通知一次、另一玩家仍可发送应用消息的回归。 |

未新增兼容分支、未删除源文件，无 Inspector 或 prefab 布线变更。

## 已执行的辅助验证

- 当前项目仍在 Unity Editor 打开；未启动该项目的 batch mode。Unity MCP 在本次会话不可用。
- 临时脚本 `%TEMP%/ACTGame-TestFix-20260930/Compile.ps1` 使用 Unity 安装中的 Roslyn `dotnet.exe .../csc.dll @compile.rsp`，读取现有 csproj 的源文件和引用，在临时目录编译，未修改生成的项目文件。
- 9 个程序集独立编译成功：ACTNet.Transport、ACTNet.Session、ACTGame.Simulation、ACTGame.Server、对应 Transport/Session/Simulation/Server EditModeTests，以及 Assembly-CSharp-Editor。此编译是辅助检查，不等同 Unity 导入/编译。
- `mono.exe Runner.exe` 调用真实 NUnit 断言，覆盖 ChannelMuxTransportTests、SessionIntegrationTests、SimulationStepKernelTests、ServerSimulationRunnerTests、ServerLaunchConfigResolverTests，以及 PartyCombatCoordinatorTests 的四个 Placement 用例：**38 passed / 0 failed**。结果位于同一临时目录 Results.txt。
- 全仓 `git diff --check` 发现既有 Vivian asset 的尾随空格；本次未修改这些资产。针对本次源码/测试文件的 `git diff --check -- <文件列表>` 已通过。

## 仍待 Unity Editor 验证

等待 Editor 导入/编译完成，在 `Window > General > Test Runner > EditMode` 复跑：

1. `Assembly-CSharp-Editor` / `ActionEditorViewportTests.AdhesionPreviewMatchesSimulationAndResumesBaseOutsideWindow`。
2. `ACTGame.Server.EditModeTests` / `DedicatedServerRuntimeTests`，尤其 BindFailure_ReturnsBindFailed、EmptyLobbyTimeout_AfterFirstJoin_DoesNotExit、LocalClientDisconnect_DoesNotDestroyRemainingGuest；同时运行 ServerSimulationRunnerTests、ServerLaunchConfigResolverTests。
3. `ACTGame.Simulation.EditModeTests` / PartyCombatCoordinatorTests、SimulationStepKernelTests。
4. `ACTNet.Transport.EditModeTests` / ChannelMuxTransportTests；`ACTNet.Session.EditModeTests` / SessionIntegrationTests。

前三个 Dedicated Runtime 失败及预览用例依赖 Unity 环境，尚未声明通过。Play 冒烟：Gameplay 场景普通换人确认右侧 0.6m 落点；Listen/Dedicated 两客户端加入后退出一个，确认另一玩家继续对局。无资产或 Inspector 操作要求。
