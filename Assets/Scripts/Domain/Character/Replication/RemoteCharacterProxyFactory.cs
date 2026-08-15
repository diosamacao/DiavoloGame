using System;
using UnityEngine;

/// <summary>
/// 装配幽灵表现图，或本机 Autonomous 座位（InputManager + 内层走跑机）。
/// 禁止走 CharacterActorFactory（会注册命中收集）。
/// </summary>
/// <summary>本机预测座位：表现体 + 走跑 Runner，共用同一 MotorSim。</summary>
public readonly struct AutonomousPredictedSeat
{
    /// <summary>绑定已装配的预测体与 Runner。</summary>
    public AutonomousPredictedSeat(RemoteCharacterProxy proxy, AutonomousLocomotionRunner runner)
    {
        Proxy = proxy ?? throw new ArgumentNullException(nameof(proxy));
        Runner = runner ?? throw new ArgumentNullException(nameof(runner));
    }

    /// <summary>本机预测表现体；走跑时不得再 Play Locomotion。</summary>
    public RemoteCharacterProxy Proxy { get; }

    /// <summary>本机内层走跑机。</summary>
    public AutonomousLocomotionRunner Runner { get; }
}

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
        BuiltGhost built = BuildGhost(
            config,
            catalog,
            collisionWorld,
            worldOffset,
            fixedDeltaSeconds,
            parent,
            IdleMoveIntentSource.Instance);
        return built.Proxy;
    }

    /// <summary>
    /// 本机 Autonomous 座位：Motor 绑可 Ingest 的 InputManager，并挂内层走跑机。
    /// 禁止走 CharacterActorFactory。
    /// </summary>
    public static AutonomousPredictedSeat CreateAutonomous(
        CharacterConfig config,
        ActionReplicationCatalog catalog,
        ISimCollisionWorld collisionWorld,
        Vector3 worldOffset,
        float fixedDeltaSeconds,
        Transform parent = null)
    {
        var input = new InputManager();
        BuiltGhost built = BuildGhost(
            config,
            catalog,
            collisionWorld,
            worldOffset,
            fixedDeltaSeconds,
            parent,
            input);
        var runner = new AutonomousLocomotionRunner(
            input,
            built.Motor,
            built.Animation,
            built.LocomotionProfile,
            built.Proxy.Root,
            fixedDeltaSeconds);
        return new AutonomousPredictedSeat(built.Proxy, runner);
    }

    /// <summary>装配幽灵表现图；意图源由调用方注入（他人 Idle / 本机 InputManager）。</summary>
    static BuiltGhost BuildGhost(
        CharacterConfig config,
        ActionReplicationCatalog catalog,
        ISimCollisionWorld collisionWorld,
        Vector3 worldOffset,
        float fixedDeltaSeconds,
        Transform parent,
        IMoveIntentSource moveIntent)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));
        if (catalog == null)
            throw new ArgumentNullException(nameof(catalog));
        if (moveIntent == null)
            throw new ArgumentNullException(nameof(moveIntent));
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
            moveIntent,
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

        var proxy = new RemoteCharacterProxy(
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
        return new BuiltGhost(proxy, motor, animation, locomotionProfile);
    }

    readonly struct BuiltGhost
    {
        public BuiltGhost(
            RemoteCharacterProxy proxy,
            CharacterMotor motor,
            CharacterAnimationService animation,
            CharacterLocomotionProfile locomotionProfile)
        {
            Proxy = proxy;
            Motor = motor;
            Animation = animation;
            LocomotionProfile = locomotionProfile;
        }

        public RemoteCharacterProxy Proxy { get; }
        public CharacterMotor Motor { get; }
        public CharacterAnimationService Animation { get; }
        public CharacterLocomotionProfile LocomotionProfile { get; }
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
