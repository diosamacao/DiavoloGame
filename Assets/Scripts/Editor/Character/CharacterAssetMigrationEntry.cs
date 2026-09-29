using System;

/// <summary>经审阅的资产迁移条目；源内容指纹阻止覆盖清单生成后的作者修改。</summary>
[Serializable]
public sealed class CharacterAssetMigrationEntry
{
    public string oldPath, newPath, guid, oldName, newName, sha256;
}
