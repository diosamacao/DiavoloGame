using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>验证真实 Playable 混合图的换向连续性、时钟隔离与片段释放，不依赖角色资源。</summary>
public sealed class PlayableMovementBlendTests
{
    GameObject _root;
    PlayableAnimationPlayback _playback;
    readonly List<AnimationClip> _clips = new();

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("MovementBlendTest");
        new GameObject("Bone").transform.SetParent(_root.transform, false);
        _playback = new PlayableAnimationPlayback(_root.AddComponent<Animator>());
    }

    [TearDown]
    public void TearDown()
    {
        _playback.Dispose();
        Object.DestroyImmediate(_root);
        foreach (AnimationClip clip in _clips) Object.DestroyImmediate(clip);
        _clips.Clear();
    }

    AnimationClip Clip(float position)
    {
        var clip = new AnimationClip();
        clip.SetCurve("Bone", typeof(Transform), "m_LocalPosition.x", AnimationCurve.Constant(0f, 4f, position));
        _clips.Add(clip);
        return clip;
    }

    AnimationMixerPlayable Mixer => (AnimationMixerPlayable)typeof(PlayableAnimationPlayback)
        .GetField("_mixer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_playback);

    float Weight(AnimationClip clip)
    {
        var mixer = Mixer;
        for (int i = 0; i < mixer.GetInputCount(); i++)
            if (((AnimationClipPlayable)mixer.GetInput(i)).GetAnimationClip() == clip)
                return mixer.GetInputWeight(i);
        return 0f;
    }

    double Time(AnimationClip clip)
    {
        var mixer = Mixer;
        for (int i = 0; i < mixer.GetInputCount(); i++)
            if (((AnimationClipPlayable)mixer.GetInput(i)).GetAnimationClip() == clip)
                return mixer.GetInput(i).GetTime();
        Assert.Fail("Expected clip in mixer");
        return 0;
    }

    [Test]
    public void InterruptedHalfSecondFade_PreservesPoseLeftWeightsBeforeRightFade()
    {
        var pose = Clip(0); var left = Clip(-1); var right = Clip(1); var clock = new object();
        _playback.Play(pose, 0, clock);
        _playback.Play(left, .5f, clock);
        _playback.Tick(.2f);
        _playback.Play(right, .5f, clock);
        Assert.That(Weight(pose), Is.EqualTo(.6f).Within(.0001));
        Assert.That(Weight(left), Is.EqualTo(.4f).Within(.0001));
        Assert.That(Weight(right), Is.Zero);
        _playback.Tick(.25f);
        Assert.That(Weight(pose), Is.EqualTo(.3f).Within(.0001));
        Assert.That(Weight(left), Is.EqualTo(.2f).Within(.0001));
        Assert.That(Weight(right), Is.EqualTo(.5f).Within(.0001));
        _playback.Tick(.25f);
        Assert.That(Mixer.GetInputCount(), Is.EqualTo(1));
        Assert.That(Weight(right), Is.EqualTo(1));
    }

    [Test]
    public void RepeatedReversalAndRelease_ReusesClipsWithoutWeightDiscontinuity()
    {
        var clips = new[] { Clip(0), Clip(-1), Clip(1) }; var clock = new object();
        _playback.Play(clips[0], 0, clock);
        for (int step = 0; step < 120; step++)
        {
            var before = new[] { Weight(clips[0]), Weight(clips[1]), Weight(clips[2]) };
            _playback.Play(clips[(step + 1) % 3], .5f, clock);
            for (int i = 0; i < clips.Length; i++)
                Assert.That(Weight(clips[i]), Is.EqualTo(before[i]).Within(.0001));
            Assert.That(Mixer.GetInputCount(), Is.LessThanOrEqualTo(3));
            _playback.Tick(1f / 60);
            Assert.That(Weight(clips[0]) + Weight(clips[1]) + Weight(clips[2]), Is.EqualTo(1f).Within(.0001));
        }
    }

    [Test]
    public void Seek_SynchronizesFadingDirectionsButDoesNotRewindPreviousActionSegment()
    {
        var pose = Clip(0); var left = Clip(-1); var cancel = Clip(1); var clock = new object();
        _playback.Play(pose, 0, clock);
        _playback.Seek(1);
        _playback.Play(left, .5f, clock);
        _playback.Tick(.1f);
        _playback.Seek(2);
        Assert.That(Time(pose), Is.EqualTo(2).Within(.0001));
        Assert.That(Time(left), Is.EqualTo(2).Within(.0001));
        _playback.Play(cancel, .5f, new object());
        _playback.Seek(.1f);
        Assert.That(Time(pose), Is.EqualTo(2).Within(.0001));
        Assert.That(Time(left), Is.EqualTo(2).Within(.0001));
        Assert.That(Time(cancel), Is.EqualTo(.1f).Within(.0001));
    }

    [Test]
    public void Freeze_PreservesWeights_AndHardCutReleasesAllOutgoingClips()
    {
        var left = Clip(-1); var right = Clip(1);
        _playback.Play(left, 0);
        _playback.Play(right, .5f);
        _playback.Tick(.2f);
        _playback.Speed = 0;
        _playback.Tick(.5f);
        Assert.That(Weight(left), Is.EqualTo(.6f).Within(.0001));
        Assert.That(Weight(right), Is.EqualTo(.4f).Within(.0001));
        _playback.Play(left, 0);
        Assert.That(Mixer.GetInputCount(), Is.EqualTo(1));
        Assert.That(Weight(left), Is.EqualTo(1));
        Assert.That(Time(left), Is.Zero);
    }
}
