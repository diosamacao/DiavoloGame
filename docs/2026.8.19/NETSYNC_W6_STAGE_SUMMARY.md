# NetSync W6 阶段性说明（DS3 + DS4 首版）

> 撰写：2026-08-19  
> 角色：**W6 代码落地备忘**（Editor Play 待确认）  
> 排期：[`../2026.8.17/NETSYNC_FRAMEWORK_DEDICATED_MASTER_DEVELOPMENT_PLAN.md`](../2026.8.17/NETSYNC_FRAMEWORK_DEDICATED_MASTER_DEVELOPMENT_PLAN.md)

---

## 0. 一句话

Dedicated 用同一份 `SimulationHost.StepOnce` + `ServerSimulationRunner` 单调时钟推进权威世界；角色工厂增加 `AuthorityHeadless`（无 Model / Graph / VFX / SFX）。Join 增加 Gameplay `ContentFingerprint`。完整 Action Bake 与下行 Frame 仍属后续 Wave。

---

## 1. 交付

| 项 | 入口 |
|----|------|
| 追帧核 | `SimulationStepKernel`；Listen `SimulationHost.Update` 与 Dedicated Runner 共用步进体 `StepOnce` |
| 单调时钟 | `ServerSimulationRunner`：首拍对齐，之后按 dt 追帧并记 overrun |
| Headless 装配 | `CharacterPresentationMode.AuthorityHeadless` + `NullAnimationPlayback` |
| Notify 分类 | `ActionNotifyClassification`：VFX/SFX=Presentation |
| Locomotion 采集 | Capture 读 `SimulationNormalizedTime`，不再读 Animator |
| 内容指纹 | `ServerContentManifest`；Join 双方 Valid 且不同 → `ContentMismatch` |
| Dedicated 世界 | `DedicatedAuthorityWorld`：Join 建 Headless Actor、灌命令、外部时钟步进 |
| 敌人 | 权威端（含 Dedicated）可刷怪；Dedicated 走 Headless 工厂 |

**明确后置**

- 完整 ActionGameplayBake（仍用现有 SO 闭包）
- 向客户端发送 `ReplicationFrame` / Match 状态机（W7）
- 10 分钟浸泡与双端固定脚本对照（Editor Play）

---

## 2. 组合

```
CombatWorldController.Awake
  → 扫描场景配置 → ServerContentManifest.Fingerprint
  Dedicated → SimulationHost.DriveFromExternalClock
            → DedicatedAuthorityWorld + DedicatedServerBootstrap
            → Poll：Join/命令 → Runner.Advance → Host.StepOnce
  Listen → 仍由 SimulationHost.Update 自动步进
```
