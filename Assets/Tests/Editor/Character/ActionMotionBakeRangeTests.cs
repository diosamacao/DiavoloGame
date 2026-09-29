using NUnit.Framework;
using UnityEngine;

/// <summary>保护播放时序：烘焙只取播放帧范围，不能按 RM 长度扩展或缩短动作。</summary>
public sealed class ActionMotionBakeRangeTests
{
    AnimationClip clip;
    [SetUp] public void Setup()
    {
        clip = new AnimationClip { legacy = true };
        clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 1, 1));
    }
    [TearDown] public void Cleanup() => Object.DestroyImmediate(clip);
    [TestCase(61)]
    [TestCase(90)]
    public void LongerRootTrackUsesPlaybackEnd(int rootFrames)
    {
        var segment = new ActionAnimationSegment { clip = clip, endFrame = -1 };
        Assert.That(ActionMotionBakeRange.TryResolve(segment, rootFrames, 60, out int start, out int end, out _), Is.True);
        Assert.That(start, Is.Zero); Assert.That(end, Is.EqualTo(59));
    }
    [Test] public void ShorterRootTrackIsRejected()
    {
        var segment = new ActionAnimationSegment { clip = clip, endFrame = -1 };
        Assert.That(ActionMotionBakeRange.TryResolve(segment, 59, 60, out _, out _, out _), Is.False);
    }
    [Test] public void ExplicitTrimMatchesPlaybackRange()
    {
        var segment = new ActionAnimationSegment { clip = clip, startFrame = 12, endFrame = 29 };
        Assert.That(ActionMotionBakeRange.TryResolve(segment, 90, 60, out int start, out int end, out _), Is.True);
        Assert.That(end - start + 1, Is.EqualTo(segment.GetFrameCount(60)));
        Assert.That(start, Is.EqualTo(12)); Assert.That(end, Is.EqualTo(29));
    }
    [Test] public void SameLengthShiftedTrimChangesFingerprint() =>
        Assert.That(ActionMotionBakeRange.Fingerprint(clip, 0, 19), Is.Not.EqualTo(ActionMotionBakeRange.Fingerprint(clip, 10, 29)));
}
