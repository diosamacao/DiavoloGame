using UnityEngine;

/// <summary>播放、烘焙与 Dirty 检查共用的动作段范围：以播放 Clip 的逻辑帧为准，不拉伸 RM。</summary>
public static class ActionMotionBakeRange
{
    /// <summary>解析播放帧范围，RM 必须覆盖全部帧；失败时禁止截短动作或覆盖旧表。</summary>
    public static bool TryResolve(ActionAnimationSegment segment, int rootMotionFrames, int logicHz,
        out int start, out int end, out string error)
    {
        start = end = 0; error = null;
        if (logicHz != ActionSim.LogicHz || !segment.TryGetFrameRange(logicHz, out start, out end))
        { error = "需要有效动画段与固定 60Hz。"; return false; }
        if (end >= rootMotionFrames)
        { error = $"播放需要帧 {start}..{end}，RM 仅有 {rootMotionFrames} 帧；请核对配对与裁切，未拉伸或补帧。"; return false; }
        return true;
    }

    /// <summary>来源版本和裁切范围参与指纹；同长度裁切平移也必须重烘焙。</summary>
    public static string Fingerprint(AnimationClip clip, int start, int end) =>
        $"PlaybackRangeV2|{RootMotionBakeUtility.ComputeClipContentHash(clip)}|{start}:{end}";
}
