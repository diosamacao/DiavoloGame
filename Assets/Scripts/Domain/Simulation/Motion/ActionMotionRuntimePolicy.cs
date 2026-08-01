/// <summary>动作位移源选择：烘焙表优先，避免与 Animator RM 双加。</summary>
public static class ActionMotionRuntimePolicy
{
    /// <summary>表就绪时运行时必须查表，禁止再开 Animator Root Motion。</summary>
    public static bool ShouldUseBakedMotion(bool bakedMotionReady) => bakedMotionReady;

    /// <summary>仅策略要求 RootMotion 且尚未烘焙成功时，才启用 OnAnimatorMove。</summary>
    public static bool ShouldUseAnimatorRootMotion(bool useRootMotionPolicy, bool bakedMotionReady) =>
        useRootMotionPolicy && !bakedMotionReady;
}
