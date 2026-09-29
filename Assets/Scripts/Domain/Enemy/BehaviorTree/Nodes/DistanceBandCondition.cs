using System;
using UnityEngine;

/// <summary>
/// 条件装饰：带滞回与最短驻留的距离带；状态在本实例上，Reset 清 latch/dwell。
/// Chase 用 OutsideFar（exit&lt;enter）；Strafe 用 InsideBand；Attack 勿套本节点。
/// </summary>
public sealed class DistanceBandCondition : ConditionalDecoratorNode
{
    readonly DistanceBandMode _mode;
    readonly float _enterDistance;
    readonly float _exitDistance;
    readonly int _minDwellFrames;

    bool _latched;
    int _dwellFrames;

    /// <summary>创建距离滞回装饰。</summary>
    public DistanceBandCondition(
        DistanceBandMode mode,
        float enterDistance,
        float exitDistance,
        int minDwellFrames,
        IBehaviorNode child) : base(child)
    {
        _mode = mode;
        _enterDistance = Mathf.Max(0f, enterDistance);
        _exitDistance = Mathf.Max(0f, exitDistance);
        _minDwellFrames = Mathf.Max(0, minDwellFrames);
    }

    /// <summary>当前是否已锁在带内（单测/调试）。</summary>
    public bool IsLatched => _latched;

    /// <summary>已驻留帧数（单测/调试）。</summary>
    public int DwellFrames => _dwellFrames;

    /// <inheritdoc />
    protected override bool Evaluate(EnemyBlackboard blackboard)
    {
        if (blackboard == null || !blackboard.HasTarget)
        {
            _latched = false;
            _dwellFrames = 0;
            return false;
        }

        float distance = blackboard.PlanarDistance;
        if (!_latched)
        {
            if (ShouldEnter(distance))
            {
                _latched = true;
                _dwellFrames = 0;
            }
        }
        else
        {
            // 已入带：累加驻留；满足离开阈值且 dwell 满才翻面
            _dwellFrames++;
            if (ShouldLeave(distance) && _dwellFrames >= _minDwellFrames)
            {
                _latched = false;
                _dwellFrames = 0;
            }
        }

        return _latched;
    }

    /// <inheritdoc />
    public override void Reset()
    {
        _latched = false;
        _dwellFrames = 0;
        base.Reset();
    }

    bool ShouldEnter(float distance)
    {
        switch (_mode)
        {
            case DistanceBandMode.OutsideFar:
                return distance > _enterDistance;
            case DistanceBandMode.OutsideNear:
                return distance < _enterDistance;
            case DistanceBandMode.InsideBand:
                return distance >= Mathf.Min(_enterDistance, _exitDistance)
                       && distance <= Mathf.Max(_enterDistance, _exitDistance);
            default:
                return false;
        }
    }

    bool ShouldLeave(float distance)
    {
        switch (_mode)
        {
            case DistanceBandMode.OutsideFar:
                // Chase：exit 通常 &lt; enter；贴近后离开追击支
                return distance <= _exitDistance;
            case DistanceBandMode.OutsideNear:
                return distance >= _exitDistance;
            case DistanceBandMode.InsideBand:
                float lo = Mathf.Min(_enterDistance, _exitDistance);
                float hi = Mathf.Max(_enterDistance, _exitDistance);
                return distance < lo || distance > hi;
            default:
                return true;
        }
    }
}
