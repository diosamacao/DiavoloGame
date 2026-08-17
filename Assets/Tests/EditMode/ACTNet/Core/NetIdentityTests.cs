using System;
using NUnit.Framework;

/// <summary>冻结 ACTNet.Core 稳定身份、帧号、版本与 ACT 映射契约。</summary>
public sealed class NetIdentityTests
{
    /// <summary>所有整数 Id 的 default 必须无效，正值必须稳定比较和哈希。</summary>
    [Test]
    public void IntegerIds_InvalidEqualityAndHash_AreStable()
    {
        Assert.That(NetConnectionId.Invalid.IsValid, Is.False);
        Assert.That(NetPlayerId.Invalid.IsValid, Is.False);
        Assert.That(NetEntityId.Invalid.IsValid, Is.False);
        Assert.That(NetArchetypeId.Invalid.IsValid, Is.False);

        var connection = new NetConnectionId(7);
        var player = new NetPlayerId(7);
        var entity = new NetEntityId(7);
        var archetype = new NetArchetypeId(7);

        Assert.That(connection, Is.EqualTo(new NetConnectionId(7)));
        Assert.That(player, Is.EqualTo(new NetPlayerId(7)));
        Assert.That(entity, Is.EqualTo(new NetEntityId(7)));
        Assert.That(archetype, Is.EqualTo(new NetArchetypeId(7)));
        Assert.That(entity.GetHashCode(), Is.EqualTo(new NetEntityId(7).GetHashCode()));
        Assert.That(entity.CompareTo(new NetEntityId(8)), Is.LessThan(0));
    }

    /// <summary>整数 Id 不允许把 0 或负数伪装成有效身份。</summary>
    [Test]
    public void IntegerIds_RejectNonPositiveValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new NetConnectionId(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NetPlayerId(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NetEntityId(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NetArchetypeId(-1));
    }

    /// <summary>Tick 0 必须有效，同时 default 仍保留为 Invalid。</summary>
    [Test]
    public void NetTick_ZeroAndInvalid_AreDistinct()
    {
        var zero = new NetTick(0);

        Assert.That(NetTick.Invalid.IsValid, Is.False);
        Assert.That(NetTick.Invalid.Value, Is.EqualTo(-1));
        Assert.That(zero.IsValid, Is.True);
        Assert.That(zero.Value, Is.Zero);
        Assert.That(zero.Next(), Is.EqualTo(new NetTick(1)));
        Assert.Throws<InvalidOperationException>(() => NetTick.Invalid.Next());
    }

    /// <summary>Sequence 0 必须有效并可单调推进，同时 default 保持无效。</summary>
    [Test]
    public void NetSequence_ZeroAndInvalid_AreDistinct()
    {
        var zero = new NetSequence(0);

        Assert.That(NetSequence.Invalid.IsValid, Is.False);
        Assert.That(zero.IsValid, Is.True);
        Assert.That(zero.Next(), Is.EqualTo(new NetSequence(1)));
        Assert.That(new NetSequence(2).CompareTo(new NetSequence(1)), Is.GreaterThan(0));
    }

    /// <summary>协议版本和 Content 指纹必须按稳定数值比较。</summary>
    [Test]
    public void VersionsAndFingerprint_Equality_IsValueBased()
    {
        var version = new NetworkProtocolVersion(1);
        var fingerprint = new ContentFingerprint(0x0102030405060708ul, 0x1112131415161718ul);

        Assert.That(NetworkProtocolVersion.Invalid.IsValid, Is.False);
        Assert.That(version, Is.EqualTo(new NetworkProtocolVersion(1)));
        Assert.That(fingerprint, Is.EqualTo(
            new ContentFingerprint(0x0102030405060708ul, 0x1112131415161718ul)));
        Assert.That(fingerprint.ToString(), Is.EqualTo("01020304050607081112131415161718"));
        Assert.Throws<ArgumentException>(() => new ContentFingerprint(0ul, 0ul));
    }

    /// <summary>失败结果必须携带非 None 的稳定原因。</summary>
    [Test]
    public void NetResult_Failure_RequiresReason()
    {
        NetResult success = NetResult.Success;
        NetResult failure = NetResult.Failure(
            DisconnectReason.ProtocolMismatch,
            "version");

        Assert.That(success.IsSuccess, Is.True);
        Assert.That(success.Reason, Is.EqualTo(DisconnectReason.None));
        Assert.That(failure.IsSuccess, Is.False);
        Assert.That(failure.Reason, Is.EqualTo(DisconnectReason.ProtocolMismatch));
        Assert.Throws<ArgumentException>(
            () => NetResult.Failure(DisconnectReason.None));
    }
}
