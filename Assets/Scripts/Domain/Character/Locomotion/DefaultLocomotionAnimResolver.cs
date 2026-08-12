using UnityEngine;

/// <summary>
/// L-DIR1/2 选片：DirectionModel → AnimSet；起步/循环回退均在 AnimSet。
/// </summary>
public sealed class DefaultLocomotionAnimResolver : ILocomotionAnimResolver
{
    /// <summary>与 DirectionModel 默认死区一致。</summary>
    public const float LateralDominanceEpsilon = LocomotionDirectionModel.DefaultEpsilon;

    readonly LocomotionAnimSet _animSet;
    readonly float _cardinalEpsilon;

    /// <summary>使用给定 AnimSet；空则默认表。</summary>
    public DefaultLocomotionAnimResolver(
        LocomotionAnimSet animSet = null,
        float cardinalEpsilon = LateralDominanceEpsilon)
    {
        _animSet = animSet ?? LocomotionAnimSet.CreateDefault();
        _cardinalEpsilon = Mathf.Max(0.01f, cardinalEpsilon);
    }

    /// <inheritdoc />
    public AnimationKey Resolve(
        LocomotionGait gait,
        Vector2 localMoveIntent,
        ILocomotionAnimClipQuery clips)
    {
        MoveCardinal cardinal = LocomotionDirectionModel.Resolve(localMoveIntent, _cardinalEpsilon);
        if (cardinal == MoveCardinal.None)
            cardinal = MoveCardinal.Forward;
        return _animSet.ResolveLoop(gait, cardinal, clips);
    }
}
