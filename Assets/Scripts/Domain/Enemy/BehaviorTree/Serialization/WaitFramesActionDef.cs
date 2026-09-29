using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>Wait 行动定义；作者填秒，Build 转逻辑帧。</summary>
[Serializable]
public sealed class WaitFramesActionDef : EnemyBehaviorNodeDef
{
    [SerializeField] float durationSeconds = 0.5f;

    /// <summary>等待秒数（至少转成 1 逻辑帧）。</summary>
    public float DurationSeconds
    {
        get => durationSeconds;
        set => durationSeconds = Mathf.Max(0f, value);
    }

    /// <inheritdoc />
    public override IBehaviorNode Build() =>
        Wrap(new WaitFramesAction(EnemyBehaviorTime.SecondsToWaitFrames(durationSeconds)));
}
