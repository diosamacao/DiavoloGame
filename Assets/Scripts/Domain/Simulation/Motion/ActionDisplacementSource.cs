/// <summary>运行时本帧实际选用的位移源（由 BaseMotionMode 解析得到）。</summary>
public enum ActionDisplacementSource
{
    None = 0,
    BakedMotion = 1,
    ScriptedTimeline = 2,
    AnimatorRootMotion = 3,
}
