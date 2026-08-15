using UnityEditor;
using UnityEngine;

/// <summary>
/// 无 ParrelSync 时用 EditorPrefs 切角色。克隆编辑器由运行时自动当 Client，不必点菜单。
/// </summary>
public static class ReplicationRoomMenu
{
    const string HostMenu = "ACTGame/Room/Use Listen Host";
    const string ClientMenu = "ACTGame/Room/Use Client (127.0.0.1)";
    const string ClearMenu = "ACTGame/Room/Clear Role Override";

    /// <summary>勾选本机为 Listen Host；默认一人房间。</summary>
    [MenuItem(HostMenu)]
    public static void UseListenHost()
    {
        EditorPrefs.SetInt(ReplicationRoomLaunchSettings.RolePrefsKey, (int)ReplicationRole.ListenHost);
        EditorPrefs.SetString(ReplicationRoomLaunchSettings.HostPrefsKey, "127.0.0.1");
        EditorPrefs.SetInt(ReplicationRoomLaunchSettings.PortPrefsKey, ReplicationRoomProtocol.DefaultPort);
        Debug.Log("[Room] 下次 Play 以 Listen Host 启动（可等人加入）。");
    }

    /// <summary>Host 菜单勾选状态。</summary>
    [MenuItem(HostMenu, true)]
    public static bool UseListenHostValidate()
    {
        Menu.SetChecked(HostMenu, GetRole() == ReplicationRole.ListenHost);
        return true;
    }

    /// <summary>勾选本机为客机，连接本机 127.0.0.1。</summary>
    [MenuItem(ClientMenu)]
    public static void UseClient()
    {
        EditorPrefs.SetInt(ReplicationRoomLaunchSettings.RolePrefsKey, (int)ReplicationRole.Client);
        EditorPrefs.SetString(ReplicationRoomLaunchSettings.HostPrefsKey, "127.0.0.1");
        EditorPrefs.SetInt(ReplicationRoomLaunchSettings.PortPrefsKey, ReplicationRoomProtocol.DefaultPort);
        Debug.Log("[Room] 下次 Play 以 Client 连接 127.0.0.1:7777。请先在另一编辑器以 Host 进入 Play。");
    }

    /// <summary>Client 菜单勾选状态。</summary>
    [MenuItem(ClientMenu, true)]
    public static bool UseClientValidate()
    {
        Menu.SetChecked(ClientMenu, GetRole() == ReplicationRole.Client);
        return true;
    }

    /// <summary>删除角色覆盖，回到场景默认 Listen Host。</summary>
    [MenuItem(ClearMenu)]
    public static void ClearOverride()
    {
        EditorPrefs.DeleteKey(ReplicationRoomLaunchSettings.RolePrefsKey);
        EditorPrefs.DeleteKey(ReplicationRoomLaunchSettings.HostPrefsKey);
        EditorPrefs.DeleteKey(ReplicationRoomLaunchSettings.PortPrefsKey);
        Debug.Log("[Room] 已清除角色覆盖。");
    }

    static ReplicationRole GetRole() =>
        (ReplicationRole)EditorPrefs.GetInt(
            ReplicationRoomLaunchSettings.RolePrefsKey,
            (int)ReplicationRole.ListenHost);
}
