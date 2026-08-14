using NUnit.Framework;
using UnityEngine;

/// <summary>GaitPolicy / AnimResolver EditMode 覆盖（L-GP1 / L-GP3）。</summary>
public sealed class LocomotionGaitPolicyTests
{
    [Test]
    public void Evaluate_FullPolicy_RunHold_ReachesSprint()
    {
        var policy = new LocomotionGaitPolicy(LocomotionGait.Sprint, allowPivot: true, sprintAfterRunSeconds: 3f);
        float hold = 0f;
        LocomotionGait gait = LocomotionGait.Walk;

        // 进 Run
        GaitPolicyResult r0 = policy.Evaluate(new GaitPolicyInput(gait, 1f, 0.5f, 0.1f, hold));
        Assert.That(r0.NextGait, Is.EqualTo(LocomotionGait.Run));
        gait = r0.NextGait;
        hold = r0.RunHoldSeconds;

        // 一次累满 3s，避免 0.1f 循环加法的二进制误差。
        GaitPolicyResult rSprint = policy.Evaluate(new GaitPolicyInput(gait, 1f, 0.5f, 3f, hold));
        Assert.That(rSprint.NextGait, Is.EqualTo(LocomotionGait.Sprint));
    }

    [Test]
    public void Evaluate_MaxGaitRun_NeverSprints()
    {
        var policy = new LocomotionGaitPolicy(LocomotionGait.Run, allowPivot: false, sprintAfterRunSeconds: 0.1f);
        LocomotionGait gait = LocomotionGait.Walk;
        float hold = 0f;

        for (int i = 0; i < 100; i++)
        {
            GaitPolicyResult r = policy.Evaluate(new GaitPolicyInput(gait, 1f, 0.5f, 0.1f, hold));
            gait = r.NextGait;
            hold = r.RunHoldSeconds;
        }

        Assert.That(gait, Is.EqualTo(LocomotionGait.Run));
        Assert.That(policy.AllowsPivot(gait), Is.False);
    }

    [Test]
    public void Evaluate_MaxGaitWalk_StaysWalk()
    {
        var policy = new LocomotionGaitPolicy(LocomotionGait.Walk, allowPivot: false, sprintAfterRunSeconds: 3f);
        GaitPolicyResult r = policy.Evaluate(
            new GaitPolicyInput(LocomotionGait.Walk, 1f, 0.5f, 0.1f, 0f));
        Assert.That(r.NextGait, Is.EqualTo(LocomotionGait.Walk));
    }

    [Test]
    public void Evaluate_LowMagnitude_DropsToWalk()
    {
        var policy = new LocomotionGaitPolicy();
        GaitPolicyResult r = policy.Evaluate(
            new GaitPolicyInput(LocomotionGait.Run, 0.1f, 0.5f, 0.1f, 2f));
        Assert.That(r.NextGait, Is.EqualTo(LocomotionGait.Walk));
        Assert.That(r.RunHoldSeconds, Is.EqualTo(0f));
    }

    [Test]
    public void AllowsPivot_OnlySprintWhenEnabled()
    {
        var policy = new LocomotionGaitPolicy(LocomotionGait.Sprint, allowPivot: true);
        Assert.That(policy.AllowsPivot(LocomotionGait.Sprint), Is.True);
        Assert.That(policy.AllowsPivot(LocomotionGait.Run), Is.False);
    }

    [Test]
    public void AnimResolver_WalkLateral_UsesWalkLeftRight_WhenClipsExist()
    {
        var resolver = new DefaultLocomotionAnimResolver();
        var clips = new FakeClips(walkLeft: true, walkRight: true, sprint: false);

        Assert.That(
            resolver.Resolve(LocomotionGait.Walk, new Vector2(-1f, 0f), clips),
            Is.EqualTo(AnimationKey.WalkLeft));
        Assert.That(
            resolver.Resolve(LocomotionGait.Walk, new Vector2(1f, 0f), clips),
            Is.EqualTo(AnimationKey.WalkRight));
    }

    [Test]
    public void AnimResolver_WalkLateral_FallsBackWalk_WhenNoSideClips()
    {
        var resolver = new DefaultLocomotionAnimResolver();
        var clips = new FakeClips(walkLeft: false, walkRight: false, sprint: false);

        Assert.That(
            resolver.Resolve(LocomotionGait.Walk, new Vector2(-1f, 0f), clips),
            Is.EqualTo(AnimationKey.Walk));
    }

    [Test]
    public void AnimResolver_Run_IgnoresLateralKeys()
    {
        var resolver = new DefaultLocomotionAnimResolver();
        var clips = new FakeClips(walkLeft: true, walkRight: true, sprint: true);

        Assert.That(
            resolver.Resolve(LocomotionGait.Run, new Vector2(-1f, 0f), clips),
            Is.EqualTo(AnimationKey.Run));
    }

    [Test]
    public void ResolveStart_WalkForward_PrefersWalkStart()
    {
        var set = LocomotionAnimSet.CreateDefault();
        var clips = new FakeClips(
            walkStart: true,
            walkStartLeft: true,
            walkStartRight: true,
            runStart: true);
        Assert.That(
            set.ResolveStart(LocomotionGait.Walk, MoveCardinal.Forward, clips),
            Is.EqualTo(AnimationKey.WalkStart));
    }

    [Test]
    public void ResolveStart_WalkLateral_UsesWalkStartLeftRight()
    {
        var set = LocomotionAnimSet.CreateDefault();
        var clips = new FakeClips(
            walkStart: true,
            walkStartLeft: true,
            walkStartRight: true,
            runStart: true);
        Assert.That(
            set.ResolveStart(LocomotionGait.Walk, MoveCardinal.Left, clips),
            Is.EqualTo(AnimationKey.WalkStartLeft));
        Assert.That(
            set.ResolveStart(LocomotionGait.Walk, MoveCardinal.Right, clips),
            Is.EqualTo(AnimationKey.WalkStartRight));
    }

    [Test]
    public void ResolveStart_WalkLateral_FallsBackWalkStart_WhenNoSideStart()
    {
        var set = LocomotionAnimSet.CreateDefault();
        var clips = new FakeClips(walkStart: true, walkStartLeft: false, walkStartRight: false, runStart: true);
        Assert.That(
            set.ResolveStart(LocomotionGait.Walk, MoveCardinal.Left, clips),
            Is.EqualTo(AnimationKey.WalkStart));
    }

    [Test]
    public void ResolveStart_Run_PrefersStart()
    {
        var set = LocomotionAnimSet.CreateDefault();
        var clips = new FakeClips(walkStart: true, walkStartLeft: true, walkStartRight: true, runStart: true);
        Assert.That(
            set.ResolveStart(LocomotionGait.Run, MoveCardinal.Forward, clips),
            Is.EqualTo(AnimationKey.Start));
    }

    [Test]
    public void ResolveStart_Walk_FallsBackToStart()
    {
        var set = LocomotionAnimSet.CreateDefault();
        var clips = new FakeClips(walkStart: false, walkStartLeft: false, walkStartRight: false, runStart: true);
        Assert.That(
            set.ResolveStart(LocomotionGait.Walk, MoveCardinal.Forward, clips),
            Is.EqualTo(AnimationKey.Start));
    }

    sealed class FakeClips : ILocomotionAnimClipQuery
    {
        readonly bool _walkLeft;
        readonly bool _walkRight;
        readonly bool _sprint;
        readonly bool _walkStart;
        readonly bool _walkStartLeft;
        readonly bool _walkStartRight;
        readonly bool _runStart;

        public FakeClips(
            bool walkLeft = false,
            bool walkRight = false,
            bool sprint = false,
            bool walkStart = false,
            bool walkStartLeft = false,
            bool walkStartRight = false,
            bool runStart = true)
        {
            _walkLeft = walkLeft;
            _walkRight = walkRight;
            _sprint = sprint;
            _walkStart = walkStart;
            _walkStartLeft = walkStartLeft;
            _walkStartRight = walkStartRight;
            _runStart = runStart;
        }

        public bool HasClip(AnimationKey key) => key switch
        {
            AnimationKey.WalkLeft => _walkLeft,
            AnimationKey.WalkRight => _walkRight,
            AnimationKey.Sprint => _sprint,
            AnimationKey.WalkStart => _walkStart,
            AnimationKey.WalkStartLeft => _walkStartLeft,
            AnimationKey.WalkStartRight => _walkStartRight,
            AnimationKey.Start => _runStart,
            AnimationKey.Walk => true,
            AnimationKey.Run => true,
            _ => false,
        };
    }
}
