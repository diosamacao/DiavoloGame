using System.Collections.Generic;
using UnityEngine;

/// <summary>单个 Prefab 的对象池：复用实例，必要时扩容。</summary>
sealed class VfxPrefabPool
{
    readonly GameObject _prefab;
    readonly Transform _inactiveRoot;
    readonly VFXManager _manager;
    readonly Stack<GameObject> _inactive = new();

    public VfxPrefabPool(GameObject prefab, Transform inactiveRoot, VFXManager manager)
    {
        _prefab = prefab;
        _inactiveRoot = inactiveRoot;
        _manager = manager;
    }

    /// <summary>取出一个可用实例；池空时实例化新对象。</summary>
    public GameObject Get()
    {
        while (_inactive.Count > 0)
        {
            GameObject candidate = _inactive.Pop();
            if (candidate != null)
                return candidate;
        }

        GameObject instance = Object.Instantiate(_prefab, _inactiveRoot);
        instance.name = $"{_prefab.name} (Pooled)";
        EnsurePooledInstance(instance);
        return instance;
    }

    /// <summary>回收实例到池内并隐藏。</summary>
    public void Return(GameObject instance)
    {
        if (instance == null)
            return;

        VfxPooledInstance pooled = instance.GetComponent<VfxPooledInstance>();
        pooled?.OnReturnToPool();

        instance.transform.SetParent(_inactiveRoot, false);
        instance.SetActive(false);
        _inactive.Push(instance);
    }

    /// <summary>预热：预创建 count 个 inactive 实例。</summary>
    public void Prewarm(int count)
    {
        int target = Mathf.Max(0, count);
        while (_inactive.Count < target)
        {
            GameObject instance = Object.Instantiate(_prefab, _inactiveRoot);
            instance.name = $"{_prefab.name} (Pooled)";
            EnsurePooledInstance(instance);
            instance.SetActive(false);
            _inactive.Push(instance);
        }
    }

    void EnsurePooledInstance(GameObject instance)
    {
        VfxPooledInstance pooled = instance.GetComponent<VfxPooledInstance>();
        if (pooled == null)
            pooled = instance.AddComponent<VfxPooledInstance>();

        pooled.Initialize(_manager, _prefab);
    }
}
