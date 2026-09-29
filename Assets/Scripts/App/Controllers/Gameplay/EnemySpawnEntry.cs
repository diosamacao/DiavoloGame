using System;
using System.Collections.Generic;
using UnityEngine;

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
