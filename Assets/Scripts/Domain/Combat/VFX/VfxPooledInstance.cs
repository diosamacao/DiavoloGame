using System.Collections;
using UnityEngine;

/// <summary>
/// 池化 VFX 实例：Spawn 时重启粒子与 Animator，生命周期结束后回池；卡肉时同步暂停两者。
/// </summary>
[DisallowMultipleComponent]
public sealed class VfxPooledInstance : AppControllerBase, IPoolable
{
    [SerializeField] float fallbackLifetime = 2f;

    VFXManager _manager;
    GameObject _prefab;
    Transform _spawnOwner;
    Coroutine _autoReturnCoroutine;
    bool _presentationPausedForHitStop;
    Animator[] _hitStopAnimators;
    float[] _hitStopAnimatorSpeeds;

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

    /// <summary>确认当前池租约是否仍属于指定角色，避免旧引用误回收已复用给别人的实例。</summary>
    public bool IsOwnedBy(Transform ownerRoot) =>
        ownerRoot != null && _spawnOwner == ownerRoot;

    void OnEnable()
    {
        RegisterEvent<HitStopBeganEvent>(HandleHitStopBegan);
        RegisterEvent<HitStopEndedEvent>(HandleHitStopEnded);

        CombatFeedbackSystem feedbackSystem = GetSystem<CombatFeedbackSystem>();
        if (feedbackSystem != null
            && feedbackSystem.IsHitStopActive
            && ShouldPauseForHitStop(feedbackSystem.ActiveHitStopAttackerRoot))
        {
            PausePresentation();
        }
    }

    void OnDisable()
    {
        UnregisterEvent<HitStopBeganEvent>(HandleHitStopBegan);
        UnregisterEvent<HitStopEndedEvent>(HandleHitStopEnded);
    }

    /// <summary>从池中取出后调用：重启粒子/Animator 并安排自动回收。</summary>
    public void OnSpawnFromPool()
    {
        _presentationPausedForHitStop = false;
        ClearHitStopAnimatorCache();
        RestartParticleSystems();
        RestartAnimators();

        if (_autoReturnCoroutine != null)
            StopCoroutine(_autoReturnCoroutine);

        _autoReturnCoroutine = StartCoroutine(AutoReturnAfterLifetime());
    }

    /// <summary>回池前停止粒子/Animator 与自动回收协程。</summary>
    public void OnReturnToPool()
    {
        if (_autoReturnCoroutine != null)
        {
            StopCoroutine(_autoReturnCoroutine);
            _autoReturnCoroutine = null;
        }

        _presentationPausedForHitStop = false;
        ClearHitStopAnimatorCache();
        _spawnOwner = null;
        StopParticleSystems();
        StopAnimators();
    }

    void HandleHitStopBegan(HitStopBeganEvent hitStopEvent)
    {
        if (!ShouldPauseForHitStop(hitStopEvent.AttackerRoot))
            return;

        PausePresentation();
    }

    void HandleHitStopEnded(HitStopEndedEvent hitStopEvent)
    {
        if (!_presentationPausedForHitStop)
            return;

        ResumePresentation();
    }

    bool ShouldPauseForHitStop(Transform attackerRoot)
    {
        if (attackerRoot == null || _spawnOwner == null)
            return false;

        return _spawnOwner == attackerRoot || _spawnOwner.IsChildOf(attackerRoot);
    }

    /// <summary>卡肉：暂停粒子，并将 Animator.speed 置 0（保留原倍率以便恢复）。</summary>
    void PausePresentation()
    {
        if (_presentationPausedForHitStop)
            return;

        foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>(true))
            ps.Pause(true);

        Animator[] animators = GetComponentsInChildren<Animator>(true);
        _hitStopAnimators = animators;
        _hitStopAnimatorSpeeds = new float[animators.Length];
        for (int i = 0; i < animators.Length; i++)
        {
            _hitStopAnimatorSpeeds[i] = animators[i].speed;
            animators[i].speed = 0f;
        }

        _presentationPausedForHitStop = true;
    }

    /// <summary>卡肉结束：恢复粒子播放与 Animator 原 speed。</summary>
    void ResumePresentation()
    {
        if (!_presentationPausedForHitStop)
            return;

        foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>(true))
            ps.Play(true);

        if (_hitStopAnimators != null && _hitStopAnimatorSpeeds != null)
        {
            int count = Mathf.Min(_hitStopAnimators.Length, _hitStopAnimatorSpeeds.Length);
            for (int i = 0; i < count; i++)
            {
                Animator animator = _hitStopAnimators[i];
                if (animator != null)
                    animator.speed = _hitStopAnimatorSpeeds[i];
            }
        }

        ClearHitStopAnimatorCache();
        _presentationPausedForHitStop = false;
    }

    void ClearHitStopAnimatorCache()
    {
        _hitStopAnimators = null;
        _hitStopAnimatorSpeeds = null;
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

    /// <summary>池化复用时从头播放默认层状态，避免停在上一轮末帧。</summary>
    void RestartAnimators()
    {
        foreach (Animator animator in GetComponentsInChildren<Animator>(true))
        {
            animator.gameObject.SetActive(true);
            animator.enabled = true;
            animator.Rebind();
            animator.Update(0f);

            if (animator.runtimeAnimatorController != null)
                animator.Play(0, -1, 0f);

            animator.Update(0f);
        }
    }

    /// <summary>回池时复位 Animator 到绑定初始姿态，并把 speed 还原为 1 供下次 Spawn 再设倍率。</summary>
    void StopAnimators()
    {
        foreach (Animator animator in GetComponentsInChildren<Animator>(true))
        {
            animator.Rebind();
            animator.Update(0f);
            animator.speed = 1f;
        }
    }

    /// <summary>
    /// 生命周期倒计时；等一帧让 Spawn 后的 playbackSpeed 生效，再按倍率换算墙钟时长。
    /// 卡肉期间不递减，避免未播完就被回收。
    /// </summary>
    IEnumerator AutoReturnAfterLifetime()
    {
        // ActionVfxPlayer 在 Spawn 返回后才 ApplyPlaybackSpeed，需延后一帧再估算。
        yield return null;

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
    bool IsLifetimeFrozen()
    {
        CombatFeedbackSystem feedbackSystem = GetSystem<CombatFeedbackSystem>();
        return feedbackSystem != null
            && feedbackSystem.IsHitStopActive
            && ShouldPauseForHitStop(feedbackSystem.ActiveHitStopAttackerRoot);
    }

    /// <summary>
    /// 根据子级粒子与 Animator 估算最长可见墙钟时间；按各自播放倍率换算。
    /// 两者皆无时用 fallbackLifetime。
    /// </summary>
    float ResolveLifetime()
    {
        float maxLifetime = 0f;
        bool hasContent = false;

        foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>(true))
        {
            hasContent = true;
            ParticleSystem.MainModule main = ps.main;
            float startLifetime = ActionVfxPlayback.ResolveStartLifetime(main);

            // simulationSpeed 加速/减速粒子时间，墙钟回收需除以倍率。
            float speed = Mathf.Max(0.0001f, main.simulationSpeed);
            maxLifetime = Mathf.Max(maxLifetime, (main.duration + startLifetime) / speed);
        }

        foreach (Animator animator in GetComponentsInChildren<Animator>(true))
        {
            RuntimeAnimatorController controller = animator.runtimeAnimatorController;
            if (controller == null || controller.animationClips == null)
                continue;

            AnimationClip[] clips = controller.animationClips;
            for (int i = 0; i < clips.Length; i++)
            {
                AnimationClip clip = clips[i];
                if (clip == null)
                    continue;

                hasContent = true;
                float speed = Mathf.Max(0.0001f, animator.speed);
                maxLifetime = Mathf.Max(maxLifetime, clip.length / speed);
            }
        }

        if (!hasContent)
            return fallbackLifetime;

        return Mathf.Max(maxLifetime, 0.05f);
    }
}
