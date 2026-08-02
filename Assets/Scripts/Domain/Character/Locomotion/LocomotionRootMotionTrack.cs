using System;
using UnityEngine;

/// <summary>从 AnimationClip 烘焙的根位移/朝向采样轨；运行时按逻辑帧索引取 Δ。</summary>
[Serializable]
public struct LocomotionRootMotionTrack
{
    [SerializeField] bool valid;
    [SerializeField] float duration;
    [SerializeField] Vector3[] localPositions;
    [SerializeField] float[] localYaws;

    public static LocomotionRootMotionTrack Empty => new()
    {
        valid = false,
        duration = 0f,
        localPositions = Array.Empty<Vector3>(),
        localYaws = Array.Empty<float>(),
    };

    public bool IsValid =>
        valid && duration > 0f && localPositions != null && localPositions.Length >= 2;

    public float Duration => duration;

    public int SampleCount => localPositions != null ? localPositions.Length : 0;

    /// <summary>构造已烘焙轨道；positions/yaws 为 Clip 局部空间绝对采样。</summary>
    public static LocomotionRootMotionTrack Create(float clipDuration, Vector3[] positions, float[] yaws)
    {
        if (positions == null || positions.Length < 2 || yaws == null || yaws.Length != positions.Length)
            return Empty;

        return new LocomotionRootMotionTrack
        {
            valid = true,
            duration = Mathf.Max(0.0001f, clipDuration),
            localPositions = positions,
            localYaws = yaws,
        };
    }

    /// <summary>按 logicHz 计算轨覆盖的逻辑帧数（至少 1）。</summary>
    public int GetFrameCount(int logicHz)
    {
        if (!IsValid)
            return 0;

        int hz = Mathf.Max(1, logicHz);
        return Mathf.Max(1, Mathf.CeilToInt(duration * hz));
    }

    /// <summary>取第 frame 逻辑帧的局部位移/偏航；越界钳到最后一帧。</summary>
    public bool TryGetFrameDelta(
        int frame,
        int logicHz,
        out Vector3 localPositionDelta,
        out float localYawDelta)
    {
        localPositionDelta = Vector3.zero;
        localYawDelta = 0f;
        if (!IsValid)
            return false;

        int hz = Mathf.Max(1, logicHz);
        int frameCount = GetFrameCount(hz);
        int index = frame < 0 ? 0 : frame;
        if (index >= frameCount)
            index = frameCount - 1;

        float t0 = index / (float)hz;
        float t1 = (index + 1) / (float)hz;
        return TryGetDelta(t0, t1, out localPositionDelta, out localYawDelta);
    }

    /// <summary>在 [timePrev, timeNext] 上取局部位移与偏航增量（秒）；供烘焙校验与帧换算。</summary>
    public bool TryGetDelta(float timePrev, float timeNext, out Vector3 localPositionDelta, out float localYawDelta)
    {
        localPositionDelta = Vector3.zero;
        localYawDelta = 0f;
        if (!IsValid)
            return false;

        float t0 = Mathf.Clamp(timePrev, 0f, duration);
        float t1 = Mathf.Clamp(timeNext, 0f, duration);
        if (t1 <= t0)
            return false;

        Vector3 p0 = SamplePosition(t0);
        Vector3 p1 = SamplePosition(t1);
        localPositionDelta = p1 - p0;
        localYawDelta = Mathf.DeltaAngle(SampleYaw(t0), SampleYaw(t1));
        return true;
    }

    Vector3 SamplePosition(float timeSeconds)
    {
        SampleIndices(timeSeconds, out int i0, out int i1, out float u);
        return Vector3.LerpUnclamped(localPositions[i0], localPositions[i1], u);
    }

    float SampleYaw(float timeSeconds)
    {
        SampleIndices(timeSeconds, out int i0, out int i1, out float u);
        return Mathf.LerpAngle(localYaws[i0], localYaws[i1], u);
    }

    void SampleIndices(float timeSeconds, out int i0, out int i1, out float u)
    {
        int last = localPositions.Length - 1;
        if (duration <= 0.0001f || last <= 0)
        {
            i0 = i1 = 0;
            u = 0f;
            return;
        }

        float norm = Mathf.Clamp01(timeSeconds / duration);
        float f = norm * last;
        i0 = Mathf.FloorToInt(f);
        i1 = Mathf.Min(i0 + 1, last);
        u = f - i0;
    }
}
