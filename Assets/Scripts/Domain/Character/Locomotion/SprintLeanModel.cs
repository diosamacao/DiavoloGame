using UnityEngine;

/// <summary>
/// L-DIR4：目标 lean 由 facing↔wish 偏角决定；实际 lean 经 SmoothDamp 切入/回正，避免硬切。
/// 符号：wish 在 facing 右侧（SignedAngle&gt;0）→ lean01&lt;0（右倾 Roll）。
/// </summary>
public sealed class SprintLeanModel
{
    const float SnapEpsilon = 0.001f;

    float _leanVelocity;

    /// <summary>当前倾身权重，范围约 [-1,1]。</summary>
    public float Lean01 { get; private set; }

    /// <summary>
    /// 由有符号偏角算目标 lean；死区内为 0。
    /// yawErrorDeg = SignedAngle(facing, wish)。
    /// </summary>
    public static float ComputeTargetLean01(float yawErrorDeg, SprintLeanSettings settings)
    {
        if (settings == null || !settings.IsEnabled)
            return 0f;

        float abs = Mathf.Abs(yawErrorDeg);
        float dead = settings.DeadZoneDeg;
        if (abs <= dead)
            return 0f;

        float span = settings.MaxEngageYawDeg - dead;
        if (span <= 0.0001f)
            return 0f;

        float amount = Mathf.Clamp01((abs - dead) / span);
        // 右偏（正角）→ 负 lean（与 Visual localEuler.z 右倾一致）
        return -Mathf.Sign(yawErrorDeg) * amount;
    }

    /// <summary>lean01 × maxLeanDeg → 视觉 Roll 度。</summary>
    public static float ToRollDegrees(float lean01, SprintLeanSettings settings)
    {
        if (settings == null)
            return 0f;
        return lean01 * settings.MaxLeanDeg;
    }

    /// <summary>
    /// 刷新 lean：先算目标，再按 engage/recover SmoothTime 逼近；目标为 0 且足够近时精确贴 0。
    /// </summary>
    public void Tick(
        SprintLeanSettings settings,
        Vector3 facingForward,
        Vector3 wishWorld,
        bool allowLean,
        float deltaTime)
    {
        float dt = Mathf.Max(0f, deltaTime);
        float target = 0f;

        if (allowLean && settings != null && settings.IsEnabled)
        {
            facingForward.y = 0f;
            wishWorld.y = 0f;
            if (facingForward.sqrMagnitude >= 0.0001f && wishWorld.sqrMagnitude >= 0.0001f)
            {
                float yawError = Vector3.SignedAngle(
                    facingForward.normalized,
                    wishWorld.normalized,
                    Vector3.up);
                target = ComputeTargetLean01(yawError, settings);
            }
        }

        // 禁用或未启用：目标恒 0，仍走回正平滑（Hit/Exit 用 Reset 硬清）
        float smoothTime = ResolveSmoothTime(Lean01, target, settings);
        if (smoothTime <= 0.001f || dt <= 0f)
        {
            Lean01 = target;
            _leanVelocity = 0f;
            return;
        }

        Lean01 = Mathf.SmoothDamp(Lean01, target, ref _leanVelocity, smoothTime, Mathf.Infinity, dt);

        // 回正尾段贴死 0，满足「对齐后 lean 观测为 0」
        if (Mathf.Abs(target) <= SnapEpsilon && Mathf.Abs(Lean01) <= SnapEpsilon)
        {
            Lean01 = 0f;
            _leanVelocity = 0f;
        }
    }

    /// <summary>|target| 增大用 engage；减小或回 0 用 recover。</summary>
    static float ResolveSmoothTime(float current, float target, SprintLeanSettings settings)
    {
        if (settings == null)
            return 0f;

        float curAbs = Mathf.Abs(current);
        float tgtAbs = Mathf.Abs(target);
        // 从静止加深必须算 engage：Unity Mathf.Sign(0)==1，Sign(0)*Sign(负目标)<0 会误走 recover。
        bool fromRest = curAbs <= SnapEpsilon;
        bool sameSide = current * target >= 0f;
        bool engaging = tgtAbs > curAbs + SnapEpsilon && (fromRest || sameSide);
        return engaging ? settings.LeanEngageSmoothTime : settings.LeanRecoverSmoothTime;
    }

    /// <summary>强制清零（离开 Locomotion / Hit 等）。</summary>
    public void Reset()
    {
        Lean01 = 0f;
        _leanVelocity = 0f;
    }
}
