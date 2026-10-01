# ActionGraph 隐式路由与连线布局

实现日期：2026-10-01。代码已实现，补充编译通过；Unity 编译、Test Runner 与画布交互尚待验收。

## 使用方式

**最新规则：只有当前节点的当前 Cancel 通道完全没有显式连线，才允许隐式 Cancel→Entry。** 有连线但输入不匹配或目标失效，不再回 Entry。Normal 与 Perfect 独立判断；共享路由仍优先解析。此规则替代下文早期的无条件兜底描述。

此次逐文件修改：`ActionGraph.cs` 收紧解析与候选意图收集；`ActionGraphEditorWindow.cs` 更新规则提示；`ActionGraphAutomaticTransitionTests.cs` 补充 Dodge 专用出口拒绝 Attack、另一通道独立、失效出口拒绝回 Entry、移除全部连线后恢复 Entry 的回归。删除原先“有边但未匹配也回 Entry”的路径，无资产修改。

验证：补充编译脚本 Combat、Character、Enemy、App、Editor（含测试）通过，修改源码格式检查通过；测试断言曾因 NUnit API 重载不匹配编译失败，修正后重编通过。Unity Test Runner 尚未执行。手动运行 EditMode / `Assembly-CSharp-Editor` / `ActionGraphAutomaticTransitionTests`；Play 中验证 Vivian Main 第 27 帧 Attack 不回 Attack01，Dodge 仍转 Ground；确认无显式连线的 Cancel 通道仍可回 Entry。无需新增 Inspector 或 Prefab 配置。Ground 原有提前攻击显式边仍按原配置执行。

**重叠窗口修正规则：** 对同一输入，先尝试 Perfect 的显式边/共享路由，再尝试 Normal 的显式边/共享路由，最后才使用 Entry。仅 Perfect 窗口有效时，Perfect 仍可最终回 Entry。

1. 当前动作处于 Normal 或 Perfect Cancel 窗口时，输入按「显式边 → 共享路由 → Entry」依次解析。Entry 按 Intent 匹配，无需从每个节点连回入口；None 意图入口不参与该规则。
2. 双击已有 Cancel 或 Auto 连线增加转折点，左键拖动点位调整布局，右键点击点位删除。删除全部点后恢复原生曲线。
3. 点击 Save 保存，关闭重开保留点位。Undo/Redo 恢复布局；删除连线后对应布局随之清理。点位是图内容坐标，支持画布缩放和平移。

隐式规则对全部 Graph 生效，不生成或修改实体边。画布顶部显示隐式规则摘要，避免大量重复连线。布局元数据不参与战斗路由，不要求 Inspector / Prefab 布线。

## 逐文件记录

| 源文件 | 修改 |
|---|---|
| `Assets/Scripts/Domain/Combat/Actions/Resolution/ActionGraph.cs` | Cancel 解析增加 Entry 最后一级去向；候选意图加入 Entry；增加独立的编辑器连线布局数组。 |
| `Assets/Scripts/Editor/Combat/ActionGraph/ActionGraphEditorWindow.cs` | 统一使用可布线 Edge；增加点位插入、拖动、删除、拾取、框选和保存重开；顶部说明隐式规则。 |
| `Assets/Tests/Editor/Combat/ActionGraphAutomaticTransitionTests.cs` | 增加 Normal/Perfect 路由优先级与门槛、双边布局保存、删除布局、Undo、线段拾取/框选回归。 |

技术文档同步于 `.agents/skills/actgame-architecture/TECHNICAL.md`。未新增兼容路径；无转折点使用原生曲线属于正常绘制模式。未修改生产角色资产。

## 已执行验证

- 复用 `.utmp/check_authoring_compile.py`，先以当前源码刷新 `ACTGame.Simulation`，再编译 Combat、Character、Enemy、App、Editor（含 Editor 测试）：全部退出码 0。
- 该检查使用 Roslyn 和本地 Unity 引用，只证明补充编译通过，不代表 Unity 编译或测试执行通过。
- 本次修改的源码 `git diff --check` 无格式错误；全仓检查发现已有 Vivian Graph 资产尾随空格，未修改该资产。
- 检测到本项目已在 Unity 中打开；没有启动第二个 batch-mode Unity。当前工具列表无 Unity MCP，未执行 Test Runner 或鼠标交互验证。

## Unity 验收步骤

1. 等待 Unity 编译结束，确认 Console 无新错误；打开 **Window → General → Test Runner → EditMode**，搜索 `ActionGraphAutomaticTransitionTests`（`Assembly-CSharp-Editor`）并运行全部测试。
2. 打开一个有 Cancel / Auto 连线的 ActionGraph。双击线上不同位置添加多个点；拖动、右键删除；检查连线跟随、选中高亮、缩放和平移、移动节点后的端口连接。
3. 两条 Auto 规则连接相同目标时分别布线，Save 后关闭重开，检查两条路径独立保留；执行 Undo/Redo；删除连线并保存，确认不会复活。
4. Play：使用当前玩家 Graph，选一个已有 Cancel 窗的动作；在窗口内输入一个存在 Entry、但没有该来源显式边或共享路由的 Intent，应进入对应 Entry。窗口外不应由该隐式规则切招（Recovery 与高优打断仍遵循各自规则）。
5. 用同一 Intent 的显式边和共享路由检查优先级：显式目标优先；没有显式去向时共享目标优先；两者都不能解析时才使用 Entry。Normal / Perfect 分别检查。

自动规则端口布局键包含规则序号；重排规则、合并或拆分节点组改变可视拓扑后，需要重新检查布线。

## 后续优化：网格与吸附

### Unagi 普攻 1→2→3 循环修复

Unagi Attack03 的 Normal 与 Perfect 都在 28～65 帧生效。旧实现先尝试 Perfect，但 Perfect 普攻无显式去向时立即回 Attack01 Entry，抢占了 Normal 的 3→5。此次不修改角色配置。

| 源文件 | 本次修改 |
|---|---|
| `Assets/Scripts/Domain/Combat/Actions/Resolution/ActionResolveContext.cs` | 增加 `AllowCancelEntryFallback`，由调用轮次控制是否可以进入最终 Entry 兜底。 |
| `Assets/Scripts/Domain/Combat/Actions/Resolution/ActionSimResolverBridge.cs` | 按当前快照帧检测窗口重叠，Perfect 轮延后 Entry 到 Normal 轮；单独 Perfect 窗仍可兜底。 |
| `Assets/Scripts/Domain/Combat/Actions/Resolution/ActionGraph.cs` | Entry 解析遵守上下文门槛，删除无条件回 Entry 的路径。 |
| `Assets/Tests/Editor/Combat/ActionGraphAutomaticTransitionTests.cs` | 实际解析桥回归覆盖重叠窗口、普通显式/共享路由优先、最后回 Entry、单独 Perfect 窗和 Perfect 显式边。 |

验证：`.utmp/check_authoring_compile.py` 的 Combat、Character、Enemy、App、Editor（含测试）全部退出码 0；本次源码 `git diff --check` 通过。没有执行 Unity Test Runner，不能视为测试通过。当前无 Unity MCP，本项目已打开，不启动 batch mode。

Editor：等待编译，运行 EditMode / `Assembly-CSharp-Editor` / `ActionGraphAutomaticTransitionTests`。Play 切到 Unagi，在 Cancel 窗口持续输入普通攻击，预期 1→2→3→5→6→2；同时检查 Perfect 特殊分支仍优先、无匹配配置路由时可回 Entry。无需新增 Inspector / Prefab 布线或资产修改。

### 连线初始化异常修复

视觉加粗曾在 `Port.ConnectTo<T>` 设置端口、尚未将 Edge 加入 GraphView 时直接设置 `EdgeControl.edgeWidth`。该 setter 立即调用布局计算，内部访问祖先 GraphView 的 minScale，导致空引用并中断图加载。依据 [Unity 2022.3 EdgeControl 源码](https://raw.githubusercontent.com/Unity-Technologies/UnityCsReference/2022.3/Modules/GraphViewEditor/EdgeControl.cs) 核对。

- `ActionGraphEditorWindow.cs`：`RoutedActionGraphEdge.UpdateEdgeControl` 首先检查实际祖先 GraphView；未挂载或已移除时直接返回 false，基类更新成功后才设置线宽和颜色。移除无条件设置线宽的路径，保留加粗和提亮效果。
- `ActionGraphAutomaticTransitionTests.cs`：新增 `RoutedEdges_PortInitializationAndDetachedUpdatesDoNotComputeLayout`，覆盖初始端口赋值、挂入图后线宽、移出图后更新及清空端口。
- 实际执行补充编译脚本，五个程序集（含 Editor 测试源码）退出码均为 0；两份源码 diff 格式检查通过。Unity Test Runner 尚未运行。
- Editor 验收：等待编译结束，重新打开 Graph 或 Reload，确认连线恢复且 Console 不再新增该异常；检查新建连线、删除、重开和选中加粗。EditMode 运行 `Assembly-CSharp-Editor` / `ActionGraphAutomaticTransitionTests`。无需 Play、Inspector 或 Prefab 配置。

- `ActionGraphEditorWindow.cs`：以铺满视口的 `ActionGraphGrid` 替换默认 GridBackground；细格 20、主格 100，跟随平移/缩放，缩小时隐藏过密细格。新增和拖动转折点逐轴吸附到两端端口、同线其它转折点（8 屏幕像素内最近者优先），否则吸附到 20 单位网格；Alt 绕过吸附。
- `ActionGraphAutomaticTransitionTests.cs`：增加最近点逐轴匹配、负坐标网格、Alt 绕过与不同缩放下的屏幕容差测试。
- 技术文档同步 `TECHNICAL.md`。移除了默认 GridBackground 创建路径，没有增加运行时配置或兼容分支。
- 实际执行 `.utmp/check_authoring_compile.py`：Combat、Character、Enemy、App、Editor（含测试）全部退出码 0；本次两份源码 `git diff --check` 通过。未执行 Unity Test Runner。
- Editor 补验：运行 EditMode / `Assembly-CSharp-Editor` / `ActionGraphAutomaticTransitionTests`；打开 Graph 检查网格铺满且随平移缩放保持位置一致，双击加点和拖动接近端口/同线点时应对齐，Alt 可自由移动。Save/重开、Undo/Redo 后位置不变。此次仅编辑器布局变化，无新增 Play 路径或 Inspector / Prefab 布线要求。
