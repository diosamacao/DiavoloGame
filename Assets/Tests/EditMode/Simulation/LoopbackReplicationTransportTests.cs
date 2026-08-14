using NUnit.Framework;

/// <summary>Loopback 延迟 0 时权威 Tick 帧号连续单调。</summary>
public sealed class LoopbackReplicationTransportTests
{
    /// <summary>延迟 0：连续 60 帧下行 authorityFrame 严格 +1。</summary>
    [Test]
    public void DelayZero_SixtyTicks_AuthorityFrameMonotonic()
    {
        var transport = new LoopbackReplicationTransport();
        transport.SetLatencyMs(0);
        var motor = new CharacterMotorSim(OpenFieldSimCollisionWorld.Instance, radiusMm: 280);
        var idle = new ActionSimSnapshot(null, null, null, 0, 0, false, false, 0);
        long previous = -1;

        for (int i = 0; i < 60; i++)
        {
            motor.TeleportMm(i * 10, 0);
            ActorReplicationSnapshot snapshot = ReplicationSnapshotBuilder.FromAuthority(
                new SimActorId(1),
                1,
                ReplicationActorKind.Player,
                motor,
                in idle,
                actionId: 0,
                SimActorId.Invalid,
                healthMilli: 100000,
                flagsPacked: 0,
                VitalityReplicationEdge.None);
            var tick = new AuthorityTick(i, new[] { snapshot });
            transport.SendAuthorityToClients(ReplicationCodec.WriteAuthorityTick(tick));
            transport.Pump();

            Assert.That(transport.TryDequeueClient(out byte[] payload), Is.True);
            AuthorityTick received = ReplicationCodec.ReadAuthorityTick(payload);
            Assert.That(received.AuthorityFrame, Is.EqualTo(i));
            if (previous >= 0)
                Assert.That(received.AuthorityFrame, Is.EqualTo(previous + 1));
            previous = received.AuthorityFrame;
        }

        Assert.That(transport.TryDequeueClient(out _), Is.False);
    }

    /// <summary>上行命令经 Loopback 原样到达权威。</summary>
    [Test]
    public void DelayZero_ClientCommand_ReachesAuthority()
    {
        var transport = new LoopbackReplicationTransport();
        var input = new InputFrame(3, new SimActorId(1), 5, 0, 0ul, 0ul, 0ul, 0);
        var command = new ClientCommand(3, 1, in input);

        transport.SendClientToAuthority(ReplicationCodec.WriteClientCommand(in command));
        transport.Pump();

        Assert.That(transport.TryDequeueAuthority(out byte[] payload), Is.True);
        Assert.That(ReplicationCodec.ReadClientCommand(payload).Equals(command), Is.True);
    }
}
