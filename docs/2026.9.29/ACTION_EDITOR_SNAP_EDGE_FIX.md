# 时间轴右边缘吸附线修复

原因：窗口使用闭区间 startFrame/endFrame，条块宽度为 endFrame-startFrame+1 帧；右边缘在 endFrame+1，原辅助线却画在 endFrame。辅助线还使用夹紧前的候选值，无法正确反映最短长度、动作末尾的限制。

本轮修改：

- `Assets/Scripts/Editor/Combat/ActionEditor/Timeline/ActionTimelineView.cs`：右边缘及动画右侧修剪的辅助线使用 endFrame+1；按最终夹紧后的帧回写辅助线；右边缘允许鼠标到达 TotalFrames 边界；右侧拖动的 Δ 以原结束帧计算。
- `Assets/Tests/Editor/Character/ActionEditorViewportTests.cs`：扩展真实 IMGUI 输入回归，依次拖动区间窗右边缘到中间帧、最后一帧、最短一帧，对比实际绘制热区 xMax 与辅助线位置。

帧标签保持数据语义：例如结束帧 19 表示包含第 19 帧，条块右边缘与辅助线在刻度 20。没有改变动作运行时的闭区间语义，没有增加兼容路径。

验证：既有补充编译脚本五个生产程序集返回 0；已打开 Unity 内 CharacterAuthoringValidationRunner 的 run 定向 EditMode 测试 113/113 通过，0 failed/0 skipped，结果更新时间 UTC 2026-09-29 13:08:10；结构/内容审计 failures=0；git diff --check 无错误（仅工作区已有的换行提示）。本轮结果保存在 ACTION_EDITOR_SNAP_EDGE_TEST_RESULTS.xml。

Editor 复验：无需游戏 Play；在 ActionEditor 打开带 Phase/Rotation/Hitbox 等区间窗的动作，拖右边缘并观察辅助线，在动作末尾及最短一帧处确认重合；再检查动画片段右侧修剪。无需 Inspector/Prefab 接线。自动回归入口：Test Runner → EditMode → Assembly-CSharp-Editor → ActionEditorViewportTests。本轮未对生产动作做拖拽写入，人工复验可在临时动作上执行。
