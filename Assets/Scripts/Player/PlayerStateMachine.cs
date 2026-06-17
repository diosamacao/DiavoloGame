using UnityEngine;

[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(CharacterAnimationController))]
[RequireComponent(typeof(CharacterRootMotionDriver))]
[RequireComponent(typeof(ActionRuntimeController))]
[RequireComponent(typeof(InputReader))]
public class PlayerStateMachine : CharacterStateMachine
{
    PlayerController _player;

    protected override void Awake()
    {
        _player = GetComponent<PlayerController>();
        base.Awake();
    }

    protected override void ConfigureContext(CharacterContext context)
    {
        context.Input = GetComponent<ICharacterInput>();
        context.ActionRuntime = GetComponent<IActionRuntime>();
    }

    protected override void UpdateContext()
    {
        Context.MoveInputMagnitude = _player.MoveInputMagnitude;
        Context.RunThreshold = _player.RunThreshold;
        Context.IsGrounded = _player.IsGrounded;
    }
}
