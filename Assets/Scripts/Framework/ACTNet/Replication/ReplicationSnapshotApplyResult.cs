using System;
using System.Collections.Generic;

/// <summary>快照已立即应用、正在等待生命周期屏障，或本包验证失败。</summary>
public enum ReplicationSnapshotApplyResult : byte
{
    /// <summary>记录已越过屏障。</summary>
    Applied = 0,
    /// <summary>记录已进入有界最新值缓冲。</summary>
    Buffered = 1,
    /// <summary>本包校验失败并已请求恢复；不得当作已应用。</summary>
    Rejected = 2,
}
