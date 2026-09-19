using UnityEngine;

/// <summary>
/// 未烘焙招式的临时 Root Motion 桥：Animator delta 写入 CharacterMotor（MotorSim 权威）。
/// 表就绪招式由表现桥关闭本驱动，禁止与查表位移双加。
/// </summary>
public sealed class CharacterRootMotionDriver
{
    readonly CharacterMotor _motor;
    readonly Animator animator;
    CharacterRootMotionReceiver _receiver;

    public bool IsActive => _receiver != null && _receiver.IsActive;

    /// <summary>创建 Root Motion 服务；仅在 Animator 所在物体挂一个 Unity 消息桥接器。</summary>
    public CharacterRootMotionDriver(CharacterMotor motor, Animator targetAnimator)
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
    CharacterMotor _motor;
    Animator _animator;
    Vector3 _initialLocalPosition;
    Quaternion _initialLocalRotation;
    bool _active;

    public bool IsActive => _active;

    public void Bind(CharacterMotor motor, Animator animator)
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

    /// <summary>承接 Playable Manual Evaluate 产生的 delta；切段 Seek 期间由表现桥临时关闭本组件。</summary>
    void OnAnimatorMove()
    {
        if (!_active || _motor == null || _animator == null)
            return;

        // 只取水平 delta，竖直由 Motor 重力管
        Vector3 delta = _animator.deltaPosition;
        delta.y = 0f;
        // 逻辑步长未知于此回调；速度估算用 0 跳过
        _motor.MovePlanar(delta, 0f);

        // 烘焙 yaw 写入 Motor，避免 Animator 把模型根转走
        if (_animator.deltaRotation != Quaternion.identity)
        {
            float yaw = _animator.deltaRotation.eulerAngles.y;
            if (yaw > 180f)
                yaw -= 360f;
            _motor.ApplyYawDegrees(yaw);
        }

        // 吃完 delta 后把 Animator 局部 Pose 掰回绑定初值
        ResetLocalPose();
    }

    void ResetLocalPose()
    {
        transform.localPosition = _initialLocalPosition;
        transform.localRotation = _initialLocalRotation;
    }
}
