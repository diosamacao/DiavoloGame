using System;
using UnityEngine;

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
