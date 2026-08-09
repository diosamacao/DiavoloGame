using UnityEditor;
using UnityEngine;

/// <summary>
/// Edit Mode 下驱动 VFX Prefab 预览：粒子按绝对时间 Simulate，Animator 按同时间采样（对齐 showcase 的 Play + Animator）。
/// </summary>
public static class ActionVfxEditorPreview
{
    static double s_lastUpdateTime;

    /// <summary>实例化后重启粒子与 Animator（对齐 playOnAwake + Animator 从头播）。</summary>
    public static void Restart(GameObject instance)
    {
        if (instance == null)
            return;

        instance.SetActive(true);
        RestartParticleSystems(instance);
        RestartAnimators(instance);
        ResetTiming();
    }

    /// <summary>仅重启粒子；兼容旧调用，完整预览请用 <see cref="Restart"/>。</summary>
    public static void RestartParticleSystems(GameObject instance)
    {
        if (instance == null)
            return;

        instance.SetActive(true);

        foreach (ParticleSystem ps in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.gameObject.SetActive(true);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Clear(true);
            ps.Play(true);
        }
    }

    /// <summary>Rebind 并从头播放全部子级 Animator。</summary>
    public static void RestartAnimators(GameObject instance)
    {
        if (instance == null)
            return;

        foreach (Animator animator in instance.GetComponentsInChildren<Animator>(true))
        {
            animator.gameObject.SetActive(true);
            animator.enabled = true;
            animator.speed = 1f;
            animator.Rebind();
            animator.Update(0f);

            if (animator.runtimeAnimatorController != null)
                animator.Play(0, -1, 0f);

            animator.Update(0f);
        }
    }

    /// <summary>
    /// 按绝对本地时间采样粒子与 Animator；scaledTime = localTime * playbackSpeed。
    /// 不在此处 RepaintAll：由调用方在预览帧变化时统一请求 Scene 刷新。
    /// </summary>
    public static void SampleAt(GameObject instance, float scaledTimeSeconds)
    {
        SimulateParticlesAt(instance, scaledTimeSeconds);
        SampleAnimatorsAt(instance, scaledTimeSeconds);
    }

    /// <summary>兼容旧名：等价于 <see cref="SampleAt"/>。</summary>
    public static void SimulateAt(GameObject instance, float scaledTimeSeconds) =>
        SampleAt(instance, scaledTimeSeconds);

    /// <summary>按绝对本地时间 Simulate 粒子（withChildren + restart）。</summary>
    public static void SimulateParticlesAt(GameObject instance, float scaledTimeSeconds)
    {
        if (instance == null || !instance.activeInHierarchy)
            return;

        float time = Mathf.Max(0f, scaledTimeSeconds);

        // 只从根粒子推进，避免 withChildren 在子级上重复 Simulate。
        foreach (ParticleSystem ps in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (!ps.gameObject.activeInHierarchy)
                continue;

            ParticleSystem parentPs = ps.transform.parent != null
                ? ps.transform.parent.GetComponentInParent<ParticleSystem>()
                : null;
            if (parentPs != null && parentPs != ps)
                continue;

            ps.Simulate(time, true, true, true);
        }
    }

    /// <summary>
    /// 按绝对时间采样 Animator：Edit Mode 用 Clip.SampleAnimation 写姿态，再同步状态机归一化时间。
    /// </summary>
    public static void SampleAnimatorsAt(GameObject instance, float scaledTimeSeconds)
    {
        if (instance == null || !instance.activeInHierarchy)
            return;

        float time = Mathf.Max(0f, scaledTimeSeconds);

        foreach (Animator animator in instance.GetComponentsInChildren<Animator>(true))
        {
            if (!animator.gameObject.activeInHierarchy)
                continue;

            RuntimeAnimatorController controller = animator.runtimeAnimatorController;
            if (controller == null)
                continue;

            AnimationClip clip = ResolvePrimaryClip(controller);
            if (clip == null)
                continue;

            float length = Mathf.Max(0.0001f, clip.length);
            float sampleTime = clip.isLooping
                ? time % length
                : Mathf.Min(time, length);
            float normalized = sampleTime / length;

            animator.enabled = true;
            // Scrub 时锁 speed，避免 Update 自行推进。
            animator.speed = 0f;
            clip.SampleAnimation(animator.gameObject, sampleTime);
            animator.Play(0, -1, normalized);
            animator.Update(0f);
        }
    }

    /// <summary>每帧推进粒子模拟；非循环特效结束后自动重播以便持续预览。</summary>
    public static void Simulate(GameObject instance)
    {
        if (instance == null || !instance.activeInHierarchy)
            return;

        double now = EditorApplication.timeSinceStartup;
        float deltaTime = s_lastUpdateTime > 0d ? (float)(now - s_lastUpdateTime) : 0.016f;
        s_lastUpdateTime = now;
        deltaTime = Mathf.Clamp(deltaTime, 0f, 0.05f);

        bool anyParticle = false;
        bool anyAlive = false;

        foreach (ParticleSystem ps in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (!ps.gameObject.activeInHierarchy)
                continue;

            anyParticle = true;
            ps.Simulate(deltaTime, true, false, true);
            if (ps.IsAlive(true))
                anyAlive = true;
        }

        // 实时推进 Animator（对齐 showcase 墙钟播放）。
        foreach (Animator animator in instance.GetComponentsInChildren<Animator>(true))
        {
            if (!animator.gameObject.activeInHierarchy || animator.runtimeAnimatorController == null)
                continue;

            animator.enabled = true;
            animator.speed = 1f;
            animator.Update(deltaTime);
        }

        // 刀光类 Prefab 多为非循环粒子；播完后自动重播，避免 Scene 中只剩空轴。
        if (anyParticle && !anyAlive)
            Restart(instance);
    }

    /// <summary>切换预览实例时重置模拟时间基准。</summary>
    public static void ResetTiming() => s_lastUpdateTime = 0d;

    /// <summary>预览实例是否包含可模拟的 ParticleSystem。</summary>
    public static bool HasParticleSystems(GameObject instance)
    {
        if (instance == null)
            return false;

        return instance.GetComponentInChildren<ParticleSystem>(true) != null;
    }

    /// <summary>是否含 Animator（无粒子仅靠动画的 VFX 也可预览）。</summary>
    public static bool HasAnimators(GameObject instance)
    {
        if (instance == null)
            return false;

        foreach (Animator animator in instance.GetComponentsInChildren<Animator>(true))
        {
            if (animator.runtimeAnimatorController != null)
                return true;
        }

        return false;
    }

    /// <summary>粒子或 Animator 任一存在即可进入 VFX 预览。</summary>
    public static bool HasPreviewableContent(GameObject instance) =>
        HasParticleSystems(instance) || HasAnimators(instance);

    /// <summary>取控制器中最长 Clip，供时间轴与寿命估算对齐。</summary>
    static AnimationClip ResolvePrimaryClip(RuntimeAnimatorController controller)
    {
        AnimationClip[] clips = controller.animationClips;
        if (clips == null || clips.Length == 0)
            return null;

        AnimationClip primary = null;
        float maxLength = -1f;
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip == null)
                continue;

            if (clip.length >= maxLength)
            {
                maxLength = clip.length;
                primary = clip;
            }
        }

        return primary;
    }
}
