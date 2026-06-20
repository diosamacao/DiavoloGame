using Cinemachine;
using UnityEngine;

/// <summary>Cinemachine Impulse 震动参数，可被 Profile 或 Controller 内联配置复用。</summary>
[System.Serializable]
public struct CameraShakeSettings
{
    [Tooltip("Impulse 速度向量长度，决定震动强度。")]
    [SerializeField] float force;

    [Tooltip("Impulse 持续时间（秒）。")]
    [SerializeField] float duration;

    [Tooltip("沿命中水平方向的前向权重；侧向与上向见 directionSide / directionUp。")]
    [SerializeField] float directionForward;

    [Tooltip("相对命中方向的侧向权重。")]
    [SerializeField] float directionSide;

    [Tooltip("垂直方向权重。")]
    [SerializeField] float directionUp;

    [Tooltip("Impulse 信号振幅增益。")]
    [SerializeField] float amplitudeGain;

    [Tooltip("Impulse 波形；Bump 适合短促命中反馈。")]
    [SerializeField] CinemachineImpulseDefinition.ImpulseShapes impulseShape;

    [Tooltip("传播速度（m/s）；ACT 命中反馈建议设大值以即时响应。")]
    [SerializeField] float propagationSpeed;

    [Tooltip("超出该半径后信号衰减；第三人称一般设较大值。")]
    [SerializeField] float dissipationDistance;

    /// <summary>默认轻击：短促、低幅。</summary>
    public static CameraShakeSettings DefaultLight => new()
    {
        force = 1f,
        duration = 0.12f,
        directionForward = 0.55f,
        directionSide = 0.25f,
        directionUp = 0.2f,
        amplitudeGain = 1f,
        impulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump,
        propagationSpeed = 1000f,
        dissipationDistance = 100f,
    };

    /// <summary>默认重击：更长、更强。</summary>
    public static CameraShakeSettings DefaultHeavy => new()
    {
        force = 1.25f,
        duration = 0.18f,
        directionForward = 0.6f,
        directionSide = 0.2f,
        directionUp = 0.35f,
        amplitudeGain = 1.15f,
        impulseShape = CinemachineImpulseDefinition.ImpulseShapes.Recoil,
        propagationSpeed = 1000f,
        dissipationDistance = 100f,
    };

    public float Force => force;
    public float Duration => duration;

    /// <summary>将参数写入 Impulse Source 的 Definition。</summary>
    public void ApplyTo(CinemachineImpulseSource source)
    {
        if (source == null)
            return;

        CinemachineImpulseDefinition definition = source.m_ImpulseDefinition;
        definition.m_ImpulseDuration = Mathf.Max(0.01f, duration);
        definition.m_AmplitudeGain = amplitudeGain;
        definition.m_ImpulseShape = impulseShape;
        definition.m_ImpulseChannel = 1;
        // Uniform：命中反馈不受 Impulse Source 物体与相机距离影响。
        definition.m_ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
        definition.m_PropagationSpeed = propagationSpeed;
        definition.m_DissipationDistance = dissipationDistance;
    }

    /// <summary>根据世界空间命中方向构建 Impulse 速度向量。</summary>
    public Vector3 BuildImpulseVelocity(Vector3 worldHitDirection)
    {
        Vector3 forward = worldHitDirection;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;
        else
            forward.Normalize();

        Vector3 side = Vector3.Cross(Vector3.up, forward);
        Vector3 velocity =
            forward * directionForward +
            side * directionSide +
            Vector3.up * directionUp;

        if (velocity.sqrMagnitude < 0.0001f)
            velocity = Vector3.down;

        return velocity.normalized * force;
    }
}
