# 角色基础目录与共享来源提示 — 2026-09-29

四个已有角色 Anbi、Monster、Unagi、UnagiEnemy 统一保留 Config / Graphs / Locomotion / Actions / Reactions。AI 与 Actions/Resolvers 为按需扩展，不要求所有角色都有。共享配置仍为同一份资产，空目录不生成占位 ScriptableObject。

实际新增目录为 Unagi/Locomotion、Unagi/Reactions、UnagiEnemy/Locomotion、UnagiEnemy/Reactions（位于 Assets/Data/Characters 下），各自有 Unity .meta 和用于 Git 保存空目录的 .gitkeep。新建角色和目录补齐共用 EnsureBaseFolders，重复执行不会改动既有目录 GUID。

工作台总览、移动、连招、反应页显示对应资产来源；提供资产路径与定位按钮。共享目录与实际多角色共用分别显示，根据 FindOwners 的实际引用判断，不以目录位置冒充归属。共用时列出受影响的身体配置。未绑定时明确显示“尚未配置”。引用扫描缓存最多 2 秒，切换角色立即清除。

## 源码逐文件变化

- 修改 Assets/Scripts/Editor/Character/CharacterAssetLayout.cs：统一补齐基础目录、保留空目录标记、失败清理。
- 修改 Assets/Scripts/Editor/Character/CharacterAuthoringService.cs：空角色创建复用基础目录规则。
- 新增 Assets/Scripts/Editor/Character/CharacterConfigSourcePanel.cs（及 .meta）：只读来源、引用影响范围与定位显示。
- 修改 Assets/Scripts/Editor/Character/CharacterAuthoringWindow.cs：总览和编辑页接入来源提示。
- 修改 Assets/Scripts/Editor/Character/CharacterAuthoringValidationRunner.cs：当前 Editor 接收明确的四角色补齐请求。
- 修改 Assets/Tests/Editor/Character/CharacterAssetMigrationTests.cs：新增目录补齐幂等、不生成占位资产、实际引用识别共用配置的测试。

移除新建角色中重复的目录循环，改为调用统一规则；没有新增运行时兼容路径。

## 验证

- 执行现有 .utmp/check_authoring_compile.py：5 模块补充编译 0 错误。
- 在已打开 Unity 中执行 complete-layout 和 run；最后一次 EditMode 87/87 通过，结束时间 2026-09-29 10:54:51 UTC。包括本次新增 2 项测试。
- 当前结构与内容审计 0 问题。上轮 NewCharacter 空草稿已不在当前目录，本次没有删除或修改它。
- 对本轮开始时记录的全部 Assets/Data 配置与 .meta 做字节哈希比较，已有文件变化数：0。
- 测试结果与审计分别见 CHARACTER_BASE_FOLDERS_TEST_RESULTS.xml、CHARACTER_BASE_FOLDERS_AUDIT.txt。未重复全套 EditMode 或 Gameplay Play。

## Editor 检查

ACT/Character Workbench 选择 Unagi / UnagiEnemy，在“总览”和“移动”查看共享移动来源，在“反应”查看共享 Hit 及两个身体配置的影响提示；“定位”应选中 Shared 内真实资产。Anbi 的独占配置不应显示“多角色共用”。完整 UI 点击检查留给作者，本次自动测试覆盖数据判断和目录操作。

Test Runner → EditMode → Assembly-CSharp-Editor → CharacterAssetMigrationTests 可复跑。无需 Inspector / prefab 重新接线；未改运行时和 Gameplay 场景。
