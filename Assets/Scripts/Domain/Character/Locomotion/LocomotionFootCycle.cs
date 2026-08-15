using System;
using UnityEngine;

/// <summary>脚步相位真源：按 Clip 归一化时间采样落脚标记，驱动脚步声与急停选脚。</summary>
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

    /// <summary>冻结采样（Stop / Pivot / 离开 Locomotion）；保留 LastPlanted。</summary>
    public void Freeze()
    {
        _frozen = true;
        PlantedThisFrame = null;
    }

    /// <summary>恢复采样；不清除 LastPlanted。</summary>
    public void Unfreeze() => _frozen = false;

    /// <summary>按归一化时间推进；越过标记时更新 LastPlanted 并置 PlantedThisFrame。</summary>
    public void Tick(float normalizedTime)
    {
        PlantedThisFrame = null;
        if (_frozen || _activeMarkers.Length == 0)
            return;

        // 循环 Clip 的周期索引；非循环时 cycle 恒为 0。
        float wrapped = normalizedTime - Mathf.Floor(normalizedTime);
        int cycle = Mathf.FloorToInt(normalizedTime);
        if (normalizedTime < 0f)
        {
            wrapped = 0f;
            cycle = 0;
        }

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
            if (wrapped + 0.0001f < marker.NormalizedTime)
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
