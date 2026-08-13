using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>本地设备采样边界；只负责把 Unity 输入量化为下一逻辑帧数据。</summary>
public interface ILocalInputSampler
{
    /// <summary>当前渲染帧视角输入；仅供相机表现，不进入权威输入帧。</summary>
    Vector2 LookInput { get; }

    /// <summary>本渲染帧是否按下纯表现 CameraLock；不进入 InputFrame。</summary>
    bool CameraLockPressedThisFrame { get; }

    /// <summary>暂存相机 Orbit 偏航，供下一次设备采样固化为移动参考。</summary>
    void StageMoveReferenceYaw(float yawDegrees);

    /// <summary>采集并量化指定 Actor 的目标逻辑帧。</summary>
    InputFrame Sample(long targetFrame, SimActorId actorId);

    /// <summary>配置需要轮询的离散设备输入。</summary>
    void ConfigureDiscreteInputs(InputActionReference[] references);

    /// <summary>启用设备输入。</summary>
    void Enable();

    /// <summary>禁用设备输入。</summary>
    void Disable();
}
