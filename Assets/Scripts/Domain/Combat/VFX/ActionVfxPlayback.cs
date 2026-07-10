using UnityEngine;

/// <summary>对 VFX 实例应用播放倍率（ParticleSystem.simulationSpeed）。</summary>
public static class ActionVfxPlayback
{
    /// <summary>将实例内全部粒子的 simulationSpeed 设为指定倍率。</summary>
    public static void ApplyPlaybackSpeed(GameObject instance, float playbackSpeed)
    {
        if (instance == null)
            return;

        float speed = Mathf.Max(0.0001f, playbackSpeed);
        foreach (ParticleSystem ps in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = ps.main;
            main.simulationSpeed = speed;
        }
    }

    /// <summary>从 Prefab 估算自然时长（秒）：取子级 ParticleSystem 的 duration + startLifetime 最大值。</summary>
    public static float EstimateNaturalDurationSeconds(GameObject prefab, float fallbackSeconds = 0.5f)
    {
        if (prefab == null)
            return Mathf.Max(0f, fallbackSeconds);

        float maxLifetime = 0f;
        bool hasParticle = false;

        foreach (ParticleSystem ps in prefab.GetComponentsInChildren<ParticleSystem>(true))
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
            return Mathf.Max(0f, fallbackSeconds);

        return Mathf.Max(maxLifetime, 0.05f);
    }

    /// <summary>将自然时长换算为逻辑帧数（至少 1 帧）。</summary>
    public static int DurationSecondsToFrameCount(float durationSeconds, float sampleRate)
    {
        float rate = sampleRate > 0f ? sampleRate : 30f;
        return Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(0f, durationSeconds) * rate));
    }
}
