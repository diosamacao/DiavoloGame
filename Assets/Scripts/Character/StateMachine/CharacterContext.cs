using UnityEngine;

public class CharacterContext
{
    public CharacterContext(
        Transform transform,
        CharacterAnimationController animation,
        CharacterController motor)
    {
        Transform = transform;
        Animation = animation;
        Motor = motor;
    }

    public Transform Transform { get; }
    public CharacterAnimationController Animation { get; }
    public CharacterController Motor { get; }

    public float MoveInputMagnitude { get; set; }
    public float RunThreshold { get; set; }
    public bool IsGrounded { get; set; }
    public ICharacterStateMachine StateMachine { get; set; }
}
