using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>角色实例工厂，负责从 CharacterConfig 和输入源创建纯 C# 服务图。</summary>
public static class CharacterActorFactory
{
    /// <summary>按配置创建角色实例；Hitbox 仅写共享帧末命中流水线。</summary>
    public static CharacterActor Create(
        GameObject owner,
        Transform root,
        CharacterConfig config,
        int teamId,
        ILocalInputSampler localInput,
        Transform cameraTransform,
        Func<IReadOnlyList<IHurtboxTarget>> activeTargetsProvider,
        CombatHitPipeline combatHitPipeline,
        out ActionSim actionSim,
        out CharacterAnimationService animation,
        ISimCollisionWorld collisionWorld = null)
    {
        CharacterActor actor = null;
        CharacterMotorConfig motorConfig = config.Motor;
        Transform presentationRoot = CreatePresentationRoot(root);
        // Wave 2：模型挂在 VisualMotionRoot，横摆残差不进 SimulationRoot / CameraRoot
        Transform visualMotionRoot = CreateVisualMotionRoot(presentationRoot);
        Transform modelRoot = SpawnModelInstance(config, visualMotionRoot);
        Animator animator = modelRoot.GetComponentInChildren<Animator>();
        if (animator == null)
            throw new MissingComponentException("CharacterActorFactory: ModelPrefab 中找不到 Animator。");

        CharacterController controller = GetOrAddCharacterController(owner);
        motorConfig.ApplyTo(controller);
        // CC 仅保留半径/高度配置；保持禁用，避免 Sync 时 enable 被地面挤出造成悬空
        controller.enabled = false;

        var sharedInput = new InputManager();
        GameplayIntentProfile intentProfile = GameplayIntentSettings.Active;
        if (intentProfile == null)
            throw new InvalidOperationException(
                "CharacterActorFactory: 全局 GameplayIntentProfile 未就绪。");

        localInput?.ConfigureDiscreteInputs(intentProfile.CollectInputReferences());
        ISimCollisionWorld world = collisionWorld ?? OpenFieldSimCollisionWorld.Instance;
        var motorSim = new CharacterMotorSim(
            world,
            MotionQuantization.MetersToMm(motorConfig.ControllerRadius),
            motorConfig.SoftBodyMass,
            motorConfig.SoftBodyImmovable,
            SimulationConfig.DefaultLogicHz,
            MotionQuantization.MetersToMm(motorConfig.Gravity),
            MotionQuantization.MetersToMm(motorConfig.GroundedGravity));
        var motor = new CharacterMotor(
            root,
            controller,
            motorConfig,
            sharedInput,
            cameraTransform,
            motorSim);
        IAnimationPlayback playback = new PlayableAnimationPlayback(animator);
        if (config.CombatProfile == null
            || !config.CombatProfile.TryGetLocomotionProfile(
                config.CombatProfile.DefaultMode,
                out CharacterLocomotionProfile locomotionProfile)
            || locomotionProfile.AnimationProfile == null)
        {
            throw new InvalidOperationException(
                "CharacterActorFactory: CombatModeProfile 默认模式缺少 LocomotionProfile（须含 AnimationProfile）。");
        }

        animation = new CharacterAnimationService(
            playback,
            animator,
            locomotionProfile.AnimationProfile);
        var rootMotion = new CharacterRootMotionDriver(motor, animator);
        var combatMode = new CombatModeService(config.CombatProfile, animation);

        var context = new CharacterContext(root, animation, controller, motor);
        var footstepPlayer = new LocomotionFootstepPlayer(root, locomotionProfile);
        var locomotionStateMachine = new LocomotionStateMachine(
            root,
            motor,
            animation,
            sharedInput,
            locomotionProfile,
            footstepPlayer);
        context.LocomotionStateMachine = locomotionStateMachine;

        var stateMachine = new CharacterStateMachine(context);
        // 缓冲时长由全局 Intent Profile 统一配置
        var intentBuffer = new GameplayIntentBuffer(intentProfile.ActionBufferDurationFrames);
        var resolverService = new ActionResolverService(combatMode);
        var numeric = new NumericSystem(
            CharacterNumericConfig.FromResourceConfig(config.Resources, config.Combat.MaxHealth));
        var vitality = new CharacterVitality(numeric);
        // DOT 扣血走 Vitality：无 Hit Reaction，死亡边沿仍生效
        numeric.Effects.SetHealthDamageHandler(vitality.ApplyPeriodicHealthDamageMilli);
        var resourceGate = new NumericCostGate(numeric);
        // Bridge 需 Gate 做同键 EX 选形；ActionSim 再用同一 Gate 扣费
        var resolverBridge = new ActionSimResolverBridge(resolverService, root, motor, resourceGate);
        // Begin 成功清完美反击缓冲（含 Cancel/硬打断起手）
        actionSim = new ActionSim(
            resolverBridge,
            intentBuffer,
            resourceGate,
            onBegun: intent =>
            {
                if (intent == GameplayIntentType.PerfectDodgeAttack)
                    numeric.ClearPerfectDodgeCounter();
            });
        context.ActionSim = actionSim;

        var intentProducer = new GameplayIntentProducer(
            intentProfile,
            sharedInput,
            intentBuffer,
            stateMachine,
            locomotionStateMachine,
            actionSim,
            hasPerfectDodgeCounter: () => numeric.Flags.HasPerfectDodgeCounter);

        Transform defaultAttach = ResolveModelPoint(config.Combat.AttachPointName, modelRoot, root);
        Transform aimOrigin = ResolveModelPoint(config.Combat.AimOriginName, modelRoot, root);
        var attachPoints = new CharacterAttachPointResolver(modelRoot, defaultAttach);
        var targetLock = new CombatTargetLock(root, teamId, aimOrigin, activeTargetsProvider);
        var hitboxFrameConsumer = new HitboxFrameConsumer(
            root,
            motorSim,
            teamId,
            actionSim,
            attachPoints,
            activeTargetsProvider,
            () => actor?.SimulationId ?? SimActorId.Invalid,
            combatHitPipeline);
        var vfxPlayer = new ActionVfxPlayer(root, attachPoints);
        var sfxPlayer = new ActionSfxPlayer(root);
        var visualMotion = new CharacterVisualMotionBridge(visualMotionRoot);
        var actionPresentation = new CharacterActionPresentationBridge(
            actionSim,
            root,
            motor,
            animation,
            rootMotion,
            combatMode,
            motor,
            new ActionTimelineRunner(),
            defaultAttach,
            visualMotion);
        actionPresentation.RegisterFrameConsumer(hitboxFrameConsumer);
        actionPresentation.RegisterNotifyConsumer(vfxPlayer);
        actionPresentation.RegisterNotifyConsumer(sfxPlayer);

        var actionDriver = new CharacterActionDriver(
            sharedInput,
            intentBuffer,
            stateMachine,
            actionSim,
            combatMode,
            targetLock,
            resolverService,
            root,
            motor);

        actor = new CharacterActor(
            localInput,
            sharedInput,
            intentProducer,
            motor,
            stateMachine,
            actionDriver,
            actionSim,
            actionPresentation,
            combatMode,
            animation,
            new CharacterPresentationBridge(root, presentationRoot),
            visualMotion,
            numeric,
            vitality,
            intentBuffer,
            targetLock,
            root);

        var rotationDriver = new ActionRotationDriver(
            root,
            sharedInput,
            motor,
            actionSim,
            targetLock);

        context.ActionRotation = rotationDriver;
        return actor;
    }

    static Transform SpawnModelInstance(CharacterConfig config, Transform parent)
    {
        GameObject modelInstance = UnityEngine.Object.Instantiate(config.ModelPrefab, parent);
        modelInstance.name = config.ModelPrefab.name;
        Transform modelTransform = modelInstance.transform;
        modelTransform.localPosition = config.ModelLocalPosition;
        modelTransform.localRotation = config.ModelLocalRotation;
        return modelTransform;
    }

    /// <summary>创建与权威根分离的运行时表现锚点，模型只在该锚点下接受渲染插值。</summary>
    static Transform CreatePresentationRoot(Transform simulationRoot)
    {
        var presentationObject = new GameObject("CharacterPresentationRoot");
        Transform presentationRoot = presentationObject.transform;
        presentationRoot.SetParent(simulationRoot, false);
        return presentationRoot;
    }

    /// <summary>Wave 2：动作视觉残差根；与 CameraRoot 并列，禁止把相机挂到其下。</summary>
    static Transform CreateVisualMotionRoot(Transform presentationRoot)
    {
        var visualObject = new GameObject("CharacterVisualMotionRoot");
        Transform visualRoot = visualObject.transform;
        visualRoot.SetParent(presentationRoot, false);
        visualRoot.localPosition = Vector3.zero;
        visualRoot.localRotation = Quaternion.identity;
        return visualRoot;
    }

    static CharacterController GetOrAddCharacterController(GameObject owner)
    {
        CharacterController controller = owner.GetComponent<CharacterController>();
        return controller != null ? controller : owner.AddComponent<CharacterController>();
    }

    static Transform ResolveModelPoint(string pointName, Transform modelRoot, Transform fallback)
    {
        if (string.IsNullOrWhiteSpace(pointName))
            return fallback;

        Transform point = CharacterAttachPointResolver.FindByName(modelRoot, pointName);
        if (point == null)
        {
            Debug.LogWarning($"CharacterActorFactory: 模型中找不到挂点 {pointName}，已回退到角色根节点。");
            return fallback;
        }

        return point;
    }
}
