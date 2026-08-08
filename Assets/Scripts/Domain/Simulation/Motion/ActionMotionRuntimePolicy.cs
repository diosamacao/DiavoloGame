/// <summary>动作位移源选择：仅 Baked / Scripted / None，无 Animator RM 回退。</summary>
public static class ActionMotionRuntimePolicy
{
    /// <summary>表就绪时运行时必须查表。</summary>
    public static bool ShouldUseBakedMotion(bool bakedMotionReady) => bakedMotionReady;

    /// <summary>
    /// 解析本帧位移权威。显式模式互斥；未知/已删枚举值（含旧 Legacy=0）→ None。
    /// </summary>
    public static ActionDisplacementSource Resolve(
        ActionBaseMotionMode baseMotionMode,
        bool bakedMotionReady,
        bool hasScriptedMovement)
    {
        switch (baseMotionMode)
        {
            case ActionBaseMotionMode.BakedMotion:
                return bakedMotionReady
                    ? ActionDisplacementSource.BakedMotion
                    : ActionDisplacementSource.None;
            case ActionBaseMotionMode.ScriptedTimeline:
                return hasScriptedMovement
                    ? ActionDisplacementSource.ScriptedTimeline
                    : ActionDisplacementSource.None;
            case ActionBaseMotionMode.None:
            default:
                return ActionDisplacementSource.None;
        }
    }
}
