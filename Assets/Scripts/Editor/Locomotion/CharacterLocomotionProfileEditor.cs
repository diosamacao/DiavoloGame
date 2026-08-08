using UnityEditor;
using UnityEngine;

/// <summary>LocomotionProfile 检视：用内嵌 AnimationProfile 烘焙 StartEnd/Stop/Pivot 根位移轨。</summary>
[CustomEditor(typeof(CharacterLocomotionProfile))]
public sealed class CharacterLocomotionProfileEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var profile = (CharacterLocomotionProfile)target;
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Root Motion Bake", EditorStyles.boldLabel);

        CharacterAnimationProfile bakeSource = profile.AnimationProfile;
        using (new EditorGUI.DisabledScope(bakeSource == null))
        {
            if (GUILayout.Button("烘焙 StartEnd / StopL / StopR / PivotTurn 根位移"))
            {
                Undo.RecordObject(profile, "Bake Locomotion Root Motion");
                Bake(profile, bakeSource, AnimationKey.StartEnd);
                Bake(profile, bakeSource, AnimationKey.StopL);
                Bake(profile, bakeSource, AnimationKey.StopR);
                Bake(profile, bakeSource, AnimationKey.PivotTurn);
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
                Debug.Log($"LocomotionRootMotion: 已烘焙到「{profile.name}」。", profile);
            }
        }

        if (bakeSource == null)
        {
            EditorGUILayout.HelpBox("请先指定 Animation Profile，再烘焙根位移。", MessageType.Warning);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "需 Clip 含 Humanoid RootT/Q 或 Generic LocalPosition 曲线。烘焙后 StartEnd/Stop/Pivot 用采样位移。",
                MessageType.Info);
        }
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
