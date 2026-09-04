using System;
using NUnit.Framework;
using UnityEngine;

/// <summary>招架窗管道：玩家吞伤、攻击者 IssueParried、仅无敌不武装。</summary>
public sealed class AssistParryPipelineTests
{
    /// <summary>窗内命中：玩家不 OnHit / 不 EnterHit，攻击者 EnterHit，武装突击。</summary>
    [Test]
    public void AssistParryWindow_IssuesParriedAndArmsFollowUp()
    {
        using ActorHarness attacker = ActorHarness.Create("Attacker", new SimActorId(1));
        using ActorHarness player = ActorHarness.Create("Player", new SimActorId(2));
        var attackerReactions = new CharacterReactionService(
            attacker.Actor.Vitality,
            attacker.Actor,
            new CharacterReactionResolver(new CharacterReactionSet()),
            baseInterruptResist: 3);

        ResolvedCombatHit? resolved = null;
        var pipeline = new CombatHitPipeline(hit => resolved = hit);
        pipeline.BindAssistParryLookups(
            id => id.Equals(attacker.Actor.SimulationId) ? attackerReactions : null,
            id => id.Equals(player.Actor.SimulationId) ? player.Actor : null);

        var target = new AbsorbTarget(player.Actor.SimulationId, assistParry: true, invincible: true);
        var receiver = new HitReceiver();
        var context = new ActionHitContext(null, null, null, 1, attacker.Actor.SimulationId);

        pipeline.BeginFrame(1);
        pipeline.Collect(
            attacker.Actor.SimulationId,
            1,
            0,
            target,
            receiver,
            in context,
            Vector3.zero);
        pipeline.ResolveBeforePostCombat(1);
        pipeline.CompleteFrame(1);

        Assert.That(target.OnHitCount, Is.Zero);
        Assert.That(receiver.Confirmed, Is.True);
        Assert.That(resolved.HasValue, Is.True);
        Assert.That(resolved.Value.AbsorbedByAssistParry, Is.True);
        Assert.That(attacker.Actor.CurrentState, Is.EqualTo(CharacterStateType.Hit));
        Assert.That(attacker.Actor.Vitality.LastConfirmedReactionKind, Is.EqualTo(HitReactionKind.LightStun));
        Assert.That(attacker.Actor.Vitality.ReplicationEdge, Is.EqualTo(VitalityReplicationEdge.Hit));
        Assert.That(attacker.Actor.Vitality.CurrentHealth, Is.EqualTo(attacker.Actor.Vitality.MaxHealth));
        Assert.That(player.Actor.Numeric.Flags.HasAssistFollowUp, Is.True);
        Assert.That(player.Actor.CurrentState, Is.Not.EqualTo(CharacterStateType.Hit));

        attackerReactions.Dispose();
    }

    /// <summary>仅无敌、无招架窗：吞伤但不 IssueParried、不武装。</summary>
    [Test]
    public void InvincibleOnly_DoesNotIssueParried()
    {
        using ActorHarness attacker = ActorHarness.Create("AttackerInvuln", new SimActorId(3));
        var attackerReactions = new CharacterReactionService(
            attacker.Actor.Vitality,
            attacker.Actor,
            new CharacterReactionResolver(new CharacterReactionSet()),
            baseInterruptResist: 3);

        ResolvedCombatHit? resolved = null;
        var pipeline = new CombatHitPipeline(hit => resolved = hit);
        pipeline.BindAssistParryLookups(_ => attackerReactions, _ => null);

        var target = new AbsorbTarget(new SimActorId(20), assistParry: false, invincible: true);
        var receiver = new HitReceiver();
        var context = new ActionHitContext(null, null, null, 1, attacker.Actor.SimulationId);

        pipeline.BeginFrame(1);
        pipeline.Collect(
            attacker.Actor.SimulationId,
            1,
            0,
            target,
            receiver,
            in context,
            Vector3.zero);
        pipeline.ResolveBeforePostCombat(1);
        pipeline.CompleteFrame(1);

        Assert.That(target.OnHitCount, Is.Zero);
        Assert.That(receiver.Confirmed, Is.True);
        Assert.That(resolved.HasValue, Is.False);
        Assert.That(attacker.Actor.CurrentState, Is.Not.EqualTo(CharacterStateType.Hit));
        Assert.That(attacker.Actor.Vitality.LastConfirmedReactionKind, Is.EqualTo(HitReactionKind.None));

        attackerReactions.Dispose();
    }

    /// <summary>ResolveParried 无视冲击与 SuperArmor，固定 LightStun。</summary>
    [Test]
    public void ResolveParried_IgnoresToughnessAndSuperArmor()
    {
        var resolver = new CharacterReactionResolver(new CharacterReactionSet());
        HitReactionCommand command = resolver.ResolveParried();
        Assert.That(command.Kind, Is.EqualTo(HitReactionKind.LightStun));
        Assert.That(command.InterruptsAction, Is.True);
        Assert.That(
            CharacterReactionResolver.ResolveKind(1, 99, superArmor: true),
            Is.EqualTo(HitReactionKind.Flinch));
    }

    /// <summary>最小权威 Actor：能 EnterHit / 武装突击，不经 Factory。</summary>
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

        public static ActorHarness Create(string name, SimActorId id)
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
            actor.BindSimulationInput(id, new InputFrameBuffer());
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

    sealed class AbsorbTarget : ITargetable, IHitAbsorbQuery
    {
        public AbsorbTarget(SimActorId id, bool assistParry, bool invincible)
        {
            SimulationId = id;
            IsInAssistParryWindow = assistParry;
            IsInvincible = invincible;
        }

        public int OnHitCount { get; private set; }
        public SimActorId SimulationId { get; }
        public Transform TargetTransform => null;
        public Transform AimTransform => null;
        public bool IsAlive => true;
        public float CurrentHealth => 1f;
        public int TeamId => 1;
        public bool IsInvincible { get; }
        public bool IsInPerfectDodgeWindow => false;
        public bool IsInAssistParryWindow { get; }

        public HitboxOrientedBox GetLogicalHurtbox() => default;
        public SimCombatPose GetLogicalCombatPose() => default;
        public void OnHit(in ActionHitContext context) => OnHitCount++;
    }

    sealed class HitReceiver : IActionSimHitReceiver
    {
        public bool Confirmed { get; private set; }

        public bool ConfirmHit(int actionInstanceId)
        {
            Confirmed = true;
            return true;
        }

        public bool RequestHitStop(int actionInstanceId, int frames, bool oncePerAction) => false;
    }
}
