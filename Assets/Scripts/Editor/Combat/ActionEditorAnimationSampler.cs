using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Edit Mode 下用 AnimationMode 将 ActionDefinition 的 Clip 采样到 Preview Character。</summary>
public static class ActionEditorAnimationSampler
{
    static GameObject s_sampleRoot;
    static Animator s_animator;
    static Transform s_boundPreviewCharacter;
    static bool s_animatorWasEnabled;

    public static bool IsSessionActive => AnimationMode.InAnimationMode();

    /// <summary>在 Preview Character 上查找 Animator 采样根节点。</summary>
    public static bool TryResolveSampleRoot(Transform previewCharacter, out GameObject sampleRoot, out Animator animator)
    {
        sampleRoot = null;
        animator = null;

        if (previewCharacter == null)
            return false;

        animator = previewCharacter.GetComponentInChildren<Animator>();

        if (animator == null)
            return false;

        sampleRoot = animator.gameObject;
        return true;
    }

    /// <summary>开启 AnimationMode 并暂时禁用 Animator，避免与采样结果冲突。</summary>
    public static bool BeginSession(Transform previewCharacter)
    {
        // 热路径：同一 Preview Character 已在 AnimationMode 时跳过 GetComponentInChildren。
        if (previewCharacter != null
            && s_boundPreviewCharacter == previewCharacter
            && s_sampleRoot != null
            && IsSessionActive)
            return true;

        if (!TryResolveSampleRoot(previewCharacter, out GameObject sampleRoot, out Animator animator))
            return false;

        if (s_sampleRoot == sampleRoot && IsSessionActive)
        {
            s_boundPreviewCharacter = previewCharacter;
            return true;
        }

        EndSession();

        s_sampleRoot = sampleRoot;
        s_animator = animator;
        s_boundPreviewCharacter = previewCharacter;
        s_animatorWasEnabled = animator.enabled;
        animator.enabled = false;
        AnimationMode.StartAnimationMode();
        return true;
    }

    /// <summary>将 Clip 采样到 previewTimeSeconds；sampleRate 与 ActionDefinition 逻辑帧对齐。</summary>
    public static void Sample(AnimationClip clip, float previewTimeSeconds, float sampleRate)
    {
        if (clip == null || s_sampleRoot == null || !IsSessionActive)
            return;

        // Unity 2022.3 SampleAnimationClip 仅接受 (root, clip, time)；逻辑帧对齐由 previewTimeSeconds 保证。
        float time = Mathf.Clamp(previewTimeSeconds, 0f, clip.length);

        AnimationMode.BeginSampling();
        AnimationMode.SampleAnimationClip(s_sampleRoot, clip, time);
        AnimationMode.EndSampling();
    }

    /// <summary>结束采样并恢复 Animator 状态。</summary>
    public static void EndSession()
    {
        if (IsSessionActive)
            AnimationMode.StopAnimationMode();

        if (s_animator != null)
            s_animator.enabled = s_animatorWasEnabled;

        s_sampleRoot = null;
        s_animator = null;
        s_boundPreviewCharacter = null;
    }
}
