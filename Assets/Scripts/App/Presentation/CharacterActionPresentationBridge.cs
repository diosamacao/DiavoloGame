using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>只读解释 ActionSim 事件与快照，驱动动画、Timeline Notify 和视觉残差。</summary>
public sealed class CharacterActionPresentationBridge : IActionPresentationSink
{
    readonly Transform _actorRoot;
    readonly CharacterAnimationService _animation;
    readonly CharacterRootMotionDriver _rootMotion;
    readonly List<IActionNotifyConsumer> _notifyConsumers = new();
    readonly ActionTimelineRunner _timelineRunner;
    readonly ICharacterPresentationSink _characterPresentation;
    Transform _defaultAttachPoint;
    ActionDefinition _animationAction;
    int _animationSegmentIndex = -1;
    bool _hitStopPresentationActive;
    float _normalAnimationSpeed = 1f;

    /// <summary>创建动作表现桥；方法输入仅接受模拟事件或快照。</summary>
    public CharacterActionPresentationBridge(
        Transform actorRoot,
        CharacterAnimationService animation,
        CharacterRootMotionDriver rootMotion,
        ActionTimelineRunner timelineRunner,
        Transform defaultAttachPoint,
        ICharacterPresentationSink characterPresentation)
    {
        _actorRoot = actorRoot;
        _animation = animation;
        _rootMotion = rootMotion;
        _timelineRunner = timelineRunner ?? new ActionTimelineRunner();
        _defaultAttachPoint = defaultAttachPoint != null ? defaultAttachPoint : actorRoot;
        _characterPresentation = characterPresentation;
    }

    /// <summary>注册统一时间轴通知消费者；同一实例不会重复注册。</summary>
    public void RegisterNotifyConsumer(IActionNotifyConsumer consumer)
    {
        if (consumer != null && !_notifyConsumers.Contains(consumer))
            _notifyConsumers.Add(consumer);
    }

    /// <inheritdoc />
    public void ResetForVisibilityLoss()
    {
        for (int i = 0; i < _notifyConsumers.Count; i++)
        {
            if (_notifyConsumers[i] is IActionVisibilityResetConsumer resettable)
                resettable.ResetForVisibilityLoss();
        }

        _animation?.StopAdditive();
    }

    /// <inheritdoc />
    public void ConsumeFlinch(AnimationKey key, in ActionHitContext context)
    {
        if (_animation == null || !_animation.HasPlayback)
            return;

        _animation.TryPlayAdditive(key, mask: null, fadeDuration: 0.05f);
    }

    /// <summary>更新时间轴默认挂点；空值回退角色根。</summary>
    public void BindDefaultAttachPoint(Transform attachPoint) =>
        _defaultAttachPoint = attachPoint != null ? attachPoint : _actorRoot;

    /// <inheritdoc />
    public void ConsumeEvent(in ActionSimEvent actionEvent)
    {
        switch (actionEvent.Type)
        {
            case ActionSimEventType.Started:
                HandleStarted();
                break;
            case ActionSimEventType.Stopped:
                HandleStopped();
                break;
            case ActionSimEventType.FrameAdvanced:
                DispatchFrameEvent(in actionEvent);
                break;
        }
    }

    /// <inheritdoc />
    public void ApplyBeforeGameplay(in ActionSimSnapshot snapshot, float fixedDeltaSeconds)
    {
        SyncHitStopPresentation(snapshot.IsFrozen);
        if (snapshot.IsActive
            && !snapshot.IsComplete
            && !snapshot.IsFrozen
            && snapshot.Content is ActionDefinition action)
        {
            SyncAnimation(action, snapshot.CurrentFrame);
        }
    }

    /// <inheritdoc />
    public void ApplyAfterGameplay(in ActionSimSnapshot snapshot)
    {
        if (!snapshot.IsActive || snapshot.Content is not ActionDefinition action)
            return;

        int residualFrame = snapshot.CurrentFrame;
        if (residualFrame >= action.TotalFrames)
            residualFrame = Mathf.Max(0, action.TotalFrames - 1);
        _characterPresentation?.CaptureActionFrame(action, residualFrame);
    }

    /// <inheritdoc />
    public void CompleteSimulationStep(in ActionSimSnapshot snapshot, float fixedDeltaSeconds)
    {
        if (fixedDeltaSeconds > 0f)
            _animation?.Tick(fixedDeltaSeconds);
    }

    /// <summary>动作开始时禁止 Animator Root Motion 写入逻辑 Motor。</summary>
    void HandleStarted() => _rootMotion?.SetActive(false);

    /// <summary>结束动作持续表现并让视觉残差平滑回锚。</summary>
    void HandleStopped()
    {
        for (int i = 0; i < _notifyConsumers.Count; i++)
            _notifyConsumers[i].OnActionEnded();

        _rootMotion?.SetActive(false);
        _animationAction = null;
        _animationSegmentIndex = -1;
        SyncHitStopPresentation(frozen: false);
        _characterPresentation?.EndAction(VisualResidualExitPolicy.BlendToZero);
    }

    /// <summary>按整数动作帧解释 Timeline；终止哨兵只派发区间 Exit。</summary>
    void DispatchFrameEvent(in ActionSimEvent actionEvent)
    {
        if (actionEvent.Content is not ActionDefinition action)
            return;

        var context = new CombatFrameContext(
            action,
            actionEvent.Frame,
            actionEvent.PreviousFrame,
            _actorRoot,
            actionEvent.InstanceId);
        if (actionEvent.Frame >= action.TotalFrames)
        {
            _timelineRunner.DispatchTerminalExits(
                in context,
                _defaultAttachPoint,
                _notifyConsumers);
            return;
        }

        SyncAnimation(action, actionEvent.Frame);
        _timelineRunner.Dispatch(in context, _defaultAttachPoint, _notifyConsumers);
    }

    /// <summary>仅在动作或动画段切换时 Play+Seek。</summary>
    void SyncAnimation(ActionDefinition action, int frame)
    {
        ActionFrameQueryResult query = ActionFrameQuery.Query(action, frame);
        if (_animation == null || !query.HasAnimationSegment)
            return;

        int segmentIndex = query.SegmentIndex;
        if (_animationAction == action && _animationSegmentIndex == segmentIndex)
            return;

        ActionAnimationSegment segment = query.Segment;
        _rootMotion?.SetActive(false);
        _animation.PlayClip(segment.clip, action.ResolveSegmentCrossFade(segmentIndex));
        _animation.SeekClip(query.SegmentLocalTime);
        _animationAction = action;
        _animationSegmentIndex = segmentIndex;
    }

    /// <summary>按动作冻结快照启停骨骼动画。</summary>
    void SyncHitStopPresentation(bool frozen)
    {
        if (_animation == null)
            return;

        if (frozen)
        {
            if (_hitStopPresentationActive)
                return;

            _normalAnimationSpeed = _animation.Speed > 0f ? _animation.Speed : 1f;
            _animation.SetSpeed(0f);
            _hitStopPresentationActive = true;
            return;
        }

        if (!_hitStopPresentationActive)
            return;

        _animation.SetSpeed(_normalAnimationSpeed);
        _hitStopPresentationActive = false;
    }
}
