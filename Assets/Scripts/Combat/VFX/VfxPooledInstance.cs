using System.Collections;
using UnityEngine;

/// <summary>池化 VFX 实例：Spawn 时重启粒子，生命周期结束后自动回收到 VFXManager。</summary>
[DisallowMultipleComponent]
public sealed class VfxPooledInstance : MonoBehaviour
{
    [SerializeField] float fallbackLifetime = 2f;

    VFXManager _manager;
    GameObject _prefab;
    Coroutine _autoReturnCoroutine;

    /// <summary>该实例对应的 Prefab，用于归还正确的对象池。</summary>
    public GameObject SourcePrefab => _prefab;

    /// <summary>由 VfxPrefabPool 在创建/取出前绑定所属 Manager 与 Prefab 键。</summary>
    public void Initialize(VFXManager manager, GameObject prefab)
    {
        _manager = manager;
        _prefab = prefab;
    }

    /// <summary>从池中取出并激活后调用：重启粒子并安排自动回收。</summary>
    public void OnSpawnFromPool()
    {
        gameObject.SetActive(true);
        RestartParticleSystems();

        if (_autoReturnCoroutine != null)
            StopCoroutine(_autoReturnCoroutine);

        _autoReturnCoroutine = StartCoroutine(AutoReturnAfterLifetime());
    }

    /// <summary>回池前停止粒子与自动回收协程。</summary>
    public void OnReturnToPool()
    {
        if (_autoReturnCoroutine != null)
        {
            StopCoroutine(_autoReturnCoroutine);
            _autoReturnCoroutine = null;
        }

        StopParticleSystems();
    }

    void RestartParticleSystems()
    {
        foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.gameObject.SetActive(true);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Clear(true);
            ps.Play(true);
        }
    }

    void StopParticleSystems()
    {
        foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Clear(true);
        }
    }

    IEnumerator AutoReturnAfterLifetime()
    {
        yield return new WaitForSeconds(ResolveLifetime());
        _autoReturnCoroutine = null;

        if (_manager != null)
            _manager.Despawn(gameObject);
        else
            gameObject.SetActive(false);
    }

    /// <summary>根据子级 ParticleSystem 估算最长可见时间；无粒子时用 fallbackLifetime。</summary>
    float ResolveLifetime()
    {
        float maxLifetime = 0f;
        bool hasParticle = false;

        foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>(true))
        {
            hasParticle = true;
            ParticleSystem.MainModule main = ps.main;
            float startLifetime = main.startLifetime.mode switch
            {
                ParticleSystemCurveMode.Constant => main.startLifetime.constant,
                ParticleSystemCurveMode.TwoConstants => main.startLifetime.constantMax,
                _ => main.startLifetime.constantMax,
            };

            maxLifetime = Mathf.Max(maxLifetime, main.duration + startLifetime);
        }

        if (!hasParticle)
            return fallbackLifetime;

        return Mathf.Max(maxLifetime, 0.05f);
    }
}
