using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
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
        Assert.That(resolved.Value.HitStopFrames, Is.EqualTo(AssistParryHitStop.DefaultFrames));
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

    /// <summary>双方已起手时，IssueParried 之后冻新受击招与玩家 Guard。</summary>
    [Test]
    public void AssistParryWindow_FreezesBothCurrentActions()
    {
        ActionDefinition hitAction = CreateReadyAction("ParriedHit");
        ActionDefinition guard = CreateReadyAction("Guard", parryHitStopFrames: 5);
        using ActorHarness attacker = ActorHarness.Create("AttackerFreeze", new SimActorId(11));
        using ActorHarness player = ActorHarness.Create("PlayerFreeze", new SimActorId(12));
        CharacterReactionSet reactionSet = CreateHitReactionSet(hitAction);
        var attackerReactions = new CharacterReactionService(
            attacker.Actor.Vitality,
            attacker.Actor,
            new CharacterReactionResolver(reactionSet),
            baseInterruptResist: 3);

        ResolvedCombatHit? resolved = null;
        var pipeline = new CombatHitPipeline(hit => resolved = hit);
        pipeline.BindAssistParryLookups(
            id => id.Equals(attacker.Actor.SimulationId) ? attackerReactions : null,
            id => LookupActor(id, attacker, player));

        Assert.That(player.Actor.ActionSim.TryStart(ActionSimResolveResult.FromContent(guard)), Is.True);
        Assert.That(attacker.Actor.ActionSim.TryStart(ActionSimResolveResult.FromContent(hitAction)), Is.True);

        var target = new AbsorbTarget(player.Actor.SimulationId, assistParry: true, invincible: true);
        var context = new ActionHitContext(null, null, null, 1, attacker.Actor.SimulationId);

        try
        {
            pipeline.BeginFrame(1);
            pipeline.Collect(
                attacker.Actor.SimulationId,
                attacker.Actor.ActionSim.InstanceId,
                0,
                target,
                attacker.Actor.ActionSim,
                in context,
                Vector3.zero);
            pipeline.ResolveBeforePostCombat(1);
            pipeline.CompleteFrame(1);

            Assert.That(target.OnHitCount, Is.Zero);
            Assert.That(resolved.HasValue, Is.True);
            Assert.That(resolved.Value.HitStopFrames, Is.EqualTo(5));
            Assert.That(attacker.Actor.CurrentState, Is.EqualTo(CharacterStateType.Hit));
            Assert.That(attacker.Actor.ActionSim.IsActive, Is.True);
            Assert.That(attacker.Actor.ActionSim.FreezeFrames, Is.EqualTo(5));
            Assert.That(player.Actor.ActionSim.IsActive, Is.True);
            Assert.That(player.Actor.ActionSim.FreezeFrames, Is.EqualTo(5));
            Assert.That(player.Actor.CurrentState, Is.Not.EqualTo(CharacterStateType.Hit));
        }
        finally
        {
            attackerReactions.Dispose();
            DestroyAction(hitAction);
            DestroyAction(guard);
        }
    }

    /// <summary>无受击片时攻击者 ActionSim 已停，该侧跳过写入且不抛。</summary>
    [Test]
    public void AssistParryWindow_NoStunAction_SkipsAttackerFreeze()
    {
        ActionDefinition guard = CreateReadyAction("GuardNoStun");
        ActionDefinition attack = CreateReadyAction("AttackNoStun");
        using ActorHarness attacker = ActorHarness.Create("AttackerNoStun", new SimActorId(13));
        using ActorHarness player = ActorHarness.Create("PlayerNoStun", new SimActorId(14));
        var attackerReactions = new CharacterReactionService(
            attacker.Actor.Vitality,
            attacker.Actor,
            new CharacterReactionResolver(new CharacterReactionSet()),
            baseInterruptResist: 3);

        ResolvedCombatHit? resolved = null;
        var pipeline = new CombatHitPipeline(hit => resolved = hit);
        pipeline.BindAssistParryLookups(
            id => id.Equals(attacker.Actor.SimulationId) ? attackerReactions : null,
            id => LookupActor(id, attacker, player));

        Assert.That(player.Actor.ActionSim.TryStart(ActionSimResolveResult.FromContent(guard)), Is.True);
        Assert.That(attacker.Actor.ActionSim.TryStart(ActionSimResolveResult.FromContent(attack)), Is.True);

        var target = new AbsorbTarget(player.Actor.SimulationId, assistParry: true, invincible: true);
        var context = new ActionHitContext(null, null, null, 1, attacker.Actor.SimulationId);

        try
        {
            pipeline.BeginFrame(1);
            pipeline.Collect(
                attacker.Actor.SimulationId,
                attacker.Actor.ActionSim.InstanceId,
                0,
                target,
                attacker.Actor.ActionSim,
                in context,
                Vector3.zero);
            pipeline.ResolveBeforePostCombat(1);
            pipeline.CompleteFrame(1);

            Assert.That(resolved.HasValue, Is.True);
            Assert.That(resolved.Value.HitStopFrames, Is.EqualTo(AssistParryHitStop.DefaultFrames));
            Assert.That(attacker.Actor.CurrentState, Is.EqualTo(CharacterStateType.Hit));
            Assert.That(attacker.Actor.ActionSim.IsActive, Is.False);
            Assert.That(player.Actor.ActionSim.FreezeFrames, Is.EqualTo(AssistParryHitStop.DefaultFrames));
        }
        finally
        {
            attackerReactions.Dispose();
            DestroyAction(guard);
            DestroyAction(attack);
        }
    }

    /// <summary>真伤未勾 UseHitStop 不冻；仍 OnHit。</summary>
    [Test]
    public void TrueHit_WithoutUseHitStop_DoesNotFreeze()
    {
        ActionDefinition attack = CreateReadyAction("TrueHit");
        using ActorHarness attacker = ActorHarness.Create("AttackerTrue", new SimActorId(15));
        ResolvedCombatHit? resolved = null;
        var pipeline = new CombatHitPipeline(hit => resolved = hit);

        Assert.That(attacker.Actor.ActionSim.TryStart(ActionSimResolveResult.FromContent(attack)), Is.True);
        var target = new AbsorbTarget(new SimActorId(21), assistParry: false, invincible: false);
        var context = new ActionHitContext(null, null, null, attacker.Actor.ActionSim.InstanceId, attacker.Actor.SimulationId);

        try
        {
            pipeline.BeginFrame(1);
            pipeline.Collect(
                attacker.Actor.SimulationId,
                attacker.Actor.ActionSim.InstanceId,
                0,
                target,
                attacker.Actor.ActionSim,
                in context,
                Vector3.zero);
            pipeline.ResolveBeforePostCombat(1);
            pipeline.CompleteFrame(1);

            Assert.That(target.OnHitCount, Is.EqualTo(1));
            Assert.That(resolved.HasValue, Is.True);
            Assert.That(resolved.Value.AbsorbedByAssistParry, Is.False);
            Assert.That(resolved.Value.HitStopFrames, Is.Zero);
            Assert.That(attacker.Actor.ActionSim.FreezeFrames, Is.Zero);
        }
        finally
        {
            DestroyAction(attack);
        }
    }

    /// <summary>ResolveParried 无视冲击与 SuperArmor，固定 LightStun。</summary>
    [Test]
    public void ResolveParried_IgnoresToughnessAndSuperArmor()
    {
        var resolver = new CharacterReactionResolver(new CharacterReactionSet());
        HitReactionCommand command = resolver.ResolveParried(string.Empty);
        Assert.That(command.Kind, Is.EqualTo(HitReactionKind.LightStun));
        Assert.That(command.InterruptsAction, Is.True);
        Assert.That(
            CharacterReactionResolver.ResolveKind(1, 99, superArmor: true),
            Is.EqualTo(HitReactionKind.Flinch));
    }

    /// <summary>精确 Parried Id 命中对应片子，不落到默认片。</summary>
    [Test]
    public void ResolveParried_ReactionId_SelectsExactParriedAction()
    {
        ActionDefinition left = CreateReadyAction("Hit_Parry_Left");
        ActionDefinition right = CreateReadyAction("Hit_Parry_Right");
        ActionDefinition fallback = CreateReadyAction("Hit_Parry_Default");
        try
        {
            CharacterReactionSet set = CreateParriedReactionSet(fallback, left, right);
            var resolver = new CharacterReactionResolver(set);

            Assert.That(resolver.ResolveParried("Left").StunAction, Is.SameAs(left));
            Assert.That(resolver.ResolveParried("Right").StunAction, Is.SameAs(right));
            Assert.That(resolver.ResolveParried(string.Empty).StunAction, Is.SameAs(fallback));
            Assert.That(resolver.ResolveParried("Missing").StunAction, Is.SameAs(fallback));
            Assert.That(resolver.ResolveParried("Left").Kind, Is.EqualTo(HitReactionKind.LightStun));
        }
        finally
        {
            DestroyAction(left);
            DestroyAction(right);
            DestroyAction(fallback);
        }
    }

    /// <summary>Continue：不停招、不写边沿、不 NotifyHit；Resolved 档为 None。</summary>
    [Test]
    public void AssistParry_Continue_DoesNotEnterHitOrNotify()
    {
        ActionDefinition attack = CreateReadyAction("ContinueAttack");
        using ActorHarness attacker = ActorHarness.Create("AttackerContinue", new SimActorId(21));
        int notifyCount = 0;
        var attackerReactions = new CharacterReactionService(
            attacker.Actor.Vitality,
            attacker.Actor,
            new CharacterReactionResolver(new CharacterReactionSet()),
            hitSideEffect: _ => notifyCount++,
            baseInterruptResist: 3);

        ResolvedCombatHit? resolved = null;
        var pipeline = new CombatHitPipeline(hit => resolved = hit);
        pipeline.BindAssistParryLookups(
            id => id.Equals(attacker.Actor.SimulationId) ? attackerReactions : null,
            _ => null);

        Assert.That(attacker.Actor.ActionSim.TryStart(ActionSimResolveResult.FromContent(attack)), Is.True);
        int instanceId = attacker.Actor.ActionSim.InstanceId;
        HitboxNotifyState hitbox = CreateHitbox(ParriedActionPolicy.Continue);
        var target = new AbsorbTarget(new SimActorId(22), assistParry: true, invincible: true);
        var context = new ActionHitContext(attack, hitbox, null, instanceId, attacker.Actor.SimulationId);

        try
        {
            pipeline.BeginFrame(1);
            pipeline.Collect(
                attacker.Actor.SimulationId,
                instanceId,
                0,
                target,
                attacker.Actor.ActionSim,
                in context,
                Vector3.zero);
            pipeline.ResolveBeforePostCombat(1);
            pipeline.CompleteFrame(1);

            Assert.That(resolved.HasValue, Is.True);
            Assert.That(resolved.Value.AbsorbedByAssistParry, Is.True);
            Assert.That(resolved.Value.ReactionKind, Is.EqualTo(HitReactionKind.None));
            Assert.That(attacker.Actor.CurrentState, Is.Not.EqualTo(CharacterStateType.Hit));
            Assert.That(attacker.Actor.ActionSim.IsActive, Is.True);
            Assert.That(attacker.Actor.ActionSim.InstanceId, Is.EqualTo(instanceId));
            Assert.That(attacker.Actor.Vitality.LastConfirmedReactionKind, Is.EqualTo(HitReactionKind.None));
            Assert.That(attacker.Actor.Vitality.ReplicationEdge, Is.EqualTo(VitalityReplicationEdge.None));
            Assert.That(notifyCount, Is.Zero);
            Assert.That(target.OnHitCount, Is.Zero);
        }
        finally
        {
            attackerReactions.Dispose();
            DestroyAction(attack);
        }
    }

    /// <summary>已在 Success 图节点上时，二次接触不排队、不换实例。</summary>
    [Test]
    public void AssistParry_AlreadyPlayingSuccess_DoesNotRequeue()
    {
        ActionDefinition success = CreateReadyAction("AutoParrySuccess", parryHitStopFrames: 5);
        ActionGraph graph = CreateSuccessGraph(success);
        using ActorHarness player = ActorHarness.Create("PlayerSuccess", new SimActorId(23));
        using ActorHarness attacker = ActorHarness.Create("AttackerSuccess", new SimActorId(24));
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

        Assert.That(
            player.Actor.ActionSim.TryStart(ActionSimResolveResult.FromGraph(
                success,
                graph,
                "Success",
                GameplayIntentType.AssistParrySuccess)),
            Is.True);
        int successInstance = player.Actor.ActionSim.InstanceId;

        var target = new AbsorbTarget(player.Actor.SimulationId, assistParry: true, invincible: true);
        var context = new ActionHitContext(null, null, null, 1, attacker.Actor.SimulationId);

        try
        {
            pipeline.BeginFrame(1);
            pipeline.Collect(
                attacker.Actor.SimulationId,
                1,
                0,
                target,
                new HitReceiver(),
                in context,
                Vector3.zero);
            pipeline.ResolveBeforePostCombat(1);
            pipeline.CompleteFrame(1);

            Assert.That(resolved.HasValue, Is.True);
            Assert.That(resolved.Value.AbsorbedByAssistParry, Is.True);
            Assert.That(player.Actor.ActionSim.InstanceId, Is.EqualTo(successInstance));
            Assert.That(ReadQueuedIntent(player.Actor), Is.EqualTo(GameplayIntentType.None));
            Assert.That(player.Actor.Numeric.Flags.HasAssistFollowUp, Is.True);
        }
        finally
        {
            attackerReactions.Dispose();
            DestroyAction(success);
            UnityEngine.Object.DestroyImmediate(graph);
        }
    }

    static CharacterActor LookupActor(SimActorId id, ActorHarness attacker, ActorHarness player)
    {
        if (id.Equals(attacker.Actor.SimulationId))
            return attacker.Actor;
        if (id.Equals(player.Actor.SimulationId))
            return player.Actor;
        return null;
    }

    static CharacterReactionSet CreateParriedReactionSet(
        ActionDefinition defaultParried,
        ActionDefinition left,
        ActionDefinition right)
    {
        var defaultRule = new CharacterReactionRule();
        SetField(defaultRule, "reactionType", CharacterReactionType.Parried);
        SetField(defaultRule, "defaultRule", true);
        SetField(defaultRule, "action", defaultParried);

        var leftRule = new CharacterReactionRule();
        SetField(leftRule, "reactionType", CharacterReactionType.Parried);
        SetField(leftRule, "reactionId", "Left");
        SetField(leftRule, "action", left);

        var rightRule = new CharacterReactionRule();
        SetField(rightRule, "reactionType", CharacterReactionType.Parried);
        SetField(rightRule, "reactionId", "Right");
        SetField(rightRule, "action", right);

        var set = new CharacterReactionSet();
        SetField(set, "rules", new[] { defaultRule, leftRule, rightRule });
        return set;
    }

    static HitboxNotifyState CreateHitbox(ParriedActionPolicy policy, string parriedReactionId = "")
    {
        var hitbox = new HitboxNotifyState();
        SetField(hitbox.Payload, "parriedActionPolicy", policy);
        SetField(hitbox.Payload, "parriedReactionId", parriedReactionId ?? string.Empty);
        return hitbox;
    }

    static ActionGraph CreateSuccessGraph(ActionDefinition success)
    {
        ActionGraph graph = ScriptableObject.CreateInstance<ActionGraph>();
        var node = new ActionGraphNode();
        SetField(node, "nodeId", "Success");
        SetField(node, "action", success);
        SetField(node, "intent", GameplayIntentType.AssistParrySuccess);
        SetField(graph, "nodes", new[] { node });
        return graph;
    }

    static GameplayIntentType ReadQueuedIntent(CharacterActor actor)
    {
        FieldInfo field = typeof(CharacterActor).GetField(
            "_queuedExternalIntent",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        return (GameplayIntentType)field.GetValue(actor);
    }

    static CharacterReactionSet CreateHitReactionSet(ActionDefinition action)
    {
        var rule = new CharacterReactionRule();
        SetField(rule, "reactionType", CharacterReactionType.Hit);
        SetField(rule, "defaultRule", true);
        SetField(rule, "action", action);
        var set = new CharacterReactionSet();
        SetField(set, "rules", new[] { rule });
        return set;
    }

    static ActionDefinition CreateReadyAction(string name, int parryHitStopFrames = -1)
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
        if (parryHitStopFrames >= 0)
        {
            SerializedProperty windows = so
                .FindProperty("timeline")
                .FindPropertyRelative("assistParryWindowStates");
            windows.arraySize = 1;
            SerializedProperty window = windows.GetArrayElementAtIndex(0);
            window.FindPropertyRelative("startFrame").intValue = 0;
            window.FindPropertyRelative("endFrame").intValue = 11;
            window.FindPropertyRelative("hitStopFrames").intValue = parryHitStopFrames;
        }

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

    static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, name);
        field.SetValue(target, value);
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
