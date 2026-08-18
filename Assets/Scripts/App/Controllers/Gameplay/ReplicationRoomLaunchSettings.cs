/// <summary>
/// Editor 入房覆盖：ParrelSync 克隆优先当 Client；否则读菜单写入的 EditorPrefs。
/// 不硬引用 ParrelSync 程序集。
/// </summary>
public static class ReplicationRoomLaunchSettings
{
    /// <summary>0=ListenHost，1=Client，2=DedicatedServer。</summary>
    public const string RolePrefsKey = "ACTGame.Room.Role";

    /// <summary>客机连接地址。</summary>
    public const string HostPrefsKey = "ACTGame.Room.Host";

    /// <summary>UDP 端口。</summary>
    public const string PortPrefsKey = "ACTGame.Room.Port";

    /// <summary>
    /// 把 Editor 启动覆盖写回参数。克隆编辑器固定 Client→127.0.0.1，避免与原工程抢 EditorPrefs。
    /// </summary>
    public static void ApplyEditorOverride(ref ReplicationRole role, ref string host, ref int port)
    {
#if UNITY_EDITOR
        if (IsParrelSyncClone())
        {
            role = ReplicationRole.Client;
            host = "127.0.0.1";
            port = ReplicationRoomProtocol.DefaultPort;
            return;
        }

        if (UnityEditor.EditorPrefs.HasKey(RolePrefsKey))
            role = (ReplicationRole)UnityEditor.EditorPrefs.GetInt(RolePrefsKey, (int)role);
        if (UnityEditor.EditorPrefs.HasKey(HostPrefsKey))
            host = UnityEditor.EditorPrefs.GetString(HostPrefsKey, host);
        if (UnityEditor.EditorPrefs.HasKey(PortPrefsKey))
            port = UnityEditor.EditorPrefs.GetInt(PortPrefsKey, port);
#endif
    }

#if UNITY_EDITOR
    /// <summary>反射探测 ParrelSync.ClonesManager.IsClone；未安装包时为 false。</summary>
    public static bool IsParrelSyncClone()
    {
        System.Type type = System.Type.GetType("ParrelSync.ClonesManager, ParrelSync");
        if (type == null)
            return false;

        var method = type.GetMethod(
            "IsClone",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        if (method == null)
            return false;

        return method.Invoke(null, null) is bool clone && clone;
    }
#endif
}
