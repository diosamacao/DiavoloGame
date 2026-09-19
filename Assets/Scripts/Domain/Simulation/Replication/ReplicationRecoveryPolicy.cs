/// <summary>客户端 ReplicationRecover 发送冷却：首次立即发，冷却期内不重置，到期可再发。</summary>
public static class ReplicationRecoveryPolicy
{
    /// <summary>两次 Recover 最小间隔；给 ForceFull 生命周期到达的窗口，避免重置风暴。</summary>
    public const int RetryIntervalMs = 500;

    /// <summary>当前时刻是否允许再发 Recover 并重置 Client Registry。</summary>
    public static bool CanSend(long nowMs, long cooldownUntilMs) =>
        nowMs >= cooldownUntilMs;

    /// <summary>本次发送后的下一次允许时刻。</summary>
    public static long NextCooldown(long nowMs) => nowMs + RetryIntervalMs;
}
