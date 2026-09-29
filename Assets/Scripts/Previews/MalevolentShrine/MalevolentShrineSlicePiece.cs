using UnityEngine;

/// <summary>预切片块的初始姿态，供重播时复位。</summary>
public sealed class MalevolentShrineSlicePiece : MonoBehaviour
{
    public Vector3 restLocalPosition;
    public Quaternion restLocalRotation;
}
