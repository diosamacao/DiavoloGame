using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 架构级玩家花名册：登记本机 Local 与全部玩家入口，供相机/HUD/敌人感知查询。
/// 禁止玩法再 FindObjectOfType&lt;PlayerController&gt;。
/// </summary>
public sealed class LocalPlayerService : ArchitectureSystemBase
{
    static readonly Transform[] EmptyRoots = Array.Empty<Transform>();

    readonly List<ILocalPlayer> _players = new();
    readonly List<Transform> _playerRoots = new();
    ILocalPlayer _local;

    /// <summary>当前拥有输入与相机的玩家；尚未登记时为空。</summary>
    public ILocalPlayer Local => IsDestroyed(_local) ? null : _local;

    /// <summary>场上全部已登记玩家（含本机）。</summary>
    public IReadOnlyList<ILocalPlayer> Players => _players;

    /// <summary>全部玩家权威根；感知每帧只读此缓存，不分配。</summary>
    public IReadOnlyList<Transform> PlayerRoots =>
        _playerRoots.Count == 0 ? EmptyRoots : _playerRoots;

    /// <summary>初始化花名册；无额外启动逻辑。</summary>
    protected override void OnInit() { }

    /// <summary>登记玩家；isLocalOwner 为 true 时同时设为相机/输入拥有者。</summary>
    public void Register(ILocalPlayer player, bool isLocalOwner)
    {
        if (player == null)
            return;

        if (!_players.Contains(player))
            _players.Add(player);

        if (isLocalOwner)
            _local = player;

        RebuildPlayerRoots();
    }

    /// <summary>注销玩家；若为本机 Local 则清空，不自动改认其他人。</summary>
    public void Unregister(ILocalPlayer player)
    {
        if (player == null)
            return;

        _players.Remove(player);
        if (ReferenceEquals(_local, player))
            _local = null;

        RebuildPlayerRoots();
    }

    /// <summary>用当前仍有效的玩家根重建感知列表。</summary>
    void RebuildPlayerRoots()
    {
        _playerRoots.Clear();
        for (int i = _players.Count - 1; i >= 0; i--)
        {
            ILocalPlayer player = _players[i];
            if (IsDestroyed(player))
            {
                _players.RemoveAt(i);
                continue;
            }

            Transform root = player.Root;
            if (root != null)
                _playerRoots.Add(root);
        }
    }

    /// <summary>Unity 对象销毁后接口引用仍非 C# null，必须走 Object 重载。</summary>
    static bool IsDestroyed(ILocalPlayer player)
    {
        return player is UnityEngine.Object obj && obj == null;
    }
}
