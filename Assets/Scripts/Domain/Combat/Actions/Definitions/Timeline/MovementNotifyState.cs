using System;
using UnityEngine;

/// <summary>脚本位移区间窗口；用于非 RootMotion 动作在指定帧段内推进角色。</summary>
[Serializable]
public class MovementNotifyState : ActionNotifyState
{
    [SerializeField] float displacementDistance = 0f;

    /// <summary>窗口持续期间沿角色前方推进的总距离。</summary>
    public float DisplacementDistance => displacementDistance;

    /// <summary>是否配置了非零脚本位移。</summary>
    public bool HasDisplacement => Mathf.Abs(displacementDistance) > 0.001f;

    /// <summary>根据窗口帧长与动作采样率换算每秒位移速度。</summary>
    public float ResolveSpeed(float sampleRate)
    {
        if (!HasDisplacement || sampleRate <= 0f)
            return 0f;

        int frameCount = EndFrame - StartFrame + 1;
        if (frameCount <= 0)
            return 0f;

        return displacementDistance / (frameCount / sampleRate);
    }
}
