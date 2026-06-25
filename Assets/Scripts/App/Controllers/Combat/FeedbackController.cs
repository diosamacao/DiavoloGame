using UnityEngine;

/// <summary>场景级战斗反馈控制器；统一承载卡肉等全局反馈，避免角色 Prefab 上重复订阅。</summary>
[DisallowMultipleComponent]
public class FeedbackController : MonoBehaviour
{
    HitStopController _hitStop;

    void Awake()
    {
        EnsureHitStopController();
    }

    /// <summary>确保卡肉控制器只由场景反馈系统托管。</summary>
    void EnsureHitStopController()
    {
        _hitStop = GetComponent<HitStopController>();
        if (_hitStop == null)
            _hitStop = gameObject.AddComponent<HitStopController>();
    }
}
