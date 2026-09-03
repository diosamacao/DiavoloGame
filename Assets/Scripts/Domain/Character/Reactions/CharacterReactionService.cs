using System;

/// <summary>
/// Vitality 边沿 → 档位裁定 → Actor。Flinch 不停招、不通知树；Stun+ / Death 走原路径。
/// </summary>
public sealed class CharacterReactionService : IDisposable
{
    readonly CharacterVitality _vitality;
    readonly CharacterActor _actor;
    readonly CharacterReactionResolver _resolver;
    readonly Action<ActionHitContext> _hitSideEffect;
    readonly Action<ActionHitContext, float> _deathSideEffect;

    /// <summary>绑定 Vitality 事件；副作用委托仅用于 Stun+ / Death 的上层同步（如 BT Reset）。</summary>
    public CharacterReactionService(
        CharacterVitality vitality,
        CharacterActor actor,
        CharacterReactionResolver resolver,
        Action<ActionHitContext> hitSideEffect = null,
        Action<ActionHitContext, float> deathSideEffect = null)
    {
        _vitality = vitality ?? throw new ArgumentNullException(nameof(vitality));
        _actor = actor ?? throw new ArgumentNullException(nameof(actor));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _hitSideEffect = hitSideEffect;
        _deathSideEffect = deathSideEffect;

        _vitality.HitReceived += OnHitReceived;
        _vitality.Died += OnDied;
    }

    /// <summary>解绑事件；角色销毁前必须调用。</summary>
    public void Dispose()
    {
        _vitality.HitReceived -= OnHitReceived;
        _vitality.Died -= OnDied;
    }

    /// <summary>按 Command 分支：None/Flinch 不进 Hit；Stun+ 才 Notify + EnterHit。</summary>
    void OnHitReceived(ActionHitContext context)
    {
        HitReactionCommand command = _resolver.Resolve(BuildQuery(in context));
        _vitality.ConfirmHitReaction(command.Kind);

        if (command.Kind == HitReactionKind.None)
            return;

        if (command.Kind == HitReactionKind.Flinch)
        {
            _actor.IssueFlinch(command.FlinchKey, in context);
            return;
        }

        _hitSideEffect?.Invoke(context);
        _actor.EnterHit(new CharacterReactionRequest(command.StunFrames, command.StunAction));
    }

    /// <summary>先同步上层死亡状态，再把已解析请求交给 Actor。</summary>
    void OnDied(ActionHitContext context, float damage)
    {
        _vitality.ConfirmHitReaction(HitReactionKind.Death);
        _deathSideEffect?.Invoke(context, damage);
        CharacterReactionRequest request = _resolver.ResolveDeath(in context);
        _actor.EnterDeath(in request);
    }

    /// <summary>从当前招式窗读 SuperArmor；无敌/吞伤由管道早退，这里不再判。</summary>
    HitReactionResolveQuery BuildQuery(in ActionHitContext context)
    {
        bool superArmor = false;
        if (_actor.ActionSim != null && _actor.ActionSim.IsActive)
        {
            ActionSimSnapshot snap = _actor.ActionSim.Snapshot;
            if (snap.Content is ActionDefinition action)
                superArmor = action.IsSuperArmorAtFrame(snap.CurrentFrame);
        }

        return HitReactionResolveQuery.FromHitContext(
            in context,
            superArmor: superArmor);
    }
}
