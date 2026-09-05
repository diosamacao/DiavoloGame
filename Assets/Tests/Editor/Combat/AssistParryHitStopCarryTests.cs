using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>弹刀 Success 起手后把 carry 写回新实例；其它起手只清空。</summary>
public sealed class AssistParryHitStopCarryTests
{
    /// <summary>Begin 清 freeze 后，NotifyActionBegun(Success) 把 carry 写到新实例。</summary>
    [Test]
    public void NotifyActionBegun_AssistParrySuccess_ReappliesCarry()
    {
        using ActorHarness harness = ActorHarness.Create("CarrySuccess");
        ActionDefinition guard = CreateReadyAction("Guard");
        ActionDefinition success = CreateReadyAction("Success");
        try
        {
            Assert.That(
                harness.Actor.ActionSim.TryStart(ActionSimResolveResult.FromContent(guard)),
                Is.True);
            harness.Actor.ArmAssistParryHitStopCarry(8);
            Assert.That(harness.Actor.TryRequestHitStopOnCurrentAction(8, oncePerAction: true), Is.True);
            Assert.That(harness.Actor.ActionSim.FreezeFrames, Is.EqualTo(8));

            harness.Actor.ActionSim.Stop();
            Assert.That(
                harness.Actor.ActionSim.TryStart(ActionSimResolveResult.FromContent(success)),
                Is.True);
            Assert.That(harness.Actor.ActionSim.FreezeFrames, Is.Zero);

            harness.Actor.NotifyActionBegun(GameplayIntentType.AssistParrySuccess);
            Assert.That(harness.Actor.ActionSim.FreezeFrames, Is.EqualTo(8));
            Assert.That(harness.Actor.ActionSim.CurrentFrame, Is.Zero);
        }
        finally
        {
            DestroyAction(guard);
            DestroyAction(success);
        }
    }

    /// <summary>非 Success 起手只丢 carry，不把卡肉写到下一招。</summary>
    [Test]
    public void NotifyActionBegun_OtherIntent_ClearsCarryWithoutApplying()
    {
        using ActorHarness harness = ActorHarness.Create("CarryAttack");
        ActionDefinition next = CreateReadyAction("Attack");
        try
        {
            harness.Actor.ArmAssistParryHitStopCarry(8);
            Assert.That(
                harness.Actor.ActionSim.TryStart(ActionSimResolveResult.FromContent(next)),
                Is.True);
            harness.Actor.NotifyActionBegun(GameplayIntentType.Attack);
            Assert.That(harness.Actor.ActionSim.FreezeFrames, Is.Zero);

            harness.Actor.NotifyActionBegun(GameplayIntentType.AssistParrySuccess);
            Assert.That(harness.Actor.ActionSim.FreezeFrames, Is.Zero);
        }
        finally
        {
            DestroyAction(next);
        }
    }

    /// <summary>冻结期间 CanCancelByMovement 为假，Driver 移动取消不得拆招。</summary>
    [Test]
    public void FrozenAction_CannotCancelByMovement()
    {
        using ActorHarness harness = ActorHarness.Create("FrozenCancel");
        ActionDefinition action = CreateReadyAction("Guard");
        try
        {
            Assert.That(
                harness.Actor.ActionSim.TryStart(ActionSimResolveResult.FromContent(action)),
                Is.True);
            Assert.That(harness.Actor.TryRequestHitStopOnCurrentAction(4, oncePerAction: true), Is.True);
            Assert.That(harness.Actor.ActionSim.IsFrozen, Is.True);
            Assert.That(harness.Actor.ActionSim.CanCancelByMovement, Is.False);

            Assert.That(harness.StateMachine.TryChangeState(CharacterStateType.Action), Is.True);
            harness.Input.IngestFrame(new InputFrame(
                1,
                harness.Actor.SimulationId,
                moveX: 80,
                moveY: 0,
                buttonsPressed: 0,
                buttonsHeld: 0,
                buttonsReleased: 0));
            harness.Driver.ProcessGameplayInput();
            Assert.That(harness.StateMachine.CurrentStateId, Is.EqualTo(CharacterStateType.Action));
        }
        finally
        {
            DestroyAction(action);
        }
    }

    static ActionDefinition CreateReadyAction(string name)
    {
        ActionDefinition action = ScriptableObject.CreateInstance<ActionDefinition>();
        action.name = name;
        AnimationClip clip = new AnimationClip { name = name + "Clip", legacy = true };
        var so = new SerializedObject(action);
        so.FindProperty("sampleRate").intValue = ActionSim.LogicHz;
        so.FindProperty("totalFrames").intValue = 12;
        SerializedProperty segments = so.FindProperty("animationSegments");
        segments.arraySize = 1;
        segments.GetArrayElementAtIndex(0).FindPropertyRelative("clip").objectReferenceValue = clip;
        so.ApplyModifiedPropertiesWithoutUndo();
        return action;
    }

    static void DestroyAction(ActionDefinition action)
    {
        if (action == null)
            return;

        AnimationClip clip = action.HasAnimation ? action.AnimationSegments[0].clip : null;
        UnityEngine.Object.DestroyImmediate(action);
        if (clip != null)
            UnityEngine.Object.DestroyImmediate(clip);
    }

    /// <summary>最小 Actor：能起手 / 写 carry，不经 Factory。</summary>
    sealed class ActorHarness : IDisposable
    {
        readonly GameObject _owner;
        readonly CharacterLocomotionProfile _locomotionProfile;
        readonly CharacterAnimationProfile _animationProfile;

        ActorHarness(
            CharacterActor actor,
            CharacterActionDriver driver,
            CharacterStateMachine stateMachine,
            InputManager input,
            GameObject owner,
            CharacterLocomotionProfile locomotionProfile,
            CharacterAnimationProfile animationProfile)
        {
            Actor = actor;
            Driver = driver;
            StateMachine = stateMachine;
            Input = input;
            _owner = owner;
            _locomotionProfile = locomotionProfile;
            _animationProfile = animationProfile;
        }

        public CharacterActor Actor { get; }

        public CharacterActionDriver Driver { get; }

        public CharacterStateMachine StateMachine { get; }

        public InputManager Input { get; }

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
            return new ActorHarness(
                actor,
                actionDriver,
                stateMachine,
                input,
                owner,
                locomotionProfile,
                animationProfile);
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
