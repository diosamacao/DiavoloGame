using System;
using UnityEngine;

/// <summary>相机 Binding 的根来源；不包含任何模型部位预设。</summary>
public enum CameraBindingSource
{
    Character = 0,
    SelectedTarget = 1,
    World = 2,
}

/// <summary>Binding 是否逐帧跟随根，或在镜头窗进入时冻结世界 Pose。</summary>
public enum CameraBindingSpace
{
    Dynamic = 0,
    Snapshot = 1,
}

/// <summary>端点驱动的预设路径几何；Custom 才允许作者直接编辑 Knot 与 Tangent。</summary>
public enum CameraSplineCurveRule
{
    Custom = 0,
    Linear = 1,
    ArcUp = 2,
    ArcDown = 3,
    ArcLeft = 4,
    ArcRight = 5,
}

/// <summary>模型无关的相机参考系；AnchorId 为空表示来源 Root。</summary>
[Serializable]
public sealed class CameraTransformBinding
{
    [SerializeField] CameraBindingSource source = CameraBindingSource.Character;
    [SerializeField] CameraBindingSpace space = CameraBindingSpace.Dynamic;
    [SerializeField] string anchorId;

    /// <summary>参考根来自角色、当前目标或世界。</summary>
    public CameraBindingSource Source => source;

    /// <summary>逐帧跟随或进入窗时冻结。</summary>
    public CameraBindingSpace Space => space;

    /// <summary>由角色 CameraAnchorProvider 解析的自定义 Id；空表示来源 Root。</summary>
    public string AnchorId => anchorId;
}

/// <summary>模型组件向相机系统暴露自定义锚点的只读契约。</summary>
public interface ICameraAnchorProvider
{
    /// <summary>将配置 Id 映射为当前模型 Transform，不得修改角色状态。</summary>
    bool TryResolveCameraAnchor(string anchorId, out Transform anchor);
}

/// <summary>参考系的世界位置与旋转快照。</summary>
public readonly struct CameraReferencePose
{
    /// <summary>创建世界参考 Pose。</summary>
    public CameraReferencePose(Vector3 position, Quaternion rotation)
    {
        Position = position;
        Rotation = rotation;
    }

    /// <summary>世界位置。</summary>
    public Vector3 Position { get; }

    /// <summary>世界旋转。</summary>
    public Quaternion Rotation { get; }

    /// <summary>将局部点变换到世界。</summary>
    public Vector3 TransformPoint(Vector3 localPoint) => Position + Rotation * localPoint;

    /// <summary>世界坐标单位参考系。</summary>
    public static CameraReferencePose Identity => new(Vector3.zero, Quaternion.identity);
}

/// <summary>共享求值器输出的完整演出相机 Pose。</summary>
public readonly struct CameraShotPose
{
    /// <summary>创建有效镜头 Pose。</summary>
    public CameraShotPose(Vector3 worldPosition, Vector3 worldLookAt, float fieldOfView)
    {
        WorldPosition = worldPosition;
        WorldLookAt = worldLookAt;
        FieldOfView = fieldOfView;
    }

    /// <summary>相机世界位置。</summary>
    public Vector3 WorldPosition { get; }

    /// <summary>观察点世界位置。</summary>
    public Vector3 WorldLookAt { get; }

    /// <summary>视野角。</summary>
    public float FieldOfView { get; }
}

/// <summary>演出结束后的 Gameplay 相机恢复策略。</summary>
public enum CameraRestoreMode
{
    PreviousGameplay = 0,
    ForceFree = 1,
    ForceLockOn = 2,
}

/// <summary>Action 级 Camera 轨设置；与镜头窗口一同内嵌在 ActionDefinition Timeline。</summary>
[Serializable]
public sealed class CameraTrackSettings
{
    [SerializeField] CameraRestoreMode restoreMode = CameraRestoreMode.PreviousGameplay;
    [SerializeField] bool suppressLookInput = true;

    /// <summary>演出结束后恢复哪个 Gameplay 模式。</summary>
    public CameraRestoreMode RestoreMode => restoreMode;

    /// <summary>任一 Camera 窗生效时是否屏蔽玩家 Look。</summary>
    public bool SuppressLookInput => suppressLookInput;
}
