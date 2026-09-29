using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>以角色和模式组织已有资产编辑、问题定位和角色依赖范围内的烘焙。</summary>
public sealed class CharacterAuthoringWindow : EditorWindow
{
    [SerializeField] Object root;
    [SerializeField] int modeIndex;
    [SerializeField] int tab;
    Vector2 scroll;
    Editor embedded;
    readonly List<ContentValidationIssue> issues = new();
    [SerializeField] DefaultAsset rootMotionFolder;
    string message;
    readonly CharacterConfigSourcePanel sources = new();

    [MenuItem("ACT/Character Workbench")]
    public static void Open() => GetWindow<CharacterAuthoringWindow>("Character").Show();

    /// <summary>从 Inspector 跳到指定角色；不改变场景或阵容。</summary>
    public static void Open(Object value)
    {
        var window = GetWindow<CharacterAuthoringWindow>("Character");
        window.SetRoot(value);
        window.Show();
    }

    void OnDisable() { if (embedded != null) DestroyImmediate(embedded); }
    void SetRoot(Object value)
    {
        root = value; modeIndex = 0; issues.Clear(); rootMotionFolder = null; message = null;
        sources.Invalidate();
        var config = CharacterAuthoringService.ResolveConfig(value);
        if (config != null) rootMotionFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(EditorPrefs.GetString(FolderKey(config), ""));
        if (embedded != null) DestroyImmediate(embedded);
    }

    void OnGUI()
    {
        EditorGUI.BeginChangeCheck();
        Object next = EditorGUILayout.ObjectField("角色 / 敌人 / 身体", root, typeof(Object), false);
        if (EditorGUI.EndChangeCheck())
        {
            if (next == null || CharacterAuthoringService.ResolveConfig(next) != null) SetRoot(next);
            else message = "选择 CharacterDefinition、EnemyDefinition 或 CharacterConfig。";
        }
        if (GUILayout.Button("创建空白角色…")) CharacterCreateWindow.Open();
        CharacterConfig config = CharacterAuthoringService.ResolveConfig(root);
        if (config == null) { EditorGUILayout.HelpBox(message ?? "先选择角色配置。", MessageType.Info); return; }
        var modes = config.CombatModes?.Entries;
        if (modes != null && modes.Count > 0)
            modeIndex = EditorGUILayout.Popup("战斗模式", Mathf.Clamp(modeIndex, 0, modes.Count - 1),
                modes.Select(m => m.Mode.ToString()).ToArray());
        CharacterLocomotionProfile locomotion = modes != null && modeIndex < modes.Count ? modes[modeIndex].LocomotionProfile : null;
        ActionGraph graph = modes != null && modeIndex < modes.Count ? modes[modeIndex].ActionGraph : null;
        tab = GUILayout.Toolbar(tab, new[] { "总览", "身体", "移动", "招式", "连招", "反应", "检查 / 烘焙" });
        scroll = EditorGUILayout.BeginScrollView(scroll);
        switch (tab)
        {
            case 0:
                EditorGUILayout.LabelField(config.name, EditorStyles.boldLabel);
                EditorGUILayout.LabelField("已引用动作", CharacterAuthoringService.CollectActions(config).Count.ToString());
                EditorGUILayout.HelpBox("每个角色保留 Config / Graphs / Locomotion / Actions / Reactions 五个基础目录。空目录不代表漏配，请以这里显示的实际引用为准。", MessageType.Info);
                sources.Draw("移动配置", locomotion);
                sources.Draw("动作图", graph);
                DrawReactionSources(config);
                EditorGUILayout.HelpBox("角色创建后仍需手工接入 PartyLoadout。动作草稿的命中和取消时序需要作者调整。", MessageType.Info);
                DrawAsset(root == config ? null : root);
                if (root is EnemyDefinition enemy)
                    EditorGUILayout.HelpBox($"当前敌人使用 EnemyDefinition 的 HP={enemy.MaxHp} / Team={enemy.TeamId}。身体默认值不会覆盖它。", MessageType.Info);
                break;
            case 1: DrawAsset(config); break;
            case 2: sources.Draw("移动配置", locomotion); DrawAsset(locomotion); break;
            case 3:
                if (GUILayout.Button("创建并绑定动作…")) CharacterActionCreateWindow.Open(config, modeIndex);
                if (GUILayout.Button("批量 Clip → 动作草稿…")) CharacterActionBatchWindow.Open(config, modeIndex);
                foreach (ActionDefinition action in CharacterAuthoringService.CollectActions(config, modeIndex))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.ObjectField(action, typeof(ActionDefinition), false);
                        if (GUILayout.Button("时间轴", GUILayout.Width(65))) ActionEditorWindow.OpenForCharacter(config, modeIndex, action);
                    }
                }
                break;
            case 4:
                sources.Draw("动作图", graph);
                if (graph != null && GUILayout.Button("打开当前模式动作图")) ActionGraphEditorWindow.Open(graph);
                DrawAsset(graph); break;
            case 5:
                DrawReactionSources(config);
                var so = new SerializedObject(config);
                so.Update();
                EditorGUILayout.PropertyField(so.FindProperty("combat.reactions"), true);
                so.ApplyModifiedProperties();
                if (GUILayout.Button("新增反应动作…")) CharacterActionCreateWindow.Open(config, modeIndex, true);
                break;
            case 6: DrawChecks(config, locomotion); break;
        }
        if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.Info);
        EditorGUILayout.EndScrollView();
    }

    void DrawReactionSources(CharacterConfig config)
    {
        var reactions = new List<ActionDefinition>();
        config.Combat.Reactions.CollectActions(reactions);
        if (reactions.Count == 0) EditorGUILayout.LabelField("反应动作", "尚未配置");
        foreach (var action in reactions.Where(a => a != null).Distinct()) sources.Draw("反应动作", action);
    }

    void DrawAsset(Object asset)
    {
        if (asset == null) { EditorGUILayout.HelpBox("尚未绑定此配置。", MessageType.Info); return; }
        Editor.CreateCachedEditor(asset, null, ref embedded);
        embedded.OnInspectorGUI();
    }

    void DrawChecks(CharacterConfig config, CharacterLocomotionProfile locomotion)
    {
        if (GUILayout.Button("检查当前角色（只读）"))
        {
            issues.Clear();
            issues.AddRange(CharacterValidationPanel.Collect(root, config));
            bool valid = issues.All(i => i.Severity != ContentIssueSeverity.Error);
            message = valid ? "角色核心校验通过；请继续检查烘焙与实际表现。" : "存在内容错误，请从下方问题定位配置。";
        }
        foreach (ContentValidationIssue issue in issues)
        {
            EditorGUILayout.HelpBox(issue.ToString(), issue.Severity == ContentIssueSeverity.Error ? MessageType.Error : MessageType.Warning);
            if (GUILayout.Button("定位 " + issue.PropertyPath))
            {
                Selection.activeObject = issue.Asset;
                EditorGUIUtility.PingObject(issue.Asset);
                if (issue.Asset is ActionGraph graph) ActionGraphEditorWindow.Open(graph, issue.NodeId);
                if (issue.Asset is ActionDefinition action) ActionEditorWindow.OpenForCharacter(config, modeIndex, action);
                if (issue.Asset != null && !string.IsNullOrEmpty(issue.PropertyPath)) CharacterIssueFieldWindow.Open(issue.Asset, issue.PropertyPath);
            }
        }
        EditorGUILayout.Space();
        EditorGUI.BeginChangeCheck();
        rootMotionFolder = (DefaultAsset)EditorGUILayout.ObjectField("当前角色 RM 文件夹", rootMotionFolder, typeof(DefaultAsset), false);
        if (EditorGUI.EndChangeCheck()) EditorPrefs.SetString(FolderKey(config), AssetDatabase.GetAssetPath(rootMotionFolder));
        string folder = rootMotionFolder != null ? AssetDatabase.GetAssetPath(rootMotionFolder) : "";
        var actions = CharacterAuthoringService.CollectActions(config)
            .Where(a => a.ExecutionPolicy.BaseMotionMode == ActionBaseMotionMode.BakedMotion).ToList();
        using (new EditorGUI.DisabledScope(!AssetDatabase.IsValidFolder(folder)))
        {
            if (GUILayout.Button("预览当前角色 Dirty 动作"))
                message = string.Join("\n", actions.Select(a => DescribeBake(a, folder)));
            if (GUILayout.Button("烘焙当前角色 Dirty 动作…"))
            {
                var dirty = actions.Where(a => ActionMotionDirtyUtility.IsDirty(a, folder, ActionSim.LogicHz)).ToList();
                string scope = string.Join("\n", dirty.Select(a => DescribeBake(a, folder) + "\n影响：" + string.Join(", ", CharacterAuthoringService.FindOwners(a).Select(c => c.name))));
                if (dirty.Count > 0 && EditorUtility.DisplayDialog("确认烘焙范围", scope + "\n共享这些动作的角色也会受到影响。", "烘焙", "取消"))
                {
                    var lines = new List<string>();
                    foreach (ActionDefinition action in dirty)
                    {
                        Undo.RecordObject(action, "Bake Character Action");
                        bool ok = ActionMotionBakeService.BakeAction(action, folder, action.BakedMotion.planarMode, ActionSim.LogicHz, out string result);
                        lines.Add($"{action.name}: {(ok ? "OK" : "失败")} {result}");
                        EditorUtility.SetDirty(action); AssetDatabase.SaveAssetIfDirty(action);
                    }
                    message = string.Join("\n", lines);
                    issues.Clear(); issues.AddRange(CharacterValidationPanel.Collect(root, config));
                }
            }
        }
        if (locomotion != null && GUILayout.Button("打开移动 Timing / RootMotion 烘焙")) { tab = 2; }
    }

    static string FolderKey(CharacterConfig config) => "ACTGame.Authoring.RM." + Application.dataPath + "." + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(config));

    static string DescribeBake(ActionDefinition action, string folder)
    {
        var lines = new List<string> { AssetDatabase.GetAssetPath(action) + ": " + (ActionMotionDirtyUtility.IsDirty(action, folder, ActionSim.LogicHz) ? "Dirty" : "有效") };
        foreach (var segment in action.AnimationSegments)
        {
            bool found = MotionClipPairMatcher.TryMatchSingle(segment.clip, folder, out var pair, out string error);
            lines.Add(found ? $"  {segment.clip.name} → {AssetDatabase.GetAssetPath(pair.RootMotionClip)} / {pair.RootMotionClip.name}" : "  未匹配：" + error);
        }
        return string.Join("\n", lines);
    }
}
