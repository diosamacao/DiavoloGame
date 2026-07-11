using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>在 Action Editor 内创建 ActionDefinition 资产的工具方法。</summary>
public static class ActionDefinitionCreateUtility
{
    public const string DefaultFolder = "Assets/Data/Combat/Actions/Player/ActioniDefinition";

    /// <summary>
    /// 创建新招式资产并写入基础字段；成功返回资产，失败返回 null。
    /// 仅新建文件，不修改已有 .asset。
    /// </summary>
    public static ActionDefinition Create(
        string displayName,
        string id,
        AnimationClip animationClip,
        string folder = DefaultFolder)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            EditorUtility.DisplayDialog("Create Action", "Display Name 不能为空。", "OK");
            return null;
        }

        if (string.IsNullOrWhiteSpace(folder))
            folder = DefaultFolder;

        EnsureFolderExists(folder);

        string fileName = SanitizeFileName(string.IsNullOrWhiteSpace(id) ? displayName : id);
        string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{fileName}.asset");

        var action = ScriptableObject.CreateInstance<ActionDefinition>();
        AssetDatabase.CreateAsset(action, assetPath);

        var so = new SerializedObject(action);
        so.FindProperty("displayName").stringValue = displayName.Trim();
        so.FindProperty("id").stringValue = string.IsNullOrWhiteSpace(id)
            ? Path.GetFileNameWithoutExtension(assetPath)
            : id.Trim();
        so.FindProperty("animationClip").objectReferenceValue = animationClip;

        // 与 ActionDefinition.OnValidate 对齐：有 Clip 时写入 totalFrames。
        float sampleRate = Mathf.Max(1f, so.FindProperty("sampleRate").floatValue);
        if (animationClip != null)
            so.FindProperty("totalFrames").intValue = Mathf.Max(1, Mathf.RoundToInt(animationClip.length * sampleRate));

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
