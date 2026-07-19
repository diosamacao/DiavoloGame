using UnityEngine;

/// <summary>六向动作解析：按角色朝向分派前、后、左前、左后、右前、右后变体。</summary>
[CreateAssetMenu(fileName = "DirectionalActionResolver", menuName = "ACT/Combat/Resolvers/Directional Action Resolver")]
public class DirectionalActionResolver : ActionResolver
{
    [Tooltip("无方向输入或方向动作缺失时的回退动作（通常是原地/后闪根动作）。")]
    [SerializeField] ActionDefinition defaultAction = null;
    [Tooltip("前/后正向扇区的半角；其余方向按左右与前后半区分为四个斜向闪避。")]
    [SerializeField, Range(0f, 89f)] float cardinalSectorHalfAngleDeg = 30f;
    [SerializeField] ActionDefinition forwardAction = null;
    [SerializeField] ActionDefinition backwardAction = null;
    [SerializeField] ActionDefinition forwardLeftAction = null;
    [SerializeField] ActionDefinition backwardLeftAction = null;
    [SerializeField] ActionDefinition forwardRightAction = null;
    [SerializeField] ActionDefinition backwardRightAction = null;

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

    /// <summary>按角色平面朝向把输入划分为两个正向扇区与四个斜向扇区。</summary>
    ActionDefinition ResolveVariant(in ActionResolveContext context)
    {
        IActionStartContext startContext = context.StartContext;
        if (startContext == null || !TryGetIntentDirection(startContext, out Vector3 intentDirection))
            return backwardAction;

        if (!TryGetPlanarForward(context.ActorRoot, out Vector3 actorForward))
            return backwardAction;

        float signedAngle = Vector3.SignedAngle(actorForward, intentDirection, Vector3.up);
        float absoluteAngle = Mathf.Abs(signedAngle);
        float cardinalHalfAngle = Mathf.Clamp(cardinalSectorHalfAngleDeg, 0f, 89f);

        if (absoluteAngle <= cardinalHalfAngle)
            return forwardAction;

        if (absoluteAngle >= 180f - cardinalHalfAngle)
            return backwardAction;

        bool rightSide = signedAngle > 0f;
        // 纯左/右输入位于 90° 边界，统一偏向左前/右前，避免无纵向分量时随机后闪。
        bool forwardHalf = absoluteAngle <= 90f;
        if (rightSide)
            return forwardHalf ? forwardRightAction : backwardRightAction;
        return forwardHalf ? forwardLeftAction : backwardLeftAction;
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
