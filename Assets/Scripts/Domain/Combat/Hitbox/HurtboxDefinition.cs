using System;
using UnityEngine;

/// <summary>受击框定义：相对挂点的局部 Box，用于静止目标或常驻 Hurtbox。</summary>
[Serializable]
public class HurtboxDefinition
{
    [SerializeField] Vector3 localOffset = Vector3.zero;
    [SerializeField] Vector3 localEulerAngles;
    [Tooltip("Box 全尺寸（与 Unity BoxCollider.size 一致），非半长。")]
    [SerializeField] Vector3 size = new(0.8f, 1.8f, 0.8f);

    public Vector3 LocalOffset => localOffset;
    public Vector3 LocalEulerAngles => localEulerAngles;
    public Vector3 Size => size;
}
