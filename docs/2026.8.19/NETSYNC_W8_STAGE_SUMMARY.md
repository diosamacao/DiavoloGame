# NetSync W8 阶段性说明（DS6 代码切面）

> 撰写：2026-08-19  
> 角色：**W8 代码落地备忘**（Unity Dedicated Build / H-DS-D 仍待 Editor 验收）  
> 排期：[`../2026.8.17/NETSYNC_FRAMEWORK_DEDICATED_MASTER_DEVELOPMENT_PLAN.md`](../2026.8.17/NETSYNC_FRAMEWORK_DEDICATED_MASTER_DEVELOPMENT_PLAN.md)  
> 启动手册：[`DEDICATED_SERVER_LAUNCH.md`](./DEDICATED_SERVER_LAUNCH.md)

---

## 0. 一句话

Dedicated 进程可按 **CLI > Env > File > Default** 解析启动参数，监听成功打 `READY`，并可在空 Lobby 超时或对局结束后请求退出（玩家构建 `Application.Quit`，**Editor 不退出**）。真正的 Windows/Linux Dedicated Build 与双 Client 对局（H-DS-D-1～10）必须在 Unity Editor 完成，本环境无法出包。

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

**明确仍属 Editor / 本机**

- 安装 Dedicated Server Build Support
- Windows / Linux Server Build Profile 与 Bootstrap 场景
- 无 GPU 环境启动 Dedicated Build
- 两个 Client Build 打完一局（H-DS-D-1～10）
- CI 里真正跑 Unity 出包（脚本只覆盖「已有 exe 后看 READY」）

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

W7 Editor Play 已于 2026-08-19 用户验收。W7 合同（合并进下一帧、首 Hint 和解、Headless 记 `CurrentKey`、整段推迟 2m 硬吸）不变。W8 只加进程生命周期与启动覆盖，不改复制/预测。
