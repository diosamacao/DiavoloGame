# Cursor User Rules（ACTGame）

本项目的关键约束已写入两处，建议**同时使用**以提高 Agent 遵守率：

| 层级 | 位置 | 说明 |
|------|------|------|
| 项目规则 | `.cursor/rules/*.mdc` | 随 git 版本管理，团队共享 |
| User Rules | `~/.cursor/rules/*.mdc` + Settings UI | 全局权重更高，本机生效 |

## 已写入的全局 User Rules 文件

以下文件位于 `C:\Users\Diavolo\.cursor\rules\`（`alwaysApply: true`）：

- `actgame-editor-only-assets.mdc` — 美术 / Data / Prefab 仅 Editor 人工
- `actgame-code-finish-gate.mdc` — 注释 + ReadLints + 「代码收尾」清单
- `actgame-change-summary.mdc` — 各脚本改动总结 + 整体思路 + Editor 验收清单
- `actgame-architecture-explain-with-code.mdc` — 解释架构时举真实代码、画流程图；无则伪代码+方案图
- `actgame-no-legacy-compatibility.mdc` — 重写/重构不保留旧兼容层

新开 Agent 对话后应自动加载。若未生效，请将下方「Settings 粘贴版」复制到 **Cursor Settings → Rules → User Rules**。

## Settings 粘贴版（备用）

将以下内容粘贴到 **Cursor Settings → Rules → User Rules**：

```
ACTGame Unity 项目约束（最高优先级，与 .cursor/rules/ 一致）：

【资产】不得创建/修改/删除 Assets/Art/**（Shader 源码除外）、Assets/Data/**、Assets/Prefabs/**、任意 .asset / .prefab / .meta / .inputactions、.mat 材质。只改 Assets/Scripts/** 的 C#；**允许**创建/编写 Shader 源码（.shader / .shadergraph / .hlsl / .cginc 等，如 Assets/Shaders/** 或 Assets/Art/**/Shaders/**）。材质绑 Shader、Prefab 引用仍输出 Unity Editor 操作步骤。

【代码收尾】每次创建或修改代码后，向用户表示「已完成」前必须：
1. 为改动的类、public/protected 成员、非 obvious 逻辑补充注释；
2. 调用 ReadLints 工具，paths 覆盖本次全部改动文件；有错误则修复后再查；
3. 在最终回复末尾附「代码收尾」清单（注释文件列表、ReadLints 结果、未解决问题）。
禁止仅用文字说「已检查 linter」而不调用 ReadLints。

【架构解释】解释分层、模块边界、数据流或某系统怎么组织时：
1. 先读 Assets/Scripts 再解释，每个关键结论至少引用 1 处真实代码（路径+行号）；
2. 必须画 mermaid 代码流程图（flowchart 或 sequenceDiagram），节点用真实类型/方法名；
3. 仓库里没有对应实现时，写明「当前代码无此实现」，再给伪代码方案，并画标明「方案」的流程图；
4. 禁止只画分层、只引文档、流程图用空泛模块名，或把未落地的 API 写成已存在。

【重写/重构】涉及代码逻辑重写、框架重构、模块替换时，默认不保留旧兼容层：
1. 新逻辑为唯一真源，删除旧入口/旧分支/旧适配层；
2. 不保留 Legacy/Old/V1/Fallback 与 New/V2 双轨运行；
3. 非我明确要求，不要加入过渡兼容 wrapper/adapter；
4. 最终说明中列出清理了哪些旧逻辑。
```

## 验证

1. 打开 **Cursor Settings → Rules**，确认 User Rules 中有上述文本（若使用粘贴版）
2. 在 Agent 中改一个 `.cs` 文件，检查是否：补注释 → 调用 ReadLints → 回复含「改动总结」与「代码收尾」；涉及运行时/测试时还含「请你在 Editor 确认」
3. 问「XX 架构怎么组织」时，检查回复是否引用了真实 `.cs` 且含 mermaid 流程图；若功能未落地，是否写了「当前代码无此实现」+ 伪代码 + 方案流程图
