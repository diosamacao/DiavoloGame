using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>在角色上下文内创建一个有明确用途的动作并自动绑定。</summary>
public sealed class CharacterActionCreateWindow : EditorWindow
{
    CharacterConfig config;
    int mode;
    AnimationClip[] clips = Array.Empty<AnimationClip>();
    string actionName;
    CombatActionType type;
    GameplayIntentType intent = GameplayIntentType.Attack;
    bool entry;
    bool reaction;
    CharacterReactionType reactionType;
    string error;
    ActionAnimationPickerPanel picker;
    Vector2 scroll;
    void OnEnable() => picker = new ActionAnimationPickerPanel(this);
    void OnDisable() { picker?.Dispose(); picker = null; }
    /// <summary>在角色上下文打开用途命名的动作草稿创建窗口。</summary>
    public static void Open(CharacterConfig config, int mode, bool reaction = false)
    {
        var window = GetWindow<CharacterActionCreateWindow>(true, "Create Character Action");
        window.config = config; window.mode = mode; window.reaction = reaction;
        window.clips = Array.Empty<AnimationClip>(); window.minSize = new Vector2(540, 600);
        window.picker.Bind(config, config.ModelPrefab);
        window.type = reaction ? CombatActionType.Hit : CombatActionType.Attack;
        window.actionName = reaction ? "Hit_01" : "Attack_01"; window.error = null; window.Show();
    }
    void OnGUI()
    {
        if (config == null) { Close(); return; }
        picker.Bind(config, config.ModelPrefab);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField(config.name, EditorStyles.boldLabel);
        actionName = EditorGUILayout.TextField("动作用途名称", actionName);
        clips = picker.Draw(clips);
        type = (CombatActionType)EditorGUILayout.EnumPopup("动作类型", type);
        reaction = EditorGUILayout.Toggle("绑定为反应动作", reaction);
        if (reaction) reactionType = (CharacterReactionType)EditorGUILayout.EnumPopup("反应类型", reactionType);
        else
        {
            intent = (GameplayIntentType)EditorGUILayout.EnumPopup("节点 Intent", intent);
            entry = EditorGUILayout.Toggle("起手入口", entry);
        }
        EditorGUILayout.HelpBox("创建动画草稿与绑定；命中框、取消窗、资源和关键帧需在时间轴设置。", MessageType.Info);
        try { EditorGUILayout.LabelField("保存到", CharacterAssetLayout.ActionFolder(config, reaction) + "/" + CharacterAssetLayout.ActionName(config, actionName) + ".asset"); }
        catch (Exception e) { EditorGUILayout.HelpBox(e.Message, MessageType.Warning); }
        using (new EditorGUI.DisabledScope(clips.Length == 0 || clips.Any(c => c == null)))
            if (GUILayout.Button("创建并编辑"))
                try
                {
                    ActionDefinition result = CharacterAuthoringService.CreateAction(config, mode, clips, actionName, type, intent, entry, reaction, reactionType);
                    ActionEditorWindow.OpenForCharacter(config, mode, result); Close();
                }
                catch (Exception e) { error = e.Message; }
        if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
        EditorGUILayout.EndScrollView();
    }
}
