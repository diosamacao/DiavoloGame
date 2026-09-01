using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>ACT 联机内容唯一注册表：集中持有动作 Catalog、角色 Archetype 与 Unity 配置绑定。</summary>
public sealed class ActContentRegistry
{
    readonly ActionReplicationCatalog _actions = new();
    readonly CharacterArchetypeCatalog _archetypes = new();
    readonly Dictionary<CharacterConfig, NetArchetypeId> _players = new();
    readonly Dictionary<EnemyDefinition, NetArchetypeId> _enemies = new();
    readonly Dictionary<string, UnityEngine.Object> _ownersByKey =
        new(StringComparer.Ordinal);
    readonly Dictionary<NetArchetypeId, CharacterConfig> _configsById = new();
    readonly Dictionary<NetArchetypeId, ReplicationActorKind> _kindsById = new();
    PartyLoadout _playerLoadout;

    /// <summary>
    /// 当前房间动作 Id 与资产映射。
    /// 仅供 ACT Capture、Owner/Observer 表现依赖注入；Room 不再单独创建 Catalog。
    /// </summary>
    public ActionReplicationCatalog Actions => _actions;

    /// <summary>当前已登记动作数，不含 Id=0。</summary>
    public int ActionCount => _actions.Count;

    /// <summary>当前房间声明的玩家阵容；Dedicated Join 用同一槽序创建稳定实体。</summary>
    public PartyLoadout PlayerLoadout => _playerLoadout;

    /// <summary>复制已登记网络原型 Id，供 Gameplay 指纹哈希。</summary>
    public void CopyArchetypeIds(List<int> results)
    {
        if (results == null)
            throw new ArgumentNullException(nameof(results));
        results.Clear();
        foreach (NetArchetypeId id in _configsById.Keys)
            results.Add(id.Value);
    }

    /// <summary>从角色配置预填动作 Graph、变体与受击反应。</summary>
    public void PrefillActions(CharacterConfig config) => _actions.Prefill(config);

    /// <summary>登记玩家配置；stableKey 固定为 player/{CharacterConfig.name}。</summary>
    public NetArchetypeId RegisterPlayer(CharacterConfig config)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));
        if (_players.TryGetValue(config, out NetArchetypeId existing))
            return existing;

        string stableKey = BuildStableKey("player", config.name, nameof(config));
        NetArchetypeId id = Register(
            stableKey,
            ReplicationActorKind.Player,
            config,
            config);
        _players.Add(config, id);
        return id;
    }

    /// <summary>
    /// 登记当前房间唯一玩家阵容并登记全部非空角色 Archetype。
    /// 同一 Registry 不接受两份不同 Loadout，避免客户端与权威槽序分叉。
    /// </summary>
    public void RegisterPlayerLoadout(PartyLoadout loadout)
    {
        if (loadout == null)
            throw new ArgumentNullException(nameof(loadout));
        if (_playerLoadout != null && _playerLoadout != loadout)
            throw new InvalidOperationException("当前房间已登记另一份 PartyLoadout。");

        _playerLoadout = loadout;
        IReadOnlyList<CharacterDefinition> members = loadout.Members;
        for (int i = 0; i < members.Count; i++)
        {
            CharacterConfig config = members[i]?.CharacterConfig;
            if (config != null)
                RegisterPlayer(config);
        }
    }

    /// <summary>登记敌人定义；stableKey 固定为 enemy/{EnemyDefinition.name}。</summary>
    public NetArchetypeId RegisterEnemy(EnemyDefinition definition)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));
        if (_enemies.TryGetValue(definition, out NetArchetypeId existing))
            return existing;
        if (definition.CharacterConfig == null)
        {
            throw new InvalidOperationException(
                $"EnemyDefinition '{definition.name}' 未绑定 CharacterConfig。");
        }

        string stableKey = BuildStableKey("enemy", definition.name, nameof(definition));
        NetArchetypeId id = Register(
            stableKey,
            ReplicationActorKind.Enemy,
            definition,
            definition.CharacterConfig);
        _enemies.Add(definition, id);
        return id;
    }

    /// <summary>按已登记玩家配置取得网络原型；未知配置明确失败。</summary>
    public NetArchetypeId GetArchetypeId(CharacterConfig config)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));
        if (!_players.TryGetValue(config, out NetArchetypeId id))
            throw new KeyNotFoundException($"玩家配置 '{config.name}' 尚未登记。");
        return id;
    }

    /// <summary>按已登记敌人定义取得网络原型；未知定义明确失败。</summary>
    public NetArchetypeId GetArchetypeId(EnemyDefinition definition)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));
        if (!_enemies.TryGetValue(definition, out NetArchetypeId id))
            throw new KeyNotFoundException($"敌人定义 '{definition.name}' 尚未登记。");
        return id;
    }

    /// <summary>按网络原型精确解析 CharacterConfig；未知 Id 不提供默认内容。</summary>
    public CharacterConfig ResolveCharacterConfig(NetArchetypeId archetypeId)
    {
        if (!_configsById.TryGetValue(archetypeId, out CharacterConfig config))
            throw new KeyNotFoundException($"角色网络原型 {archetypeId.Value} 未登记。");
        return config;
    }

    /// <summary>按网络原型解析角色类别，供 Spawn 与 Snapshot 类别一致性校验。</summary>
    public ReplicationActorKind ResolveKind(NetArchetypeId archetypeId)
    {
        if (!_kindsById.TryGetValue(archetypeId, out ReplicationActorKind kind))
            throw new KeyNotFoundException($"角色网络原型 {archetypeId.Value} 未登记。");
        return kind;
    }

    /// <summary>锁定 stableKey 所属 Unity 资产，并交给纯 C# Archetype Catalog 检测哈希碰撞。</summary>
    NetArchetypeId Register(
        string stableKey,
        ReplicationActorKind kind,
        UnityEngine.Object owner,
        CharacterConfig config)
    {
        if (_ownersByKey.TryGetValue(stableKey, out UnityEngine.Object existingOwner))
        {
            if (existingOwner == owner)
                throw new InvalidOperationException($"角色原型 '{stableKey}' 的对象索引不一致。");
            throw new InvalidOperationException(
                $"角色原型 stableKey '{stableKey}' 已由另一资产 '{existingOwner.name}' 占用。");
        }

        CharacterArchetype archetype = _archetypes.Register(stableKey, kind);
        _ownersByKey.Add(stableKey, owner);
        _configsById.Add(archetype.NetArchetypeId, config);
        _kindsById.Add(archetype.NetArchetypeId, kind);
        return archetype.NetArchetypeId;
    }

    /// <summary>按 Unity 资产原始名称构造 Ordinal stableKey；禁止 Trim 产生隐式别名。</summary>
    static string BuildStableKey(string prefix, string assetName, string parameterName)
    {
        if (string.IsNullOrEmpty(assetName))
            throw new ArgumentException("角色内容资产 name 不能为空。", parameterName);
        return $"{prefix}/{assetName}";
    }
}
