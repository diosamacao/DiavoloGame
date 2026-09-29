using UnityEditor;
using UnityEngine;

/// <summary>本机动画来源偏好；按项目、角色或独立动作分组，以目录 GUID 抵抗目录移动。</summary>
public static class CharacterAnimationSourcePreferences
{
    /// <summary>编辑当前角色的动画来源目录；各工作页与创建窗口共用同一偏好，空上下文用于独立创建。</summary>
    public static void DrawAnimationFolder(Object scope)
    {
        string path = Get(scope, false);
        var current = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
        EditorGUI.BeginChangeCheck();
        var next = (DefaultAsset)EditorGUILayout.ObjectField(
            new GUIContent("动作库文件夹", "动画 Clip / FBX 所在目录，用于新建动作时筛选动画；不是 ActionDefinition 保存目录。按角色在本机记忆。"),
            current, typeof(DefaultAsset), false);
        if (EditorGUI.EndChangeCheck()) Set(scope, false, AssetDatabase.GetAssetPath(next));
    }

    static string Key(Object scope, bool rootMotion) => "ACTGame.AnimationSources." + Application.dataPath + "."
        + (scope != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(scope)) : "Standalone")
        + (rootMotion ? ".RM" : ".Animation");

    /// <summary>读取有效目录；旧 RM 路径首次读取时迁移，成功后删除旧键。</summary>
    public static string Get(Object scope, bool rootMotion)
    {
        string key = Key(scope, rootMotion);
        if (rootMotion && scope != null && !EditorPrefs.HasKey(key))
        {
            string oldKey = (scope is CharacterConfig ? "ACTGame.Authoring.RM." : "ACTGame.ActionMotion.RM.")
                + Application.dataPath + "." + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(scope));
            if (EditorPrefs.HasKey(oldKey))
            {
                string path = EditorPrefs.GetString(oldKey);
                if (AssetDatabase.IsValidFolder(path)) { Set(scope, true, path); EditorPrefs.DeleteKey(oldKey); }
            }
        }
        string result = AssetDatabase.GUIDToAssetPath(EditorPrefs.GetString(key, ""));
        return AssetDatabase.IsValidFolder(result) ? result : "";
    }

    /// <summary>保存已选目录；空值清空，非目录输入拒绝写入。</summary>
    public static void Set(Object scope, bool rootMotion, string path)
    {
        if (!string.IsNullOrEmpty(path) && !AssetDatabase.IsValidFolder(path)) return;
        EditorPrefs.SetString(Key(scope, rootMotion), string.IsNullOrEmpty(path) ? "" : AssetDatabase.AssetPathToGUID(path));
    }
}
