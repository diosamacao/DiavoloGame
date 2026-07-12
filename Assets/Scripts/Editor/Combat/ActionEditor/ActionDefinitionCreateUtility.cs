using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>在 Action Editor 内创建 ActionDefinition 资产的工具方法。</summary>
public static class ActionDefinitionCreateUtility
{
    public const string DefaultFolder = "Assets/Data/Combat/Actions/Player/ActioniDefinition";

    /// <summary>
    /// 创建新招式资产；文件名即显示名。成功返回资产，失败返回 null。
    /// 仅新建文件，不修改已有 .asset。首段写入 animationSegments[0]。
    /// </summary>
    public static ActionDefinition Create(
        string fileName,
        AnimationClip animationClip,
        string folder = DefaultFolder)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            EditorUtility.DisplayDialog("Create Action", "文件名不能为空。", "OK");
            return null;
        }

        if (string.IsNullOrWhiteSpace(folder))
            folder = DefaultFolder;

        EnsureFolderExists(folder);

        string safeName = SanitizeFileName(fileName);
        string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{safeName}.asset");

        var action = ScriptableObject.CreateInstance<ActionDefinition>();
        AssetDatabase.CreateAsset(action, assetPath);

        var so = new SerializedObject(action);
        SerializedProperty segmentsProp = so.FindProperty("animationSegments");
        if (animationClip != null && segmentsProp != null)
        {
            segmentsProp.arraySize = 1;
            SerializedProperty element = segmentsProp.GetArrayElementAtIndex(0);
            element.FindPropertyRelative("clip").objectReferenceValue = animationClip;
            element.FindPropertyRelative("startFrame").intValue = 0;
            element.FindPropertyRelative("endFrame").intValue = -1;
            element.FindPropertyRelative("crossFadeDuration").floatValue = 0f;

            float sampleRate = Mathf.Max(1f, so.FindProperty("sampleRate").floatValue);
            so.FindProperty("totalFrames").intValue =
                Mathf.Max(1, Mathf.RoundToInt(animationClip.length * sampleRate));
        }
        else if (segmentsProp != null)
        {
            segmentsProp.arraySize = 0;
            so.FindProperty("totalFrames").intValue = 1;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(action);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        action = AssetDatabase.LoadAssetAtPath<ActionDefinition>(assetPath);
        Selection.activeObject = action;
        EditorGUIUtility.PingObject(action);
        return action;
    }

    static void EnsureFolderExists(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;

        string[] parts = folder.Replace('\\', '/').Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');

        name = name.Trim().Replace(' ', '_');
        return string.IsNullOrEmpty(name) ? "NewAction" : name;
    }
}
