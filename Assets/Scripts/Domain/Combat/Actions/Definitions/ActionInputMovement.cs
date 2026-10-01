using System;
using UnityEngine;

/// <summary>动作内输入移动配置；方向 Clip 只覆盖表现，不改变动作实例和时间轴。</summary>
[Serializable]
public sealed class ActionInputMovement : ActionNotifyState
{
    /// <summary>方向动画使用的时钟；同步模式只换片，不重置 Action 段进度。</summary>
    public enum AnimationTimeMode
    {
        FollowActionSegment = 0,
        FromDirectionChange = 1
    }

    [Tooltip("开启时有输入播放四向动画；关闭时只移动，继续播放 Action 原动画（例如飘落）。")]
    public bool overrideMovementAnimation = true;
    [Min(0)] public int speedMmPerSecond = 2500;
    [Range(0.1f, 1f)] public float inputThreshold = 0.2f;
    [Min(0)] public int minimumDirectionFrames = 3;
    public AnimationClip forward;
    public AnimationClip back;
    public AnimationClip left;
    public AnimationClip right;
    [Tooltip("FollowActionSegment 与当前 Action 动画段同步（含裁剪起始帧），不独立循环；FromDirectionChange 从换向时起播。")]
    public AnimationTimeMode animationTimeMode = AnimationTimeMode.FollowActionSegment;
    public bool loopMove = true;
    [Min(0)] public float crossFadeSeconds = 0.08f;

    /// <summary>输入动作必须具备完整选片及有限数值配置。</summary>
    public bool IsValid => speedMmPerSecond > 0 && speedMmPerSecond <= 1000000
        && StartFrame >= 0 && EndFrame >= StartFrame
        && inputThreshold >= .1f && inputThreshold <= 1f
        && minimumDirectionFrames >= 0
        && (animationTimeMode == AnimationTimeMode.FollowActionSegment || animationTimeMode == AnimationTimeMode.FromDirectionChange)
        && crossFadeSeconds >= 0 && !float.IsInfinity(crossFadeSeconds)
        && (!overrideMovementAnimation || (forward != null && back != null && left != null && right != null));

    /// <summary>按确定性方向槽选择表现片；0 返回空，由播放者恢复 Action 原动画。</summary>
    public AnimationClip ResolveClip(int cardinal) => cardinal switch
    {
        1 => forward, 2 => back, 3 => left, 4 => right, _ => null
    };

    /// <summary>同动作内的最短方向驻留；松手立即恢复原动画，冻结时调用方不推进。</summary>
    public int AdvanceState(int state, int frame, int proposed)
    {
        int current = ActionInputMoveState.Cardinal(state);
        int start = ActionInputMoveState.StartFrame(state);
        if (frame < start || (proposed != current
            && (proposed == 0 || current == 0 || frame - start >= minimumDirectionFrames)))
            return ActionInputMoveState.Pack(proposed, frame);
        return state;
    }

    /// <summary>采样当前 Action 动画段的片内时间，保留裁剪起点及 Observer 的小数帧。</summary>
    public static float SampleActionSegmentTime(ActionDefinition action, float actionFrame)
    {
        if (action == null) return 0f;
        int frame = Mathf.FloorToInt(Mathf.Max(0f, actionFrame));
        return action.GetLocalTimeInSegment(frame) + (Mathf.Max(0f, actionFrame) - frame) / ActionSim.LogicHz;
    }

    /// <summary>同步模式跟随原动画段；独立模式才使用方向起始帧和 LoopMove，均不改变动作到期帧。</summary>
    public float SampleTime(ActionDefinition action, int state, float actionFrame)
    {
        int direction = ActionInputMoveState.Cardinal(state);
        AnimationClip clip = ResolveClip(direction);
        if (clip == null) return 0;
        if (animationTimeMode == AnimationTimeMode.FollowActionSegment)
            return Mathf.Clamp(SampleActionSegmentTime(action, actionFrame), 0f, clip.length);
        float seconds = Mathf.Max(0, actionFrame - ActionInputMoveState.StartFrame(state)) / ActionSim.LogicHz;
        return loopMove && clip.length > 0 ? seconds % clip.length
            : Mathf.Min(seconds, Mathf.Max(0, clip.length - 1f / ActionSim.LogicHz));
    }
}
