using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>经启动阶段完整构建并冻结的 Gameplay 内容目录；运行时只允许稳定查询。</summary>
public sealed class GameContentCatalog
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
    bool _frozen;

    /// <summary>仅 Bootstrap 与同程序集测试装配可创建；生产运行时必须使用 ValidateAndBuild。</summary>
    internal GameContentCatalog()
    {
    }

    /// <summary>当前房间冻结后的动作 Id 与资产映射。</summary>
    public ActionReplicationCatalog Actions => _actions;

    /// <summary>当前房间声明的玩家阵容；Dedicated Join 使用同一槽序。</summary>
    public PartyLoadout PlayerLoadout => _playerLoadout;

    /// <summary>目录是否完成构建并禁止后续登记。</summary>
    public bool IsFrozen => _frozen;

    /// <summary>复制已登记网络原型 Id，供 Gameplay 指纹哈希。</summary>
    public void CopyArchetypeIds(List<int> results)
    {
        if (results == null)
            throw new ArgumentNullException(nameof(results));
        results.Clear();
        foreach (NetArchetypeId id in _configsById.Keys)
            results.Add(id.Value);
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

    /// <summary>Build 阶段登记唯一玩家阵容及其全部非空角色。</summary>
    internal void AddPlayerLoadout(PartyLoadout loadout)
    {
        EnsureMutable();
        if (loadout == null)
            throw new ArgumentNullException(nameof(loadout));
        if (_playerLoadout != null && _playerLoadout != loadout)
            throw new InvalidOperationException("场景声明了多份不同 PartyLoadout。");

        _playerLoadout = loadout;
        IReadOnlyList<CharacterDefinition> members = loadout.Members;
        for (int i = 0; i < members.Count; i++)
        {
            CharacterConfig config = members[i]?.CharacterConfig;
            if (config != null)
                AddPlayer(config);
        }
    }

    /// <summary>Build 阶段登记敌人定义及其角色配置。</summary>
    internal void AddEnemy(EnemyDefinition definition)
    {
        EnsureMutable();
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));
        if (_enemies.ContainsKey(definition))
            return;
        if (definition.CharacterConfig == null)
            throw new InvalidOperationException($"EnemyDefinition '{definition.name}' 未绑定 CharacterConfig。");

        NetArchetypeId id = AddArchetype(
            BuildStableKey("enemy", definition.name, nameof(definition)),
            ReplicationActorKind.Enemy,
            definition,
            definition.CharacterConfig);
        _enemies.Add(definition, id);
        _actions.Prefill(definition.CharacterConfig);
    }

    /// <summary>完成 Build 并冻结动作与原型映射。</summary>
    internal void Freeze()
    {
        EnsureMutable();
        if (_playerLoadout == null)
            throw new InvalidOperationException("Gameplay 内容缺少 PartyLoadout。");
        _actions.Freeze();
        _frozen = true;
    }

    /// <summary>登记单个玩家角色，并预填其全部动作。</summary>
    void AddPlayer(CharacterConfig config)
    {
        if (_players.ContainsKey(config))
            return;
        NetArchetypeId id = AddArchetype(
            BuildStableKey("player", config.name, nameof(config)),
            ReplicationActorKind.Player,
            config,
            config);
        _players.Add(config, id);
        _actions.Prefill(config);
    }

    /// <summary>锁定 stable key 所属资产，并由纯 C# Catalog 检测哈希碰撞。</summary>
    NetArchetypeId AddArchetype(
        string stableKey,
        ReplicationActorKind kind,
        UnityEngine.Object owner,
        CharacterConfig config)
    {
        if (_ownersByKey.TryGetValue(stableKey, out UnityEngine.Object existingOwner))
        {
            if (existingOwner == owner)
                throw new InvalidOperationException($"角色原型 '{stableKey}' 被重复登记。");
            throw new InvalidOperationException(
                $"角色原型 stableKey '{stableKey}' 已由另一资产 '{existingOwner.name}' 占用。");
        }

        CharacterArchetype archetype = _archetypes.Register(stableKey, kind);
        _ownersByKey.Add(stableKey, owner);
        _configsById.Add(archetype.NetArchetypeId, config);
        _kindsById.Add(archetype.NetArchetypeId, kind);
        return archetype.NetArchetypeId;
    }

    /// <summary>冻结后拒绝任何稳定 Id 变化。</summary>
    void EnsureMutable()
    {
        if (_frozen)
            throw new InvalidOperationException("Gameplay 内容目录已冻结。");
    }

    /// <summary>按 Unity 资产原始名称构造 Ordinal stable key。</summary>
    static string BuildStableKey(string prefix, string assetName, string parameterName)
    {
        if (string.IsNullOrEmpty(assetName))
            throw new ArgumentException("角色内容资产 name 不能为空。", parameterName);
        return $"{prefix}/{assetName}";
    }
}
