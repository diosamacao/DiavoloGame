using System;
using UnityEngine;

/// <summary>Action 级 Camera 轨设置；与镜头窗口一同内嵌在 ActionDefinition Timeline。</summary>
[Serializable]
public sealed class CameraTrackSettings
{
    [SerializeField] CameraRestoreMode restoreMode = CameraRestoreMode.PreviousGameplay;
    [SerializeField] bool suppressLookInput = true;

    /// <summary>演出结束后恢复哪个 Gameplay 模式。</summary>
    public CameraRestoreMode RestoreMode => restoreMode;

    /// <summary>任一 Camera 窗生效时是否屏蔽玩家 Look。</summary>
    public bool SuppressLookInput => suppressLookInput;
}
