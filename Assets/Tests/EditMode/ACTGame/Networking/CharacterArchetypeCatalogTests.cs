using System;
using NUnit.Framework;

/// <summary>验证角色原型稳定 Id、登记顺序无关性与目录失败语义。</summary>
public sealed class CharacterArchetypeCatalogTests
{
    /// <summary>同一 stableKey 跨 Catalog 且不同登记顺序仍得到相同 Id。</summary>
    [Test]
    public void Register_SameKeyAcrossCatalogsAndOrder_ReturnsSameId()
    {
        var first = new CharacterArchetypeCatalog();
        var second = new CharacterArchetypeCatalog();

        CharacterArchetype firstTarget =
            first.Register("enemy/raider/heavy", ReplicationActorKind.Enemy);
        first.Register("player/katana/default", ReplicationActorKind.Player);
        second.Register("player/katana/default", ReplicationActorKind.Player);
        CharacterArchetype secondTarget =
            second.Register("enemy/raider/heavy", ReplicationActorKind.Enemy);

        Assert.That(secondTarget.NetArchetypeId, Is.EqualTo(firstTarget.NetArchetypeId));
    }

    /// <summary>不同敌种 stableKey 生成不同原型 Id，不把 Enemy 压成单一原型。</summary>
    [Test]
    public void Register_TwoEnemyStableKeys_ReturnsDifferentArchetypes()
    {
        var catalog = new CharacterArchetypeCatalog();

        CharacterArchetype raider =
            catalog.Register("enemy/raider/heavy", ReplicationActorKind.Enemy);
        CharacterArchetype lancer =
            catalog.Register("enemy/lancer/elite", ReplicationActorKind.Enemy);

        Assert.That(lancer.NetArchetypeId, Is.Not.EqualTo(raider.NetArchetypeId));
        Assert.That(catalog.Count, Is.EqualTo(2));
    }

    /// <summary>可按 stableKey 与 NetArchetypeId 取回同一不可变 descriptor 及其类别。</summary>
    [Test]
    public void TryGet_ByKeyAndId_ReturnsRegisteredDescriptor()
    {
        var catalog = new CharacterArchetypeCatalog();
        CharacterArchetype registered =
            catalog.Register("enemy/lancer/elite", ReplicationActorKind.Enemy);

        bool foundByKey = catalog.TryGet(
            "enemy/lancer/elite",
            out CharacterArchetype byKey);
        bool foundById = catalog.TryGet(registered.NetArchetypeId, out CharacterArchetype byId);

        Assert.That(foundByKey, Is.True);
        Assert.That(foundById, Is.True);
        Assert.That(byKey, Is.SameAs(registered));
        Assert.That(byId, Is.SameAs(registered));
        Assert.That(byId.StableKey, Is.EqualTo("enemy/lancer/elite"));
        Assert.That(byId.Kind, Is.EqualTo(ReplicationActorKind.Enemy));
    }

    /// <summary>登记拒绝 null、空白 stableKey 与重复 stableKey。</summary>
    [Test]
    public void Register_RejectsEmptyAndDuplicateKeys()
    {
        var catalog = new CharacterArchetypeCatalog();

        Assert.Throws<ArgumentException>(
            () => catalog.Register(null, ReplicationActorKind.Player));
        Assert.Throws<ArgumentException>(
            () => catalog.Register(string.Empty, ReplicationActorKind.Player));
        Assert.Throws<ArgumentException>(
            () => catalog.Register("   ", ReplicationActorKind.Player));

        catalog.Register("player/katana/default", ReplicationActorKind.Player);
        Assert.Throws<InvalidOperationException>(
            () => catalog.Register("player/katana/default", ReplicationActorKind.Enemy));
    }
}
