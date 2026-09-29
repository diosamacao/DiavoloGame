using System;
using UnityEngine;

/// <summary>GaitPolicy.Evaluate 输入。</summary>
public readonly struct GaitPolicyInput
{
    /// <summary>构造求值输入。</summary>
    public GaitPolicyInput(
        LocomotionGait currentGait,
        float moveMagnitude,
        float runThreshold,
        int runHoldFrames)
    {
        CurrentGait = currentGait;
        MoveMagnitude = moveMagnitude;
        RunThreshold = runThreshold;
        RunHoldFrames = runHoldFrames;
    }

    /// <summary>当前稳态步态。</summary>
    public LocomotionGait CurrentGait { get; }
    /// <summary>当前移动输入幅度。</summary>
    public float MoveMagnitude { get; }
    /// <summary>进入 Run 档的输入阈值。</summary>
    public float RunThreshold { get; }
    /// <summary>已连续保持 Run 输入的逻辑帧数。</summary>
    public int RunHoldFrames { get; }
}
