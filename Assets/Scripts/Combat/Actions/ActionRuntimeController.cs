using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterAnimationController))]
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(CharacterRootMotionDriver))]
/// <summary>驱动招式播放、连招衔接、脚本位移与取消窗口查询。</summary>
public class ActionRuntimeController : MonoBehaviour, IActionRuntime
{
    [SerializeField] CharacterAnimationController animationController = null!;
    [SerializeField] ActionDefinition defaultAttack = null!;
    [SerializeField] ActionDefinition defaultDodge = null!;
    
    CharacterController _motor = null!;
    CharacterRootMotionDriver _rootMotion = null!;
    IActionComboInput _comboInput;
    ActionDefinition _current;
    bool _isPlaying;
    float _elapsed;

    public bool IsPlaying => _isPlaying;
    public bool CanCancelByMovement =>
        _isPlaying && _current != null && _current.IsInMovementCancelWindow(_elapsed);
    public ActionDefinition CurrentAction => _current;
    public ActionDefinition DefaultAttack => defaultAttack;
    public ActionDefinition DefaultDodge => defaultDodge;

    void Awake()
    {
        if (animationController == null)
            animationController = GetComponent<CharacterAnimationController>();

        _motor = GetComponent<CharacterController>();
        _rootMotion = GetComponent<CharacterRootMotionDriver>();
    }

    public void BindComboInput(IActionComboInput comboInput) => _comboInput = comboInput;

    public bool TryStartDefaultAction() => TryPlay(defaultAttack);

    public bool TryStartDefaultDodge()
    {
        if (defaultDodge == null)
        {
            Debug.LogWarning("ActionRuntimeController: defaultDodge 未分配。", this);
            return false;
        }

        return TryPlay(defaultDodge);
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
        ApplyScriptedDisplacement(deltaTime);

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
        _rootMotion?.SetActive(false);
    }

    void BeginAction(ActionDefinition action)
    {
        _current = action;
        _isPlaying = true;
        _elapsed = 0f;
        _rootMotion?.SetActive(action.UseRootMotion);
        animationController.PlayClip(action.AnimationClip, action.CrossFadeDuration);
    }

    bool TryConsumeBufferedCombo()
    {
        if (_comboInput == null || !_comboInput.HasBufferedAttack || !_current.IsInComboLinkWindow(_elapsed))
            return false;

        ActionDefinition next = _current.NextAction;
        if (next == null || next.AnimationClip == null || animationController == null)
            return false;

        _comboInput.ConsumeBufferedAttack();
        BeginAction(next);
        return true;
    }

    /// <summary>沿面朝方向脚本位移；距离为负时向后（反 forward）移动。</summary>
    void ApplyScriptedDisplacement(float deltaTime)
    {
        if (_motor == null || !_current.HasScriptedDisplacement || !_current.IsInDisplacementWindow(_elapsed))
            return;

        Vector3 forward = transform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
            return;

        forward.Normalize();
        float signedSpeed = _current.DisplacementSpeed;
        _motor.Move(forward * (signedSpeed * deltaTime));
    }
}
