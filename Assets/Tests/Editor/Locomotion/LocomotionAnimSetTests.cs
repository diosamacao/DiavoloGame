using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>L-DIR1 AnimSet：Walk 四向槽与缺片回退 Fwd。</summary>
public sealed class LocomotionAnimSetTests
{
    sealed class FakeClips : ILocomotionAnimClipQuery
    {
        readonly HashSet<AnimationKey> _keys;

        public FakeClips(params AnimationKey[] keys) =>
            _keys = new HashSet<AnimationKey>(keys);

        public bool HasClip(AnimationKey key) => _keys.Contains(key);
    }

    [Test]
    public void Walk_Left_UsesLeftSlot_WhenBound()
    {
        var set = LocomotionAnimSet.CreateDefault();
        var clips = new FakeClips(AnimationKey.Walk, AnimationKey.WalkLeft, AnimationKey.WalkRight);
        AnimationKey key = set.ResolveLoop(LocomotionGait.Walk, MoveCardinal.Left, clips);
        Assert.That(key, Is.EqualTo(AnimationKey.WalkLeft));
    }

    [Test]
    public void Walk_Right_UsesRightSlot_WhenBound()
    {
        var set = LocomotionAnimSet.CreateDefault();
        var clips = new FakeClips(AnimationKey.Walk, AnimationKey.WalkLeft, AnimationKey.WalkRight);
        AnimationKey key = set.ResolveLoop(LocomotionGait.Walk, MoveCardinal.Right, clips);
        Assert.That(key, Is.EqualTo(AnimationKey.WalkRight));
    }

    [Test]
    public void Walk_Left_FallsBackToForward_WhenLeftMissing()
    {
        var set = LocomotionAnimSet.CreateDefault();
        var clips = new FakeClips(AnimationKey.Walk);
        AnimationKey key = set.ResolveLoop(LocomotionGait.Walk, MoveCardinal.Left, clips);
        Assert.That(key, Is.EqualTo(AnimationKey.Walk));
    }

    [Test]
    public void Walk_Back_FallsBackToForward_WhenBackUnbound()
    {
        // 默认 Back 槽=Walk；若仅有 Walk 则仍 Walk
        var set = LocomotionAnimSet.CreateDefault();
        var clips = new FakeClips(AnimationKey.Walk);
        AnimationKey key = set.ResolveLoop(LocomotionGait.Walk, MoveCardinal.Back, clips);
        Assert.That(key, Is.EqualTo(AnimationKey.Walk));
    }

    [Test]
    public void Sprint_FallsBackToRun_WhenSprintMissing()
    {
        var set = LocomotionAnimSet.CreateDefault();
        var clips = new FakeClips(AnimationKey.Run);
        AnimationKey key = set.ResolveLoop(LocomotionGait.Sprint, MoveCardinal.Forward, clips);
        Assert.That(key, Is.EqualTo(AnimationKey.Run));
    }

    [Test]
    public void Resolver_WalkLeftIntent_SelectsWalkLeft()
    {
        var resolver = new DefaultLocomotionAnimResolver();
        var clips = new FakeClips(AnimationKey.Walk, AnimationKey.WalkLeft, AnimationKey.WalkRight);
        AnimationKey key = resolver.Resolve(LocomotionGait.Walk, new Vector2(-1f, 0f), clips);
        Assert.That(key, Is.EqualTo(AnimationKey.WalkLeft));
    }

    [Test]
    public void WalkStart_Left_UsesStartLeftSlot()
    {
        var set = LocomotionAnimSet.CreateDefault();
        var clips = new FakeClips(
            AnimationKey.WalkStart,
            AnimationKey.WalkStartLeft,
            AnimationKey.WalkStartRight,
            AnimationKey.Start);
        AnimationKey key = set.ResolveStart(LocomotionGait.Walk, MoveCardinal.Left, clips);
        Assert.That(key, Is.EqualTo(AnimationKey.WalkStartLeft));
    }

    [Test]
    public void WalkStart_Left_FallsBackToForward_WhenLeftMissing()
    {
        var set = LocomotionAnimSet.CreateDefault();
        var clips = new FakeClips(AnimationKey.WalkStart, AnimationKey.Start);
        AnimationKey key = set.ResolveStart(LocomotionGait.Walk, MoveCardinal.Left, clips);
        Assert.That(key, Is.EqualTo(AnimationKey.WalkStart));
    }

    [Test]
    public void RunStart_UsesStartKey_WhenBound()
    {
        var set = LocomotionAnimSet.CreateDefault();
        var clips = new FakeClips(AnimationKey.Start, AnimationKey.WalkStart);
        AnimationKey key = set.ResolveStart(LocomotionGait.Run, MoveCardinal.Forward, clips);
        Assert.That(key, Is.EqualTo(AnimationKey.Start));
    }

    [Test]
    public void HasAnyStartClip_True_WhenOnlyWalkStart()
    {
        var set = LocomotionAnimSet.CreateDefault();
        var clips = new FakeClips(AnimationKey.WalkStart);
        Assert.That(set.HasAnyStartClip(clips), Is.True);
    }
}
