# Editor 测试组失败排查（2026-10-01）

用户报告 HitReactionResolverTests、RemoteCharacterProxyTests、RoomArchitectureBoundaryTests 的汇总错误。没有可用 Unity MCP，未操作 Editor 窗口，也未对已打开项目启动 batch mode。

在只读检查 `UserSettings/Layouts/CurrentMaximizeLayout.dwlt` 时发现历史 Test Runner 序列化结果。该缓存修改时间为 2026-09-29，并非本次运行的权威结果；其中以下 7 个子测试的失败断言与当前源码缺陷一致，可独立定位并修复。

| 测试文件 | 修改 |
| --- | --- |
| `Assets/Tests/Editor/Combat/HitReactionResolverTests.cs` | 轻硬直用例以冲击力 1、韧性 1 验证持平边界，原 3 对 1 应为 HeavyStun；Flinch 清边沿用例先断言扣血后仍为 None，再确认 LightStun 建立 Hit，最后确认 Flinch 清除；Phase 叠加用例配置真实临时动画段，避免 OnValidate 将两个窗口都裁到第 0 帧，并在 finally 销毁临时对象。 |
| `Assets/Tests/Editor/Replication/RemoteCharacterProxyTests.cs` | `ApplySnapshot_BindsReadOnlyTargetable_OnHitDoesNotChangeHealth` 和 `TargetingState_AcquiresProxyInRange` 显式使用 Active 队伍状态。原默认 Empty 会隐藏根对象，IsAlive 为 false，因此不能作为可索敌对象。未改变其余用例默认状态。 |
| `Assets/Tests/Editor/Replication/RoomArchitectureBoundaryTests.cs` | Headless 检查更新为实际 `IActionPresentationSink` 的空实现分支，替代已不存在的 presentationEnabled 参数；读取生产源码时排除整行 XML 文档注释，避免 Facade 时序注释里的 PlayerController 误报类型依赖，继续保留实际代码的禁用符号检查。 |

此次只修改三个测试文件；无生产逻辑、资产、场景、Inspector 布线或兼容路径变更。

## 已执行验证

- `%TEMP%/ACTGame-TestFix-20260930/Compile.ps1`：使用 Unity 安装内 Roslyn 独立编译，包含 Assembly-CSharp-Editor 的 9 个程序集成功；这只是辅助编译，不等同 Editor 导入编译。
- `mono.exe EditorLogicRunner.exe`：执行 HitReactionResolverTests 中不涉及 ScriptableObject 的 21 项、RemoteCharacterProxyTests 中 13 项纯逻辑检查，真实 NUnit 断言 **34 passed / 0 failed**。结果在临时目录 `EditorLogicResults.txt`。
- PowerShell 对两个 Facade 的禁用符号扫描和 Headless Sink 分支表达式检查通过。
- 三个修改测试文件的 `git diff --check` 通过。

## 待验证

在 Unity `Window > General > Test Runner > EditMode` 中搜索并运行上述三个类（Assembly-CSharp-Editor）。特别确认 Phase 窗口构造和两个 Active Proxy 用例；这些依赖真实 Unity 对象，尚未执行。本轮没有 Play 路径修改，无需额外 prefab 接线。
