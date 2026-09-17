using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>验证 Locomotion 整数时钟、Full/Headless 采样一致性与帧落脚。</summary>
public sealed class LocomotionIntegerClockTests
{
    /// <summary>600 Tick 中 Full 的伪播放头即使报告错误完成态，也与 Headless 消费相同 Key+PhaseFrame。</summary>
    [Test]
    public void FullAndHeadless_ConsumeSameKeyAndPhaseFrame_For600Ticks()
    {
        AnimationClip clip = new AnimationClip();
        CharacterAnimationProfile profile = BuildProfile(AnimationKey.Run, clip);
        var fullPlayback = new AdversarialPlayback();
        var full = new CharacterAnimationService(fullPlayback, null, profile);
        var headless = new CharacterAnimationService(new NullAnimationPlayback(), null, profile);
        var timing = new LocomotionClipTiming(AnimationKey.Run, 60, true, 60, 60);

        for (int phaseFrame = 0; phaseFrame < 600; phaseFrame++)
        {
            full.SampleLocomotion(AnimationKey.Run, phaseFrame, in timing);
            headless.SampleLocomotion(AnimationKey.Run, phaseFrame, in timing);
            Assert.That(full.CurrentKey, Is.EqualTo(headless.CurrentKey));
            Assert.That(fullPlayback.LastSeekSeconds, Is.EqualTo((phaseFrame % 60) / 60f).Within(0.0001f));
        }

        Assert.That(fullPlayback.NormalizedTime, Is.EqualTo(999f));
        Assert.That(fullPlayback.HasFinished, Is.True);
        full.Dispose();
        headless.Dispose();
        Object.DestroyImmediate(profile);
        Object.DestroyImmediate(clip);
    }

    /// <summary>整数落脚标记在 600 Tick/60 帧周期内精确触发十次。</summary>
    [Test]
    public void FootCycle_IntegerMarker_FiresTenTimesAcross600Ticks()
    {
        var cycle = new LocomotionFootCycle();
        cycle.SetMarkers(new[]
        {
            new FootPlantMarker { frame = 10, foot = FootSide.Left },
        });
        cycle.Unfreeze();
        int fired = 0;
        for (int frame = 0; frame < 600; frame++)
        {
            cycle.Tick(frame, durationFrames: 60, loop: true);
            if (cycle.PlantedThisFrame.HasValue)
                fired++;
        }

        Assert.That(fired, Is.EqualTo(10));
    }

    /// <summary>RootMotion 同一 PhaseFrame 重采样得到相同增量，不依赖内部可变游标。</summary>
    [Test]
    public void RootMotionPlayer_SamplesDirectlyByPhaseFrame()
    {
        CharacterLocomotionProfile profile = ScriptableObject.CreateInstance<CharacterLocomotionProfile>();
        var track = LocomotionRootMotionTrack.Create(
            2,
            new[] { Vector3.zero, Vector3.forward, Vector3.forward * 3f },
            new[] { 0f, 10f, 30f });
        profile.SetRootMotionTrack(AnimationKey.StopR, track);
        var player = new LocomotionRootMotionPlayer(profile);
        player.Begin(AnimationKey.StopR, Quaternion.identity);

        Assert.That(player.TrySample(1, true, out Vector3 first, out float firstYaw), Is.True);
        Assert.That(player.TrySample(1, true, out Vector3 replay, out float replayYaw), Is.True);
        Assert.That(replay, Is.EqualTo(first));
        Assert.That(replayYaw, Is.EqualTo(firstYaw));
        Assert.That(track.GetAccumulatedYawDegrees(2), Is.EqualTo(30f).Within(0.001f));

        Object.DestroyImmediate(profile);
    }

    /// <summary>缺失或重复 timing 必须失败，禁止运行时猜默认 Clip 时长。</summary>
    [Test]
    public void ProfileTiming_MissingOrDuplicate_IsRejected()
    {
        CharacterLocomotionProfile profile = ScriptableObject.CreateInstance<CharacterLocomotionProfile>();
        Assert.That(profile.TryGetClipTiming(AnimationKey.Run, out _), Is.False);

        var timing = new LocomotionClipTiming(AnimationKey.Run, 60, true, 60, 60);
        profile.SetClipTimings(new[] { timing, timing });
        Assert.That(profile.TryGetClipTiming(AnimationKey.Run, out _), Is.False);
        Assert.Throws<System.InvalidOperationException>(
            () => profile.RequireClipTiming(AnimationKey.Run));

        Object.DestroyImmediate(profile);
    }

    /// <summary>构造只含一个键的动画 Profile，避免依赖项目资产。</summary>
    static CharacterAnimationProfile BuildProfile(AnimationKey key, AnimationClip clip)
    {
        CharacterAnimationProfile profile = ScriptableObject.CreateInstance<CharacterAnimationProfile>();
        var entries = new[]
        {
            new CharacterAnimationProfile.Entry { Key = key, Clip = clip },
        };
        typeof(CharacterAnimationProfile)
            .GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(profile, entries);
        return profile;
    }

    /// <summary>故意返回错误 NormalizedTime/HasFinished，确保它们不影响整数采样调用。</summary>
    sealed class AdversarialPlayback : IAnimationPlayback
    {
        /// <inheritdoc />
        public bool IsValid => true;
        /// <inheritdoc />
        public float Speed { get; set; } = 1f;
        /// <inheritdoc />
        public AnimationClip CurrentClip { get; private set; }
        /// <inheritdoc />
        public float NormalizedTime => 999f;
        /// <inheritdoc />
        public bool HasFinished => true;
        /// <inheritdoc />
        public float AdditiveWeight => 0f;
        /// <summary>最近一次由 PhaseFrame 折算出的采样秒数。</summary>
        public float LastSeekSeconds { get; private set; }

        /// <inheritdoc />
        public void Play(AnimationClip clip, float fadeDuration) => CurrentClip = clip;
        /// <inheritdoc />
        public void PlayAdditive(AnimationClip clip, AvatarMask mask, float fadeDuration) { }
        /// <inheritdoc />
        public void StopAdditive() { }
        /// <inheritdoc />
        public void Seek(float timeSeconds) => LastSeekSeconds = timeSeconds;
        /// <inheritdoc />
        public void Tick(float deltaTime) { }
        /// <inheritdoc />
        public void Dispose() { }
    }
}
