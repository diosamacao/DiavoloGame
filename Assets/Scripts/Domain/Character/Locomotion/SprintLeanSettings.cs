using System;
using UnityEngine;

/// <summary>
/// L-DIR4 Sprint 倾身参数；挂在 LocomotionProfile。maxLeanDeg=0 关闭。
/// </summary>
[Serializable]
public sealed class SprintLeanSettings
{
    [Tooltip("最大视觉倾角（度）；0=关闭。")]
    [SerializeField, Min(0f)] float maxLeanDeg = 8f;

    [Tooltip("偏角死区（度）；|yawError| 小于此值时目标 lean 为 0。")]
    [SerializeField, Min(0f)] float deadZoneDeg = 4f;

    [Tooltip("达到满倾所需偏角（度）；须大于 deadZone。")]
    [SerializeField, Min(0.01f)] float maxEngageYawDeg = 45f;

    [Tooltip("从直立切入目标倾角的 SmoothDamp 时间（秒）；越大越柔。0=瞬时。")]
    [SerializeField, Min(0f)] float leanEngageSmoothTime = 0.22f;

    [Tooltip("倾角回到 0（对齐 / 松手停转）的 SmoothDamp 时间（秒）；越大回正越柔。0=瞬时。")]
    [SerializeField, Min(0f)] float leanRecoverSmoothTime = 0.28f;

    /// <summary>最大倾角（度）。</summary>
    public float MaxLeanDeg => Mathf.Max(0f, maxLeanDeg);

    /// <summary>死区（度）。</summary>
    public float DeadZoneDeg => Mathf.Max(0f, deadZoneDeg);

    /// <summary>满倾偏角（度），至少比死区大一点。</summary>
    public float MaxEngageYawDeg => Mathf.Max(DeadZoneDeg + 0.01f, maxEngageYawDeg);

    /// <summary>切入倾身平滑时间（秒）。</summary>
    public float LeanEngageSmoothTime => Mathf.Max(0f, leanEngageSmoothTime);

    /// <summary>回正平滑时间（秒）。</summary>
    public float LeanRecoverSmoothTime => Mathf.Max(0f, leanRecoverSmoothTime);

    /// <summary>是否启用倾身。</summary>
    public bool IsEnabled => MaxLeanDeg > 0.001f;

    /// <summary>运行时/单测构造。</summary>
    public SprintLeanSettings(
        float maxLeanDeg = 8f,
        float deadZoneDeg = 4f,
        float maxEngageYawDeg = 45f,
        float leanEngageSmoothTime = 0.22f,
        float leanRecoverSmoothTime = 0.28f)
    {
        this.maxLeanDeg = maxLeanDeg;
        this.deadZoneDeg = deadZoneDeg;
        this.maxEngageYawDeg = maxEngageYawDeg;
        this.leanEngageSmoothTime = leanEngageSmoothTime;
        this.leanRecoverSmoothTime = leanRecoverSmoothTime;
    }
}
