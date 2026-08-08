using System;
using UnityEngine;

/// <summary>离散位移点事件（Relocate 等）；首版可不接线，供 ActionMotionResolver 编译。</summary>
[Serializable]
public class MotionCommandNotify : ActionNotify
{
    [SerializeField] MotionCommandType commandType = MotionCommandType.RelocateBehindTarget;
    [SerializeField] MotionTargetSource targetSource = MotionTargetSource.ActionTarget;
    [SerializeField] int behindDistanceMm = 1000;
    [SerializeField] Vector3 localOffsetMm = Vector3.zero;
    [SerializeField] MotionFacingPolicy facingPolicy = MotionFacingPolicy.FaceTarget;
    [SerializeField] MotionCollisionPolicy collisionPolicy = MotionCollisionPolicy.IgnoreCharacters;
    [SerializeField] MotionFallbackPolicy fallbackPolicy = MotionFallbackPolicy.CancelCommand;
    [SerializeField] int forwardFallbackMm = 800;
    [SerializeField] int softBodySuppressFrames = 8;
    [SerializeField] bool preserveVertical = true;

    /// <summary>指令类型。</summary>
    public MotionCommandType CommandType => commandType;

    /// <summary>目标来源。</summary>
    public MotionTargetSource TargetSource => targetSource;

    /// <summary>绕背距离（毫米）。</summary>
    public int BehindDistanceMm => behindDistanceMm;

    /// <summary>相对目标局部偏移（毫米）。</summary>
    public Vector3 LocalOffsetMm => localOffsetMm;

    /// <summary>朝向策略。</summary>
    public MotionFacingPolicy FacingPolicy => facingPolicy;

    /// <summary>碰撞策略。</summary>
    public MotionCollisionPolicy CollisionPolicy => collisionPolicy;

    /// <summary>失败回退。</summary>
    public MotionFallbackPolicy FallbackPolicy => fallbackPolicy;

    /// <summary>前向回退距离（毫米）。</summary>
    public int ForwardFallbackMm => forwardFallbackMm;

    /// <summary>落地后软体抑制帧。</summary>
    public int SoftBodySuppressFrames => Mathf.Max(0, softBodySuppressFrames);

    /// <summary>是否保持竖直坐标。</summary>
    public bool PreserveVertical => preserveVertical;
}
