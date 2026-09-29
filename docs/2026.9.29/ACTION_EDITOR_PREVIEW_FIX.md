# ActionEditor 事件与预览回归修复

## 原因与处理

1. 隔离视口用了无参 `new Handles.DrawingScope()`。它是结构体，无参初始化没有保存 Handles 状态，Dispose 将全局矩阵恢复为零矩阵，连带影响后续时间轴菱形和辅助线。改为显式颜色、单位矩阵构造，并恢复相机、深度比较和预览渲染目标。
2. 主光固定世界方向，补光沿用 Unity 默认暗色，环境光没有设置。现在主光、补光随观察相机旋转，增加环境补光，使观察面保持受光。
3. 离屏预览混用了 Scene 的 Handles.Label 和三维辅助线。隔离模式只在 RT 中画三维线，文字移至视口工具栏；辅助线与模型使用相同投影，隔离辅助线使用 Always 深度比较，网格不写深度，避免共面遮挡造成显示不稳。Scene 模式仍保留世界空间标签。
4. 实机验收额外发现：点击事件后 MouseUp 会重新磁吸并写入帧数。现仅 MouseDrag 改帧，MouseUp/Ignore 结束手势，单击只改变选择。

## 本轮源码清单

| 文件 | 本轮变化 |
|---|---|
| `Assets/Scripts/Editor/Combat/ActionEditor/Preview/ActionEditorPreviewViewport.cs` | 修复 Handles 状态作用域及恢复顺序；相机相对双灯、环境光；统一投影与辅助线深度策略 |
| `Assets/Scripts/Editor/Combat/ActionEditor/ActionEditorWindow.cs` | 隔离模式采用固定工具栏轨迹图例，不调用世界标签 |
| `Assets/Scripts/Editor/Combat/Motion/ActionMotionTrajectorySceneDrawing.cs` | 增加 drawLabel 参数，让 Scene 与离屏预览各自使用适合的文字绘制方式 |
| `Assets/Scripts/Editor/Combat/ActionEditor/Timeline/ActionTimelineView.cs` | MouseUp/Ignore 不再计算磁吸和写回窗口帧数 |
| `Assets/Tests/Editor/Character/ActionEditorViewportTests.cs` | 增加实际双 IMGUIContainer 重绘状态、点事件点击/拖动、相机相对灯光回归 |

同步 `.agents/skills/actgame-architecture/TECHNICAL.md`。未新增兼容路径；移除离屏 Scene 标签及松鼠标时重新吸附的旧路径。没有修改运行时动作语义或生产资源结构。

## 验证与限制

- 使用既有 `.utmp/check_authoring_compile.py` 做补充编译，五个生产程序集返回 0；未创建新的 Python 脚本。
- 在已经打开的 Unity 中通过 `CharacterAuthoringValidationRunner` 的 `run` 请求执行定向 EditMode 回归；结果和审计分别保存为本目录的 `ACTION_EDITOR_PREVIEW_FIX_TEST_RESULTS.xml`、`ACTION_EDITOR_PREVIEW_FIX_AUDIT.txt`。
- 最终结果：113 passed / 0 failed / 0 skipped，结构与内容审计 failures=0（结果文件更新时间 UTC 2026-09-29 12:48:14）。
- 新回归通过真实 EditorWindow/IMGUIContainer 重绘，检查矩阵、颜色、深度比较、Camera.current 和 RT 恢复；通过 `EditorWindow.SendEvent` 验证 VFX/SFX 单击不改帧、各拖动 5 帧且保持单帧。灯光检查覆盖 0/90/155/270 度。
- Computer Use 实机确认 Unagi_Attack_01 的青色 VFX、粉色 SFX 菱形可见，点击可打开事件属性，正面受光明显改善。点击验收中发现的意外吸附已即时撤销，随后修复代码并补入测试。
- 做过动作播放画面抽查；截图抽查不能替代连续视觉观察，尚需人工确认长时间循环播放下没有文字/轨迹闪烁，尤其是其他角色、自定义材质及不同窗口比例。
- `git diff --check` 无错误；已有工作区变化保留。未提交、推送或保存生产动作资产。

## Editor 复验

1. 等待脚本编译完成，在 ActionEditor 选择含 VFX/SFX 的动作。点选菱形应显示属性且触发帧不变；拖动应改变触发帧，Ctrl+Z 应还原。
2. 切隔离模式，右键旋转观察角色正面/侧面，检查受光；开启轨迹，点动作编辑器自身的“播放”，循环观察图例与地面线。无需进入游戏 Play Mode。
3. 切回场景模式，确认 Scene 轨迹与世界空间标签仍可见。
4. 自动复跑：Test Runner → EditMode → `Assembly-CSharp-Editor` → `ActionEditorViewportTests`。无需 Inspector/prefab 重新接线。

实现核对参考 Unity 2022.3 官方源码：[Handles.DrawingScope / Label](https://github.com/Unity-Technologies/UnityCsReference/blob/2022.3/Editor/Mono/Handles.cs)、[PreviewRenderUtility](https://github.com/Unity-Technologies/UnityCsReference/blob/2022.3/Editor/Mono/Inspector/PreviewRenderUtility.cs)。最终判断以本项目实际 Unity 回归和画面检查为准。
