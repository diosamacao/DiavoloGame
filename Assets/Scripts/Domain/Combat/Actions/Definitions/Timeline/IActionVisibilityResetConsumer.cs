
/// <summary>角色离开可见阵容状态时，立即清理由该角色持有且可能随父节点冻结的动作表现。</summary>
public interface IActionVisibilityResetConsumer
{
    /// <summary>在角色根停用前回收或停止仍存活的表现实例。</summary>
    void ResetForVisibilityLoss();
}
