/// <summary>动作图节点在无输入时自动衔接的触发条件。</summary>
public enum ActionTransitionCondition
{
    /// <summary>动作播放结束时衔接。</summary>
    AnimationEnd = 0,

    /// <summary>当前帧达到规则起始帧时衔接。</summary>
    AtFrame = 1,

    /// <summary>当前动作至少命中一次后立即衔接。</summary>
    OnHitConfirm = 2,

    /// <summary>动作结束且未命中时衔接。</summary>
    OnWhiff = 3,
}
