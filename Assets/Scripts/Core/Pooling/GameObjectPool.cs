using System;
using UnityEngine;

/// <summary>GameObject 对象池：基于 Prefab 实例化，支持 IPoolable 与新建实例配置回调。</summary>
public sealed class GameObjectPool : ObjectPoolBase<GameObject>
{
    readonly GameObject _prefab;
    readonly Transform _inactiveRoot;
    readonly Action<GameObject> _configureNewInstance;

    public GameObjectPool(
        GameObject prefab,
        Transform inactiveRoot,
        Action<GameObject> configureNewInstance = null)
    {
        _prefab = prefab;
        _inactiveRoot = inactiveRoot;
        _configureNewInstance = configureNewInstance;
    }

    public GameObject Prefab => _prefab;

    protected override GameObject CreateInstance()
    {
        GameObject instance = UnityEngine.Object.Instantiate(_prefab, _inactiveRoot);
        instance.name = $"{_prefab.name} (Pooled)";
        _configureNewInstance?.Invoke(instance);
        return instance;
    }

    protected override void OnGet(GameObject item)
    {
        item.SetActive(true);

        IPoolable poolable = item.GetComponent<IPoolable>();
        poolable?.OnSpawnFromPool();
    }

    protected override void OnReturn(GameObject item)
    {
        IPoolable poolable = item.GetComponent<IPoolable>();
        poolable?.OnReturnToPool();

        item.transform.SetParent(_inactiveRoot, false);
        item.SetActive(false);
    }
}
