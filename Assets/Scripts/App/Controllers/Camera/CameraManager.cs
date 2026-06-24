using Cinemachine;
using UnityEngine;

/// <summary>场景相机控制器：创建第三人称虚拟相机并驱动 look 输入与相机层级同步。</summary>
public class CameraManager : MonoBehaviour
{
    const string CameraRootName = "CameraRoot";
    const string OrbitPivotName = "CameraOrbitPivot";
    const string PitchPivotName = "CameraPitchPivot";

    [Header("Targets")]
    [SerializeField] Transform followTarget;
    [SerializeField] string playerTag = "Player";
    [SerializeField] float cameraRootHeight = 1.4f;

    [Header("Input")]
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

    CinemachineVirtualCamera virtualCamera;
    Transform cameraRoot;
    Transform orbitPivot;
    Transform pitchPivot;
    float yaw;
    float pitch;
    bool lookEnabled = true;

    public Transform FollowTarget => cameraRoot != null ? cameraRoot : followTarget;

    /// <summary>运行时创建或绑定的第三人称 Virtual Camera。</summary>
    public CinemachineVirtualCamera VirtualCamera => virtualCamera;

    void Awake()
    {
        pitch = initialPitch;
        EnsureBrain();
        ResolveFollowTarget();
        EnsureCameraRoot();
        ResolvePlayerController();
        EnsureCameraShakeController();
        EnsureVirtualCamera();
    }

    void Start()
    {
        if (cameraRoot == null || virtualCamera == null)
        {
            ResolveFollowTarget();
            EnsureCameraRoot();
            ResolvePlayerController();
            EnsureCameraShakeController();
            EnsureVirtualCamera();
        }

        if (lockCursorOnStart)
            SetCursorLocked(true);
    }

    void Update()
    {
        ApplyLookInput();
    }

    void LateUpdate()
    {
        SyncOrbitPivots();
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

    void ResolvePlayerController()
    {
        if (playerController != null)
            return;

        if (followTarget != null)
            playerController = followTarget.GetComponent<PlayerController>();
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
        vcam.Follow = pitchPivot;
        vcam.LookAt = cameraRoot;
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
        if (!lookEnabled || playerController == null || playerController.Input == null)
            return;

        Vector2 lookInput = playerController.Input.LookIntent;
        float verticalInput = invertY ? -lookInput.y : lookInput.y;

        yaw += lookInput.x * horizontalSensitivity;
        pitch += verticalInput * verticalSensitivity;
        pitch = Mathf.Clamp(pitch, bottomClamp, topClamp);
    }

    void SyncOrbitPivots()
    {
        if (orbitPivot == null || pitchPivot == null || cameraRoot == null)
            return;

        orbitPivot.position = cameraRoot.position;
        orbitPivot.rotation = Quaternion.Euler(0f, yaw, 0f);
        pitchPivot.localPosition = Vector3.zero;
        pitchPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
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
