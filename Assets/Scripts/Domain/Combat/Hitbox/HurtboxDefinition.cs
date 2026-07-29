using System;
using UnityEngine;

/// <summary>受击框定义：相对挂点的局部 Box，用于静止目标或常驻 Hurtbox。</summary>
[Serializable]
public class HurtboxDefinition
{
    [Tooltip("相对角色根的受击框中心；默认抬高半个标准人形高度。")]
    [SerializeField] Vector3 localOffset = new(0f, 0.9f, 0f);
    [SerializeField] Vector3 localEulerAngles = Vector3.zero;
    [Tooltip("Box 全尺寸（与 Unity BoxCollider.size 一致），非半长。")]
    [SerializeField] Vector3 size = new(0.8f, 1.8f, 0.8f);

    public Vector3 LocalOffset => localOffset;
    public Vector3 LocalEulerAngles => localEulerAngles;
    public Vector3 Size => size;
}
