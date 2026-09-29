using System;
using UnityEngine;

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
