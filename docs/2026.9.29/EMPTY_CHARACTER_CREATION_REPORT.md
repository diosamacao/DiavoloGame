# 空角色创建改造 — 2026-09-29

## 结果

创建入口改为独立空角色草稿，不读取选中角色、不复制旧角色资源；使用角色 ID 建立专属目录，用途决定文件名。仅初始化身份、身体、默认图、默认移动配置及它们之间的引用。空角色的数值来自类型默认值；图节点、动画、反应动作均为空，模型可显式选择，也可稍后填写。

```text
Assets/Data/Characters/<Id>/
  <Id>_Character.asset
  Config/<Id>_Config.asset
  Graphs/<Id>_Default_Graph.asset
  Locomotion/<Id>_Default_Locomotion.asset
  Actions/
  Reactions/
```

后续新动作例如 `<Id>_Attack_01.asset` 存在 Actions，受击反应 `<Id>_Hit_01.asset` 存在 Reactions。用途输入已有当前角色前缀时不重复添加。既有资产不自动搬迁或改名；现有平铺配置后续动作进入自身目录的 Actions / Reactions。

## 本次源码文件

- 新增 `Assets/Scripts/Editor/Character/CharacterAssetLayout.cs`（及 Unity 自动生成的 .meta）：集中角色目录、用途命名、ID 和路径校验、失败清理规则。
- 修改 `Assets/Scripts/Editor/Character/CharacterAuthoringService.cs`：空角色创建替代模板复制，接入动作目录与命名，保留批量原子回滚。
- 修改 `Assets/Scripts/Editor/Character/CharacterCreateWindow.cs`：空草稿表单、可选模型、完整目录预览。
- 修改 `Assets/Scripts/Editor/Character/CharacterAuthoringWindow.cs`：入口改为“创建空白角色”。
- 修改 `Assets/Scripts/Editor/Character/CharacterActionCreateWindow.cs`：用途输入和最终路径预览。
- 修改 `Assets/Scripts/Editor/Character/CharacterActionBatchWindow.cs`：用途前缀、完整名字和保存位置预览。
- 修改 `Assets/Tests/Editor/Character/CharacterAuthoringTests.cs`：替换复制测试，验证空白内容、引用隔离、目录命名、重复 ID、非法路径、动作绑定及批量回滚。

删除的路径：CloneCharacter、PlanClone、CollectSharedResources、模板选择 UI；没有保留复制兼容分支。未修改生产角色资产。

## 自动验证

- 执行现有 `.utmp/check_authoring_compile.py`：5 个模块补充 Roslyn 检查均 0 错误；不代替 Unity。
- 向现有 Editor 验证入口 `.utmp/authoring-validation/request.txt` 写入 `run`，由当前已打开 Unity 执行，未启动第二个批处理实例。
- Unity EditMode 定向测试：76 通过、0 失败，其中 CharacterAuthoringCreationTests 11 项通过；结束时间 2026-09-29 06:50:57 UTC。结果见 `EMPTY_CHARACTER_TEST_RESULTS.xml`。
- 全项目结构与内容审计：0 问题，见 `EMPTY_CHARACTER_AUDIT.txt`。
- `rg` 确认源码与测试中已无旧模板复制 API 或入口文本。
- 本次未重复上次全套 EditMode；上次全套的 17 项其他失败不能视为本次已解决。

## Editor 人工验收

1. 打开 `ACT/Character Workbench`，选中任意旧角色，再点“创建空白角色…”。填写新的合法 ID，确认预览路径；此操作会创建新资产。
2. 创建后检查身份只指向新 Config，Config 有一个 Default 模式；新图没有节点，移动动画为空，未指定模型时模型为空。旧角色内容不得出现。
3. 配置 Clip 后分别创建普通动作和反应动作，检查 Actions / Reactions 保存位置和 `<Id>_<用途>` 名字。批量创建应使用同一规则。
4. Test Runner → EditMode → Assembly-CSharp-Editor → CharacterAuthoringCreationTests，可单独重跑创建回归。
5. 空草稿还不是可出战角色：在工作台补齐模型、移动动画、图 Entry、动作与所需反应，执行烘焙和校验；再将 CharacterDefinition 显式加入 PartyLoadout 后，在 Gameplay 场景 Play 检查移动、攻击、受击。此次没有自动改动任何 PartyLoadout / prefab 绑定，也没有执行这条 Play 路径。

架构、技术说明、约定、路线图已同步。没有新增 Python 文件。
