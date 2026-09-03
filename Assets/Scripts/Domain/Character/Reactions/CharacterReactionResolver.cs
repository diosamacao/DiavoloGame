/// <summary>
/// 角色反应解析器。命中走 <see cref="Resolve"/> 出 Command；
/// <see cref="ResolveHit"/> / <see cref="ResolveDeath"/> 仅给快照硬吸与死亡选招。
/// </summary>
public sealed class CharacterReactionResolver
{
    readonly CharacterReactionSet _reactionSet;

    /// <summary>使用角色配置中的反应映射创建解析器。</summary>
    public CharacterReactionResolver(CharacterReactionSet reactionSet)
    {
        _reactionSet = reactionSet ?? new CharacterReactionSet();
    }

    /// <summary>
    /// 按冲击力对韧性裁定反馈。不切状态、不播动画，不读 desiredReaction。
    /// </summary>
    public HitReactionCommand Resolve(in HitReactionResolveQuery query)
    {
        if (query.IsDead || query.IsFatal)
            return CreateDeath(query.HitReactionId);

        if (query.IsInvincible || query.AbsorbedByPerfectDodge)
            return HitReactionCommand.None;

        // DOT 与无 Hitbox 的数值伤没有打击语义，不进档位。
        if (query.IsDot || !query.HasHitPayload)
            return HitReactionCommand.None;

        HitReactionKind actual = ResolveKind(query.Impact, query.Toughness, query.SuperArmor);
        return Create(actual, query.HitReactionId);
    }

    /// <summary>
    /// 冲击力对韧性：不足则 Flinch；持平起 LightStun；超出 2 HeavyStun；超出 4 Launch。
    /// SuperArmor 非死最多 Flinch。
    /// </summary>
    public static HitReactionKind ResolveKind(int impact, int toughness, bool superArmor)
    {
        if (superArmor)
            return HitReactionKind.Flinch;

        int safeImpact = impact < 0 ? 0 : impact;
        int safeToughness = toughness < 0 ? 0 : toughness;
        int excess = safeImpact - safeToughness;
        if (excess < 0)
            return HitReactionKind.Flinch;
        if (excess < HitReactionResolveQuery.HeavyStunExcess)
            return HitReactionKind.LightStun;
        if (excess < HitReactionResolveQuery.LaunchExcess)
            return HitReactionKind.HeavyStun;
        return HitReactionKind.Launch;
    }

    /// <summary>解析一次非致命命中；默认帧数同时作为反应动作启动失败时的硬直回退。</summary>
    public CharacterReactionRequest ResolveHit(in ActionHitContext context)
    {
        string reactionId = context.Hitbox?.Payload.HitReactionId;
        ActionDefinition action = _reactionSet.Resolve(CharacterReactionType.Hit, reactionId);
        return new CharacterReactionRequest(_reactionSet.DefaultHitStunFrames, action);
    }

    /// <summary>解析一次致命命中的死亡表现请求。</summary>
    public CharacterReactionRequest ResolveDeath(in ActionHitContext context)
    {
        string reactionId = context.Hitbox?.Payload.HitReactionId;
        ActionDefinition action = _reactionSet.Resolve(CharacterReactionType.Death, reactionId);
        return new CharacterReactionRequest(0, action);
    }

    /// <summary>按已裁定档填 Action / 帧数 / Flinch 键。</summary>
    HitReactionCommand Create(HitReactionKind kind, string reactionId)
    {
        if (kind == HitReactionKind.None)
            return HitReactionCommand.None;

        if (kind == HitReactionKind.Flinch)
        {
            return new HitReactionCommand(
                HitReactionKind.Flinch,
                stunAction: null,
                stunFrames: 0,
                AnimationKey.HitShake);
        }

        if (kind == HitReactionKind.Death)
            return CreateDeath(reactionId);

        ActionDefinition stunAction = _reactionSet.Resolve(CharacterReactionType.Hit, reactionId);
        return new HitReactionCommand(
            kind,
            stunAction,
            _reactionSet.DefaultHitStunFrames,
            AnimationKey.Idle);
    }

    /// <summary>死亡档：查 Death 规则，硬直帧为 0。</summary>
    HitReactionCommand CreateDeath(string reactionId)
    {
        ActionDefinition deathAction = _reactionSet.Resolve(CharacterReactionType.Death, reactionId);
        return new HitReactionCommand(
            HitReactionKind.Death,
            deathAction,
            stunFrames: 0,
            AnimationKey.Idle);
    }
}
