/// <summary>本 Tick 生命边沿；用于补播受击/死亡，避免只靠 HP 差值漏事件。</summary>
public enum VitalityReplicationEdge : byte
{
    /// <summary>本帧无生命边沿。</summary>
    None = 0,

    /// <summary>本帧发生非致命有效受击。</summary>
    Hit = 1,

    /// <summary>本帧生命首次归零。</summary>
    Death = 2,
}
