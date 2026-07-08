using System;
using UnityEngine;

/// <summary>动作旋转修正区间；允许输入方向或索敌方向在窗口内影响朝向。</summary>
[Serializable]
public class RotationNotifyState : ActionNotifyState
{
    [Tooltip("<=0 时使用 CharacterMotor 的默认 rotationSmoothTime。")]
    [SerializeField] float smoothTimeOverride = 0f;

    /// <summary>旋转平滑时间覆盖值；小于等于 0 时使用默认值。</summary>
    public float SmoothTimeOverride => smoothTimeOverride;

    /// <summary>返回旋转平滑时间；未覆盖时回退 defaultSmoothTime。</summary>
    public float ResolveSmoothTime(float defaultSmoothTime) =>
        smoothTimeOverride > 0f ? smoothTimeOverride : defaultSmoothTime;
}
