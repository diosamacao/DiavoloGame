using UnityEngine;

/// <summary>场景级战斗反馈控制器；统一承载卡肉与受击 Cue，避免角色 Prefab 上重复订阅。</summary>
[DisallowMultipleComponent]
public class FeedbackController : AppControllerBase
{
    HitStopController _hitStop;
    HitImpactController _hitImpact;

    void Awake()
    {
        EnsureHitStopController();
        EnsureHitImpactController();
    }

    /// <summary>确保卡肉控制器只由场景反馈系统托管。</summary>
    void EnsureHitStopController()
    {
        _hitStop = GetComponent<HitStopController>();
        if (_hitStop == null)
            _hitStop = gameObject.AddComponent<HitStopController>();
    }

    /// <summary>确保受击 VFX/SFX 控制器挂在同一反馈宿主上。</summary>
    void EnsureHitImpactController()
    {
        _hitImpact = GetComponent<HitImpactController>();
        if (_hitImpact == null)
            _hitImpact = gameObject.AddComponent<HitImpactController>();
    }
}
