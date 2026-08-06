# 2026.8.6 优化文档索引

本目录为 2026-08 一批战斗/动作/相机优化方案。**先读总案，再读单篇。**

| 文档 | 角色 |
|------|------|
| **[MASTER_IMPLEMENTATION_PLAN.md](./MASTER_IMPLEMENTATION_PLAN.md)** | **排期 / 依赖 / 真源裁定（先读）** |
| [ACTION_DEFINITION_OPTIMIZATION_PLAN.md](./ACTION_DEFINITION_OPTIMIZATION_PLAN.md) | Action 数据权威、BaseMotion、Modifier/Command |
| [CHARACTER_MOVEMENT_ANCHOR_OPTIMIZATION_PLAN.md](./CHARACTER_MOVEMENT_ANCHOR_OPTIMIZATION_PLAN.md) | Gameplay/Residual 轨迹、VisualMotionRoot |
| [SKILL_AND_RESOURCE_SYSTEM_PLAN.md](./SKILL_AND_RESOURCE_SYSTEM_PLAN.md) | 技能槽语义与 Graph 路由（字段见 NUMERICS） |
| [CAMERA_SYSTEM_PLAN.md](./CAMERA_SYSTEM_PLAN.md) | Director / Lock-On / SkillShot |

关联真源（目录外）：

- [`../COMBAT_NUMERICS_PLAN.md`](../COMBAT_NUMERICS_PLAN.md) — `ActionResourceSpec` 字段与 N*
- [`../ACTION_SYSTEM_LOCKSTEP_REFACTOR_PLAN.md`](../ACTION_SYSTEM_LOCKSTEP_REFACTOR_PLAN.md) — 锁步与 Sim 边界

## Wave 速览

```text
0 观测保护网 → 1 位移止血 → 2 稳定锚点+删RM
  → 3 资源循环(含同键EX) → 4 吸附/绕背+LockOn → 5 大招镜头(+后置)
```

细节与验收见总案 §6。
