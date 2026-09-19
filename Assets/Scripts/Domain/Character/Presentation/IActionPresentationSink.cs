/// <summary>
/// 只读消费 ActionSim 的事件与快照；实现不得修改 ActionSim、MotorSim、Numeric 或 PartyState。
/// </summary>
public interface IActionPresentationSink
{
    /// <summary>消费动作生命周期或整数帧事件。</summary>
    void ConsumeEvent(in ActionSimEvent actionEvent);

    /// <summary>在 Gameplay 位移前按快照同步动画与冻结表现。</summary>
    void ApplyBeforeGameplay(in ActionSimSnapshot snapshot, float fixedDeltaSeconds);

    /// <summary>在 Gameplay 位移后按快照记录视觉残差。</summary>
    void ApplyAfterGameplay(in ActionSimSnapshot snapshot);

    /// <summary>角色状态完成本逻辑步后，按同一动作快照推进可见动画。</summary>
    void CompleteSimulationStep(in ActionSimSnapshot snapshot, float fixedDeltaSeconds);

    /// <summary>角色退出可见状态时清理持续表现。</summary>
    void ResetForVisibilityLoss();

    /// <summary>消费不切主状态的轻受击表现事件。</summary>
    void ConsumeFlinch(AnimationKey key, in ActionHitContext context);
}
