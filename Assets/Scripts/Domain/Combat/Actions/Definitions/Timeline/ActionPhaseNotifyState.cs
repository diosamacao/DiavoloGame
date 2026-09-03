using System;
using UnityEngine;

/// <summary>
/// 动作语义阶段窗口；Startup / Active / Recovery 与覆盖标签统一存放在 ActionTimeline。
/// Recovery 可同时声明移动取消与按 Graph Entry 重开。
/// </summary>
[Serializable]
public sealed class ActionPhaseNotifyState : ActionNotifyState
{
    [SerializeField] ActionPhaseKind kind = ActionPhaseKind.Startup;
    [Tooltip("当前阶段是否允许更高优先级 Action 经 Entry 硬打断。")]
    [SerializeField] bool interruptible = true;
    [Tooltip("仅 Recovery 生效：有移动输入时允许退出 Action 返回 Locomotion。")]
    [SerializeField] bool allowMovementCancel = true;
    [Tooltip("仅 Recovery 生效：有效动作输入可按当前 ActionGraph Entry 重开。")]
    [SerializeField] bool allowEntryRestart = true;
    [Tooltip("本窗受击抗打断加成。与 SuperArmor 独立：标签窗仍完全不可断。")]
    [SerializeField] int interruptResistBonus = 0;

    /// <summary>阶段语义或覆盖标签。</summary>
    public ActionPhaseKind Kind => kind;

    /// <summary>Startup / Active / Recovery 才参与可打断性判定；覆盖标签不改变阶段规则。</summary>
    public bool ControlsInterruptibility =>
        kind is ActionPhaseKind.Startup or ActionPhaseKind.Active or ActionPhaseKind.Recovery;

    /// <summary>是否允许高优 Entry 硬打断。</summary>
    public bool Interruptible => interruptible;

    /// <summary>Recovery 阶段是否允许移动取消。</summary>
    public bool AllowMovementCancel =>
        kind == ActionPhaseKind.Recovery && allowMovementCancel;

    /// <summary>Recovery 阶段是否允许按 Entry 重开。</summary>
    public bool AllowEntryRestart =>
        kind == ActionPhaseKind.Recovery && allowEntryRestart;

    /// <summary>本窗抗打断加成；负值按 0。</summary>
    public int InterruptResistBonus => interruptResistBonus > 0 ? interruptResistBonus : 0;
}
