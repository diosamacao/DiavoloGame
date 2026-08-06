using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Wave 0：展示 Action 位移源全库审计报告。</summary>
public sealed class ActionDefinitionAuditWindow : EditorWindow
{
    string _report = string.Empty;
    Vector2 _scroll;
    List<ActionDefinitionAuditEntry> _entries;

    /// <summary>打开窗口并填入报告。</summary>
    public static void ShowReport(string report, List<ActionDefinitionAuditEntry> entries)
    {
        var window = GetWindow<ActionDefinitionAuditWindow>(true, "Action Motion Audit", true);
        window._report = report ?? string.Empty;
        window._entries = entries;
        window.minSize = new Vector2(640f, 420f);
        window.Show();
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Wave 0 — Action Motion Source Audit", EditorStyles.boldLabel);
        if (GUILayout.Button("Rescan Project"))
        {
            _entries = ActionDefinitionAuditUtility.AuditProject();
            _report = ActionDefinitionAuditUtility.BuildReport(_entries);
            Debug.Log(_report);
        }

        if (_entries != null)
        {
            int conflict = 0;
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].MotionSourceKind == ActionMotionSourceKind.Conflict)
                    conflict++;
            }

            EditorGUILayout.HelpBox(
                $"Entries={_entries.Count}, Conflict={conflict}. 详细分类见下方文本；Conflict 须在 Wave 1/2 前人工决策。",
                conflict > 0 ? MessageType.Warning : MessageType.Info);
        }

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        EditorGUILayout.TextArea(_report, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }
}
