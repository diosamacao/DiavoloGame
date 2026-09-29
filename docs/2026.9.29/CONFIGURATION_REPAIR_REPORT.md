# 配置校验与修复记录 — 2026-09-29

## 结果

全库内容校验中的 **34 项配置错误已归零**。综合校验从 85 降至 51；余下 51 项全部为既有 `StructureAudit` 源码结构问题，不是 ActionGraph、角色、动作、Locomotion 或行为树配置错误。没有为了消除错误而放宽内容校验。

Unity 已打开，因此通过现有 Editor 内的 `CharacterAuthoringValidationRunner` 执行，未启动第二个批处理 Unity。最终定向 EditMode 测试 **71 通过、0 失败、0 跳过**，执行时间为北京时间 2026-09-29 02:03:07–02:03:12。此结果不等同于全工程全部测试或 Play 手感通过。

## 修复内容与原因

| 原问题 | 原因 | 处理 |
|---|---|---|
| 31 个 `BAKED_FRAME_COUNT_MISMATCH` | 播放按 Clip 的 RoundToInt 计算帧数，旧烘焙按 RM 的 CeilToInt/整段长度取尾，多段动作累积差异；个别表还已过期 | 按当前配对 RM 重新采样，以播放段范围截取和拼接 |
| `Unagi_Attack_Counter` 330 → 240 帧 | 原表与当前两个播放段不一致 | 核对当前同名 RM：91 + 149 = 240，重新生成，不直接裁剪旧表 |
| `Unagi_ChargeAttack_03` 空表 | 两个播放槽误用了 RootMotion Clip，无法按 InPlace 规则配对 | 改为已有的 Attack03、Attack03_End InPlace；分别 2.35、3.25 秒，与原动画同长，生成 336 帧表 |
| `Monster_Goblin_Hit_Shake` 空表 | 播放槽误用了 RootMotion Clip | 改为同长的现有 InPlace，生成 25 帧表 |
| `Unagi_Enemy_LocomotionSO` 无有效整数帧轨 | StartEnd、StopL、StopR、PivotTurn 的轨帧数为 0 | 由当前映射 Clip 重烘焙 4 条轨，帧数 217、222、222、70 |
| 另 13 个原本帧数正确的动作 | 旧表来源指纹不含新版播放范围 | 一并重烘焙、更新指纹，防止持续显示 Dirty |

共涉及 **46 个 ActionDefinition + 1 个 LocomotionProfile = 47 个资产**。完整逐资产清单、最终帧数和 Clip 替换数量见 `CONFIGURATION_REPAIR_SEMANTIC_CHECK.json`，配对与写入记录见 `CONFIGURATION_REPAIR_ALL_MOTION.txt`。

## 代码变更逐文件

| 文件 | 本次改动 |
|---|---|
| `Assets/Scripts/Editor/Combat/Motion/ActionMotionBakeRange.cs`（新增） | 共用播放范围解析；RM 不足时拒绝写入；指纹纳入起止帧和版本 |
| `Assets/Scripts/Editor/Combat/Motion/ActionMotionBakeService.cs`（修改） | 每段按播放范围截取；空 Clip 拒绝；总帧匹配后才写表；单招只保存目标资产 |
| `Assets/Scripts/Editor/Combat/Motion/ActionMotionDirtyUtility.cs`（修改） | 与 Baker 共用范围和指纹，能识别同长度但裁切位置改变的旧表 |
| `Assets/Scripts/Editor/Combat/Motion/MotionContentRepair.cs`（新增） | 只读计划、确定性配对、修复前资产/meta备份；在临时副本成功烘焙后写回目标；重新导入保存的目标 |
| `Assets/Scripts/Editor/Character/CharacterAuthoringValidationRunner.cs`（修改已有本地文件） | 增加 plan-motion、repair-motion 请求与本轮回归测试筛选 |
| `Assets/Tests/Editor/Character/ActionMotionBakeRangeTests.cs`（新增） | 5 个测试用例：RM 较长、RM 较短拒绝、显式裁切、同长度平移改变指纹 |

新脚本对应 `.meta` 由 Unity 导入生成。没有新增 Python 脚本；复用既有补充编译脚本，并通过临时 stdin 执行只读资产对比。同步了 `TECHNICAL.md` 与 `CONVENTIONS.md`。

移除的旧逻辑：Baker/Dirty 中以 RM 全长决定动作段结束帧的分支。不保留双路径或自动拉伸/补帧机制。Unity 保存资产时清除了已从类型删除的 `useRootMotion`、`desiredReaction`、部分旧 Locomotion 字段，并补写当前类型的默认空列表、默认相机设置与 HitPayload 默认值；未重新引入旧兼容逻辑。

## 验证与证据

1. Editor 内执行 `MotionContentRepair.Run(false)`，先核对全部配对及播放时长，再执行 `Run(true)`。
2. 最终刷新、重新加载后的只读计划没有待修复动作/移动项，`BlockedOrFailed=0`。证据：`CONFIGURATION_REPAIR_FINAL_PLAN.txt`。
3. `StructureValidationBatch.ValidateAll()`：51 项，错误均为 `StructureAudit`。覆盖全库 Action、Graph、CharacterConfig、LocomotionProfile、敌人行为树。证据：`CONFIGURATION_REPAIR_AUDIT.txt`。
4. Unity TestRunner：71/71，`ACTGame.Simulation.EditModeTests.dll` 6 项，`Assembly-CSharp-Editor.dll` 65 项。证据：`CONFIGURATION_REPAIR_TEST_RESULTS.xml`。
5. 47 个资产与本轮首次写入前备份逐一对比：全部 GUID/meta 不变；动作总帧、段裁切、命中/取消等时序、资源和位移模式保持；移动配置除生成轨及来源指纹外的有效配置保持。仅 3 个明确误绑的播放引用更换。Unity 默认字段归一化规则记录在 JSON 中。
6. 复用 `.utmp/check_authoring_compile.py` 的 Roslyn 补充编译：5 个模块成功；不代替 Unity 编译结果。
7. `git diff --check -- Assets/Scripts Assets/Tests` 通过；全库标准检查提示 Unity 序列化新增空字符串行的尾空格，按 `git -c core.whitespace=-blank-at-eol diff --check` 检查无其余空白错误。

备份：`.utmp/motion-content-repair/20260928-175619/` 与 `20260928-180127/`。如需恢复同一资产，优先使用本轮最早备份；其中包含原 `.asset` 与 `.meta`。未提交、推送或切换分支，保留此前本地修改。

## 剩余 Editor 验收

- 可再次运行菜单 `ACTGame > Architecture > Validate All Structure And Content`，预期仍是 51 项源码结构问题，内容错误为 0。
- Test Runner > EditMode > `Assembly-CSharp-Editor` > `ActionMotionBakeRangeTests` 可单独复跑本轮 5 项回归；菜单 `ACT > Character > Run Authoring Validation` 运行本轮 71 项集合。
- Gameplay 场景 Play：确认 Unagi 连段、Counter、ChargeAttack03 的位移与命中窗口，Goblin Hit_Shake 播放，以及敌人 StartEnd/StopL/StopR/PivotTurn 的急停转身。尚未以人工操作验收这些手感项。
- Inspector 中检查修复动作的表帧数等于 Total Frames、Status=Ok；Dirty 判断使用当前角色的 RM 目录。Action Inspector 的全局 RootMotion Folder 若指向其他角色目录，仍会因配对失败显示 Dirty，应选回对应目录。
- 本轮不需要新增 prefab/场景连线；未修改 prefab、场景、动画源或导入设置。
