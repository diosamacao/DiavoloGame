using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Action Editor 时间轴窗口剪贴板：支持多选复制，并在切换 Action 后粘贴。
/// 不复制 Animation 段；Object 引用按资产 GUID 还原。
/// </summary>
public static class ActionTimelineClipboard
{
    [Serializable]
    sealed class Payload
    {
        public List<Entry> entries = new();
        public int anchorStartFrame;
    }

    [Serializable]
    sealed class Entry
    {
        public ActionTimelineTrackKind kind;
        public int startFrame;
        public List<Field> fields = new();
    }

    [Serializable]
    sealed class Field
    {
        public string path;
        public int propertyType;
        public bool boolValue;
        public int intValue;
        public float floatValue;
        public string stringValue;
        public string objectGuid;
        public int objectInstanceId;
        public float[] floats;
    }

    /// <summary>AnimationCurve 的 Json 载体，保留 Key 与首尾 WrapMode。</summary>
    [Serializable]
    sealed class CurvePayload
    {
        public Keyframe[] keys;
        public WrapMode preWrapMode;
        public WrapMode postWrapMode;
    }

    static string s_json = string.Empty;

    /// <summary>剪贴板是否有可粘贴内容。</summary>
    public static bool HasData => !string.IsNullOrEmpty(s_json);

    /// <summary>从当前选中窗口采集剪贴板；忽略 Animation 与无效项。返回复制条数。</summary>
    public static int Copy(SerializedObject so, ActionEditorSelectionSet selection)
    {
        if (so == null || selection == null || !selection.HasSelection)
            return 0;

        var payload = new Payload();
        int minStart = int.MaxValue;

        for (int i = 0; i < selection.Items.Count; i++)
        {
            ActionEditorSelection item = selection.Items[i];
            if (!item.IsValid || item.Kind == ActionTimelineTrackKind.Animation)
                continue;

            SerializedProperty element = item.ElementProperty;
            if (element == null)
                continue;

            var entry = new Entry
            {
                kind = item.Kind,
                startFrame = element.FindPropertyRelative("startFrame")?.intValue ?? 0,
            };
            CaptureFields(element, entry.fields);
            payload.entries.Add(entry);
            minStart = Mathf.Min(minStart, entry.startFrame);
        }

        if (payload.entries.Count == 0)
            return 0;

        payload.anchorStartFrame = minStart == int.MaxValue ? 0 : minStart;
        s_json = JsonUtility.ToJson(payload);
        return payload.entries.Count;
    }

    /// <summary>
    /// 粘贴到目标 Action：按 previewFrame 对齐最早窗口起点，自动补齐同名轨道。
    /// 返回新粘贴项的选中集合。
    /// </summary>
    public static ActionEditorSelectionSet Paste(
        SerializedObject so,
        ActionDefinition action,
        int previewFrame)
    {
        var result = new ActionEditorSelectionSet();
        if (so == null || action == null || !HasData)
            return result;

        Payload payload;
        try
        {
            payload = JsonUtility.FromJson<Payload>(s_json);
        }
        catch (ArgumentException)
        {
            return result;
        }

        if (payload?.entries == null || payload.entries.Count == 0)
            return result;

        int maxFrame = Mathf.Max(0, action.TotalFrames - 1);
        int frameDelta = Mathf.Clamp(previewFrame, 0, maxFrame) - payload.anchorStartFrame;
        Undo.RecordObject(so.targetObject, "Paste Action Windows");

        bool cancelSkipped = false;
        var pasted = new List<(ActionTimelineTrackKind kind, int index)>();

        for (int i = 0; i < payload.entries.Count; i++)
        {
            Entry entry = payload.entries[i];
            if (entry.kind == ActionTimelineTrackKind.Animation)
                continue;

            if (entry.kind == ActionTimelineTrackKind.Cancel
                && !CanPasteCancelWindow(so, entry))
            {
                cancelSkipped = true;
                continue;
            }

            string arrayName = ActionTimelineCommands.GetArrayPropertyName(entry.kind);
            if (arrayName == null)
                continue;

            SerializedProperty arrayProp = so.FindProperty($"timeline.{arrayName}");
            if (arrayProp == null)
                continue;

            string trackName = FindFieldString(entry.fields, "trackName") ?? "Default";
            ActionTimelineCommands.EnsureTrack(so, entry.kind, trackName);

            int index = arrayProp.arraySize;
            arrayProp.arraySize++;
            SerializedProperty element = arrayProp.GetArrayElementAtIndex(index);
            ApplyFields(element, entry.fields);
            ShiftFrames(element, frameDelta, maxFrame, ActionEditorStyles.IsPointEventTrack(entry.kind));
            MakeUniqueId(element, arrayProp, index);
            pasted.Add((entry.kind, index));
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(so.targetObject);

        for (int i = 0; i < pasted.Count; i++)
        {
            string arrayName = ActionTimelineCommands.GetArrayPropertyName(pasted[i].kind);
            SerializedProperty arrayProp = arrayName != null
                ? so.FindProperty($"timeline.{arrayName}")
                : null;
            if (arrayProp == null)
                continue;

            var sel = new ActionEditorSelection(arrayProp, pasted[i].index, pasted[i].kind);
            result.AddRange(new[] { sel }, sel);
        }

        if (cancelSkipped)
        {
            EditorUtility.DisplayDialog(
                "Paste",
                "部分 Cancel 窗口未粘贴：每个 Action 最多各 1 个 Normal / Perfect。",
                "OK");
        }

        return result;
    }

    static void CaptureFields(SerializedProperty element, List<Field> fields)
    {
        SerializedProperty iterator = element.Copy();
        SerializedProperty end = iterator.GetEndProperty();
        bool enterChildren = true;
        // Spline 的 TangentMode 位于 HideInInspector MetaData，必须遍历全部序列化字段。
        while (iterator.Next(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
        {
            enterChildren = false;
            string path = iterator.propertyPath;
            string prefix = element.propertyPath + ".";
            if (!path.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            string relative = path.Substring(prefix.Length);
            var field = new Field
            {
                path = relative,
                propertyType = (int)iterator.propertyType,
            };

            switch (iterator.propertyType)
            {
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.LayerMask:
                    field.intValue = iterator.intValue;
                    break;
                case SerializedPropertyType.Boolean:
                    field.boolValue = iterator.boolValue;
                    break;
                case SerializedPropertyType.Float:
                    field.floatValue = iterator.floatValue;
                    break;
                case SerializedPropertyType.String:
                    field.stringValue = iterator.stringValue;
                    break;
                case SerializedPropertyType.Enum:
                    field.intValue = iterator.enumValueIndex;
                    break;
                case SerializedPropertyType.ObjectReference:
                    CaptureObjectReference(iterator, field);
                    break;
                case SerializedPropertyType.Vector2:
                    field.floats = new[] { iterator.vector2Value.x, iterator.vector2Value.y };
                    break;
                case SerializedPropertyType.Vector3:
                    field.floats = new[]
                    {
                        iterator.vector3Value.x, iterator.vector3Value.y, iterator.vector3Value.z,
                    };
                    break;
                case SerializedPropertyType.Vector4:
                    field.floats = new[]
                    {
                        iterator.vector4Value.x, iterator.vector4Value.y,
                        iterator.vector4Value.z, iterator.vector4Value.w,
                    };
                    break;
                case SerializedPropertyType.Quaternion:
                    Quaternion quaternion = iterator.quaternionValue;
                    field.floats = new[] { quaternion.x, quaternion.y, quaternion.z, quaternion.w };
                    break;
                case SerializedPropertyType.Color:
                    field.floats = new[]
                    {
                        iterator.colorValue.r, iterator.colorValue.g,
                        iterator.colorValue.b, iterator.colorValue.a,
                    };
                    break;
                case SerializedPropertyType.AnimationCurve:
                    AnimationCurve curve = iterator.animationCurveValue;
                    field.stringValue = JsonUtility.ToJson(new CurvePayload
                    {
                        keys = curve?.keys ?? Array.Empty<Keyframe>(),
                        preWrapMode = curve?.preWrapMode ?? WrapMode.Clamp,
                        postWrapMode = curve?.postWrapMode ?? WrapMode.Clamp,
                    });
                    break;
                case SerializedPropertyType.Generic:
                    enterChildren = true;
                    continue;
                default:
                    continue;
            }

            fields.Add(field);
        }
    }

    static void CaptureObjectReference(SerializedProperty prop, Field field)
    {
        UnityEngine.Object obj = prop.objectReferenceValue;
        if (obj == null)
            return;

        field.objectInstanceId = obj.GetInstanceID();
        string path = AssetDatabase.GetAssetPath(obj);
        if (!string.IsNullOrEmpty(path))
            field.objectGuid = AssetDatabase.AssetPathToGUID(path);
    }

    static void ApplyFields(SerializedProperty element, List<Field> fields)
    {
        if (fields == null)
            return;

        for (int i = 0; i < fields.Count; i++)
        {
            Field field = fields[i];
            if (string.IsNullOrEmpty(field.path))
                continue;

            SerializedProperty prop = element.FindPropertyRelative(field.path);
            if (prop == null)
                continue;

            switch ((SerializedPropertyType)field.propertyType)
            {
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.LayerMask:
                    prop.intValue = field.intValue;
                    break;
                case SerializedPropertyType.Boolean:
                    prop.boolValue = field.boolValue;
                    break;
                case SerializedPropertyType.Float:
                    prop.floatValue = field.floatValue;
                    break;
                case SerializedPropertyType.String:
                    prop.stringValue = field.stringValue ?? string.Empty;
                    break;
                case SerializedPropertyType.Enum:
                    prop.enumValueIndex = field.intValue;
                    break;
                case SerializedPropertyType.ObjectReference:
                    prop.objectReferenceValue = ResolveObjectReference(field);
                    break;
                case SerializedPropertyType.Vector2 when field.floats is { Length: >= 2 }:
                    prop.vector2Value = new Vector2(field.floats[0], field.floats[1]);
                    break;
                case SerializedPropertyType.Vector3 when field.floats is { Length: >= 3 }:
                    prop.vector3Value = new Vector3(field.floats[0], field.floats[1], field.floats[2]);
                    break;
                case SerializedPropertyType.Vector4 when field.floats is { Length: >= 4 }:
                    prop.vector4Value = new Vector4(
                        field.floats[0], field.floats[1], field.floats[2], field.floats[3]);
                    break;
                case SerializedPropertyType.Quaternion when field.floats is { Length: >= 4 }:
                    prop.quaternionValue = new Quaternion(
                        field.floats[0], field.floats[1], field.floats[2], field.floats[3]);
                    break;
                case SerializedPropertyType.Color when field.floats is { Length: >= 4 }:
                    prop.colorValue = new Color(
                        field.floats[0], field.floats[1], field.floats[2], field.floats[3]);
                    break;
                case SerializedPropertyType.AnimationCurve:
                    CurvePayload curve = JsonUtility.FromJson<CurvePayload>(field.stringValue);
                    if (curve != null)
                    {
                        prop.animationCurveValue = new AnimationCurve(curve.keys ?? Array.Empty<Keyframe>())
                        {
                            preWrapMode = curve.preWrapMode,
                            postWrapMode = curve.postWrapMode,
                        };
                    }
                    break;
            }
        }
    }

    static UnityEngine.Object ResolveObjectReference(Field field)
    {
        if (!string.IsNullOrEmpty(field.objectGuid))
        {
            string path = AssetDatabase.GUIDToAssetPath(field.objectGuid);
            if (!string.IsNullOrEmpty(path))
            {
                UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                if (asset != null)
                    return asset;
            }
        }

        return field.objectInstanceId != 0
            ? EditorUtility.InstanceIDToObject(field.objectInstanceId)
            : null;
    }

    static string FindFieldString(List<Field> fields, string path)
    {
        for (int i = 0; i < fields.Count; i++)
        {
            if (fields[i].path == path)
                return fields[i].stringValue;
        }

        return null;
    }

    static void ShiftFrames(SerializedProperty element, int delta, int maxFrame, bool pointEvent)
    {
        SerializedProperty startProp = element.FindPropertyRelative("startFrame");
        SerializedProperty endProp = element.FindPropertyRelative("endFrame");
        if (startProp == null || endProp == null)
            return;

        int start = startProp.intValue + delta;
        int end = endProp.intValue + delta;
        int length = Mathf.Max(0, end - start);
        start = Mathf.Clamp(start, 0, maxFrame);
        end = pointEvent ? start : Mathf.Clamp(start + length, start, maxFrame);
        startProp.intValue = start;
        endProp.intValue = end;
    }

    static void MakeUniqueId(SerializedProperty element, SerializedProperty arrayProp, int selfIndex)
    {
        SerializedProperty idProp = element.FindPropertyRelative("id");
        if (idProp == null)
            return;

        string baseId = string.IsNullOrEmpty(idProp.stringValue) ? "window" : idProp.stringValue;
        string candidate = baseId;
        int suffix = 2;
        while (IdExists(arrayProp, candidate, selfIndex))
        {
            candidate = $"{baseId}_{suffix}";
            suffix++;
        }

        idProp.stringValue = candidate;
    }

    static bool IdExists(SerializedProperty arrayProp, string id, int exceptIndex)
    {
        for (int i = 0; i < arrayProp.arraySize; i++)
        {
            if (i == exceptIndex)
                continue;

            SerializedProperty idProp = arrayProp.GetArrayElementAtIndex(i).FindPropertyRelative("id");
            if (idProp != null && idProp.stringValue == id)
                return true;
        }

        return false;
    }

    static bool CanPasteCancelWindow(SerializedObject so, Entry entry)
    {
        SerializedProperty arrayProp = so.FindProperty("timeline.cancelWindowStates");
        if (arrayProp == null || arrayProp.arraySize >= 2)
            return false;

        int windowType = 0;
        for (int i = 0; i < entry.fields.Count; i++)
        {
            if (entry.fields[i].path == "windowType")
            {
                windowType = entry.fields[i].intValue;
                break;
            }
        }

        for (int i = 0; i < arrayProp.arraySize; i++)
        {
            SerializedProperty typeProp = arrayProp.GetArrayElementAtIndex(i).FindPropertyRelative("windowType");
            if (typeProp != null && typeProp.enumValueIndex == windowType)
                return false;
        }

        return true;
    }
}
