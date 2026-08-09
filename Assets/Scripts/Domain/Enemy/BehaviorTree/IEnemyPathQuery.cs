using UnityEngine;

/// <summary>敌人追击方向查询预留；首版直线实现，日后可换 NavMesh/A*。</summary>
public interface IEnemyPathQuery
{
    /// <summary>返回水平单位方向；无效时返回 zero。</summary>
    Vector3 GetSteerDirection(Vector3 selfPosition, Vector3 targetPosition, Vector3 planarDirectionToTarget);
}
