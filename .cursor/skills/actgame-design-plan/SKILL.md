---
name: actgame-design-plan
description: >-
  Authors ACTGame technical design / optimization plan markdown under docs/
  using the project's canonical plan structure (GAS-lite and Locomotion GaitPolicy
  style): problem, principles, architecture, phased 任务/验收/出口 checklists,
  migration, risks, Editor steps. Use when the user asks for 方案, 优化方案,
  实施计划, 重构计划, design plan, or to draft/update a docs/**/*_PLAN.md
  before coding.
---

# ACTGame 方案文档产出

## 何时使用

用户要求「先出方案 / 写优化方案 / 制定计划 / 重构方案」或新建/改写 `docs/**/*_PLAN.md`、`docs/**/*_OUTLINE.md` 中的**可实施技术真源**时，**先读本 skill 再写文档**；未定稿前不直接大改代码。

## 必读范本（按顺序对照）

写新方案前，至少打开并对照：

1. [docs/2026.8.9/LOCOMOTION_GAIT_POLICY_PLAN.md](../../../docs/2026.8.9/LOCOMOTION_GAIT_POLICY_PLAN.md) — **结构范本（推荐）**：一句话 → 问题 → 原则 → 架构 → **分阶段任务/验收/出口** → 迁移删除表 → 风险 → Editor 步骤  
2. [docs/2026.8.7/GAS_STYLE_COMBAT_REFACTOR_PLAN.md](../../../docs/2026.8.7/GAS_STYLE_COMBAT_REFACTOR_PLAN.md) — **阶段勾选范本**：`### G*` + **任务** / **验收** / **出口**；零兼容政策写法  

可选对照（同类问题再读）：

- 编辑器/体验向：`docs/2026.8.9/ENEMY_BEHAVIOR_TREE_OPTIMIZATION_PLAN.md`  
- 契约锁定向：`docs/ENEMY_BEHAVIOR_TREE_PLAN.md`  

完整章节骨架见 [TEMPLATE.md](TEMPLATE.md)。

## 工作流

1. **定范围**：一句话说清改什么、不改什么；标出锁步 / InputFrame / 资产边界。  
2. **扫现状**：用只读搜索核对代码基线，禁止凭记忆写「现状」。  
3. **选目录**：`docs/YYYY.M.D/<TOPIC>_PLAN.md`（与当日工作目录一致；无则用当天日期文件夹）。  
4. **按模板撰写**（见下节强制结构）。  
5. **挂索引**：更新同目录 `README.md`（若有）；若影响架构方向，在 `actgame-architecture` 的 ROADMAP 加一条指向本方案（勿空改 TECHNICAL 实现细节）。  
6. **收尾**：变更日志一行；阶段勾选全为 `[ ]`（未做）或如实 `[x]`。  
7. **默认停在文档**：除非用户明确说「按方案实现」，否则不开始编码。

## 强制结构

方案正文**必须**包含以下块（标题级别可微调，语义不可缺）：

| 块 | 要求 |
|----|------|
| 文头引用块 | 制定日期、角色（是否为某子系统真源）、关联文档链接 |
| §0 一句话 | 单段；含结构手段 + 禁止项（如禁 if 身份 / 禁双轨） |
| 问题与动机 | 现状基线（可用代码路径/调用链）+ 痛点 + 目标表（含「不做」） |
| 设计原则 | 条目列表；对齐项目铁律（见下） |
| 目标架构 | ASCII/mermaid 数据流或模块图 + 关键契约（输入输出） |
| **分阶段交付** | 每阶段：`### <ID> — 标题` + **任务** + **验收** + **出口** |
| 迁移与删除 | 迁什么、删什么；禁止长期 Compat/Legacy 双轨 |
| 风险与对策 | 表 |
| Editor 人工步骤 | Agent 不改 `.asset`/Prefab 时的清单（若涉及配置） |
| 开工顺序 | 最小可感切片一句 |
| 变更日志 | 日期 + 说明 |

### 阶段节格式（强制，对齐 GAS / L-GP）

```markdown
### L-XX1 — 阶段名

**任务**

- [ ] 具体可交付物（类名/文件/删除项写清）
- [ ] …

**验收**

- [ ] 可判定的通过条件（单测名、Play 现象、rg 无某符号）
- [ ] …

**出口：** 一句话完成态。→ **未达成** / **已达成（YYYY-MM-DD）**
```

规则：

- 任务写「做什么」；验收写「怎么证明做完」；禁止验收复述任务空话。  
- 同一阶段内完成定义自洽；跨阶段依赖在任务里写明「依赖 L-XXx」。  
- 明确删除旧路径时，把删除项写进**任务**与**迁移删除表**，验收用 `rg`/编译/单测可证。

## 项目铁律（方案里必须体现）

写入原则或「不做 / 删除」时，与仓库规则一致：

1. **零长期兼容**：不保留 Legacy/Old/V1 与 New 双轨；迁移窗口须用户明确要求。  
2. **锁步边界**：Gameplay 权威仍在 `SimulationWorld` / `InputFrame`；方案不得引入 Update 旁路权威。  
3. **资产**：方案可列 Editor 步骤；正文注明 Agent **不直接改** `Assets/Data/**`、Prefab、非 Shader 美术。  
4. **结构优先于 if**：角色/模式差异优先 Policy、Profile、策略对象、不同资产装配，禁止方案以 `if (敌人)` 为终态。  
5. **标识英文、说明可中文**；方案正文默认简体中文。

## 文风

- 直接、可执行；少空话。  
- 表格承载对比与预设；调用链用 `text` 代码块。  
- 不写「可选地考虑也许」而无定案；二选一时写**推荐项 + 只留一种**。  
- 阶段 ID 稳定（如 `G0`/`L-GP1`/`BT-E3`），便于 ROADMAP / 对话引用。

## 禁止

- 只有叙述没有 **任务/验收/出口** 勾选的「散文方案」。  
- 把实现细节 PR 当成方案（方案是真源，代码随后对齐）。  
- 在方案里承诺 Agent 会改 `.asset` / Prefab。  
- 复制范本全文而不按当前问题改写现状与验收。

## 完成后自检

- [ ] 已对照 LOCOMOTION_GAIT_POLICY 与 GAS 阶段节格式  
- [ ] 每阶段均有任务 + 验收 + 出口  
- [ ] 有「不做 / 删除」边界  
- [ ] 路径落在 `docs/YYYY.M.D/` 且索引/ROADMAP 已按需更新  
- [ ] 用户未要求实现前未改业务代码  
