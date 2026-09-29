"""Read-only Unity YAML inventory; preserves exact bytes outside Assets before migration."""
import base64
import hashlib
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'docs/2026.9.28'
BACKUP = ROOT / '.utmp/character-authoring-baseline.json'
TYPES = {
    'CharacterConfig': 'Domain/Character/CharacterConfig.cs',
    'CombatModeProfile': 'Domain/Character/Combat/CombatModeProfile.cs',
    'CharacterAnimationProfile': 'Domain/Character/Animation/CharacterAnimationProfile.cs',
    'CharacterLocomotionProfile': 'Domain/Character/Locomotion/CharacterLocomotionProfile.cs',
    'CharacterDefinition': 'Domain/Character/Party/CharacterDefinition.cs',
    'ActionDefinition': 'Domain/Combat/Actions/Definitions/ActionDefinition.cs',
    'ActionGraph': 'Domain/Combat/Actions/Resolution/ActionGraph.cs',
}

def digest(data):
    return hashlib.sha256(data).hexdigest()

def main():
    guids = {}
    for kind, source in TYPES.items():
        meta = (ROOT / 'Assets/Scripts' / (source + '.meta')).read_text('utf-8-sig')
        guids[re.search(r'^guid: (\w+)', meta, re.M)[1]] = kind
    records = []
    for path in sorted((ROOT / 'Assets').rglob('*.asset')):
        data = path.read_bytes()
        body = data.decode('utf-8-sig', errors='replace')
        match = re.search(r'm_Script:.*guid: (\w+)', body)
        if not match or match[1] not in guids:
            continue
        kind = guids[match[1]]
        operation = ('delete' if kind in ('CombatModeProfile', 'CharacterAnimationProfile')
                     else 'modify' if kind in ('CharacterConfig', 'CharacterLocomotionProfile',
                                               'CharacterDefinition', 'ActionDefinition') else 'read-only')
        if kind == 'ActionDefinition':
            rate = re.search(r'^  sampleRate: (\d+)', body, re.M)
            if not rate or int(rate[1]) != 60:
                raise ValueError(f'Non-60Hz action: {path}')
        meta = path.with_suffix('.asset.meta').read_bytes()
        records.append(dict(path=path.relative_to(ROOT).as_posix(), kind=kind, operation=operation,
                            sha256=digest(data), content=base64.b64encode(data).decode(),
                            meta=base64.b64encode(meta).decode(), metaSha256=digest(meta)))
    serialized = json.dumps(dict(version=1, assets=records), ensure_ascii=False, indent=2)+'\n'
    BACKUP.parent.mkdir(parents=True, exist_ok=True)
    if BACKUP.exists() and BACKUP.read_text('utf-8') != serialized:
        raise RuntimeError('Existing baseline differs; refusing to overwrite backup.')
    BACKUP.write_text(serialized, 'utf-8')
    OUT.mkdir(parents=True, exist_ok=True)
    lines = ['# 角色配置迁移资产清单', '',
             '> 只读磁盘清点；尚未迁移。原始字节与 meta 备份在 `.utmp/character-authoring-baseline.json`。',
             '> Unity 正打开该项目；磁盘快照不代表尚未保存的 Editor 内存值。执行迁移前必须确认已保存相关配置。',
             '', '修改：Config 内嵌模式、Locomotion 内嵌动画映射、Definition 删除预留标签、Action 删除采样率字段。',
             '删除：已内嵌的两类 Profile 资产及其 meta。其它资产不修改，不改场景、Prefab、模型或 Clip。',
             '', '| 操作 | 类型 | 路径 | SHA256 |', '|---|---|---|---|']
    for r in records:
        lines.append(f'| {r["operation"]} | {r["kind"]} | `{r["path"]}` | `{r["sha256"]}` |')
    (OUT/'CHARACTER_AUTHORING_MIGRATION_MANIFEST.md').write_text('\n'.join(lines)+'\n', 'utf-8')
    print(json.dumps({kind: sum(r['kind']==kind for r in records) for kind in TYPES}))
    print(f'Baseline: {BACKUP}; original bytes preserved; Assets unchanged.')

if __name__ == '__main__':
    main()
