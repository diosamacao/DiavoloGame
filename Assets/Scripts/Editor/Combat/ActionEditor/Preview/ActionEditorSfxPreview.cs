using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>显式试听选中的原始音频；不随拖帧触发，不创建场景音源。</summary>
public sealed class ActionEditorSfxPreview : IDisposable
{
    // Unity 2022.3 的音频资产预览接口未公开；不可用时禁用试听并显示原因。
    static readonly Type AudioUtil = typeof(Editor).Assembly.GetType("UnityEditor.AudioUtil");
    static readonly MethodInfo Play = AudioUtil?.GetMethod("PlayPreviewClip", BindingFlags.Static | BindingFlags.Public,
        null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
    static readonly MethodInfo Stop = AudioUtil?.GetMethod("StopAllPreviewClips", BindingFlags.Static | BindingFlags.Public);
    bool ownsPlayback;
    bool muted = true;

    /// <summary>默认静音，明确点击才试听；音量/音调配置仍在游戏音频路径验收。</summary>
    public void Draw(ActionEditorSelectionSet selection)
    {
        bool next = GUILayout.Toggle(muted, "静音", EditorStyles.miniButton, GUILayout.Width(45));
        if (next != muted) { muted = next; if (muted) Dispose(); }
        var item = selection.Primary;
        AudioClip clip = item.IsValid && item.Kind == ActionTimelineTrackKind.Sfx
            ? item.ElementProperty.FindPropertyRelative("audioClip")?.objectReferenceValue as AudioClip : null;
        using (new EditorGUI.DisabledScope(muted || clip == null || Play == null || Stop == null))
            if (GUILayout.Button(new GUIContent("试听", "选中 SFX 后原速试听源音频，不模拟音量/音调。拖帧不会重复触发。"), EditorStyles.miniButton, GUILayout.Width(45)))
            {
                Dispose();
                Play.Invoke(null, new object[] { clip, 0, false });
                ownsPlayback = true;
            }
        if (ownsPlayback && GUILayout.Button("停止试听", EditorStyles.miniButton, GUILayout.Width(65))) Dispose();
        if (Play == null || Stop == null) GUILayout.Label("当前 Unity 不支持音频预览接口", EditorStyles.miniLabel);
    }

    /// <summary>只在本窗口启动过试听时停止编辑器音频；模型/动作切换和关闭调用。</summary>
    public void Dispose()
    {
        if (ownsPlayback) Stop?.Invoke(null, null);
        ownsPlayback = false;
    }
}
