using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>DistanceBand 滞回条件装饰定义；Chase/Strafe 带，Attack 勿套。</summary>
[Serializable]
public sealed class DistanceBandConditionDef : EnemyBehaviorConditionNodeDef
{
    [SerializeField] DistanceBandMode mode = DistanceBandMode.OutsideFar;
    [SerializeField] float enterDistance = 4f;
    [SerializeField] float exitDistance = 3f;
    [SerializeField] float minDwellSeconds = 0.1f;

    /// <summary>滞回带模式。</summary>
    public DistanceBandMode Mode
    {
        get => mode;
        set => mode = value;
    }

    /// <summary>进入本支的距离阈值（米）。</summary>
    public float EnterDistance
    {
        get => enterDistance;
        set => enterDistance = Mathf.Max(0f, value);
    }

    /// <summary>离开本支的距离阈值（米）；Chase/OutsideFar 宜 &lt; enter。</summary>
    public float ExitDistance
    {
        get => exitDistance;
        set => exitDistance = Mathf.Max(0f, value);
    }

    /// <summary>最短驻留秒数；满后才允许因距离翻面失败。</summary>
    public float MinDwellSeconds
    {
        get => minDwellSeconds;
        set => minDwellSeconds = Mathf.Max(0f, value);
    }

    /// <inheritdoc />
    public override IBehaviorNode Build() =>
        Wrap(new DistanceBandCondition(
            mode,
            enterDistance,
            exitDistance,
            EnemyBehaviorTime.SecondsToFrames(minDwellSeconds),
            BuildChild()));
}
