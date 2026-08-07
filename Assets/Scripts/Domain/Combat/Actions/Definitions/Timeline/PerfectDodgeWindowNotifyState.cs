using System;

/// <summary>
/// 玩家 Dodge Timeline 上的完美闪避窗口；窗内被命中时 Pipeline 吞伤并武装反击缓冲。
/// 在 Action Editor 中通过轨道类型 PerfectDodge 添加（非 Phase/Invincible）。
/// </summary>
[Serializable]
public sealed class PerfectDodgeWindowNotifyState : ActionNotifyState
{
}
