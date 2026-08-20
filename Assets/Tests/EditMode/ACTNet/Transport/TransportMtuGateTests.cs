using System;
using NUnit.Framework;

/// <summary>MTU 门禁：超限拒绝，合法包通过。不引用 ACT。</summary>
public sealed class TransportMtuGateTests
{
    /// <summary>默认数据报上限内的包可通过。</summary>
    [Test]
    public void TryAccept_WithinMtu_Succeeds()
    {
        Assert.That(
            TransportMtuGate.TryAccept(64, TransportMtuGate.DefaultMaxDatagramBytes, out string reason),
            Is.True);
        Assert.That(reason, Is.Empty);
        Assert.That(TransportMtuGate.MaxPayloadBytes(1400), Is.EqualTo(1391));
    }

    /// <summary>超过配置 MTU 必须失败并给出原因。</summary>
    [Test]
    public void TryAccept_OverMtu_Fails()
    {
        Assert.That(TransportMtuGate.TryAccept(65, maxDatagramBytes: 64, out string reason), Is.False);
        Assert.That(reason, Does.Contain("超过配置 MTU"));
        Assert.Throws<InvalidOperationException>(() => TransportMtuGate.EnsureAccepted(65, 64));
    }
}
