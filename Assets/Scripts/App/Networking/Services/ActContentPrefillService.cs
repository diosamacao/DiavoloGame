using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>扫描当前 ACT 场景并把玩家、敌人 Archetype 与动作资产预填到唯一内容 Registry。</summary>
public sealed class ActContentPrefillService
{
    readonly ACTGameArchitecture _architecture;
    readonly ActContentRegistry _content;
    readonly List<EnemyDefinition> _enemyDefinitions = new();

    /// <summary>创建绑定 Architecture 查询入口与当前房间内容 Registry 的预填服务。</summary>
    public ActContentPrefillService(
        ACTGameArchitecture architecture,
        ActContentRegistry content)
    {
        _architecture = architecture ?? throw new ArgumentNullException(nameof(architecture));
        _content = content ?? throw new ArgumentNullException(nameof(content));
    }

    /// <summary>当前本机 PlayerController；尚未登记时返回 null。</summary>
    public PlayerController LocalPlayer =>
        _architecture.SendQuery(new GetLocalPlayerQuery()) as PlayerController;

    /// <summary>对称登记本机玩家与场景刷怪表声明的全部角色及动作内容。</summary>
    public void InitializeFromScene()
    {
        PlayerController player = LocalPlayer;
        if (player?.PartyLoadout != null)
        {
            _content.RegisterPlayerLoadout(player.PartyLoadout);
            PrefillPlayerActions(player);
        }

        VisitEnemyDefinitions(
            definition =>
            {
                _content.RegisterEnemy(definition);
                _content.PrefillActions(definition.CharacterConfig);
            });
    }

    /// <summary>动作目录为空时从本机与刷怪定义补齐，避免运行时新连接缺少变体 Id。</summary>
    public void EnsureActionsReady()
    {
        if (_content.ActionCount > 0)
            return;

        PlayerController player = LocalPlayer;
        if (player != null)
            PrefillPlayerActions(player);
        VisitEnemyDefinitions(
            definition => _content.PrefillActions(definition.CharacterConfig));
    }

    /// <summary>
    /// 预填阵容全部角色动作；角色 Archetype 已由 RegisterPlayerLoadout 统一登记。
    /// </summary>
    void PrefillPlayerActions(PlayerController player)
    {
        if (player?.PartyLoadout == null)
            return;

        IReadOnlyList<CharacterDefinition> members = player.PartyLoadout.Members;
        for (int i = 0; i < members.Count; i++)
        {
            CharacterConfig config = members[i]?.CharacterConfig;
            if (config != null)
                _content.PrefillActions(config);
        }
    }

    /// <summary>遍历全部 EnemySpawnController 声明的定义，并跳过空条目。</summary>
    void VisitEnemyDefinitions(Action<EnemyDefinition> visitor)
    {
        EnemySpawnController[] spawns =
            UnityEngine.Object.FindObjectsOfType<EnemySpawnController>();
        for (int i = 0; i < spawns.Length; i++)
        {
            _enemyDefinitions.Clear();
            spawns[i].CollectDefinitions(_enemyDefinitions);
            for (int d = 0; d < _enemyDefinitions.Count; d++)
            {
                EnemyDefinition definition = _enemyDefinitions[d];
                if (definition != null)
                    visitor(definition);
            }
        }
    }
}
