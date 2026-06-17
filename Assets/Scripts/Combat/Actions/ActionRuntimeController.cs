using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterAnimationController))]
[RequireComponent(typeof(CharacterController))]
public class ActionRuntimeController : MonoBehaviour, IActionRuntime
{
    [SerializeField] CharacterAnimationController animationController;
    [SerializeField] ActionDefinition defaultAttack;

    CharacterController _motor;
    ActionDefinition _current;
    bool _isPlaying;
    float _elapsed;
    bool _attackBuffered;

    public bool IsPlaying => _isPlaying;
    public ActionDefinition CurrentAction => _current;
    public ActionDefinition DefaultAttack => defaultAttack;

    void Awake()
    {
        if (animationController == null)
            animationController = GetComponent<CharacterAnimationController>();

        _motor = GetComponent<CharacterController>();
    }

    public bool TryStartDefaultAction() => TryPlay(defaultAttack);

    public void BufferAttackInput()
    {
        if (_isPlaying)
            _attackBuffered = true;
    }

    public bool TryPlay(ActionDefinition action)
    {
        if (_isPlaying || action == null || action.AnimationClip == null || animationController == null)
            return false;

        BeginAction(action);
        return true;
    }

    public void Tick(float deltaTime)
    {
        if (!_isPlaying || _current == null)
            return;

        _elapsed += deltaTime;
        ApplyDisplacement(deltaTime);

        if (TryConsumeBufferedCombo())
            return;

        if (_elapsed >= _current.DurationSeconds)
            Stop();
    }

    public void Stop()
    {
        _isPlaying = false;
        _current = null;
        _elapsed = 0f;
        _attackBuffered = false;
    }

    void BeginAction(ActionDefinition action)
    {
        _current = action;
        _isPlaying = true;
        _elapsed = 0f;
        _attackBuffered = false;
        animationController.PlayClip(action.AnimationClip, action.CrossFadeDuration);
    }

    bool TryConsumeBufferedCombo()
    {
        if (!_attackBuffered || !_current.IsInComboLinkWindow(_elapsed))
            return false;

        ActionDefinition next = _current.NextAction;
        if (next == null || next.AnimationClip == null || animationController == null)
            return false;

        BeginAction(next);
        return true;
    }

    void ApplyDisplacement(float deltaTime)
    {
        if (_motor == null || !_current.HasDisplacement || !_current.IsInDisplacementWindow(_elapsed))
            return;

        Vector3 forward = transform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
            return;

        forward.Normalize();
        _motor.Move(forward * (_current.DisplacementSpeed * deltaTime));
    }
}
