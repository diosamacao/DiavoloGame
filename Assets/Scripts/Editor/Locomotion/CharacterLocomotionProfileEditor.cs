using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>移动作者面板：映射、可调时序与生成轨道分开，烘焙先验证再提交。</summary>
[CustomEditor(typeof(CharacterLocomotionProfile))]
public sealed class CharacterLocomotionProfileEditor : Editor
{
    bool advanced;
    bool footsteps;
    bool generated;
    string message;
    public override void OnInspectorGUI()
    {
        var profile = (CharacterLocomotionProfile)target;
        serializedObject.Update();
        EditorGUILayout.LabelField("动画映射", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Clip 只在此表维护。多个方向槽使用同一 Key 时会共享 Clip；在高级设置查看槽位映射。", MessageType.Info);
        Field("animationEntries");
        Field("defaultCrossFadeDuration");
        Field("gaitPolicy");
        Field("facingMode");
        DrawTimings(profile);
        Field("stopUseRootMotion");
        Field("pivotUseRootMotion");
        if (serializedObject.FindProperty("stopUseRootMotion").boolValue || serializedObject.FindProperty("pivotUseRootMotion").boolValue)
            Field("rootMotionPositionScale");
        footsteps = EditorGUILayout.Foldout(footsteps, "落脚点与脚步", true);
        if (footsteps)
            foreach (string name in new[] { "walkFootPlants", "runFootPlants", "sprintFootPlants", "startFootPlants", "footstepLeft", "footstepRight", "footstepVolume" }) Field(name);
        advanced = EditorGUILayout.Foldout(advanced, "高级：选片与移动调节", true);
        if (advanced)
            foreach (string name in new[] { "animSet", "idleInputThreshold", "stopMinSpeedFactor", "pivotAngleDegrees", "gaitInputGapGraceFrames", "cardinalEpsilon", "cardinalMinDwellFrames", "interruptFadeDuration", "sprintLean" }) Field(name);
        generated = EditorGUILayout.Foldout(generated, "生成的根位移数据（只读）", true);
        if (generated)
            using (new EditorGUI.DisabledScope(true))
                foreach (string name in new[] { "startEndRootMotion", "stopLRootMotion", "stopRRootMotion", "pivotTurnRootMotion" }) Field(name);
        serializedObject.ApplyModifiedProperties();
        if (GUILayout.Button("更新 Timing（保留退出 / 交接帧）"))
            Run(() => { Undo.RecordObject(profile, "Bake Timing"); return $"更新 {LocomotionTimingBaker.Bake(profile)} 个 Timing。"; });
        bool rootMotionDirty = LocomotionRootMotionFingerprint.IsDirty(profile);
        if (rootMotionDirty) EditorGUILayout.HelpBox("根位移来源尚未记录或 Clip 已变化，请确认后重新烘焙。", MessageType.Warning);
        using (new EditorGUI.DisabledScope(!rootMotionDirty))
        if (GUILayout.Button("烘焙已启用的根位移…")
            && EditorUtility.DisplayDialog("移动位移烘焙", AssetDatabase.GetAssetPath(profile) + "\n引用本移动配置的所有角色都会受到影响；将替换采样轨道。", "烘焙", "取消"))
            Run(() => BakeRootMotion(profile));
        if (GUILayout.Button("只读校验")) message = profile.Validate(profile) ? "校验通过。" : "存在错误，见 Console。";
        if (message != null) EditorGUILayout.HelpBox(message, MessageType.Info);
    }

    void Field(string name) => EditorGUILayout.PropertyField(serializedObject.FindProperty(name), true);
    void DrawTimings(CharacterLocomotionProfile profile)
    {
        EditorGUILayout.LabelField("60Hz 时序", EditorStyles.boldLabel);
        SerializedProperty timings = serializedObject.FindProperty("clipTimings");
        for (int i = 0; i < timings.arraySize; i++)
        {
            SerializedProperty item = timings.GetArrayElementAtIndex(i);
            AnimationKey key = (AnimationKey)item.FindPropertyRelative("key").intValue;
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField(key.ToString(), EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.PropertyField(item.FindPropertyRelative("durationFrames"));
                    EditorGUILayout.PropertyField(item.FindPropertyRelative("loop"));
                }
                EditorGUILayout.PropertyField(item.FindPropertyRelative("exitFrame"));
                EditorGUILayout.PropertyField(item.FindPropertyRelative("handoffFrame"));
                if (profile.TryGetClip(key, out AnimationClip clip)
                    && (Mathf.CeilToInt(clip.length * ActionSim.LogicHz) != item.FindPropertyRelative("durationFrames").intValue
                        || clip.isLooping != item.FindPropertyRelative("loop").boolValue))
                    EditorGUILayout.HelpBox("Clip 时长或循环属性已改变，请更新 Timing。", MessageType.Warning);
            }
        }
    }

    void Run(Func<string> operation)
    {
        try { message = operation(); EditorUtility.SetDirty(target); AssetDatabase.SaveAssetIfDirty(target); }
        catch (Exception e) { message = e.Message; Debug.LogError(e.Message, target); }
    }

    /// <summary>先收集全部有效轨道，任何失败均不覆盖原有轨道。</summary>
    public static string BakeRootMotion(CharacterLocomotionProfile profile)
    {
        if (!LocomotionRootMotionFingerprint.IsDirty(profile)) return "来源未变化，无需重复烘焙。";
        string fingerprint = LocomotionRootMotionFingerprint.Compute(profile);
        var tracks = new Dictionary<AnimationKey, LocomotionRootMotionTrack>();
        foreach (AnimationKey key in new[] { AnimationKey.StartEnd, AnimationKey.StopL, AnimationKey.StopR, AnimationKey.PivotTurn })
        {
            // 未使用的可选 Key 可以没有 Clip；完整性由 Profile 的槽位校验决定。
            if (!profile.IsRootMotionEnabled(key) || !profile.TryGetClip(key, out AnimationClip clip)) continue;
            LocomotionRootMotionTrack track = LocomotionRootMotionBaker.Bake(clip);
            if (!track.IsValid) throw new InvalidOperationException($"{key}: 烘焙失败，保留全部原始轨道。");
            tracks.Add(key, track);
        }
        Undo.RecordObject(profile, "Bake Locomotion Root Motion");
        foreach (var pair in tracks) profile.SetRootMotionTrack(pair.Key, pair.Value);
        profile.EditorSetRootMotionSourceFingerprint(fingerprint);
        return $"更新 {tracks.Count} 条根位移轨。";
    }
}
