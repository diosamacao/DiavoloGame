using UnityEngine;

/// <summary>应用一次命中：回调受击方、通知攻击者命中确认，并广播跨系统命中事件。</summary>
public sealed class ApplyHitCommand : ArchitectureCommandBase
{
    readonly ActionHitContext _context;
    readonly IHurtboxTarget _target;
    readonly IActionHitReceiver _hitReceiver;
    readonly Transform _targetTransform;

    /// <summary>创建命中应用命令；调用方只负责提供检测结果。</summary>
    public ApplyHitCommand(
        ActionHitContext context,
        IHurtboxTarget target,
        IActionHitReceiver hitReceiver,
        Transform targetTransform)
    {
        _context = context;
        _target = target;
        _hitReceiver = hitReceiver;
        _targetTransform = targetTransform;
    }

    /// <summary>执行命中结算；IHurtboxTarget 负责把上下文换算为自身伤害与受击响应。</summary>
    protected override void OnExecute()
    {
        if (_context.Action == null)
            return;

        // 即使未来新增非 HitDetector 命中入口，也不能让角色自身层级产生伤害与反馈事件。
        if (IsSameHierarchy(_context.Attacker, _targetTransform))
            return;

        _target?.OnHit(in _context);
        _hitReceiver?.NotifyHit(in _context);

        Vector3 direction = ResolveHitDirection(_context.Attacker, _targetTransform);
        this.SendEvent(new AttackHitEvent(_context, _targetTransform, direction));
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

    /// <summary>判断攻击者与目标是否属于同一角色层级。</summary>
    static bool IsSameHierarchy(Transform attacker, Transform target)
    {
        if (attacker == null || target == null)
            return false;

        return target == attacker
            || target.IsChildOf(attacker)
            || attacker.IsChildOf(target);
    }
}
