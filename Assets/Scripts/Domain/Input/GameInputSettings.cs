using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 项目唯一 InputActionAsset 访问点；角色 Config 不再挂 InputActions。
/// 正式路径：Resources/ACT/GameInputActions；Editor 可回退查找。
/// </summary>
public static class GameInputSettings
{
    /// <summary>Resources.Load 相对路径（不含扩展名）。</summary>
    public const string ResourcesPath = "ACT/GameInputActions";

    static InputActionAsset _cached;

    /// <summary>全局输入资产；缺失时打 Error 并返回 null。</summary>
    public static InputActionAsset Active
    {
        get
        {
            if (_cached != null)
                return _cached;

            _cached = Resources.Load<InputActionAsset>(ResourcesPath);
#if UNITY_EDITOR
            if (_cached == null)
                _cached = FindEditorFallback();
#endif
            if (_cached == null)
            {
                Debug.LogError(
                    "GameInputSettings: 未找到全局 InputActionAsset。" +
                    $"请放到 Assets/Resources/{ResourcesPath}.inputactions，或运行菜单 ACTGame/Input/Migrate Input Actions To Resources。");
            }

            return _cached;
        }
    }

    /// <summary>测试或资产热重载后清空缓存。</summary>
    public static void ClearCache() => _cached = null;

#if UNITY_EDITOR
    static InputActionAsset FindEditorFallback()
    {
        string[] guids = AssetDatabase.FindAssets("t:InputActionAsset");
        if (guids == null || guids.Length == 0)
            return null;

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
        if (guids.Length > 1)
        {
            Debug.LogWarning(
                $"GameInputSettings: 找到 {guids.Length} 个 InputActionAsset，Editor 暂用 {path}。" +
                "请迁到 Resources 并保证项目唯一。",
                asset);
        }
        else
        {
            Debug.LogWarning(
                $"GameInputSettings: 正在使用 {path}（尚未迁入 Resources/{ResourcesPath}）。" +
                "打包前请执行 ACTGame/Input/Migrate Input Actions To Resources。",
                asset);
        }

        return asset;
    }
#endif
}
