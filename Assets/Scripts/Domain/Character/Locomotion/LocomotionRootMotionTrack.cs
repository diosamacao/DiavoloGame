using System;
using UnityEngine;

/// <summary>从 AnimationClip 烘焙的根位移/朝向采样轨；运行时按逻辑帧索引取 Δ。</summary>
[Serializable]
public struct LocomotionRootMotionTrack
{
    [SerializeField] bool valid;
    [SerializeField, Min(1)] int frameCount;
    [SerializeField] Vector3[] localPositions;
    [SerializeField] float[] localYaws;

    public static LocomotionRootMotionTrack Empty => new()
    {
        valid = false,
        frameCount = 0,
        localPositions = Array.Empty<Vector3>(),
        localYaws = Array.Empty<float>(),
    };

    public bool IsValid =>
        valid && frameCount > 0 && localPositions != null && localPositions.Length == frameCount + 1;

    /// <summary>轨道覆盖的 60Hz 逻辑帧数。</summary>
    public int FrameCount => frameCount;

    public int SampleCount => localPositions != null ? localPositions.Length : 0;

    /// <summary>返回进入轨道到指定下一采样帧前的累计偏航，用于从复制朝向反推根运动基。</summary>
    public float GetAccumulatedYawDegrees(int phaseFrame)
    {
        if (!IsValid || localYaws == null || localYaws.Length != frameCount + 1)
            return 0f;
        int sample = Mathf.Clamp(phaseFrame, 0, frameCount);
        return Mathf.DeltaAngle(localYaws[0], localYaws[sample]);
    }

    /// <summary>构造已烘焙轨道；positions/yaws 为 Clip 局部空间绝对采样。</summary>
    public static LocomotionRootMotionTrack Create(int frames, Vector3[] positions, float[] yaws)
    {
        if (frames <= 0
            || positions == null
            || positions.Length != frames + 1
            || yaws == null
            || yaws.Length != positions.Length)
            return Empty;

        return new LocomotionRootMotionTrack
        {
            valid = true,
            frameCount = frames,
            localPositions = positions,
            localYaws = yaws,
        };
    }

    /// <summary>返回烘焙时固定的逻辑帧数；仅接受项目 60Hz。</summary>
    public int GetFrameCount(int logicHz)
    {
        if (!IsValid || logicHz != ActionSim.LogicHz)
            return 0;
        return frameCount;
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

        int hz = logicHz;
        int frameCount = GetFrameCount(hz);
        if (frameCount <= 0)
            return false;
        int index = frame < 0 ? 0 : frame;
        if (index >= frameCount)
            index = frameCount - 1;

        localPositionDelta = localPositions[index + 1] - localPositions[index];
        localYawDelta = Mathf.DeltaAngle(localYaws[index], localYaws[index + 1]);
        return true;
    }

#if UNITY_EDITOR
    /// <summary>Editor 烘焙校验按秒区间采样；运行时 Locomotion 禁止调用。</summary>
    public bool TryGetDelta(
        float timePrev,
        float timeNext,
        out Vector3 localPositionDelta,
        out float localYawDelta)
    {
        localPositionDelta = Vector3.zero;
        localYawDelta = 0f;
        if (!IsValid || timeNext <= timePrev)
            return false;

        float maxSeconds = frameCount / (float)ActionSim.LogicHz;
        float t0 = Mathf.Clamp(timePrev, 0f, maxSeconds);
        float t1 = Mathf.Clamp(timeNext, 0f, maxSeconds);
        if (t1 <= t0)
            return false;

        SampleEditor(t0, out Vector3 p0, out float y0);
        SampleEditor(t1, out Vector3 p1, out float y1);
        localPositionDelta = p1 - p0;
        localYawDelta = Mathf.DeltaAngle(y0, y1);
        return true;
    }

    /// <summary>Editor 秒采样在相邻 60Hz 烘焙点间线性插值。</summary>
    void SampleEditor(float seconds, out Vector3 position, out float yaw)
    {
        float sample = Mathf.Clamp(seconds * ActionSim.LogicHz, 0f, frameCount);
        int i0 = Mathf.FloorToInt(sample);
        int i1 = Mathf.Min(i0 + 1, frameCount);
        float t = sample - i0;
        position = Vector3.LerpUnclamped(localPositions[i0], localPositions[i1], t);
        yaw = Mathf.LerpAngle(localYaws[i0], localYaws[i1], t);
    }
#endif
}
