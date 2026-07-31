using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>角色实例工厂，负责从 CharacterConfig 和输入源创建纯 C# 服务图。</summary>
public static class CharacterActorFactory
{
    /// <summary>按配置创建角色实例；跨系统注册和命中结算由调用方通过委托接回 App 层。</summary>
    public static CharacterActor Create(
        GameObject owner,
        Transform root,
        CharacterConfig config,
        int teamId,
        ICharacterInputSource inputSource,
        Transform cameraTransform,
        Func<IReadOnlyList<IHurtboxTarget>> activeTargetsProvider,
        Action<ActionHitContext, IHurtboxTarget, IActionHitReceiver, Transform> hitDetected,
        out ActionExecutor actionExecutor,
        out CharacterAnimationService animation)
    {
        CharacterMotorConfig motorConfig = config.Motor;
        Transform presentationRoot = CreatePresentationRoot(root);
        Transform modelRoot = SpawnModelInstance(config, presentationRoot);
        Animator animator = modelRoot.GetComponentInChildren<Animator>();
        if (animator == null)
            throw new MissingComponentException("CharacterActorFactory: ModelPrefab 中找不到 Animator。");

        CharacterController controller = GetOrAddCharacterController(owner);
        motorConfig.ApplyTo(controller);

        var sharedInput = new InputManager();
        inputSource.ConfigureDiscreteInputs(config.GameplayIntentProfile.CollectInputReferences());
        var motor = new CharacterMotor(root, controller, motorConfig, sharedInput, cameraTransform);
        IAnimationPlayback playback = new PlayableAnimationPlayback(animator);
        animation = new CharacterAnimationService(
            playback,
            animator,
            config.DefaultLocomotionProfile);
        var rootMotion = new CharacterRootMotionDriver(controller, animator);
        var combatMode = new CombatModeService(config.CombatProfile, animation);

        var context = new CharacterContext(root, animation, controller, motor);
        CharacterLocomotionProfile locomotionProfile = config.LocomotionProfile;
        if (locomotionProfile == null)
            locomotionProfile = ScriptableObject.CreateInstance<CharacterLocomotionProfile>();

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
        var resolverService = new ActionResolverService(combatMode);
        actionExecutor = new ActionExecutor(root, controller, animation, rootMotion, combatMode, resolverService);
        context.ActionExecutor = actionExecutor;

        // 缓冲时长由输入 Profile 统一配置，避免工厂与动作执行器各自维护不同窗口。
        var intentBuffer = new GameplayIntentBuffer(
            config.GameplayIntentProfile.ActionBufferDurationSeconds);
        var intentProducer = new GameplayIntentProducer(
            config.GameplayIntentProfile,
            sharedInput,
            intentBuffer,
            stateMachine,
            locomotionStateMachine,
            actionExecutor);

        Transform defaultAttach = ResolveModelPoint(config.Combat.AttachPointName, modelRoot, root);
        Transform aimOrigin = ResolveModelPoint(config.Combat.AimOriginName, modelRoot, root);
        var attachPoints = new CharacterAttachPointResolver(modelRoot, defaultAttach);
        var targetLock = new CombatTargetLock(root, teamId, aimOrigin, activeTargetsProvider);
        var hitboxFrameConsumer = new HitboxFrameConsumer(
            root,
            teamId,
            actionExecutor,
            attachPoints,
            activeTargetsProvider,
            hitDetected);
        var vfxPlayer = new ActionVfxPlayer(root, attachPoints);
        var sfxPlayer = new ActionSfxPlayer(root);

        actionExecutor.RegisterFrameConsumer(hitboxFrameConsumer);
        actionExecutor.RegisterNotifyConsumer(vfxPlayer);
        actionExecutor.RegisterNotifyConsumer(sfxPlayer);
        actionExecutor.BindTimelineAttachPoint(defaultAttach);

        var actionDriver = new CharacterActionDriver(
            sharedInput,
            intentBuffer,
            stateMachine,
            actionExecutor,
            combatMode,
            targetLock,
            resolverService,
            root,
            motor);
        actionExecutor.BindInputBuffer(intentBuffer);

        var actor = new CharacterActor(
            inputSource,
            sharedInput,
            intentProducer,
            motor,
            stateMachine,
            actionDriver,
            combatMode,
            animation,
            new CharacterPresentationBridge(root, presentationRoot));

        var rotationDriver = new ActionRotationDriver(
            root,
            sharedInput,
            motor,
            actionExecutor,
            targetLock);

        context.ActionRotation = rotationDriver;
        actionExecutor.BindActionStartContext(motor);
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
