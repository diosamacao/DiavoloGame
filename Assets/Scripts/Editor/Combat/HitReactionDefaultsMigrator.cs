using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// P-HR3：把未填的冲击力 / 站立韧性写成杂兵默认。
/// </summary>
public static class HitReactionDefaultsMigrator
{
    const string MenuFill = "ACTGame/Combat/Fill Hit Reaction Defaults";
    const string MenuValidate = "ACTGame/Combat/Validate Hit Reaction Defaults";

    /// <summary>扫描并补写 Action Hitbox 的冲击力≤0、CharacterConfig 韧性≤0。</summary>
    [MenuItem(MenuFill)]
    public static void FillDefaults()
    {
        int actionsTouched = FillActionDefinitions(apply: true);
        int configsTouched = FillCharacterConfigs(apply: true);
        AssetDatabase.SaveAssets();
        Debug.Log(
            $"HitReactionDefaults: 已写入 ActionDefinition {actionsTouched} 个、" +
            $"CharacterConfig {configsTouched} 个。");
    }

    /// <summary>只报告未填字段，不写资产。</summary>
    [MenuItem(MenuValidate)]
    public static void ValidateDefaults()
    {
        int actionsMissing = FillActionDefinitions(apply: false);
        int configsMissing = FillCharacterConfigs(apply: false);
        Debug.Log(
            $"HitReactionDefaults: 待补 ActionDefinition {actionsMissing} 个、" +
            $"CharacterConfig {configsMissing} 个（冲击力/韧性≤0）。");
    }

    /// <summary>扫描 ActionDefinition Hitbox；apply=false 只计数 interruptLevel≤0。</summary>
    static int FillActionDefinitions(bool apply)
    {
        string[] guids = AssetDatabase.FindAssets("t:ActionDefinition");
        Array.Sort(guids, StringComparer.Ordinal);
        int touched = 0;
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            ActionDefinition action = AssetDatabase.LoadAssetAtPath<ActionDefinition>(path);
            if (action == null)
                continue;

            var so = new SerializedObject(action);
            SerializedProperty timeline = so.FindProperty("timeline");
            SerializedProperty boxes = timeline != null
                ? timeline.FindPropertyRelative("hitboxStates")
                : null;
            if (boxes == null)
                continue;

            bool dirty = false;
            for (int b = 0; b < boxes.arraySize; b++)
            {
                SerializedProperty payload = boxes.GetArrayElementAtIndex(b)
                    .FindPropertyRelative("payload");
                SerializedProperty level = payload != null
                    ? payload.FindPropertyRelative("interruptLevel")
                    : null;
                if (level == null || level.intValue > 0)
                    continue;

                dirty = true;
                if (apply)
                    level.intValue = HitReactionResolveQuery.DefaultInterruptLevel;
            }

            if (!dirty)
                continue;

            touched++;
            if (apply)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(action);
            }
        }

        return touched;
    }

    /// <summary>扫描 CharacterConfig；apply=false 只计数 baseInterruptResist≤0。</summary>
    static int FillCharacterConfigs(bool apply)
    {
        string[] guids = AssetDatabase.FindAssets("t:CharacterConfig");
        Array.Sort(guids, StringComparer.Ordinal);
        int touched = 0;
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            CharacterConfig config = AssetDatabase.LoadAssetAtPath<CharacterConfig>(path);
            if (config == null)
                continue;

            var so = new SerializedObject(config);
            SerializedProperty combat = so.FindProperty("combat");
            SerializedProperty resist = combat != null
                ? combat.FindPropertyRelative("baseInterruptResist")
                : null;
            if (resist == null || resist.intValue > 0)
                continue;

            touched++;
            if (!apply)
                continue;

            resist.intValue = HitReactionResolveQuery.DefaultBaseInterruptResist;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
        }

        return touched;
    }
}
