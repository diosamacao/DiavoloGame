using System;
using System.Collections.Generic;

/// <summary>以调用方稳定键为唯一真源，登记并查找角色网络原型。</summary>
public sealed class CharacterArchetypeCatalog
{
    readonly Dictionary<string, CharacterArchetype> _byKey =
        new Dictionary<string, CharacterArchetype>(StringComparer.Ordinal);
    readonly Dictionary<NetArchetypeId, CharacterArchetype> _byId =
        new Dictionary<NetArchetypeId, CharacterArchetype>();

    /// <summary>当前已登记的角色原型数量。</summary>
    public int Count => _byKey.Count;

    /// <summary>登记稳定键与角色类别；拒绝空键、重复键和 FNV-1a 碰撞。</summary>
    public CharacterArchetype Register(string stableKey, ReplicationActorKind kind)
    {
        if (string.IsNullOrWhiteSpace(stableKey))
            throw new ArgumentException("角色原型 stableKey 不能为空。", nameof(stableKey));
        if (_byKey.ContainsKey(stableKey))
            throw new InvalidOperationException($"角色原型 stableKey '{stableKey}' 已注册。");

        var id = new NetArchetypeId(ComputeStableId(stableKey));
        if (_byId.TryGetValue(id, out CharacterArchetype collision))
        {
            // 不能用线性探测消解碰撞，否则同一 key 的 Id 会依赖注册顺序。
            throw new InvalidOperationException(
                $"角色原型哈希碰撞：'{stableKey}' 与 '{collision.StableKey}' 均映射到 {id.Value}。");
        }

        var archetype = new CharacterArchetype(stableKey, id, kind);
        _byKey.Add(stableKey, archetype);
        _byId.Add(id, archetype);
        return archetype;
    }

    /// <summary>按稳定键查找不可变原型描述；空键或未登记时返回 false。</summary>
    public bool TryGet(string stableKey, out CharacterArchetype archetype)
    {
        if (string.IsNullOrEmpty(stableKey))
        {
            archetype = null;
            return false;
        }

        return _byKey.TryGetValue(stableKey, out archetype);
    }

    /// <summary>按网络原型标识查找不可变原型描述；无效或未登记时返回 false。</summary>
    public bool TryGet(NetArchetypeId id, out CharacterArchetype archetype)
    {
        if (!id.IsValid)
        {
            archetype = null;
            return false;
        }

        return _byId.TryGetValue(id, out archetype);
    }

    /// <summary>使用与 ActionReplicationCatalog 相同的 FNV-1a 32-bit 规则生成正整数 Id。</summary>
    public static int ComputeStableId(string stableKey)
    {
        if (string.IsNullOrWhiteSpace(stableKey))
            throw new ArgumentException("角色原型 stableKey 不能为空。", nameof(stableKey));

        unchecked
        {
            int hash = (int)2166136261u;
            for (int i = 0; i < stableKey.Length; i++)
                hash = (hash ^ stableKey[i]) * 16777619;
            if (hash == int.MinValue)
                hash = 1;
            int id = Math.Abs(hash);
            return id == 0 ? 1 : id;
        }
    }
}
