using System;
using UnityEngine;

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
