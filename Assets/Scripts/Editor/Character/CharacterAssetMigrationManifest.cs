using System;

/// <summary>显式迁移清单，不通过目录名推断角色归属。</summary>
[Serializable]
public sealed class CharacterAssetMigrationManifest
{
    public CharacterAssetMigrationEntry[] entries;
}
