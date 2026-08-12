using UnityEngine;

/// <summary>
/// Locomotion FaceTarget 朝向单一入口（L-DIR3）；禁止 State 内 if(lockOn)。
/// </summary>
public interface ILocomotionFacingTargetSource
{
    /// <summary>取水平朝向目标方向；无有效目标时 false。</summary>
    bool TryGetFacingWorldDirection(out Vector3 planarForward);
}
