using System;
using UnityEngine;

/// <summary>单个 Hitbox 命中确认后的镜头、卡肉与受击 Cue（VFX/SFX）配置。</summary>
[Serializable]
public sealed class HitFeedbackSettings
{
    [Tooltip("玩家主动命中时使用的镜头震动预设；为空则不震动。")]
    [SerializeField] CameraShakeProfile cameraShakeProfile = null;
    [Tooltip("是否对攻击者施加卡肉。")]
    [SerializeField] bool useHitStop = false;
    [Tooltip("卡肉持续逻辑帧数（60Hz）；写入攻击者 ActionSim.freezeFrames。")]
    [SerializeField] int hitStopFrames = 0;
    [Tooltip("同一动作会话是否只允许第一次命中触发卡肉。")]
    [SerializeField] bool hitStopOncePerAction = true;

    [Header("受击 Cue（Confirm 后 App 播放）")]
    [Tooltip("命中受击点播放的特效 Prefab；为空则不播。挥空不会走到此通道。")]
    [SerializeField] GameObject hitImpactVfxPrefab = null;
    [Tooltip("命中确认时播放的受击音效；为空则不播。")]
    [SerializeField] AudioClip hitImpactSfx = null;
    [Tooltip("受击音效音量。")]
    [Range(0f, 1f)]
    [SerializeField] float hitImpactSfxVolume = 1f;
    [Tooltip("相对逻辑接触点的世界偏移。")]
    [SerializeField] Vector3 hitImpactWorldOffset = Vector3.zero;
    [Tooltip("受击特效世界缩放。")]
    [SerializeField] Vector3 hitImpactScale = Vector3.one;
    [Tooltip("是否在基准朝向（命中水平方向）上叠加随机欧拉角。")]
    [SerializeField] bool randomizeImpactRotation = true;
    [Tooltip("随机欧拉角下限（度），相对基准朝向。")]
    [SerializeField] Vector3 impactRandomEulerMin = new Vector3(0f, 0f, 0f);
    [Tooltip("随机欧拉角上限（度），相对基准朝向。默认绕 Y 全周。")]
    [SerializeField] Vector3 impactRandomEulerMax = new Vector3(0f, 360f, 0f);

    /// <summary>镜头震动预设；为空表示不触发震动。</summary>
    public CameraShakeProfile CameraShakeProfile => cameraShakeProfile;

    /// <summary>是否配置了有效卡肉。</summary>
    public bool UseHitStop => useHitStop && hitStopFrames > 0;

    /// <summary>卡肉持续逻辑帧数。</summary>
    public int HitStopFrames => Mathf.Max(0, hitStopFrames);

    /// <summary>同一动作会话是否只触发一次卡肉。</summary>
    public bool HitStopOncePerAction => hitStopOncePerAction;

    /// <summary>受击特效 Prefab；为空表示不播。</summary>
    public GameObject HitImpactVfxPrefab => hitImpactVfxPrefab;

    /// <summary>受击音效；为空表示不播。</summary>
    public AudioClip HitImpactSfx => hitImpactSfx;

    /// <summary>受击音效音量（0～1）。</summary>
    public float HitImpactSfxVolume => Mathf.Clamp01(hitImpactSfxVolume);

    /// <summary>相对逻辑接触点的世界偏移。</summary>
    public Vector3 HitImpactWorldOffset => hitImpactWorldOffset;

    /// <summary>受击特效世界缩放（分量下限 0.01）。</summary>
    public Vector3 HitImpactScale => Vector3.Max(hitImpactScale, Vector3.one * 0.01f);

    /// <summary>是否对受击特效叠加随机旋转。</summary>
    public bool RandomizeImpactRotation => randomizeImpactRotation;

    /// <summary>随机欧拉角下限（度）。</summary>
    public Vector3 ImpactRandomEulerMin => impactRandomEulerMin;

    /// <summary>随机欧拉角上限（度）。</summary>
    public Vector3 ImpactRandomEulerMax => impactRandomEulerMax;

    /// <summary>是否配置了任一受击 Cue（VFX 或 SFX）。</summary>
    public bool HasHitImpactCue => hitImpactVfxPrefab != null || hitImpactSfx != null;
}
