using UnityEngine;

/// <summary>
/// 客机受击 Cue：用复制落点 + Catalog 招式/Hitbox 还原 Feedback，不走权威 AttackHitEvent。
/// </summary>
public static class HitImpactCuePlayer
{
    /// <summary>
    /// 按招式 Hitbox 下标取 Feedback 并在世界点播放。
    /// 找不到 Feedback 或未配置 Cue 时返回 false。
    /// </summary>
    public static bool TryPlay(
        ActionReplicationCatalog catalog,
        int actionId,
        int hitboxIndex,
        Vector3 hitPoint,
        Vector3 hitDirection,
        Transform attackerRoot)
    {
        if (!TryResolveFeedback(catalog, actionId, hitboxIndex, out HitFeedbackSettings feedback))
            return false;

        Vector3 position = hitPoint + feedback.HitImpactWorldOffset;
        Quaternion rotation = ResolveImpactRotation(hitDirection, feedback);
        if (feedback.HitImpactVfxPrefab != null)
            SpawnImpactVfx(feedback, position, rotation, attackerRoot);
        if (feedback.HitImpactSfx != null)
            PlayImpactSfx(feedback.HitImpactSfx, feedback.HitImpactSfxVolume, attackerRoot);
        return true;
    }

    /// <summary>从 Catalog 招式的 Hitbox 窗口取受击 Feedback。</summary>
    public static bool TryResolveFeedback(
        ActionReplicationCatalog catalog,
        int actionId,
        int hitboxIndex,
        out HitFeedbackSettings feedback)
    {
        feedback = null;
        if (catalog == null || actionId <= 0 || hitboxIndex < 0)
            return false;
        if (!catalog.TryGet(actionId, out ActionDefinition action) || action == null)
            return false;

        HitboxNotifyState[] boxes = action.HitboxStates;
        if (boxes == null || hitboxIndex >= boxes.Length || boxes[hitboxIndex] == null)
            return false;

        feedback = boxes[hitboxIndex].Payload.Feedback;
        return feedback != null && feedback.HasHitImpactCue;
    }

    static void SpawnImpactVfx(
        HitFeedbackSettings feedback,
        Vector3 position,
        Quaternion rotation,
        Transform attackerRoot)
    {
        GameObject prefab = feedback.HitImpactVfxPrefab;
        Vector3 scale = feedback.HitImpactScale;
        GameObject instance;
        if (VFXManager.TryGetInstance(out VFXManager manager))
            instance = manager.Spawn(prefab, position, rotation, scale, parent: null);
        else
        {
            instance = Object.Instantiate(prefab, position, rotation);
            instance.transform.localScale = scale;
            float lifetime = ActionVfxPlayback.EstimateNaturalDurationSeconds(prefab);
            Object.Destroy(instance, lifetime);
        }

        if (instance == null)
            return;
        instance.GetComponent<VfxPooledInstance>()?.SetSpawnOwner(attackerRoot);
    }

    static void PlayImpactSfx(AudioClip clip, float volume, Transform fallbackRoot)
    {
        if (clip == null)
            return;
        if (fallbackRoot == null)
        {
            AudioSource.PlayClipAtPoint(clip, Vector3.zero, volume);
            return;
        }

        AudioSource.PlayClipAtPoint(clip, fallbackRoot.position, volume);
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
}
