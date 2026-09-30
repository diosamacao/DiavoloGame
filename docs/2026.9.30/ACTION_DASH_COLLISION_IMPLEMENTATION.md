# 动作突刺连续碰撞 — 实施与验证记录

> 日期：2026-09-30  
> 对应：[优化方案](ACTION_DASH_COLLISION_PLAN.md)。  
> 状态：业务代码、测试、预览和调试接入；Unity 编译、Test Runner、Play / 网络体验仍待验收。不能视为 DB0～DB4 全部完成。

## 实现结果

动作连续位移现在统一经过 `CharacterMotor.MoveActionMm`。默认策略保留软弹开与静态滑墙；显式选择 `StopOnContact` 后，基础位移和吸附修正都经过 `ActionBodySweep`，在角色圆盘或静态 AABB 的最早接触点停止，动画继续、位移不欠账。此策略碰墙也停止，不沿墙改变已扫描的路径。

身体取逻辑电机半径，与攻击盒和防御窗口独立。无敌/SoftBodySuppress 不移除身体；死亡、停用、阵容离场被过滤。查询只保存本次提交的值快照，下次重新读取当前注册表。

Listen 的 TargetSystem 实际同时注册权威 HurtboxTarget 和 Proxy，因此工厂显式按座位注入源筛选；不能将共享列表未经筛选直接送入身体求解。客户端动作和帧末软分离共用该查询，不写远端 Proxy。Observer 仍不执行动作位移。

## 现场基线

- Vivian Graph 的 `Vivian_Attack_04` 确认引用 GUID `1140ae4b96dfe2a40bf7e84fc36b4e14`；`Vivian_Attack_Branch_Ground` 引用 `1765d3cf85800bc4b81d73fa98a67f7f`，与两份动作 `.meta` 一致。
- Vivian 配置逻辑半径为 0.28m。前一轮解析的峰值步长分别为 2226mm、1943mm，已进入回归用例。
- 通过进程命令行确认 `Unity.exe -projectpath D:\Projects\ACTGame-code` 正在运行。当前会话没有 Unity MCP，因此未启动 Unity batch mode，未执行桌面控制。
- 没有实际采集敌人场景位置、近中远 Play 复现或联网误差，因此 DB0 的场景基线和 DB4 体验出口仍待完成。
- 用户已有的吸附代码、动作/场景资产、Packages 和其他工作区修改均保留；本次不提交、不切换分支。

## 验证结果

| 验证 | 结果 / 限制 |
|---|---|
| 补充程序集编译 | 使用现有 Bee rsp 的引用/宏，在临时目录重新编译 `ACTGame.Simulation`、`ACTGame.Domain.Combat`、`ACTGame.Domain.Character`、`ACTGame.App`、`Assembly-CSharp-Editor`，全部通过；不写 Library，移除临时编译中的 analyzer 参数，不能代替 Unity 导入/生成器编译 |
| 独立 Mono / NUnit | **42 例通过、0 失败**；运行 ActionBodySweepTests、CharacterMotorSimTests、SimStaticCollisionWorldTests、SoftBodySeparationTests、ActionDashPredictionTests、ServerContentManifestTests；不是 Unity Test Runner |
| 几何覆盖 | 2226/1943mm 单步跨体、输入顺序/同点 Id 决胜、初始重叠/同心、切线、退离、墙与身体先后、斜向遇墙、两角色互冲、无位移欠账、空场、静态脱嵌、大坐标；包含 2000 个固定种子的斜向量化案例 |
| 查询与指纹 | 自身/停用/注销/身份变化、单次快照与下一次重新采样、Listen 来源筛选、只读目标、同几何双端结果、新策略/间隙改变指纹 |
| Unity 集成用例 | `ActionDashCollisionIntegrationTests` 已补充编译，涉及 GameObject / AnimationClip / SerializedObject 的 3 例须在 Unity 执行；不在独立 Mono 中伪造 Unity 原生环境 |
| 初次 .NET 9 运行尝试 | 项目 NUnit 的 net35 库依赖 CallContext，42 例中的早期 32 例无法在 .NET 9 执行；改用 Unity 随附 Mono 后同批断言及新增用例通过。此前异常为测试宿主不兼容，不计作通过 |
| 源码清理 | `ApplyBaseDisplacement` 与 `CompareProxyId` 已无定义/调用；角色软分离算法保留，未新增 Legacy / Compat 双轨 |

补充验证输出位于 `%TEMP%/ACTGameDashValidation/`。实际执行入口：

```powershell
& "$env:TEMP/ACTGameDashValidation/CompileChanged.ps1"
dotnet 'C:/Program Files/dotnet/sdk/9.0.316/Roslyn/bincore/csc.dll' "@$env:TEMP/ACTGameDashValidation/compile.rsp"
# MONO_PATH 临时指向补充程序集、项目已有 ScriptAssemblies 与 Unity Managed，执行后恢复。
& 'D:/UnityEngine/Engine/2022.3.62f3c1/Editor/Data/MonoBleedingEdge/bin/mono.exe' "$env:TEMP/ACTGameDashValidation/DashCheck.exe"
```

独立 Mono 微基准（固定输入，每组预热 1000 次，再求解 20000 次；仅测求解器，不代表整帧/整场景预算）：

| 候选圆盘数 | 总耗时 | 平均每次 | 托管分配 |
|---:|---:|---:|---:|
| 10 | 8.96ms | 约 0.45μs | 0B |
| 30 | 23.15ms | 约 1.16μs | 0B |
| 100 | 75.77ms | 约 3.79μs | 0B |

## 源码变更清单

每个本次新增/修改的源文件各一行；同文件已有的用户吸附改动未被清除。

| 文件 | 本次变更 |
|---|---|
| [ActionBodyCollisionMode.cs](../../Assets/Scripts/Domain/Simulation/Character/ActionBodyCollisionMode.cs) | 新增动作软分离/接触停止策略枚举。 |
| [SimBodyObstacle.cs](../../Assets/Scripts/Domain/Simulation/Character/SimBodyObstacle.cs) | 新增只读实体圆盘值快照和输入校验。 |
| [ISimBodyObstacleSource.cs](../../Assets/Scripts/Domain/Simulation/Character/ISimBodyObstacleSource.cs) | 新增实体体积提供契约，与受击资格分离。 |
| [ISimBodyObstacleQuery.cs](../../Assets/Scripts/Domain/Simulation/Character/ISimBodyObstacleQuery.cs) | 新增稳定查询与调用方缓冲契约。 |
| [ActionBodySweep.cs](../../Assets/Scripts/Domain/Simulation/Character/ActionBodySweep.cs) | 新增连续几何检测、稳定决胜、量化复查与规则版本。 |
| [ISimCollisionWorld.cs](../../Assets/Scripts/Domain/Simulation/Character/ISimCollisionWorld.cs) | 扩展无副作用脱嵌/直线扫掠接口。 |
| [OpenFieldSimCollisionWorld.cs](../../Assets/Scripts/Domain/Simulation/Character/OpenFieldSimCollisionWorld.cs) | 实现空场地查询语义。 |
| [SimStaticCollisionWorld.cs](../../Assets/Scripts/Domain/Simulation/Character/SimStaticCollisionWorld.cs) | 增加静态 AABB 直线入射检测，保留原走跑滑墙。 |
| [CharacterMotorSim.cs](../../Assets/Scripts/Domain/Simulation/Character/CharacterMotorSim.cs) | 新增动作原子求解/提交，不暴露任意安全点写入。 |
| [CharacterBodyObstacleQuery.cs](../../Assets/Scripts/Domain/Character/Motion/CharacterBodyObstacleQuery.cs) | 新增注册表查询、来源筛选、稳定排序与重复 Id 检查。 |
| [CharacterHurtboxTarget.cs](../../Assets/Scripts/Domain/Character/Combat/CharacterHurtboxTarget.cs) | 权威目标提供独立身体体积。 |
| [RemoteCharacterProxy.cs](../../Assets/Scripts/App/Presentation/RemoteCharacterProxy.cs) | 远端角色提供只读逻辑体积，过滤不可见阵容成员。 |
| [CharacterActorFactory.cs](../../Assets/Scripts/App/Composition/CharacterActorFactory.cs) | 装配查询及 Authority/Proxy 座位来源筛选。 |
| [CharacterMotor.cs](../../Assets/Scripts/Domain/Character/CharacterMotor.cs) | 基础/吸附统一动作提交、复用查询和碰撞调试信息。 |
| [CharacterActionGameplayStep.cs](../../Assets/Scripts/Domain/Character/Combat/CharacterActionGameplayStep.cs) | 删除重复基础提交分支；统一位移生成、吸附、碰撞顺序，保留脚本位移世界量化。 |
| [CharacterActor.cs](../../Assets/Scripts/Domain/Character/CharacterActor.cs) | 暴露只读体积收集，填充碰撞调试快照。 |
| [CharacterDebugSnapshot.cs](../../Assets/Scripts/Domain/Character/CharacterDebugSnapshot.cs) | 增加动作起点、期望终点、阻挡标志和 Id。 |
| [CombatDebugHudController.cs](../../Assets/Scripts/App/Controllers/Debug/CombatDebugHudController.cs) | F3 显示 ActionBody 与实际 Motor 坐标对照。 |
| [ActionExecutionPolicy.cs](../../Assets/Scripts/Domain/Combat/Actions/Definitions/ActionExecutionPolicy.cs) | 新增 BodyMode/Skin 及配置有效性检查。 |
| [ActionDefinition.cs](../../Assets/Scripts/Domain/Combat/Actions/Definitions/ActionDefinition.cs) | 启动期拒绝非法身体配置。 |
| [ReplicatedFeedbackCoordinator.cs](../../Assets/Scripts/App/Networking/Services/ReplicatedFeedbackCoordinator.cs) | 本机软分离复用 Actor 查询，删除重复 Proxy 采集/排序路径。 |
| [ServerContentManifest.cs](../../Assets/Scripts/App/Networking/Content/ServerContentManifest.cs) | 指纹覆盖动作阻挡配置和规则版本。 |
| [ActionDefinitionAuditUtility.cs](../../Assets/Scripts/Editor/Combat/Motion/ActionDefinitionAuditUtility.cs) | Editor 内容审计报告非法阻挡配置。 |
| [ActionNotifySelectionDrawer.cs](../../Assets/Scripts/Editor/Combat/ActionEditor/Inspectors/ActionNotifySelectionDrawer.cs) | 提供策略语义提示，复用现有 Execution Policy 字段绘制。 |
| [ActionMotionAdhesionSceneDrawing.cs](../../Assets/Scripts/Editor/Combat/Motion/ActionMotionAdhesionSceneDrawing.cs) | 原吸附预览接入同一碰撞求解，支持无吸附窗口的身体预览。 |
| [ActionEditorWindow.cs](../../Assets/Scripts/Editor/Combat/ActionEditor/ActionEditorWindow.cs) | StopOnContact 直接开启预览，显示/调整假敌半径，自身半径取角色配置。 |
| [ActionBodySweepTests.cs](../../Assets/Tests/EditMode/Simulation/ActionBodySweepTests.cs) | 新增连续路径与电机回归用例。 |
| [ActionDashCollisionIntegrationTests.cs](../../Assets/Tests/Editor/Combat/ActionDashCollisionIntegrationTests.cs) | 新增真实 GameplayStep / 预览集成测试，待 Unity 执行。 |
| [ActionDashPredictionTests.cs](../../Assets/Tests/Editor/Replication/ActionDashPredictionTests.cs) | 新增只读查询、生命周期、Listen 隔离与同输入一致性测试。 |
| [ServerContentManifestTests.cs](../../Assets/Tests/Editor/Replication/ServerContentManifestTests.cs) | 新增身体模式和 skin 的指纹差异测试。 |

9 个新 C# 文件均补齐对应 `.meta` GUID。没有移动/删除源文件；删除项为已有类内被替换的方法与采集路径。方案、ARCHITECTURE、TECHNICAL 和 ROADMAP 已同步。

## 待完成的 Editor 与资产步骤

1. 两份动作资产启用授权正在等待用户答复；拟仅在 executionPolicy 写 `bodyCollisionMode: 1`、`bodyContactSkinMm: 20`，不更改烘焙、动画、命中窗口或 Graph。未得到授权前不写资产。
2. Unity 完成脚本导入并检查 Console；当前没有经过 Unity 编译完成确认。
3. Test Runner → EditMode：运行 `ACTGame.Simulation.EditModeTests` 的 ActionBodySweepTests、CharacterMotorSimTests、SimStaticCollisionWorldTests、SoftBodySeparationTests；运行 `Assembly-CSharp-Editor` 的 ActionDashCollisionIntegrationTests、ActionDashPredictionTests、ServerContentManifestTests，并回归 ActionEditorViewportTests。
4. ActionEditor 从 Vivian 角色入口打开两招；Execution Policy 展开选择 StopOnContact、skin 20mm。假敌半径按目标实际配置填写，预览核对绿点；预览不包含场景墙体。
5. Gameplay Play：普通连招进入 Attack04，再按当前 Graph 转入 AttackBranch_Ground；近中远起手、贴身、群怪、墙边、后撤、敌人死亡/移开、连续攻击、穿敌策略逐项验证。F3 对照 ActionBody / Motor，区分模型残差与逻辑根。
6. Listen 与 Dedicated/Client 分别验收；记录实际延迟、动作中峰值位置误差与和解结果。还需验证 30/60/120 渲染 FPS 和整场景 Profiler；独立微基准不代替这些结果。

无需新增场景组件或 Prefab 布线。当前预测 Replay 只重放走跑；未增加动态碰撞历史或动作重放。初始深度重叠和未启用策略的另一角色高速穿入，不在完整两体 CCD 保证范围。

仅在确认 Editor 已关闭后才可使用项目脚本：

```powershell
& ./tools/codex/Invoke-UnityTests.ps1 -TestPlatform EditMode -AssemblyNames ACTGame.Simulation.EditModeTests
& ./tools/codex/Invoke-UnityTests.ps1 -TestPlatform EditMode -AssemblyNames Assembly-CSharp-Editor -TestFilter ActionDashCollisionIntegrationTests
& ./tools/codex/Invoke-UnityTests.ps1 -TestPlatform EditMode -AssemblyNames Assembly-CSharp-Editor -TestFilter ActionDashPredictionTests
```

## 变更日志

| 日期 | 记录 |
|---|---|
| 2026-09-30 | 核心路径与工具接入；42 项独立 Mono 测试通过、5 个程序集补充编译通过；Unity、Play、网络及具体资产启用状态分别列出。 |
