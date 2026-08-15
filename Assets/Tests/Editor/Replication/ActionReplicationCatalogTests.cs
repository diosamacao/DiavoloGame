using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>动作目录：同资产稳定 Id，同名跨目录同 Id，null 为 0。</summary>
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

    /// <summary>两个目录对同名资产必须得到同一 Id，供跨进程 Seek。</summary>
    [Test]
    public void GetOrAdd_SameName_AcrossCatalogs_SharesStableId()
    {
        ActionDefinition first = ScriptableObject.CreateInstance<ActionDefinition>();
        ActionDefinition second = ScriptableObject.CreateInstance<ActionDefinition>();
        first.name = "NS5_StableSlash";
        second.name = "NS5_StableSlash";

        var host = new ActionReplicationCatalog();
        var client = new ActionReplicationCatalog();
        int hostId = host.GetOrAdd(first);
        int clientId = client.GetOrAdd(second);

        Assert.That(hostId, Is.EqualTo(clientId));
        Assert.That(hostId, Is.EqualTo(ActionReplicationCatalog.ComputeStableId("NS5_StableSlash")));
        Assert.That(client.TryGet(clientId, out ActionDefinition restored), Is.True);
        Assert.That(restored, Is.SameAs(second));

        Object.DestroyImmediate(first);
        Object.DestroyImmediate(second);
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

    /// <summary>六向 Resolver 变体必须能预填，否则客机侧/后闪只有位移没有 Clip。</summary>
    [Test]
    public void Prefill_DirectionalResolverVariants_CanTryGetEach()
    {
        ActionDefinition forward = CreateNamedAction("Dodge_Forward");
        ActionDefinition backward = CreateNamedAction("Dodge_Backward");
        ActionDefinition forwardLeft = CreateNamedAction("Dodge_ForwardLeft");
        var resolver = ScriptableObject.CreateInstance<DirectionalActionResolver>();
        var serialized = new SerializedObject(resolver);
        serialized.FindProperty("forwardAction").objectReferenceValue = forward;
        serialized.FindProperty("backwardAction").objectReferenceValue = backward;
        serialized.FindProperty("forwardLeftAction").objectReferenceValue = forwardLeft;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        var collected = new List<ActionDefinition>();
        resolver.CollectActions(collected);
        var catalog = new ActionReplicationCatalog();
        catalog.Prefill(collected);

        Assert.That(catalog.TryGet(ActionReplicationCatalog.ComputeStableId("Dodge_Forward"), out _), Is.True);
        Assert.That(catalog.TryGet(ActionReplicationCatalog.ComputeStableId("Dodge_Backward"), out _), Is.True);
        Assert.That(catalog.TryGet(ActionReplicationCatalog.ComputeStableId("Dodge_ForwardLeft"), out _), Is.True);

        Object.DestroyImmediate(resolver);
        Object.DestroyImmediate(forward);
        Object.DestroyImmediate(backward);
        Object.DestroyImmediate(forwardLeft);
    }

    static ActionDefinition CreateNamedAction(string name)
    {
        ActionDefinition action = ScriptableObject.CreateInstance<ActionDefinition>();
        action.name = name;
        return action;
    }
}
