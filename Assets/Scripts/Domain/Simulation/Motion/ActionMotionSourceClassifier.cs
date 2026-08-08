/// <summary>
/// 根据烘焙就绪与脚本位移窗口互斥规则归类动作位移源；纯逻辑，供 Editor 审计与 EditMode 测试。
/// </summary>
public static class ActionMotionSourceClassifier
{
    /// <summary>
    /// 互斥归类：Baked 与 Scripted 同时成立则为 Conflict；
    /// Animator RM 不再作为 Action 位移源。
    /// </summary>
    public static ActionMotionSourceKind Classify(bool bakedReady, bool hasScriptedMovement)
    {
        if (bakedReady && hasScriptedMovement)
            return ActionMotionSourceKind.Conflict;
        if (bakedReady)
            return ActionMotionSourceKind.Baked;
        if (hasScriptedMovement)
            return ActionMotionSourceKind.Scripted;
        return ActionMotionSourceKind.None;
    }
}
