/// <summary>
/// 轻受击表现事件。只读；订阅者只能叠 Additive，禁止回写 ActionSim / Locomotion。
/// </summary>
public readonly struct HitFlinchEvent : IArchitectureEvent
{
    /// <summary>创建一次 Flinch 表现通知。</summary>
    public HitFlinchEvent(
        SimActorId targetId,
        AnimationKey flinchKey,
        SimActorId attackerId,
        int actionInstanceId)
    {
        TargetId = targetId;
        FlinchKey = flinchKey;
        AttackerId = attackerId;
        ActionInstanceId = actionInstanceId;
    }

    /// <summary>受击者稳定 Id。</summary>
    public SimActorId TargetId { get; }

    /// <summary>Additive 逻辑键，默认 HitShake。</summary>
    public AnimationKey FlinchKey { get; }

    /// <summary>攻击者 Id，供去重。</summary>
    public SimActorId AttackerId { get; }

    /// <summary>招式会话编号，供去重。</summary>
    public int ActionInstanceId { get; }
}
