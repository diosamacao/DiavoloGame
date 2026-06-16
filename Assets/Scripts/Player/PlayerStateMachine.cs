using UnityEngine;

[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(CharacterAnimationController))]
public class PlayerStateMachine : CharacterStateMachine
{
    PlayerController _player;

    protected override void Awake()
    {
        _player = GetComponent<PlayerController>();
        base.Awake();
    }

    protected override void UpdateContext()
    {
        Context.MoveInputMagnitude = _player.MoveInputMagnitude;
        Context.RunThreshold = _player.RunThreshold;
        Context.IsGrounded = _player.IsGrounded;
    }
}
