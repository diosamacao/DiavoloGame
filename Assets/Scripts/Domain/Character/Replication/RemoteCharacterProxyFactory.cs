using System;
using UnityEngine;

/// <summary>
/// 只装配幽灵表现图：模型、Motor、动画、插值锚点、VisualMotionRoot、VFX/SFX 消费者。
/// 禁止走 CharacterActorFactory（会注册命中收集）。
/// </summary>
public static class RemoteCharacterProxyFactory
{
    /// <summary>按与权威相同的模型/动画配置生成 RemoteProxy；不注册 World、不挂 Hurtbox。</summary>
    public static RemoteCharacterProxy Create(
        CharacterConfig config,
        ActionReplicationCatalog catalog,
        ISimCollisionWorld collisionWorld,
        Vector3 worldOffset,
        float fixedDeltaSeconds,
        Transform parent = null)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));
        if (catalog == null)
            throw new ArgumentNullException(nameof(catalog));
        if (config.ModelPrefab == null)
            throw new InvalidOperationException("RemoteCharacterProxyFactory: CharacterConfig 未绑定 ModelPrefab。");

        if (config.CombatProfile == null
            || !config.CombatProfile.TryGetLocomotionProfile(
                config.CombatProfile.DefaultMode,
                out CharacterLocomotionProfile locomotionProfile)
            || locomotionProfile.AnimationProfile == null)
        {
            throw new InvalidOperationException(
                "RemoteCharacterProxyFactory: CombatModeProfile 默认模式缺少 LocomotionProfile（须含 AnimationProfile）。");
        }

        var owner = new GameObject("RemoteCharacterGhost");
        if (parent != null)
            owner.transform.SetParent(parent, false);

        CharacterMotorConfig motorConfig = config.Motor;
        Transform presentationRoot = CreatePresentationRoot(owner.transform);
        Transform visualMotionRoot = CreateVisualMotionRoot(presentationRoot);
        Transform modelRoot = SpawnModelInstance(config, visualMotionRoot);
        Animator animator = modelRoot.GetComponentInChildren<Animator>();
        if (animator == null)
        {
            UnityEngine.Object.Destroy(owner);
            throw new MissingComponentException("RemoteCharacterProxyFactory: ModelPrefab 中找不到 Animator。");
        }

        CharacterController controller = owner.AddComponent<CharacterController>();
        motorConfig.ApplyTo(controller);
        controller.enabled = false;

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
            owner.transform,
            controller,
            motorConfig,
            IdleMoveIntentSource.Instance,
            motorSim);
        IAnimationPlayback playback = new PlayableAnimationPlayback(animator);
        var animation = new CharacterAnimationService(
            playback,
            animator,
            locomotionProfile.AnimationProfile);
        // 与权威工厂相同：默认关掉 Animator RM 并复位局部，避免 Clip 根曲线叠在快照朝向上
        _ = new CharacterRootMotionDriver(motor, animator);
        var presentation = new CharacterPresentationBridge(owner.transform, presentationRoot);

        Transform defaultAttach = ResolveModelPoint(
            config.Combat.AttachPointName,
            modelRoot,
            owner.transform);
        var attachPoints = new CharacterAttachPointResolver(modelRoot, defaultAttach);
        IActionNotifyConsumer[] notifyConsumers =
        {
            new ActionVfxPlayer(owner.transform, attachPoints),
            new ActionSfxPlayer(owner.transform),
        };

        return new RemoteCharacterProxy(
            owner.transform,
            motor,
            animation,
            presentation,
            catalog,
            worldOffset,
            fixedDeltaSeconds,
            visualMotionRoot,
            ownsRoot: true,
            notifyConsumers);
    }

    /// <summary>按配置挂点名在模型下查找；找不到回退角色根。</summary>
    static Transform ResolveModelPoint(string pointName, Transform modelRoot, Transform fallback)
    {
        if (string.IsNullOrWhiteSpace(pointName))
            return fallback;

        Transform point = CharacterAttachPointResolver.FindByName(modelRoot, pointName);
        return point != null ? point : fallback;
    }

    static Transform SpawnModelInstance(CharacterConfig config, Transform parent)
    {
        GameObject modelInstance = UnityEngine.Object.Instantiate(config.ModelPrefab, parent);
        modelInstance.name = config.ModelPrefab.name + "_Ghost";
        Transform modelTransform = modelInstance.transform;
        modelTransform.localPosition = config.ModelLocalPosition;
        modelTransform.localRotation = config.ModelLocalRotation;
        return modelTransform;
    }

    static Transform CreatePresentationRoot(Transform simulationRoot)
    {
        var presentationObject = new GameObject("CharacterPresentationRoot");
        Transform presentationRoot = presentationObject.transform;
        presentationRoot.SetParent(simulationRoot, false);
        return presentationRoot;
    }

    static Transform CreateVisualMotionRoot(Transform presentationRoot)
    {
        var visualObject = new GameObject("CharacterVisualMotionRoot");
        Transform visualRoot = visualObject.transform;
        visualRoot.SetParent(presentationRoot, false);
        visualRoot.localPosition = Vector3.zero;
        visualRoot.localRotation = Quaternion.identity;
        return visualRoot;
    }

    /// <summary>幽灵不读输入；Motor 构造仍需要空意图源。</summary>
    sealed class IdleMoveIntentSource : IMoveIntentSource
    {
        public static readonly IdleMoveIntentSource Instance = new();

        public Vector2 MoveIntent => Vector2.zero;
        public float MoveMagnitude => 0f;
        public bool HasMoveIntent => false;
        public Vector2 BufferedMoveIntent => Vector2.zero;
        public ushort MoveReferenceYawQuantized => 0;
    }
}
