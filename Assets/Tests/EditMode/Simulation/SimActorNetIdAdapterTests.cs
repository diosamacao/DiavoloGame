using System;
using NUnit.Framework;

/// <summary>冻结 ACTGame Simulation 身份到 ACTNet 实体身份的显式边界映射。</summary>
public sealed class SimActorNetIdAdapterTests
{
    /// <summary>首版 Adapter 必须保持 SimActorId 与 NetEntityId 数值一一对应。</summary>
    [Test]
    public void RoundTrip_PreservesValue()
    {
        var actorId = new SimActorId(42);

        var entityId = SimActorNetIdAdapter.ToNetEntityId(actorId);
        SimActorId restored = SimActorNetIdAdapter.ToSimActorId(entityId);

        Assert.That(entityId.Value, Is.EqualTo(42));
        Assert.That(restored, Is.EqualTo(actorId));
        Assert.Throws<ArgumentException>(
            () => SimActorNetIdAdapter.ToNetEntityId(SimActorId.Invalid));
        Assert.Throws<ArgumentException>(
            () => SimActorNetIdAdapter.ToSimActorId(default));
    }
}
