using System.Collections.Generic;
using UnityEngine;

/// <summary>扫描场景内玩家/刷怪配置以预填 Gameplay Registry；含未激活物体，不启用本机座位。</summary>
public static class ActServerContentProbe
{
    /// <summary>把场景声明的 CharacterConfig / EnemyDefinition 写入 Registry 并预填动作。</summary>
    public static void PrefillFromScene(ActContentRegistry content)
    {
        if (content == null)
            return;

        PlayerController[] players = Object.FindObjectsOfType<PlayerController>(true);
        for (int i = 0; i < players.Length; i++)
        {
            CharacterConfig config = players[i] != null ? players[i].CharacterConfig : null;
            if (config == null)
                continue;
            content.RegisterPlayer(config);
            content.PrefillActions(config);
        }

        var definitions = new List<EnemyDefinition>();
        EnemySpawnController[] spawns = Object.FindObjectsOfType<EnemySpawnController>(true);
        for (int i = 0; i < spawns.Length; i++)
        {
            if (spawns[i] == null)
                continue;
            definitions.Clear();
            spawns[i].CollectDefinitions(definitions);
            for (int d = 0; d < definitions.Count; d++)
            {
                EnemyDefinition definition = definitions[d];
                if (definition == null || definition.CharacterConfig == null)
                    continue;
                content.RegisterEnemy(definition);
                content.PrefillActions(definition.CharacterConfig);
            }
        }
    }
}
