using System;
using UnityEngine;

/// <summary>创建一个敌人 App 根节点并在 Start 前注入 Definition；target 可空，空则感知读玩家花名册。</summary>
public sealed class SpawnEnemyCommand : ArchitectureCommandBase
{
    readonly EnemyDefinition _definition;
    readonly Vector3 _position;
    readonly Quaternion _rotation;
    readonly Transform _target;
    readonly int _maxAlive;
    readonly Action<EnemyController> _spawned;

    /// <summary>创建刷怪命令；maxAlive 按同一 EnemyDefinition 统计。</summary>
    public SpawnEnemyCommand(
        EnemyDefinition definition,
        Vector3 position,
        Quaternion rotation,
        Transform target,
        int maxAlive,
        Action<EnemyController> spawned = null)
    {
        _definition = definition;
        _position = position;
        _rotation = rotation;
        _target = target;
        _maxAlive = maxAlive;
        _spawned = spawned;
    }

    /// <summary>通过 EnemySpawnSystem 门禁后创建轻量 Controller 根节点。</summary>
    protected override void OnExecute()
    {
        EnemySpawnSystem spawnSystem = this.GetSystem<EnemySpawnSystem>();
        if (spawnSystem == null || !spawnSystem.CanSpawn(_definition, _maxAlive))
            return;

        var root = new GameObject(_definition.DisplayName);
        root.transform.SetPositionAndRotation(_position, _rotation);
        EnemyController controller = root.AddComponent<EnemyController>();
        controller.Initialize(_definition, _target);
        // Start 要到下一帧才装配；先占用名额，防止同帧多个命令突破 maxAlive。
        spawnSystem.Register(controller);
        _spawned?.Invoke(controller);
    }
}
