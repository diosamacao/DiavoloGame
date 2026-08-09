using UnityEngine;

/// <summary>直线朝向目标的寻路占位实现。</summary>
public sealed class StraightPathQuery : IEnemyPathQuery
{
    /// <summary>直接返回已归一化的平面朝向目标方向。</summary>
    public Vector3 GetSteerDirection(
        Vector3 selfPosition,
        Vector3 targetPosition,
        Vector3 planarDirectionToTarget)
    {
        return planarDirectionToTarget;
    }
}
