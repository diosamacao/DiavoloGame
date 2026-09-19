using NUnit.Framework;

/// <summary>验证 Recover 冷却：首次立即发送，窗口内不重发，到期后允许再发。</summary>
public sealed class ReplicationRecoveryPolicyTests
{
    /// <summary>冷却未到不得再次 Reset/发送，避免 ForceFull 到达前清空 Registry。</summary>
    [Test]
    public void CanSend_RespectsRetryInterval()
    {
        const long nowMs = 1000;
        long cooldown = ReplicationRecoveryPolicy.NextCooldown(nowMs);

        Assert.That(ReplicationRecoveryPolicy.CanSend(nowMs, 0), Is.True);
        Assert.That(ReplicationRecoveryPolicy.CanSend(nowMs + 1, cooldown), Is.False);
        Assert.That(
            ReplicationRecoveryPolicy.CanSend(nowMs + ReplicationRecoveryPolicy.RetryIntervalMs, cooldown),
            Is.True);
        Assert.That(cooldown - nowMs, Is.EqualTo(ReplicationRecoveryPolicy.RetryIntervalMs));
    }
}
