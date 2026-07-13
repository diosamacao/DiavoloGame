using System;
using UnityEngine;

/// <summary>播放 VFX 的点事件；在触发帧生成实例，播放倍率由 Inspector 显式配置。</summary>
[Serializable]
public class PlayVfxNotify : ActionNotify
{
    [SerializeField] GameObject prefab = null;
    [Tooltip("按模型子节点名解析挂点；空则使用角色默认挂点。")]
    [SerializeField] string attachPointId = string.Empty;
    [SerializeField] Vector3 localOffset = new(0f, 1f, 0.8f);
    [SerializeField] Vector3 localEulerAngles = Vector3.zero;
    [SerializeField] Vector3 localScale = Vector3.one;
    [Tooltip("勾选时实例挂到 attachPoint 下并使用 local 变换；否则在世界空间一次性生成。")]
    [SerializeField] bool parentToAttachPoint = true;
    [Tooltip("粒子 simulationSpeed 倍率；1 = 原速。")]
    [SerializeField] float playbackSpeed = 1f;

    /// <summary>触发时实例化的 VFX Prefab。</summary>
    public GameObject Prefab => prefab;

    /// <summary>挂点名；空则回退默认挂点。</summary>
    public string AttachPointId => attachPointId;

    /// <summary>相对挂点的局部位置偏移。</summary>
    public Vector3 LocalOffset => localOffset;

    /// <summary>相对挂点的局部欧拉角。</summary>
    public Vector3 LocalEulerAngles => localEulerAngles;

    /// <summary>实例化后的局部缩放。</summary>
    public Vector3 LocalScale => localScale;

    /// <summary>是否将实例作为挂点子物体生成。</summary>
    public bool ParentToAttachPoint => parentToAttachPoint;

    /// <summary>播放倍率；至少为极小正数，避免粒子停死。</summary>
    public float PlaybackSpeed => Mathf.Max(0.0001f, playbackSpeed);
}
