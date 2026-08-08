using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 项目唯一 GameplayIntentProfile 访问点；角色 Config 不再挂 Intent。
/// 正式路径：Resources/ACT/GameplayIntentProfile；Editor 下可回退查找唯一资产。
/// </summary>
public static class GameplayIntentSettings
{
    /// <summary>Resources.Load 相对路径（不含扩展名）。</summary>
    public const string ResourcesPath = "ACT/GameplayIntentProfile";

    static GameplayIntentProfile _cached;

    /// <summary>全局意图映射；缺失时打 Error 并返回 null。</summary>
    public static GameplayIntentProfile Active
    {
        get
        {
            if (_cached != null)
                return _cached;

            _cached = Resources.Load<GameplayIntentProfile>(ResourcesPath);
#if UNITY_EDITOR
            if (_cached == null)
                _cached = FindEditorFallback();
#endif
            if (_cached == null)
            {
                Debug.LogError(
                    "GameplayIntentSettings: 未找到全局 GameplayIntentProfile。" +
                    $"请放到 Assets/Resources/{ResourcesPath}.asset，或运行菜单 ACTGame/Input/Migrate Intent Profile To Resources。");
            }

            return _cached;
        }
    }

    /// <summary>测试或资产热重载后清空缓存。</summary>
    public static void ClearCache() => _cached = null;

#if UNITY_EDITOR
    /// <summary>Editor Play：若尚未迁入 Resources，使用项目中唯一（或首个）Intent 资产。</summary>
    static GameplayIntentProfile FindEditorFallback()
    {
        string[] guids = AssetDatabase.FindAssets("t:GameplayIntentProfile");
        if (guids == null || guids.Length == 0)
            return null;

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        var profile = AssetDatabase.LoadAssetAtPath<GameplayIntentProfile>(path);
        if (guids.Length > 1)
        {
            Debug.LogWarning(
                $"GameplayIntentSettings: 找到 {guids.Length} 个 GameplayIntentProfile，Editor 暂用 {path}。" +
                "请迁到 Resources 并删除多余副本。",
                profile);
        }
        else
        {
            Debug.LogWarning(
                $"GameplayIntentSettings: 正在使用 {path}（尚未迁入 Resources/{ResourcesPath}）。" +
                "打包前请执行 ACTGame/Input/Migrate Intent Profile To Resources。",
                profile);
        }

        return profile;
    }
#endif
}
