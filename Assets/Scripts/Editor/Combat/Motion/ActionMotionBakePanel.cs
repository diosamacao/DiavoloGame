using System;
using UnityEditor;
using UnityEngine;

/// <summary>动作编辑器与 Inspector 共用的单招位移烘焙面板；复用现有匹配、指纹和写回服务。</summary>
public sealed class ActionMotionBakePanel
{
    ActionDefinition target;
    CharacterConfig owner;
    CharacterConfig preferenceOwner;
    DefaultAsset folder;
    ActionMotionPlanarMode mode;
    string message;
    bool succeeded, dirty;
    int dirtyCount = -1;
    double nextCheck;

    /// <summary>切换动作时读取已有模式和该角色的 RM 路径；独立动作使用自身路径偏好。</summary>
    public void Bind(ActionDefinition action, CharacterConfig context = null)
    {
        if (target == action && owner == context) return;
        target = action; owner = context;
        preferenceOwner = context;
        if (preferenceOwner == null && action != null)
        {
            var owners = CharacterAuthoringService.FindOwners(action);
            if (owners.Count == 1) preferenceOwner = owners[0];
        }
        mode = action != null && action.BakedMotion.IsReady
            ? action.BakedMotion.planarMode : ActionMotionPlanarMode.EndpointSigned;
        folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(CharacterAnimationSourcePreferences.Get(PreferenceScope, true));
        message = null; dirtyCount = -1; nextCheck = 0;
    }

    UnityEngine.Object PreferenceScope => preferenceOwner != null ? (UnityEngine.Object)preferenceOwner : target;

    /// <summary>显示来源、状态和烘焙命令；成功返回 true，宿主应重新读取序列化数据并刷新预览。</summary>
    public bool Draw(ActionDefinition action, CharacterConfig context = null, Action beforeBake = null)
    {
        Bind(action, context);
        if (action == null) return false;
        EditorGUILayout.LabelField("位移烘焙", EditorStyles.boldLabel);
        CharacterAnimationSourcePreferences.DrawAnimationFolder(preferenceOwner);
        var savedFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(CharacterAnimationSourcePreferences.Get(PreferenceScope, true));
        if (folder != savedFolder) { folder = savedFolder; nextCheck = 0; }
        EditorGUI.BeginChangeCheck();
        folder = (DefaultAsset)EditorGUILayout.ObjectField("RM 文件夹", folder, typeof(DefaultAsset), false);
        if (EditorGUI.EndChangeCheck())
        {
            CharacterAnimationSourcePreferences.Set(PreferenceScope, true, AssetDatabase.GetAssetPath(folder));
            nextCheck = 0; message = null;
        }
        string path = AssetDatabase.GetAssetPath(folder);
        bool validFolder = AssetDatabase.IsValidFolder(path);
        mode = (ActionMotionPlanarMode)EditorGUILayout.EnumPopup("位移模式", mode);
        EditorGUILayout.HelpBox("EndpointSigned：沿起终点连线保留推进与回撤，末帧残差归零；FullPlanar：保留完整水平轨迹。仅烘焙水平位移，不烘焙转向。", MessageType.None);
        var motion = action.BakedMotion;
        if (validFolder && (dirtyCount != EditorUtility.GetDirtyCount(action) || EditorApplication.timeSinceStartup >= nextCheck))
        {
            dirty = ActionMotionDirtyUtility.IsDirty(action, path, ActionSim.LogicHz);
            dirtyCount = EditorUtility.GetDirtyCount(action);
            nextCheck = EditorApplication.timeSinceStartup + 2;
        }
        string status = !motion.IsReady ? "未烘焙 / 未就绪"
            : !validFolder ? "已有烘焙，待指定 RM 来源验证"
            : dirty || motion.planarMode != mode ? "需要重新烘焙" : "与来源一致";
        EditorGUILayout.LabelField("状态", status);
        EditorGUILayout.LabelField("帧数", $"{motion.frameCount} / {action.TotalFrames} · {ActionSim.LogicHz} Hz");
        EditorGUILayout.LabelField("已烘焙模式", motion.IsReady ? motion.planarMode.ToString() : "—");
        EditorGUILayout.LabelField("匹配 RM", motion.matchedRootMotionName ?? "", EditorStyles.wordWrappedLabel);
        if (!validFolder) EditorGUILayout.HelpBox("请选择 Project 中有效的 RootMotion 文件夹。", MessageType.Warning);
        EditorGUILayout.HelpBox("烘焙会写回并保存当前动作，支持 Undo；共享动作的引用方会一起生效。", MessageType.Info);
        bool changed = false;
        using (new EditorGUI.DisabledScope(!validFolder || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling))
        {
            if (GUILayout.Button("烘焙当前动作", GUILayout.Height(26)))
            {
                changed = Bake(beforeBake);
            }
        }
        if (GUILayout.Button("重新检查来源")) { nextCheck = 0; message = null; }
        if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, succeeded ? MessageType.Info : MessageType.Error);
        return changed;
    }

    /// <summary>烘焙 Bind 指定的当前动作；失败保留旧表，回调允许宿主先提交属性和暂停预览。</summary>
    public bool Bake(Action beforeBake = null)
    {
        string path = AssetDatabase.GetAssetPath(folder);
        if (target == null || !AssetDatabase.IsValidFolder(path)
            || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        { succeeded = false; message = "当前无法烘焙，请检查动作、RM 文件夹及编辑器状态。"; return false; }
        beforeBake?.Invoke();
        succeeded = ActionMotionBakeService.BakeAction(target, path, mode, ActionSim.LogicHz, out message);
        nextCheck = 0;
        return succeeded;
    }
}
