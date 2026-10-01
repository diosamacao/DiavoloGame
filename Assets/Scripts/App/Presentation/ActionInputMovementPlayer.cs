using UnityEngine;

/// <summary>本机与 Observer 共用的动作方向播放者；只读状态，绝不重新起手 Action。</summary>
public sealed class ActionInputMovementPlayer
{
    ActionDefinition _action;
    AnimationClip _clip;
    int _state = -1;
    int _segmentIndex = -1;
    ActionInputMovement _window;
    // 每个动作段独占时钟身份，避免 Seek 同步到上一招或上一段的淡出片段。
    object _timeGroup;

    /// <summary>释放上一动作的片段身份，下次首次采样必须重新 Play。</summary>
    public void Reset() { _action = null; _clip = null; _state = -1; _window = null; _segmentIndex = -1; _timeGroup = null; }

    /// <summary>只在动作/方向切换时 Play，每次按同一动作逻辑时钟定位；支持循环与冻结。</summary>
    public void Sample(CharacterAnimationService animation, ActionDefinition action, float frame, int state, bool restart = false, float deltaTime = 0f)
    {
        if (animation == null || action == null) return;
        ActionInputMovement config = action.GetInputMovementAtFrame((int)frame);
        if (config == null) return;
        AnimationClip clip = config.ResolveClip(ActionInputMoveState.Cardinal(state));
        float localTime;
        int segmentIndex = -1;
        if (ActionInputMoveState.Cardinal(state) == 0 || !config.overrideMovementAnimation)
        {
            // 松手恢复当前 Action 帧对应的原片段，不能从头播放或停在离开前的 Pose。
            ActionFrameQueryResult query = ActionFrameQuery.Query(action, (int)frame);
            if (!query.HasAnimationSegment) return;
            clip = query.Segment.clip;
            segmentIndex = query.SegmentIndex;
            localTime = ActionInputMovement.SampleActionSegmentTime(action, frame);
        }
        else
        {
            localTime = config.SampleTime(action, state, frame);
            if (config.animationTimeMode == ActionInputMovement.AnimationTimeMode.FollowActionSegment)
                action.TryGetSegmentAtFrame((int)frame, out segmentIndex, out _, out _);
        }
        if (clip == null) return;
        bool synchronized = config.animationTimeMode == ActionInputMovement.AnimationTimeMode.FollowActionSegment;
        if (!synchronized) _timeGroup = null;
        else if (restart || action != _action || segmentIndex != _segmentIndex || _timeGroup == null)
            _timeGroup = new object();
        if (restart || action != _action || config != _window || state != _state || clip != _clip || segmentIndex != _segmentIndex)
            animation.PlayClip(clip, restart ? 0f : config.crossFadeSeconds, _timeGroup);
        // Tick 推进混合权重与附加层，再按逻辑时钟定位主片，避免混合永远停在旧片。
        if (deltaTime > 0f) animation.Tick(deltaTime);
        animation.SeekClip(localTime);
        _action = action; _clip = clip; _state = state; _window = config;
        _segmentIndex = segmentIndex;
    }
}
