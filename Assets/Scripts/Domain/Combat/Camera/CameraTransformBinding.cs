using System;
using UnityEngine;

/// <summary>模型无关的相机参考系；AnchorId 为空表示来源 Root。</summary>
[Serializable]
public sealed class CameraTransformBinding
{
    [SerializeField] CameraBindingSource source = CameraBindingSource.Character;
    [SerializeField] CameraBindingSpace space = CameraBindingSpace.Dynamic;
    [SerializeField] string anchorId;

    /// <summary>参考根来自角色、当前目标或世界。</summary>
    public CameraBindingSource Source => source;

    /// <summary>逐帧跟随或进入窗时冻结。</summary>
    public CameraBindingSpace Space => space;

    /// <summary>由角色 CameraAnchorProvider 解析的自定义 Id；空表示来源 Root。</summary>
    public string AnchorId => anchorId;
}
