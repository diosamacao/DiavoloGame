using System;
using UnityEngine;

/// <summary>距离滞回带模式（进/出双阈值语义）。</summary>
public enum DistanceBandMode
{
    /// <summary>过远带（追击）：未入则 d&gt;enter 进入；已入则 d&gt;exit 保持，d≤exit 且 dwell 满离开。</summary>
    OutsideFar = 0,

    /// <summary>过近带：未入则 d&lt;enter 进入；已入则 d&lt;exit 保持，d≥exit 且 dwell 满离开。</summary>
    OutsideNear = 1,

    /// <summary>区间带（对峙）：未入则 enter≤d≤exit 进入；已入则越界且 dwell 满离开。</summary>
    InsideBand = 2,
}
