using UnityEngine;

/// <summary>可复用的镜头震动预设，供 ActionDefinition 或 CameraShakeController 引用。</summary>
[CreateAssetMenu(fileName = "CameraShakeProfile", menuName = "ACT/Camera/Camera Shake Profile")]
public class CameraShakeProfile : ScriptableObject
{
    [SerializeField] CameraShakeSettings settings = CameraShakeSettings.DefaultLight;

    /// <summary>震动参数快照。</summary>
    public CameraShakeSettings Settings => settings;
}
