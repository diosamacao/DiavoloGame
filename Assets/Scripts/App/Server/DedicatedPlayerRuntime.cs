/// <summary>单连接权威运行时：独立命令 Hint / ACK；复制差分机在权威世界按连接持有。</summary>
public sealed class DedicatedPlayerRuntime
{
    /// <summary>用 Match 槽位与 World 分配的实体 Id 创建连接运行时。</summary>
    public DedicatedPlayerRuntime(in MatchPlayerSlot slot, NetEntityId entityId)
    {
        Slot = slot;
        EntityId = entityId;
    }

    /// <summary>Match 分配结果。</summary>
    public MatchPlayerSlot Slot { get; }

    /// <summary>JoinAccept 使用的权威实体 Id，等于 World SimulationId。</summary>
    public NetEntityId EntityId { get; }

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
