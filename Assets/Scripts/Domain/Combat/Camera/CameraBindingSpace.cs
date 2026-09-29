using System;
using UnityEngine;

/// <summary>Binding 是否逐帧跟随根，或在镜头窗进入时冻结世界 Pose。</summary>
public enum CameraBindingSpace
{
    Dynamic = 0,
    Snapshot = 1,
}
