using System;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

/// <summary>显式设置整批 Clip 的用途，并在生成前展示名字和绑定目标。</summary>
public sealed class CharacterActionBatchWindow : EditorWindow
{
    [SerializeField] CharacterConfig config;
    [SerializeField] int mode;
    [SerializeField] AnimationClip[] clips = Array.Empty<AnimationClip>();
    string prefix;
    int preset;
    string error;
    Vector2 scroll;
    ReorderableList clipList;

    /// <summary>打开指定角色模式的批量草稿窗口。</summary>
    public static void Open(CharacterConfig config, int mode)
    {
        var window = GetWindow<CharacterActionBatchWindow>(true, "Batch Actions");
        window.config = config; window.mode = mode; window.prefix = "Attack";
        window.clips = Array.Empty<AnimationClip>(); window.error = null;
        window.Show();
    }

    void OnGUI()
    {
        if (config == null) { EditorGUILayout.HelpBox("请从角色工作台打开。", MessageType.Info); return; }
        CharacterAnimationSourcePreferences.DrawAnimationFolder(config);
        string source = CharacterAnimationSourcePreferences.Get(config, false);
        using (new EditorGUI.DisabledScope(!AssetDatabase.IsValidFolder(source)))
        {
            if (GUILayout.Button("追加目录全部动画"))
                AppendFromLibrary();
        }
        EditorGUILayout.HelpBox("选择窗口限定当前动作库（含子目录）。追加不会重复添加同一个 Clip；可在下方清单删除或调整顺序后创建。", MessageType.Info);
        int previousPreset = preset;
        preset = EditorGUILayout.Popup("用途初值", preset, new[] { "普攻", "闪避" });
        if (previousPreset != preset && (prefix == "Attack" || prefix == "Dodge")) prefix = preset == 0 ? "Attack" : "Dodge";
        prefix = EditorGUILayout.TextField("动作用途前缀", prefix);
        DrawClips();
        scroll = EditorGUILayout.BeginScrollView(scroll);
        try
        {
            EditorGUILayout.LabelField("保存目录", CharacterAssetLayout.ActionFolder(config, false));
            for (int i = 0; i < clips.Length; i++)
                EditorGUILayout.LabelField(CharacterAssetLayout.ActionName(config, $"{prefix}_{i + 1:D2}"), clips[i] != null ? clips[i].name : "缺少 Clip");
        }
        catch (Exception e) { EditorGUILayout.HelpBox(e.Message, MessageType.Warning); }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.HelpBox("全部绑定到当前模式的图，初始均不是 Entry。不会生成命中帧、取消窗或连线；创建后按玩法调整。整批失败会清理本批新增资产。", MessageType.Info);
        if (GUILayout.Button("创建全部草稿"))
            try
            {
                CharacterAuthoringService.CreateActions(config, mode, clips, prefix,
                    preset == 0 ? CombatActionType.Attack : CombatActionType.Dodge,
                    preset == 0 ? GameplayIntentType.Attack : GameplayIntentType.Dodge);
                CharacterAuthoringWindow.Open(config); Close();
            }
            catch (Exception e) { error = e.Message; }
        if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
    }

    void DrawClips()
    {
        if (clipList == null)
        {
            clipList = new ReorderableList(clips.ToList(), typeof(AnimationClip), true, true, true, true);
            clipList.onReorderCallback = list => clips = list.list.Cast<AnimationClip>().ToArray();
            clipList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Clips（点击小圆圈从动作库选择）");
            clipList.drawElementCallback = (rect, index, active, focused) =>
            {
                rect.height = EditorGUIUtility.singleLineHeight;
                var snapshot = clips.ToArray();
                var context = config;
                clips[index] = ActionAnimationPickerPanel.DrawClipField(rect, new GUIContent($"Element {index}"), clips[index], config,
                    clip =>
                    {
                        // 选择窗口打开期间若切换角色或调整清单，不向变化后的行写入旧选择。
                        if (this == null || config != context || !clips.SequenceEqual(snapshot)) return;
                        clips[index] = clip; Repaint();
                    });
            };
            clipList.onAddCallback = list => { clips = clips.Concat(new AnimationClip[] { null }).ToArray(); list.index = clips.Length - 1; };
            clipList.onRemoveCallback = list =>
            {
                if (list.index < 0 || list.index >= clips.Length) return;
                clips = clips.Where((clip, i) => i != list.index).ToArray();
                list.index = Mathf.Min(list.index, clips.Length - 1);
            };
        }
        clipList.list = clips.ToList();
        clipList.DoLayoutList();
    }

    /// <summary>读取当前角色最新的动作库，将目录及子目录动画去重追加；不创建资产。</summary>
    public void AppendFromLibrary()
    {
        string source = CharacterAnimationSourcePreferences.Get(config, false);
        if (!AssetDatabase.IsValidFolder(source)) return;
        clips = clips.Concat(ActionAnimationPickerPanel.Collect(source)).Where(c => c != null).Distinct().ToArray();
    }
}
