# 美术本机化 + 删库重建降 LFS 执行方案

> 制定：2026-09-02  
> 修订：2026-09-06 — **A 定案**：Art / Audio / Resources 二进制只留本机；新库禁用 LFS（导出 YAML 为必做，不是可选）  
> 角色：**GitHub LFS 超额处置的操作真源**（单人、美术只留本机、Git 只管代码与配置）  
> 仓库：`https://github.com/diosamacao/DiavoloGame.git`  
> 当前开发分支：`NetSync`（远程另有 `main`、`develop`；本机可能还有 `master`）  
> 约束：全程在**仓库副本**上改写历史；WORK 与本机二进制先整盘备份；**禁止**把抠干净的历史 force push 回旧库指望额度下降

---

## 0. 一句话

用 `git filter-repo` **一次抠掉全部历史里的模型 / 贴图 / 音频**，把剩余 YAML **导出成普通 Git 并卸载 LFS**，推到**新空库**；SMOKE 从备份还原 Art / Audio / Resources 并能开工程后，**删除旧库**。禁止双轨（旧库继续收 LFS + 新库再传 LFS），禁止 Resources 进 Git、Art 留本机的半套策略。

---

## 1. 问题与动机

### 1.1 现状基线（2026-09-06 本机 HEAD）

```text
账号 LFS 配额（GitHub Free）= 存储 10 GiB + 带宽 10 GiB/月（账号级，不是单库）
    → 旧库历史里的 LFS 对象一直占存储
    → 只改当前树 / 只加 .gitignore / force push 旧库：额度几乎不降

当前 LFS 跟踪（约 4100+ 文件）
    Assets/Art          ~7.6 GB / 1799 文件   ← 主因
    Assets/Resources    ~407 MB / 1892 文件   ← 刀光 / 特效贴图，比 Art 文件数还多
    Assets/Audio        ~191 MB / 201 文件
    Assets/MagicaCloth2 ~145 MB / 104 文件    ← 含 Example 与 Res/Icon
    Assets/Data 等      很小 / .asset .prefab .mat .unity 被误标 LFS
```

| 点 | 现状 |
|----|------|
| `.gitattributes` | 模型 / 贴图 / 音频 **以及** `.unity` / `.prefab` / `.asset` / `.mat` / `.controller` 全走 LFS |
| `.gitignore` | 标准 Unity 忽略；**不**忽略 Art / Audio / Resources 二进制 |
| 远程分支 | `origin/NetSync`、`origin/develop`、`origin/main`（`HEAD → main`） |
| 本机分支 | 常见：`NetSync`、`develop`、`master`（未必有本地 `main`） |

### 1.2 痛点

1. 账号 LFS 已满或将满：不能再推大文件，clone 也费带宽。  
2. GitHub **不会**因改写历史或 `git rm` 自动删服务器上的旧 LFS 对象。  
3. 月中删对象 **不重算当月 storage**；超额且预算 $0 时，当月可能整月禁推 LFS。新库若还走 LFS，会卡死。  
4. Resources 特效与 Art 是同一类「换机靠拷贝」的二进制；只备份 Art 会开工程 Missing。

### 1.3 目标

| 目标 | 说明 |
|------|------|
| 历史 | 新库全部提交不再含 FBX / 贴图 / 音频等大文件 |
| 提交故事 | 说明 / 作者 / 日期大致保留；**hash 全部变** |
| 进 Git | 脚本、测试、`Assets/Data`、Prefab、场景、`.meta`、Art 下 YAML/着色器、`ProjectSettings`、`Packages`、插件代码 |
| 只留本机 | `Assets/Art`、`Assets/Audio`、`Assets/Resources` 下的模型 / 贴图 / 音频（**A 定案**） |
| 新库 LFS | **用量为 0**：推送不得触发 LFS 上传 |
| 不做 | 不保留可检出的模型/贴图历史；不保留旧库 Issue / PR / Release / Star / Actions；不把 Resources 留在 Git（B 已否决）；不保留「YAML 继续走 LFS」的双轨 |

### 1.4 明确无效的做法

| 做法 | 结果 |
|------|------|
| 只删当前美术再提交 | HEAD 干净，**额度几乎不变** |
| 只加 `.gitignore` | 以后不再涨，**已占用不降** |
| 改写历史后 force push **旧库** | GitHub 仍留旧 LFS 对象，**额度通常不降** |
| 删库后再把**未抠历史**的原仓库推上去 | LFS 会涨回去 |
| 新库仍推 LFS（含 `.prefab` / `.asset`） | 账号已满则 **拒推**；旧库未删则配额叠加 |
| 只还原 Art / Audio，不还原 Resources | 刀光 / 特效大面积 Missing |

---

## 2. 设计原则

1. **A 为唯一策略**：凡 `.gitattributes` 里的模型 / 贴图 / 音频后缀，**全库**（含 `Resources`、MagicaCloth Example）历史里抠掉、本机用备份还原；不另开「特效进库」旁路。  
2. **新库禁用 LFS**：剩余 YAML 必须 `lfs migrate export` 成普通 blob，然后 `git lfs uninstall`。保险靠 ignore + GitHub 单文件 100 MB 限制，不靠「误加走 LFS」。  
3. **GUID 不断**：`.meta` 留在 Git；拷回二进制必须同路径；拷完之前禁止开 Unity。  
4. **YAML 进库**：禁止按后缀删除 `*.unity` / `*.prefab` / `*.asset` / `*.mat` / `*.controller` / `*.anim` / `*.mask` / `*.playable`。  
5. **零双轨**：不保留旧远程当「还能 push 美术」的备用；日常只认 SMOKE + 新 origin。  
6. **破坏性命令只在 REWRITE**：MIRROR 只读；WORK 不跑 `filter-repo`。

---

## 3. 目标架构

```text
WORK（旧 hash，含完整二进制）     ARTBAK（Art+Audio+Resources+MagicaCloth Icon）
        │                                    │
        │ 只读取 LFS 缓存供导出               │ SMOKE 还原（开 Unity 之前）
        ▼                                    ▼
MIRROR（--mirror 档案） ──clone──► REWRITE
                              filter-repo 抠二进制
                              lfs export YAML → 卸载 LFS
                              提交 .gitignore / .gitattributes
                              提交 MagicaCloth Res/Icon（普通 Git）
                                    │
                                    ▼
                              新空库 origin（无 LFS 对象）
                                    │
                         SMOKE 验证通过后删除旧库
```

### 3.1 进 Git / 只留本机（A）

| 进新库（含历史，普通 Git） | 只留本机（历史里抠掉，ignore 防再 add） |
|----------------------------|----------------------------------------|
| `Assets/Scripts/`、`Assets/Tests/` | `Assets/Art/**` 下 FBX / OBJ / Blend / 贴图等 |
| `Assets/Data/` | `Assets/Audio/**` 下 wav / mp3 / ogg 等 |
| `Assets/Prefabs/`、`Assets/Scenes/` | `Assets/Resources/**` 下同后缀二进制（特效贴图 / 网格） |
| 上述对应的 `.meta` | MagicaCloth **Example** 下的 FBX / 贴图（不还原也可玩） |
| Art / Resources 下的 `.anim` / `.mat` / `.controller` / `.overrideController` / `.mask` / `.playable` / shader / Shader Graph | |
| `ProjectSettings/`、`Packages/`、`Assets/Settings/` | |
| `Assets/Plugins/` 代码与设置 | |
| MagicaCloth **插件代码** + `Res/Icon/*.png`（体积可忽略，**进库**） | |
| `.cursor/`、`docs/` | |

**禁止**按后缀删除：`*.unity`、`*.prefab`、`*.asset`、`*.mat`、`*.controller`、`*.anim`、`*.mask`、`*.playable`、`*.inputactions`。

**唯一二进制进库例外：** `Assets/MagicaCloth2/Res/Icon/**/*.png`。filter-repo 会先删掉它们，LFS-2 从 ARTBAK 拷回并提交。没有这几张图，MagicaCloth Inspector 图标会丢；不把整个 MagicaCloth Example 重新进库。

### 3.2 边界

| 层 | 职责 | 不负责 |
|----|------|--------|
| MIRROR | 旧历史档案 | 不在此改写 |
| REWRITE | 改写、去 LFS、推新库 | 不当日常工程 |
| ARTBAK | 换机 / SMOKE 的唯一二进制来源 | 不进 Git |
| SMOKE | 日常工程（新历史 + 已还原二进制） | 不 `pull` 旧库 |
| WORK | 封存；未提交补丁迁到 SMOKE | 不跑 filter-repo、不 merge 新历史 |

---

## 4. 路径占位（PowerShell）

把路径换成真实目录。改 Git / 拷贝期间 **关 Unity**。

```text
WORK    = 正在用的 Unity 工程（含完整二进制）
MIRROR  = 只读镜像备份（永远不要在这里跑 filter-repo）
REWRITE = 改写历史的工作副本
ARTBAK  = 二进制备份（再拷一份到移动硬盘更稳）
SMOKE   = 从新库干净 clone 的日常工程
```

先赋值再跑后文：

```powershell
$env:WORK    = "D:\Projects\ACTGame"
$env:MIRROR  = "D:\Backup\DiavoloGame.mirror.git"
$env:REWRITE = "D:\Backup\DiavoloGame.rewrite"
$env:ARTBAK  = "D:\Backup\ACTGame-artbak"
$env:SMOKE   = "D:\Projects\ACTGame-code"
```

---

## 5. 分阶段交付（任务 / 验收 / 出口）

> 勾选：未开始 `[ ]`；完成后 `[x]` 并在出口注明日期。未达成本阶段出口 **禁止** 进入下一阶段。

### LFS-0 — 备份与齐套

**任务**

- [ ] 关闭所有 Unity / Hub 打开的本工程。  
- [ ] 备份本机二进制（必须；Resources 不可省）：

```powershell
robocopy "$env:WORK\Assets\Art"   "$env:ARTBAK\Art"   /E
robocopy "$env:WORK\Assets\Audio" "$env:ARTBAK\Audio" /E
robocopy "$env:WORK\Assets\Resources" "$env:ARTBAK\Resources" /E
robocopy "$env:WORK\Assets\MagicaCloth2\Res\Icon" "$env:ARTBAK\MagicaCloth2\Res\Icon" /E
```

- [ ] 资源管理器目视：`ARTBAK\Art` 有 FBX/贴图，`ARTBAK\Audio` 有 wav（含中文文件名），`ARTBAK\Resources` 有特效贴图，`ARTBAK\MagicaCloth2\Res\Icon` 有 png。  
- [ ] 镜像旧远程（指针即可；**不要**为继续往下而 `lfs fetch --all`）：

```powershell
git clone --mirror git@github.com:diosamacao/DiavoloGame.git $env:MIRROR
```

- [ ] 在 WORK：`git fetch --all`；未推送提交做成补丁文件另存（`git diff` / `git format-patch`），或先推到**旧库**。  
- [ ] 安装：`python` 3、`pip install git-filter-repo`、`git filter-repo --version`、`git lfs version`。

**验收**

- [ ] `ARTBAK` 四棵目录都非空；Resources 下能看到 `FX_Slash_Collection` / `Hovl Studio` / `OrdosFX` 一类贴图，不是只有 `.meta`。  
- [ ] `MIRROR` 为 bare：`git --git-dir=$env:MIRROR branch -a` 能看到 `NetSync`、`develop`、`main`。  
- [ ] 本机未提交改动已有补丁或已进旧库，REWRITE 丢了也不丢活改动。

**出口：** 二进制与旧历史都有独立副本，工具已装。→ **未达成**

---

### LFS-1 — filter-repo 抠掉全库美术二进制

**任务**

- [ ] 从 MIRROR 建 REWRITE，并先把要保留的远程分支全部变成**本地分支**（否则 `push --all` 会漏 `main` / `develop`）：

```powershell
git clone $env:MIRROR $env:REWRITE
cd $env:REWRITE
foreach ($b in @("NetSync", "develop", "main", "master")) {
  if (git show-ref --verify --quiet "refs/remotes/origin/$b") {
    git checkout -B $b "origin/$b"
  }
}
git checkout NetSync
git branch
```

- [ ] 分析体积（`--analyze` 后若要求 `--force`，确认 cwd 是 REWRITE 再加）：

```powershell
git filter-repo --analyze
# 打开 .git/filter-repo/analysis/path-all-sizes.txt
# 仍占体积的 .fbx/.png/.wav/.tif/.obj/.ttf 等，补进下面 glob 后重来
```

- [ ] 一次抠掉全部提交中的二进制（全库后缀兜底 = A；PowerShell 续行用反引号）。**不要**加 `*.unity` / `*.prefab` / `*.asset` / `*.mat` / `*.controller` / `*.anim`。

```powershell
git filter-repo --force --invert-paths `
  --path-glob '*.fbx' --path-glob '*.FBX' `
  --path-glob '*.obj' --path-glob '*.OBJ' `
  --path-glob '*.blend' --path-glob '*.dae' `
  --path-glob '*.png' --path-glob '*.PNG' `
  --path-glob '*.jpg' --path-glob '*.jpeg' `
  --path-glob '*.psd' --path-glob '*.tga' `
  --path-glob '*.tif' --path-glob '*.tiff' `
  --path-glob '*.gif' --path-glob '*.bmp' `
  --path-glob '*.exr' --path-glob '*.hdr' `
  --path-glob '*.wav' --path-glob '*.mp3' --path-glob '*.ogg' `
  --path-glob '*.aif' --path-glob '*.aiff' `
  --path-glob '*.mp4' --path-glob '*.mov' --path-glob '*.avi' `
  --path-glob '*.ttf' --path-glob '*.otf' `
  --path-glob '*.unitypackage' --path-glob '*.zip'
```

`--invert-paths` = 删掉匹配路径、保留其余。`.meta` 不在列表里，会留下。跑完后 `origin` 消失是正常的。

**验收**

- [ ] `git branch` 仍有 `NetSync`，且 `main` / `develop` 凡远程曾存在的都还在本地。  
- [ ] `git ls-files "*.fbx"`、`git ls-files "*.png"`、`git ls-files "*.wav"` 均为空。  
- [ ] `git ls-files "Assets/Art/**/*.meta"`、`git ls-files "Assets/Scripts/**/*.cs"`、`git ls-files "Assets/Data/**/*.asset"` 非空。  
- [ ] `path-all-sizes.txt` 里原先靠前的模型 / 贴图已不在当前树。

**出口：** 全部本地分支的历史里已无美术二进制，YAML / 脚本 / meta 仍在。→ **未达成**

---

### LFS-2 — 去 LFS 化 + ignore（必做）

依赖 LFS-1。额度已满时，**不做本阶段就不要推新库**。

**任务**

- [ ] 把 WORK 里已有的 LFS 对象缓存拷进 REWRITE（只需保留文件的本体；不要 `lfs fetch --all`）：

```powershell
Copy-Item "$env:WORK\.git\lfs" "$env:REWRITE\.git\lfs" -Recurse -Force
```

- [ ] 导出剩余 LFS（场景 / Prefab / Data / 材质 / Animator 等）为普通 Git 对象：

```powershell
cd $env:REWRITE
git lfs migrate export --everything --include="*.unity,*.prefab,*.asset,*.mat,*.controller,*.playable,*.mask,*.cubemap,*.flare,*.rendertexture,*.lighting,*.terrainlayer"
```

- [ ] **删除** `.gitattributes` 里全部 `filter=lfs` 行（含模型 / 贴图 / 音频行）。保留文本声明（`*.cs` / `*.shader` / `*.meta` 等）即可。  
- [ ] `git lfs uninstall`。  
- [ ] `.gitignore` 追加下面整块（Art / Audio / Resources 二进制只留本机；YAML 与着色器放行）：

```gitignore
# 美术 / 音频 / Resources 特效只留本机；.meta 与 YAML / shader 仍进库
Assets/Art/**
!Assets/Art/**/
!Assets/Art/**/*.meta
!Assets/Art/**/*.shader
!Assets/Art/**/*.hlsl
!Assets/Art/**/*.cginc
!Assets/Art/**/*.shadersubgraph
!Assets/Art/**/*.shadergraph
!Assets/Art/**/*.anim
!Assets/Art/**/*.controller
!Assets/Art/**/*.overrideController
!Assets/Art/**/*.mask
!Assets/Art/**/*.mat
!Assets/Art/**/*.playable
!Assets/Art/**/*.inputactions

Assets/Audio/**
!Assets/Audio/**/
!Assets/Audio/**/*.meta

Assets/Resources/**
!Assets/Resources/**/
!Assets/Resources/**/*.meta
!Assets/Resources/**/*.prefab
!Assets/Resources/**/*.asset
!Assets/Resources/**/*.mat
!Assets/Resources/**/*.controller
!Assets/Resources/**/*.overrideController
!Assets/Resources/**/*.anim
!Assets/Resources/**/*.mask
!Assets/Resources/**/*.playable
!Assets/Resources/**/*.unity
!Assets/Resources/**/*.shader
!Assets/Resources/**/*.hlsl
!Assets/Resources/**/*.cginc
!Assets/Resources/**/*.shadergraph
!Assets/Resources/**/*.shadersubgraph
!Assets/Resources/**/*.inputactions

# MagicaCloth：示例资源不进库；插件图标进库
Assets/MagicaCloth2/**/*.fbx
Assets/MagicaCloth2/**/*.FBX
Assets/MagicaCloth2/**/*.png
Assets/MagicaCloth2/**/*.PNG
Assets/MagicaCloth2/**/*.jpg
Assets/MagicaCloth2/**/*.jpeg
!Assets/MagicaCloth2/Res/Icon/**/*.png
```

`!**/` 用来放行目录，否则后面的 `!*.meta` 不生效。

- [ ] 从 ARTBAK 拷回 MagicaCloth 图标并加入版本库：

```powershell
robocopy "$env:ARTBAK\MagicaCloth2\Res\Icon" "$env:REWRITE\Assets\MagicaCloth2\Res\Icon" /E
git add -f "Assets/MagicaCloth2/Res/Icon"
```

- [ ] 在**每一个**还要长期用的本地分支提交同一改动（不要只改 `NetSync`）：

```powershell
git add .gitignore .gitattributes
git status   # 不得出现 .fbx / .png / .wav（Icon 除外）
git commit -m "Keep art and VFX binaries local; stop Git LFS."
# 对其余分支 cherry-pick 该提交
```

**验收**

- [ ] `git lfs ls-files` 在每个长期分支上都为空。  
- [ ] 工作区与索引无 `oid sha256:` / `git-lfs.github.com` 指针（可用 `git grep -n "git-lfs.github.com"`，应无匹配）。  
- [ ] `.gitattributes` 无 `filter=lfs`。  
- [ ] `git ls-files "Assets/MagicaCloth2/Res/Icon/*.png"` 非空。  
- [ ] `git status` 不把 Art / Audio / Resources 二进制列为新文件。

**出口：** 新库推送路径上不再存在任何 LFS 对象。→ **未达成**

---

### LFS-3 — 体积与历史自检

依赖 LFS-2。未通过 **不要** 建 GitHub 空库。

**任务**

- [ ] 在 `NetSync` 上跑：

```powershell
git checkout NetSync
git log --oneline -20
git log --all --full-history -- "*.fbx"
git log --all --full-history -- "*.png"
git log --all --full-history -- "*.wav"
git ls-files "*.fbx"
git ls-files "*.png"
git ls-files "Assets/Art/**/*.meta" | Select-Object -First 20
git ls-files "Assets/Resources/**/*.meta" | Select-Object -First 20
git ls-files "Assets/Scripts/**/*.cs" | Select-Object -First 10
```

- [ ] 看当前库最大 blob（不应再有几十 MB 模型 / 贴图）：

```powershell
git rev-list --objects --all |
  git cat-file --batch-check="%(objecttype) %(objectname) %(objectsize) %(rest)" |
  Where-Object { $_ -match '^blob' } |
  Sort-Object { [int64]($_ -split ' ')[2] } -Descending |
  Select-Object -First 30
```

**验收**

- [ ] `git ls-files` 对 `*.fbx` / `*.png` / `*.wav` 为空（MagicaCloth Icon 的 png 除外；若 `ls-files "*.png"` 只有 `Assets/MagicaCloth2/Res/Icon/` 下文件，算通过）。  
- [ ] 最大 blob 为脚本 / YAML / meta 量级，不是 FBX。  
- [ ] `git lfs ls-files` 仍为空。

**出口：** 自检通过，可以推新空库。→ **未达成**

---

### LFS-4 — 推新空库

**任务**

- [ ] GitHub → New repository；**不要**勾 README / `.gitignore` / License。  
- [ ] 旧库还在时不能复用 `DiavoloGame`：先建 `DiavoloGame-code`（或同类临时名）。  
- [ ] 在 REWRITE：

```powershell
git remote add origin git@github.com:diosamacao/<新库名>.git
git push -u origin --all
git push origin --tags
```

推送过程 **不得** 出现 `Uploading LFS objects` / `batch response` / 超额错误。若出现：停，回 LFS-2 / LFS-3，**不要**对旧库 force push。

**验收**

- [ ] GitHub 上 `NetSync`、`develop`、`main`（凡推了的）都能打开。  
- [ ] 任意旧提交能看到脚本 diff；`Assets/Art`、`Assets/Resources` 只有 `.meta` / YAML / shader，没有 FBX/贴图。  
- [ ] 新库 Settings 无有效 LFS 用量（或 LFS 文件数为 0）。

**出口：** 新空库已含改写后的全部分支与 tag，且未上传 LFS。→ **未达成**

---

### LFS-5 — SMOKE 还原并开工程

依赖 LFS-4。未完成 **不要** 删旧库。

**任务**

- [ ] 另开目录干净 clone（先不要开 Unity）：

```powershell
git clone git@github.com:diosamacao/<新库名>.git $env:SMOKE
cd $env:SMOKE
git checkout NetSync
```

- [ ] 按**相同相对路径**还原二进制：

```powershell
robocopy "$env:ARTBAK\Art"   "$env:SMOKE\Assets\Art"   /E
robocopy "$env:ARTBAK\Audio" "$env:SMOKE\Assets\Audio" /E
robocopy "$env:ARTBAK\Resources" "$env:SMOKE\Assets\Resources" /E
```

MagicaCloth Icon 已在库内，不必从 ARTBAK 再盖一层（盖了也应是同一批文件）。

- [ ] 再开 Unity。首次导入 Art 可能要很久，不要中断、不要让 Editor「修复」出新 GUID。  
- [ ] `git status`：Art / Audio / Resources 二进制不应出现在待提交；允许少量 Unity 生成的已忽略文件。  
- [ ] **日常改用 SMOKE**。WORK 整夹封存。不要在 WORK 上 `git pull` 新历史（无关历史）。WORK 里未进库的代码用补丁迁到 SMOKE。

**验收**

- [ ] clone 很快、工作树无 FBX（还原前）。  
- [ ] Play：角色网格 / 材质 / 刀光与受击特效能出来，无大面积 Missing。  
- [ ] MagicaCloth 组件 Inspector 图标正常。  
- [ ] `git status` 不跟踪美术 / 特效 / 音频二进制。

**出口：** SMOKE 可当日常工程，引用未因 GUID 断裂。→ **未达成**

---

### LFS-6 — 删除旧库

依赖 LFS-5。

**任务**

- [ ] GitHub → 旧 `DiavoloGame` → Settings → Delete this repository。  
- [ ] 需要的话把新库改名为 `DiavoloGame`，SMOKE 上 `git remote set-url` 同步。  
- [ ] 若有协作者：通知旧 clone 作废；对方自备 Art / Audio / Resources，从新库 clone 后再拷，禁止 `git pull` 旧仓库。

**验收**

- [ ] 旧库 404。  
- [ ] SMOKE 仍指向新库且 `git fetch` 正常。  
- [ ] **不要**用「Billing 当天归零」当通过条件。现行计量下，月中删除 **不重算当月 storage**，数字可能要到下个计费周期才好看；带宽用过不退。通过 = 旧库已删 + 新库无 LFS 对象。

**出口：** 旧 LFS 对象随旧库删除；日常只走新库。→ **未达成**

---

## 6. 迁移与删除

### 6.1 保留 / 迁入

- 提交说明、作者、日期（hash 变）。  
- 脚本、文档、`.cursor`、Data / Prefab / 场景 / ProjectSettings。  
- Art / Resources 下已跟踪的 `.anim` / `.mat` / `.controller` / shader / `.meta`。  
- MagicaCloth 插件代码 + `Res/Icon`。  
- WORK 未推送改动：补丁 → SMOKE。

### 6.2 明确删除

| 删除 | 原因 |
|------|------|
| 历史中全部模型 / 贴图 / 音频（全库后缀） | A：本机化；降 LFS |
| 旧 GitHub 库（含 Issue / PR / Release / Actions） | 官方清 LFS 对象的途径 |
| `.gitattributes` 全部 `filter=lfs` | 新库禁用 LFS，避免额度再封推送 |
| 本机 WORK 作为日常远程 | 旧 hash 与新历史不能 pull 对齐 |
| 「Resources 继续进 Git」的 B 方案 | 与 A 互斥，不留旁路 |
| 「YAML 仍走 LFS 当保险」 | 超额账号会拒推；改由 ignore + 100 MB 限制 |

---

## 7. 风险与对策

| 风险 | 对策 |
|------|------|
| 账号当月仍禁 LFS 推送 | LFS-2 必须做完；新库零 LFS。Billing 数字可下月再看 |
| `push --all` 漏分支 | LFS-1 先 `checkout -B` 齐 `main` / `develop` / `NetSync` |
| 只还原 Art，特效 Missing | ARTBAK / SMOKE 必须含 Resources |
| 开 Unity 早于拷贝，`.meta` 重生 | 关 Editor；用 ARTBAK + 库内 `.meta` 覆盖，不用编辑器「修复」GUID |
| `filter-repo` glob 漏后缀 | LFS-1 `--analyze`；漏了丢 REWRITE，从 MIRROR 重 clone |
| 导出时只有指针、YAML 变成指针文本 | 先拷 WORK 的 `.git/lfs`；导出后 `git grep git-lfs.github.com` 必须空 |
| 误 `git add -A` 且 ignore 未生效 | LFS-5 验收 status；发现立刻 `git reset`，不要 commit |
| 中文音频文件名在终端乱码 | 以资源管理器 / `robocopy` 日志为准，不要只靠 `git log` 显示 |
| 误删旧库且新库不可用 | 用 MIRROR 重建旧库（LFS 涨回；当时没 `lfs fetch --all` 则大文件可能只有指针） |
| 有人 fork 旧库 | 对象可能仍挂在 fork 上；个人库确认无 fork 再删 |
| hash 全变 | 旧 PR 链接、submodule 指针失效；接受 |

---

## 8. Editor 人工步骤

Agent 不改 `.asset` / Prefab / 非 Shader 美术。本方案是 Git 操作，Editor 只做验收：

1. LFS-0 / 改写 / 拷贝期间关闭本工程的 Unity。  
2. SMOKE：先 `robocopy` Art + Audio + Resources，再进项目。  
3. 等待首次导入结束（Art 约数 GB，可能数小时）。  
4. Play：进常用关 / 场景，看角色、刀光、受击特效是否 Missing。  
5. 打开任一 MagicaCloth 组件，确认 Inspector 图标还在。  
6. 不要点 Unity 对 Missing 的「自动修复」生成新 meta。

---

## 9. 推荐开工顺序

```text
LFS-0 备份齐套
  → LFS-1 filter-repo
  → LFS-2 导出并卸载 LFS + ignore + Icon
  → LFS-3 自检
  → LFS-4 推新空库
  → LFS-5 SMOKE 还原 Play
  → LFS-6 删旧库
```

**最小可感切片：** 只做完 LFS-1 + LFS-2，在 REWRITE 上 `git lfs ls-files` 为空、`ls-files "*.fbx"` 为空。不要先建 GitHub 空库。

回滚：规则写错 → 丢掉 REWRITE，从 MIRROR 再 clone；新库有问题且未删旧库 → 修 REWRITE 或重来；Missing + meta 已坏 → 关 Unity，ARTBAK 二进制 + 库内 `.meta` 覆盖。

---

## 10. 变更日志

| 日期 | 说明 |
|------|------|
| 2026-09-02 | 初版：filter-repo + 新库 + 删旧库 |
| 2026-09-06 | **A 定案**：Resources 本机化；去 LFS 化为 LFS-2 必做；mirror 分支齐套；ignore 放行 YAML；MagicaCloth 仅 Icon 进库；Billing 不以当天归零为验收 |
