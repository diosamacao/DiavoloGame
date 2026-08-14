using Cinemachine;
using UnityEngine;

/// <summary>
/// 场景相机：第三人称 VCam、Look 输入、锚点平滑；L-DIR5 移动时 yaw 跟随角色朝向（不写 Motor）。
/// </summary>
public class CameraManager : AppControllerBase
{
    const string CameraRootName = "CameraRoot";
    const string OrbitPivotName = "CameraOrbitPivot";
    const string PitchPivotName = "CameraPitchPivot";

    /// <summary>超过该距离时直接吸附，避免传送后长时间追赶。</summary>
    const float SnapDistance = 3f;

    [Header("Targets")]
    [SerializeField] Transform followTarget;
    [SerializeField] string playerTag = "Player";
    [SerializeField] float cameraRootHeight = 1.4f;

    [Header("Input")]
    [Tooltip("可选覆盖；为空时查询 LocalPlayerService。")]
    [SerializeField] PlayerController playerController;

    [Header("Look")]
    [SerializeField] float horizontalSensitivity = 0.15f;
    [SerializeField] float verticalSensitivity = 0.15f;
    [SerializeField] bool invertY = true;
    [SerializeField] float topClamp = 70f;
    [SerializeField] float bottomClamp = -60f;
    [SerializeField] bool lockCursorOnStart = true;

    [Header("Third Person")]
    [SerializeField] float followDistance = 4f;
    [SerializeField] float initialPitch = 15f;

    [Header("Follow Smoothing")]
    [Tooltip("Orbit 锚点追 CameraRoot 的 SmoothDamp 时间；越大越稳，攻击多段位移越不易抖。")]
    [SerializeField] float followSmoothTime = 0.1f;

    [Header("Follow Facing (L-DIR5)")]
    [Tooltip("移动时 Orbit yaw 插值跟随角色朝向，反哺镜头相对 wish 以绕圈。")]
    [SerializeField] bool followFacingWhileMoving = true;
    [Tooltip("相机 yaw 追角色朝向的 SmoothDamp 时间；须明显大于 0，避免贴死自旋。")]
    [SerializeField, Min(0.01f)] float cameraFollowFacingSmoothTime = 0.35f;
    [Tooltip("有 Look 输入后暂停跟随的恢复延迟（秒）。")]
    [SerializeField, Min(0f)] float lookOverrideResumeDelay = 0.25f;
    [Tooltip("判定 Look 抢权的输入死区。")]
    [SerializeField, Min(0f)] float lookOverrideThreshold = 0.01f;

    [Header("Wave1 Lateral Follow")]
    [Tooltip("吸收 CameraRoot 相对 Follow 状态的左右分量；0=忽略左右（Wave1 止血），1=完整跟随。")]
    [Range(0f, 1f)]
    [SerializeField] float lateralFollowFactor = 0.1f;

    [Header("Debug Visualization")]
    [Tooltip("Play 时生成实心锚点球（Game 视图可见），并驱动 Scene 附加箭头/图例。")]
    [SerializeField] bool drawCameraDebugGizmos = true;
    [Tooltip("实心锚点球半径（米）。")]
    [SerializeField] float debugAnchorRadius = 0.07f;
    [Tooltip("Game 视图是否叠锚点名称。")]
    [SerializeField] bool debugAnchorLabels = true;

    CinemachineVirtualCamera virtualCamera;
    Transform cameraRoot;
    Transform orbitPivot;
    Transform pitchPivot;
    float yaw;
    float pitch;
    bool lookEnabled = true;
    Vector3 orbitFollowVelocity;
    bool orbitPositionInitialized;
    Vector3 followAnchorPosition;
    CameraDebugAnchorVisualizer _debugAnchorVisualizer;
    float _yawFollowVelocity;
    float _lookOverrideRemaining;
    bool _cameraLockEnabled;

    public Transform FollowTarget => cameraRoot != null ? cameraRoot : followTarget;

    /// <summary>挂在 Presentation 下的胸口高度锚点；供 Gizmo / Rig 调试。</summary>
    public Transform CameraRootTransform => cameraRoot;

    /// <summary>水平 Orbit 枢轴；供 Gizmo 调试。</summary>
    public Transform OrbitPivotTransform => orbitPivot;

    /// <summary>俯仰枢轴（VCam Follow）；供 Gizmo 调试。</summary>
    public Transform PitchPivotTransform => pitchPivot;

    /// <summary>滤左右 + SmoothDamp 后的跟随点（逻辑 FollowAnchor）。</summary>
    public Vector3 FollowAnchorPosition => followAnchorPosition;

    /// <summary>当前 yaw（度）。</summary>
    public float YawDegrees => yaw;

    /// <summary>当前 pitch（度）。</summary>
    public float PitchDegrees => pitch;

    /// <summary>滤左右吸收系数；供调试面板显示。</summary>
    public float LateralFollowFactor => lateralFollowFactor;

    /// <summary>是否绘制相机调试锚点（运行时实心球 + Scene 附加信息）。</summary>
    public bool DrawCameraDebugGizmos => drawCameraDebugGizmos;

    /// <summary>实心锚点球半径（米）。</summary>
    public float DebugAnchorRadius => debugAnchorRadius;

    /// <summary>Presentation / 跟随用角色根（相机源父级）。</summary>
    public Transform PresentationFollowTarget => followTarget;

    /// <summary>Orbit Yaw 投影的水平前向；供 Motor 相机相对移动（不含挤墙偏转）。</summary>
    public Vector3 PlanarForward
    {
        get
        {
            Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        }
    }

    /// <summary>Orbit Yaw 投影的水平右向。</summary>
    public Vector3 PlanarRight
    {
        get
        {
            Vector3 right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            right.y = 0f;
            return right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.right;
        }
    }

    /// <summary>运行时创建或绑定的第三人称 Virtual Camera。</summary>
    public CinemachineVirtualCamera VirtualCamera => virtualCamera;

    /// <summary>纯表现 Camera Lock 开关；不进入 InputFrame 或角色逻辑状态。</summary>
    public bool CameraLockEnabled => _cameraLockEnabled;

    void Awake()
    {
        pitch = initialPitch;
        EnsureBrain();
        ResolveFollowTarget();
        ResolvePresentationFollowTarget();
        EnsureCameraRoot();
        EnsureCameraShakeController();
        EnsureVirtualCamera();
    }

    void Start()
    {
        ResolveFollowTarget();
        ResolvePresentationFollowTarget();
        if (cameraRoot == null || virtualCamera == null)
        {
            EnsureCameraRoot();
            EnsureCameraShakeController();
            EnsureVirtualCamera();
        }

        if (lockCursorOnStart)
            SetCursorLocked(true);
    }

    void Update()
    {
        ApplyLookInput();
        UpdateCameraLock();
    }

    void LateUpdate()
    {
        // 先跟朝向改 yaw，再把最终 Orbit yaw 暂存给下一逻辑输入帧。
        ApplyFollowFacingYaw();
        SyncOrbitPivots();
        StageMoveReferenceYaw();
        SyncDebugAnchorVisualizer();
    }

    /// <summary>确保开发构建下有实心球可视化，并同步半径/开关。</summary>
    void SyncDebugAnchorVisualizer()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!drawCameraDebugGizmos)
        {
            if (_debugAnchorVisualizer != null)
                _debugAnchorVisualizer.Sync();
            return;
        }

        if (_debugAnchorVisualizer == null)
        {
            _debugAnchorVisualizer = GetComponent<CameraDebugAnchorVisualizer>();
            if (_debugAnchorVisualizer == null)
                _debugAnchorVisualizer = gameObject.AddComponent<CameraDebugAnchorVisualizer>();
            _debugAnchorVisualizer.Bind(this);
        }

        _debugAnchorVisualizer.SetRadius(debugAnchorRadius);
        _debugAnchorVisualizer.SetShowLabels(debugAnchorLabels);
        // 位置刷新放在 Visualizer.LateUpdate（更高 executionOrder），保证本帧 Follow 已写入
#endif
    }

    /// <summary>把最终 Orbit yaw 暂存到设备输入边界，不直接写 Motor。</summary>
    void StageMoveReferenceYaw()
    {
        ResolveLocalPlayer()?.StageMoveReferenceYaw(yaw);
    }

    void EnsureBrain()
    {
        Camera camera = Camera.main;
        if (camera == null)
            camera = FindObjectOfType<Camera>();

        if (camera != null && camera.GetComponent<CinemachineBrain>() == null)
            camera.gameObject.AddComponent<CinemachineBrain>();
    }

    void ResolveFollowTarget()
    {
        if (followTarget != null)
            return;

        ILocalPlayer local = ResolveLocalPlayer();
        if (local != null)
        {
            followTarget = local.PresentationRoot != null ? local.PresentationRoot : local.Root;
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player != null)
            followTarget = player.transform;
    }

    void EnsureCameraRoot()
    {
        if (followTarget == null)
            return;

        Transform existing = followTarget.Find(CameraRootName);
        if (existing != null)
        {
            cameraRoot = existing;
            return;
        }

        var rootObject = new GameObject(CameraRootName);
        rootObject.transform.SetParent(followTarget, false);
        rootObject.transform.localPosition = new Vector3(0f, cameraRootHeight, 0f);
        cameraRoot = rootObject.transform;
    }

    /// <summary>本机玩家：Inspector 覆盖优先，否则 LocalPlayerService，禁止场景 Find。</summary>
    ILocalPlayer ResolveLocalPlayer()
    {
        if (playerController != null)
            return playerController;

        ILocalPlayer local = GetSystem<LocalPlayerService>()?.Local;
        if (local is UnityEngine.Object obj && obj == null)
            return null;
        return local;
    }

    /// <summary>角色装配完成后把相机锚点切到插值表现根，避免追随阶梯式逻辑 Transform。</summary>
    void ResolvePresentationFollowTarget()
    {
        ILocalPlayer local = ResolveLocalPlayer();
        if (local == null)
            return;

        Transform presentationRoot = local.PresentationRoot;
        if (presentationRoot == null || presentationRoot == followTarget)
            return;

        followTarget = presentationRoot;
        if (cameraRoot == null)
            return;

        cameraRoot.SetParent(followTarget, false);
        cameraRoot.localPosition = new Vector3(0f, cameraRootHeight, 0f);
        cameraRoot.localRotation = Quaternion.identity;
    }

    void EnsureOrbitPivots()
    {
        if (orbitPivot == null)
        {
            Transform existingOrbit = transform.Find(OrbitPivotName);
            if (existingOrbit != null)
                orbitPivot = existingOrbit;
            else
            {
                var orbitObject = new GameObject(OrbitPivotName);
                orbitObject.transform.SetParent(transform, false);
                orbitPivot = orbitObject.transform;
            }
        }

        if (pitchPivot == null)
        {
            Transform existingPitch = orbitPivot.Find(PitchPivotName);
            if (existingPitch != null)
                pitchPivot = existingPitch;
            else
            {
                var pitchObject = new GameObject(PitchPivotName);
                pitchObject.transform.SetParent(orbitPivot, false);
                pitchPivot = pitchObject.transform;
            }
        }
    }

    void EnsureVirtualCamera()
    {
        if (cameraRoot == null)
            return;

        CinemachineFreeLook legacyFreeLook = GetComponentInChildren<CinemachineFreeLook>(true);
        if (legacyFreeLook != null)
            Destroy(legacyFreeLook.gameObject);

        EnsureOrbitPivots();

        virtualCamera = GetComponentInChildren<CinemachineVirtualCamera>(true);
        if (virtualCamera == null)
        {
            var cameraObject = new GameObject("CM ThirdPerson");
            cameraObject.transform.SetParent(transform, false);
            virtualCamera = cameraObject.AddComponent<CinemachineVirtualCamera>();
        }

        ConfigureVirtualCamera(virtualCamera);
    }

    void ConfigureVirtualCamera(CinemachineVirtualCamera vcam)
    {
        // Follow / LookAt 都走平滑后的 Orbit，避免 LookAt 仍锁 CameraRoot 造成朝向抽搐。
        vcam.Follow = pitchPivot;
        vcam.LookAt = orbitPivot;
        vcam.m_Lens.FieldOfView = 60f;

        CinemachineTransposer transposer = vcam.GetCinemachineComponent<CinemachineTransposer>();
        if (transposer == null)
            transposer = vcam.AddCinemachineComponent<CinemachineTransposer>();

        transposer.m_FollowOffset = new Vector3(0f, 0f, -followDistance);
        transposer.m_BindingMode = CinemachineTransposer.BindingMode.LockToTarget;
        transposer.m_XDamping = 0f;
        transposer.m_YDamping = 0f;
        transposer.m_ZDamping = 0f;

        CinemachineComposer composer = vcam.GetCinemachineComponent<CinemachineComposer>();
        if (composer != null)
            vcam.DestroyCinemachineComponent<CinemachineComposer>();

        CinemachineHardLookAt hardLookAt = vcam.GetCinemachineComponent<CinemachineHardLookAt>();
        if (hardLookAt == null)
            hardLookAt = vcam.AddCinemachineComponent<CinemachineHardLookAt>();

        CinemachineCollider collider = vcam.GetComponent<CinemachineCollider>();
        if (collider == null)
            collider = vcam.gameObject.AddComponent<CinemachineCollider>();

        collider.m_AvoidObstacles = true;
        collider.m_MinimumDistanceFromTarget = 0.5f;
        collider.m_CollideAgainst = LayerMask.GetMask("Default");
        collider.m_Strategy = CinemachineCollider.ResolutionStrategy.PreserveCameraHeight;

        CameraShakeController shakeController = GetComponent<CameraShakeController>();
        if (shakeController != null)
            shakeController.BindVirtualCamera(vcam);
    }

    void ApplyLookInput()
    {
        ILocalPlayer local = ResolveLocalPlayer();
        if (!lookEnabled || local == null)
            return;

        Vector2 lookInput = local.LookInput;
        float verticalInput = invertY ? -lookInput.y : lookInput.y;

        // Look 抢权：有视角输入时暂停自动跟朝向，松手后延迟恢复
        if (Mathf.Abs(lookInput.x) > lookOverrideThreshold
            || Mathf.Abs(lookInput.y) > lookOverrideThreshold)
        {
            _lookOverrideRemaining = lookOverrideResumeDelay;
            _yawFollowVelocity = 0f;
        }

        yaw += lookInput.x * horizontalSensitivity;
        pitch += verticalInput * verticalSensitivity;
        pitch = Mathf.Clamp(pitch, bottomClamp, topClamp);
    }

    /// <summary>
    /// L-DIR5：有移动且无 Look 抢权时，Orbit yaw 平滑追角色移动朝向；只读 facing，不写 Motor。
    /// </summary>
    void ApplyFollowFacingYaw()
    {
        if (_lookOverrideRemaining > 0f)
            _lookOverrideRemaining = Mathf.Max(0f, _lookOverrideRemaining - Time.deltaTime);

        if (!followFacingWhileMoving || cameraFollowFacingSmoothTime <= 0f)
            return;

        if (_cameraLockEnabled)
        {
            _yawFollowVelocity = 0f;
            return;
        }

        if (_lookOverrideRemaining > 0f)
            return;

        ILocalPlayer local = ResolveLocalPlayer();
        if (local?.Input == null || !local.Input.HasMoveIntent)
        {
            _yawFollowVelocity = 0f;
            return;
        }

        // FaceTarget 时不跟朝向，避免与 strafing 锁面抢 yaw
        if (local.Actor != null
            && local.Actor.IsLocomotionFaceTargetActive)
        {
            _yawFollowVelocity = 0f;
            return;
        }

        Transform facingSource = followTarget != null
            ? followTarget
            : local.Root;
        float targetYaw = facingSource.eulerAngles.y;
        yaw = Mathf.SmoothDampAngle(
            yaw,
            targetYaw,
            ref _yawFollowVelocity,
            cameraFollowFacingSmoothTime);
    }

    /// <summary>读取本地锁定键；有 SelectedTarget 才能开启，无目标时自动退出。</summary>
    void UpdateCameraLock()
    {
        ILocalPlayer local = ResolveLocalPlayer();
        ILocalCameraTargetSource targetSource = local?.Actor;
        bool hasTarget = targetSource != null && targetSource.TargetingSnapshot.HasSelectedTarget;
        if (_cameraLockEnabled && !hasTarget)
            _cameraLockEnabled = false;

        if (local == null || !local.CameraLockPressedThisFrame)
            return;

        if (_cameraLockEnabled)
            _cameraLockEnabled = false;
        else if (hasTarget)
            _cameraLockEnabled = true;
    }

    /// <summary>
    /// FollowAnchor：按角色水平朝向吸收前后/竖直，左右按 lateralFollowFactor 比例吸收，再 SmoothDamp。
    /// Wave 1 临时止血；Wave 2 轨迹拆分后降为构图缓冲。
    /// </summary>
    void SyncOrbitPivots()
    {
        if (orbitPivot == null || pitchPivot == null || cameraRoot == null)
            return;

        Vector3 source = cameraRoot.position;
        if (!orbitPositionInitialized ||
            (followAnchorPosition - source).sqrMagnitude > SnapDistance * SnapDistance)
        {
            followAnchorPosition = source;
            orbitPivot.position = source;
            orbitFollowVelocity = Vector3.zero;
            orbitPositionInitialized = true;
        }
        else
        {
            Vector3 forward = ResolveFollowForwardAxis();
            Vector3 delta = source - followAnchorPosition;
            Vector3 forwardPart = Vector3.Dot(delta, forward) * forward;
            Vector3 verticalPart = new Vector3(0f, delta.y, 0f);
            Vector3 lateralPart = delta - forwardPart - verticalPart;
            float lateralFactor = Mathf.Clamp01(lateralFollowFactor);
            Vector3 absorbed = forwardPart + verticalPart + lateralPart * lateralFactor;
            Vector3 desired = followAnchorPosition + absorbed;

            if (followSmoothTime <= 0f)
            {
                followAnchorPosition = desired;
                orbitFollowVelocity = Vector3.zero;
            }
            else
            {
                followAnchorPosition = Vector3.SmoothDamp(
                    followAnchorPosition,
                    desired,
                    ref orbitFollowVelocity,
                    followSmoothTime);
            }

            orbitPivot.position = followAnchorPosition;
        }

        orbitPivot.rotation = Quaternion.Euler(0f, yaw, 0f);
        pitchPivot.localPosition = Vector3.zero;
        pitchPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    /// <summary>滤左右用角色表现朝向，不用镜头 forward。</summary>
    Vector3 ResolveFollowForwardAxis()
    {
        Transform basis = followTarget != null ? followTarget : cameraRoot;
        Vector3 forward = basis.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            return PlanarForward;
        return forward.normalized;
    }

    /// <summary>供 Gizmo 绘制滤左右轴向。</summary>
    public Vector3 GetFollowForwardAxis() => ResolveFollowForwardAxis();

    /// <summary>立刻吸附到 CameraRoot（传送、切场景等场景用）。</summary>
    public void SnapFollowToTarget()
    {
        if (orbitPivot == null || cameraRoot == null)
            return;

        followAnchorPosition = cameraRoot.position;
        orbitPivot.position = followAnchorPosition;
        orbitFollowVelocity = Vector3.zero;
        orbitPositionInitialized = true;
    }

    public void SetLookEnabled(bool enabled)
    {
        lookEnabled = enabled;
    }

    public void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    /// <summary>确保同物体上有 CameraShakeController，避免场景遗漏挂载导致无震动。</summary>
    void EnsureCameraShakeController()
    {
        if (GetComponent<CameraShakeController>() == null)
            gameObject.AddComponent<CameraShakeController>();
    }
}
