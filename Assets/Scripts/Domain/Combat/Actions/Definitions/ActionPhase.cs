using System;
using UnityEngine;

/// <summary>动作阶段帧区间：Startup / Active / Recovery 与无敌、霸体覆盖标记。</summary>
[Serializable]
public class ActionPhase
{
    [SerializeField] ActionPhaseKind kind = ActionPhaseKind.Startup;
    [SerializeField] int startFrame;
    [SerializeField] int endFrame;
    [SerializeField] bool interruptible = true;
    [SerializeField] string interruptActionId = string.Empty;

    public ActionPhaseKind Kind => kind;
    public int StartFrame => startFrame;
    public int EndFrame => endFrame;
    public bool Interruptible => interruptible;
    public string InterruptActionId => interruptActionId;

    /// <summary>指定逻辑帧是否落在此阶段区间内。</summary>
    public bool IsActiveAtFrame(int frame) => frame >= startFrame && frame <= endFrame;

    /// <summary>将起止帧限制在 [0, totalFrames - 1]。</summary>
    public void ClampToTotalFrames(int totalFrames)
    {
        int maxFrame = Mathf.Max(0, totalFrames - 1);
        startFrame = Mathf.Clamp(startFrame, 0, maxFrame);
        endFrame = Mathf.Clamp(endFrame, startFrame, maxFrame);
    }
}
