using System;
using UnityEngine;

/// <summary>单个 Hitbox 命中后的镜头与卡肉反馈配置。</summary>
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

    /// <summary>镜头震动预设；为空表示不触发震动。</summary>
    public CameraShakeProfile CameraShakeProfile => cameraShakeProfile;

    /// <summary>是否配置了有效卡肉。</summary>
    public bool UseHitStop => useHitStop && hitStopFrames > 0;

    /// <summary>卡肉持续逻辑帧数。</summary>
    public int HitStopFrames => Mathf.Max(0, hitStopFrames);

    /// <summary>同一动作会话是否只触发一次卡肉。</summary>
    public bool HitStopOncePerAction => hitStopOncePerAction;
}
