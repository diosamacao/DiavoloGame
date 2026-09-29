using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>单条 InPlace↔RootMotion 匹配结果。</summary>
public readonly struct MotionClipBakePair
{
    public MotionClipBakePair(
        AnimationClip inplaceClip,
        AnimationClip rootMotionClip,
        string stem,
        int priority,
        string inplacePath,
        string rootMotionPath)
    {
        InplaceClip = inplaceClip;
        RootMotionClip = rootMotionClip;
        Stem = stem;
        Priority = priority;
        InplacePath = inplacePath;
        RootMotionPath = rootMotionPath;
    }

    public AnimationClip InplaceClip { get; }
    public AnimationClip RootMotionClip { get; }
    public string Stem { get; }
    public int Priority { get; }
    public string InplacePath { get; }
    public string RootMotionPath { get; }
}
