using System;
using System.Collections.Generic;

/// <summary>远端命令灌入结果；Applied=false 时调用方必须保留原 Hint 状态。</summary>
public readonly struct ActAuthorityInputApplyResult
{
    /// <summary>创建输入灌入结果。firstAppliedHint 写入下行 appliedHint；newestHint 用于跳过冗余。</summary>
    public ActAuthorityInputApplyResult(bool applied, long newestHint, long firstAppliedHint)
    {
        Applied = applied;
        NewestHint = newestHint;
        FirstAppliedHint = firstAppliedHint;
    }

    /// <summary>本批命令是否实际写入权威输入缓冲。</summary>
    public bool Applied { get; }

    /// <summary>成功应用后的最新客户端 FrameHint；未应用时等于调用方传入值。</summary>
    public long NewestHint { get; }

    /// <summary>本批第一条新 Hint；客机按该帧预测位姿和解，避免用 newest 对压缩后的权威步。</summary>
    public long FirstAppliedHint { get; }
}
