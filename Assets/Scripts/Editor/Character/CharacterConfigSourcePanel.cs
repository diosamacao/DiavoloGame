using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>显示实际配置来源和共用身体配置；短期缓存引用扫描，避免每次重绘遍历项目。</summary>
public sealed class CharacterConfigSourcePanel
{
    readonly Dictionary<Object, List<CharacterConfig>> owners = new();
    double expiresAt;

    /// <summary>根据引用数量与实际路径描述来源；共享目录和多角色共用分别标明。</summary>
    public static string Describe(Object asset, int ownerCount)
    {
        if (asset == null) return "尚未配置";
        string path = AssetDatabase.GetAssetPath(asset);
        string source = path.StartsWith("Assets/Data/Shared/") ? "共享目录" : "角色配置";
        if (ownerCount > 1) source += " · 多角色共用";
        return source + $" · {ownerCount} 个身体配置引用";
    }

    /// <summary>清除缓存，供切换角色或配置修改后立即刷新。</summary>
    public void Invalidate() => owners.Clear();

    /// <summary>只读展示绑定资产、保存路径和影响范围；定位按钮打开实际资产。</summary>
    public void Draw(string label, Object asset)
    {
        if (EditorApplication.timeSinceStartup >= expiresAt)
        {
            owners.Clear();
            expiresAt = EditorApplication.timeSinceStartup + 2;
        }
        if (asset == null)
        {
            EditorGUILayout.LabelField(label, "尚未配置");
            return;
        }
        if (!owners.TryGetValue(asset, out var references))
            owners[asset] = references = CharacterAuthoringService.FindOwners(asset);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(label + "：" + Describe(asset, references.Count), EditorStyles.wordWrappedLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField(asset, asset.GetType(), false);
                if (GUILayout.Button("定位", GUILayout.Width(45)))
                {
                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);
                }
            }
            EditorGUILayout.LabelField(AssetDatabase.GetAssetPath(asset), EditorStyles.wordWrappedMiniLabel);
            if (references.Count > 1)
                EditorGUILayout.HelpBox("修改此配置会同时影响：" + string.Join("、", references.Select(c => c.name)), MessageType.Info);
        }
    }
}
