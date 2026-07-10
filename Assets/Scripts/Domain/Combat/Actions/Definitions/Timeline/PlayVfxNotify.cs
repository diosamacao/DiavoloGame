using System;
using UnityEngine;

/// <summary>
/// 播放 VFX 的区间窗口；窗口时长可拖拽，播放倍率 = 自然时长 / 窗口时长。
/// </summary>
[Serializable]
public class PlayVfxNotify : ActionNotifyState
{
    [SerializeField] GameObject prefab = null;
    [SerializeField] Vector3 localOffset = new(0f, 1f, 0.8f);
    [SerializeField] Vector3 localEulerAngles = Vector3.zero;
    [SerializeField] Vector3 localScale = Vector3.one;
    [Tooltip("勾选时实例挂到 attachPoint 下并使用 local 变换；否则在世界空间一次性生成。")]
    [SerializeField] bool parentToAttachPoint = true;
    [Tooltip("资源自然时长（秒）；对应倍率 1.0。为 0 时运行时按倍率 1 播放。")]
    [SerializeField] float naturalDurationSeconds;

    /// <summary>进入窗口时实例化的 VFX Prefab。</summary>
    public GameObject Prefab => prefab;

    /// <summary>相对挂点的局部位置偏移。</summary>
    public Vector3 LocalOffset => localOffset;

    /// <summary>相对挂点的局部欧拉角。</summary>
    public Vector3 LocalEulerAngles => localEulerAngles;

    /// <summary>实例化后的局部缩放。</summary>
    public Vector3 LocalScale => localScale;

    /// <summary>是否将实例作为挂点子物体生成。</summary>
    public bool ParentToAttachPoint => parentToAttachPoint;

    /// <summary>资源自然时长（秒）；拖拽窗口长度时不改此值。</summary>
    public float NaturalDurationSeconds => Mathf.Max(0f, naturalDurationSeconds);

    /// <summary>按采样率换算当前窗口占用秒数。</summary>
    public float GetWindowDurationSeconds(float sampleRate)
    {
        float rate = sampleRate > 0f ? sampleRate : 30f;
        int frameCount = Mathf.Max(1, EndFrame - StartFrame + 1);
        return frameCount / rate;
    }

    /// <summary>
    /// 播放倍率：自然时长 / 窗口时长；窗口拉长则减速，缩短则加速。
    /// 未配置自然时长时返回 1，兼容旧资产。
    /// </summary>
    public float GetPlaybackSpeed(float sampleRate)
    {
        if (NaturalDurationSeconds <= 0f)
            return 1f;

        return NaturalDurationSeconds / Mathf.Max(GetWindowDurationSeconds(sampleRate), 0.0001f);
    }

    /// <summary>窗口内相对起始帧的本地时间（秒），未乘播放倍率。</summary>
    public float GetLocalTimeSeconds(int frameIndex, float sampleRate)
    {
        float rate = sampleRate > 0f ? sampleRate : 30f;
        int localFrame = Mathf.Max(0, frameIndex - StartFrame);
        return localFrame / rate;
    }

    /// <summary>写入缓存的自然时长；编辑器从 Prefab 解析后调用。</summary>
    public void SetNaturalDurationSeconds(float seconds) =>
        naturalDurationSeconds = Mathf.Max(0f, seconds);
}
