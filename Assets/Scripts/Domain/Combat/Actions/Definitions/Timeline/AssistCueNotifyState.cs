using System;
using UnityEngine;

/// <summary>
/// 敌人进攻 Timeline 的极限支援闪光窗。只裁定「能不能切」，不裁定弹刀成功。
/// </summary>
[Serializable]
public sealed class AssistCueNotifyState : ActionNotifyState
{
    [SerializeField] AssistCueKind kind = AssistCueKind.Gold;
    [Tooltip("勾选后上场必须是远程回避，否则按红光处理。")]
    [SerializeField] bool requiresRanged;
    [Tooltip("招架点相对进攻者逻辑根（敌人朝向局部毫米）。缺省正前。")]
    [SerializeField] Vector3 parryLocalOffsetMm = new(0f, 0f, 1200f);
    [Tooltip("回避点局部毫米偏移，可更侧向/更远。")]
    [SerializeField] Vector3 evadeLocalOffsetMm = new(800f, 0f, 1400f);

    /// <summary>金光或红光。</summary>
    public AssistCueKind Kind => kind;

    /// <summary>是否点名远程回避。</summary>
    public bool RequiresRanged => requiresRanged;

    /// <summary>招架 Relocate 局部偏移（毫米）。</summary>
    public Vector3 ParryLocalOffsetMm => parryLocalOffsetMm;

    /// <summary>回避 Relocate 局部偏移（毫米）。</summary>
    public Vector3 EvadeLocalOffsetMm => evadeLocalOffsetMm;

    /// <summary>当前帧起还剩几帧（含本帧）。</summary>
    public int RemainingFramesAt(int frame)
    {
        if (!IsActiveAtFrame(frame))
            return 0;
        return EndFrame - frame + 1;
    }
}
