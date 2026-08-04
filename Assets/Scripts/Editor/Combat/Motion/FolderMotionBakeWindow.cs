using UnityEditor;
using UnityEngine;

/// <summary>选择 InPlace / RootMotion 文件夹并 Preview / Bake / Bake Dirty 运动表。</summary>
public sealed class FolderMotionBakeWindow : EditorWindow
{
    const string PrefInplace = "ACTGame.MotionBake.InplaceFolder";
    const string PrefRootMotion = "ACTGame.MotionBake.RootMotionFolder";

    DefaultAsset _inplaceFolder;
    DefaultAsset _rootMotionFolder;
    ActionMotionPlanarMode _planarMode = ActionMotionPlanarMode.FullPlanar;
    int _logicHz = ActionSim.LogicHz;
    Vector2 _scroll;
    string _log = string.Empty;

    /// <summary>打开文件夹烘焙窗口。</summary>
    [MenuItem("ACTGame/Motion/Bake From Folders...")]
    public static void Open()
    {
        var window = GetWindow<FolderMotionBakeWindow>("Motion Bake");
        window.minSize = new Vector2(480f, 360f);
        window.Show();
    }

    /// <summary>菜单：校验工程内 Action 运动表是否 Dirty/Failed。</summary>
    [MenuItem("ACTGame/Motion/Validate Motion Dirty")]
    public static void ValidateMotionDirtyMenu()
    {
        string rm = EditorPrefs.GetString(PrefRootMotion, "Assets/Art/Arts/Unagi/RootMotion");
        string report = ActionMotionDirtyUtility.ValidateProject(rm, ActionSim.LogicHz);
        Debug.Log(report);
        EditorUtility.DisplayDialog(
            "Validate Motion Dirty",
            report.Length > 1500 ? report.Substring(0, 1500) + "…" : report,
            "OK");
    }

    void OnEnable()
    {
        string inplacePath = EditorPrefs.GetString(PrefInplace, "Assets/Art/Arts/Unagi/Inplace");
        string rmPath = EditorPrefs.GetString(PrefRootMotion, "Assets/Art/Arts/Unagi/RootMotion");
        _inplaceFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(inplacePath);
        _rootMotionFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(rmPath);
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("InPlace + RootMotion 文件夹烘焙", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "使用已有 InPlace 作为表现源；水平位移从配对 RootMotion 采样。"
            + " 朝向不烘焙，由运行时索敌/输入旋转规则控制。不生成 InPlace。",
            MessageType.Info);

        _inplaceFolder = (DefaultAsset)EditorGUILayout.ObjectField(
            "InPlace Folder",
            _inplaceFolder,
            typeof(DefaultAsset),
            false);
        _rootMotionFolder = (DefaultAsset)EditorGUILayout.ObjectField(
            "RootMotion Folder",
            _rootMotionFolder,
            typeof(DefaultAsset),
            false);

        _planarMode = (ActionMotionPlanarMode)EditorGUILayout.EnumPopup("Planar Mode", _planarMode);
        _logicHz = EditorGUILayout.IntField("Logic Hz", _logicHz);

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Preview Matches", GUILayout.Height(28f)))
                RunPreview();
            if (GUILayout.Button("Bake All", GUILayout.Height(28f)))
                RunBake(dirtyOnly: false);
            if (GUILayout.Button("Bake Dirty Only", GUILayout.Height(28f)))
                RunBake(dirtyOnly: true);
        }

        if (GUILayout.Button("Validate Dirty (Project)"))
            RunValidate();

        EditorGUILayout.Space();
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        EditorGUILayout.TextArea(_log, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    void RunPreview()
    {
        if (!TryGetFolderPaths(out string inplace, out string rm))
            return;

        PersistFolders(inplace, rm);
        _log = ActionMotionBakeService.PreviewMatches(inplace, rm);
        Debug.Log("Motion Bake Preview\n" + _log);
    }

    void RunBake(bool dirtyOnly)
    {
        if (!TryGetFolderPaths(out string inplace, out string rm))
            return;

        PersistFolders(inplace, rm);
        string title = dirtyOnly ? "Bake Dirty Motion" : "Bake All Motion";
        string body = dirtyOnly
            ? "仅重烘指纹过期或未就绪的 ActionDefinition。"
            : "将把匹配成功的运动表写回引用对应 InPlace 的 ActionDefinition。";
        bool confirmed = EditorUtility.DisplayDialog(
            title,
            body + "\n不修改任何 InPlace/RM 动画资产。",
            "Bake",
            "Cancel");
        if (!confirmed)
            return;

        ActionMotionBakeService.BakeReport report = ActionMotionBakeService.BakeFromFolders(
            inplace,
            rm,
            _planarMode,
            Mathf.Max(1, _logicHz),
            dirtyOnly);
        _log = report.ToString();
        Debug.Log("Motion Bake\n" + _log);
        EditorUtility.DisplayDialog(title, _log.Length > 1500 ? _log.Substring(0, 1500) + "…" : _log, "OK");
    }

    void RunValidate()
    {
        if (!TryGetFolderPaths(out string inplace, out string rm))
            return;

        PersistFolders(inplace, rm);
        _log = ActionMotionDirtyUtility.ValidateProject(rm, Mathf.Max(1, _logicHz));
        Debug.Log(_log);
    }

    bool TryGetFolderPaths(out string inplace, out string rm)
    {
        inplace = _inplaceFolder != null ? AssetDatabase.GetAssetPath(_inplaceFolder) : string.Empty;
        rm = _rootMotionFolder != null ? AssetDatabase.GetAssetPath(_rootMotionFolder) : string.Empty;
        if (!AssetDatabase.IsValidFolder(inplace) || !AssetDatabase.IsValidFolder(rm))
        {
            EditorUtility.DisplayDialog(
                "Motion Bake",
                "请指定有效的 InPlace 与 RootMotion 文件夹。",
                "OK");
            return false;
        }

        return true;
    }

    void PersistFolders(string inplace, string rm)
    {
        EditorPrefs.SetString(PrefInplace, inplace);
        EditorPrefs.SetString(PrefRootMotion, rm);
    }
}
