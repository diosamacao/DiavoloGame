using System;
using UnityEngine;

/// <summary>战斗命中反馈路由：将 Hitbox 命中事件分发给镜头震动等子系统。</summary>
public static class CombatHitFeedback
{
    /// <summary>攻击命中时广播；由 CameraShakeController 等订阅，Combat 层不直接依赖 Camera。</summary>
    public static event Action<ActionDefinition, Vector3> AttackHit;

    /// <summary>攻击命中时广播完整上下文；由 HitStopController 等订阅。</summary>
    public static event Action<ActionHitContext, Transform> AttackHitDetailed;

    /// <summary>攻击命中时触发镜头震动、卡肉等反馈。</summary>
    /// <param name="context">命中上下文。</param>
    /// <param name="targetTransform">受击目标 Transform，用于计算方向；可为 null。</param>
    public static void OnAttackHit(in ActionHitContext context, Transform targetTransform)
    {
        if (context.Action == null)
            return;

        Vector3 direction = ResolveHitDirection(context.Attacker, targetTransform);
        AttackHitDetailed?.Invoke(context, targetTransform);
        AttackHit?.Invoke(context.Action, direction);
    }

    /// <summary>攻击者指向受击者的水平方向；缺省为攻击者 forward。</summary>
    static Vector3 ResolveHitDirection(Transform attacker, Transform targetTransform)
    {
        if (attacker == null)
            return Vector3.forward;

        if (targetTransform != null)
        {
            Vector3 toTarget = targetTransform.position - attacker.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.0001f)
                return toTarget.normalized;
        }

        Vector3 forward = attacker.forward;
        forward.y = 0f;
        return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
    }
}

/// <summary>卡肉（Hit Stop）全局状态桥接：广播开始/结束，供 VfxPooledInstance 等订阅。</summary>
public static class CombatHitStop
{
    /// <summary>当前是否处于卡肉中。</summary>
    public static bool IsActive { get; private set; }

    /// <summary>当前卡肉对应的攻击者根 Transform。</summary>
    public static Transform ActiveAttackerRoot { get; private set; }

    /// <summary>卡肉开始；attackerRoot 为攻击者角色根节点。</summary>
    public static event Action<Transform> Began;

    /// <summary>卡肉结束。</summary>
    public static event Action Ended;

    /// <summary>由 HitStopController 调用：进入卡肉并广播。</summary>
    internal static void NotifyBegan(Transform attackerRoot)
    {
        IsActive = true;
        ActiveAttackerRoot = attackerRoot;
        Began?.Invoke(attackerRoot);
    }

    /// <summary>由 HitStopController 调用：退出卡肉并广播。</summary>
    internal static void NotifyEnded()
    {
        if (!IsActive)
            return;

        IsActive = false;
        ActiveAttackerRoot = null;
        Ended?.Invoke();
    }
}
