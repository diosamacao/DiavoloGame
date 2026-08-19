# NetSync W8 阶段性说明（DS6）

> 撰写：2026-08-19  
> 角色：**W8 / M2 DS-Demo 备忘**（Dedicated 出包与人工验收已于 2026-08-19 用户关闭）  
> 排期：[`../2026.8.17/NETSYNC_FRAMEWORK_DEDICATED_MASTER_DEVELOPMENT_PLAN.md`](../2026.8.17/NETSYNC_FRAMEWORK_DEDICATED_MASTER_DEVELOPMENT_PLAN.md)  
> 启动手册：[`DEDICATED_SERVER_LAUNCH.md`](./DEDICATED_SERVER_LAUNCH.md)

---

## 0. 一句话

Dedicated 进程可按 **CLI > Env > File > Default** 解析启动参数，监听成功打 `READY`，并可在空 Lobby 超时或对局结束后请求退出（玩家构建 `Application.Quit`，**Editor 不退出**）。Windows Dedicated Build + 双 Client 对局已验收。**M2 / LAN DS-Demo 关闭**；恢复联网主路径时从 **W9 Listen 组合**开始。

---

## 1. 本阶段代码交付

| 项 | 入口 |
|----|------|
| 覆盖优先级 | `ServerLaunchConfigResolver`：CLI `-actgame-*` > `ACTGAME_*` > 配置文件 > `CreateDefault` |
| 生命周期 | `EmptyLobbyTimeoutMs`（0=不超时）、`ExitOnMatchEnd` |
| Ready | `DedicatedServerRuntime.IsReady`；日志 `DedicatedServerBootstrap: READY port=… role=DedicatedServer。` |
| 空房超时 | 仅「从未有人加入」的 Lobby；到时 `ShouldExit`，退出码 0 |
| 对局结束退出 | `ExitOnMatchEnd=true` 时 EmptyRoom / Completed 后 `ShouldExit`，不再 Accept |
| Editor | `CombatWorldController` 强制 `ExitOnMatchEnd=false` 且超时 0，保留回 Lobby 再入房 |
| 玩家构建默认 | `ExitOnMatchEnd=true`；可用 `-actgame-exit-on-match-end 0` 改成常驻 |
| 烟测脚本 | `tools/dedicated/smoke-ready.ps1`（需先有 Dedicated 可执行文件） |

**后置（不挡 M2）**

- CI 里自动 Unity 出包（现有脚本只覆盖「已有 exe 后看 READY」）
- 自动拉起 Server + Client A/B 并断言 MatchEnd
- Linux Dedicated Build（LAN Demo 以 Windows 包为准）

---

## 2. 组合

```
CombatWorldController.Awake（Dedicated）
  → CreateDefault（Editor 不退出 / 玩家构建 ExitOnMatchEnd=true）
  → ServerLaunchConfigResolver.TryResolve（CLI > Env > File）
  → DedicatedServerBootstrap.Configure
       TryStart → READY
       Poll → ShouldExit 时玩家构建 Quit(0)，Editor 只 Dispose
```

空 Playing 房仍先发 `MatchEnd(EmptyRoom)` 再回 Lobby；若 `ExitOnMatchEnd` 则接着请求退出。

---

## 3. 测试

- `ServerLaunchConfigResolverTests`：优先级、缺文件、非法端口/超时、密钥键忽略
- `DedicatedServerRuntimeTests`：Ready、空房超时、超时在入房后不再触发、`ExitOnMatchEnd` 拒收再入、默认配置仍可再入房

---

## 4. 与 W7 的关系

W7 Editor Play 已于 2026-08-19 用户验收。W7 合同（合并进下一帧、首 Hint 和解、Headless 记 `CurrentKey`、整段推迟 2m 硬吸）不变。W8 出包与 H-DS-D 已于同日用户验收，M2 关闭。
