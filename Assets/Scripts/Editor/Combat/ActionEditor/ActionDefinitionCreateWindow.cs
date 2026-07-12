using UnityEditor;
using UnityEngine;

/// <summary>创建 ActionDefinition 的独立工具面板（非内联表单）。</summary>
public sealed class ActionDefinitionCreateWindow : EditorWindow
{
    string _fileName = "new_action";
    AnimationClip _clip;
    string _folder = ActionDefinitionCreateUtility.DefaultFolder;
    System.Action<ActionDefinition> _onCreated;

    /// <summary>打开创建面板；创建成功后回调 onCreated。</summary>
    public static void Open(System.Action<ActionDefinition> onCreated)
    {
        ActionDefinitionCreateWindow window = CreateInstance<ActionDefinitionCreateWindow>();
        window.titleContent = new GUIContent("Create Action Definition");
        window._onCreated = onCreated;
        window.minSize = new Vector2(440f, 180f);
        window.maxSize = new Vector2(560f, 240f);
        window.ShowUtility();
        window.Focus();
    }

    void OnGUI()
    {
        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("New Action Definition", EditorStyles.boldLabel);
        EditorGUILayout.Space(4f);

        _fileName = EditorGUILayout.TextField("File Name", _fileName);
        _clip = (AnimationClip)EditorGUILayout.ObjectField("Animation Clip", _clip, typeof(AnimationClip), false);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Save Folder", EditorStyles.miniLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            _folder = EditorGUILayout.TextField(_folder);
            if (GUILayout.Button("…", GUILayout.Width(28f)))
            {
                string picked = EditorUtility.OpenFolderPanel("Select Folder", Application.dataPath, "");
                if (!string.IsNullOrEmpty(picked) && picked.Replace('\\', '/').StartsWith(Application.dataPath.Replace('\\', '/')))
                {
                    _folder = "Assets" + picked.Replace('\\', '/').Substring(Application.dataPath.Replace('\\', '/').Length);
                }
            }
        }

        EditorGUILayout.Space(16f);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Cancel", GUILayout.Width(90f), GUILayout.Height(24f)))
                Close();

            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_fileName)))
            {
                if (GUILayout.Button("Create", GUILayout.Width(90f), GUILayout.Height(24f)))
                {
                    ActionDefinition created = ActionDefinitionCreateUtility.Create(_fileName, _clip, _folder);
                    if (created != null)
                    {
                        _onCreated?.Invoke(created);
                        Close();
                    }
                }
            }
        }
    }
}
