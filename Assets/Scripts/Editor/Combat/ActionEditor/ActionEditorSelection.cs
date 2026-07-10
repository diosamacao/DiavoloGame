using UnityEditor;

/// <summary>时间轴上选中的窗口引用（数组属性 + 下标）。</summary>
public readonly struct ActionEditorSelection
{
    public ActionEditorSelection(SerializedProperty arrayProperty, int index, ActionTimelineTrackKind kind)
    {
        ArrayProperty = arrayProperty;
        Index = index;
        Kind = kind;
    }

    public SerializedProperty ArrayProperty { get; }
    public int Index { get; }
    public ActionTimelineTrackKind Kind { get; }

    public bool IsValid =>
        ArrayProperty != null
        && Index >= 0
        && Index < ArrayProperty.arraySize;

    /// <summary>选中窗口的 SerializedProperty；无效时返回 null。</summary>
    public SerializedProperty ElementProperty =>
        IsValid ? ArrayProperty.GetArrayElementAtIndex(Index) : null;

    /// <summary>
    /// 按 Kind + Index 比较。SerializedProperty 每帧 FindProperty 都是新对象，不能用引用相等。
    /// </summary>
    public bool Equals(ActionEditorSelection other) =>
        Kind == other.Kind && Index == other.Index;

    public override bool Equals(object obj) =>
        obj is ActionEditorSelection other && Equals(other);

    public override int GetHashCode() => ((int)Kind * 397) ^ Index;
}
