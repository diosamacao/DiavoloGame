using UnityEngine;

/// <summary>
/// FaceTarget 朝向源：只在 Profile 声明 FaceTarget 时由 Context 消费 SelectedTarget 方向。
/// </summary>
public sealed class LocomotionFacingTargetSource : ILocomotionFacingTargetSource
{
    readonly CharacterTargetingState _targetingState;
    readonly CharacterMotorSim _motor;

    /// <summary>装配唯一目标状态与请求者逻辑电机。</summary>
    public LocomotionFacingTargetSource(
        CharacterTargetingState targetingState,
        CharacterMotorSim motor)
    {
        _targetingState = targetingState;
        _motor = motor;
    }

    /// <inheritdoc />
    public bool TryGetFacingWorldDirection(out Vector3 planarForward)
    {
        planarForward = Vector3.zero;
        return _targetingState != null
            && _targetingState.TryGetSelectedDirection(_motor, out planarForward);
    }
}
