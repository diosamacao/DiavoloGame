using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterAnimationController))]
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(CharacterRootMotionDriver))]
/// <summary>驱动招式播放；ComboLink 窗口内按输入在普攻链与闪避间衔接。</summary>
public class ActionRuntimeController : MonoBehaviour, IActionRuntime
{
    [SerializeField] CharacterAnimationController animationController = null!;
    [SerializeField] PlayerActionSet actionSet = null!;

    CharacterController _motor = null!;
    CharacterRootMotionDriver _rootMotion = null!;
    IActionComboInput _comboInput;
    Action _onDodgeStarted;
    ActionDefinition _current;
    bool _isPlaying;
    float _elapsed;
    int _attackIndex;

    public bool IsPlaying => _isPlaying;
    public bool CanCancelByMovement =>
        _isPlaying && _current != null && _current.IsInMovementCancelWindow(_elapsed);
    public ActionDefinition CurrentAction => _current;
    public int AttackIndex => _attackIndex;

    void Awake()
    {
        if (animationController == null)
            animationController = GetComponent<CharacterAnimationController>();

        _motor = GetComponent<CharacterController>();
        _rootMotion = GetComponent<CharacterRootMotionDriver>();
    }

    public void BindComboInput(IActionComboInput comboInput) => _comboInput = comboInput;

    public void BindDodgeFacing(Action onDodgeStarted) => _onDodgeStarted = onDodgeStarted;

    public bool TryStartAttackChain()
    {
        ActionDefinition[] chain = GetAttackChain();
        if (chain.Length == 0)
        {
            Debug.LogWarning("ActionRuntimeController: attackChain 为空。", this);
            return false;
        }

        if (_isPlaying)
            return false;

        _attackIndex = 0;
        return BeginActionIfValid(chain[0]);
    }

    public bool TryStartDodge()
    {
        ActionDefinition dodge = GetDodge();
        if (dodge == null)
        {
            Debug.LogWarning("ActionRuntimeController: dodge 未分配。", this);
            return false;
        }

        if (_isPlaying)
            return false;

        _attackIndex = 0;
        _onDodgeStarted?.Invoke();
        return BeginActionIfValid(dodge);
    }

    public void Tick(float deltaTime)
    {
        if (!_isPlaying || _current == null)
            return;

        _elapsed += deltaTime;
        ApplyScriptedDisplacement(deltaTime);

        if (TryResolveComboLink())
            return;

        if (_elapsed >= _current.DurationSeconds)
            Stop();
    }

    public void Stop()
    {
        _isPlaying = false;
        _current = null;
        _elapsed = 0f;
        _attackIndex = 0;
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

    bool BeginActionIfValid(ActionDefinition action)
    {
        if (action == null || action.AnimationClip == null || animationController == null)
            return false;

        BeginAction(action);
        return true;
    }

    /// <summary>ComboLink 内按缓冲输入衔接：Dodge 优先于 Attack。</summary>
    bool TryResolveComboLink()
    {
        if (_comboInput == null || _current == null || !_current.IsInComboLinkWindow(_elapsed))
            return false;

        if (_comboInput.HasBuffer(InputSlot.Dodge)
            && _current.AllowsComboInput(InputSlot.Dodge)
            && TryLinkDodge())
            return true;

        if (_comboInput.HasBuffer(InputSlot.Attack)
            && _current.AllowsComboInput(InputSlot.Attack)
            && TryLinkAttack())
            return true;

        return false;
    }

    bool TryLinkDodge()
    {
        ActionDefinition dodge = GetDodge();
        if (dodge == null || dodge.AnimationClip == null)
            return false;

        _comboInput.TryConsumeBuffer(InputSlot.Dodge);
        _comboInput.TryConsumeBuffer(InputSlot.Attack);
        _attackIndex = 0;
        _onDodgeStarted?.Invoke();
        BeginAction(dodge);
        return true;
    }

    bool TryLinkAttack()
    {
        ActionDefinition[] chain = GetAttackChain();
        if (chain.Length == 0)
            return false;

        _comboInput.TryConsumeBuffer(InputSlot.Attack);
        _comboInput.TryConsumeBuffer(InputSlot.Dodge);

        if (_current.ActionType == CombatActionType.Dodge)
            _attackIndex = 0;
        else if (_attackIndex < chain.Length - 1)
            _attackIndex++;
        else
            _attackIndex = 0;

        ActionDefinition next = chain[_attackIndex];
        if (next == null || next.AnimationClip == null)
            return false;

        BeginAction(next);
        return true;
    }

    ActionDefinition[] GetAttackChain() =>
        actionSet != null && actionSet.AttackChain != null ? actionSet.AttackChain : Array.Empty<ActionDefinition>();

    ActionDefinition GetDodge() => actionSet != null ? actionSet.Dodge : null;

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
