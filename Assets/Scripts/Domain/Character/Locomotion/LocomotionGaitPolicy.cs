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

    [Tooltip("Run 步态下连续保持跑输入达到该逻辑帧数后进入 Sprint（受 MaxGait 限制）。")]
    [SerializeField, Min(0)] int sprintAfterRunFrames = 180;

    /// <summary>允许的最高步态。</summary>
    public LocomotionGait MaxGait => maxGait;

    /// <summary>是否允许 Pivot（且当前须为 Sprint）。</summary>
    public bool AllowPivot => allowPivot;

    /// <summary>Run→Sprint 所需持续跑输入逻辑帧数。</summary>
    public int SprintAfterRunFrames => Mathf.Max(0, sprintAfterRunFrames);

    /// <summary>创建策略（测试与默认装配）。</summary>
    public LocomotionGaitPolicy(
        LocomotionGait maxGait = LocomotionGait.Sprint,
        bool allowPivot = true,
        int sprintAfterRunFrames = 180)
    {
        this.maxGait = maxGait;
        this.allowPivot = allowPivot;
        this.sprintAfterRunFrames = Mathf.Max(0, sprintAfterRunFrames);
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
    /// 移动中升档求值：幅度≤跑阈→Walk；否则升 Run；Run 满帧且 Max 允许→Sprint。
    /// </summary>
    public GaitPolicyResult Evaluate(in GaitPolicyInput input)
    {
        int hold = Mathf.Max(0, input.RunHoldFrames);
        bool wantRunTier = input.MoveMagnitude > input.RunThreshold;

        if (!wantRunTier)
        {
            return new GaitPolicyResult(ClampGait(LocomotionGait.Walk), 0);
        }

        LocomotionGait current = input.CurrentGait;
        if (current == LocomotionGait.Walk
            || (int)current < (int)LocomotionGait.Run)
        {
            return new GaitPolicyResult(ClampGait(LocomotionGait.Run), 0);
        }

        if (current == LocomotionGait.Run)
        {
            // MaxGait 不够 Sprint：保持 Run，不累计无意义的 hold
            if ((int)maxGait < (int)LocomotionGait.Sprint)
                return new GaitPolicyResult(LocomotionGait.Run, 0);

            hold++;
            if (hold >= SprintAfterRunFrames)
                return new GaitPolicyResult(LocomotionGait.Sprint, 0);

            return new GaitPolicyResult(LocomotionGait.Run, hold);
        }

        // 已在 Sprint：保持（仍受 Max 钳制）
        return new GaitPolicyResult(ClampGait(LocomotionGait.Sprint), 0);
    }
}
