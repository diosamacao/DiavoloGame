using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>单条落脚标记：当前 AnimationKey 周期内的整数逻辑帧。</summary>
[Serializable]
public struct FootPlantMarker
{
    [Min(0)] public int frame;
    public FootSide foot;

    /// <summary>周期内触发帧。</summary>
    public int Frame => frame;

    /// <summary>触发脚。</summary>
    public FootSide Foot => foot;

}
