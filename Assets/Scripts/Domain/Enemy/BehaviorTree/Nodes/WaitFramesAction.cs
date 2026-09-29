using UnityEngine;

/// <summary>行动：跨逻辑帧等待（Running → Success）；由 Def 秒数转帧后构造。</summary>
public sealed class WaitFramesAction : IBehaviorNode
{
    readonly int _durationFrames;
    int _remaining;

    /// <summary>创建按逻辑帧等待的节点。</summary>
    public WaitFramesAction(int durationFrames)
    {
        _durationFrames = Mathf.Max(1, durationFrames);
        _remaining = _durationFrames;
    }

    /// <inheritdoc />
    public BehaviorStatus Tick(EnemyBlackboard blackboard)
    {
        _remaining--;
        if (_remaining > 0)
            return BehaviorStatus.Running;
        _remaining = _durationFrames;
        return BehaviorStatus.Success;
    }

    /// <inheritdoc />
    public void Reset() => _remaining = _durationFrames;
}
