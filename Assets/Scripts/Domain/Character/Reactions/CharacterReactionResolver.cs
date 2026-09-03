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
    /// 按打断等级 / 抗打断 / 期望档裁定反馈。不切状态、不播动画。
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

        int resist = ClampNonNegative(query.BaseInterruptResist)
            + ClampNonNegative(query.PhaseInterruptResistBonus);
        bool canInterrupt = !query.SuperArmor
            && ClampNonNegative(query.InterruptLevel) >= resist;

        HitReactionKind desired = NormalizeDesired(query.DesiredReaction);
        HitReactionKind actual;
        if (!canInterrupt)
        {
            // 没打断成功：想击飞也最多微颤；期望本就是 None/Flinch 则保持。
            actual = desired <= HitReactionKind.Flinch ? desired : HitReactionKind.Flinch;
        }
        else
        {
            // 等级够：仍尊重期望。None/Flinch 不进 Hit。非致命不得出 Death。
            actual = desired == HitReactionKind.Death
                ? HitReactionKind.Launch
                : desired;
        }

        return Create(actual, query.HitReactionId);
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

    /// <summary>非法期望档回退到旧盒子默认 LightStun。</summary>
    static HitReactionKind NormalizeDesired(HitReactionKind desired)
    {
        if (desired > HitReactionKind.Death)
            return HitReactionKind.LightStun;
        return desired;
    }

    static int ClampNonNegative(int value) => value < 0 ? 0 : value;
}
