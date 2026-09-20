# ACTGame UI 框架搭建方案 — Framework/UIFramework + UGUI + MVVM-lite

> 制定：2026-09-19  
> 修订：2026-09-20
> 角色：**通用 UIFramework 与 ACTGame 业务 UI 的结构、边界及手写实施真源（先文档，后实现）**
> 实施约束：本方案只定义边界、顺序与验收；业务代码由项目作者手写  
> 相关：  
> - [架构文档](../../.agents/skills/actgame-architecture/ARCHITECTURE.md)  
> - [架构约定](../../.agents/skills/actgame-architecture/CONVENTIONS.md)  
> - [相机与 UI 展示舱方案](../2026.8.26/CAMERA_SYSTEM_PLAN.md)  
> - [项目总清单](../PROJECT_CHECKLIST.md)  
> - 数据链：`Simulation / Owner Snapshot → App 只读投影 → ViewModel → UGUI View`

---

## 0. 一句话

在 `Assets/Scripts/Framework/UIFramework` 建立以 **`UIManager` + `UIPanel`** 为核心的 UGUI 通用界面管理框架，由 `ACTGame.UI` 通过具体 Panel、Presenter、ViewModel 和 App 只读投影接入项目页面；先完成“生命值 HUD”最小闭环，再扩展页面栈、弹窗和 3D 展示舱，禁止 Framework 引用 App/Domain、禁止 View 直读写玩法权威、禁止静态 `UIManager.Instance` 与 Runtime UI Toolkit 双轨。

---

## 1. 问题与动机

### 1.1 现状基线

```text
SimulationHost.StepOnce
  → SimulationLogicStepEvent
  → LocalPlayerService.Local.Actor
       ├─ CharacterVitality（Health / MaxHealth）
       └─ PlayerPartyRuntime（ActiveSlot / AssistPoints）

Owner Snapshot
  → ActOwnerReplicationAdapter.ApplySnapshot
  → CharacterVitality.ApplyAuthorityHealthMilli
```

| 点 | 现状与结论 |
|----|------------|
| 正式 UI | 尚未实现，只有开发期 Debug HUD；可以从单一入口开始，不需要兼容旧 UI 框架 |
| Framework 基线 | `Framework/ACTNet` 已证明“独立目录 + 自有 asmdef + 上层单向引用”的项目约定；UI 只需一个轻量运行时框架，因此采用直白的 `Framework/UIFramework`，不复制 ACTNet 的产品前缀与多程序集规模 |
| 渲染技术 | 工程已安装 `com.unity.ugui 1.0.0`；首版采用 UGUI，适合 HUD、菜单、World Space 血条和后续 RenderTexture 展示舱 |
| 架构入口 | `ACTGameArchitecture` 已提供强类型 System / Query / Command / Event；UI 应复用，不新增 static event bus 或 Service Locator |
| 本机数据 | `LocalPlayerService` 已统一提供 Local Player；`PlayerController.Actor` 是 HUD 只读入口，Owner 快照也会回写该 Actor 的 Vitality |
| 固定帧通知 | `SimulationLogicStepEvent` 在整帧 Combat/PostCombat 完成后发布；适合 App 投影采样，不需要 View 每渲染帧轮询 Domain |
| 阵容切换 | `PlayerPartyRuntime.ActiveActorChanged` 已覆盖预测切人、权威纠正、死亡换人与队灭；App 投影需把它收口成 UI 快照，而不是让每个 View 自己换绑 Actor |
| Dedicated | `ACTGame.Server` 不得依赖 HUD/Input/Camera；运行时 UI 必须是仅客户端引用的叶子程序集 |
| 3D 展示 | C5 已约定独立 Booth + Camera + RenderTexture；它是后续页面内容，不应进入基础导航或战斗 Camera Director |

### 1.2 核心痛点

1. 如果 HUD 直接在 `Update` 中抓 `PlayerController.CurrentHealth`，生命周期、换人、断线和测试都会散落在 View 中。  
2. 如果 ViewModel 直接持有 `CharacterActor`，所谓 MVVM 只剩目录命名，表现层仍与 Domain 强耦合。  
3. 如果先写“万能 UIManager”，容易在没有真实页面需求前堆反射、字符串路由、对象池和异步加载。  
4. 离线、Listen 与远端 Client 必须看到同一套 HUD 数据语义，不能分别维护本地血量与网络血量两条 UI 路径。  
5. 联机菜单不能擅自 `Time.timeScale = 0`；UI 输入焦点和玩法暂停是两个不同问题。

### 1.3 目标与不做

| 目标 | 完成定义 |
|------|----------|
| 清晰分层 | View 只操作 UGUI；ViewModel 只保存展示状态；Presenter 只做生命周期和映射；App Projection 只读玩法状态 |
| 可复用框架 | `UIFramework` 只依赖 Unity/UGUI，不引用 ACTGame App、Domain、Simulation 或 Server，可复制到另一 Unity 游戏项目独立编译 |
| 单一数据流 | 离线/Listen/Client 都从当前 Local Actor 生成同一 `LocalHudSnapshot`；网络纠正通过现有 Actor 回写自然进入 HUD |
| 可学习 | 每阶段只引入一个新概念，并有纯 C# 测试或 Play 现象作为出口 |
| 可扩展 | 支持 HUD、全屏页、Modal、Toast 四种层级；后续可接角色详情的 3D 展示舱 |
| 可回收 | 页面关闭后无残留订阅；重复开关页面不会重复回调或累积实例 |
| 不做 | 首版不做 Addressables、国际化、复杂动效系统、通用数据绑定反射、UI 对象池、热更 UI、编辑器可视化路由 |

---

## 2. 设计原则

1. **权威状态只在 Gameplay**：UI 展示结果，不计算伤害、不修改 Numeric、不决定换人是否合法。  
2. **单向数据流**：Gameplay → Projection → ViewModel → View；用户操作反向只产生 UI 意图或 Architecture Command。  
3. **View 被动化**：View 不查场景、不拿单例、不发玩法请求；只暴露控件事件并渲染传入状态。  
4. **显式绑定优先**：首版使用 C# event 和明确的 `Bind/Unbind`，不引入反射式自动绑定。  
5. **值快照越层**：跨 App/UI 边界传不可变 `LocalHudSnapshot`，不传 `CharacterActor`、`NumericSystem` 或可写集合。  
6. **变化才通知**：Projection 可在逻辑帧采样，但只有快照发生变化才发布 `LocalHudStateChangedEvent`。  
7. **导航与业务正交**：路由只决定显示哪一页和层级，不根据角色、关卡或联网身份写 `if`。  
8. **Framework 零业务依赖**：`UIFramework` 只引用 Unity/UGUI；禁止引用 `ACTGame.*`，也不定义 HUD、角色、背包等项目概念。
9. **业务 UI 是客户端叶子**：`ACTGame.UI` 引用 `UIFramework` 与 `ACTGame.App`；App、Domain、Simulation、Server 均不得反向引用业务 UI。
10. **一种运行时技术**：本阶段统一 UGUI；UI Toolkit 继续只用于现有 EditorWindow，不抽象一套兼容两者的控件层。
11. **零长期兼容**：正式 HUD 达到 Debug HUD 的必要观察能力后删除重复的旧显示路径，不长期双显。

---

## 3. 目标架构

```mermaid
flowchart LR
    Sim["SimulationWorld / Owner Snapshot"] --> Actor["Local CharacterActor"]
    Actor --> Projection["LocalHudProjectionSystem · ACTGame.App"]
    Step["SimulationLogicStepEvent"] --> Projection
    Projection --> Snapshot["LocalHudSnapshot · immutable"]
    Snapshot --> AppEvent["LocalHudStateChangedEvent"]
    AppEvent --> Presenter["HudPresenter · ACTGame.UI"]
    Presenter --> VM["HudViewModel · pure C#"]
    Framework["UIManager + UIPanel · UIFramework"] --> View["HudPanel · ACTGame.UI"]
    VM --> View
    View --> Intent["UI Intent"]
    Intent --> Presenter
    Presenter --> Command["Architecture Command / UIManager"]
    Command --> App["App System"]
```

### 3.1 五层职责

| 层 | 推荐类型 | 职责 | 不负责 |
|----|----------|------|--------|
| UIFramework | `UIManager`, `UIPanel`, `UILayer`, `UIPanelStack` | Panel 注册、打开/关闭、显示层、返回栈和生命周期 | ACTGame 页面类型、HUD 字段、Architecture、Gameplay |
| App Projection | `LocalHudProjectionSystem`, `LocalHudSnapshot`, `LocalHudStateChangedEvent`, `GetLocalHudStateQuery` | 读取 Local Actor/Party，去重并发布稳定 UI 数据 | UGUI、颜色、动画、按钮 |
| ViewModel | `HudViewModel`, `MenuViewModel` | 把快照转换为比例、文本、可见性等展示状态；发 `Changed` | 查 Architecture、持有 Unity 对象、写 Gameplay |
| Presenter | `HudPresenter`, `PauseMenuPresenter` | 建立/解除订阅，把快照灌入 VM，把 View 意图转为命令/导航 | 保存权威业务状态、直接操作具体 Graphic |
| View | `HudView`, `PauseMenuView` | Inspector 引用、按钮事件、渲染文本/Slider/Image/CanvasGroup | 查 Actor、发网络包、计算业务规则 |

`UIFramework` 只提供 Panel 管理机制，不提供 ACTGame 的 ViewModel。`ViewModel` 首版不需要第三方响应式库：每个业务 ViewModel 一个明确的不可变 `State` 或少量属性，加一个 `Changed` 事件即可。只有第二个页面证明重复模式后，再考虑把真正业务无关的订阅容器移入 UIFramework；不要在 U0 先造反射式响应框架。

### 3.2 UI Root 与显示层级

```text
UIManager (DontDestroyOnLoad 由 Composition Root 决定，首版可先场景内)
├── HudLayer       // 常驻，无射线遮挡空白区域
├── ScreenLayer    // 全屏页，单栈顶
├── ModalLayer     // 对话框，可压在 Screen/HUD 上
├── ToastLayer     // 提示，不参与返回栈
└── Blocker        // 仅 Modal/加载时启用
```

| 层 | 导航规则 | 输入规则 |
|----|----------|----------|
| HUD | 场景有效期间常驻，不进返回栈 | 默认不拦截玩法输入 |
| Screen | `Push(route)` / `Pop()`，同一时刻只有栈顶交互 | 打开后请求 App 切换本机输入上下文 |
| Modal | `ShowModal(route)` / `CloseModal()` | 必须有 Blocker；Esc/Cancel 先关最上层 Modal |
| Toast | `Show(message, duration)`，不入栈 | 不抢焦点，不暂停玩法 |

首版使用 Panel 类型作为强类型身份：`UIManager.Open<TPanel>()`、`Close<TPanel>()`、`Back()`；具体 `HudPanel`、`PauseMenuPanel`、`CharacterDetailPanel` 定义在 `ACTGame.UI`，Framework 不维护业务路由枚举。`UIManager` 通过 Inspector 面板模板注册表建立 `Type → UIPanel` 映射，禁止字符串类名和反射扫描。等确实需要跨场景/异步加载时，再替换 Panel 创建来源，公开生命周期不改。

### 3.3 HUD 快照契约

首个垂直切片只放真实需要的字段：

```text
LocalHudSnapshot
  HasLocalPlayer
  CurrentHealthMilli
  MaxHealthMilli
  ActiveSlot
  AssistPoints
  MaxAssistPoints
  IsPartyWiped
```

- 数值保持整数 milli，比例只在 ViewModel 计算，避免 UI 引入另一套浮点状态。  
- `HasLocalPlayer=false` 时 ViewModel 隐藏 HUD，不用 0 血猜“未初始化”。  
- 后续能量、喧响、技能冷却各自证明需求后再加字段；不提前塞整个 `NumericDebugSnapshot`。  
- 世界敌人血条应是独立 `WorldHealthBarPresenter` 切片，绑定只读目标句柄；不要复用 Local HUD ViewModel。

### 3.4 输入与暂停契约

```text
View.ButtonClicked
  → Presenter
  → Open/Close Route 或 SendCommand
  → App 决定 Cursor / Gameplay Input 是否启用
```

- UI 只能请求 `UiInputMode.Gameplay | Menu`，不能直接启停 `InputActionAsset`。  
- 联机房间打开菜单只屏蔽本机玩法采样并显示光标，**不得**暂停 `SimulationWorld` 或设置 `Time.timeScale=0`。  
- 将来离线暂停也由 App 的 `PausePolicy` 决定；View 不判断当前是 Client / Listen / Offline。

### 3.5 程序集边界

```mermaid
flowchart BT
    Domain["ACTGame.Domain.*"] --> App["ACTGame.App"]
    Simulation["ACTGame.Simulation"] --> App
    UnityUGUI["UnityEngine.UI"] --> Framework["UIFramework"]
    App --> UI["ACTGame.UI"]
    Framework --> UI
    Framework -. forbidden .-> App
    Framework -. forbidden .-> Domain
    UI -. forbidden .-> Server["ACTGame.Server"]
```

图中实线方向表示“被上层引用”：`UIFramework → ACTGame.UI`，且 `ACTGame.UI` 同时引用 `ACTGame.App`。UIFramework 不认识任何 ACTGame 业务；`ACTGame.Server`、Domain 和 Simulation 不添加 UI 引用，Dedicated 场景不装配 `UIManager`。

---

## 4. 范围声明

| 阶段 | 包含 | 不包含 |
|------|------|--------|
| U0 | `Framework/UIFramework` 单一 asmdef、`UIManager/UIPanel` 生命周期与业务 UI 叶子程序集 | Canvas 美术、真实 Gameplay 数据、异步加载与对象池 |
| U1 | UI Root、四层 Canvas、路由栈、空白页 | 异步加载、对象池、转场动画 |
| U2 | Local HP + ActiveSlot + AssistPoints HUD 垂直切片 | 敌人世界血条、Boss UI |
| U3 | 菜单输入模式、返回键、Modal/Toast 生命周期 | 联机暂停、设置持久化 |
| U4 | 世界血条或第二个真实页面，用于验证复用 | 大批量血条优化、完整角色养成 |
| U5 | C5 Booth + RenderTexture 接入角色详情页 | 修改战斗 VCam、让展示角色进入 Simulation |

---

## 5. 分阶段交付（任务 / 验收 / 出口）

> 全部代码由项目作者手写。每阶段先写测试/最小契约，再写 Unity 组件；未开始保持 `[ ]`。

### U0 — UIFramework 骨架与程序集边界

**任务**

- [ ] 新建 `Framework/UIFramework/UIFramework.asmdef`，程序集引用只添加 `UnityEngine.UI`（包名为 `com.unity.ugui`），不引用任何 `ACTGame.*` 程序集。
- [ ] 定义 `UIManager`、`UIPanel`、`UILayer`、`UIPanelStack`；只处理 Panel 注册、Open/Close/Back、层级和生命周期。
- [ ] `UIManager` 采用场景/Composition Root 持有的实例，不提供 `static Instance`；公开 `Open<TPanel>()`、`Close<TPanel>()` 与 `Back()`。
- [ ] 新建 `ACTGame.UI` 客户端叶子程序集；定义项目自己的具体 Panels、ViewModels、Presenters 与 Views 目录。
- [ ] 定义 `LocalHudSnapshot`、`HudViewModel.State` 与显式 `Changed` 生命周期。
- [ ] 为 UIFramework/业务 UI asmdef 补结构门禁：UIFramework 禁止 `ACTGame.*`；App/Domain/Simulation/Server 禁止反向引用 `ACTGame.UI`。

**验收**

- [ ] `UIPanelStackTests`：Push、Pop、重复打开策略、Modal 优先返回均可判定。
- [ ] `HudViewModelTests`：0/Max、无玩家、队灭、非法 Max 的展示状态明确。
- [ ] 结构测试证明 `UIFramework` 不引用任何 `ACTGame.*`，且 `ACTGame.Domain.*`、`ACTGame.Simulation`、`ACTGame.Server` 不引用 `ACTGame.UI`。
- [ ] Unity 编译与最窄 EditMode 测试通过。

**出口：** UIFramework 可脱离 ACTGame 业务独立编译，且 Panel 栈与 HUD 展示规则可分别测试。→ **未达成**

### U1 — UI Root 与空白导航闭环

**任务**

- [ ] 在 `UIFramework` 手写 `UIManager` 与 `UIPanel`，实现 HUD/Screen/Modal/Toast 四层及 `Open/Close/Back`。
- [ ] 页面采用 Inspector 明确注册；同一路由实例策略固定为“单实例、关闭隐藏”，首版不做池化。
- [ ] 实现 `Open → Enter → Exit → Close` 生命周期；Presenter 在 Enter/Exit 对称 Bind/Unbind。
- [ ] 在 Editor 人工创建 `UIRoot` Prefab/Scene 装配、EventSystem、CanvasScaler 与四层节点。

**验收**

- [ ] Play：按钮打开空白测试页，返回键关闭；Modal 打开时先关 Modal，再退 Screen。
- [ ] 连续开关 20 次，层级中实例数不增长，按钮和事件每次只响应一次。
- [ ] UIFramework 独立测试覆盖未注册 Panel、空栈 Back、重复 Open 策略，不引用 ACTGame 测试程序集。
- [ ] Dedicated 场景/程序集不出现 `UIManager` 依赖。

**出口：** 一个空白页面可稳定打开、返回和释放订阅，尚不接玩法数据。→ **未达成**

### U2 — Local HUD 最小垂直切片

**任务**

- [ ] 在 `ACTGame.App` 手写 `LocalHudProjectionSystem`：订阅 `SimulationLogicStepEvent`，从 `LocalPlayerService.Local.Actor/Party` 生成快照。
- [ ] 只有快照变化时发送 `LocalHudStateChangedEvent`，并提供 `GetLocalHudStateQuery` 供 Presenter 首次同步。
- [ ] 手写 `HudPresenter → HudViewModel → HudView`；完成 HP 数值/比例、ActiveSlot、AssistPoints 与无玩家隐藏。
- [ ] 换人和 Owner 权威生命覆盖继续走现有 Actor 单轨；禁止为 Client 单独读取 `LocalClientRuntime.SelfHealthMilli` 建第二条 HUD 数据源。

**验收**

- [ ] `LocalHudProjectionSystemTests`：相同快照不重复发事件；伤害、换人、队灭各只产生正确变化。
- [ ] Play：本机受伤后血条变化；换人后 HUD 同帧换绑；死亡自动换人后显示新 Active；队灭隐藏或进入明确终态。
- [ ] Listen/Client：Owner 快照覆盖 Actor 后，HUD 最终与权威 HP 一致，无第二套网络 HUD 分支。
- [ ] Profiler：静止 HUD 每帧无字符串重建与可见 GC Alloc；逻辑帧采样不导致 Canvas 每帧 rebuild。

**出口：** 第一块正式 HUD 通过统一 App 投影同时服务本地与联网路径。→ **未达成**

### U3 — 输入模式、菜单、Modal 与 Toast

**任务**

- [ ] 定义 `UiInputMode` 与 App 命令/系统，由 App 统一切本机 Gameplay/UI 输入和 Cursor。
- [ ] 手写简单 Pause/Menu 页面；打开 Screen 时申请 Menu 模式，最后一个阻塞页关闭时恢复 Gameplay。
- [ ] 完成 Modal Blocker、确认/取消结果回传和 Toast 队列；结果对象不携带 Domain 可写引用。
- [ ] 明确异常/销毁收尾：页面被禁用、场景卸载时恢复输入模式并解除全部订阅。

**验收**

- [ ] Play：菜单打开时角色不再接收本机移动/攻击，关闭后恢复；UI 按钮和返回键正常。
- [ ] Listen/Client：菜单打开期间服务器与其他角色继续运行，`Time.timeScale` 保持 1。
- [ ] Modal 连续打开/关闭无穿透点击；Toast 不抢焦点、不进入返回栈。
- [ ] 输入模式测试覆盖嵌套 Modal、场景卸载和重复 Close。

**出口：** 导航、焦点和玩法输入有明确所有者，联机菜单不冒充暂停。→ **未达成**

### U4 — 第二切片验证框架复用

**任务**

- [ ] 在“世界敌人血条”与“设置/角色信息页”中选择一个真实需求作为第二切片；推荐先做世界血条。
- [ ] 世界血条使用目标只读快照 + Presenter，View 只做屏幕坐标与可见性表现；不从 Slider 反写 Health。
- [ ] 仅当两个 Presenter 已出现相同订阅样板时提取小型 `SubscriptionBag`；不得先造通用 Binder DSL。
- [ ] 正式 HUD 达到必要观察能力后，删除被替代的 Debug HUD 重复字段/入口。

**验收**

- [ ] 第二切片没有复制 `UiRoot`、路由栈或输入切换逻辑。
- [ ] 目标 Spawn/Despawn、死亡与场景退出后不保留悬空 Presenter。
- [ ] `rg` 确认 View/ViewModel 不引用 `CharacterActor`、`NumericSystem`、`ACTGameArchitecture.Interface`。
- [ ] 被替代 Debug HUD 路径已删除，不保留长期双显。

**出口：** 第二个真实功能证明框架能复用，同时没有演变为万能管理器。→ **未达成**

### U5 — 角色详情 3D 展示舱

**任务**

- [ ] 依照 Camera C5 实现 `UiShowcaseProfile`、`UiShowcaseService`、`UiShowcaseBooth` 与独立 Camera/RenderTexture。
- [ ] 角色详情 Presenter 只向 Showcase Service 提交 Profile/CharacterId；View 只显示 RawImage 与 2D 信息。
- [ ] 展示角色为视觉副本，不注册 `SimulationWorld`、`TargetSystem` 或 `CombatActorSystem`。
- [ ] 页面关闭时释放/回池 Booth 与 RenderTexture 引用，恢复 UI 输入状态。

**验收**

- [ ] Play：详情页同时显示 3D 角色与 2D UI；关闭后战斗相机 Follow、InputFrame、Motor 均不变化。
- [ ] 两份 Profile 可切换不同背景/机位，导航代码无按页面名分支。
- [ ] `rg "UiShowcase"` 不出现在 `CharacterMotor`、`InputFrame` 或 Server 程序集。
- [ ] RenderTexture 生命周期无残留，重复进出详情页实例数稳定。

**出口：** 3D 展示作为普通页面内容接入，和战斗相机/模拟完全隔离。→ **未达成**

---

## 6. 迁移与兼容

### 6.1 保留 / 迁入

- 保留 `ACTGameArchitecture` 的 Query / Command / Event，UI 不另建全局总线。  
- 保留 `LocalPlayerService` 作为本机玩家唯一发现入口。  
- 保留 `SimulationLogicStepEvent` 作为投影采样时机；UI 自己不复制固定帧时钟。  
- 保留 `CharacterVitality` / `PlayerPartyRuntime` 作为真实状态，投影只读并复制成值快照。  
- 保留 Debug HUD 直到 U2 的正式 HUD 能覆盖必要观察项。
- `UIFramework` 只迁入 Panel 注册、显示层、返回栈、生命周期和 UGUI 宿主；具体 Panel、ViewModel、Presenter 与 HUD 数据永远留在 `ACTGame.UI` / App。

### 6.2 明确删除 / 禁止保留

| 删除或禁止 | 原因 |
|------------|------|
| 正式 HUD 与 Debug HUD 长期双显 | 两套展示真源会漂移；U4 达标后删旧入口 |
| View 中的 `FindObjectOfType` / Tag 查找 | 破坏 Composition Root 与可测试生命周期 |
| View/ViewModel 持有 `CharacterActor` / `NumericSystem` | 穿透 App 边界，使联网和换人逻辑散落 |
| `UIManager.Instance` + static event | Manager 类型本身合理，但全局静态入口与现有 Composition Root 重复，且测试/订阅难回收 |
| 字符串 Panel 名 + 反射创建页面 | 重构不安全，错误只能在运行时发现；首版使用 `Open<TPanel>()` |
| UIFramework 引用 `ACTGame.App` / Domain / Server | Framework 失去跨项目复用能力，并形成反向依赖 |
| 把 `HudViewModel`、`HudPanel` 放入 UIFramework | 产品语义污染通用框架 |
| UGUI/UI Toolkit 双 Runtime 实现 | 学习和维护成本翻倍，没有当前需求 |
| Client 专用 `SelfHealthMilli` HUD 分支 | Owner Snapshot 已回写 Actor；双轨会产生显示竞态 |

---

## 7. 目录与文件预期（增量）

```text
Assets/Scripts/Framework/UIFramework/
├── UIFramework.asmdef
├── UIManager.cs          # 注册、Open/Close/Back、层级与实例所有权
├── UIPanel.cs            # Panel 基类与显式生命周期
├── UILayer.cs            # HUD / Screen / Modal / Toast
├── UILayerHost.cs        # 各层 Transform 宿主
└── UIPanelStack.cs       # 可单测的返回栈规则

Assets/Scripts/App/
├── Events/UI/
│   └── LocalHudStateChangedEvent.cs
├── Queries/UI/
│   └── GetLocalHudStateQuery.cs
└── Systems/UI/
    ├── LocalHudProjectionSystem.cs
    └── LocalHudSnapshot.cs

Assets/Scripts/UI/
├── ACTGame.UI.asmdef
└── Runtime/
    ├── Panels/           # HudPanel / PauseMenuPanel / CharacterDetailPanel
    ├── ViewModels/       # HudViewModel / MenuViewModel
    ├── Presenters/       # HudPresenter / MenuPresenter
    ├── Views/            # UGUI references only
    └── Showcase/         # U5，接 Camera C5

Assets/Tests/Editor/UI/
├── HudViewModelTests.cs
├── LocalHudProjectionSystemTests.cs
└── UiAssemblyBoundaryTests.cs

Assets/Tests/Editor/UIFramework/
├── UIFramework.EditorTests.asmdef
└── UIPanelStackTests.cs

docs/2026.9.19/UI_FRAMEWORK_PLAN.md
```

具体类名可在手写时微调，但 `UIFramework ← ACTGame.UI ← App Projection` 的引用边界与 `App Projection → ViewModel → Panel` 的数据方向不变。Prefab、Canvas、字体、Sprite、RenderTexture 与 ScriptableObject 由作者在 Unity Editor 中人工创建和绑定。

---

## 8. 风险与对策

| 风险 | 对策 |
|------|------|
| 每逻辑帧发送 UI 事件导致重建 | `LocalHudSnapshot` 值比较，变化才发；ViewModel 再做字段级变化判断 |
| 事件重复订阅 | Presenter 的 Enter/Exit 对称 Bind/Unbind；重复进入测试计数 |
| Architecture 全局实例在关闭 Domain Reload 时残留 | 页面 OnDisable 强制解绑；后续为 Architecture 增加明确 Reset/Deinitialize 测试入口，不靠析构 |
| Canvas rebuild/布局 GC | 静态 HUD 分 Canvas；只改变化控件；避免每帧 LayoutGroup/字符串拼接 |
| 菜单误停联机世界 | View 只请求 `UiInputMode`；联网策略固定不改 `Time.timeScale` |
| “MVVM”层数过多 | 首个 HUD 只保留 Projection、Presenter、ViewModel、View 四个明确职责；没有复用证据不抽基类 |
| Framework 设计过度 | U0/U1 只做 `UIManager/UIPanel` 当前需要的注册、层级、返回栈和生命周期；对象池、异步资源、动画 DSL 等有第二个明确需求再加 |
| UI 路由膨胀成业务中心 | 路由只管层级和生命周期；购买、换装、领奖等行为仍走 App Command/System |
| World Space 血条数量增长 | U4 先做正确生命周期；出现测量证据后再做池化/批处理，不提前优化 |
| 3D 展示污染战斗 | 独立 Layer/Camera/RT，视觉副本不入 Simulation；按 Camera C5 验收 |

---

## 9. Editor 人工步骤

1. U1 创建 `UIRoot` Prefab：Screen Space Overlay Canvas、CanvasScaler、GraphicRaycaster、HUD/Screen/Modal/Toast 四层与 Blocker。  
2. 场景确保只有一个 EventSystem；输入模块与当前 Input System 配套。  
3. 创建空白测试 Screen 与 Modal Prefab，将具体 `UIPanel` 模板登记到 `UIManager`。
4. U2 创建 HUD Prefab，手工绑定 HP Image/Slider、文本、槽位与支援点控件；先用占位图，不修改 Gameplay 资产。  
5. 用 Game View 分辨率预设验收 16:9、16:10、21:9 和至少一个窄屏；检查 Anchor、Safe Area、CanvasScaler。  
6. U3 验收鼠标/键盘与手柄焦点恢复；最后关闭页面后选中对象和玩法输入均恢复。  
7. U5 按 Camera C5 人工创建展示 Layer、Camera、RenderTexture、Booth Prefab 和 Profile 资产；不得改战斗 VCam Follow。

---

## 10. 推荐开工顺序

```text
U0 UIFramework 与程序集边界
  → U1 空白页面导航
  → U2 HP HUD 垂直切片
  → U3 输入/菜单/Modal
  → U4 第二真实切片
  → U5 3D 展示舱
```

**最小可感切片：** 先只做 U0 + U1 + U2 的 HP 部分：`UIManager` 注册并打开 `HudPanel`，`UIPanel` 提供显式生命周期；角色受伤后，`LocalHudProjectionSystem` 发布变化，`HudPresenter` 更新 `HudViewModel`，血条变化；换人后同一 HUD 自动显示新 Active Actor。这个切片同时验证 Framework 可独立编译与 ACTGame 业务接入，不提前加入对象池、异步加载或动画系统。

建议每个阶段的手写节奏固定为：

```text
写失败测试 → 写最小纯 C# 契约 → 让测试通过
→ 写 Unity View/Presenter → Editor 手工绑定
→ Play 验收 → 记录问题 → 再进入下一阶段
```

---

## 11. 变更日志

| 日期 | 说明 |
|------|------|
| 2026-09-19 | 初版：确定 UGUI + MVVM-lite、App 只读投影、客户端叶子程序集与 U0～U5 手写学习路线 |
| 2026-09-20 | 收敛命名与规模：改为单一 `Framework/UIFramework` + `UIFramework.asmdef`，核心类型定为 `UIManager/UIPanel`，业务页面继续留在 `ACTGame.UI` |
| 2026-09-20 | 更正 UGUI asmdef 引用名：Package 为 `com.unity.ugui`，运行时 Assembly 为 `UnityEngine.UI` |
