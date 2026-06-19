using UnityEditor;
using UnityEngine;

/// <summary>
/// ActionDefinition Hitbox 预览编辑器：帧 Scrub + Scene Handles 快捷调整 offset / rotation / size。
/// </summary>
[CustomEditor(typeof(ActionDefinition))]
public class ActionDefinitionHitboxEditor : Editor
{
    const string PreviewCharacterPrefKey = "ACTGame.ActionDefinitionHitboxEditor.PreviewCharacter";

    SerializedProperty _hitboxesProp;
    Transform _previewCharacter;
    int _previewFrame;
    int _selectedHitboxIndex;

    void OnEnable()
    {
        _hitboxesProp = serializedObject.FindProperty("hitboxes");
        SceneView.duringSceneGui += OnSceneGUI;
        RestorePreviewCharacter();
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        SavePreviewCharacter();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Hitbox 预览", EditorStyles.boldLabel);

        ActionDefinition action = (ActionDefinition)target;
        int maxFrame = Mathf.Max(0, action.TotalFrames - 1);

        EditorGUI.BeginChangeCheck();
        _previewCharacter = (Transform)EditorGUILayout.ObjectField(
            "Preview Character",
            _previewCharacter,
            typeof(Transform),
            true);

        _previewFrame = EditorGUILayout.IntSlider("Preview Frame", _previewFrame, 0, maxFrame);

        int hitboxCount = _hitboxesProp != null ? _hitboxesProp.arraySize : 0;
        if (hitboxCount > 0)
        {
            _selectedHitboxIndex = EditorGUILayout.IntSlider(
                "Selected Hitbox",
                Mathf.Clamp(_selectedHitboxIndex, 0, hitboxCount - 1),
                0,
                hitboxCount - 1);
        }
        else
        {
            EditorGUILayout.HelpBox("在 Hitboxes 列表中添加至少一条 HitboxKeyframe。", MessageType.Info);
        }

        if (EditorGUI.EndChangeCheck())
        {
            SavePreviewCharacter();
            SceneView.RepaintAll();
        }

        if (_previewCharacter == null)
        {
            EditorGUILayout.HelpBox(
                "拖入场景中的 Player Transform 以在 Scene 视图预览与编辑 Hitbox。",
                MessageType.Warning);
        }

        serializedObject.ApplyModifiedProperties();
    }

    void OnSceneGUI(SceneView sceneView)
    {
        if (target is not ActionDefinition action || _previewCharacter == null)
            return;

        Transform root = _previewCharacter;
        Transform anchor = ResolveAttachPoint(root);

        DrawAllHitboxPreviews(action, root, anchor);

        if (_hitboxesProp == null || _hitboxesProp.arraySize == 0)
            return;

        int index = Mathf.Clamp(_selectedHitboxIndex, 0, _hitboxesProp.arraySize - 1);
        SerializedProperty hitboxProp = _hitboxesProp.GetArrayElementAtIndex(index);
        DrawSelectedHitboxHandles(hitboxProp, anchor);
    }

    /// <summary>绘制全部 Hitbox 线框：当前帧生效高亮，选中项黄色。</summary>
    void DrawAllHitboxPreviews(ActionDefinition action, Transform root, Transform anchor)
    {
        HitboxKeyframe[] hitboxes = action.Hitboxes;
        for (int i = 0; i < hitboxes.Length; i++)
        {
            HitboxKeyframe hitbox = hitboxes[i];
            if (hitbox == null)
                continue;

            bool isActive = hitbox.IsActiveAtFrame(_previewFrame);
            bool isSelected = i == _selectedHitboxIndex;
            Color color = isSelected
                ? new Color(1f, 0.85f, 0.1f, 1f)
                : isActive
                    ? new Color(1f, 0.35f, 0.15f, 0.95f)
                    : new Color(0.6f, 0.6f, 0.6f, 0.35f);

            HitboxOrientedBox box = HitboxMath.BuildFromHitbox(root, anchor, hitbox);
            HitboxSceneDrawing.DrawWireOrientedBox(box, color);
        }
    }

    /// <summary>为选中的 Hitbox 绘制 Position / Rotation / Scale Handles。</summary>
    void DrawSelectedHitboxHandles(SerializedProperty hitboxProp, Transform anchor)
    {
        SerializedProperty offsetProp = hitboxProp.FindPropertyRelative("localOffset");
        SerializedProperty eulerProp = hitboxProp.FindPropertyRelative("localEulerAngles");
        SerializedProperty sizeProp = hitboxProp.FindPropertyRelative("size");

        if (offsetProp == null || eulerProp == null || sizeProp == null)
            return;

        Quaternion localRotation = Quaternion.Euler(eulerProp.vector3Value);
        Vector3 worldCenter = anchor.TransformPoint(offsetProp.vector3Value);
        Quaternion worldRotation = anchor.rotation * localRotation;
        float handleSize = HandleUtility.GetHandleSize(worldCenter);

        EditorGUI.BeginChangeCheck();

        Quaternion newWorldRotation = Handles.RotationHandle(worldRotation, worldCenter);
        Vector3 newWorldCenter = Handles.PositionHandle(worldCenter, newWorldRotation);
        Vector3 newSize = Handles.ScaleHandle(
            sizeProp.vector3Value,
            newWorldCenter,
            newWorldRotation,
            handleSize);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(target, "Edit Hitbox");

            offsetProp.vector3Value = anchor.InverseTransformPoint(newWorldCenter);
            eulerProp.vector3Value = (Quaternion.Inverse(anchor.rotation) * newWorldRotation).eulerAngles;
            sizeProp.vector3Value = Vector3.Max(newSize, Vector3.one * 0.01f);

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }
    }

    /// <summary>优先使用 Preview Character 上 HitBoxSystem 的 attachPoint。</summary>
    static Transform ResolveAttachPoint(Transform root)
    {
        if (root == null)
            return null;

        HitBoxSystem hitBoxSystem = root.GetComponent<HitBoxSystem>();
        if (hitBoxSystem != null)
        {
            Transform attach = hitBoxSystem.AttachPoint;
            if (attach != null)
                return attach;
        }

        return root;
    }

    void SavePreviewCharacter()
    {
        if (_previewCharacter == null)
        {
            EditorPrefs.DeleteKey(PreviewCharacterPrefKey);
            return;
        }

        GlobalObjectId id = GlobalObjectId.GetGlobalObjectIdSlow(_previewCharacter);
        EditorPrefs.SetString(PreviewCharacterPrefKey, id.ToString());
    }

    void RestorePreviewCharacter()
    {
        if (!EditorPrefs.HasKey(PreviewCharacterPrefKey))
            return;

        string idString = EditorPrefs.GetString(PreviewCharacterPrefKey);
        if (!GlobalObjectId.TryParse(idString, out GlobalObjectId globalId))
            return;

        Object obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId);
        _previewCharacter = obj as Transform;
    }
}
