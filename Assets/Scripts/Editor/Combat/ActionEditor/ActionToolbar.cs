using UnityEditor;
using UnityEngine;

/// <summary>统一播放控制；速度只改变预览时钟，不改写动作数据。</summary>
public sealed class ActionToolbar
{
    /// <summary>手动定位会暂停；帧始终为整数。</summary>
    public bool Draw(ActionDefinition action, ref int frame, ref bool playing, ref bool loop, ref float speed)
    {
        bool stopped = false;
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        using (new EditorGUI.DisabledScope(action == null || EditorApplication.isPlayingOrWillChangePlaymode))
        {
            int last = action != null ? Mathf.Max(0, action.TotalFrames - 1) : 0;
            if (GUILayout.Button("|◀", EditorStyles.toolbarButton, GUILayout.Width(28))) { frame = 0; playing = false; }
            if (GUILayout.Button("◀", EditorStyles.toolbarButton, GUILayout.Width(28))) { frame = Mathf.Max(0, frame - 1); playing = false; }
            if (GUILayout.Button(playing ? "暂停" : "播放", EditorStyles.toolbarButton, GUILayout.Width(42))) playing = !playing;
            if (GUILayout.Button("停止", EditorStyles.toolbarButton, GUILayout.Width(42))) { playing = false; frame = 0; stopped = true; }
            if (GUILayout.Button("▶", EditorStyles.toolbarButton, GUILayout.Width(28))) { frame = Mathf.Min(last, frame + 1); playing = false; }
            if (GUILayout.Button("▶|", EditorStyles.toolbarButton, GUILayout.Width(28))) { frame = last; playing = false; }
            EditorGUI.BeginChangeCheck();
            frame = EditorGUILayout.IntSlider(frame, 0, last, GUILayout.MinWidth(180));
            if (EditorGUI.EndChangeCheck()) playing = false;
            GUILayout.Label($"{frame / (float)(action != null ? Mathf.Max(1, action.SampleRate) : 60):0.000}s", GUILayout.Width(65));
            loop = GUILayout.Toggle(loop, "循环", EditorStyles.toolbarButton, GUILayout.Width(42));
            float[] speeds = { .25f, .5f, 1, 1.5f, 2 };
            int index = Mathf.Max(0, System.Array.IndexOf(speeds, speed));
            speed = speeds[EditorGUILayout.Popup(index, new[] { "0.25×", "0.5×", "1×", "1.5×", "2×" }, EditorStyles.toolbarPopup, GUILayout.Width(60))];
        }
        return stopped;
    }
}
