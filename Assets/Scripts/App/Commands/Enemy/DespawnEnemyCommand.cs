/// <summary>注销并销毁一个敌人 Controller；具体资源释放统一由 OnDestroy 完成。</summary>
public sealed class DespawnEnemyCommand : ArchitectureCommandBase
{
    readonly EnemyController _enemy;

    /// <summary>创建敌人回收命令。</summary>
    public DespawnEnemyCommand(EnemyController enemy)
    {
        _enemy = enemy;
    }

    /// <summary>先从生成系统注销，再触发 Unity 销毁生命周期。</summary>
    protected override void OnExecute()
    {
        if (_enemy == null)
            return;

        this.GetSystem<EnemySpawnSystem>()?.Unregister(_enemy);
        _enemy.Despawn();
    }
}
