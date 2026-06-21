using UnityEngine;

/// <summary>玩家角色运行时工厂，负责从 CharacterConfig 创建纯 C# 服务图。</summary>
public static class PlayerCharacterRuntimeFactory
{
    /// <summary>按配置创建玩家运行时；Player 根对象只会补齐 CharacterController。</summary>
    public static PlayerCharacterRuntime Create(
        GameObject owner,
        Transform root,
        CharacterConfig config,
        Transform cameraTransform)
    {
        EnsureCombatWorldSystem();

        CharacterMotorConfig motorConfig = config.Motor;
        Transform modelRoot = SpawnModelInstance(config, root);
        Animator animator = modelRoot.GetComponentInChildren<Animator>();
        if (animator == null)
            throw new MissingComponentException("PlayerCharacterRuntimeFactory: ModelPrefab 中找不到 Animator。");

        CharacterController controller = GetOrAddCharacterController(owner);
        motorConfig.ApplyTo(controller);

        var inputReader = new InputReader(config.InputActions);
        var animation = new CharacterAnimationController(
            animator,
            config.DefaultLocomotionProfile,
            config.AnimatorLayerIndex);
        var rootMotion = new CharacterRootMotionDriver(controller, animator);
        var combatMode = new CombatModeController(config.CombatProfile, animation);

        var context = new CharacterContext(root, animation, controller);
        var stateMachine = new CharacterStateMachine(context);
        var actionRuntime = new ActionRuntimeController(root, controller, animation, rootMotion, combatMode);
        context.ActionRuntime = actionRuntime;

        Transform attachPoint = ResolveModelPoint(config.Combat.AttachPointName, modelRoot, root);
        Transform aimOrigin = ResolveModelPoint(config.Combat.AimOriginName, modelRoot, root);
        var targetLock = new CombatTargetLock(root, config.Combat.TeamId, aimOrigin);
        var hitBoxSystem = new HitBoxSystem(root, actionRuntime, attachPoint);
        var vfxPlayer = new ActionVfxPlayer(root, attachPoint);

        actionRuntime.RegisterFrameConsumer(hitBoxSystem);
        actionRuntime.RegisterFrameConsumer(vfxPlayer);

        var sharedInput = new InputManager();
        var actionDriver = new CharacterActionDriver(
            inputReader,
            sharedInput,
            stateMachine,
            actionRuntime,
            combatMode,
            targetLock);
        actionRuntime.BindComboInput(actionDriver.CreateComboInputBridge());

        var runtime = new PlayerCharacterRuntime(
            root,
            controller,
            motorConfig,
            inputReader,
            sharedInput,
            stateMachine,
            actionDriver,
            combatMode,
            cameraTransform);

        var rotationDriver = new ActionRotationDriver(
            root,
            stateMachine,
            runtime.Input,
            runtime,
            actionRuntime,
            targetLock);

        runtime.BindRotationDriver(rotationDriver);
        actionRuntime.BindActionStartContext(runtime);
        CombatRuntimeRegistry.Register(root, actionRuntime, animation.Animator);
        return runtime;
    }

    static Transform SpawnModelInstance(CharacterConfig config, Transform parent)
    {
        GameObject modelInstance = Object.Instantiate(config.ModelPrefab, parent);
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
            Debug.LogWarning($"PlayerCharacterRuntimeFactory: 模型中找不到挂点 {pointName}，已回退到角色根节点。");
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

    static void EnsureCombatWorldSystem()
    {
        if (CombatWorldSystem.Current != null || Object.FindObjectOfType<CombatWorldSystem>() != null)
            return;

        var world = new GameObject("CombatWorldSystem");
        world.AddComponent<CombatWorldSystem>();
    }
}
