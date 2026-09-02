# 美术本机化 + 删库重建降 LFS 执行方案

> 制定：2026-09-02  
> 角色：**GitHub LFS 超额处置的操作真源**（单人、美术只留本机、Git 只管代码与配置）  
> 仓库：`https://github.com/diosamacao/DiavoloGame.git`  
> 当前开发分支：`NetSync`（另有 `main`、`develop`）  
> 约束：全程在**仓库副本**上改写历史；正在用的 Unity 工程与 `Assets/Art` 先整盘备份；**禁止**把抠干净的历史 force push 回旧库指望额度下降

---

## 0. 一句话

用 `git filter-repo` **一次改写全部提交**，从历史里去掉美术二进制（不必逐条检查）；把结果推到**新空库**；确认能开工程后**删除旧库**。GitHub 账号 LFS 额度这时才会掉下来。本机美术从备份拷回，靠 `.gitignore` 永远不再进 Git。

---

## 1. 目标与非目标

### 目标

- 新库几乎不再含 FBX / 贴图 / 音频等大文件（当前树 + 全部历史）。
- 代码提交故事还在（说明、作者、日期大致保留；**hash 会全部变**）。
- `.meta`、`Assets/Data`、Prefab、场景、脚本、`ProjectSettings`、`Packages` 仍在 Git。
- 本机 `Assets/Art`、`Assets/Audio` 从备份还原，Unity 引用不丢。
- 删除旧库后，GitHub LFS **存储占用降到远低于 10 GiB**。

### 非目标

- 不在 GitHub 上保留可检出的模型 / 贴图历史。
- 不保留旧库的 Issue / PR / Release / Star / Actions 记录（删库会一起没）。
- 不把额度降到「绝对 0」：误标成 LFS 的 YAML（`.prefab` / `.asset` 等）若仍走 LFS，会占很小一截；方案里会改 `.gitattributes` 避免以后再涨。

### 明确无效的做法

| 做法 | 结果 |
|------|------|
| 只删当前美术再提交 | HEAD 干净，**额度几乎不变** |
| 只加 `.gitignore` | 以后不再涨，**已占用不降** |
| 改写历史后 force push **旧库** | GitHub 仍留旧 LFS 对象，**额度通常不降** |
| 删库后再把**未抠历史**的原仓库推上去 | LFS 会涨回去 |

---

## 2. 进 Git / 只留本机

| 进新库（含历史） | 只留本机，历史里也抠掉 |
|------------------|------------------------|
| `Assets/Scripts/`、`Assets/Tests/` | `Assets/Art/**` 下 FBX / OBJ / Blend / 贴图等 |
| `Assets/Data/` | `Assets/Audio/**` 下 wav / mp3 / ogg 等 |
| `Assets/Prefabs/`、`Assets/Scenes/` | 其他目录里同后缀的二进制（见 §5 清单） |
| 上述对应的 `.meta` | |
| `Assets/Art/**` 的 `.shader` / `.hlsl` / `.cginc` / Shader Graph | |
| `ProjectSettings/`、`Packages/`、`Assets/Settings/` | |
| `Assets/Plugins/`、`Assets/MagicaCloth2/` | |
| `.cursor/`、`docs/` | |

**禁止**按后缀删除：`*.unity`、`*.prefab`、`*.asset`、`*.mat`、`*.controller`。这些是 YAML，误删等于拆掉场景、Prefab 和 Action 配置。

---

## 3. 前提与备份（第 0 天，未完成禁止往下）

在 PowerShell 中把路径换成你的真实目录。下面用占位符：

```text
WORK   = 正在用的 Unity 工程（含完整 Art）
MIRROR = 只读镜像备份（永远不要在这里跑 filter-repo）
REWRITE = 专门用来改写历史的工作副本
ARTBAK = 美术单独备份（再留一份到移动硬盘更稳）
```

### 3.1 关 Unity

改 Git / 拷贝工程期间不要开编辑器，避免 `.meta` 被重生。

### 3.2 备份正在用的工程与美术

```powershell
# 整工程打一份 zip（含 Library 也行，仅作灾难恢复；体积大）
# 至少保证 WORK 目录不要删

# 美术单独备份（必须）
robocopy "$env:WORK\Assets\Art"   "$env:ARTBAK\Art"   /E
robocopy "$env:WORK\Assets\Audio" "$env:ARTBAK\Audio" /E
```

用资源管理器确认 `ARTBAK` 里模型贴图在。这是换机 / 新 clone 后的唯一美术来源。

### 3.3 镜像备份当前远程（含全部 LFS 指针与分支）

```powershell
git clone --mirror https://github.com/diosamacao/DiavoloGame.git $env:MIRROR
```

`MIRROR` 只当档案。旧库删除后，若新库推坏了，还能从这里抢救**旧历史**（含美术指针；LFS 本体若没 `git lfs fetch --all` 则只有指针）。

若希望镜像里也有全部 LFS 二进制（很占磁盘，可选）：

```powershell
cd $env:MIRROR
git lfs fetch --all
```

额度已经爆了时，`lfs fetch --all` 可能失败或下不完。不影响后续「抠历史推新库」：改写只需要 Git 指针，不需要把 10GB+ 都拉齐。

### 3.4 确认本机分支齐

在 `WORK` 里：

```powershell
cd $env:WORK
git fetch --all
git branch -a
```

至少要有：`NetSync`、`main`、`develop`。本地有未推送提交先推到**旧库**或 `git stash` / 另开补丁备份，避免改写时丢掉。

### 3.5 安装工具

```powershell
python --version          # 需要 Python 3
pip install git-filter-repo
git filter-repo --version
git lfs version
```

---

## 4. 阶段总览

```text
A  克隆 REWRITE 工作副本（从 MIRROR 或旧 origin）
B  分析体积（可选，建议做）
C  filter-repo 按 glob 抠掉美术二进制（全部提交一次完成）
D  补 .gitignore + 收窄 .gitattributes
E  自检：当前树与历史上不应再有 fbx/png/wav
F  GitHub 新建空仓库，推送 REWRITE 的全部分支与 tag
G  用新库试 clone：应很小；再把 ARTBAK 拷进 Assets
H  本机 WORK 改 remote 指向新库，Art 保持不动
I  确认无误后删除 GitHub 旧库 → LFS 额度下降
```

预计耗时：备份与拷贝视过程体积；`filter-repo` 对「提交多、大文件已是 LFS 指针」通常几分钟到几十分钟；推新库若只剩文本会很快。

---

## 5. 阶段 A–C：改写历史

### 5.1 建工作副本

```powershell
git clone $env:MIRROR $env:REWRITE
cd $env:REWRITE
git checkout NetSync
```

之后所有破坏性命令只在 `REWRITE` 里执行。

### 5.2 分析（建议）

```powershell
git filter-repo --analyze
```

打开：

```text
.git/filter-repo/analysis/path-all-sizes.txt
```

按体积扫：漏网的 `.fbx` / `.png` / `.wav` / 字体 / 视频补进下一节 glob。  
`--analyze` 会动仓库元数据；若提示必须加 `--force`，确认当前就是 `REWRITE` 再加。

### 5.3 一次抠掉全部历史中的美术二进制

不必打开任何一个旧提交。下面覆盖当前 `.gitattributes` 里的大文件类型，并限制在 Art / Audio；同时用一批全库后缀兜底（字体、视频、压缩包也常进 LFS）。

在 `REWRITE` 根目录执行（PowerShell 续行用反引号）：

```powershell
git filter-repo --force --invert-paths `
  --path-glob 'Assets/Art/**/*.fbx' `
  --path-glob 'Assets/Art/**/*.FBX' `
  --path-glob 'Assets/Art/**/*.obj' `
  --path-glob 'Assets/Art/**/*.OBJ' `
  --path-glob 'Assets/Art/**/*.blend' `
  --path-glob 'Assets/Art/**/*.dae' `
  --path-glob 'Assets/Art/**/*.png' `
  --path-glob 'Assets/Art/**/*.PNG' `
  --path-glob 'Assets/Art/**/*.jpg' `
  --path-glob 'Assets/Art/**/*.jpeg' `
  --path-glob 'Assets/Art/**/*.psd' `
  --path-glob 'Assets/Art/**/*.tga' `
  --path-glob 'Assets/Art/**/*.tif' `
  --path-glob 'Assets/Art/**/*.tiff' `
  --path-glob 'Assets/Art/**/*.gif' `
  --path-glob 'Assets/Art/**/*.bmp' `
  --path-glob 'Assets/Art/**/*.exr' `
  --path-glob 'Assets/Art/**/*.hdr' `
  --path-glob 'Assets/Audio/**/*.wav' `
  --path-glob 'Assets/Audio/**/*.mp3' `
  --path-glob 'Assets/Audio/**/*.ogg' `
  --path-glob 'Assets/Audio/**/*.aif' `
  --path-glob 'Assets/Audio/**/*.aiff' `
  --path-glob '*.fbx' `
  --path-glob '*.FBX' `
  --path-glob '*.png' `
  --path-glob '*.PNG' `
  --path-glob '*.jpg' `
  --path-glob '*.jpeg' `
  --path-glob '*.tga' `
  --path-glob '*.psd' `
  --path-glob '*.exr' `
  --path-glob '*.hdr' `
  --path-glob '*.wav' `
  --path-glob '*.mp3' `
  --path-glob '*.ogg' `
  --path-glob '*.mp4' `
  --path-glob '*.mov' `
  --path-glob '*.avi' `
  --path-glob '*.unitypackage' `
  --path-glob '*.zip'
```

说明：

- `--invert-paths` = 保留**未**匹配的路径，匹配到的从**每一个提交**删除。
- 全库 `*.png` 会一并去掉 `Assets/Resources` 等处的图。若某张 UI 图必须进库，先从 `path-all-sizes.txt` 挑出来，不要用这条全库规则，改为只删 `Assets/Art/**`。
- **不要**加 `*.asset` / `*.prefab` / `*.unity` / `*.mat` / `*.controller`。
- `.meta` 不在列表里，会留下。

跑完后 `origin` 会被 `filter-repo` 去掉，这是正常的。

---

## 6. 阶段 D：防止以后再进库

仍在 `REWRITE` 的当前分支（建议 `NetSync`）改两个文件，再合并进你还要保留的分支，或在各分支各提交一次。最简单：先只在 `NetSync` 提交，`main` / `develop` 若还要长期用，把同一改动 cherry-pick 过去。

### 6.1 `.gitignore` 追加

```gitignore
# 美术 / 音频只留本机；.meta 与 shader 源码仍进库
Assets/Art/**
!Assets/Art/**/
!Assets/Art/**/*.meta
!Assets/Art/**/*.shader
!Assets/Art/**/*.hlsl
!Assets/Art/**/*.cginc
!Assets/Art/**/*.shadersubgraph
!Assets/Art/**/*.shadergraph

Assets/Audio/**
!Assets/Audio/**/
!Assets/Audio/**/*.meta
```

`!**/` 是为了让目录存在，`.meta` 例外才能生效。

### 6.2 收窄 `.gitattributes`

从 LFS 规则中**删除**（这些是文本，不该占 LFS）：

```text
*.unity
*.prefab
*.asset
*.mat
*.controller
*.playable
*.mask
*.cubemap
*.flare
*.rendertexture
*.lighting
*.terrainlayer
```

**保留**模型 / 贴图 / 音频 / 视频的 LFS 行：作为保险。文件已被 ignore，正常不会再 `add`；万一误加，仍走 LFS 而不是撑爆普通 Git 的 100MB 限制。

若希望新库 LFS 用量接近 0，可在改完 attributes 后，把历史上误标的 YAML 从 LFS 导出成普通 Git 对象（可选，在 `REWRITE`）：

```powershell
git lfs migrate export --everything --include="*.unity,*.prefab,*.asset,*.mat,*.controller,*.playable,*.mask"
```

导出后这些文件会变成普通 blob，新库推送不再走 LFS。体积通常仍远小于原来的美术。

### 6.3 提交

```powershell
git add .gitignore .gitattributes
git status
git commit -m "Keep art binaries local; stop LFS on Unity YAML."
```

确认 `git status` **没有**把本机 Art 大文件加进来。`REWRITE` 里此时不应再有那些二进制。

---

## 7. 阶段 E：自检（未通过不要推）

```powershell
git checkout NetSync
git log --oneline -20

# 历史上不应再出现这些实体文件
git log --all --full-history -- "*.fbx"
git log --all --full-history -- "*.png"
git log --all --full-history -- "*.wav"

# 当前索引
git ls-files "*.fbx"
git ls-files "*.png"

# 还在的应是代码 / meta / data / prefab
git ls-files "Assets/Art/**/*.meta" | Select-Object -First 20
git ls-files "Assets/Scripts/**/*.cs" | Select-Object -First 10
```

`git log -- "*.fbx"` 应为空（或只有「删除」类改写痕迹，取决于工具版本；`git ls-files "*.fbx"` 必须为空）。

再看体积（可选）：

```powershell
git rev-list --objects --all |
  git cat-file --batch-check="%(objecttype) %(objectname) %(objectsize) %(rest)" |
  Where-Object { $_ -match '^blob' } |
  Sort-Object { [int64]($_ -split ' ')[2] } -Descending |
  Select-Object -First 30
```

不应再出现几十 MB 的模型 / 贴图。

---

## 8. 阶段 F：新库推送

1. 浏览器打开 GitHub → New repository。  
2. 不要勾 README / `.gitignore` / License（必须是空库）。  
3. 建议新名字仍用 `DiavoloGame`：**只有删掉旧库之后才能重用同名**。在旧库还在时，先建 `DiavoloGame-code` 或同类临时名，删旧库后再改名。  
4. 在 `REWRITE`：

```powershell
git remote add origin https://github.com/diosamacao/<新库名>.git
git push -u origin --all
git push origin --tags
```

若提示 LFS 超额，说明改写不干净或还在推旧对象。回到 §5.2 / §7 查漏，不要对旧库 force push。

推完后在 GitHub 打开若干旧提交：应能看到当年的脚本 diff，打开 `Assets/Art` 应只有 `.meta` / shader 或空目录。

---

## 9. 阶段 G–H：本机工程接上新库

### 9.1 干净 clone 冒烟（推荐另开文件夹）

```powershell
git clone https://github.com/diosamacao/<新库名>.git $env:SMOKE
cd $env:SMOKE
git checkout NetSync
```

预期：clone 很快、体积小、没有 FBX。此时**先不要开 Unity**。

把美术拷回去（路径必须一致）：

```powershell
robocopy "$env:ARTBAK\Art"   "$env:SMOKE\Assets\Art"   /E
robocopy "$env:ARTBAK\Audio" "$env:SMOKE\Assets\Audio" /E
```

再开 Unity。角色 / 场景不应大面积 Missing。  
`git status` 不应把 Art 二进制列成待提交（ignore 生效）。允许出现少量 Unity 生成的忽略文件。

### 9.2 正在用的 `WORK`

不要在 `WORK` 上跑 filter-repo。只改远端：

```powershell
cd $env:WORK
git remote -v
git remote set-url origin https://github.com/diosamacao/<新库名>.git
git fetch origin
```

注意：`WORK` 仍是**旧 hash**。两条路选一条：

- **稳妥：** 以后以 `SMOKE`（新历史 + 已拷美术）作为日常工程，`WORK` 整夹封存。  
- **续用 WORK：** 不能 `git pull` 对齐新历史（无关历史）。需要把 `WORK` 当「有 Art 的工作树」，用补丁把未推送的代码改动迁到 `SMOKE`。未完成的本地修改用 `git diff` / 补丁文件转移，不要 merge 旧远程。

推荐：**日常改用 SMOKE**，避免两套历史缠在一起。

---

## 10. 阶段 I：删旧库（额度在这里下降）

全部通过后再做：

1. GitHub → 旧 `DiavoloGame` → Settings → Delete this repository。  
2. 需要的话把新库改名为 `DiavoloGame`，本地 `git remote set-url` 同步。  
3. 账号 Settings → Billing → Git LFS 存储应在一段时间后反映下降（通常删库后较快，页面有缓存则隔天再看）。

未确认 SMOKE 能开 Unity 之前，**不要删旧库**。

---

## 11. 协作者（若有）

旧 clone 全部作废。通知对方：

1. 备份自己的 `Assets/Art`。  
2. 删掉旧目录，从新库 clone。  
3. 把美术拷到相同路径。  
4. 不要 `git pull` 旧仓库。

---

## 12. 回滚

| 失败点 | 做法 |
|--------|------|
| `filter-repo` 规则错了 | 丢掉 `REWRITE`，从 `MIRROR` 再 clone 一份重跑 |
| 新库推上去但缺代码 / meta | 不要删旧库；修 `REWRITE` 或从 `MIRROR` 重来 |
| Unity Missing 且 `.meta` 被重生 | 关掉 Unity，用 `ARTBAK` + `REWRITE` 里的 `.meta` 覆盖，勿用编辑器「修复」出来的新 GUID |
| 误删旧库且新库不可用 | 用 `MIRROR` 重建 GitHub 旧库（LFS 会涨回；若当时没 `lfs fetch --all`，大文件可能只有指针） |

---

## 13. 执行勾选

- [ ] Unity 已关  
- [ ] `ARTBAK` 有完整 Art / Audio  
- [ ] `MIRROR` clone --mirror 完成  
- [ ] 本地未推送提交已另存  
- [ ] `REWRITE` 上 `filter-repo` 完成  
- [ ] `git ls-files "*.fbx"` 为空  
- [ ] `.gitignore` / `.gitattributes` 已提交  
- [ ] 新空库 `--all` + tags 推送成功  
- [ ] 干净 clone + 拷美术后 Unity 可玩、无大面积 Missing  
- [ ] `git status` 不再跟踪美术二进制  
- [ ] 删除 GitHub 旧库  
- [ ] Billing 里 LFS 存储已下降  

---

## 14. 风险摘要

- **hash 全变**，旧 PR 链接、旧 submodule 指针失效。  
- **删库不可逆**地带走 Issue / PR。  
- 全库 `*.png` 会去掉非 Art 目录的图；UI 若必须进库，改用更窄 glob。  
- 新库若误 `git add -A` 且 ignore 未生效，LFS 会再次上涨。  
- GitHub 免费 LFS：**存储 10 GiB + 带宽 10 GiB/月**。本次降的是存储；大克隆仍耗带宽（新库应很小）。
