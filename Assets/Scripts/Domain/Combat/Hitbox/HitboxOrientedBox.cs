using UnityEngine;

/// <summary>世界空间定向包围盒；HalfExtents 为各轴半长。</summary>
public readonly struct HitboxOrientedBox
{
    public HitboxOrientedBox(Vector3 center, Vector3 halfExtents, Quaternion rotation)
    {
        Center = center;
        HalfExtents = halfExtents;
        Rotation = rotation;
    }

    public Vector3 Center { get; }
    public Vector3 HalfExtents { get; }
    public Quaternion Rotation { get; }

    /// <summary>按 local 轴索引返回世界空间轴向（0=Right, 1=Up, 2=Forward）。</summary>
    public Vector3 GetAxis(int index)
    {
        return index switch
        {
            0 => Rotation * Vector3.right,
            1 => Rotation * Vector3.up,
            _ => Rotation * Vector3.forward,
        };
    }
}
