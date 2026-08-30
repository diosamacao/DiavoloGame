using UnityEngine;

/// <summary>Camera 轨的统一反馈入口；当前负责段入 Impulse，FOV/Dolly 由 Director 逐帧求值。</summary>
[DisallowMultipleComponent]
public sealed class CameraFeedback : AppControllerBase
{
    CameraShakeController _shake;

    void Awake()
    {
        _shake = GetComponent<CameraShakeController>();
    }

    /// <summary>首次进入 Shot 时播放可选震屏；不写动作或命中状态。</summary>
    public void PlayShotEnter(CameraShotNotifyState shot, Vector3 worldDirection)
    {
        if (shot == null || shot.ImpulseOnEnter == null)
            return;

        if (_shake == null)
            _shake = GetComponent<CameraShakeController>();
        _shake?.Play(shot.ImpulseOnEnter, worldDirection);
    }
}
