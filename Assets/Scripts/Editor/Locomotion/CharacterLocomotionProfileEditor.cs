using UnityEditor;
using UnityEngine;

/// <summary>LocomotionProfile 检视：从 AnimationProfile 烘焙 Stop/Pivot 根位移轨。</summary>
[CustomEditor(typeof(CharacterLocomotionProfile))]
public sealed class CharacterLocomotionProfileEditor : Editor
{
    CharacterAnimationProfile _bakeSource;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var profile = (CharacterLocomotionProfile)target;
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Root Motion Bake（方案 B）", EditorStyles.boldLabel);
        _bakeSource = (CharacterAnimationProfile)EditorGUILayout.ObjectField(
            "Animation Profile",
            _bakeSource,
            typeof(CharacterAnimationProfile),
            false);

        using (new EditorGUI.DisabledScope(_bakeSource == null))
        {
            if (GUILayout.Button("烘焙 StopL / StopR / PivotTurn 根位移"))
            {
                Undo.RecordObject(profile, "Bake Locomotion Root Motion");
                Bake(profile, _bakeSource, AnimationKey.StopL);
                Bake(profile, _bakeSource, AnimationKey.StopR);
                Bake(profile, _bakeSource, AnimationKey.PivotTurn);
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
                Debug.Log($"LocomotionRootMotion: 已烘焙到「{profile.name}」。", profile);
            }
        }

        EditorGUILayout.HelpBox(
            "需 Clip 含 Humanoid RootT/Q 或 Generic LocalPosition 曲线。烘焙后 Stop/Pivot 用采样位移，不再吃输入推移。",
            MessageType.Info);
    }

    static void Bake(CharacterLocomotionProfile profile, CharacterAnimationProfile source, AnimationKey key)
    {
        if (!source.TryGetClip(key, out AnimationClip clip) || clip == null)
        {
            profile.SetRootMotionTrack(key, LocomotionRootMotionTrack.Empty);
            Debug.LogWarning($"LocomotionRootMotion: AnimationProfile 未绑定 {key}，已清空轨道。", source);
            return;
        }

        LocomotionRootMotionTrack track = LocomotionRootMotionBaker.Bake(clip);
        profile.SetRootMotionTrack(key, track);
        if (!track.IsValid)
            Debug.LogWarning($"LocomotionRootMotion: {key}（{clip.name}）烘焙失败。", clip);
    }
}
