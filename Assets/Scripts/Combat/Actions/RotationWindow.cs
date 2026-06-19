using System;
using UnityEngine;

/// <summary>招式帧窗口：允许玩家用移动输入实时修正朝向（如前摇瞄准）。</summary>
[Serializable]
public class RotationWindow
{
    [SerializeField] int startFrame;
    [SerializeField] int endFrame = -1;
    [Tooltip("<=0 时使用 PlayerController 的 rotationSmoothTime。")]
    [SerializeField] float smoothTimeOverride;

    public int StartFrame => startFrame;
    public int EndFrame => endFrame;
    public float SmoothTimeOverride => smoothTimeOverride;

    /// <summary>Inspector 中配置了有效帧区间。</summary>
    public bool IsConfigured => endFrame >= startFrame && endFrame >= 0;

    /// <summary>指定逻辑帧是否落在此旋转修正窗口内。</summary>
    public bool IsActiveAtFrame(int frame) =>
        IsConfigured && frame >= startFrame && frame <= endFrame;

    /// <summary>返回旋转平滑时间；未覆盖时回退 defaultSmoothTime。</summary>
    public float ResolveSmoothTime(float defaultSmoothTime) =>
        smoothTimeOverride > 0f ? smoothTimeOverride : defaultSmoothTime;

    /// <summary>将帧区间限制在 [0, totalFrames - 1] 内。</summary>
    public void ClampToTotalFrames(int totalFrames)
    {
        if (totalFrames <= 0 || !IsConfigured)
            return;

        startFrame = Mathf.Clamp(startFrame, 0, totalFrames - 1);
        endFrame = Mathf.Clamp(endFrame, startFrame, totalFrames - 1);
    }
}
