using UnityEngine;

public class CharacterContext
{
    public CharacterContext(
        Transform transform,
        CharacterAnimationController animation,
        CharacterController motor,
        CharacterMotor movement)
    {
        Transform = transform;
        Animation = animation;
        Motor = motor;
        Movement = movement;
    }

    public Transform Transform { get; }
    public CharacterAnimationController Animation { get; }
    public CharacterController Motor { get; }
    public CharacterMotor Movement { get; }

    public float MoveInputMagnitude { get; set; }
    public float RunThreshold { get; set; }
    public bool IsGrounded { get; set; }
    public ICharacterStateMachine StateMachine { get; set; }

    public IActionRuntime ActionRuntime { get; set; }
    public ActionRotationDriver ActionRotation { get; set; }
}
