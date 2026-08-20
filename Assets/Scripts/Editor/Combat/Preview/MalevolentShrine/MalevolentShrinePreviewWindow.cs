using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>伏魔御厨子独立技能预览。不写入 ActionDefinition，不碰战斗模拟。</summary>
public sealed class MalevolentShrinePreviewWindow : EditorWindow
{
    const string SceneDir = "Assets/Scenes/Previews";
    const string ScenePath = SceneDir + "/MalevolentShrinePreview.unity";

    MalevolentShrinePreviewBootstrap _bootstrap;
    Vector2 _scroll;

    [MenuItem("ACT/Combat/Preview/Malevolent Shrine")]
    public static void Open()
    {
        MalevolentShrinePreviewWindow window = GetWindow<MalevolentShrinePreviewWindow>();
        window.titleContent = new GUIContent("伏魔御厨子");
        window.minSize = new Vector2(420f, 360f);
        window.Show();
    }

    void OnEnable()
    {
        FindBootstrap();
    }

    void OnFocus()
    {
        FindBootstrap();
    }

    void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        EditorGUILayout.LabelField("伏魔御厨子 · Unity 技能预览", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "独立预览场景，不接入 ActionSim / 伤害 / 联网。\n" +
            "1. 创建或打开预览场景\n" +
            "2. 搭建灰盒（御厨子、开放半径、假建筑、假敌）\n" +
            "3. 进入 Play，看 6.8 秒演出\n" +
            "Play 中按空格重播。",
            MessageType.Info);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("场景", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("创建 / 打开预览场景", GUILayout.Height(28)))
            CreateOrOpenScene();
        if (GUILayout.Button("搭建灰盒", GUILayout.Height(28)))
            RebuildCurrent();
        EditorGUILayout.EndHorizontal();

        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying && _bootstrap == null))
        {
            if (GUILayout.Button(EditorApplication.isPlaying ? "重播" : "进入 Play", GUILayout.Height(32)))
                PlayOrRestart();
        }

        EditorGUILayout.Space(8);
        DrawSettings();
        EditorGUILayout.Space(8);
        DrawStatus();
        EditorGUILayout.EndScrollView();
    }

    void DrawSettings()
    {
        EditorGUILayout.LabelField("参数", EditorStyles.boldLabel);
        if (_bootstrap == null)
        {
            EditorGUILayout.HelpBox("当前场景没有 MalevolentShrinePreviewBootstrap。先创建预览场景。", MessageType.Warning);
            return;
        }

        SerializedObject so = new SerializedObject(_bootstrap);
        so.Update();
        SerializedProperty settings = so.FindProperty("settings");
        if (settings != null)
            EditorGUILayout.PropertyField(settings, true);
        so.ApplyModifiedProperties();
    }

    void DrawStatus()
    {
        EditorGUILayout.LabelField("状态", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("场景", SceneManager.GetActiveScene().path);
        EditorGUILayout.LabelField("Bootstrap", _bootstrap != null ? _bootstrap.name : "未找到");
        if (EditorApplication.isPlaying && _bootstrap != null && _bootstrap.Director != null)
        {
            EditorGUILayout.LabelField("阶段", _bootstrap.Director.PhaseName);
            EditorGUILayout.LabelField("时间", _bootstrap.Director.PlaybackTime.ToString("0.00") + "s");
            Repaint();
        }
    }

    void CreateOrOpenScene()
    {
        if (File.Exists(ScenePath))
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            FindBootstrap();
            if (_bootstrap == null)
                CreateBootstrap();
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        EnsureFolder("Assets/Scenes");
        EnsureFolder(SceneDir);
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = new Color(0.08f, 0.06f, 0.05f);
        RenderSettings.fogDensity = 0.012f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.16f, 0.13f, 0.12f);
        CreateBootstrap();
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();
        FindBootstrap();
    }

    void CreateBootstrap()
    {
        GameObject go = new GameObject("MalevolentShrinePreview");
        _bootstrap = go.AddComponent<MalevolentShrinePreviewBootstrap>();
        _bootstrap.settings = MalevolentShrinePreviewSettings.CreateDefault();
        Undo.RegisterCreatedObjectUndo(go, "Create Malevolent Shrine Preview");
        Selection.activeGameObject = go;
        _bootstrap.Rebuild();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    void RebuildCurrent()
    {
        FindBootstrap();
        if (_bootstrap == null)
        {
            EditorUtility.DisplayDialog("伏魔御厨子", "当前场景没有预览根节点。请先创建预览场景。", "确定");
            return;
        }

        _bootstrap.Rebuild();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    void PlayOrRestart()
    {
        FindBootstrap();
        if (EditorApplication.isPlaying)
        {
            if (_bootstrap != null && _bootstrap.Director != null)
                _bootstrap.Director.Restart();
            return;
        }

        if (_bootstrap == null)
            CreateOrOpenScene();
        EditorApplication.isPlaying = true;
    }

    void FindBootstrap()
    {
        _bootstrap = FindFirstObjectByType<MalevolentShrinePreviewBootstrap>();
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = Path.GetFileName(path);
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
            return;
        if (!AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
