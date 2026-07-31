using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>单角色运行实例，集中持有输入、移动、状态、动作和战斗服务。</summary>
public sealed class CharacterActor :
    IDisposable,
    ISimulationActor,
    IRenderFrameSampler,
    ISimulationRenderable
{
    readonly ICharacterInputSource _inputSource;
    readonly InputManager _inputManager;
    readonly GameplayIntentProducer _intentProducer;
    readonly CharacterMotor _motor;
    readonly CharacterStateMachine _stateMachine;
    readonly CharacterActionDriver _actionDriver;
    readonly CombatModeService _combatMode;
    readonly CharacterAnimationService _animation;
    readonly CharacterPresentationBridge _presentation;
    PlayerInputFrame _renderInputFrame = PlayerInputFrame.Empty;
    bool _renderSamplingEnabled;
    bool _hasRenderInput;

    /// <summary>角色输入中枢，玩家相机等系统可读取 LookIntent。</summary>
    public InputManager Input => _inputManager;

    /// <summary>动画门面；供注册系统与卡肉使用。</summary>
    public CharacterAnimationService Animation => _animation;

    /// <summary>供相机与表现系统跟随的插值锚点。</summary>
    public Transform PresentationRoot => _presentation.PresentationRoot;

    /// <summary>当前渲染帧插值后的位置。</summary>
    public Vector3 RenderedPosition => _presentation.RenderedPosition;

    /// <summary>当前移动输入幅度。</summary>
    public float MoveInputMagnitude => _motor.MoveInputMagnitude;

    /// <summary>当前跑步阈值。</summary>
    public float RunThreshold => _motor.RunThreshold;

    /// <summary>当前是否着地。</summary>
    public bool IsGrounded => _motor.IsGrounded;

    /// <summary>当前战斗模式服务。</summary>
    public ICombatModeService CombatMode => _combatMode;

    /// <summary>当前顶层角色状态，供 AI 感知与生命周期控制器读取。</summary>
    public CharacterStateType CurrentState => _stateMachine.CurrentStateId;

    /// <summary>死亡动作是否已播放完成。</summary>
    public bool DeathPresentationComplete => _stateMachine.DeathPresentationComplete;

    /// <summary>创建角色实例；所有依赖由工厂一次性注入。</summary>
    public CharacterActor(
        ICharacterInputSource inputSource,
        InputManager inputManager,
        GameplayIntentProducer intentProducer,
        CharacterMotor motor,
        CharacterStateMachine stateMachine,
        CharacterActionDriver actionDriver,
        CombatModeService combatMode,
        CharacterAnimationService animation,
        CharacterPresentationBridge presentation)
    {
        _inputSource = inputSource;
        _inputManager = inputManager;
        _intentProducer = intentProducer;
        _motor = motor;
        _stateMachine = stateMachine;
        _actionDriver = actionDriver;
        _combatMode = combatMode;
        _animation = animation;
        _presentation = presentation;
    }

    /// <summary>启用输入源。</summary>
    public void Enable() => _inputSource.Enable();

    /// <summary>禁用输入源。</summary>
    public void Disable() => _inputSource.Disable();

    /// <summary>更新相机 Transform，用于相机相对移动。</summary>
    public void SetCameraTransform(Transform cameraTransform)
    {
        _motor.SetCameraTransform(cameraTransform);
    }

    /// <summary>执行上层已解析的受击表现并进入硬直；Actor 不负责选招。</summary>
    public void EnterHit(float durationSeconds, ActionDefinition resolvedAction = null)
    {
        if (CurrentState == CharacterStateType.Death)
            return;

        ClearControlledInput();
        var request = new CharacterReactionRequest(durationSeconds, resolvedAction);
        _stateMachine.EnterHit(in request);
    }

    /// <summary>执行上层已解析的死亡表现并进入不可逆死亡状态。</summary>
    public void EnterDeath(ActionDefinition resolvedAction = null)
    {
        ClearControlledInput();
        var request = new CharacterReactionRequest(0f, resolvedAction);
        _stateMachine.EnterDeath(in request);
    }

    /// <summary>汇聚渲染帧设备输入；离散边沿会保留到下一个逻辑 Step。</summary>
    public void SampleRenderFrame()
    {
        PlayerInputFrame sampled = _inputSource.CaptureFrame();
        _renderSamplingEnabled = true;
        if (!_hasRenderInput)
        {
            _renderInputFrame = sampled;
            _hasRenderInput = true;
            return;
        }

        // 高渲染 FPS 可能连续多帧没有逻辑 Step，必须合并边沿而不是只保留最后一帧。
        _renderInputFrame = new PlayerInputFrame(
            sampled.Move,
            sampled.Look,
            MergeInputIds(_renderInputFrame.PressedInputIds, sampled.PressedInputIds),
            sampled.HeldInputIds,
            MergeInputIds(_renderInputFrame.ReleasedInputIds, sampled.ReleasedInputIds));
    }

    /// <summary>由 SimulationWorld 按固定顺序推进输入、动作路由、重力、状态机与动画淡入。</summary>
    public void Step(long frameIndex, float fixedDeltaSeconds)
    {
        _presentation.BeginSimulationStep();
        try
        {
            PlayerInputFrame inputFrame = _hasRenderInput
                ? _renderInputFrame
                : _inputSource.CaptureFrame();
            _inputManager.IngestFrame(inputFrame);

            if (_hasRenderInput)
            {
                // 同一渲染帧追多个逻辑帧时连续量/Held 延续，Pressed/Released 只消费一次。
                _renderInputFrame = new PlayerInputFrame(
                    inputFrame.Move,
                    inputFrame.Look,
                    Array.Empty<string>(),
                    inputFrame.HeldInputIds,
                    Array.Empty<string>());
            }

            _intentProducer.Tick(fixedDeltaSeconds);
            _actionDriver.ProcessGameplayInput();
            _motor.TickGravity(fixedDeltaSeconds);
            _stateMachine.Tick(fixedDeltaSeconds);
            // 状态机内可能 Play 新 Clip，同帧末推进 CrossFade 权重。
            _animation.Tick(fixedDeltaSeconds);
        }
        finally
        {
            _presentation.EndSimulationStep();
        }
    }

    /// <summary>把前后逻辑 Pose 插值到模型表现锚点，不修改权威角色根。</summary>
    public void Render(float interpolationAlpha) => _presentation.Render(interpolationAlpha);

    /// <summary>释放动画 PlayableGraph 等资源。</summary>
    public void Dispose() => _animation?.Dispose();

    /// <summary>受击与死亡优先级高于输入，必须同步清掉连续量和动作缓冲。</summary>
    void ClearControlledInput()
    {
        _inputManager.IngestFrame(PlayerInputFrame.Empty);
        _renderInputFrame = PlayerInputFrame.Empty;
        _hasRenderInput = _renderSamplingEnabled;
        _inputManager.ClearBufferedMoveIntent();
        _actionDriver.ClearPendingActions();
    }

    /// <summary>按首次出现顺序合并多个渲染帧的离散输入边沿并去重。</summary>
    static string[] MergeInputIds(string[] current, string[] sampled)
    {
        if (current == null || current.Length == 0)
            return sampled ?? Array.Empty<string>();
        if (sampled == null || sampled.Length == 0)
            return current;

        var result = new List<string>(current.Length + sampled.Length);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < current.Length; i++)
        {
            if (!string.IsNullOrEmpty(current[i]) && seen.Add(current[i]))
                result.Add(current[i]);
        }
        for (int i = 0; i < sampled.Length; i++)
        {
            if (!string.IsNullOrEmpty(sampled[i]) && seen.Add(sampled[i]))
                result.Add(sampled[i]);
        }

        return result.ToArray();
    }
}
