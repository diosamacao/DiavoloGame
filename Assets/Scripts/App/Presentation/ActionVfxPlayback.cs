using UnityEngine;

/// <summary>对 VFX 实例应用播放倍率，并估算 Prefab 自然时长（粒子 + Animator）。</summary>
public static class ActionVfxPlayback
{
    /// <summary>将实例内粒子 simulationSpeed 与 Animator.speed 设为指定倍率。</summary>
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

        foreach (Animator animator in instance.GetComponentsInChildren<Animator>(true))
            animator.speed = speed;
    }

    /// <summary>
    /// 从 Prefab 估算自然时长（秒）：取子级粒子（duration + startLifetime）与 Animator clip 长度的最大值。
    /// </summary>
    public static float EstimateNaturalDurationSeconds(GameObject prefab, float fallbackSeconds = 0.5f)
    {
        if (prefab == null)
            return Mathf.Max(0f, fallbackSeconds);

        float maxLifetime = 0f;
        bool hasContent = false;

        foreach (ParticleSystem ps in prefab.GetComponentsInChildren<ParticleSystem>(true))
        {
            hasContent = true;
            ParticleSystem.MainModule main = ps.main;
            float startLifetime = ResolveStartLifetime(main);
            maxLifetime = Mathf.Max(maxLifetime, main.duration + startLifetime);
        }

        float animatorDuration = EstimateAnimatorDurationSeconds(prefab);
        if (animatorDuration > 0f)
        {
            hasContent = true;
            maxLifetime = Mathf.Max(maxLifetime, animatorDuration);
        }

        if (!hasContent)
            return Mathf.Max(0f, fallbackSeconds);

        return Mathf.Max(maxLifetime, 0.05f);
    }

    /// <summary>遍历子级 Animator 的全部 AnimationClip，取最长秒数；无控制器时返回 0。</summary>
    public static float EstimateAnimatorDurationSeconds(GameObject root)
    {
        if (root == null)
            return 0f;

        float maxLifetime = 0f;
        foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
        {
            RuntimeAnimatorController controller = animator.runtimeAnimatorController;
            if (controller == null)
                continue;

            AnimationClip[] clips = controller.animationClips;
            if (clips == null)
                continue;

            for (int i = 0; i < clips.Length; i++)
            {
                AnimationClip clip = clips[i];
                if (clip != null)
                    maxLifetime = Mathf.Max(maxLifetime, clip.length);
            }
        }

        return maxLifetime;
    }

    /// <summary>将自然时长换算为逻辑帧数（至少 1 帧）；编辑器估测显示用。</summary>
    public static int DurationSecondsToFrameCount(float durationSeconds, float sampleRate)
    {
        float rate = sampleRate > 0f ? sampleRate : ActionSim.LogicHz;
        return Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(0f, durationSeconds) * rate));
    }

    /// <summary>按 MainModule 曲线模式取 startLifetime 上界，供寿命估算。</summary>
    public static float ResolveStartLifetime(ParticleSystem.MainModule main)
    {
        return main.startLifetime.mode switch
        {
            ParticleSystemCurveMode.Constant => main.startLifetime.constant,
            ParticleSystemCurveMode.TwoConstants => main.startLifetime.constantMax,
            _ => main.startLifetime.constantMax,
        };
    }
}
