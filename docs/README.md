# ACTGame 文档索引

> 更新：2026-09-03 — 受击档由冲击力对韧性裁定。换人仍见 `2026.8.30`。

**先读**

| 文档 | 角色 |
|------|------|
| [PROJECT_CHECKLIST.md](./PROJECT_CHECKLIST.md) | 一页总览：进度 / 下一步 / 明确不做 |
| `.cursor/skills/actgame-architecture/` | 运行时真源：ARCHITECTURE / TECHNICAL / CONVENTIONS / ROADMAP |

---

## 现行方案（仍有未关出口或字段合同）

| 文档 | 角色 |
|------|------|
| [2026.8.23/NETSYNC_FROM_JOIN_TO_HIT.md](./2026.8.23/NETSYNC_FROM_JOIN_TO_HIT.md) | **联网实现阅读入口**：Join → 命中现行调用链 |
| [2026.8.24/README.md](./2026.8.24/README.md) | 下行角色快照带宽：掩码 / 分频 / 本地推帧（方案，未实现） |
| [2026.8.17/NETSYNC_FRAMEWORK_DEDICATED_MASTER_DEVELOPMENT_PLAN.md](./2026.8.17/NETSYNC_FRAMEWORK_DEDICATED_MASTER_DEVELOPMENT_PLAN.md) | 联网排期：W10/W11 Play 与 W12 未关 |
| [2026.8.19/DEDICATED_SERVER_LAUNCH.md](./2026.8.19/DEDICATED_SERVER_LAUNCH.md) | Dedicated 本地启动与退出码 |
| [2026.8.15/UE_ALIGNED_CLIENT_PREDICTION_PLAN.md](./2026.8.15/UE_ALIGNED_CLIENT_PREDICTION_PLAN.md) | 走跑纠偏合同（2m 硬吸 / Restore+Replay） |
| [2026.8.20/NETSYNC_ARCHITECTURE_PROBLEMS.md](./2026.8.20/NETSYNC_ARCHITECTURE_PROBLEMS.md) | 联网踩坑备忘（不是排期真源） |
| [2026.8.6/MASTER_IMPLEMENTATION_PLAN.md](./2026.8.6/MASTER_IMPLEMENTATION_PLAN.md) | 战斗 / 位移 Wave 排期（0～4 已关；相机独立） |
| [2026.8.29/CAMERA_SKILLSHOT_AND_STRETCH_PLAN.md](./2026.8.29/CAMERA_SKILLSHOT_AND_STRETCH_PLAN.md) | **大招多机位 + 镜头拉伸** 排期真源（CS0～CS3） |
| [2026.8.29/CAMERA_SPLINE_INTEGRATION_PLAN.md](./2026.8.29/CAMERA_SPLINE_INTEGRATION_PLAN.md) | **Action Camera 样条轨迹接替**实施真源（C-SP0～C-SP3） |
| [2026.8.26/CAMERA_SYSTEM_PLAN.md](./2026.8.26/CAMERA_SYSTEM_PLAN.md) | 相机总览：Director / Lock-On / UI 展示舱（C5） |
| [2026.8.13/CAMERA_AUTHORITY_AND_TARGETING_REFACTOR_PLAN.md](./2026.8.13/CAMERA_AUTHORITY_AND_TARGETING_REFACTOR_PLAN.md) | Camera C1 前置：MoveReferenceYaw + SelectedTarget |
| [COMBAT_NUMERICS_PLAN.md](./COMBAT_NUMERICS_PLAN.md) | 资源字段与产品语义 |
| [2026.8.30/PARTY_SWITCH_ASSIST_PLAN.md](./2026.8.30/PARTY_SWITCH_ASSIST_PLAN.md) | **三人编队换人 / 极限支援（弹刀）/ 支援突击**（P-SW0～5；切人不再后置） |
| [2026.9.3/HIT_REACTION_IMPLEMENTATION_PLAN.md](./2026.9.3/HIT_REACTION_IMPLEMENTATION_PLAN.md) | **受击档位 + Additive**：冲击力对韧性已接；轻击 Play 已验 |
| [2026.8.6/SKILL_AND_RESOURCE_SYSTEM_PLAN.md](./2026.8.6/SKILL_AND_RESOURCE_SYSTEM_PLAN.md) | 技能槽 / 完美闪避产品；切人/支援改由 8.30 篇真源 |
| [ACTION_SYSTEM_LOCKSTEP_REFACTOR_PLAN.md](./ACTION_SYSTEM_LOCKSTEP_REFACTOR_PLAN.md) | 模拟核 L0～L2；剩余 L1B Play / L2 斜坡 / L3 |
| [ENEMY_BEHAVIOR_TREE_PLAN.md](./ENEMY_BEHAVIOR_TREE_PLAN.md) | BT Runner 契约（§3.4 输出槽） |
| [2026.8.11/ENEMY_BEHAVIOR_TREE_BACKLOG_PLAN.md](./2026.8.11/ENEMY_BEHAVIOR_TREE_BACKLOG_PLAN.md) | BT 编辑器待优化（A2～A5） |
| [ACTION_EDITOR.md](./ACTION_EDITOR.md) | Action Editor 愿景 |
| [ACTION_EDITOR_IMPLEMENTATION.md](./ACTION_EDITOR_IMPLEMENTATION.md) | Action Editor 实现方案 |

## 方案范本（已落地，供新方案对照格式）

| 文档 | 角色 |
|------|------|
| [2026.8.9/LOCOMOTION_GAIT_POLICY_PLAN.md](./2026.8.9/LOCOMOTION_GAIT_POLICY_PLAN.md) | 结构范本：对峙循环 + GaitPolicy |
| [2026.8.7/GAS_STYLE_COMBAT_REFACTOR_PLAN.md](./2026.8.7/GAS_STYLE_COMBAT_REFACTOR_PLAN.md) | 阶段勾选范本：Numeric / Effect（G0～G5 已关） |

新方案写法见 `.cursor/skills/actgame-design-plan/`。

## 其它

| 文档 | 角色 |
|------|------|
| [THIRD_PARTY_LICENSES.md](./THIRD_PARTY_LICENSES.md) | 第三方许可 |

日期子目录只保留仍被上表引用的文件；已关闭的波次备忘、日计划与被替代方案不再归档。
