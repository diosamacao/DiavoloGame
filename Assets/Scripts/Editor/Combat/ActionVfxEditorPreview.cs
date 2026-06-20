using UnityEditor;
using UnityEngine;

/// <summary>Edit Mode 下驱动 VFX Prefab 预览（ParticleSystem 需手动 Simulate）。</summary>
public static class ActionVfxEditorPreview
{
    static double s_lastUpdateTime;

    /// <summary>实例化后重启全部粒子并清零模拟计时。</summary>
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

        ResetTiming();
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

        // 刀光类 Prefab 多为非循环粒子；播完后自动重播，避免 Scene 中只剩空轴。
        if (anyParticle && !anyAlive)
            RestartParticleSystems(instance);

        if (anyParticle)
            SceneView.RepaintAll();
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
}
