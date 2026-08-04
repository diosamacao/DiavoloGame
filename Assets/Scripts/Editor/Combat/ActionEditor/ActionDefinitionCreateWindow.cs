using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 创建 ActionDefinition 面板：选择角色文件夹，自动落到/创建其下 ActionDefinition 子目录。
/// </summary>
public sealed class ActionDefinitionCreateWindow : EditorWindow
{
    const string LastCharacterFolderPrefKey = "ACTGame.ActionEditor.Create.LastCharacterFolder";

    string _fileName = "new_action";
    AnimationClip _clip;
    /// <summary>用户选择的角色文件夹（如 Unagi），不是最终保存目录。</summary>
    string _characterFolder = ActionDefinitionCreateUtility.DefaultCharacterFolder;
    DefaultAsset _characterFolderAsset;
    /// <summary>用户是否已手动改过文件名；为 true 时不再被默认规则覆盖。</summary>
    bool _fileNameUserEdited;
    /// <summary>上一次自动生成的默认名，用于判断是否仍可自动刷新。</summary>
    string _lastAutoFileName = string.Empty;
    System.Action<ActionDefinition> _onCreated;

    /// <summary>打开创建面板；创建成功后回调 onCreated。</summary>
    public static void Open(System.Action<ActionDefinition> onCreated)
    {
        ActionDefinitionCreateWindow window = CreateInstance<ActionDefinitionCreateWindow>();
        window.titleContent = new GUIContent("Create Action Definition");
        window._onCreated = onCreated;
        window.minSize = new Vector2(460f, 220f);
        window.maxSize = new Vector2(640f, 280f);
        window.RestoreCharacterFolder();
        window.RefreshDefaultFileName(force: true);
        window.ShowUtility();
        window.Focus();
    }

    void OnGUI()
    {
        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("New Action Definition", EditorStyles.boldLabel);
        EditorGUILayout.Space(4f);

        DrawCharacterFolderField();

        string previewSaveFolder =
            ActionDefinitionCreateUtility.ResolveActionDefinitionFolder(_characterFolder, createIfMissing: false);
        EditorGUILayout.LabelField("Save To", previewSaveFolder ?? string.Empty, EditorStyles.miniLabel);
        if (!string.IsNullOrEmpty(previewSaveFolder) && !AssetDatabase.IsValidFolder(previewSaveFolder))
        {
            EditorGUILayout.HelpBox(
                $"将自动创建子文件夹 {ActionDefinitionCreateUtility.ActionDefinitionFolderName}。",
                MessageType.Info);
        }

        EditorGUI.BeginChangeCheck();
        _clip = (AnimationClip)EditorGUILayout.ObjectField(
            "First Animation Clip",
            _clip,
            typeof(AnimationClip),
            false);
        if (EditorGUI.EndChangeCheck())
            RefreshDefaultFileName(force: false);

        EditorGUI.BeginChangeCheck();
        _fileName = EditorGUILayout.TextField(
            new GUIContent(
                "File Name",
                "默认：保存目录内最后一个 Action 名（若无则用角色文件夹名）；选中 Clip 后追加 _Clip名。可手动修改。"),
            _fileName);
        if (EditorGUI.EndChangeCheck())
            _fileNameUserEdited = true;

        EditorGUILayout.Space(16f);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Cancel", GUILayout.Width(90f), GUILayout.Height(24f)))
                Close();

            using (new EditorGUI.DisabledScope(
                       string.IsNullOrWhiteSpace(_fileName)
                       || !AssetDatabase.IsValidFolder(_characterFolder)))
            {
                if (GUILayout.Button("Create", GUILayout.Width(90f), GUILayout.Height(24f)))
                {
                    ActionDefinition created =
                        ActionDefinitionCreateUtility.Create(_fileName, _clip, _characterFolder);
                    if (created != null)
                    {
                        EditorPrefs.SetString(LastCharacterFolderPrefKey, _characterFolder);
                        _onCreated?.Invoke(created);
                        Close();
                    }
                }
            }
        }
    }

    /// <summary>选择角色文件夹（如 Unagi）；实际写入其下 ActionDefinition。</summary>
    void DrawCharacterFolderField()
    {
        EditorGUILayout.LabelField(
            new GUIContent("Character Folder", "选择角色目录（如 Unagi），资产将保存到其子目录 ActionDefinition。"),
            EditorStyles.miniLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUI.BeginChangeCheck();
            DefaultAsset next = (DefaultAsset)EditorGUILayout.ObjectField(
                _characterFolderAsset,
                typeof(DefaultAsset),
                false);
            if (EditorGUI.EndChangeCheck())
                TrySetCharacterFolderFromAsset(next);

            if (GUILayout.Button("Select…", GUILayout.Width(64f)))
            {
                string start = Directory.Exists(ToAbsolutePath(_characterFolder))
                    ? ToAbsolutePath(_characterFolder)
                    : Application.dataPath;
                string picked = EditorUtility.OpenFolderPanel("Select Character Folder", start, string.Empty);
                if (!string.IsNullOrEmpty(picked))
                    TrySetCharacterFolderFromAbsolute(picked);
            }
        }
    }

    void RestoreCharacterFolder()
    {
        string saved = EditorPrefs.GetString(
            LastCharacterFolderPrefKey,
            ActionDefinitionCreateUtility.DefaultCharacterFolder);
        // 兼容旧 pref：若记的是 ActionDefinition 子目录，回退到角色目录。
        saved = ActionDefinitionCreateUtility.GetCharacterFolder(saved);
        if (!AssetDatabase.IsValidFolder(saved))
            saved = ActionDefinitionCreateUtility.DefaultCharacterFolder;

        SetCharacterFolder(saved, refreshName: false);
    }

    void TrySetCharacterFolderFromAsset(DefaultAsset asset)
    {
        if (asset == null)
            return;

        string path = AssetDatabase.GetAssetPath(asset);
        if (!AssetDatabase.IsValidFolder(path))
        {
            EditorUtility.DisplayDialog("Create Action", "请选择文件夹资源，而不是文件。", "OK");
            return;
        }

        SetCharacterFolder(ActionDefinitionCreateUtility.GetCharacterFolder(path), refreshName: true);
    }

    void TrySetCharacterFolderFromAbsolute(string absolutePath)
    {
        string dataPath = Application.dataPath.Replace('\\', '/');
        string picked = absolutePath.Replace('\\', '/');
        if (!picked.StartsWith(dataPath))
        {
            EditorUtility.DisplayDialog("Create Action", "只能选择项目 Assets 目录下的文件夹。", "OK");
            return;
        }

        string relative = "Assets" + picked.Substring(dataPath.Length);
        if (!AssetDatabase.IsValidFolder(relative))
        {
            EditorUtility.DisplayDialog("Create Action", "所选路径不是有效的 Assets 文件夹。", "OK");
            return;
        }

        SetCharacterFolder(ActionDefinitionCreateUtility.GetCharacterFolder(relative), refreshName: true);
    }

    void SetCharacterFolder(string characterFolder, bool refreshName)
    {
        _characterFolder = characterFolder.Replace('\\', '/').TrimEnd('/');
        _characterFolderAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(_characterFolder);
        if (refreshName)
            RefreshDefaultFileName(force: false);
    }

    /// <summary>按角色/保存目录与 Clip 刷新默认名；用户手改后不再覆盖，除非 force。</summary>
    void RefreshDefaultFileName(bool force)
    {
        string saveFolder =
            ActionDefinitionCreateUtility.ResolveActionDefinitionFolder(_characterFolder, createIfMissing: false);
        string autoName = ActionDefinitionCreateUtility.BuildDefaultFileName(
            _characterFolder,
            saveFolder,
            _clip);

        if (force || !_fileNameUserEdited || _fileName == _lastAutoFileName)
        {
            _fileName = autoName;
            _fileNameUserEdited = false;
        }

        _lastAutoFileName = autoName;
    }

    static string ToAbsolutePath(string assetsRelative)
    {
        if (string.IsNullOrEmpty(assetsRelative) || !assetsRelative.StartsWith("Assets"))
            return Application.dataPath;

        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), assetsRelative));
    }
}
