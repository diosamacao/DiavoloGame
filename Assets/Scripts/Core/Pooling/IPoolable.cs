/// <summary>对象池实例在取出/归还时的生命周期回调。</summary>
public interface IPoolable
{
    /// <summary>从池中取出并即将使用时调用。</summary>
    void OnSpawnFromPool();

    /// <summary>归还池之前调用。</summary>
    void OnReturnToPool();
}
