using UnityEngine;

/// <summary>
/// 冷却门：表未就绪则 Failure；普通行为成功立即写 CD，CombatRequest 先暂存至起手确认。
/// basic_attack 另阻塞 AttackConfirmPending，与 CooldownReady 一致。
/// </summary>
public sealed class CooldownGateNode : IBehaviorNode
{
    readonly string _cooldownId;
    readonly int _cooldownFrames;
    readonly IBehaviorNode _child;

    /// <summary>创建冷却门装饰。</summary>
    public CooldownGateNode(string cooldownId, int cooldownFrames, IBehaviorNode child)
    {
        _cooldownId = string.IsNullOrEmpty(cooldownId) ? EnemyCooldownIds.Dodge : cooldownId;
        _cooldownFrames = Mathf.Max(0, cooldownFrames);
        _child = child ?? throw new System.ArgumentNullException(nameof(child));
    }

    /// <inheritdoc />
    public BehaviorStatus Tick(EnemyBlackboard blackboard)
    {
        if (blackboard?.Cooldowns == null)
        {
            _child.Reset();
            return BehaviorStatus.Failure;
        }

        if (!EnemyCooldownIds.IsGateReady(blackboard.Cooldowns, _cooldownId))
        {
            // 与条件装饰一致：门未开时 Abort Self
            _child.Reset();
            return BehaviorStatus.Failure;
        }

        // 起手确认期内禁止再进 basic_attack，避免每帧 Pulse
        if (_cooldownId == EnemyCooldownIds.BasicAttack && blackboard.AttackConfirmPending)
        {
            _child.Reset();
            return BehaviorStatus.Failure;
        }

        BehaviorStatus status = _child.Tick(blackboard);
        if (status == BehaviorStatus.Success && _cooldownFrames > 0)
        {
            // Request 叶的 Success 仅代表“已提交”，真实起手由下一帧 Brain 观测 Action 后确认。
            if (blackboard.HasCombatRequest)
                blackboard.Cooldowns.Stage(_cooldownId, _cooldownFrames);
            else
                blackboard.Cooldowns.Set(_cooldownId, _cooldownFrames);
        }
        return status;
    }

    /// <inheritdoc />
    public void Reset() => _child.Reset();
}
