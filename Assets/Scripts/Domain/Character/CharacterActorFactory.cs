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
        ICharacterInputSource inputSource,
        Transform cameraTransform,
        Func<IReadOnlyList<IHurtboxTarget>> activeTargetsProvider,
        Action<ActionHitContext, IHurtboxTarget, IActionHitReceiver, Transform> hitDetected,
        out ActionExecutor actionExecutor,
        out Animator animator)
    {
        CharacterMotorConfig motorConfig = config.Motor;
        Transform modelRoot = SpawnModelInstance(config, root);
        animator = modelRoot.GetComponentInChildren<Animator>();
        if (animator == null)
            throw new MissingComponentException("CharacterActorFactory: ModelPrefab 中找不到 Animator。");

        CharacterController controller = GetOrAddCharacterController(owner);
        motorConfig.ApplyTo(controller);

        var sharedInput = new InputManager();
        var motor = new CharacterMotor(root, controller, motorConfig, sharedInput, cameraTransform);
        var animation = new CharacterAnimationService(
            animator,
            config.DefaultLocomotionProfile,
            config.AnimatorLayerIndex);
        var rootMotion = new CharacterRootMotionDriver(controller, animator);
        var combatMode = new CombatModeService(config.CombatProfile, animation);

        var context = new CharacterContext(root, animation, controller, motor);
        var stateMachine = new CharacterStateMachine(context);
        var resolverService = new ActionResolverService(combatMode);
        actionExecutor = new ActionExecutor(root, controller, animation, rootMotion, combatMode, resolverService);
        context.ActionExecutor = actionExecutor;

        Transform attachPoint = ResolveModelPoint(config.Combat.AttachPointName, modelRoot, root);
        Transform aimOrigin = ResolveModelPoint(config.Combat.AimOriginName, modelRoot, root);
        var targetLock = new CombatTargetLock(root, config.Combat.TeamId, aimOrigin, activeTargetsProvider);
        var hitboxFrameConsumer = new HitboxFrameConsumer(root, actionExecutor, attachPoint, activeTargetsProvider, hitDetected);
        var vfxPlayer = new ActionVfxPlayer(root, attachPoint);

        actionExecutor.RegisterFrameConsumer(hitboxFrameConsumer);
        actionExecutor.RegisterNotifyConsumer(vfxPlayer);
        actionExecutor.BindTimelineAttachPoint(attachPoint);

        var actionDriver = new CharacterActionDriver(
            inputSource,
            sharedInput,
            stateMachine,
            actionExecutor,
            combatMode,
            targetLock,
            resolverService,
            root,
            motor);
        actionExecutor.BindInputBuffer(actionDriver.CreateInputBufferBridge());

        var actor = new CharacterActor(
            inputSource,
            sharedInput,
            motor,
            stateMachine,
            actionDriver,
            combatMode);

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

    static CharacterController GetOrAddCharacterController(GameObject owner)
    {
        CharacterController controller = owner.GetComponent<CharacterController>();
        return controller != null ? controller : owner.AddComponent<CharacterController>();
    }

    static Transform ResolveModelPoint(string pointName, Transform modelRoot, Transform fallback)
    {
        if (string.IsNullOrWhiteSpace(pointName))
            return fallback;

        Transform point = FindChildRecursive(modelRoot, pointName);
        if (point == null)
        {
            Debug.LogWarning($"CharacterActorFactory: 模型中找不到挂点 {pointName}，已回退到角色根节点。");
            return fallback;
        }

        return point;
    }

    static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null)
            return null;

        if (root.name == childName)
            return root;

        foreach (Transform child in root)
        {
            Transform match = FindChildRecursive(child, childName);
            if (match != null)
                return match;
        }

        return null;
    }
}
