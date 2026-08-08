using UnityEngine;

/// <summary>
/// 命中确认后的受击 Cue：订阅帧末 AttackHitEvent，在逻辑接触点播 VFX/SFX。
/// 仅消费 Feedback 配置；完美闪避吞伤不播；不回写 Sim。
/// </summary>
[DisallowMultipleComponent]
public class HitImpactController : AppControllerBase
{
    const string SfxSourceObjectName = "HitImpactSfx";

    AudioSource _audioSource;

    void Awake()
    {
        _audioSource = ResolveOrCreateAudioSource();
    }

    void OnEnable()
    {
        RegisterEvent<AttackHitEvent>(HandleAttackHit);
    }

    void OnDisable()
    {
        UnregisterEvent<AttackHitEvent>(HandleAttackHit);
    }

    /// <summary>帧末命中回调：按接触点与 Feedback 播放特效与音效。</summary>
    void HandleAttackHit(AttackHitEvent hitEvent)
    {
        // 完美吸收无受击权威，不播受击 Cue
        if (hitEvent.AbsorbedByPerfectDodge)
            return;

        ActionHitContext context = hitEvent.Context;
        if (context.Hitbox == null)
            return;

        HitFeedbackSettings feedback = context.Hitbox.Payload.Feedback;
        if (feedback == null || !feedback.HasHitImpactCue)
            return;

        Vector3 position = hitEvent.HitPoint + feedback.HitImpactWorldOffset;
        Quaternion rotation = ResolveImpactRotation(hitEvent.HitDirection, feedback);

        if (feedback.HitImpactVfxPrefab != null)
            SpawnImpactVfx(feedback, position, rotation, context.Attacker);

        if (feedback.HitImpactSfx != null)
            PlayImpactSfx(feedback.HitImpactSfx, feedback.HitImpactSfxVolume);
    }

    /// <summary>优先走 VFXManager 池；世界空间生成，Owner 绑攻击者供卡肉暂停。</summary>
    void SpawnImpactVfx(
        HitFeedbackSettings feedback,
        Vector3 position,
        Quaternion rotation,
        Transform attackerRoot)
    {
        GameObject prefab = feedback.HitImpactVfxPrefab;
        Vector3 scale = feedback.HitImpactScale;
        GameObject instance;

        if (VFXManager.TryGetInstance(out VFXManager manager))
        {
            // 世界空间落点，不挂攻击者，避免位移拖拽火花
            instance = manager.Spawn(prefab, position, rotation, scale, parent: null);
        }
        else
        {
            instance = Instantiate(prefab, position, rotation);
            instance.transform.localScale = scale;
            float lifetime = ActionVfxPlayback.EstimateNaturalDurationSeconds(prefab);
            Destroy(instance, lifetime);
        }

        if (instance == null)
            return;

        // 卡肉筛选按 SpawnOwner==攻击者，与招式刀光一致
        instance.GetComponent<VfxPooledInstance>()?.SetSpawnOwner(attackerRoot);
    }

    void PlayImpactSfx(AudioClip clip, float volume)
    {
        if (_audioSource == null)
            _audioSource = ResolveOrCreateAudioSource();
        if (_audioSource == null || clip == null)
            return;

        _audioSource.PlayOneShot(clip, volume);
    }

    /// <summary>水平命中方向为基准朝向，再按 Feedback 叠加随机欧拉角。</summary>
    static Quaternion ResolveImpactRotation(Vector3 hitDirection, HitFeedbackSettings feedback)
    {
        Vector3 flat = hitDirection;
        flat.y = 0f;
        Quaternion baseRotation = flat.sqrMagnitude < 0.0001f
            ? Quaternion.identity
            : Quaternion.LookRotation(flat.normalized, Vector3.up);

        if (feedback == null || !feedback.RandomizeImpactRotation)
            return baseRotation;

        Vector3 min = feedback.ImpactRandomEulerMin;
        Vector3 max = feedback.ImpactRandomEulerMax;
        Vector3 randomEuler = new(
            Random.Range(Mathf.Min(min.x, max.x), Mathf.Max(min.x, max.x)),
            Random.Range(Mathf.Min(min.y, max.y), Mathf.Max(min.y, max.y)),
            Random.Range(Mathf.Min(min.z, max.z), Mathf.Max(min.z, max.z)));
        return baseRotation * Quaternion.Euler(randomEuler);
    }

    AudioSource ResolveOrCreateAudioSource()
    {
        Transform existing = transform.Find(SfxSourceObjectName);
        if (existing != null)
        {
            AudioSource source = existing.GetComponent<AudioSource>();
            if (source != null)
                return source;
        }

        var go = new GameObject(SfxSourceObjectName);
        go.transform.SetParent(transform, false);
        AudioSource created = go.AddComponent<AudioSource>();
        created.playOnAwake = false;
        created.spatialBlend = 0f;
        return created;
    }
}
