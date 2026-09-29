using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>在 Action Editor 内创建 ActionDefinition 资产的工具方法。</summary>
public static class ActionDefinitionCreateUtility
{
    /// <summary>角色目录下存放招式 SO 的标准子文件夹名。</summary>
    public const string ActionDefinitionFolderName = "Actions";

    public const string DefaultCharacterFolder = "Assets/Data/Shared";

    /// <summary>
    /// 将用户选中的角色文件夹解析为实际保存目录：`{Character}/Actions`。
    /// 若已是 Actions 或 Reactions 则原样使用；
    /// 子目录不存在且 createIfMissing 时创建标准名文件夹。
    /// </summary>
    public static string ResolveActionDefinitionFolder(string selectedFolder, bool createIfMissing)
    {
        if (string.IsNullOrWhiteSpace(selectedFolder))
            selectedFolder = DefaultCharacterFolder;

        string selected = selectedFolder.Replace('\\', '/').TrimEnd('/');
        string leaf = GetFolderLeafName(selected);

        // 已点进招式目录：直接用。
        if (IsActionDefinitionFolderName(leaf))
            return selected;

        string preferred = $"{selected}/{ActionDefinitionFolderName}";

        if (AssetDatabase.IsValidFolder(preferred))
            return preferred;

        if (createIfMissing)
        {
            if (!AssetDatabase.IsValidFolder(selected))
            {
                EditorUtility.DisplayDialog("Create Action", $"角色文件夹无效：{selected}", "OK");
                return null;
            }

            EnsureFolderExists(preferred);
            return preferred;
        }

        return preferred;
    }

    /// <summary>取用于 UI 展示/记忆的角色文件夹：选中招式目录时回退到其父级。</summary>
    public static string GetCharacterFolder(string selectedOrResolvedFolder)
    {
        if (string.IsNullOrWhiteSpace(selectedOrResolvedFolder))
            return DefaultCharacterFolder;

        string path = selectedOrResolvedFolder.Replace('\\', '/').TrimEnd('/');
        if (IsActionDefinitionFolderName(GetFolderLeafName(path)))
        {
            int slash = path.LastIndexOf('/');
            return slash > 0 ? path.Substring(0, slash) : path;
        }

        return path;
    }

    /// <summary>默认名字仅取当前目录身份与用途，不继承旧动作或动画资源名。</summary>
    public static string BuildDefaultFileName(string characterFolder, string saveFolder, AnimationClip clip)
    {
        string key = GetFolderLeafName(GetCharacterFolder(characterFolder));
        return SanitizeFileName(key == "Shared" ? "Action_01" : key + "_Action_01");
    }

    /// <summary>取 Assets 相对路径的最后一段文件夹名。</summary>
    public static string GetFolderLeafName(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
            return "Action";

        string normalized = folder.Replace('\\', '/').TrimEnd('/');
        int slash = normalized.LastIndexOf('/');
        string leaf = slash >= 0 ? normalized.Substring(slash + 1) : normalized;
        return string.IsNullOrEmpty(leaf) ? "Action" : leaf;
    }

    /// <summary>
    /// 创建新招式资产；fileFolder 应为已解析的 ActionDefinition 目录。
    /// 仅新建文件，不修改已有 .asset。首段写入 animationSegments[0]。
    /// </summary>
    public static ActionDefinition Create(
        string fileName,
        AnimationClip animationClip,
        string folder)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            EditorUtility.DisplayDialog("Create Action", "文件名不能为空。", "OK");
            return null;
        }

        folder = ResolveActionDefinitionFolder(folder, createIfMissing: true);
        if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
        {
            EditorUtility.DisplayDialog("Create Action", "无法解析或创建 ActionDefinition 文件夹。", "OK");
            return null;
        }

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

            int sampleRate = ActionSim.LogicHz;
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

    static bool IsActionDefinitionFolderName(string leaf) =>
        string.Equals(leaf, ActionDefinitionFolderName, System.StringComparison.OrdinalIgnoreCase)
        || string.Equals(leaf, "Reactions", System.StringComparison.OrdinalIgnoreCase);

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
        if (string.IsNullOrWhiteSpace(name))
            return "NewAction";

        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');

        name = name.Trim().Replace(' ', '_');
        return string.IsNullOrEmpty(name) ? "NewAction" : name;
    }
}
