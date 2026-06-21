/// <summary>单个角色运行时门面，集中调度输入、动作路由与状态机快照。</summary>
public sealed class CharacterRuntimeFacade
{
    readonly InputReader _inputReader;
    readonly InputManager _inputManager;
    readonly CharacterActionDriver _actionDriver;
    readonly CharacterStateMachine _stateMachine;

    /// <summary>创建角色运行时门面；依赖必须在 Bootstrap 阶段完成绑定。</summary>
    public CharacterRuntimeFacade(
        InputReader inputReader,
        InputManager inputManager,
        CharacterActionDriver actionDriver,
        CharacterStateMachine stateMachine)
    {
        _inputReader = inputReader;
        _inputManager = inputManager;
        _actionDriver = actionDriver;
        _stateMachine = stateMachine;
    }

    /// <summary>采集并分发本帧玩法输入。</summary>
    public void TickInput()
    {
        _inputManager.IngestFrame(_inputReader.CaptureFrame());
        _actionDriver.ProcessGameplayInput();
    }

    /// <summary>把 Motor 层快照推给状态机，保证 StateMachine.Tick 前数据已更新。</summary>
    public void PushMotorSnapshot(float moveInputMagnitude, float runThreshold, bool isGrounded)
    {
        _stateMachine.PushMotorSnapshot(moveInputMagnitude, runThreshold, isGrounded);
    }
}
