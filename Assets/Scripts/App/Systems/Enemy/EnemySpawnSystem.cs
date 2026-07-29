using System.Collections.Generic;

/// <summary>架构级敌人实例注册系统；提供存活上限检查与统一生命周期查询。</summary>
public sealed class EnemySpawnSystem : ArchitectureSystemBase
{
    readonly List<EnemyController> _enemies = new();

    /// <summary>当前已生成且尚未销毁的敌人。</summary>
    public IReadOnlyList<EnemyController> ActiveEnemies => _enemies;

    /// <summary>初始化敌人生成系统；当前无额外启动逻辑。</summary>
    protected override void OnInit() { }

    /// <summary>注册成功装配的敌人 Controller。</summary>
    public void Register(EnemyController enemy)
    {
        if (enemy != null && !_enemies.Contains(enemy))
            _enemies.Add(enemy);
    }

    /// <summary>注销即将销毁的敌人 Controller。</summary>
    public void Unregister(EnemyController enemy)
    {
        if (enemy != null)
            _enemies.Remove(enemy);
    }

    /// <summary>检查指定 Definition 的当前实例数是否低于上限。</summary>
    public bool CanSpawn(EnemyDefinition definition, int maxAlive)
    {
        if (definition == null || maxAlive <= 0)
            return false;

        int alive = 0;
        for (int i = _enemies.Count - 1; i >= 0; i--)
        {
            EnemyController enemy = _enemies[i];
            if (enemy == null)
            {
                _enemies.RemoveAt(i);
                continue;
            }

            if (!enemy.IsDead && enemy.Definition == definition)
                alive++;
        }

        return alive < maxAlive;
    }
}
