# 战斗姿态资源实施与撤回记录

> 当前状态：2026-10-01 用户要求暂停并撤销整个姿态方案，全部相关代码已撤回。方案仅保留设计，不继续实施。下方历史实施和验证记录不代表当前代码。

## 2026-10-01 撤回清单

| 文件 | 撤回结果 |
|---|---|
| Assets/Scripts/Domain/Combat/CombatModeTypes.cs | 恢复原 CombatModeType 枚举，撤销刚开始的 CombatModeId 改动 |
| Assets/Scripts/Domain/Combat/Resources/CharacterResourceConfig.cs | 删除自定义资源配置字段 |
| Assets/Scripts/Domain/Combat/Resources/ActionResourceSpec.cs | 删除自定义费用、命中回复字段及构造参数 |
| Assets/Scripts/Domain/Combat/Numeric/CharacterNumericConfig.cs | 删除自定义池初始化配置 |
| Assets/Scripts/Domain/Combat/Numeric/NumericSystem.cs | 删除自定义资源池成员与注册逻辑 |
| Assets/Scripts/Domain/Combat/Numeric/ActionResourceSpecEffectCompiler.cs | 删除自定义资源扣费、检查与回复接线 |
| Assets/Scripts/Domain/Combat/Resources/ResourcePoolDefinition.cs | 删除方案新增文件及 meta |
| Assets/Scripts/Domain/Combat/Numeric/ResourcePoolSet.cs | 删除方案新增文件及 meta |
| Assets/Tests/EditMode/Domain/ResourcePoolSetTests.cs | 删除方案新增测试及 meta |

6 个已有源码文件经逐处反向补丁恢复，git diff 无差异；被中断的其余模式消费者迁移没有写入。新增文件删除前已逐一备份并验证 SHA256，备份目录：`C:/Users/Diavolo/AppData/Local/Temp/ACTGame-CombatRollback-5c9b59d6bebd4b82aae8c9fa18b77407`。全库源码引用检查无资源池或 CombatModeId 残留。

此前两轮测试修复、用户原有角色资产/场景/动画及动作碰撞修改均保留。架构和技术文档删除资源池已实现描述，路线图与方案标记暂停。不运行新测试或 Unity batch mode；本次撤回通过源码差异及引用检查确认，Unity 重新导入编译未验证。无 Inspector 或 prefab 布线要求。

## 历史：已撤回的源码修改

| 文件 | 本轮修改 |
|---|---|
| Assets/Scripts/Domain/Combat/Resources/ResourcePoolDefinition.cs | 新增稳定资源 Id、容量、初值定义，以及动作费用/回复量数据；附 meta |
| Assets/Scripts/Domain/Combat/Numeric/ResourcePoolSet.cs | 新增资源存储、原子扣费、回复、整数衰减余量及完整 Capture/Restore；附 meta |
| Assets/Scripts/Domain/Combat/Resources/CharacterResourceConfig.cs | 增加角色自定义资源数组 |
| Assets/Scripts/Domain/Combat/Numeric/CharacterNumericConfig.cs | 将角色资源定义传到 Numeric 初始化 |
| Assets/Scripts/Domain/Combat/Numeric/NumericSystem.cs | 独占资源集合并注册配置；已有生命/能量/喧响路径保持 |
| Assets/Scripts/Domain/Combat/Resources/ActionResourceSpec.cs | 自定义起手费用、命中回复及构造接口 |
| Assets/Scripts/Domain/Combat/Numeric/ActionResourceSpecEffectCompiler.cs | 将动作的自定义费用与命中回复连接到同一资源集合 |
| Assets/Tests/EditMode/Domain/ResourcePoolSetTests.cs | 新增七项资源测试；附 meta |

未修改角色资产、Prefab、场景或美术。工作区原有动作碰撞等改动保留。曾修改的 CombatModeEntry/CharacterCombatModes 类型已精确撤回，git diff 确认无本轮残留；被拒绝的核心补丁未应用。

## 历史：撤回前的验证

- `git diff --check`（本轮五个修改的已有源码）：通过。
- Unity 自带 `NetCoreRuntime/dotnet.exe DotNetSdkRoslyn/csc.dll`：隔离编译 Resources/*.cs、Numeric/*.cs 和两组测试成功，输出仅在系统临时目录 `ACTGame-ResourcePool-20260930`。首次缺少 netstandard 引用，补充 facade 后成功。
- Unity 自带 Mono 运行临时反射测试入口：ResourcePoolSetTests 7 项、既有 NumericSystemTests 7 项，共 14 通过、0 失败。使用真实 NUnit 断言；这是辅助方法执行，不是 Unity Test Runner 结果。
- 未启动 Unity batch mode。已确认该项目正在 Editor 中打开（PID 35876）；当前无 Unity MCP 工具。
- Unity 编译和 Test Runner 尚未验证。人工路径：Window → General → Test Runner → EditMode → ACTGame.Domain.EditModeTests，运行 ResourcePoolSetTests 与 NumericSystemTests。

## 历史：撤回前的剩余工作与阻塞

自动审批拒绝了核心迁移补丁：替换模式类型/接口、迁移 SwitchCombatMode 起手行为、增加 ActionSim 结束原因。给出的风险为跨运行时层改动及未完成消费者迁移可能破坏编译和行为，要求更明确授权。未通过拆分同一补丁绕过拒绝。

仍未实现：资源绑定的模式生命周期、耗尽退出、N 姿态 ID、-1 时长编译、切换动作提交点、完整移动配置切换、预测/网络复制和 Editor 模式配置。ResourcePoolSet.Drain 目前只提供显式调用 API，没有自动衰减调用者；Capture/Restore 没有接入网络快照。通用 Effect 的自定义资源增减也尚未接入。

当时尚未具备 Vivian Play 验收条件。2026-10-01 已按用户要求撤回实现，此段仅记述历史，不作为继续实施请求。
