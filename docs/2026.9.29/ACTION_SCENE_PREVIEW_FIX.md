# ActionEditor 场景模型选择回归修复

原因：ActionToolbar 用 CharacterConfig 是否存在禁用了 Transform 选择框；角色上下文恢复也无条件绑定隔离模型。因此从工作台进入无法选择 Hierarchy 中的模型。

修复：场景预览目标选择与角色动作范围解耦。保留角色动作列表，同时允许选择场景模型或清空；提供“隔离预览”按钮。切换目标先调用现有 ActionEditorPreviewSession.SetPreviewCharacter，恢复旧采样姿态和烘焙预览位移并停止播放。相同角色再次打开招式保留场景选择；角色上下文重载遵循显式场景/隔离选择。Prefab 资产与 PreviewScene 临时对象不能作为场景模型选择。

## 源码

- 修改 Assets/Scripts/Editor/Combat/ActionEditor/ActionToolbar.cs：移除角色上下文禁用选择框的分支，接入场景选择回调与隔离预览按钮。
- 修改 Assets/Scripts/Editor/Combat/ActionEditor/ActionEditorWindow.cs：管理场景目标、模式与恢复；统一切换时结束旧会话目标。
- 新增 Assets/Tests/Editor/Character/ActionEditorScenePreviewTests.cs（及 .meta）：覆盖上下文恢复保留场景选择、显式模式切换和清空场景目标。
- 修改 Assets/Scripts/Editor/Character/CharacterAuthoringValidationRunner.cs：定向回归包含新测试类。

移除旧的“有角色上下文就禁止场景预览”的约束；场景和隔离为明确预览选项，共用原有采样会话，不是两套动作实现。

## 验证

执行现有 .utmp/check_authoring_compile.py，5 模块补充编译无错误；当前已打开 Unity 执行 run，最终 EditMode 89/89 通过（新增 2 项），结构与内容审计 0 问题。结果见 ACTION_SCENE_PREVIEW_TEST_RESULTS.xml，结束时间 2026-09-29 11:12:34 UTC。

Test Runner → EditMode → Assembly-CSharp-Editor → ActionEditorScenePreviewTests 可复跑。没有执行完整 Gameplay Play 验收。

## 使用与人工验收

从角色工作台打开招式后，把 Hierarchy 中场景模型的 Transform 拖入 ActionEditor 顶部模型选择框；拖动时间轴或播放，在 Scene 查看招式。点击“隔离预览”可切回临时模型。确认切换模型或关闭编辑器后场景姿态与预览位移恢复。无需修改角色配置或 prefab 接线。完整拖拽、动画与场景恢复的交互验收尚待用户执行。
