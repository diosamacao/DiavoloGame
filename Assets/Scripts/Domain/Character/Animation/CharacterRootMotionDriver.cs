using UnityEngine;

/// <summary>Root Motion 临时桥；把 Animator delta 写回 CharacterController，L2 将删除该非纯模拟路径。</summary>
public sealed class CharacterRootMotionDriver
{
    readonly CharacterController _motor;
    readonly Animator animator;
    CharacterRootMotionReceiver _receiver;

    public bool IsActive => _receiver != null && _receiver.IsActive;

    /// <summary>创建 Root Motion 服务；仅在 Animator 所在物体挂一个 Unity 消息桥接器。</summary>
    public CharacterRootMotionDriver(CharacterController motor, Animator targetAnimator)
    {
        _motor = motor;
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
