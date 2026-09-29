using System;
using UnityEditor;
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

    /// <summary>打开指定角色模式的批量草稿窗口。</summary>
    public static void Open(CharacterConfig config, int mode)
    {
        var window = GetWindow<CharacterActionBatchWindow>(true, "Batch Actions");
        window.config = config; window.mode = mode; window.prefix = "Attack";
        window.Show();
    }

    void OnGUI()
    {
        if (config == null) { EditorGUILayout.HelpBox("请从角色工作台打开。", MessageType.Info); return; }
        int previousPreset = preset;
        preset = EditorGUILayout.Popup("用途初值", preset, new[] { "普攻", "闪避" });
        if (previousPreset != preset && (prefix == "Attack" || prefix == "Dodge")) prefix = preset == 0 ? "Attack" : "Dodge";
        prefix = EditorGUILayout.TextField("动作用途前缀", prefix);
        var so = new SerializedObject(this);
        so.Update(); EditorGUILayout.PropertyField(so.FindProperty("clips"), true); so.ApplyModifiedProperties();
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
        if (error != null) EditorGUILayout.HelpBox(error, MessageType.Error);
    }
}
