using UnityEngine;

/// <summary>玩家角色装配与位移入口；Scene 空物体只需挂本组件并指定 CharacterConfig。</summary>
[DefaultExecutionOrder(-50)]
public class PlayerController : MonoBehaviour, IActionStartContext, IMoveIntentResolver
{
    [Header("References")]
    [SerializeField] CharacterConfig characterConfig = null;
    [SerializeField] Transform cameraTransform;

    readonly InputManager _inputManager = new();

    CharacterMotorConfig motorConfig;
    CharacterController controller;
    InputReader inputReader;
    CharacterAnimationController animationController;
    CharacterRootMotionDriver rootMotionDriver;
    /// <summary>玩家状态机；基类类型供 PushMotorSnapshot 与当前状态读取共用。</summary>
    CharacterStateMachine stateMachine;
    CombatModeController combatMode;
    ActionRuntimeController actionRuntime;
    CharacterActionDriver actionDriver;
    ActionRotationDriver rotationDriver;
    HitBoxSystem hitBoxSystem;
    ActionVfxPlayer vfxPlayer;
    CombatTargetLock targetLock;
    CharacterRuntimeFacade runtimeFacade;
    GameObject modelInstance;

    Vector3 velocity;
    float rotationVelocity;
    float moveInputMagnitude;

    public InputManager Input => _inputManager;
    public float MoveInputMagnitude => moveInputMagnitude;
    public float RunThreshold => motorConfig.RunThreshold;
    public bool IsGrounded => controller.isGrounded;
    public ICombatModeController CombatMode => combatMode;
    public float DefaultRotationSmoothTime => motorConfig.RotationSmoothTime;

    void Awake()
    {
        if (!TryBootstrapCharacter())
        {
            enabled = false;
            return;
        }

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    /// <summary>按 CharacterConfig 创建模型并装配玩家运行时组件；失败时禁用 PlayerController。</summary>
    bool TryBootstrapCharacter()
    {
        if (characterConfig == null)
        {
            Debug.LogError("PlayerController: 未绑定 CharacterConfig。", this);
            return false;
        }

        if (!characterConfig.ValidateForPlayer(this))
            return false;

        EnsureCombatWorldSystem();
        motorConfig = characterConfig.Motor;

        Transform modelRoot = SpawnModelInstance();
        Animator animator = modelRoot.GetComponentInChildren<Animator>();
        if (animator == null)
        {
            Debug.LogError("PlayerController: CharacterConfig.ModelPrefab 中找不到 Animator。", this);
            return false;
        }

        controller = GetOrAdd<CharacterController>();
        motorConfig.ApplyTo(controller);

        inputReader = GetOrAdd<InputReader>();
        inputReader.BindInputActions(characterConfig.InputActions);

        animationController = GetOrAdd<CharacterAnimationController>();
        animationController.Bind(
            animator,
            characterConfig.DefaultLocomotionProfile,
            characterConfig.AnimatorLayerIndex);

        rootMotionDriver = GetOrAdd<CharacterRootMotionDriver>();
        rootMotionDriver.BindAnimator(animator);

        combatMode = GetOrAdd<CombatModeController>();
        combatMode.BindProfile(characterConfig.CombatProfile);

        actionRuntime = GetOrAdd<ActionRuntimeController>();
        actionRuntime.BindCombatMode(combatMode);

        Transform attachPoint = ResolveModelPoint(characterConfig.Combat.AttachPointName, modelRoot);
        Transform aimOrigin = ResolveModelPoint(characterConfig.Combat.AimOriginName, modelRoot);

        targetLock = GetOrAdd<CombatTargetLock>();
        targetLock.Bind(characterConfig.Combat.TeamId, aimOrigin);

        hitBoxSystem = GetOrAdd<HitBoxSystem>();
        hitBoxSystem.Bind(actionRuntime, attachPoint);

        vfxPlayer = GetOrAdd<ActionVfxPlayer>();
        vfxPlayer.Bind(actionRuntime, attachPoint);

        actionRuntime.RegisterFrameConsumer(hitBoxSystem);
        actionRuntime.RegisterFrameConsumer(vfxPlayer);

        stateMachine = GetOrAdd<PlayerStateMachine>();
        actionDriver = GetOrAdd<CharacterActionDriver>();
        rotationDriver = GetOrAdd<ActionRotationDriver>();

        actionDriver.BindInput(_inputManager);
        rotationDriver.Bind(_inputManager, this);
        actionRuntime.BindComboInput(actionDriver.CreateComboInputBridge());
        actionRuntime.BindActionStartContext(this);
        actionDriver.InitializeInputRouting();
        runtimeFacade = new CharacterRuntimeFacade(
            inputReader,
            _inputManager,
            actionDriver,
            stateMachine);
        return true;
    }

    /// <summary>实例化配置中的模型 Prefab，并作为当前玩家根节点的子物体。</summary>
    Transform SpawnModelInstance()
    {
        modelInstance = Instantiate(characterConfig.ModelPrefab, transform);
        modelInstance.name = characterConfig.ModelPrefab.name;
        Transform modelTransform = modelInstance.transform;
        modelTransform.localPosition = characterConfig.ModelLocalPosition;
        modelTransform.localRotation = characterConfig.ModelLocalRotation;
        return modelTransform;
    }

    /// <summary>查找模型内命名挂点；未配置或未找到时回退到玩家根节点。</summary>
    Transform ResolveModelPoint(string pointName, Transform modelRoot)
    {
        if (string.IsNullOrWhiteSpace(pointName))
            return transform;

        Transform point = FindChildRecursive(modelRoot, pointName);
        if (point == null)
        {
            Debug.LogWarning($"PlayerController: 模型中找不到挂点 {pointName}，已回退到角色根节点。", this);
            return transform;
        }

        return point;
    }

    /// <summary>递归查找子节点名，供 CharacterConfig 使用稳定挂点名而不是直接引用 Prefab 子物体。</summary>
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

    /// <summary>获取或补齐运行时组件；所有业务依赖集中在 Bootstrap 阶段建立。</summary>
    T GetOrAdd<T>() where T : Component
    {
        T component = GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    /// <summary>确保场景级战斗系统存在；只在 Bootstrap 阶段查找或创建。</summary>
    void EnsureCombatWorldSystem()
    {
        if (CombatWorldSystem.Current != null || FindObjectOfType<CombatWorldSystem>() != null)
            return;

        var world = new GameObject("CombatWorldSystem");
        world.AddComponent<CombatWorldSystem>();
    }

    void Update()
    {
        runtimeFacade.TickInput();
        ExecuteLocomotionMovement();
        ApplyGravity();
        // 在 StateMachine.Update（order 0）之前写入 Context；单向 PC → PSM，无反向引用。
        runtimeFacade.PushMotorSnapshot(moveInputMagnitude, motorConfig.RunThreshold, IsGrounded);
    }

    public void FaceBufferedMoveIntent()
    {
        Vector2 moveIntent = _inputManager.HasMoveIntent
            ? _inputManager.MoveIntent
            : _inputManager.BufferedMoveIntent;

        Vector3 direction = ResolveWorldMoveDirection(moveIntent);
        if (direction.sqrMagnitude < 0.001f)
            return;

        transform.rotation = Quaternion.LookRotation(direction);
    }

    /// <summary>Locomotion 状态下根据移动意图执行位移。</summary>
    void ExecuteLocomotionMovement()
    {
        if (stateMachine.CurrentStateId == CharacterStateType.Action)
        {
            moveInputMagnitude = 0f;
            return;
        }

        Vector2 moveIntent = _inputManager.MoveIntent;
        Vector3 moveDirection = ResolveWorldMoveDirection(moveIntent);
        moveInputMagnitude = _inputManager.MoveMagnitude;
        float speed = moveInputMagnitude > motorConfig.RunThreshold
            ? motorConfig.RunSpeed
            : motorConfig.WalkSpeed;

        if (moveDirection.sqrMagnitude <= 0.001f)
            return;

        transform.rotation = GetSmoothedRotation(moveDirection);
        controller.Move(moveDirection * (speed * moveInputMagnitude) * Time.deltaTime);
    }

    /// <summary>将移动意图转为世界空间方向。</summary>
    public Vector3 ResolveWorldMoveDirection(Vector2 moveIntent)
    {
        if (moveIntent.sqrMagnitude < 0.01f)
            return Vector3.zero;

        if (cameraTransform == null)
            return new Vector3(moveIntent.x, 0f, moveIntent.y).normalized;

        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        return (forward * moveIntent.y + right * moveIntent.x).normalized;
    }

    Quaternion GetSmoothedRotation(Vector3 moveDirection)
    {
        if (motorConfig.RotationSmoothTime <= 0.001f)
            return Quaternion.LookRotation(moveDirection);

        float targetAngle = Mathf.Atan2(moveDirection.x, moveDirection.z) * Mathf.Rad2Deg;
        float angle = Mathf.SmoothDampAngle(
            transform.eulerAngles.y,
            targetAngle,
            ref rotationVelocity,
            motorConfig.RotationSmoothTime);

        return Quaternion.Euler(0f, angle, 0f);
    }

    void ApplyGravity()
    {
        if (controller.isGrounded && velocity.y < 0f)
            velocity.y = motorConfig.GroundedGravity;

        velocity.y += motorConfig.Gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}
