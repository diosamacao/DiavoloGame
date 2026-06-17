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

新开 Agent 对话后应自动加载。若未生效，请将下方「Settings 粘贴版」复制到 **Cursor Settings → Rules → User Rules**。

## Settings 粘贴版（备用）

将以下内容粘贴到 **Cursor Settings → Rules → User Rules**：

```
ACTGame Unity 项目约束（最高优先级，与 .cursor/rules/ 一致）：

【资产】不得创建/修改/删除 Assets/Art/**、Assets/Data/**、Assets/Prefabs/**、任意 .asset / .prefab / .meta / .inputactions。只改 Assets/Scripts/** 的 C#。需要动资产时只输出 Unity Editor 操作步骤。

【代码收尾】每次创建或修改代码后，向用户表示「已完成」前必须：
1. 为改动的类、public/protected 成员、非 obvious 逻辑补充注释；
2. 调用 ReadLints 工具，paths 覆盖本次全部改动文件；有错误则修复后再查；
3. 在最终回复末尾附「代码收尾」清单（注释文件列表、ReadLints 结果、未解决问题）。
禁止仅用文字说「已检查 linter」而不调用 ReadLints。
```

## 验证

1. 打开 **Cursor Settings → Rules**，确认 User Rules 中有上述文本（若使用粘贴版）
2. 在 Agent 中改一个 `.cs` 文件，检查是否：补注释 → 调用 ReadLints → 回复含「代码收尾」清单
