using System;
using System.Collections.Generic;

/// <summary>按 Key 管理多个 ObjectPoolBase，用于 Prefab/类型分池的 Manager 基座。</summary>
public sealed class ObjectPoolGroup<TKey, TItem> where TItem : class
{
    readonly Dictionary<TKey, ObjectPoolBase<TItem>> _pools = new();
    readonly Func<TKey, ObjectPoolBase<TItem>> _factory;

    public ObjectPoolGroup(Func<TKey, ObjectPoolBase<TItem>> factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <summary>从 key 对应池取出实例；池不存在时通过 factory 创建。</summary>
    public TItem Get(TKey key) => GetOrCreatePool(key).Get();

    /// <summary>将实例归还 key 对应池；池不存在时忽略。</summary>
    public void Return(TKey key, TItem item)
    {
        if (_pools.TryGetValue(key, out ObjectPoolBase<TItem> pool))
            pool.Return(item);
    }

    /// <summary>为 key 对应池预热 inactive 实例。</summary>
    public void Prewarm(TKey key, int count)
    {
        if (count <= 0)
            return;

        GetOrCreatePool(key).Prewarm(count);
    }

    /// <summary>尝试获取已存在的池，不触发 factory。</summary>
    public bool TryGetPool(TKey key, out ObjectPoolBase<TItem> pool) => _pools.TryGetValue(key, out pool);

    ObjectPoolBase<TItem> GetOrCreatePool(TKey key)
    {
        if (_pools.TryGetValue(key, out ObjectPoolBase<TItem> existing))
            return existing;

        ObjectPoolBase<TItem> created = _factory(key);
        _pools[key] = created;
        return created;
    }
}
