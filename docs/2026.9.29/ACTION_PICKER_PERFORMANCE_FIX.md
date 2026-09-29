# 动画选择器默认页、排序与扫描优化

日期：2026-09-29。

默认页补充：Unity 2022.3 的 SearchPickerWindow.LoadSessionSettings 会覆盖 group 为 All。使用公开 hideAllGroup 隐藏重复的 All 分组，令 UpdateViewState 保持唯一的动作库来源，不使用内部反射或额外点击模拟。

- ActionAnimationPickerPanel.cs：通过 SearchViewState.group 指定动作库页；名称使用 Unity NaturalCompare，自然序号排序，通过 SearchItem.score 保留顺序，同名按路径和资源 ID 决定顺序。
- ActionAnimationPickerPanel.cs：移除打开窗口前同步 Collect，以及每次搜索 GlobalObjectId.GetGlobalObjectIdSlow；通过 Search 异步枚举逐文件加载并让出执行权。完整结果按目录缓存，projectChanged 后失效，取消扫描不缓存半成品。名称、路径和 GUID/local ID 只在扫描时计算。
- ActionEditorViewportTests.cs：新增自然排序、可让出执行权的首次扫描、重复扫描缓存复用测试。

首次扫描仍需要 FindAssets 和加载各个 FBX；单个资源加载是 Unity 同步 API，不能保证首次打开完全无停顿。此次消除了打开前全目录同步加载及重复搜索资源标识计算；具体首开/重开毫秒耗时尚未基准测量。

无生产资源修改，无运行时、Inspector 或 Prefab 接线。人工验收：单个/批量创建动画字段小圆圈 → 默认动作库页 → 检查 Attack_1/2/10 顺序 → 关闭重开；新增或改名动画后重开，确认新候选。

自动验收入口：Test Runner → EditMode → Assembly-CSharp-Editor → ActionEditorViewportTests。无需进入 Play 验证本次编辑器变化。

验证结果：补充 Roslyn 编译五个程序集通过；定向 git diff --check 通过；Unity 定向 EditMode 118/118 通过，结构/内容审计 0 问题。实际重新打开小圆圈选择窗口，动作库页已选中，动画按名称顺序显示。首开/重开的毫秒性能基准尚未测量。
