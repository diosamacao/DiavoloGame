/// <summary>
/// 内层 Locomotion 相位基类：默认允许任意相位互转，由各态 Tick 主动 RequestPhase；
/// ExecuteFrame 在转换之后由 LocomotionStateMachine 调用，保证同帧切态立即生效。
/// </summary>
public abstract class LocomotionPhaseState : StateBase<LocomotionPhase, LocomotionContext>
{
    /// <summary>内层相位默认全开；具体合法性由各态主动切决定。</summary>
    public override bool CanTransitionTo(LocomotionPhase next) => !Id.Equals(next);

    /// <summary>本相位帧执行：动画、Motor、脚步；不含跨相位转换。</summary>
    public abstract void ExecuteFrame(float deltaTime);
}
