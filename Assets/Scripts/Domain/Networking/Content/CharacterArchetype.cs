/// <summary>描述一个由稳定业务键确定的不可变角色网络原型。</summary>
public sealed class CharacterArchetype
{
    /// <summary>创建仅由 Catalog 暴露的不可变原型描述。</summary>
    internal CharacterArchetype(
        string stableKey,
        NetArchetypeId netArchetypeId,
        ReplicationActorKind kind)
    {
        StableKey = stableKey;
        NetArchetypeId = netArchetypeId;
        Kind = kind;
    }

    /// <summary>由内容生产方提供且跨进程一致的唯一稳定键。</summary>
    public string StableKey { get; }

    /// <summary>稳定键经 FNV-1a 生成的正整数网络原型标识。</summary>
    public NetArchetypeId NetArchetypeId { get; }

    /// <summary>该原型对应的角色复制类别。</summary>
    public ReplicationActorKind Kind { get; }
}
