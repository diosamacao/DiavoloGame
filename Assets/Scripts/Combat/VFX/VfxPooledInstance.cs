using System.Collections;
using UnityEngine;

/// <summary>池化 VFX 实例：Spawn 时重启粒子，生命周期结束后自动回收到 VFXManager；卡肉时同步暂停粒子。</summary>
[DisallowMultipleComponent]
public sealed class VfxPooledInstance : MonoBehaviour, IPoolable
{
    [SerializeField] float fallbackLifetime = 2f;

    VFXManager _manager;
    GameObject _prefab;
    Transform _spawnOwner;
    Coroutine _autoReturnCoroutine;
    bool _particlesPausedForHitStop;

    /// <summary>该实例对应的 Prefab，用于归还正确的对象池。</summary>
    public GameObject SourcePrefab => _prefab;

    /// <summary>由 GameObjectPool 在创建时绑定所属 Manager 与 Prefab 键。</summary>
    public void Initialize(VFXManager manager, GameObject prefab)
    {
        _manager = manager;
        _prefab = prefab;
    }

    /// <summary>记录生成该特效的攻击者根节点，用于卡肉时筛选暂停范围。</summary>
    public void SetSpawnOwner(Transform ownerRoot) => _spawnOwner = ownerRoot;

    void OnEnable()
    {
        CombatHitStop.Began += HandleHitStopBegan;
        CombatHitStop.Ended += HandleHitStopEnded;

        if (CombatHitStop.IsActive && ShouldPauseForHitStop(CombatHitStop.ActiveAttackerRoot))
            PauseParticleSystems();
    }

    void OnDisable()
    {
        CombatHitStop.Began -= HandleHitStopBegan;
        CombatHitStop.Ended -= HandleHitStopEnded;
    }

    /// <summary>从池中取出后调用：重启粒子并安排自动回收。</summary>
    public void OnSpawnFromPool()
    {
        _particlesPausedForHitStop = false;
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

        _particlesPausedForHitStop = false;
        _spawnOwner = null;
        StopParticleSystems();
    }

    void HandleHitStopBegan(Transform attackerRoot)
    {
        if (!ShouldPauseForHitStop(attackerRoot))
            return;

        PauseParticleSystems();
    }

    void HandleHitStopEnded()
    {
        if (!_particlesPausedForHitStop)
            return;

        ResumeParticleSystems();
    }

    bool ShouldPauseForHitStop(Transform attackerRoot)
    {
        if (attackerRoot == null || _spawnOwner == null)
            return false;

        return _spawnOwner == attackerRoot || _spawnOwner.IsChildOf(attackerRoot);
    }

    void PauseParticleSystems()
    {
        if (_particlesPausedForHitStop)
            return;

        foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>(true))
            ps.Pause(true);

        _particlesPausedForHitStop = true;
    }

    void ResumeParticleSystems()
    {
        if (!_particlesPausedForHitStop)
            return;

        foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>(true))
            ps.Play(true);

        _particlesPausedForHitStop = false;
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

    /// <summary>生命周期倒计时；卡肉期间不递减，避免未播完就被回收。</summary>
    IEnumerator AutoReturnAfterLifetime()
    {
        float remaining = ResolveLifetime();
        while (remaining > 0f)
        {
            if (!IsLifetimeFrozen())
                remaining -= Time.deltaTime;

            yield return null;
        }

        _autoReturnCoroutine = null;

        if (_manager != null)
            _manager.Despawn(gameObject);
        else
            gameObject.SetActive(false);
    }

    /// <summary>攻击者卡肉期间冻结该实例的生命周期计时。</summary>
    bool IsLifetimeFrozen() =>
        CombatHitStop.IsActive && ShouldPauseForHitStop(CombatHitStop.ActiveAttackerRoot);

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
