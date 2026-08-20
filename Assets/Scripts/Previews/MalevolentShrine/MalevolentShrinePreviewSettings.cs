using System;
using UnityEngine;

/// <summary>伏魔御厨子技能预览的可调参数。默认按 22m 可玩预览，而不是原作 200m。</summary>
[Serializable]
public sealed class MalevolentShrinePreviewSettings
{
    [Header("时间轴（秒）")]
    public float duration = 6.8f;
    public float mudraEnd = 0.45f;
    public float titleEnd = 0.8f;
    public float shrineRiseEnd = 1.7f;
    public float radiusPaintEnd = 2.2f;
    public float firstKaiEnd = 3.4f;
    public float stormEnd = 5.5f;
    public float timeScale = 1f;
    public bool loop = true;

    [Header("空间")]
    public float radius = 22f;
    public float shrineHeight = 10f;
    public bool groundOnly = true;

    [Header("斩击")]
    public float kaiInterval = 0.13f;
    public float hachiInterval = 0.16f;
    public float slashLife = 0.16f;
    public float kaiSlashWidth = 0.34f;
    public float hachiSlashWidth = 0.24f;
    public int kaiPerPulse = 2;
    public int hachiPerTarget = 3;

    [Header("场景")]
    public int buildingCount = 10;
    public int targetCount = 3;
    public bool showTitleCard = true;

    public static MalevolentShrinePreviewSettings CreateDefault()
    {
        return new MalevolentShrinePreviewSettings();
    }
}
