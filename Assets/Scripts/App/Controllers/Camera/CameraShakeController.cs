using Cinemachine;
using UnityEngine;

/// <summary>
/// 基于 Cinemachine Impulse 的镜头震动入口；挂载在场景相机管理对象上，
/// 订阅 AttackHitEvent 在命中时触发。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CameraManager))]
public class CameraShakeController : AppControllerBase
{
    const int DefaultImpulseChannel = 1;

    [Header("References")]
    [SerializeField] CameraManager cameraManager;

    CinemachineImpulseSource _impulseSource;
    CinemachineVirtualCamera _boundVirtualCamera;

    void Awake()
    {
        ResolveCameraManager();
        EnsureImpulseSource();
    }

    void OnEnable()
    {
        RegisterEvent<AttackHitEvent>(HandleAttackHit);
    }

    void OnDisable()
    {
        UnregisterEvent<AttackHitEvent>(HandleAttackHit);
    }

    void Start()
    {
        TryBindImpulseListener();
    }

    void LateUpdate()
    {
        // Virtual Camera 可能在 CameraManager.Start 才创建，持续重试直到绑定成功。
        if (_boundVirtualCamera == null)
            TryBindImpulseListener();
    }

    /// <summary>Virtual Camera 就绪后绑定 Impulse Listener（可由 CameraManager 主动调用）。</summary>
    public void BindVirtualCamera(CinemachineVirtualCamera virtualCamera)
    {
        if (virtualCamera == null)
            return;

        ConfigureImpulseListener(virtualCamera);
        _boundVirtualCamera = virtualCamera;
    }

    /// <summary>AttackHitEvent 命中回调。</summary>
    void HandleAttackHit(AttackHitEvent hitEvent)
    {
        // 玩家镜头只响应玩家主动命中；敌人命中玩家仍保留卡肉与受击表现，但不复用进攻震屏。
        Transform attacker = hitEvent.Context.Attacker;
        if (attacker == null || attacker.GetComponent<PlayerController>() == null)
            return;

        HitboxNotifyState hitbox = hitEvent.Context.Hitbox;
        CameraShakeProfile profile = hitbox?.Payload.Feedback.CameraShakeProfile;
        if (profile == null)
            return;

        Play(profile, hitEvent.HitDirection);
    }

    /// <summary>直接使用 Profile 播放震动。</summary>
    public void Play(CameraShakeProfile profile, Vector3 worldHitDirection)
    {
        if (profile == null)
            return;

        Play(profile.Settings, worldHitDirection);
    }

    /// <summary>直接使用参数播放震动。</summary>
    public void Play(CameraShakeSettings settings, Vector3 worldHitDirection)
    {
        if (!EnsureImpulseSource())
            return;

        TryBindImpulseListener();
        settings.ApplyTo(_impulseSource);

        Vector3 velocity = settings.BuildImpulseVelocity(worldHitDirection);
        Vector3 impulseOrigin = ResolveImpulseOrigin();
        _impulseSource.GenerateImpulseAtPositionWithVelocity(impulseOrigin, velocity);
    }

    /// <summary>Impulse 原点取 Virtual Camera / Main Camera 位置。</summary>
    Vector3 ResolveImpulseOrigin()
    {
        ResolveCameraManager();
        CinemachineVirtualCamera virtualCamera = cameraManager != null
            ? cameraManager.VirtualCamera
            : null;

        if (virtualCamera != null)
            return virtualCamera.State.FinalPosition;

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
            return mainCamera.transform.position;

        return transform.position;
    }

    void ResolveCameraManager()
    {
        if (cameraManager != null)
            return;

        cameraManager = GetComponent<CameraManager>();
        if (cameraManager == null)
            cameraManager = FindObjectOfType<CameraManager>();
    }

    bool EnsureImpulseSource()
    {
        if (_impulseSource != null)
            return true;

        _impulseSource = GetComponent<CinemachineImpulseSource>();
        if (_impulseSource == null)
            _impulseSource = gameObject.AddComponent<CinemachineImpulseSource>();

        InitializeImpulseSourceDefaults(_impulseSource);
        return _impulseSource != null;
    }

    /// <summary>运行时 AddComponent 不会走 Reset()，需手动补齐 Channel 与波形。</summary>
    static void InitializeImpulseSourceDefaults(CinemachineImpulseSource source)
    {
        if (source == null)
            return;

        CinemachineImpulseDefinition definition = source.m_ImpulseDefinition;
        if (definition.m_ImpulseChannel == 0)
            definition.m_ImpulseChannel = DefaultImpulseChannel;

        if (definition.m_ImpulseShape == CinemachineImpulseDefinition.ImpulseShapes.Custom)
            definition.m_ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump;

        definition.m_ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
    }

    void TryBindImpulseListener()
    {
        ResolveCameraManager();
        CinemachineVirtualCamera virtualCamera = cameraManager != null
            ? cameraManager.VirtualCamera
            : FindObjectOfType<CinemachineVirtualCamera>();

        if (virtualCamera == null || virtualCamera == _boundVirtualCamera)
            return;

        BindVirtualCamera(virtualCamera);
    }

    /// <summary>运行时 AddComponent 的 Listener 默认 m_ChannelMask=0，会屏蔽全部 Impulse。</summary>
    static void ConfigureImpulseListener(CinemachineVirtualCamera virtualCamera)
    {
        CinemachineImpulseListener listener = virtualCamera.GetComponent<CinemachineImpulseListener>();
        if (listener == null)
            listener = virtualCamera.gameObject.AddComponent<CinemachineImpulseListener>();

        listener.m_ChannelMask = DefaultImpulseChannel;
        listener.m_Gain = 1f;
        listener.m_UseCameraSpace = true;
        listener.m_ApplyAfter = CinemachineCore.Stage.Noise;
    }

}
