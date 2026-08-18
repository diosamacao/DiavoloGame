using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>场景刷怪入口；仅 Listen Host 按出生点发送 SpawnEnemyCommand。</summary>
public sealed class EnemySpawnController : AppControllerBase
{
    [SerializeField] Transform target = null;
    [SerializeField] EnemySpawnEntry[] entries = Array.Empty<EnemySpawnEntry>();

    void Start()
    {
        // 敌人只在 Listen Host 生成；客机跟 Snapshot；Dedicated 刷怪属 W6/W7。
        if (CombatWorldController.Current != null
            && CombatWorldController.Current.Role != ReplicationRole.ListenHost)
            return;

        // target 仅作可选钉死；为空时敌人感知读 LocalPlayerService 花名册。
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i].SpawnOnStart)
                Spawn(i);
        }
    }

    /// <summary>收集本控制器条目里的敌人定义；同一对象只返回一次。</summary>
    public void CollectDefinitions(List<EnemyDefinition> results)
    {
        if (results == null)
            return;

        for (int i = 0; i < entries.Length; i++)
        {
            EnemyDefinition definition = entries[i].Definition;
            // Definition 是网络原型 stableKey 的资产真源，不能按 CharacterConfig 合并不同敌人。
            if (definition != null && !results.Contains(definition))
                results.Add(definition);
        }
    }

    /// <summary>生成指定索引条目；无效索引或达到存活上限时不执行。</summary>
    public void Spawn(int entryIndex)
    {
        if (entryIndex < 0 || entryIndex >= entries.Length)
            return;

        EnemySpawnEntry entry = entries[entryIndex];
        if (entry.Definition == null)
            return;

        Transform point = entry.SpawnPoint != null ? entry.SpawnPoint : transform;
        SendCommand(new SpawnEnemyCommand(
            entry.Definition,
            point.position,
            point.rotation,
            target,
            entry.MaxAlive));
    }
}

/// <summary>单个场景刷怪条目；组合 Definition、出生点与存活上限。</summary>
[Serializable]
public struct EnemySpawnEntry
{
    [SerializeField] EnemyDefinition definition;
    [SerializeField] Transform spawnPoint;
    [SerializeField] bool spawnOnStart;
    [SerializeField] int maxAlive;

    /// <summary>要生成的敌人定义。</summary>
    public EnemyDefinition Definition => definition;
    /// <summary>出生点；为空时使用 EnemySpawnController 根节点。</summary>
    public Transform SpawnPoint => spawnPoint;
    /// <summary>场景启动时是否立即生成。</summary>
    public bool SpawnOnStart => spawnOnStart;
    /// <summary>同 Definition 最大存活数；未配置时为 1。</summary>
    public int MaxAlive => maxAlive > 0 ? maxAlive : 1;
}
