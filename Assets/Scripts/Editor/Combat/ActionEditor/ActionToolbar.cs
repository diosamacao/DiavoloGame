using UnityEditor;
using UnityEngine;

/// <summary>Action Editor 顶部工具栏：预览角色、帧控制与播放。</summary>
public sealed class ActionToolbar
{
    /// <summary>绘制工具栏；返回是否发生会影响预览的变更。</summary>
    public bool Draw(
        ActionDefinition action,
        Transform previewCharacter,
        ref int previewFrame,
        ref bool isPlaying,
        ref bool loop,
        CharacterConfig character,
        System.Action<Transform> selectScenePreview,
        System.Action selectIsolatedPreview)
    {
        bool changed = false;
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (character != null && GUILayout.Button("角色 / 连招 / 反应", EditorStyles.toolbarButton))
            CharacterAuthoringWindow.Open(character);

        EditorGUI.BeginChangeCheck();
        Transform selected = (Transform)EditorGUILayout.ObjectField(
            previewCharacter,
            typeof(Transform),
            true,
            GUILayout.Width(180f));
        if (EditorGUI.EndChangeCheck())
        {
            if (selected == null || (!EditorUtility.IsPersistent(selected)
                && selected.gameObject.scene.IsValid()
                && !UnityEditor.SceneManagement.EditorSceneManager.IsPreviewSceneObject(selected.gameObject)))
                selectScenePreview?.Invoke(selected);
            changed = true;
        }
        if (character != null && GUILayout.Button("隔离预览", EditorStyles.toolbarButton, GUILayout.Width(64)))
        {
            selectIsolatedPreview?.Invoke();
            changed = true;
        }

        int maxFrame = action != null ? Mathf.Max(0, action.TotalFrames - 1) : 0;
        EditorGUI.BeginChangeCheck();
        previewFrame = EditorGUILayout.IntSlider(previewFrame, 0, maxFrame, GUILayout.MinWidth(220f));
        if (EditorGUI.EndChangeCheck())
        {
            isPlaying = false;
            changed = true;
        }

        if (GUILayout.Button("◀", EditorStyles.toolbarButton, GUILayout.Width(28f)))
        {
            previewFrame = Mathf.Max(0, previewFrame - 1);
            isPlaying = false;
            changed = true;
        }

        if (GUILayout.Button(isPlaying ? "⏸" : "▶", EditorStyles.toolbarButton, GUILayout.Width(28f)))
        {
            isPlaying = !isPlaying;
            changed = true;
        }

        if (GUILayout.Button("▶|", EditorStyles.toolbarButton, GUILayout.Width(28f)))
        {
            previewFrame = Mathf.Min(maxFrame, previewFrame + 1);
            isPlaying = false;
            changed = true;
        }

        loop = GUILayout.Toggle(loop, "Loop", EditorStyles.toolbarButton, GUILayout.Width(44f));

        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField(
            action != null ? $"{action.name}  ({previewFrame}/{maxFrame})" : "No Action",
            EditorStyles.miniLabel,
            GUILayout.Width(220f));

        EditorGUILayout.EndHorizontal();
        return changed;
    }
}
