using System;

/// <summary>统一连接 Vitality 边沿、反应解析与 CharacterActor，供玩家和敌人复用。</summary>
public sealed class CharacterReactionService : IDisposable
{
    readonly CharacterVitality _vitality;
    readonly CharacterActor _actor;
    readonly CharacterReactionResolver _resolver;
    readonly Action<ActionHitContext> _hitSideEffect;
    readonly Action<ActionHitContext, float> _deathSideEffect;

    /// <summary>绑定 Vitality 事件；副作用委托仅用于 EnemyBrain 等上层状态同步。</summary>
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

    /// <summary>先同步上层受击状态，再把已解析请求交给 Actor。</summary>
    void OnHitReceived(ActionHitContext context)
    {
        _hitSideEffect?.Invoke(context);
        CharacterReactionRequest request = _resolver.ResolveHit(in context);
        _actor.EnterHit(in request);
    }

    /// <summary>先同步上层死亡状态，再把已解析请求交给 Actor。</summary>
    void OnDied(ActionHitContext context, float damage)
    {
        _deathSideEffect?.Invoke(context, damage);
        CharacterReactionRequest request = _resolver.ResolveDeath(in context);
        _actor.EnterDeath(in request);
    }
}
