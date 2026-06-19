/// <summary>招式自然结束或指定帧触发的衔接条件。</summary>
public enum ActionTransitionCondition
{
    /// <summary>动画播完时衔接（忽略 startFrame）。</summary>
    AnimationEnd = 0,

    /// <summary>当前帧 &gt;= startFrame 时自动衔接。</summary>
    AtFrame = 1,
}
