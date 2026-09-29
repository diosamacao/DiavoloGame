# 现有角色配置目录迁移 — 2026-09-29

## 结果

依据 CharacterDefinition / EnemyDefinition 的实际序列化引用闭包迁移 62 项资产，保留全部 GUID 和 .meta。新布局包括 Characters/Anbi、Characters/Unagi、Characters/Monster、Characters/UnagiEnemy；共享配置进入 Shared。UnagiEnemy 专指敌人配置，避免与玩家 Unagi 混放。

角色目录按 Config、Graphs、Locomotion、Actions、Reactions、AI 分组，方向动作解析器位于 Actions/Resolvers。文件名采用角色身份前缀和用途；共享项使用 Default_Locomotion、Hit、Default_Brain。共享移动和受击动作没有被复制为两份。

迁移清单见 CHARACTER_LAYOUT_MIGRATION.json。操作前将源文件与 .meta 备份到 `.utmp/layout-migration-backup`，清单记录源 SHA256 并拒绝迁移期间产生的作者修改。Unity AssetDatabase.MoveAsset 保留 GUID；只改名称行并重新导入，避免保存时附带的字段重序列化。源配置参数、图 NodeId、BT Entry 字符串、烘焙数据及所有对象引用保持原值。

现有 NewCharacter 空草稿未修改。另有 7 项在角色链路和项目序列化引用中未发现使用者，保持原位，没有自动删除或猜测归属。

## 配套源码

| 操作 | 文件 | 内容 |
|---|---|---|
| 新增 | Assets/Scripts/Editor/Character/CharacterAssetMigration.cs | 预检清单、路径/指纹/未保存状态保护、GUID 保持、原始载荷保护与整批逆序回滚 |
| 新增 | Assets/Scripts/Editor/Character/CharacterAssetMigrationEntry.cs | 单资产迁移条目 |
| 新增 | Assets/Scripts/Editor/Character/CharacterAssetMigrationManifest.cs | 显式迁移清单，不按目录推断归属 |
| 修改 | Assets/Scripts/Editor/Character/CharacterAuthoringValidationRunner.cs | 当前 Editor 内执行明确迁移请求，定向回归增加迁移与生产 BT 测试 |
| 修改 | Assets/Scripts/Editor/Character/CharacterAssetLayout.cs | 标准布局中的敌人身体配置按角色目录名生成动作前缀，避免 Config 后缀混入 |
| 修改 | Assets/Scripts/Editor/Combat/ActionEditor/ActionDefinitionCreateUtility.cs | 独立动作创建改为 Actions / Reactions，去除旧目录兼容与继承上个动作名的路径；共享默认目录 |
| 修改 | Assets/Scripts/Editor/Combat/ActionEditor/ActionDefinitionCreateWindow.cs | 更新目录和用途命名提示 |
| 修改 | Assets/Scripts/Editor/Combat/ActionEditor/ActionListPanel.cs | 列表分组优先显示相对 Characters 路径 |
| 新增 | Assets/Tests/Editor/Character/CharacterAssetMigrationTests.cs | 7 项测试覆盖 GUID/引用、载荷、冲突、源修改、回滚、命名与生产动作网络 ID |
| 修改 | Assets/Tests/Editor/Enemy/EnemyBehaviorTreeAssemblyMigrationTests.cs | 使用迁移后的两份生产行为树路径 |

新增源码的 .meta 由 Unity 生成。未新增 Python 文件，未提交或切换分支。

移除路径：独立动作创建器的 ActioniDefinition 兼容选择、ActionDefinition 目录默认值，以及沿用目录内最后一个动作名的方法。未引用资产所在旧目录暂时保留。

## 验证

- 现有 `.utmp/check_authoring_compile.py`：5 模块补充 Roslyn 编译均 0 错误。
- 当前打开 Unity Editor 消费 `run` / `migrate-layout` 本地请求，不启动同项目第二实例。
- 迁移前工具回归：81 通过；2 项 BT 测试因新目标路径尚未迁移而失败。迁移后已通过。
- 最终 Unity EditMode：85 通过、0 失败，包含 7 项迁移相关测试、2 项生产 BT 反序列化测试，以及动作、内容目录、网络清单相关回归。结果见 CHARACTER_LAYOUT_TEST_RESULTS.xml。
- GUID/.meta/原始字段比对：62 项通过，其他 491 项序列化资产字节未变。见 CHARACTER_LAYOUT_INTEGRITY.json。
- 迁移前后全项目审计都是 3 个失败资产；11 条错误日志逐条相同，全部对应 NewCharacter 的空图、未设置模型、缺少 Idle/Walk/Run。没有新增结构或已配置角色内容错误。见 BASELINE_AUDIT 与 FINAL_AUDIT。
- 本次没有重跑全套 EditMode，因此上次全套中的其他 17 项失败不属于本次已修复范围。

## 运行与人工检查

修改动作资产名会改变基于名字生成的网络动作 ID，修改身体和敌人定义名也会改变相应原型 key；生产动作 ID 无冲突测试已通过。联机客户端、服务端应同步使用本次资源版本，不能将旧内容包与新内容包混用。CharacterId、Graph NodeId 与行为树 Entry 字符串不变，不需要改连招和 AI 起手关系。

Test Runner → EditMode → Assembly-CSharp-Editor → CharacterAssetMigrationTests / EnemyBehaviorTreeAssemblyMigrationTests 可以单独复跑。工作台选择 Anbi、Unagi、Monster 或 UnagiEnemy，检查角色范围动作和 AI 引用；配置引用已随 GUID 自动保持，不需要额外 prefab/Inspector 重新接线。

尚未执行 Gameplay 场景的完整 Play/双端联机验收：检查移动、攻击、闪避、受击、换人，以及 Monster/UnagiEnemy 的 BT 起手。NewCharacter 仍为待填写草稿，不应直接接入出战列表。

## 逐项资产迁移

| 原路径 | 新路径 |
|---|---|
| Assets/Data/CharacterConfig/Anbi.asset | Assets/Data/Characters/Anbi/Anbi_Character.asset |
| Assets/Data/CharacterConfig/Unagi.asset | Assets/Data/Characters/Unagi/Unagi_Character.asset |
| Assets/Data/Combat/Actions/Anbi/AnbiActionGraph.asset | Assets/Data/Characters/Anbi/Graphs/Anbi_Default_Graph.asset |
| Assets/Data/Combat/Actions/Anbi/AnbiConfig.asset | Assets/Data/Characters/Anbi/Config/Anbi_Config.asset |
| Assets/Data/Combat/Actions/Anbi/AnbiEvadeResolver.asset | Assets/Data/Characters/Anbi/Actions/Resolvers/Anbi_Dodge_Resolver.asset |
| Assets/Data/Combat/Actions/Anbi/AnbiLocomotion.asset | Assets/Data/Characters/Anbi/Locomotion/Anbi_Default_Locomotion.asset |
| Assets/Data/Combat/Actions/Monster/MonsterConfig.asset | Assets/Data/Characters/Monster/Config/Monster_Config.asset |
| Assets/Data/Combat/Actions/Unagi/DirectionalActionResolver.asset | Assets/Data/Characters/Unagi/Actions/Resolvers/Unagi_Dodge_Resolver.asset |
| Assets/Data/Combat/Actions/Unagi/UnagiActionGraph.asset | Assets/Data/Characters/Unagi/Graphs/Unagi_Default_Graph.asset |
| Assets/Data/Combat/Actions/Unagi/UnagiConfig.asset | Assets/Data/Characters/Unagi/Config/Unagi_Config.asset |
| Assets/Data/Combat/Actions/Unagi/UnagiEnemyConfig.asset | Assets/Data/Characters/UnagiEnemy/Config/UnagiEnemy_Config.asset |
| Assets/Data/Combat/Actions/Unagi/Unagi_LocomotionSO.asset | Assets/Data/Shared/Locomotion/Default_Locomotion.asset |
| Assets/Data/Combat/Actions/Anbi/ActionDefinition/Anbi_Attack_Normal_01.asset | Assets/Data/Characters/Anbi/Actions/Anbi_Attack_Normal_01.asset |
| Assets/Data/Combat/Actions/Anbi/ActionDefinition/Anbi_Attack_Normal_02.asset | Assets/Data/Characters/Anbi/Actions/Anbi_Attack_Normal_02.asset |
| Assets/Data/Combat/Actions/Anbi/ActionDefinition/Anbi_Attack_Normal_03.asset | Assets/Data/Characters/Anbi/Actions/Anbi_Attack_Normal_03.asset |
| Assets/Data/Combat/Actions/Anbi/ActionDefinition/Anbi_Attack_Normal_04.asset | Assets/Data/Characters/Anbi/Actions/Anbi_Attack_Normal_04.asset |
| Assets/Data/Combat/Actions/Anbi/ActionDefinition/Anbi_Evade_Back.asset | Assets/Data/Characters/Anbi/Actions/Anbi_Evade_Back.asset |
| Assets/Data/Combat/Actions/Anbi/ActionDefinition/Anbi_Evade_Front.asset | Assets/Data/Characters/Anbi/Actions/Anbi_Evade_Front.asset |
| Assets/Data/Combat/Actions/Anbi/ActionDefinition/Anbi_Hurt.asset | Assets/Data/Characters/Anbi/Reactions/Anbi_Hurt.asset |
| Assets/Data/Combat/Actions/Anbi/ActionDefinition/Anbi_SwitchIn.asset | Assets/Data/Characters/Anbi/Actions/Anbi_SwitchIn.asset |
| Assets/Data/Combat/Actions/Anbi/ActionDefinition/Anbi_SwitchOut.asset | Assets/Data/Characters/Anbi/Actions/Anbi_SwitchOut.asset |
| Assets/Data/Combat/Actions/Monster/ActionDefinition/Monster_Goblin_Attack_01.asset | Assets/Data/Characters/Monster/Actions/Monster_Attack_01.asset |
| Assets/Data/Combat/Actions/Monster/ActionDefinition/Monster_Goblin_Attack_02.asset | Assets/Data/Characters/Monster/Actions/Monster_Attack_02.asset |
| Assets/Data/Combat/Actions/Monster/ActionDefinition/Monster_Goblin_Hit_L_Front.asset | Assets/Data/Characters/Monster/Reactions/Monster_Hit_L_Front.asset |
| Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Attack_01.asset | Assets/Data/Characters/Unagi/Actions/Unagi_Attack_01.asset |
| Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Attack_02.asset | Assets/Data/Characters/Unagi/Actions/Unagi_Attack_02.asset |
| Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Attack_03.asset | Assets/Data/Characters/Unagi/Actions/Unagi_Attack_03.asset |
| Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Attack_04.asset | Assets/Data/Characters/Unagi/Actions/Unagi_Attack_04.asset |
| Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Attack_05.asset | Assets/Data/Characters/Unagi/Actions/Unagi_Attack_05.asset |
| Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Attack_06.asset | Assets/Data/Characters/Unagi/Actions/Unagi_Attack_06.asset |
| Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Attack_Branch_01.asset | Assets/Data/Characters/Unagi/Actions/Unagi_Attack_Branch_01.asset |
| Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Attack_Branch_01_Perfect.asset | Assets/Data/Characters/Unagi/Actions/Unagi_Attack_Branch_01_Perfect.asset |
| Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Attack_Branch_02.asset | Assets/Data/Characters/Unagi/Actions/Unagi_Attack_Branch_02.asset |
| Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Attack_Branch_02_Perfect.asset | Assets/Data/Characters/Unagi/Actions/Unagi_Attack_Branch_02_Perfect.asset |
| Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Attack_Branch_03.asset | Assets/Data/Characters/Unagi/Actions/Unagi_Attack_Branch_03.asset |
| Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Attack_Branch_Start.asset | Assets/Data/Characters/Unagi/Actions/Unagi_Attack_Branch_Start.asset |
| Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Attack_Counter.asset | Assets/Data/Characters/Unagi/Actions/Unagi_Attack_Counter.asset |
| Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Attack_Rush.asset | Assets/Data/Characters/Unagi/Actions/Unagi_Attack_Rush.asset |
| Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Attack_Rush_02.asset | Assets/Data/Characters/Unagi/Actions/Unagi_Attack_Rush_02.asset |
| Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_ChargeAttack_01.asset | Assets/Data/Characters/Unagi/Actions/Unagi_ChargeAttack_01.asset |
| Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Combo_1.asset | Assets/Data/Characters/UnagiEnemy/Actions/UnagiEnemy_Combo_1.asset |
| Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Combo_2.asset | Assets/Data/Characters/UnagiEnemy/Actions/UnagiEnemy_Combo_2.asset |
| Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Dodge_Backward.asset | Assets/Data/Characters/Unagi/Actions/Unagi_Dodge_Backward.asset |
| Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Dodge_Backward_Left.asset | Assets/Data/Characters/Unagi/Actions/Unagi_Dodge_Backward_Left.asset |
| Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Dodge_Backward_Right.asset | Assets/Data/Characters/Unagi/Actions/Unagi_Dodge_Backward_Right.asset |
| Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Dodge_Forward.asset | Assets/Data/Characters/Unagi/Actions/Unagi_Dodge_Forward.asset |
| Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Dodge_Forward_Left.asset | Assets/Data/Characters/Unagi/Actions/Unagi_Dodge_Forward_Left.asset |
| Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Dodge_Forward_Right.asset | Assets/Data/Characters/Unagi/Actions/Unagi_Dodge_Forward_Right.asset |
| Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_EX.asset | Assets/Data/Characters/Unagi/Actions/Unagi_EX.asset |
| Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Hurt.asset | Assets/Data/Shared/Reactions/Hit.asset |
| Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_ParryAid_H_Guard.asset | Assets/Data/Characters/Unagi/Actions/Unagi_ParryAid_H_Guard.asset |
| Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_ParryAid_H_Success.asset | Assets/Data/Characters/Unagi/Actions/Unagi_ParryAid_H_Success.asset |
| Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_SwitchIn.asset | Assets/Data/Characters/Unagi/Actions/Unagi_SwitchIn.asset |
| Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_SwitchOut.asset | Assets/Data/Characters/Unagi/Actions/Unagi_SwitchOut.asset |
| Assets/Data/Enemy/BehaviorTrees/BT_Monster.asset | Assets/Data/Characters/Monster/AI/Monster_BehaviorTree.asset |
| Assets/Data/Enemy/BehaviorTrees/BT_Unagi.asset | Assets/Data/Characters/UnagiEnemy/AI/UnagiEnemy_BehaviorTree.asset |
| Assets/Data/Enemy/Monster/MonsterActionGraph.asset | Assets/Data/Characters/Monster/Graphs/Monster_Default_Graph.asset |
| Assets/Data/Enemy/Monster/MonsterLocomotion.asset | Assets/Data/Characters/Monster/Locomotion/Monster_Default_Locomotion.asset |
| Assets/Data/Enemy/Monster/Monster_EDF.asset | Assets/Data/Characters/Monster/Monster_Enemy.asset |
| Assets/Data/Enemy/Unagi/Unagi_EDF.asset | Assets/Data/Characters/UnagiEnemy/UnagiEnemy_Enemy.asset |
| Assets/Data/Enemy/Unagi/Unagi_Enemy_ComboGraph.asset | Assets/Data/Characters/UnagiEnemy/Graphs/UnagiEnemy_Default_Graph.asset |
| Assets/Data/Enemy/EnemyBrainProfile.asset | Assets/Data/Shared/AI/Default_Brain.asset |

## 保留待归属配置

- Assets/Data/Combat/Actions/Anbi/ActionDefinition/Anbi_Attack_Normal_04_Perfect.asset
- Assets/Data/Combat/Actions/Giant/ActionDefinition/Giant_Attack_01.asset
- Assets/Data/Combat/Actions/Monster/ActionDefinition/Monster_Goblin_Hit_Shake.asset
- Assets/Data/Combat/Actions/Monster/ActionDefinition/Monster_Goblin_Hit_Stay.asset
- Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_ChargeAttack_03.asset
- Assets/Data/Combat/Actions/Unagi/ActioniDefinition/Unagi_Death.asset
- Assets/Data/Enemy/Unagi/Unagi_Enemy_LocomotionSO.asset
