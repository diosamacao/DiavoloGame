using System;
using UnityEngine;

/// <summary>GaitPolicy.Evaluate 输出。</summary>
public readonly struct GaitPolicyResult
{
    /// <summary>构造求值结果。</summary>
    public GaitPolicyResult(LocomotionGait nextGait, int runHoldFrames)
    {
        NextGait = nextGait;
        RunHoldFrames = runHoldFrames;
    }

    /// <summary>本次求值后的步态。</summary>
    public LocomotionGait NextGait { get; }
    /// <summary>下一逻辑帧应保存的 Run 持续计数。</summary>
    public int RunHoldFrames { get; }
}
