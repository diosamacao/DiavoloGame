using System;
using UnityEngine;

/// <summary>
/// 玩家招架 Guard / Success 上的接触窗。窗内被敌人 Hitbox 打中时 Pipeline 吞伤并 IssueParried。
/// 对偶 PerfectDodgeWindow；卡肉帧由本窗配置，不读进攻盒 Feedback。
/// </summary>
[Serializable]
public sealed class AssistParryWindowNotifyState : ActionNotifyState
{
    [Tooltip("窗内接触成功时双方卡肉逻辑帧（60Hz）；0 表示不冻。")]
    [SerializeField] int hitStopFrames = AssistParryHitStop.DefaultFrames;

    /// <summary>本窗配置的卡肉帧；小于 0 视为 0。</summary>
    public int HitStopFrames => Mathf.Max(0, hitStopFrames);
}
