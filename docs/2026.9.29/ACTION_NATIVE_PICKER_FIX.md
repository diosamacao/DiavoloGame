# 原生动画选择与批量动作库接入

日期：2026-09-29。

使用 Unity 自带 SearchService.ShowPicker 显示候选，项目仅提供动作库目录内的 Clip 数据，不自行绘制候选列表。目录及子目录中的 FBX 子动画保留独立身份，搜索不会扩大到角色目录之外。取消不写回，选中后仍支持原有隔离模型预览。

## 文件变更

- ActionAnimationPickerPanel.cs：移除手绘列表、列表搜索/全项目开关及文件拖放筛选分支，改为共享原生 Search Picker；保留 Clip 字段拖放和选前预览。
- CharacterActionBatchWindow.cs：读取当前角色动作库，新增原生选择并追加、追加目录全部动画；相同 Clip 去重，新打开窗口清空上一批待创建项。
- ActionEditorViewportTests.cs：新增批量来源范围、递归子目录、去重和空来源回归。

## 使用与边界

单个创建直接点击动画字段的小圆圈。批量创建点击清单 + 添加空行，再点击该行小圆圈选片；也可点击“追加目录全部动画”。Clips 清单使用 Unity ReorderableList，可删除和拖动排序。选片入口为单选。

动作库中全部真实 Clip 均可选，不隐式过滤 RM 子目录；请将动作库指向用于播放的动画目录。已配置动作库时，单个/批量动画字段的小圆圈统一打开目录限定选择器；未配置时保留 Unity 默认选择。移除旁边重复选择按钮，直接拖入、定位与清空仍由原生 ObjectField 处理。

未改变创建服务、角色资产、动画或运行时。正式资产只在用户点击创建时生成，无新增 Inspector/Prefab 接线。

## 验证

补充 Roslyn 编译五个程序集通过，定向 git diff --check 通过。已打开的 Unity 中定向 EditMode 117/117 通过，结构/内容审计 0 问题；证据为 ACTION_NATIVE_PICKER_TEST_RESULTS.xml、ACTION_NATIVE_PICKER_AUDIT.txt。实际打开批量窗口的原生 Search Picker，确认展示所配置目录内的动画并关闭，未点击创建生产资产。

人工路径：角色工作台 → 招式 → 创建或批量创建 → 动画字段小圆圈；确认候选路径、取消保持原值、选择追加及保存前清单。无需 Play 验证本次纯编辑器候选 UI；创建内容在 Gameplay 的实际动作图播放属于内容验收。

回归入口：Test Runner → EditMode → Assembly-CSharp-Editor → ActionEditorViewportTests。
`n后续修正：选择入口并入动画字段小圆圈，单个与批量共用 DrawClipField；删除重复按钮。重新运行定向 EditMode 117/117 通过；实际点击单个创建窗口小圆圈，确认打开角色目录限定 Search Picker。批量逐行选择与排序仍需人工交互核对。
