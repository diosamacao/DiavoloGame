/// <summary>把已完成权威结算的只读命中结果发布给 App 表现订阅者。</summary>
public sealed class PublishAttackHitCommand : ArchitectureCommandBase
{
    readonly ResolvedCombatHit _hit;

    /// <summary>创建帧末命中表现发布命令；不得通过该命令回写模拟状态。</summary>
    public PublishAttackHitCommand(ResolvedCombatHit hit)
    {
        _hit = hit;
    }

    /// <summary>广播镜头、动画、VFX 与音频可消费的只读命中事件。</summary>
    protected override void OnExecute()
    {
        this.SendEvent(new AttackHitEvent(
            _hit.Context,
            _hit.TargetTransform,
            _hit.HitDirection,
            _hit.HitPoint,
            _hit.AbsorbedByPerfectDodge));
    }
}
