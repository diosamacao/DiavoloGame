using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Gameplay 内容唯一构建入口：扫描当前场景声明、集中校验、登记并冻结 Catalog。</summary>
public static class GameContentBootstrap
{
    /// <summary>验证当前场景的 Party、Character、Enemy、CombatMode、Locomotion 与 Action，并返回冻结目录。</summary>
    public static GameContentCatalog ValidateAndBuild(UnityEngine.Object context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        Scene scene = ResolveScene(context);
        if (!scene.IsValid() || !scene.isLoaded)
            throw new InvalidOperationException("Gameplay Content Build 需要已加载的有效场景。");

        var catalog = new GameContentCatalog();
        var enemyDefinitions = new List<EnemyDefinition>();
        var visitedEnemies = new HashSet<EnemyDefinition>();
        PartyLoadout playerLoadout = null;
        bool valid = true;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            PlayerController[] players = roots[i].GetComponentsInChildren<PlayerController>(true);
            for (int p = 0; p < players.Length; p++)
            {
                PartyLoadout loadout = players[p].PartyLoadout;
                if (loadout == null)
                    continue;
                if (playerLoadout != null && playerLoadout != loadout)
                {
                    throw new InvalidOperationException(
                        "GameContentBootstrap: 场景声明了多份不同 PartyLoadout。");
                }
                playerLoadout = loadout;
            }

            EnemySpawnController[] spawns =
                roots[i].GetComponentsInChildren<EnemySpawnController>(true);
            for (int s = 0; s < spawns.Length; s++)
            {
                enemyDefinitions.Clear();
                spawns[s].CollectDefinitions(enemyDefinitions);
                for (int e = 0; e < enemyDefinitions.Count; e++)
                {
                    EnemyDefinition definition = enemyDefinitions[e];
                    if (definition != null)
                        visitedEnemies.Add(definition);
                }
            }
        }

        if (playerLoadout == null)
        {
            Debug.LogError("GameContentBootstrap: 场景未声明 PartyLoadout。", context);
            valid = false;
        }
        else
        {
            valid &= ValidateLoadout(playerLoadout, context);
            catalog.AddPlayerLoadout(playerLoadout);
        }

        // Unity 场景根顺序不参与稳定 Id；敌人按 Ordinal 资产名统一登记。
        enemyDefinitions.Clear();
        enemyDefinitions.AddRange(visitedEnemies);
        enemyDefinitions.Sort(
            (left, right) => string.CompareOrdinal(left.name, right.name));
        for (int i = 0; i < enemyDefinitions.Count; i++)
        {
            EnemyDefinition definition = enemyDefinitions[i];
            valid &= definition.Validate(context);
            catalog.AddEnemy(definition);
        }

        if (!valid)
            throw new InvalidOperationException("Gameplay 内容校验失败；请修复此前输出的配置错误。");

        catalog.Freeze();
        return catalog;
    }

    /// <summary>集中校验阵容及各玩家角色的 Gameplay 内容，不把 InputAction 当 Dedicated 内容。</summary>
    static bool ValidateLoadout(PartyLoadout loadout, UnityEngine.Object context)
    {
        if (!loadout.Validate(context))
            return false;

        bool valid = true;
        IReadOnlyList<CharacterDefinition> members = loadout.Members;
        for (int i = 0; i < members.Count; i++)
        {
            CharacterConfig config = members[i]?.CharacterConfig;
            if (config != null && !config.ValidateGameplayContent(context))
                valid = false;
        }
        return valid;
    }

    /// <summary>从 Component/GameObject 上下文取得所属场景，其他对象使用当前活动场景。</summary>
    static Scene ResolveScene(UnityEngine.Object context)
    {
        if (context is Component component)
            return component.gameObject.scene;
        if (context is GameObject gameObject)
            return gameObject.scene;
        return SceneManager.GetActiveScene();
    }
}
