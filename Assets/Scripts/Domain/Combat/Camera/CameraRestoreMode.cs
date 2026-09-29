using System;
using UnityEngine;

/// <summary>演出结束后的 Gameplay 相机恢复策略。</summary>
public enum CameraRestoreMode
{
    PreviousGameplay = 0,
    ForceFree = 1,
    ForceLockOn = 2,
}
