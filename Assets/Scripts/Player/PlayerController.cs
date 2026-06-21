using UnityEngine;

/// <summary>玩家角色装配与位移入口；Scene 空物体只需挂本组件并指定 CharacterConfig。</summary>
[DefaultExecutionOrder(-50)]
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] CharacterConfig characterConfig = null;
    [SerializeField] Transform cameraTransform;

    CharacterRuntime runtime;

    /// <summary>玩家输入中枢，供 CameraManager 读取视角输入。</summary>
    public InputManager Input => runtime?.Input;

    void Awake()
    {
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (characterConfig == null)
        {
            Debug.LogError("PlayerController: 未绑定 CharacterConfig。", this);
            enabled = false;
            return;
        }

        if (!characterConfig.ValidateForPlayer(this))
        {
            enabled = false;
            return;
        }

        var inputSource = new InputReader(characterConfig.InputActions);
        runtime = CharacterRuntimeFactory.Create(
            gameObject,
            transform,
            characterConfig,
            inputSource,
            cameraTransform);
    }

    void OnEnable()
    {
        runtime?.Enable();
    }

    void OnDisable()
    {
        runtime?.Disable();
    }

    void OnDestroy()
    {
        CombatRuntimeRegistry.Unregister(transform);
    }

    void Update()
    {
        runtime?.Tick(Time.deltaTime);
    }

    /// <summary>相机就绪或切换后刷新运行时使用的相机 Transform。</summary>
    public void SetCameraTransform(Transform targetCamera)
    {
        cameraTransform = targetCamera;
        runtime?.SetCameraTransform(targetCamera);
    }
}
