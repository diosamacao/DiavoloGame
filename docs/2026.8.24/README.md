# 2026.8.24 — 复制带宽三项优化（方案，未实现）

> 制定：2026-08-24  
> 角色：**索引**。实施真源是下面三份 `*_PLAN.md`，不是本文。  
> 现行实现仍以 [`../2026.8.23/NETSYNC_FROM_JOIN_TO_HIT.md`](../2026.8.23/NETSYNC_FROM_JOIN_TO_HIT.md) 与代码为准。

组队 PVE 下行仍是「整份 67 字节角色快照、字节相等才跳过」。人数上去后，走路/出招会让整包每 Tick 变脏。下列三份方案只改 **下行角色 Schema 与 Observer 对账**，不改上行 `InputFrame`、不改权威命中。

| 顺序 | 文档 | 阶段前缀 | 一句话 |
|------|------|----------|--------|
| 1 | [REPLICATION_FIELD_MASK_PLAN.md](./REPLICATION_FIELD_MASK_PLAN.md) | `RS-M` | Schema V2 分块 + 掩码；走位只带 Pose |
| 2 | [REPLICATION_POSE_STATE_SPLIT_PLAN.md](./REPLICATION_POSE_STATE_SPLIT_PLAN.md) | `RS-S` | 同实体内 Pose 高频、状态只在变化时发 |
| 3 | [REPLICATION_ACTION_CLOCK_PLAN.md](./REPLICATION_ACTION_CLOCK_PLAN.md) | `RS-C` | 权威只发动作会话，Observer 本地推帧 |

**推荐开工：** `RS-M` → `RS-S` → `RS-C`。  
`RS-S` 依赖 V2 分块，禁止另开第二套 Entity/Schema 冒充分频。  
`RS-C` 依赖 Action 分块已存在（`RS-M2` 出口）；有 `RS-S` 后动作块才不会跟 Pose 绑在同一 Due。

未要求「按方案实现」前不改业务代码。
