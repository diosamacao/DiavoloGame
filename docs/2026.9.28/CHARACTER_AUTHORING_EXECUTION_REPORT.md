# 角色作者链路执行报告

日期：2026-09-28。状态：代码及资产迁移已落地，**整套方案尚未验收完成**。没有提交、推送或切换分支。

## 已落地

角色配置取消两个独立 Profile：身份 → 身体（内嵌模式）→ Graph / Locomotion（内嵌 Clip）。不计动作与表现资源，单模式基础配置由六类减少到四类。

菜单 `ACT/Character Workbench` 提供角色入口。模板复制自动复制私有 SO 与重连引用；追加动作自动绑定图/反应；批量 Clip 使用显式普攻/闪避初值，不猜玩法关键帧。动作窗口补资源，挂点从模型名字选择；烘焙按角色闭包并列出共享影响。

旧链路删除：CombatModeProfile、CharacterAnimationProfile 类型与 8 个对应资产/meta；三个身份预留标签；动作可自由填写 sampleRate；Graph Inspector 的独立校验算法。没有运行时双读或适配壳。

移动根位移现在保存 Clip 来源指纹；动作 RM 的旧指纹只含路径/时长/帧率，已修正为 GUID/fileID/导入依赖哈希，同长度曲线变更也会过期。已有旧指纹会显示 Dirty，需要显式重新烘焙；本次未自动重烘焙生产资产。

## 实际验证

| 操作 | 结果 |
|---|---|
| `.utmp/check_authoring_compile.py`（bundled Python + dotnet Roslyn + 现有 Unity 引用） | Domain.Combat、Domain.Character、Domain.Enemy、App、Assembly-CSharp-Editor 共 5 个编译目标退出码 0。仅补充编译。 |
| `.utmp/verify_authoring_and_report.py` | 62 个保留资产与基线预期变换完全一致；保留资产 meta 字节不变；48 个 sampleRate 原值均 60；8 个已删 Profile 的 GUID 无序列化残留。 |
| 生产 Scripts/Tests 旧类型、三个标签、字符串 sampleRate 扫描 | 无命中（rg 退出码 1 表示无匹配）。 |
| Unity GUI 接入 | 找到已打开的 2022.3.62f3c1 Editor；激活请求返回 `Computer Use app approval timed out`。未绕过授权、未启动第二个 Editor。 |
| Unity 编译 / Test Runner / Play | Unity 实际加载最新源码并运行 62 项测试，全部通过（含 16 项新增测试）；Play 未执行。 |

迁移语义证据：`CHARACTER_AUTHORING_SEMANTIC_CHECK.json`、`CHARACTER_AUTHORING_MIGRATION_RESULT.json`、`CHARACTER_AUTHORING_MIGRATION_MANIFEST.md`。可恢复的原始资产/meta 字节在本地忽略目录 `.utmp/character-authoring-baseline.json`，源码原始快照 `.utmp/authoring-source-baseline.json`；不要删除这些备份。

## 剩余工作与验收入口

1. CA0 索引覆盖 451 个声明；完整最终消费者/适用条件还未逐字段追完。未确认的字段均保留，不能说“所有字段都有用”。
2. 非图问题部分只定位到配置分组，尚未全部转换为 Domain 的精确字段问题；移动 RootMotion 来源指纹已补齐但尚待 Editor 验证。工作台检查通过也不代表 Ready。
3. 定向 Unity 测试已通过；后续复验可在现有 Unity 内执行 Assets → Refresh，等待编译。菜单 `ACT/Character/Run Authoring Validation` 运行结构审计及定向 EditMode 测试。结果写 `.utmp/authoring-validation/results.xml`、`audit.txt`、`status.txt`、`console.log`；必须检查失败详情与测试数量，不能只看菜单完成。
4. Test Runner：EditMode / Assembly-CSharp-Editor，重点 CharacterAuthoringCreationTests、CharacterAuthoringTimingTests、ActionGraphValidatorTests、LocomotionIntegerClockTests、GameContentCatalogTests、GameContentBootstrapBoundaryTests、ActionReplicationCatalogTests、ServerContentManifestTests、NullAnimationPlaybackTests、PartyExitFromHitTests、AssistParryPipelineTests、AssistParryHitStopTests、AssistParryHitStopCarryTests；另覆盖 ActionSimTests 所属程序集。
5. 当前 Gameplay 场景 Play：Anbi/Unagi 移动、起停、转身、普攻、闪避、换人、受击与极限支援；客户端/Listen/Dedicated 的内容启动和远端播放分别检查，比较同版内容指纹。未改场景、Prefab 或 PartyLoadout。
6. 编辑器操作检查：复制 A 为新角色 B；确认 B 独有动作修改不影响 A，Clip 共享；创建单动作/批量草稿、资源字段、挂点、图问题定位、Undo/Redo、切角色 RM 目录、关闭隔离预览。新角色手工在 PartyLoadout 的槽位绑定 CharacterDefinition 后再 Play。
7. 全部验收通过后删除一次性导出工具。当前保留工具与备份供调查，未标 CA6 关闭。

## 每个源文件的改动

- [Assets/Scripts/App/Composition/CharacterActorFactory.cs](D:/Projects/ACTGame-code/Assets/Scripts/App/Composition/CharacterActorFactory.cs)：读取现行内嵌模式或 Locomotion Clip 映射，去除旧 Profile API。
- [Assets/Scripts/App/Networking/Adapters/ActRemoteProxyFactory.cs](D:/Projects/ACTGame-code/Assets/Scripts/App/Networking/Adapters/ActRemoteProxyFactory.cs)：读取现行内嵌模式或 Locomotion Clip 映射，去除旧 Profile API。
- [Assets/Scripts/Domain/Character/Animation/AnimationKey.cs](D:/Projects/ACTGame-code/Assets/Scripts/Domain/Character/Animation/AnimationKey.cs)：读取现行内嵌模式或 Locomotion Clip 映射，去除旧 Profile API。
- [Assets/Scripts/Domain/Character/Animation/CharacterAnimationProfile.cs](D:/Projects/ACTGame-code/Assets/Scripts/Domain/Character/Animation/CharacterAnimationProfile.cs)：删除旧独立 Profile 类型；数据已迁入唯一新结构。
- [Assets/Scripts/Domain/Character/Animation/CharacterAnimationService.cs](D:/Projects/ACTGame-code/Assets/Scripts/Domain/Character/Animation/CharacterAnimationService.cs)：读取现行内嵌模式或 Locomotion Clip 映射，去除旧 Profile API。
- [Assets/Scripts/Domain/Character/CharacterConfig.cs](D:/Projects/ACTGame-code/Assets/Scripts/Domain/Character/CharacterConfig.cs)：以内嵌 CombatModes 取代模式资产引用。
- [Assets/Scripts/Domain/Character/Combat/CharacterCombatModes.cs](D:/Projects/ACTGame-code/Assets/Scripts/Domain/Character/Combat/CharacterCombatModes.cs)：承载模式值与启动期共用图校验。
- [Assets/Scripts/Domain/Character/Combat/CombatModeEntry.cs](D:/Projects/ACTGame-code/Assets/Scripts/Domain/Character/Combat/CombatModeEntry.cs)：独立模式条目类型，保留 Graph/Locomotion 绑定。
- [Assets/Scripts/Domain/Character/Combat/CombatModeProfile.cs](D:/Projects/ACTGame-code/Assets/Scripts/Domain/Character/Combat/CombatModeProfile.cs)：删除旧独立 Profile 类型；数据已迁入唯一新结构。
- [Assets/Scripts/Domain/Character/Combat/CombatModeService.cs](D:/Projects/ACTGame-code/Assets/Scripts/Domain/Character/Combat/CombatModeService.cs)：读取现行内嵌模式或 Locomotion Clip 映射，去除旧 Profile API。
- [Assets/Scripts/Domain/Character/Locomotion/CharacterLocomotionProfile.cs](D:/Projects/ACTGame-code/Assets/Scripts/Domain/Character/Locomotion/CharacterLocomotionProfile.cs)：合并 Clip 映射与混合参数，暴露作者时序只读视图。
- [Assets/Scripts/Domain/Character/Locomotion/LocomotionAnimSet.cs](D:/Projects/ACTGame-code/Assets/Scripts/Domain/Character/Locomotion/LocomotionAnimSet.cs)：读取现行内嵌模式或 Locomotion Clip 映射，去除旧 Profile API。
- [Assets/Scripts/Domain/Character/Party/CharacterDefinition.cs](D:/Projects/ACTGame-code/Assets/Scripts/Domain/Character/Party/CharacterDefinition.cs)：删除三个未使用标签。
- [Assets/Scripts/Domain/Character/Replication/ActionReplicationCatalog.cs](D:/Projects/ACTGame-code/Assets/Scripts/Domain/Character/Replication/ActionReplicationCatalog.cs)：读取现行内嵌模式或 Locomotion Clip 映射，去除旧 Profile API。
- [Assets/Scripts/Domain/Combat/Actions/Definitions/ActionDefinition.cs](D:/Projects/ACTGame-code/Assets/Scripts/Domain/Combat/Actions/Definitions/ActionDefinition.cs)：删除作者 sampleRate，固定 60Hz；保留派生帧数。
- [Assets/Scripts/Domain/Combat/Actions/Validation/ActionGraphValidator.cs](D:/Projects/ACTGame-code/Assets/Scripts/Domain/Combat/Actions/Validation/ActionGraphValidator.cs)：Domain 共用图校验与结构化问题。
- [Assets/Scripts/Editor/Architecture/StructureValidationBatch.cs](D:/Projects/ACTGame-code/Assets/Scripts/Editor/Architecture/StructureValidationBatch.cs)：图扫描接入同一 Domain 校验。
- [Assets/Scripts/Editor/Character/CharacterActionBatchWindow.cs](D:/Projects/ACTGame-code/Assets/Scripts/Editor/Character/CharacterActionBatchWindow.cs)：预览批量 Clip 名字和用途，生成绑定草稿。
- [Assets/Scripts/Editor/Character/CharacterAuthoringFields.cs](D:/Projects/ACTGame-code/Assets/Scripts/Editor/Character/CharacterAuthoringFields.cs)：条件切模式字段与名字语义挂点选择。
- [Assets/Scripts/Editor/Character/CharacterAuthoringPreviewWindow.cs](D:/Projects/ACTGame-code/Assets/Scripts/Editor/Character/CharacterAuthoringPreviewWindow.cs)：隔离模型预览和生命周期清理。
- [Assets/Scripts/Editor/Character/CharacterAuthoringService.cs](D:/Projects/ACTGame-code/Assets/Scripts/Editor/Character/CharacterAuthoringService.cs)：模板闭包复制、重映射、动作/批量创建与失败清理。
- [Assets/Scripts/Editor/Character/CharacterAuthoringValidationRunner.cs](D:/Projects/ACTGame-code/Assets/Scripts/Editor/Character/CharacterAuthoringValidationRunner.cs)：已打开 Editor 内的结构审计与定向测试入口，输出 XML。
- [Assets/Scripts/Editor/Character/CharacterAuthoringWindow.cs](D:/Projects/ACTGame-code/Assets/Scripts/Editor/Character/CharacterAuthoringWindow.cs)：角色工作台、模板/动作创建对话框、角色范围检查与烘焙。

- [Assets/Scripts/Editor/Character/CharacterValidationPanel.cs](D:/Projects/ACTGame-code/Assets/Scripts/Editor/Character/CharacterValidationPanel.cs)：聚合校验问题，打开字段编辑与图节点定位。
- [Assets/Scripts/Editor/Combat/ActionEditor/ActionDefinitionCreateUtility.cs](D:/Projects/ACTGame-code/Assets/Scripts/Editor/Combat/ActionEditor/ActionDefinitionCreateUtility.cs)：读取现行内嵌模式或 Locomotion Clip 映射，去除旧 Profile API。
- [Assets/Scripts/Editor/Combat/ActionEditor/ActionEditorWindow.cs](D:/Projects/ACTGame-code/Assets/Scripts/Editor/Combat/ActionEditor/ActionEditorWindow.cs)：角色/模式动作范围、隔离预览、重载恢复与角色跳转。
- [Assets/Scripts/Editor/Combat/ActionEditor/ActionListPanel.cs](D:/Projects/ACTGame-code/Assets/Scripts/Editor/Combat/ActionEditor/ActionListPanel.cs)：角色引用范围列表，保留全项目入口。
- [Assets/Scripts/Editor/Combat/ActionEditor/ActionToolbar.cs](D:/Projects/ACTGame-code/Assets/Scripts/Editor/Combat/ActionEditor/ActionToolbar.cs)：角色入口与上下文预览锁定。
- [Assets/Scripts/Editor/Combat/ActionEditor/Inspectors/ActionNotifySelectionDrawer.cs](D:/Projects/ACTGame-code/Assets/Scripts/Editor/Combat/ActionEditor/Inspectors/ActionNotifySelectionDrawer.cs)：资源字段、挂点选择、高级窗口排序与固定 Hz 展示。
- [Assets/Scripts/Editor/Combat/ActionGraph/ActionGraphEditorWindow.cs](D:/Projects/ACTGame-code/Assets/Scripts/Editor/Combat/ActionGraph/ActionGraphEditorWindow.cs)：图问题节点/顺序组定位，条件显示切模式字段。
- [Assets/Scripts/Editor/Combat/ActionGraph/ActionGraphInspector.cs](D:/Projects/ACTGame-code/Assets/Scripts/Editor/Combat/ActionGraph/ActionGraphInspector.cs)：移除重复校验算法，复用 Domain 并条件显示切模式字段。
- [Assets/Scripts/Editor/Enemy/BehaviorTree/EnemyBehaviorTreeCombatEntryPicker.cs](D:/Projects/ACTGame-code/Assets/Scripts/Editor/Enemy/BehaviorTree/EnemyBehaviorTreeCombatEntryPicker.cs)：读取现行内嵌模式或 Locomotion Clip 映射，去除旧 Profile API。
- [Assets/Scripts/Editor/Locomotion/CharacterLocomotionProfileEditor.cs](D:/Projects/ACTGame-code/Assets/Scripts/Editor/Locomotion/CharacterLocomotionProfileEditor.cs)：合并移动表单，生成数据只读，Timing 保留作者值。
- [Assets/Scripts/Editor/Locomotion/LocomotionTimingAudit.cs](D:/Projects/ACTGame-code/Assets/Scripts/Editor/Locomotion/LocomotionTimingAudit.cs)：去除动画资产前置依赖，复用现行校验。
- [Assets/Scripts/Editor/Locomotion/LocomotionTimingBaker.cs](D:/Projects/ACTGame-code/Assets/Scripts/Editor/Locomotion/LocomotionTimingBaker.cs)：直接读映射，保护已有交接帧并拒绝非法时序。
- [Assets/Tests/Editor/Character/CharacterAuthoringTests.cs](D:/Projects/ACTGame-code/Assets/Tests/Editor/Character/CharacterAuthoringTests.cs)：新增 16 个复制、失败清理、Timing 与图规则测试；尚未执行。
- [Assets/Tests/Editor/Combat/AssistParryHitStopCarryTests.cs](D:/Projects/ACTGame-code/Assets/Tests/Editor/Combat/AssistParryHitStopCarryTests.cs)：测试构造改用新动画映射/固定 60Hz 结构，保留原有行为断言。
- [Assets/Tests/Editor/Combat/AssistParryHitStopTests.cs](D:/Projects/ACTGame-code/Assets/Tests/Editor/Combat/AssistParryHitStopTests.cs)：测试构造改用新动画映射/固定 60Hz 结构，保留原有行为断言。
- [Assets/Tests/Editor/Combat/AssistParryPipelineTests.cs](D:/Projects/ACTGame-code/Assets/Tests/Editor/Combat/AssistParryPipelineTests.cs)：测试构造改用新动画映射/固定 60Hz 结构，保留原有行为断言。
- [Assets/Tests/Editor/Combat/PartyExitFromHitTests.cs](D:/Projects/ACTGame-code/Assets/Tests/Editor/Combat/PartyExitFromHitTests.cs)：测试构造改用新动画映射/固定 60Hz 结构，保留原有行为断言。
- [Assets/Tests/Editor/Locomotion/LocomotionIntegerClockTests.cs](D:/Projects/ACTGame-code/Assets/Tests/Editor/Locomotion/LocomotionIntegerClockTests.cs)：测试构造改用新动画映射/固定 60Hz 结构，保留原有行为断言。
- [Assets/Tests/Editor/Replication/GameContentBootstrapBoundaryTests.cs](D:/Projects/ACTGame-code/Assets/Tests/Editor/Replication/GameContentBootstrapBoundaryTests.cs)：更新源代码路径断言到现行模式类型。
- [Assets/Tests/Editor/Replication/NullAnimationPlaybackTests.cs](D:/Projects/ACTGame-code/Assets/Tests/Editor/Replication/NullAnimationPlaybackTests.cs)：测试构造改用新动画映射/固定 60Hz 结构，保留原有行为断言。

- [LocomotionRootMotionFingerprint.cs](D:/Projects/ACTGame-code/Assets/Scripts/Editor/Locomotion/LocomotionRootMotionFingerprint.cs)：移动根位移来源指纹与 Dirty 判定。
- [RootMotionBakeUtility.cs](D:/Projects/ACTGame-code/Assets/Scripts/Editor/Combat/Motion/RootMotionBakeUtility.cs)：修正同长度 Clip 曲线修改未触发 Dirty 的指纹缺口。

补充：`git diff --check` 通过（仅 LF/CRLF 提示，无空白错误）。结构化 Graph 问题可精确到节点与字段；非 Graph 精确问题与逐字段消费审计尚未达到方案全部出口。


## 最新统一验收结果（覆盖前述接入受阻时的状态）

Unity 在后台于本地 23:30 加载最新脚本，23:32 定向测试完成：**62 通过，0 失败，0 跳过**。其中 CharacterAuthoringCreationTests=6、CharacterAuthoringTimingTests=5、ActionGraphValidatorTests=5，其余 46 项为原有行为回归。完整 XML 已归档。

全项目审计仍未通过：**85 项 = 51 条结构规则 + 33 个动作烘焙问题 + 1 个移动配置问题**。本次新增的结构违规已拆文件修复，与原始源码比较新增为 0；Special 同意图普通/EX 多候选误报已修正并有测试。生产资产问题在原始基线中已经存在，未擅自缩放动作时序或重烘焙。详见 CHARACTER_AUTHORING_CONTENT_ISSUES.md。

仍需完成的方案出口：CA0 全字段最终消费审计；非图问题精细字段结构化；Play 与双进程实际表现；原有内容问题处置后再过全内容门禁；最后移除一次性迁移工具。当前没有运行中的测试，也没有待消费的测试请求。

- [ContentIssueSeverity.cs](D:/Projects/ACTGame-code/Assets/Scripts/Domain/Combat/Actions/Validation/ContentIssueSeverity.cs)：拆出共用问题等级类型。
- [ContentValidationIssue.cs](D:/Projects/ACTGame-code/Assets/Scripts/Domain/Combat/Actions/Validation/ContentValidationIssue.cs)：拆出结构化问题契约。
- [CharacterCreateWindow.cs](D:/Projects/ACTGame-code/Assets/Scripts/Editor/Character/CharacterCreateWindow.cs)：拆出模板创建窗口。
- [CharacterActionCreateWindow.cs](D:/Projects/ACTGame-code/Assets/Scripts/Editor/Character/CharacterActionCreateWindow.cs)：拆出角色上下文单动作创建窗口。
- [CharacterDefinitionAuthoringInspector.cs](D:/Projects/ACTGame-code/Assets/Scripts/Editor/Character/CharacterDefinitionAuthoringInspector.cs)：身份 Inspector 工作台入口。
- [CharacterConfigAuthoringInspector.cs](D:/Projects/ACTGame-code/Assets/Scripts/Editor/Character/CharacterConfigAuthoringInspector.cs)：身体 Inspector 字段分组与工作台入口。
- [EnemyDefinitionAuthoringInspector.cs](D:/Projects/ACTGame-code/Assets/Scripts/Editor/Character/EnemyDefinitionAuthoringInspector.cs)：敌人 Inspector 工作台入口。
- [CharacterIssueFieldWindow.cs](D:/Projects/ACTGame-code/Assets/Scripts/Editor/Character/CharacterIssueFieldWindow.cs)：拆出具体序列化字段编辑窗口。
