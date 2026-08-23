---
name: actgame-architecture
description: Maintains ACTGame Unity project architecture docs, technical feature documentation, coding conventions, and design direction. Scans Assets/Scripts for structural drift, updates ARCHITECTURE.md/TECHNICAL.md/CONVENTIONS.md/ROADMAP.md, and proposes phased refactors. Use when the user asks about project architecture, implemented features, how a system works, technical documentation, framework design, conventions, refactoring structure, tech debt, module boundaries, or design direction; after implementing or changing gameplay features; or when code changes affect layer boundaries or state machine patterns.
---

# ACTGame 架构维护

## 目标

保持 `.cursor/skills/actgame-architecture/` 下的文档与代码同步，让 Agent 每次做结构性改动时都有据可依。

## 必读文件（按顺序）

1. [ARCHITECTURE.md](ARCHITECTURE.md) — 模块分层、依赖关系、数据流（**怎么组织**）
2. [TECHNICAL.md](TECHNICAL.md) — 已实现功能与实现方案（**做了什么、怎么做**）
3. [CONVENTIONS.md](CONVENTIONS.md) — 编码与目录规范
4. [ROADMAP.md](ROADMAP.md) — 设计方向与待重构项

**分工**：ARCHITECTURE = 结构；TECHNICAL = 功能与方案；二者重叠时 ARCHITECTURE 保持精简，细节放 TECHNICAL。

**新建技术/优化方案文档**（`docs/**/*_PLAN.md`、分阶段任务/验收）：改用项目 skill [actgame-design-plan](../actgame-design-plan/SKILL.md)，范本为 `LOCOMOTION_GAIT_POLICY_PLAN` / `GAS_STYLE_COMBAT_REFACTOR_PLAN`。本 skill 负责落地后的 ARCHITECTURE/TECHNICAL/ROADMAP 同步，不替代方案起草格式。

## 工作模式

根据用户意图选择一种模式；未说明时默认 **Audit + Update**。

### 1. Audit（架构审计）

扫描 `Assets/Scripts/` 与 Prefab/Data 目录，对照 ARCHITECTURE.md：

```
审计清单：
- [ ] 目录树是否与文档一致
- [ ] 新增/删除的模块是否已记录
- [ ] 依赖方向是否违反分层（Core ← Character ← Player/Enemy）
- [ ] 是否存在重复职责（如同一逻辑在 Controller 与 State 两处）
- [ ] 空占位目录（.gitkeep）是否已有实现或仍待开发
```

输出 **架构差异报告**，格式见下方模板。

### 2. Update（文档同步）

在以下时机**必须**更新文档：

- 新增/移动/删除脚本目录或核心类
- **新增、修改或删除可玩功能**（移动、战斗、UI 等）
- 引入新框架模式（事件、对象池、ScriptableObject 配置等）
- 完成 ROADMAP 中的重构项
- 用户明确要求刷新架构/技术文档

更新规则：

| 文件 | 更新内容 |
|------|----------|
| ARCHITECTURE.md | 模块图、依赖关系、关键类职责、数据流 |
| TECHNICAL.md | 功能索引、实现方案、参数表、运行时流程、限制说明 |
| CONVENTIONS.md | 从代码中归纳的新约定；标记已废弃的旧模式 |
| ROADMAP.md | 新发现的 tech debt、已完成项打勾并注明日期 |

日期戳：
- ARCHITECTURE.md 顶部 `Last audited:`
- TECHNICAL.md 顶部 `Last updated:` 与文末「变更日志」

### TECHNICAL.md 同步规则

每个**已实现功能**占一节，必须包含：

1. **功能说明** — 玩家/系统视角的一句话描述
2. **实现方案** — 技术选型表（用什么组件/模式）
3. **关键参数** — SerializeField 默认值或资产路径
4. **运行时流程** — 简明的调用链或步骤
5. **已知限制** — 未实现部分、临时方案、与 ROADMAP 的关联
6. **相关文件** — 脚本与 Prefab/Asset 路径

功能索引表状态：`✅ 已实现` · `🟡 骨架/部分` · `⬜ 未实现`

**触发 TECHNICAL 专项更新**（可不改 ARCHITECTURE）：
- 仅改某个功能的算法、参数、资产绑定
- 用户问「XX 怎么实现的」且文档过时

**触发 ARCHITECTURE + TECHNICAL 双更新**：
- 新模块目录、类职责迁移、数据流变化

### 3. Refactor（重构提案）

仅在发现**结构性问题**时提出重构，不做风格性大改。

提案必须包含：

1. **问题**：违反哪条 convention 或造成什么维护成本
2. **目标状态**：改后的模块边界（一句话）
3. **阶段计划**：每阶段可独立合并、可运行
4. **影响范围**：文件列表
5. **风险**：Prefab/Animator/Input 绑定等需人工验证的点

优先小步迁移，禁止一次性重写多个子系统。

### 4. Feature（功能文档）

对照 `Assets/Scripts/`、`Assets/Prefabs/`、`Assets/Data/` 与 TECHNICAL.md：

```
功能文档清单：
- [ ] 功能索引表是否覆盖所有可玩行为
- [ ] 各节实现方案是否与当前代码一致
- [ ] 参数默认值是否与 Prefab/Script 一致
- [ ] 运行时流程是否反映最新调用链
- [ ] 预留/未完成功能是否标记 🟡 或 ⬜
```

输出 **功能文档差异**（仅列 TECHNICAL 需改项），然后执行 Update。

### 5. Convention（规范归纳）

从现有代码提取规范，写入 CONVENTIONS.md。规则：

- 只记录**项目中已出现**或**团队明确决定**的模式
- 每个约定附 1 个文件路径示例
- 冲突时以**最新代码 + 用户口头决定**为准，并在文档中标注 superseded

## 分层铁律

```
Core/          → 与 Unity 场景无关的通用逻辑（状态机泛型等）
Character/     → 所有可控角色共享（动画、状态、Context）
Player/        → 玩家专属（Input 桥接、PlayerController）
Enemy/         → 敌人专属（BT / Desire / Request）
Combat/        → 战斗判定、伤害、Hitbox、Numeric
Input/         → Input System 封装
Camera/        → 相机与 Cinemachine（Lock-On 未做）
UI/            → 界面（未建）
Editor/        → 编辑器扩展
Data/          → ScriptableObject、配置（Assets/Data/）
```

**禁止**：Core 引用 Character/Player；Character 引用 Player/Enemy。

## 输出模板

### 架构差异报告

```markdown
# 架构审计 — YYYY-MM-DD

## 摘要
[1-2 句：整体健康度与最 urgent 问题]

## 变更检测
| 类型 | 路径 | 说明 |
|------|------|------|
| 新增/移动/删除/漂移 | ... | ... |

## 分层违规
- [ ] 无 / [列出]

## 建议
1. [按优先级排序，链接 ROADMAP 条目]
```

### 重构提案

```markdown
# 重构提案 — [标题]

## 问题
...

## 目标状态
...

## 阶段
### Phase 1 — [可独立验证的范围]
- 改动：...
- 验证：Play Mode 中 ...

## 影响文件
- path/to/file.cs

## 未决问题（需用户确认）
- ...
```

## Agent 行为约束

- 先读文档再改代码；改完代码再更新文档
- 文档与代码冲突时，以代码为准并更新文档，在报告中说明
- **解释架构时必须举仓库内代码并画 mermaid 流程图**（见 `.cursor/rules/architecture-explain-with-code.mdc`）；无实现则写明并给伪代码 + 方案图，不得只引文档
- 不创建与用户请求无关的抽象层
- 重构提案需用户确认后再实施（除非用户明确说「直接改」）
- 中文撰写文档与报告；类名/路径保持英文

### 功能文档差异（TECHNICAL）

```markdown
# 功能文档审计 — YYYY-MM-DD

## 摘要
[哪些功能文档过时或缺失]

## 需更新条目
| 功能 | 问题 | 建议修改 |
|------|------|----------|
| ... | 参数/流程/状态不符 | ... |

## 建议新增功能节
- [名称] — [原因]
```

## 快速命令对照

| 用户说法 | 模式 |
|----------|------|
| 「刷新架构文档」「同步架构」 | Update（全部文档） |
| 「同步技术文档」「更新功能文档」 | Update（侧重 TECHNICAL.md） |
| 「XX 怎么实现的」 | 读 TECHNICAL → 必要时 Feature + Update |
| 「审计架构」「检查结构」 | Audit |
| 「审计功能文档」 | Feature |
| 「该怎么重构 X」 | Refactor |
| 「总结规范」「编码约定」 | Convention |
| 「设计方向」「接下来怎么搭」 | 读 ROADMAP + Audit，给出优先级建议 |
