# AI Entry 配置校验修正

2026-09-29：Entry 代表允许起手，Intent 代表输入选招语义。行为树按 NodeId 指定入口，多个 Intent=None Entry 合法且无需警告；不增加玩家/敌人开关，不修改资产或运行时选择路径。

## 本轮源文件

- `Assets/Scripts/Domain/Combat/Actions/Validation/ActionGraphValidator.cs`：删除 Entry.MissingIntent 警告，仅对非空 Intent 检查输入冲突，保留 Special 多候选例外。
- `Assets/Scripts/Domain/Combat/Actions/Resolution/ActionGraph.cs`：更新 Entry/Intent 字段说明；运行时解析逻辑保持原有行为。
- `Assets/Scripts/Editor/Combat/ActionGraph/ActionGraphInspector.cs`：明确按 NodeId 起手可留 None；修正强制 Normal Cancel 的过期提示。
- `Assets/Scripts/Editor/Combat/ActionGraph/ActionGraphEditorWindow.cs`：画布节点和策略面板同步 Entry/Intent 说明。
- `Assets/Tests/Editor/Character/CharacterAuthoringTests.cs`：覆盖多个 None/Attack/Special Entry、按 Id 成功解析、输入不误选、缺少 Entry 仍拒绝。

同步 TECHNICAL、ROADMAP、CONVENTIONS。没有新建 Python 脚本。

## 验证

- 复用 `.utmp/check_authoring_compile.py`：5 个补充编译目标退出码均为 0。
- 在现有 Unity Editor 通过 Ctrl+R 导入修改，再由 CharacterAuthoringValidationRunner 执行：66 passed / 0 failed / 0 skipped；其中 ActionGraphValidatorTests 为 9/9。已检查 results.xml 中的新测试名称与结果。
- Unity Inspector 实际验证 MonsterActionGraph：两个 Entry 的 Intent 都是 None，点击 Validate 后增加一条普通日志，没有新增警告或错误；新字段说明已显示。
- `rg Entry.MissingIntent Assets/Scripts Assets/Tests` 无命中；相关文件 `git diff --check` 通过。
- 全项目结构/内容审计仍为 85 项既有问题，本轮未关闭这些问题，也未将其说成通过。

本轮未运行 Gameplay Play。需要实际行为复验时：Gameplay 场景 → Play → 观察 BT_Monster 分别请求两个 Entry 的 NodeId 并执行对应动作。无需新增 Inspector 或 Prefab 绑定。输入 Cancel 连线目标仍要求非空 Intent；纯 AI 自动衔接使用明确目标 NodeId。
