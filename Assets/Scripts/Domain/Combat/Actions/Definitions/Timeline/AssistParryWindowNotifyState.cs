using System;

/// <summary>
/// 玩家招架 Guard 上的接触窗。窗内被敌人 Hitbox 打中时 Pipeline 吞伤并 IssueParried。
/// 对偶 PerfectDodgeWindow；不武装切人当帧突击。
/// </summary>
[Serializable]
public sealed class AssistParryWindowNotifyState : ActionNotifyState
{
}
