using NUnit.Framework;
using UnityEngine;

/// <summary>同进程动作目录：同一资产稳定 Id，null 为 0。</summary>
public sealed class ActionReplicationCatalogTests
{
    /// <summary>同一 ActionDefinition 多次 GetOrAdd 返回同一正 Id，并能反查。</summary>
    [Test]
    public void GetOrAdd_SameAsset_ReturnsStableId()
    {
        var catalog = new ActionReplicationCatalog();
        ActionDefinition action = ScriptableObject.CreateInstance<ActionDefinition>();

        int first = catalog.GetOrAdd(action);
        int second = catalog.GetOrAdd(action);

        Assert.That(first, Is.GreaterThan(0));
        Assert.That(second, Is.EqualTo(first));
        Assert.That(catalog.TryGet(first, out ActionDefinition restored), Is.True);
        Assert.That(restored, Is.SameAs(action));

        Object.DestroyImmediate(action);
    }

    /// <summary>null 与未登记 Id 不得映射到资产。</summary>
    [Test]
    public void GetOrAdd_Null_IsZero_AndUnknownIdFails()
    {
        var catalog = new ActionReplicationCatalog();
        Assert.That(catalog.GetOrAdd(null), Is.Zero);
        Assert.That(catalog.TryGet(0, out _), Is.False);
        Assert.That(catalog.TryGet(99, out _), Is.False);
    }
}
