---
name: actgame-execution-trace
description: >-
  Answers ACTGame feature runtime questions as a compact hierarchical
  call-chain (scenario title, Type.Method arrows, indented branches,
  negative paths). Use when the user asks 执行过程, 调用链, 怎么跑, 现行链路,
  命中后发生什么, 走哪条路径, or how a gameplay feature executes at runtime.
  Do not use for architecture, layering, or framework-design questions
  (those use mermaid + code citations).
---

# ACTGame 执行过程链路

用户问「某功能怎么跑 / 执行过程 / 调用链 / 现行链路 / 走哪条路径」时，**主答只用本格式**。
不要 mermaid、不要大段散文、不要按分层讲课。

架构 / 模块边界 / 框架怎么组织 → `.cursor/rules/architecture-explain-with-code.mdc`。

## 工作流

1. 先 Grep/Read `Assets/Scripts/**`（及相关测试）。文档只作索引，结论以代码为准。
2. 按**可观察场景**拆块，不按类拆块。主路径、强制旁路、早退/负路径各写一块。
3. 每块写成缩进链路。无实现则标题标明「当前代码无此实现」，再写拟议链；禁止写成已落地。

## 格式

```
<场景名>（<一句话定性，可选>）
  <入口> → <Type.Method> → <Type.Method>
    → <关键裁定>(<判定轴>)
    → <确认点>
         <分支A> → <动作>（<副作用，边沿 X>）
         <分支B> → <动作>（<副作用，边沿 Y>）

<另一场景>（<与上一条的差异>）
  <入口>：<前置条件>
    <优先级 / 互斥>
    1. <主体A 做什么>（<明确不做什么>）
    2. <主体B 调用>
         <子调用>
           Field = Value
           不读 <字段>
         <后续>
    3. <状态切换>
  <负路径条件> = <结果，不调用清单>
```

## 写法铁律

- **场景标题**：中文结果名；括号写定性（强制 Stun、不走冲击力裁定）。
- **主链用 `→`**：节点必须是真实 `Type.Method` / 字段 / 枚举 / 窗口谓词；中文只用于判定轴与副作用。
- **缩进 = 从属**：下一层展开上一跳，不是另开一条时间线。
- **兄弟分支并列缩进**：不写 if/else，不写「否则」。
- **多主体才编号**：同一对象上的连续调用继续用 `→`；换执行主体或独立步骤才 `1. 2. 3.`。
- **括号只写副作用**：不停招、边沿、吞伤、force。不写解释性从句。
- **赋值一行**：`Kind = LightStun`。
- **负路径单独一行**：`条件 = 结果，不 X、不 Y`。
- **优先 / 互斥**写在入口下第一行，不另起章节。
- **一块一事**：一条链路只讲一条可玩结果。

## 禁止

- mermaid / 分层目录 / 大段代码引用作为主答（用户另要架构时再补）
- 空泛节点（「模块处理一下」「走战斗系统」）
- 把规划 API 写成已有链
- 主路径里藏负路径；漏写「不调用」
- 用表格或编号清单复述同一条链

## 范本

```
现行真命中
  Pipeline → Target.OnHit → Vitality.HitReceived
    → Resolver.Resolve(冲击力 vs 韧性)
    → ConfirmHitReaction
         Flinch → IssueFlinch（不停招，边沿 None）
         LightStun+ → NotifyHit + EnterHit（边沿 Hit）

被弹刀（强制 Stun，不走冲击力裁定）
  Pipeline：玩家 IsInAssistParryWindow
    优先于普通无敌；与 PerfectDodgeWindow 互斥（窗内只走弹刀）
    1. 玩家吞伤，不 OnHit、不 EnterHit
    2. 按 AttackerId 取 CharacterReactionService.IssueParried()
         Resolver.ResolveParried()
           Kind = LightStun
           StunAction = ReactionSet.Resolve(Parried, default)
           不读 interruptLevel / 韧性 / SuperArmor
         ConfirmHitReaction(LightStun)  → VitalityEdge.Hit
         NotifyHit + EnterHit(force)
    3. 玩家切 AssistParrySuccess + ArmAssistFollowUp
  仅 Invincible、无 ParryWindow = 只吞伤，不 IssueParried、不武装
```
