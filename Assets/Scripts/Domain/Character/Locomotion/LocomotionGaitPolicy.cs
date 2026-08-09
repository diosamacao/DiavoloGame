using System;
using UnityEngine;

/// <summary>
/// 步态升档策略（挂在 LocomotionProfile）：MaxGait / Pivot / Sprint 计时。
/// 敌我差异靠不同 Profile 资产，不在 State 里按身份分支。
/// </summary>
[Serializable]
public sealed class LocomotionGaitPolicy
{
    [Tooltip("允许的最高稳态步态；敌人近战建议 Run。")]
    [SerializeField] LocomotionGait maxGait = LocomotionGait.Sprint;

    [Tooltip("为 true 时仅在 Sprint 步态允许大角度 Pivot（对齐现网玩家）。")]
    [SerializeField] bool allowPivot = true;

    [Tooltip("Run 步态下连续保持跑输入达到该秒数后进入 Sprint（受 MaxGait 限制）。")]
    [SerializeField] float sprintAfterRunSeconds = 3f;

    /// <summary>允许的最高步态。</summary>
    public LocomotionGait MaxGait => maxGait;

    /// <summary>是否允许 Pivot（且当前须为 Sprint）。</summary>
    public bool AllowPivot => allowPivot;

    /// <summary>Run→Sprint 所需持续跑输入秒数。</summary>
    public float SprintAfterRunSeconds => Mathf.Max(0f, sprintAfterRunSeconds);

    /// <summary>创建策略（测试与默认装配）。</summary>
    public LocomotionGaitPolicy(
        LocomotionGait maxGait = LocomotionGait.Sprint,
        bool allowPivot = true,
        float sprintAfterRunSeconds = 3f)
    {
        this.maxGait = maxGait;
        this.allowPivot = allowPivot;
        this.sprintAfterRunSeconds = Mathf.Max(0f, sprintAfterRunSeconds);
    }

    /// <summary>无参构造供 Unity 序列化。</summary>
    public LocomotionGaitPolicy()
    {
    }

    /// <summary>将候选步态压到 MaxGait 以下（含）。</summary>
    public LocomotionGait ClampGait(LocomotionGait gait)
    {
        return (int)gait > (int)maxGait ? maxGait : gait;
    }

    /// <summary>当前步态是否允许进入 PivotTurn。</summary>
    public bool AllowsPivot(LocomotionGait gait) =>
        allowPivot && gait == LocomotionGait.Sprint;

    /// <summary>
    /// 移动中升档求值：幅度≤跑阈→Walk；否则升 Run；Run 满秒且 Max 允许→Sprint。
    /// </summary>
    public GaitPolicyResult Evaluate(in GaitPolicyInput input)
    {
        float hold = Mathf.Max(0f, input.RunHoldSeconds);
        bool wantRunTier = input.MoveMagnitude > input.RunThreshold;

        if (!wantRunTier)
        {
            return new GaitPolicyResult(ClampGait(LocomotionGait.Walk), 0f);
        }

        LocomotionGait current = input.CurrentGait;
        if (current == LocomotionGait.Walk
            || (int)current < (int)LocomotionGait.Run)
        {
            return new GaitPolicyResult(ClampGait(LocomotionGait.Run), 0f);
        }

        if (current == LocomotionGait.Run)
        {
            // MaxGait 不够 Sprint：保持 Run，不累计无意义的 hold
            if ((int)maxGait < (int)LocomotionGait.Sprint)
                return new GaitPolicyResult(LocomotionGait.Run, 0f);

            hold += Mathf.Max(0f, input.DeltaTime);
            if (hold >= SprintAfterRunSeconds)
                return new GaitPolicyResult(LocomotionGait.Sprint, 0f);

            return new GaitPolicyResult(LocomotionGait.Run, hold);
        }

        // 已在 Sprint：保持（仍受 Max 钳制）
        return new GaitPolicyResult(ClampGait(LocomotionGait.Sprint), 0f);
    }
}

/// <summary>GaitPolicy.Evaluate 输入。</summary>
public readonly struct GaitPolicyInput
{
    /// <summary>构造求值输入。</summary>
    public GaitPolicyInput(
        LocomotionGait currentGait,
        float moveMagnitude,
        float runThreshold,
        float deltaTime,
        float runHoldSeconds)
    {
        CurrentGait = currentGait;
        MoveMagnitude = moveMagnitude;
        RunThreshold = runThreshold;
        DeltaTime = deltaTime;
        RunHoldSeconds = runHoldSeconds;
    }

    public LocomotionGait CurrentGait { get; }
    public float MoveMagnitude { get; }
    public float RunThreshold { get; }
    public float DeltaTime { get; }
    public float RunHoldSeconds { get; }
}

/// <summary>GaitPolicy.Evaluate 输出。</summary>
public readonly struct GaitPolicyResult
{
    /// <summary>构造求值结果。</summary>
    public GaitPolicyResult(LocomotionGait nextGait, float runHoldSeconds)
    {
        NextGait = nextGait;
        RunHoldSeconds = runHoldSeconds;
    }

    public LocomotionGait NextGait { get; }
    public float RunHoldSeconds { get; }
}
