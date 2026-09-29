using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>聚合现有内容校验和结构化图问题；不复制 Runtime 校验算法。</summary>
public static class CharacterValidationPanel
{
    /// <summary>在指定资产上下文收集问题；原有日志仍保留，方便 Test Runner 和启动诊断。</summary>
    public static List<ContentValidationIssue> Collect(Object root, CharacterConfig config)
    {
        var issues = new List<ContentValidationIssue>();
        void Capture(Object asset, string code, string path, Func<bool> validate)
        {
            void Receive(string message, string trace, LogType type)
            {
                if ((type == LogType.Error || type == LogType.Warning || type == LogType.Exception)
                    && !message.StartsWith("["))
                    issues.Add(new ContentValidationIssue(code, asset, path, message, severity:
                        type == LogType.Warning ? ContentIssueSeverity.Warning : ContentIssueSeverity.Error));
            }
            Application.logMessageReceived += Receive;
            int before = issues.Count;
            try
            {
                if (!validate() && issues.Count == before)
                    issues.Add(new ContentValidationIssue(code, asset, path, "内容校验未通过，详见 Console 的原始诊断。"));
            }
            finally { Application.logMessageReceived -= Receive; }
        }
        if (root is CharacterDefinition character)
            Capture(character, "Character.Identity", "characterId", () => character.Validate(character));
        if (config.ModelPrefab == null)
            issues.Add(new ContentValidationIssue("Character.Model", config, "modelPrefab", "模型未配置。"));
        Capture(config, "Character.Content", "combatModes", () => config.ValidateGameplayContent(config));
        foreach (var entry in config.CombatModes?.Entries ?? Array.Empty<CombatModeEntry>())
        {
            if (entry.ActionGraph != null) issues.AddRange(ActionGraphValidator.Validate(entry.ActionGraph));
            if (entry.LocomotionProfile != null)
                Capture(entry.LocomotionProfile, "Locomotion.Content", "clipTimings", () => entry.LocomotionProfile.Validate(entry.LocomotionProfile));
        }
        foreach (var action in CharacterAuthoringService.CollectActions(config))
            Capture(action, "Action.Content", "timeline", () => action.ValidateContent(action));
        return issues.GroupBy(i => $"{i.Asset?.GetInstanceID()}|{i.Code}|{i.PropertyPath}|{i.Message}").Select(g => g.First()).ToList();
    }
}
