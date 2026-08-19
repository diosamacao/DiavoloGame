# Dedicated Server 本地启动说明（W8）

> 对照代码：`ServerLaunchConfigResolver`、`DedicatedServerBootstrap`  
> H-DS-D-1～10 已于 2026-08-19 用户验收；本文保留作出包与启动手册。

覆盖优先级：**命令行 > 环境变量 > 配置文件 > Inspector / `CreateDefault`**。解析器不打印文件正文或密钥值。

---

## 1. 命令行与环境变量

| 含义 | CLI | 环境变量 | 文件键 |
|------|-----|----------|--------|
| 监听地址 | `-actgame-bind 0.0.0.0` | `ACTGAME_BIND` | `bind` |
| 端口 | `-actgame-port 7777` | `ACTGAME_PORT` | `port` |
| 人数 | `-actgame-max-players 4` | `ACTGAME_MAX_PLAYERS` | `maxPlayers` |
| 内容版本 | `-actgame-content-version 1` | `ACTGAME_CONTENT_VERSION` | `contentVersion` |
| 空闲超时 | `-actgame-idle-timeout-ms` | `ACTGAME_IDLE_TIMEOUT_MS` | `idleTimeoutMs` |
| 心跳 | `-actgame-heartbeat-ms` | `ACTGAME_HEARTBEAT_MS` | `heartbeatIntervalMs` |
| 空 Lobby 超时（0=关） | `-actgame-empty-lobby-ms 120000` | `ACTGAME_EMPTY_LOBBY_MS` | `emptyLobbyTimeoutMs` |
| 对局结束退出 | `-actgame-exit-on-match-end 1` | `ACTGAME_EXIT_ON_MATCH_END` | `exitOnMatchEnd` |
| 配置文件路径 | `-actgame-config path.cfg` | `ACTGAME_CONFIG` | — |

CLI 也支持 `-actgame-port=7777`。布尔接受 `1/0/true/false/yes/no`。

配置文件示例见 `tools/dedicated/server.example.cfg`。`#` 或 `//` 开头为注释。`password` / `token` / `secret` / `auth` 键会被忽略。

---

## 2. 退出码

| 码 | 含义 |
|----|------|
| 0 | 正常运行、对局结束退出、空 Lobby 超时退出 |
| 10 | 配置非法或指定了配置文件但读不到 |
| 20 | 绑端口失败 |

Editor Play：**不会** `Application.Quit`，且强制 `ExitOnMatchEnd=false`、空房超时 0，最后一名离开后回到 Lobby 可再加入。

玩家 Dedicated 构建默认 `ExitOnMatchEnd=true`。需要常驻大厅时加 `-actgame-exit-on-match-end 0`。

---

## 3. Ready 日志

监听成功后打一行（烟测脚本按此匹配）：

```text
DedicatedServerBootstrap: READY port=7777 role=DedicatedServer。
```

---

## 4. Unity Editor 出包步骤（必须人工）

Agent 不得改 Build Profile / 场景 / Prefab。请你在 Editor 完成：

1. **模块**：Hub → 当前 Editor 版本 → 安装 **Windows Dedicated Server Build Support**（Linux 同理）。
2. **场景**：复制当前可玩关卡，或用同一场景。`CombatWorldController.Role` 必须是 **Dedicated Server**。不要依赖 Listen Host 场景冒充 Dedicated。
3. **Build Profile**：`File → Build Profiles` → 新增 **Windows Dedicated Server** / **Linux Dedicated Server**；Scenes 只勾 Bootstrap/Dedicated 场景。
4. **出包**：Dedicated Server subtarget 构建到例如 `Builds/Dedicated/ACTGameServer.exe`。
5. **Client 包**：另打两个 Windows Player（Role=Client，或启动后连 `127.0.0.1` + 同一端口）。
6. **启动 Server**（无 GPU / batch）：

```text
ACTGameServer.exe -batchmode -nographics -logFile - -actgame-port 7777
```

7. **烟测**（先设 exe 路径）：

```powershell
$env:ACTGAME_DEDICATED_EXE = "D:\path\to\ACTGameServer.exe"
.\tools\dedicated\smoke-ready.ps1
```

8. **对局**：两 Client 加入，移动/出招/打同一敌人；空房或结束后看 Server 退出码 0。

### 包内容自检（H-DS-D-8）

Dedicated 运行时程序集是 `ACTGame.Server`，不引用 Camera / Input / HUD / Room Facade。场景里若仍挂相机或 AudioListener，无头模式不应把它们当权威；出包后请在进程里确认没有本机玩家输入采样。完整剥离客户端资产属 Editor 裁剪，不在本波代码改资产。

---

## 5. H-DS-D 人工表（2026-08-19 已通过）

| 编号 | 操作 | 期望 |
|------|------|------|
| H-DS-D-1 | 无 GPU 启动 Dedicated Build | 出现 READY，端口正确 |
| H-DS-D-2 | 两个 Client 加入 | 均为远端；Server 无本地玩家 |
| H-DS-D-3 | 双方移动、急停、折返 | Owner 即时；权威 Pose 正确 |
| H-DS-D-4 | 连招 / 闪避 / 打敌人 | HP、Hit、Death 最终一致 |
| H-DS-D-5 | Client 改本地 HP/Pose | 下一权威状态覆盖 |
| H-DS-D-6 | Client A 断开 | A Despawn；B 与 AI 不崩 |
| H-DS-D-7 | 对局结束或空房 | Client 收到 MatchEnd；玩家构建 Server 退出码 0 |
| H-DS-D-8 | 看 Server 进程 | 无 Camera / Input / VFX 权威依赖 |
| H-DS-D-9 | 不同 Content 加入 | 被明确拒绝 |
| H-DS-D-10 | 端口被占用 | 非零退出（20）且日志有原因 |
