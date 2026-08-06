/// <summary>动作位移源选择：显式 BaseMotionMode 优先，Legacy 资产回退旧策略。</summary>
public static class ActionMotionRuntimePolicy
{
    /// <summary>表就绪时运行时必须查表，禁止再开 Animator Root Motion。</summary>
    public static bool ShouldUseBakedMotion(bool bakedMotionReady) => bakedMotionReady;

    /// <summary>仅策略要求 RootMotion 且尚未烘焙成功时，才启用 OnAnimatorMove。</summary>
    public static bool ShouldUseAnimatorRootMotion(bool useRootMotionPolicy, bool bakedMotionReady) =>
        useRootMotionPolicy && !bakedMotionReady;

    /// <summary>
    /// 解析本帧位移权威。
    /// LegacyResolve：baked → scripted(!useRM) → Animator RM；显式模式互斥，不再隐式混用。
    /// </summary>
    public static ActionDisplacementSource Resolve(
        ActionBaseMotionMode baseMotionMode,
        bool useRootMotionPolicy,
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
                return ActionDisplacementSource.None;
            case ActionBaseMotionMode.LegacyResolve:
            default:
                if (bakedMotionReady)
                    return ActionDisplacementSource.BakedMotion;
                // 与 ActionDefinition.HasScriptedDisplacement 一致：UseRootMotion 时忽略脚本窗
                if (hasScriptedMovement && !useRootMotionPolicy)
                    return ActionDisplacementSource.ScriptedTimeline;
                if (useRootMotionPolicy)
                    return ActionDisplacementSource.AnimatorRootMotion;
                return ActionDisplacementSource.None;
        }
    }

    /// <summary>是否应开启 Animator.applyRootMotion。</summary>
    public static bool ShouldEnableAnimatorRootMotion(ActionDisplacementSource source) =>
        source == ActionDisplacementSource.AnimatorRootMotion;
}
