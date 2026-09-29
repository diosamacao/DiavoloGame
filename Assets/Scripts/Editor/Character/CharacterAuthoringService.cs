using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>空角色草稿创建与动作绑定操作；仅 Editor 使用，所有写入由明确的 UI 操作触发。</summary>
public static class CharacterAuthoringService
{
    /// <summary>从身份或身体入口读取配置，不引入 Domain 对敌人或 Editor 的依赖。</summary>
    public static CharacterConfig ResolveConfig(Object root) => root switch
    {
        CharacterDefinition character => character.CharacterConfig,
        EnemyDefinition enemy => enemy.CharacterConfig,
        CharacterConfig config => config,
        _ => null,
    };

    /// <summary>收集角色全部模式、方向变体和反应的动作，按稳定名字排序。</summary>
    public static List<ActionDefinition> CollectActions(CharacterConfig config, int modeIndex = -1)
    {
        var result = new List<ActionDefinition>();
        if (config == null) return result;
        if (config.CombatModes != null)
            foreach (CombatModeEntry entry in config.CombatModes.Entries.Where((_, index) => modeIndex < 0 || modeIndex == index))
                if (entry.ActionGraph != null)
                    foreach (ActionGraphNode node in entry.ActionGraph.Nodes)
                    {
                        if (node?.Action != null) result.Add(node.Action);
                        node?.VariantResolver?.CollectActions(result);
                    }
        config.Combat.Reactions.CollectActions(result);
        return result.Where(a => a != null).Distinct().OrderBy(a => a.name, StringComparer.Ordinal).ToList();
    }

    /// <summary>创建空角色草稿，仅连接默认模式的空图与移动配置，不复制其他角色内容。</summary>
    public static CharacterDefinition CreateCharacter(string characterId, string parentFolder, GameObject model = null)
    {
        string folder = CharacterAssetLayout.CharacterFolder(parentFolder, characterId);
        foreach (string guid in AssetDatabase.FindAssets("t:CharacterDefinition"))
        {
            var existing = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (existing != null && string.Equals(existing.Id.Value, characterId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("角色 Id 已存在：" + characterId);
        }
        if (Directory.Exists(folder) || File.Exists(folder))
            throw new InvalidOperationException("目标目录已存在：" + folder);
        var directories = new List<string>();
        var assets = new List<ScriptableObject>();
        bool ownsRoot = false;
        try
        {
            CharacterAssetLayout.EnsureFolder(folder, directories);
            ownsRoot = true;
            CharacterAssetLayout.EnsureBaseFolders(folder);
            var definition = CreateAsset<CharacterDefinition>(folder + "/" + characterId + "_Character.asset", assets);
            var config = CreateAsset<CharacterConfig>(folder + "/Config/" + characterId + "_Config.asset", assets);
            var graph = CreateAsset<ActionGraph>(folder + "/Graphs/" + characterId + "_Default_Graph.asset", assets);
            var locomotion = CreateAsset<CharacterLocomotionProfile>(folder + "/Locomotion/" + characterId + "_Default_Locomotion.asset", assets);
            var identity = new SerializedObject(definition);
            identity.FindProperty("characterId").stringValue = characterId;
            identity.FindProperty("characterConfig").objectReferenceValue = config;
            identity.ApplyModifiedPropertiesWithoutUndo();
            var body = new SerializedObject(config);
            body.FindProperty("modelPrefab").objectReferenceValue = model;
            body.FindProperty("combatModes.defaultMode").intValue = (int)CombatModeType.Default;
            var modes = body.FindProperty("combatModes.entries");
            modes.arraySize = 1;
            var mode = modes.GetArrayElementAtIndex(0);
            mode.FindPropertyRelative("mode").intValue = (int)CombatModeType.Default;
            mode.FindPropertyRelative("actionGraph").objectReferenceValue = graph;
            mode.FindPropertyRelative("locomotionProfile").objectReferenceValue = locomotion;
            body.ApplyModifiedPropertiesWithoutUndo();
            foreach (var asset in assets) { EditorUtility.SetDirty(asset); AssetDatabase.SaveAssetIfDirty(asset); }
            return definition;
        }
        catch
        {
            if (ownsRoot) AssetDatabase.DeleteAsset(folder);
            foreach (var asset in assets)
                if (asset != null && !EditorUtility.IsPersistent(asset)) Object.DestroyImmediate(asset);
            CharacterAssetLayout.CleanupEmptyFolders(directories);
            throw;
        }
    }

    static T CreateAsset<T>(string path, List<ScriptableObject> assets) where T : ScriptableObject
    {
        T asset = ScriptableObject.CreateInstance<T>();
        assets.Add(asset);
        asset.name = Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    /// <summary>按有序动画集合创建一个动作草稿并原子绑定到图或反应规则；不猜测命中与取消窗口。</summary>
    public static ActionDefinition CreateAction(CharacterConfig config, int modeIndex, IReadOnlyList<AnimationClip> clips,
        string actionName, CombatActionType type, GameplayIntentType intent, bool isEntry,
        bool reaction, CharacterReactionType reactionType)
    {
        if (config == null || !EditorUtility.IsPersistent(config) || clips == null || clips.Count == 0 || clips.Any(c => c == null))
            throw new ArgumentException("选择已保存角色与 Clip 后再创建。");
        if (string.IsNullOrWhiteSpace(actionName) || actionName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("动作名不能为空或包含路径字符。");
        actionName = CharacterAssetLayout.ActionName(config, actionName);
        EnsureActionNameAvailable(actionName);
        Object owner = config;
        if (!reaction)
        {
            if (config.CombatModes == null || modeIndex < 0 || modeIndex >= config.CombatModes.Entries.Count)
                throw new ArgumentException("模式未配置。");
            owner = config.CombatModes.Entries[modeIndex].ActionGraph;
            if (owner == null) throw new ArgumentException("当前模式未绑定动作图。");
        }
        string folder = CharacterAssetLayout.ActionFolder(config, reaction);
        var directories = new List<string>();
        string path = folder + "/" + actionName + ".asset";
        if (File.Exists(path)) throw new IOException("目标文件已存在。");
        string before = EditorJsonUtility.ToJson(owner);
        var action = ScriptableObject.CreateInstance<ActionDefinition>();
        bool created = false;
        try
        {
            CharacterAssetLayout.EnsureFolder(folder, directories);
            action.name = actionName;
            var so = new SerializedObject(action);
            so.FindProperty("actionType").intValue = (int)type;
            ActionAnimationSegmentCommands.InitializeDraft(so, clips);
            AssetDatabase.CreateAsset(action, path);
            created = true;
            Undo.RecordObject(owner, "Bind Created Action");
            var binding = new SerializedObject(owner);
            if (reaction)
            {
                SerializedProperty rules = binding.FindProperty("combat.reactions.rules");
                for (int i = 0; i < rules.arraySize; i++)
                {
                    SerializedProperty rule = rules.GetArrayElementAtIndex(i);
                    if (rule.FindPropertyRelative("reactionType").intValue == (int)reactionType
                        && rule.FindPropertyRelative("defaultRule").boolValue)
                        throw new InvalidOperationException("该反应已有默认动作，请在反应面板编辑，避免覆盖。");
                }
                int index = rules.arraySize++;
                SerializedProperty item = rules.GetArrayElementAtIndex(index);
                item.FindPropertyRelative("reactionType").intValue = (int)reactionType;
                item.FindPropertyRelative("reactionId").stringValue = "";
                item.FindPropertyRelative("defaultRule").boolValue = true;
                item.FindPropertyRelative("action").objectReferenceValue = action;
            }
            else
            {
                SerializedProperty nodes = binding.FindProperty("nodes");
                var graph = (ActionGraph)owner;
                if (graph.Nodes.Any(n => n != null && n.NodeId == actionName))
                    throw new InvalidOperationException("同名图节点已存在。");
                int index = nodes.arraySize++;
                SerializedProperty node = nodes.GetArrayElementAtIndex(index);
                node.FindPropertyRelative("nodeId").stringValue = actionName;
                node.FindPropertyRelative("action").objectReferenceValue = action;
                node.FindPropertyRelative("intent").intValue = (int)intent;
                node.FindPropertyRelative("isEntry").boolValue = isEntry;
                node.FindPropertyRelative("variantResolver").objectReferenceValue = null;
                ActionGraphView.ResetNodePolicy(node);
                node.FindPropertyRelative("editorPosition").vector2Value = new Vector2(80, 80 + index * 80);
            }
            binding.ApplyModifiedProperties();
            EditorUtility.SetDirty(owner);
            AssetDatabase.SaveAssetIfDirty(action);
            AssetDatabase.SaveAssetIfDirty(owner);
            return action;
        }
        catch
        {
            EditorJsonUtility.FromJsonOverwrite(before, owner);
            if (created) AssetDatabase.DeleteAsset(path); else Object.DestroyImmediate(action);
            CharacterAssetLayout.CleanupEmptyFolders(directories);
            throw;
        }
    }

    static void EnsureActionNameAvailable(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:ActionDefinition"))
        {
            var action = AssetDatabase.LoadAssetAtPath<ActionDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (action != null && string.Equals(action.name, name, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("动作名用于网络稳定身份，不能重复：" + name);
        }
    }

    /// <summary>收集已保存角色身体中受指定共享资产影响的配置。</summary>
    public static List<CharacterConfig> FindOwners(Object asset)
    {
        var result = new List<CharacterConfig>();
        foreach (string guid in AssetDatabase.FindAssets("t:CharacterConfig"))
        {
            var config = AssetDatabase.LoadAssetAtPath<CharacterConfig>(AssetDatabase.GUIDToAssetPath(guid));
            if (config == null) continue;
            if ((asset is ActionDefinition action && CollectActions(config).Contains(action))
                || (config.CombatModes != null && config.CombatModes.Entries.Any(e => e.LocomotionProfile == asset || e.ActionGraph == asset)))
                result.Add(config);
        }
        return result;
    }

    /// <summary>按显式用途批量生成图节点；任何失败恢复本批次引用并清理本批次新资产。</summary>
    public static List<ActionDefinition> CreateActions(CharacterConfig config, int mode, AnimationClip[] clips,
        string prefix, CombatActionType type, GameplayIntentType intent)
    {
        if (config?.CombatModes == null || mode < 0 || mode >= config.CombatModes.Entries.Count)
            throw new ArgumentException("模式未配置。");
        var graph = config.CombatModes.Entries[mode].ActionGraph;
        if (graph == null || clips == null || clips.Length == 0 || clips.Any(c => c == null))
            throw new ArgumentException("请选择动作图与非空 Clip 列表。");
        string before = EditorJsonUtility.ToJson(graph);
        var created = new List<ActionDefinition>();
        string folder = CharacterAssetLayout.ActionFolder(config, false);
        bool folderExisted = AssetDatabase.IsValidFolder(folder);
        try
        {
            for (int i = 0; i < clips.Length; i++)
                created.Add(CreateAction(config, mode, new[] { clips[i] }, prefix + "_" + (i + 1).ToString("D2"), type, intent, false, false, default));
            return created;
        }
        catch
        {
            EditorJsonUtility.FromJsonOverwrite(before, graph);
            EditorUtility.SetDirty(graph); AssetDatabase.SaveAssetIfDirty(graph);
            foreach (var action in created) AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(action));
            if (!folderExisted) CharacterAssetLayout.CleanupEmptyFolders(new List<string> { folder });
            throw;
        }
    }
}
