/// <summary>招式自然结束或指定帧触发的衔接条件。</summary>
public enum ActionTransitionCondition
{
    /// <summary>动画播完时衔接（忽略 startFrame）。</summary>
    AnimationEnd = 0,

    /// <summary>当前帧 &gt;= startFrame 时自动衔接。</summary>
    AtFrame = 1,

    /// <summary>本招至少命中一次后立即衔接（需 IActionHitReceiver 回流）。</summary>
    OnHitConfirm = 2,

    /// <summary>动画播完且本招未命中时衔接。</summary>
    OnWhiff = 3,
}
