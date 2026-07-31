/// <summary>上层角色反应解析器；把命中语义转换为完整的状态机播放请求。</summary>
public sealed class CharacterReactionResolver
{
    readonly CharacterReactionSet _reactionSet;

    /// <summary>使用角色配置中的反应映射创建解析器。</summary>
    public CharacterReactionResolver(CharacterReactionSet reactionSet)
    {
        _reactionSet = reactionSet ?? new CharacterReactionSet();
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
}
