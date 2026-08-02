using NUnit.Framework;
using UnityEngine;

/// <summary>验证 Locomotion 烘焙轨按整数逻辑帧取 Δ，不依赖 NormalizedTime。</summary>
public sealed class LocomotionRootMotionTrackTests
{
    /// <summary>1 秒轨在 60Hz 下应有 60 个逻辑帧。</summary>
    [Test]
    public void GetFrameCount_MatchesLogicHz()
    {
        LocomotionRootMotionTrack track = CreateLinearTrack(duration: 1f, endZ: 1f);
        Assert.That(track.GetFrameCount(ActionSim.LogicHz), Is.EqualTo(60));
    }

    /// <summary>逐帧累加水平位移应接近轨终点（插值误差内）。</summary>
    [Test]
    public void TryGetFrameDelta_AccumulatesAlongTrack()
    {
        LocomotionRootMotionTrack track = CreateLinearTrack(duration: 0.5f, endZ: 2f);
        int frames = track.GetFrameCount(ActionSim.LogicHz);
        Vector3 accum = Vector3.zero;
        for (int i = 0; i < frames; i++)
        {
            Assert.That(
                track.TryGetFrameDelta(i, ActionSim.LogicHz, out Vector3 delta, out _),
                Is.True);
            accum += delta;
        }

        Assert.That(accum.z, Is.EqualTo(2f).Within(0.05f));
        Assert.That(Mathf.Abs(accum.x), Is.LessThan(0.01f));
    }

    /// <summary>越界帧钳到末帧，仍可取到 Δ。</summary>
    [Test]
    public void TryGetFrameDelta_ClampsPastEnd()
    {
        LocomotionRootMotionTrack track = CreateLinearTrack(duration: 0.1f, endZ: 1f);
        int last = track.GetFrameCount(ActionSim.LogicHz) - 1;
        Assert.That(
            track.TryGetFrameDelta(last, ActionSim.LogicHz, out Vector3 lastDelta, out _),
            Is.True);
        Assert.That(
            track.TryGetFrameDelta(999, ActionSim.LogicHz, out Vector3 clamped, out _),
            Is.True);
        Assert.That(clamped.z, Is.EqualTo(lastDelta.z).Within(0.0001f));
    }

    static LocomotionRootMotionTrack CreateLinearTrack(float duration, float endZ)
    {
        int count = Mathf.Max(2, Mathf.CeilToInt(duration * ActionSim.LogicHz) + 1);
        var positions = new Vector3[count];
        var yaws = new float[count];
        for (int i = 0; i < count; i++)
        {
            float u = i / (float)(count - 1);
            positions[i] = new Vector3(0f, 0f, endZ * u);
            yaws[i] = 0f;
        }

        return LocomotionRootMotionTrack.Create(duration, positions, yaws);
    }
}
