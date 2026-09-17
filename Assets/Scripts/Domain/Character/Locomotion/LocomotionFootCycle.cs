using System;
using UnityEngine;

/// <summary>脚步相位真源：按 PhaseFrame 与周期帧采样落脚标记。</summary>
public sealed class LocomotionFootCycle
{
    FootSide _lastPlanted = FootSide.Right;
    bool _hasPlantRecord;
    bool _frozen;
    int _cycleIndex = -1;
    int _firedMask;
    FootPlantMarker[] _activeMarkers = Array.Empty<FootPlantMarker>();

    /// <summary>最近一次落地脚；尚无记录时为右脚。</summary>
    public FootSide LastPlanted => _hasPlantRecord ? _lastPlanted : FootSide.Right;

    /// <summary>是否已有真实落脚记录（不含默认右脚）。</summary>
    public bool HasPlantRecord => _hasPlantRecord;

    /// <summary>本帧新触发的落脚；无则为 null。</summary>
    public FootSide? PlantedThisFrame { get; private set; }

    /// <summary>绑定当前 gait Clip 的落脚表；切换 Clip 时重置周期去重。</summary>
    public void SetMarkers(FootPlantMarker[] markers)
    {
        FootPlantMarker[] next = markers ?? Array.Empty<FootPlantMarker>();
        if (ReferenceEquals(next, _activeMarkers))
            return;

        _activeMarkers = next;
        _cycleIndex = -1;
        _firedMask = 0;
    }

    /// <summary>动画键切换时清除当前周期去重，但保留最近落脚。</summary>
    public void ResetCycle()
    {
        _cycleIndex = -1;
        _firedMask = 0;
        PlantedThisFrame = null;
    }

    /// <summary>冻结采样（Stop / Pivot / 离开 Locomotion）；保留 LastPlanted。</summary>
    public void Freeze()
    {
        _frozen = true;
        PlantedThisFrame = null;
    }

    /// <summary>恢复采样；不清除 LastPlanted。</summary>
    public void Unfreeze() => _frozen = false;

    /// <summary>按整数相位帧推进；越过周期内标记时更新最近落脚。</summary>
    public void Tick(int phaseFrame, int durationFrames, bool loop)
    {
        PlantedThisFrame = null;
        if (_frozen || _activeMarkers.Length == 0)
            return;
        if (durationFrames <= 0)
            throw new ArgumentOutOfRangeException(nameof(durationFrames));

        int frame = Math.Max(0, phaseFrame);
        int cycle = loop ? frame / durationFrames : 0;
        int frameInCycle = loop ? frame % durationFrames : Math.Min(frame, durationFrames - 1);

        if (cycle != _cycleIndex)
        {
            _cycleIndex = cycle;
            _firedMask = 0;
        }

        for (int i = 0; i < _activeMarkers.Length; i++)
        {
            int bit = 1 << i;
            if ((_firedMask & bit) != 0)
                continue;

            FootPlantMarker marker = _activeMarkers[i];
            if (marker.Frame < 0 || marker.Frame >= durationFrames)
                throw new InvalidOperationException(
                    $"FootPlantMarker frame {marker.Frame} 超出周期 0..{durationFrames - 1}。");
            if (frameInCycle < marker.Frame)
                continue;

            _firedMask |= bit;
            _lastPlanted = marker.Foot;
            _hasPlantRecord = true;
            PlantedThisFrame = marker.Foot;
            // 同帧只派发第一只越过的脚，避免异常配置连发。
            break;
        }
    }

    /// <summary>进入 Stop 前冻结，并保证至少有默认右脚可选。</summary>
    public FootSide CaptureForStop()
    {
        Freeze();
        return LastPlanted;
    }

    /// <summary>纠偏恢复落脚记录；不恢复周期去重掩码（Seek 后下一 Tick 重新采样）。</summary>
    public void Restore(FootSide lastPlanted, bool hasPlantRecord, bool frozen)
    {
        _lastPlanted = lastPlanted;
        _hasPlantRecord = hasPlantRecord;
        _frozen = frozen;
        PlantedThisFrame = null;
        _cycleIndex = -1;
        _firedMask = 0;
    }
}
