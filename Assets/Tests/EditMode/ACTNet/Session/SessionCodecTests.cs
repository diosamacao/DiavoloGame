using System;
using NUnit.Framework;

/// <summary>验证 Session 控制消息字段、信封版本与既有一字节原因布局。</summary>
public sealed class SessionCodecTests
{
    /// <summary>JoinAccept 的强类型身份与权威 Tick 必须原样往返。</summary>
    [Test]
    public void JoinAccept_RoundTrip_PreservesFields()
    {
        var accept = new SessionJoinAccept(
            new NetPlayerId(2),
            new NetEntityId(7),
            new NetEntityId(1),
            3,
            new NetTick(42));

        byte[] payload = SessionCodec.WriteJoinAccept(in accept);
        SessionCodec.ReadEnvelope(payload, out byte kind, out byte[] body);
        SessionJoinAccept restored = SessionCodec.ReadJoinAccept(body);

        Assert.That(kind, Is.EqualTo((byte)SessionMessageKind.JoinAccept));
        Assert.That(restored.PlayerId.Value, Is.EqualTo(2));
        Assert.That(restored.EntityId.Value, Is.EqualTo(7));
        Assert.That(restored.AuthorityEntityId.Value, Is.EqualTo(1));
        Assert.That(restored.ContentVersion, Is.EqualTo(3));
        Assert.That(restored.AuthorityTick.Value, Is.EqualTo(42));
    }

    /// <summary>Dedicated 无房主时 AuthorityEntityId 可为 Invalid，线格式写 0。</summary>
    [Test]
    public void JoinAccept_InvalidAuthorityEntity_RoundTrips()
    {
        var accept = new SessionJoinAccept(
            new NetPlayerId(1),
            new NetEntityId(3),
            NetEntityId.Invalid,
            1,
            new NetTick(0));

        byte[] payload = SessionCodec.WriteJoinAccept(in accept);
        SessionCodec.ReadEnvelope(payload, out _, out byte[] body);
        SessionJoinAccept restored = SessionCodec.ReadJoinAccept(body);

        Assert.That(restored.AuthorityEntityId.IsValid, Is.False);
        Assert.That(restored.EntityId.Value, Is.EqualTo(3));
        Assert.That(restored.PlayerId.Value, Is.EqualTo(1));
    }

    /// <summary>不支持的信封版本必须在读取正文前被拒绝。</summary>
    [Test]
    public void ReadEnvelope_UnsupportedVersion_Throws()
    {
        byte[] payload = { 99, (byte)SessionMessageKind.JoinRequest };
        Assert.Throws<InvalidOperationException>(() =>
            SessionCodec.ReadEnvelope(payload, out _, out _));
    }

    /// <summary>心跳请求和服务端回显时间戳必须完整往返。</summary>
    [Test]
    public void Heartbeat_RoundTrip_PreservesSendAndEcho()
    {
        var heartbeat = new SessionHeartbeat(1000, 1000);
        byte[] payload = SessionCodec.WriteHeartbeat(in heartbeat);
        SessionCodec.ReadEnvelope(payload, out byte kind, out byte[] body);
        SessionHeartbeat restored = SessionCodec.ReadHeartbeat(body);

        Assert.That(kind, Is.EqualTo((byte)SessionMessageKind.Heartbeat));
        Assert.That(restored.SendTimeMs, Is.EqualTo(1000));
        Assert.That(restored.EchoTimeMs, Is.EqualTo(1000));
    }

    /// <summary>Kick 原因继续占用单字节并保持 IdleTimeout 数值。</summary>
    [Test]
    public void Kick_RoundTrip_PreservesReason()
    {
        byte[] payload = SessionCodec.WriteKick(SessionKickReason.IdleTimeout);
        Assert.That(payload, Is.EqualTo(new byte[]
        {
            SessionCodec.EnvelopeVersion,
            (byte)SessionMessageKind.Kick,
            (byte)SessionKickReason.IdleTimeout,
        }));

        SessionCodec.ReadEnvelope(payload, out _, out byte[] body);
        Assert.That(
            SessionCodec.ReadKick(body),
            Is.EqualTo(SessionKickReason.IdleTimeout));
    }
}
