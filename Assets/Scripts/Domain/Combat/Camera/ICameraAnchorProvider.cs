using System;
using UnityEngine;

/// <summary>模型组件向相机系统暴露自定义锚点的只读契约。</summary>
public interface ICameraAnchorProvider
{
    /// <summary>将配置 Id 映射为当前模型 Transform，不得修改角色状态。</summary>
    bool TryResolveCameraAnchor(string anchorId, out Transform anchor);
}
