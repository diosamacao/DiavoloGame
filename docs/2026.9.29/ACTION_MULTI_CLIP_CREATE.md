# 多动画创建单个动作

2026-09-29：单个动作创建入口支持有序动画序列，沿用既有 animationSegments，不更改运行时资产格式。

## 使用

1. 角色工作台“招式 → 创建并绑定动作”或动作编辑器 Create。
2. 配置动作库，点击“添加动画（多选）”的小圆圈。在 Unity Search 中 Ctrl/Shift 多选，Enter 执行“添加所选动画”。
3. 回到创建窗，拖动片段调整顺序，选中某段可预览；允许再次追加相同动画，以表达重复播放。
4. 创建后得到一个 ActionDefinition，按清单顺序写入连续完整动画段。默认使用既有动作混合设置，具体裁剪、混合、战斗窗和位移烘焙仍在动作编辑器设置。

批量动作窗口维持每个 Clip 生成一个独立动作的语义。来源未配置时仍可将单个 Clip 拖入添加字段；多选动作库入口需要有效目录。

## 文件变更

- ActionAnimationPickerPanel.cs：原生 Search 多选提交、按名称初始化选择顺序、可重排片段清单及逐段预览；原单个 Clip 绘制 API 改为数组，不保留双重创建路径。
- CharacterActionCreateWindow.cs：将有序动画数组提交给角色创建服务。
- ActionDefinitionCreateWindow.cs：独立创建入口提交同样的动画数组。
- CharacterAuthoringService.cs：创建 API 改为有序 Clip 集合，仅创建和绑定一个动作；批量调用显式传单元素集合。
- ActionDefinitionCreateUtility.cs：独立创建支持多段，输入空引用在写资源前拒绝。
- ActionAnimationSegmentCommands.cs：InitializeDraft 统一初始化片段、裁剪默认值与累计帧数，供两个创建服务调用。
- CharacterAuthoringTests.cs：迁移已有调用；新增多段顺序、重复片段、总帧数、单一图绑定及独立创建回归。

## 验收边界

测试入口：Test Runner → EditMode → Assembly-CSharp-Editor → CharacterAuthoringCreationTests；另有 ActionEditorViewportTests 覆盖来源与选片预览。

人工检查多选后 Enter 提交、片段拖动排序、创建后时间轴动画数量和边界；Gameplay 实际播放检查动画混合与位移烘焙。无需新增 Inspector/Prefab 接线，本次不自动创建或改写生产角色资源。

## 本次验证结果

- 通过已打开 Unity Editor 的 CharacterAuthoringValidationRunner 执行定向 EditMode 回归：119 passed、0 failed、0 skipped；结果见 ACTION_MULTI_CLIP_CREATE_TEST_RESULTS.xml。
- 结构与内容审计：0 failures，见 ACTION_MULTI_CLIP_CREATE_AUDIT.txt。
- 原生窗口实操：Vivian 动作库中多选两个动画，Enter 提交后两段均回填创建清单；拖动第一段到第二段后，清单顺序正确交换。未点击生产资源创建按钮。
- `git diff --check -- Assets/Scripts Assets/Tests` 通过；`.utmp/check_authoring_compile.py` 补充编译检查通过全部 5 个程序集。最后移除未使用的可选参数并更新注释后，Unity 刷新完成域重载，ready 时间为 2026-09-29T14:50:23Z，晚于源码修改时间；119 项测试结果来自此次无行为变化清理之前的完整运行。
- 剩余人工检查：在新动作的 ActionEditor 中检查片段边界与混合，完成战斗窗和位移烘焙后，在 Gameplay 场景通过对应角色动作入口实际播放。此次自动测试覆盖资源生成与绑定，不替代具体动画表现验收。
