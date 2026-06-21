using UnityEngine;

/// <summary>Root Motion 桥接器；把模型 Animator 的 deltaPosition 写回角色 CharacterController。</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public class CharacterRootMotionDriver : MonoBehaviour
{
    [SerializeField] Animator animator;

    CharacterController _motor;
    CharacterRootMotionReceiver _receiver;

    public bool IsActive => _receiver != null && _receiver.IsActive;

    void Awake()
    {
        _motor = GetComponent<CharacterController>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        EnsureReceiver();
        SetActive(false);
    }

    /// <summary>绑定模型 Animator，支持 PlayerController 实例化模型后显式装配。</summary>
    public void BindAnimator(Animator targetAnimator)
    {
        animator = targetAnimator;
        EnsureReceiver();
        SetActive(false);
    }

    /// <summary>启停 Root Motion 位移接管。</summary>
    public void SetActive(bool active)
    {
        EnsureReceiver();
        _receiver?.SetActive(active);
    }

    void EnsureReceiver()
    {
        if (animator == null || _motor == null)
            return;

        _receiver = animator.GetComponent<CharacterRootMotionReceiver>();
        if (_receiver == null)
            _receiver = animator.gameObject.AddComponent<CharacterRootMotionReceiver>();

        _receiver.Bind(_motor, animator);
    }
}

[DisallowMultipleComponent]
sealed class CharacterRootMotionReceiver : MonoBehaviour
{
    CharacterController _motor;
    Animator _animator;
    Vector3 _initialLocalPosition;
    Quaternion _initialLocalRotation;
    bool _active;

    public bool IsActive => _active;

    public void Bind(CharacterController motor, Animator animator)
    {
        _motor = motor;
        _animator = animator;
        _initialLocalPosition = transform.localPosition;
        _initialLocalRotation = transform.localRotation;
    }

    public void SetActive(bool active)
    {
        _active = active;

        if (_animator != null)
            _animator.applyRootMotion = active;

        if (!active)
            ResetLocalPose();
    }

    void OnAnimatorMove()
    {
        if (!_active || _motor == null || _animator == null)
            return;

        Vector3 delta = _animator.deltaPosition;
        delta.y = 0f;
        _motor.Move(delta);

        if (_animator.deltaRotation != Quaternion.identity)
            _motor.transform.rotation *= _animator.deltaRotation;

        ResetLocalPose();
    }

    void ResetLocalPose()
    {
        transform.localPosition = _initialLocalPosition;
        transform.localRotation = _initialLocalRotation;
    }
}
