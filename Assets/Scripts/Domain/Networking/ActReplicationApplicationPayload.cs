using System;

/// <summary>帧级应用载荷：applied hint。命中改走可靠事件通道；本结构仍能编解码 hits 以保持 V1 布局，生产路径编码为空数组。</summary>
public sealed class ActReplicationApplicationPayload
{
    readonly ReplicatedHitEvent[] _hits;

    /// <summary>创建不可变帧级载荷；null 命中数组按空数组处理。</summary>
    public ActReplicationApplicationPayload(
        long appliedClientFrameHint,
        ReplicatedHitEvent[] hits)
    {
        AppliedClientFrameHint = appliedClientFrameHint;
        _hits = hits == null || hits.Length == 0
            ? Array.Empty<ReplicatedHitEvent>()
            : (ReplicatedHitEvent[])hits.Clone();
    }

    /// <summary>本权威步真正采用的客户端 FrameHint；0 表示未采用新命令。</summary>
    public long AppliedClientFrameHint { get; }

    /// <summary>返回本帧权威命中表现事件的独立副本。</summary>
    public ReplicatedHitEvent[] Hits => (ReplicatedHitEvent[])_hits.Clone();

    /// <summary>供同程序集 Codec 读取不可变命中缓冲，避免重复复制。</summary>
    internal ReplicatedHitEvent[] HitBuffer => _hits;
}
