using System.Collections.Generic;
using UnityEngine;

/// <summary>全局 VFX 对象池：按 Prefab 分池 Spawn / Despawn，供 ActionVfx 等战斗特效使用。</summary>
[DisallowMultipleComponent]
public class VFXManager : MonoBehaviour
{
    static VFXManager s_instance;

    [SerializeField] int defaultPrewarmCount = 2;

    readonly Dictionary<int, VfxPrefabPool> _pools = new();
    Transform _inactiveRoot;

    /// <summary>场景内 VFXManager 单例；未放置时返回 null。</summary>
    public static VFXManager Instance => s_instance;

    /// <summary>未激活实例的父节点，便于 Hierarchy 整理。</summary>
    public Transform InactiveRoot => _inactiveRoot;

    void Awake()
    {
        if (s_instance != null && s_instance != this)
        {
            Debug.LogWarning("VFXManager: 场景中存在多个实例，销毁重复对象。", this);
            Destroy(gameObject);
            return;
        }

        s_instance = this;
        EnsureInactiveRoot();
    }

    void OnDestroy()
    {
        if (s_instance == this)
            s_instance = null;
    }

    /// <summary>尝试获取场景内 VFXManager。</summary>
    public static bool TryGetInstance(out VFXManager manager)
    {
        manager = s_instance;
        return manager != null;
    }

    /// <summary>按 ActionVfxKeyframe 从池取出实例并应用局部变换。</summary>
    public GameObject Spawn(GameObject prefab, Transform root, Transform attachPoint, ActionVfxKeyframe vfx)
    {
        if (prefab == null || vfx == null)
            return null;

        Transform anchor = attachPoint != null ? attachPoint : root;
        if (anchor == null)
            return null;

        GameObject instance = SpawnInternal(prefab);
        if (instance == null)
            return null;

        ActionVfxSpawner.ApplyTransform(instance.transform, anchor, vfx);
        return instance;
    }

    /// <summary>通用 Spawn：指定世界/局部 Transform，可选父节点。</summary>
    public GameObject Spawn(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation,
        Vector3 localScale,
        Transform parent = null)
    {
        if (prefab == null)
            return null;

        GameObject instance = SpawnInternal(prefab);
        if (instance == null)
            return null;

        Transform instanceTransform = instance.transform;
        instanceTransform.SetParent(parent, parent != null);
        instanceTransform.position = position;
        instanceTransform.rotation = rotation;
        instanceTransform.localScale = Vector3.Max(localScale, Vector3.one * 0.01f);
        return instance;
    }

    /// <summary>将实例归还对象池；非本 Manager 创建的实例会被 Destroy。</summary>
    public void Despawn(GameObject instance)
    {
        if (instance == null)
            return;

        VfxPooledInstance pooled = instance.GetComponent<VfxPooledInstance>();
        if (pooled == null || pooled.SourcePrefab == null)
        {
            Destroy(instance);
            return;
        }

        int prefabId = pooled.SourcePrefab.GetInstanceID();
        if (!_pools.TryGetValue(prefabId, out VfxPrefabPool pool))
        {
            Destroy(instance);
            return;
        }

        pool.Return(instance);
    }

    /// <summary>为指定 Prefab 预热对象池。</summary>
    public void Prewarm(GameObject prefab, int count)
    {
        if (prefab == null)
            return;

        GetOrCreatePool(prefab).Prewarm(count);
    }

    GameObject SpawnInternal(GameObject prefab)
    {
        VfxPrefabPool pool = GetOrCreatePool(prefab);
        GameObject instance = pool.Get();

        VfxPooledInstance pooled = instance.GetComponent<VfxPooledInstance>();
        pooled?.OnSpawnFromPool();
        return instance;
    }

    VfxPrefabPool GetOrCreatePool(GameObject prefab)
    {
        int prefabId = prefab.GetInstanceID();
        if (_pools.TryGetValue(prefabId, out VfxPrefabPool existing))
            return existing;

        EnsureInactiveRoot();

        Transform prefabRoot = new GameObject($"Pool_{prefab.name}").transform;
        prefabRoot.SetParent(_inactiveRoot, false);

        var pool = new VfxPrefabPool(prefab, prefabRoot, this);
        if (defaultPrewarmCount > 0)
            pool.Prewarm(defaultPrewarmCount);

        _pools.Add(prefabId, pool);
        return pool;
    }

    void EnsureInactiveRoot()
    {
        if (_inactiveRoot != null)
            return;

        var rootObject = new GameObject("InactiveVfx");
        rootObject.transform.SetParent(transform, false);
        _inactiveRoot = rootObject.transform;
    }
}
