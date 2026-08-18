/// <summary>单连接权威运行时：独立复制 baseline 与命令 Hint / ACK。</summary>
public sealed class DedicatedPlayerRuntime
{
    /// <summary>用 Match 槽位创建连接运行时。</summary>
    public DedicatedPlayerRuntime(in MatchPlayerSlot slot)
    {
        Slot = slot;
        Replication = new ReplicationServer();
    }

    /// <summary>Match 分配结果。</summary>
    public MatchPlayerSlot Slot { get; }

    /// <summary>该连接独占的复制差分机；重连必须新建，禁止继承上一连接 ACK。</summary>
    public ReplicationServer Replication { get; }

    /// <summary>已灌入权威输入的最新客户端 FrameHint。</summary>
    public long LastAppliedFrameHint { get; private set; }

    /// <summary>本 Poll 真正灌入的最新 Hint；无新命令时为 0。</summary>
    public long AppliedHintThisTick { get; private set; }

    /// <summary>新逻辑拍开始时清掉本拍 Hint，保留累计 LastApplied。</summary>
    public void BeginTick() => AppliedHintThisTick = 0;

    /// <summary>按未应用 Hint 更新本连接 ACK；无新 Hint 时保持原值。</summary>
    public void ApplyUnappliedHints(ClientCommand[] commands)
    {
        if (commands == null || commands.Length == 0)
            return;

        long newest = LastAppliedFrameHint;
        bool any = false;
        for (int i = 0; i < commands.Length; i++)
        {
            long hint = commands[i].FrameHint;
            if (hint <= 0 || !RoomRemoteInputPolicy.ShouldApply(hint, newest))
                continue;
            newest = hint;
            any = true;
        }

        if (!any)
            return;

        LastAppliedFrameHint = newest;
        AppliedHintThisTick = newest;
    }
}
