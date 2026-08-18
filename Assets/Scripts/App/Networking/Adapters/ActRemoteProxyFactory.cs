using System;
using UnityEngine;

/// <summary>装配 ACT 他人/敌人 Observer 表现图；Owner 继续使用 Autonomous CharacterActor。</summary>
public static class ActRemoteProxyFactory
{
    /// <summary>按与权威相同的模型/动画配置生成 RemoteProxy；不注册 World、不挂 Hurtbox。</summary>
    public static RemoteCharacterProxy Create(
        CharacterConfig config,
        ActContentRegistry content,
        ISimCollisionWorld collisionWorld,
        Vector3 worldOffset,
        float fixedDeltaSeconds,
        Transform parent = null)
    {
        BuiltGhost built = BuildGhost(
            config,
            content,
            collisionWorld,
            worldOffset,
            fixedDeltaSeconds,
            parent);
        return built.Proxy;
    }

    /// <summary>装配幽灵表现图；Motor 绑空意图，位移只跟 Snapshot。</summary>
    static BuiltGhost BuildGhost(
        CharacterConfig config,
        ActContentRegistry content,
        ISimCollisionWorld collisionWorld,
        Vector3 worldOffset,
        float fixedDeltaSeconds,
        Transform parent)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));
        if (content == null)
            throw new ArgumentNullException(nameof(content));
        if (config.ModelPrefab == null)
            throw new InvalidOperationException("ActRemoteProxyFactory: CharacterConfig 未绑定 ModelPrefab。");

        if (config.CombatProfile == null
            || !config.CombatProfile.TryGetLocomotionProfile(
                config.CombatProfile.DefaultMode,
                out CharacterLocomotionProfile locomotionProfile)
            || locomotionProfile.AnimationProfile == null)
        {
            throw new InvalidOperationException(
                "ActRemoteProxyFactory: CombatModeProfile 默认模式缺少 LocomotionProfile（须含 AnimationProfile）。");
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
            throw new MissingComponentException("ActRemoteProxyFactory: ModelPrefab 中找不到 Animator。");
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
        // 与权威工厂相同：禁用 Animator Root Motion 并复位局部，避免 Clip 根曲线叠加到快照朝向。
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
            content.Actions,
            worldOffset,
            fixedDeltaSeconds,
            visualMotionRoot,
            ownsRoot: true,
            notifyConsumers,
            config.Combat.Hurtbox);
        return new BuiltGhost(proxy, motor, animation, locomotionProfile);
    }

    /// <summary>Factory 内部装配结果，保留 Motor/Animation/Profile 便于完整构造图校验。</summary>
    readonly struct BuiltGhost
    {
        /// <summary>创建一次完整 Observer 表现图装配结果。</summary>
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

        /// <summary>对外应用快照的只读 Proxy。</summary>
        public RemoteCharacterProxy Proxy { get; }
        /// <summary>Proxy 使用的表现 Motor。</summary>
        public CharacterMotor Motor { get; }
        /// <summary>Proxy 使用的动画服务。</summary>
        public CharacterAnimationService Animation { get; }
        /// <summary>Proxy 使用的 Locomotion 配置。</summary>
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

    /// <summary>实例化模型并应用 CharacterConfig 声明的局部偏移。</summary>
    static Transform SpawnModelInstance(CharacterConfig config, Transform parent)
    {
        GameObject modelInstance = UnityEngine.Object.Instantiate(config.ModelPrefab, parent);
        modelInstance.name = config.ModelPrefab.name + "_Ghost";
        Transform modelTransform = modelInstance.transform;
        modelTransform.localPosition = config.ModelLocalPosition;
        modelTransform.localRotation = config.ModelLocalRotation;
        return modelTransform;
    }

    /// <summary>创建只承接逻辑根插值的表现根。</summary>
    static Transform CreatePresentationRoot(Transform simulationRoot)
    {
        var presentationObject = new GameObject("CharacterPresentationRoot");
        Transform presentationRoot = presentationObject.transform;
        presentationRoot.SetParent(simulationRoot, false);
        return presentationRoot;
    }

    /// <summary>创建动作视觉残差根，避免快照逻辑位姿被动画位移污染。</summary>
    static Transform CreateVisualMotionRoot(Transform presentationRoot)
    {
        var visualObject = new GameObject("CharacterVisualMotionRoot");
        Transform visualRoot = visualObject.transform;
        visualRoot.SetParent(presentationRoot, false);
        visualRoot.localPosition = Vector3.zero;
        visualRoot.localRotation = Quaternion.identity;
        return visualRoot;
    }

    /// <summary>Observer 不读输入；Motor 构造仍需要稳定空意图源。</summary>
    sealed class IdleMoveIntentSource : IMoveIntentSource
    {
        /// <summary>全局无输入意图实例。</summary>
        public static readonly IdleMoveIntentSource Instance = new();

        /// <summary>Observer 永远没有移动轴。</summary>
        public Vector2 MoveIntent => Vector2.zero;
        /// <summary>Observer 移动强度恒为 0。</summary>
        public float MoveMagnitude => 0f;
        /// <summary>Observer 永远没有移动意图。</summary>
        public bool HasMoveIntent => false;
        /// <summary>Observer 不缓存移动轴。</summary>
        public Vector2 BufferedMoveIntent => Vector2.zero;
        /// <summary>Observer 不使用本机相机偏航。</summary>
        public ushort MoveReferenceYawQuantized => 0;
    }
}
