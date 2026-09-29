using System.Text;
using UnityEngine;

/// <summary>移动根位移来源指纹；包含启用键、Clip 内容和固定频率，不参与运行时采样。</summary>
public static class LocomotionRootMotionFingerprint
{
    /// <summary>计算当前输入；相同长度但内容不同的 Clip 仍能判定过期。</summary>
    public static string Compute(CharacterLocomotionProfile profile)
    {
        var source = new StringBuilder("LocomotionRootMotion:1:60");
        foreach (AnimationKey key in new[] { AnimationKey.StartEnd, AnimationKey.StopL, AnimationKey.StopR, AnimationKey.PivotTurn })
        {
            source.Append('|').Append((int)key).Append(':').Append(profile.IsRootMotionEnabled(key));
            if (!profile.IsRootMotionEnabled(key)) continue;
            source.Append(':').Append(profile.TryGetClip(key, out var clip)
                ? RootMotionBakeUtility.ComputeClipContentHash(clip) : "missing");
        }
        return Hash128.Compute(source.ToString()).ToString();
    }

    /// <summary>未记录来源或输入发生改变时需要作者重新确认烘焙。</summary>
    public static bool IsDirty(CharacterLocomotionProfile profile) =>
        profile != null && (profile.StopUseRootMotion || profile.PivotUseRootMotion)
        && profile.EditorRootMotionSourceFingerprint != Compute(profile);
}
