using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>Hurt 中普通退场必须先离开 Hit，否则 SwitchOut 被丢掉、槽永久 Exiting。</summary>
public sealed class PartyExitFromHitTests
{
    /// <summary>纯帧硬直 Hurt 没有 ActionSim，切人当帧必须回到 Locomotion 才能起 SwitchOut。</summary>
    [Test]
    public void BeginPartyExit_FromFrameHit_LeavesHitForSwitchOut()
    {
        using ActorHarness harness = ActorHarness.Create("HitExit");
        harness.Actor.EnterHit(new CharacterReactionRequest(12, resolvedAction: null));

        Assert.That(harness.Actor.CurrentState, Is.EqualTo(CharacterStateType.Hit));
        harness.Actor.BeginPartyExit();

        Assert.That(harness.Actor.PartyState, Is.EqualTo(PartyMemberState.Exiting));
        Assert.That(harness.Actor.CurrentState, Is.EqualTo(CharacterStateType.Locomotion));
    }

    /// <summary>受击招已在 Recovery 时立即交接，不得停在 Hit 丢掉 SwitchOut。</summary>
    [Test]
    public void BeginPartyExit_FromHitRecovery_LeavesHitForSwitchOut()
    {
        ActionDefinition hit = CreateReadyHitActionWithRecovery(out AnimationClip clip);
        using ActorHarness harness = ActorHarness.Create("HitRecoveryExit");
        try
        {
            harness.Actor.EnterHit(new CharacterReactionRequest(12, hit));
            Assert.That(harness.Actor.CurrentState, Is.EqualTo(CharacterStateType.Hit));
            Assert.That(harness.Actor.ActionSim.IsActive, Is.True);

            harness.Actor.BeginPartyExit();

            Assert.That(harness.Actor.PartyState, Is.EqualTo(PartyMemberState.Exiting));
            Assert.That(harness.Actor.CurrentState, Is.EqualTo(CharacterStateType.Locomotion));
            Assert.That(harness.Actor.ActionSim.IsActive, Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(hit);
            if (clip != null)
                UnityEngine.Object.DestroyImmediate(clip);
        }
    }

    /// <summary>造一份 60Hz 可模拟受击招，Recovery 从第 0 帧起，便于 BeginPartyExit 立即交接。</summary>
    static ActionDefinition CreateReadyHitActionWithRecovery(out AnimationClip clip)
    {
        ActionDefinition action = ScriptableObject.CreateInstance<ActionDefinition>();
        clip = new AnimationClip { name = "HitRecoveryStub", legacy = true };
        var so = new SerializedObject(action);
        so.FindProperty("sampleRate").intValue = ActionSim.LogicHz;
        so.FindProperty("totalFrames").intValue = 12;
        SerializedProperty segments = so.FindProperty("animationSegments");
        segments.arraySize = 1;
        segments.GetArrayElementAtIndex(0).FindPropertyRelative("clip").objectReferenceValue = clip;
        SerializedProperty phases = so.FindProperty("timeline").FindPropertyRelative("phaseStates");
        phases.arraySize = 1;
        SerializedProperty recovery = phases.GetArrayElementAtIndex(0);
        recovery.FindPropertyRelative("kind").intValue = (int)ActionPhaseKind.Recovery;
        recovery.FindPropertyRelative("startFrame").intValue = 0;
        recovery.FindPropertyRelative("endFrame").intValue = 11;
        so.ApplyModifiedPropertiesWithoutUndo();
        return action;
    }

    /// <summary>最小 Actor：能 EnterHit / BeginPartyExit，不经 Factory。</summary>
    sealed class ActorHarness : IDisposable
    {
        readonly GameObject _owner;
        readonly CharacterLocomotionProfile _locomotionProfile;
        readonly CharacterAnimationProfile _animationProfile;

        ActorHarness(
            CharacterActor actor,
            GameObject owner,
            CharacterLocomotionProfile locomotionProfile,
            CharacterAnimationProfile animationProfile)
        {
            Actor = actor;
            _owner = owner;
            _locomotionProfile = locomotionProfile;
            _animationProfile = animationProfile;
        }

        public CharacterActor Actor { get; }

        public static ActorHarness Create(string name)
        {
            var owner = new GameObject(name);
            CharacterController controller = owner.AddComponent<CharacterController>();
            var input = new InputManager();
            var motor = new CharacterMotor(
                owner.transform,
                controller,
                CharacterMotorConfig.Default,
                input);
            CharacterAnimationProfile animationProfile =
                ScriptableObject.CreateInstance<CharacterAnimationProfile>();
            var animation = new CharacterAnimationService(
                new NullAnimationPlayback(),
                null,
                animationProfile);
            CharacterLocomotionProfile locomotionProfile =
                ScriptableObject.CreateInstance<CharacterLocomotionProfile>();
            var context = new CharacterContext(owner.transform, animation, controller, motor);
            context.LocomotionStateMachine = new LocomotionStateMachine(
                owner.transform,
                motor,
                animation,
                input,
                locomotionProfile,
                LocomotionFootstepPlayer.CreateSilent());
            var stateMachine = new CharacterStateMachine(context);
            var intentBuffer = new GameplayIntentBuffer(8);
            var actionSim = new ActionSim();
            context.ActionSim = actionSim;
            var actionDriver = new CharacterActionDriver(
                input,
                intentBuffer,
                stateMachine,
                actionSim,
                combatMode: null,
                resolverService: null,
                owner.transform,
                motor);
            var numeric = new NumericSystem(CharacterNumericConfig.Default);
            var vitality = new CharacterVitality(numeric);
            var targeting = new CharacterTargetingState(0, 0, 0, () => Array.Empty<IHurtboxTarget>());
            var actor = new CharacterActor(
                localInput: null,
                input,
                intentProducer: null,
                motor,
                stateMachine,
                actionDriver,
                actionSim,
                actionPresentation: null,
                combatMode: null,
                animation,
                presentation: null,
                visualMotion: null,
                numeric,
                vitality,
                intentBuffer,
                targeting,
                owner.transform);
            actor.BindSimulationInput(new SimActorId(1), new InputFrameBuffer());
            return new ActorHarness(actor, owner, locomotionProfile, animationProfile);
        }

        public void Dispose()
        {
            Actor.Dispose();
            if (_locomotionProfile != null)
                UnityEngine.Object.DestroyImmediate(_locomotionProfile);
            if (_animationProfile != null)
                UnityEngine.Object.DestroyImmediate(_animationProfile);
            if (_owner != null)
                UnityEngine.Object.DestroyImmediate(_owner);
        }
    }
}
