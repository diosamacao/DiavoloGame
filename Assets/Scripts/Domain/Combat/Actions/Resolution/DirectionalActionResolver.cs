using UnityEngine;

/// <summary>方向动作解析：按闪避输入方向与角色朝向在前/后/左/右动作间分派，替代旧 DodgeDirectionVariants。</summary>
[CreateAssetMenu(fileName = "DirectionalActionResolver", menuName = "ACT/Combat/Resolvers/Directional Action Resolver")]
public class DirectionalActionResolver : ActionResolver
{
    [Tooltip("无方向输入或方向动作缺失时的回退动作（通常是原地/后闪根动作）。")]
    [SerializeField] ActionDefinition defaultAction;
    [Tooltip("Cancel 窗口内输入与朝向夹角超过该阈值时优先走左右。")]
    [SerializeField] float sideThresholdDeg = 80f;
    [Tooltip("Locomotion 起手前闪时是否先朝输入方向转向。")]
    [SerializeField] bool rotateToInputOnForward = true;
    [SerializeField] ActionDefinition forwardAction;
    [SerializeField] ActionDefinition backwardAction;
    [SerializeField] ActionDefinition leftAction;
    [SerializeField] ActionDefinition rightAction;

    public override bool TryResolve(
        in ActionRequest request,
        in ActionResolveContext context,
        out ActionResolveResult result)
    {
        ActionDefinition variant = ResolveVariant(in context);
        if (TryFinalize(variant, out ActionDefinition action))
        {
            result = ActionResolveResult.FromAction(action);
            return true;
        }

        result = default;
        return false;
    }

    /// <summary>按来源与方向选出方向变体；无法判定方向时回退后闪。</summary>
    ActionDefinition ResolveVariant(in ActionResolveContext context)
    {
        IActionStartContext startContext = context.StartContext;
        if (startContext == null || !TryGetIntentDirection(startContext, out Vector3 intentDirection))
            return backwardAction;

        if (!TryGetPlanarForward(context.ActorRoot, out Vector3 actorForward))
            return backwardAction;

        // Locomotion 起手偏前闪，并可先朝输入方向转向。
        if (context.Origin == ActionResolveOrigin.LocomotionStart)
        {
            if (rotateToInputOnForward)
                startContext.FaceWorldDirection(intentDirection);

            return forwardAction;
        }

        // Cancel 窗口：先按阈值分左右，再按前后。
        float angle = Vector3.Angle(actorForward, intentDirection);
        if (angle > Mathf.Clamp(sideThresholdDeg, 0f, 180f))
        {
            float crossY = Vector3.Cross(actorForward, intentDirection).y;
            return crossY >= 0f ? rightAction : leftAction;
        }

        float dot = Vector3.Dot(actorForward, intentDirection);
        return dot >= 0f ? forwardAction : backwardAction;
    }

    /// <summary>方向动作缺失时回退 defaultAction；仍无效则解析失败，不启动动作。</summary>
    bool TryFinalize(ActionDefinition variant, out ActionDefinition action)
    {
        if (variant != null && variant.HasAnimation)
        {
            action = variant;
            return true;
        }

        action = defaultAction;
        return action != null && action.HasAnimation;
    }

    /// <summary>读取闪避判定方向并投影到 XZ 平面；无有效方向时返回 false。</summary>
    static bool TryGetIntentDirection(IActionStartContext startContext, out Vector3 direction)
    {
        direction = Vector3.zero;
        if (!startContext.TryGetDodgeIntentDirection(out Vector3 resolved))
            return false;

        return TryNormalizePlanar(resolved, out direction);
    }

    /// <summary>读取角色平面朝向；朝向异常（近零）时返回 false。</summary>
    static bool TryGetPlanarForward(Transform actorRoot, out Vector3 forward)
    {
        forward = actorRoot != null ? actorRoot.forward : Vector3.forward;
        return TryNormalizePlanar(forward, out forward);
    }

    /// <summary>将向量投影到 XZ 平面并单位化；长度过小返回 false。</summary>
    static bool TryNormalizePlanar(Vector3 source, out Vector3 normalized)
    {
        source.y = 0f;
        if (source.sqrMagnitude < 0.0001f)
        {
            normalized = Vector3.zero;
            return false;
        }

        normalized = source.normalized;
        return true;
    }
}
